using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    /// Puño cerrado calculado (pasos 1-3 + verificación) sobre el rig FPS ProtagonistV01, en modo edición.
    /// 1. Pone los dedos en la bind pose real (bindposes de la malla Hand_X_FPS).
    /// 2. normalPalma = cross(Index1 - Little1, Hand→Middle1), con el signo comprobado (los dedos de la bind ya están
    ///    medio doblados hacia la palma). Por falange: ejeFlexión = cross(dirDedo, normalPalma) en espacio local y
    ///    prueba de signo (+15° acerca la punta al centro de la palma).
    /// 3. Flexión TOTAL desde el dedo recto (se resta lo que la bind ya trae doblado): X1 85/90/92/95, X2 102, X3 65.
    /// 4. Pulgar por fuera: Thumb1 oposición + hacia el índice, Thumb2/3 flexión; búsqueda en rejilla dentro de los
    ///    rangos pedidos para que la yema quede a 0-3 mm de la falange media de índice/medio sin penetrar.
    /// 5. Medidas con cápsulas sacadas de la malla (radio y largo de cada falange según sus vértices dominantes).
    /// Guarda la pose cerrada (rotaciones locales + ejes + ángulos) en el HandFistRig de cada mano, captura 4 vistas
    /// y escribe Tools/IronRequiem/Reports/HangarIntro/fist_bake.txt. Deja los huesos como estaban.
    public static class FistPoseBake
    {
        const string ReportFile = "Tools/IronRequiem/Reports/HangarIntro/fist_bake.txt";
        const string ShotDir = "Tools/IronRequiem/Bridge/v8";
        static readonly string[] FingerNames = { "Index", "Middle", "Ring", "Little" };
        static readonly float[] X1Target = { 85, 90, 92, 95 };
        const float X2Target = 102, X3Target = 65;

        class Bone { public Transform t; public Vector3 dirLocal; public float radius, length; public Vector3 axisLocal; public bool signOk; public float bindFlex, target, finalFlex; }
        class HandData
        {
            public string side; public Transform hand; public SkinnedMeshRenderer skin; public Bone[][] fingers = new Bone[4][]; public Bone[] thumb = new Bone[3];
            public Vector3 N, D, L; public float palmSurface; public Vector3 palmCenter; public Dictionary<Transform, Quaternion> bind = new Dictionary<Transform, Quaternion>();
            public float thumbA, thumbB, thumbC, thumbD, thumbClear; public bool thumbInRange;
            public int[] dominant; public Mesh baked; public Vector3[] pos; public Vector3[] nrm; public int handBone;
            public Dictionary<Transform, int[]> verts = new Dictionary<Transform, int[]>(); public int[] palm;
            public float[,] fingerGap = new float[4, 1]; public bool[] fingerInRange = new bool[4];
        }

        [MenuItem("Tools/Iron Requiem/Fist Pose/4. Calcular puño (pasos 1-3) + verificar")]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[FistBake] solo en modo edición"); return; }
            var hands = Object.FindObjectsByType<ProtagonistHandsPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (!hands) { Debug.LogError("[FistBake] abre RequiemCampaign"); return; }
            var sb = new StringBuilder(); sb.AppendLine("Fist bake " + System.DateTime.Now + " — rig " + hands.name + " (ProtagonistV01 FPS)");
            var root = hands.FpsRoot; bool rootActive = root.gameObject.activeSelf; root.gameObject.SetActive(true);
            var views = root.GetComponentsInChildren<Renderer>(true); var viewsOn = views.Select(r => r.enabled).ToArray();
            var all = hands.GetComponentsInChildren<Transform>(true).Concat(root.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();
            var saved = all.ToDictionary(t => t, t => (t.localPosition, t.localRotation));
            try
            {
                foreach (var r in views) r.enabled = r.name.StartsWith("Hand_") || r.name.StartsWith("Forearm_") && !r.name.Contains("Receiver");
                Directory.CreateDirectory(ShotDir);
                foreach (bool left in new[] { true, false })
                {
                    var h = Load(hands, left, sb); if (h == null) continue;
                    ToBind(h); Shots(h, "bind");
                    Axes(h, sb); Close(h); Thumb(h); Measure(h, sb);
                    Shots(h, "closed");
                    // store: local rotations of the closed pose, plus axes and angles for reference
                    var rig = left ? hands.LeftFistRig : hands.RightFistRig; Undo.RecordObject(hands, "Puño calculado");
                    var list = h.fingers.SelectMany(f => f).Concat(h.thumb).Where(b => b != null).ToList();
                    rig.StoreBake(list.Select(b => b.t).ToList(), list.Select(b => b.axisLocal).ToArray(), list.Select(b => b.finalFlex).ToArray());
                }
                EditorUtility.SetDirty(hands); PrefabUtility.RecordPrefabInstancePropertyModifications(hands);
            }
            finally
            {
                foreach (var kv in saved) if (kv.Key) { kv.Key.localPosition = kv.Value.Item1; kv.Key.localRotation = kv.Value.Item2; }
                for (int i = 0; i < views.Length; i++) if (views[i]) views[i].enabled = viewsOn[i];
                root.gameObject.SetActive(rootActive);
            }
            EditorSceneManager.MarkSceneDirty(hands.gameObject.scene); EditorSceneManager.SaveScene(hands.gameObject.scene);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportFile)); File.WriteAllText(ReportFile, sb.ToString()); Debug.Log("[FistBake] listo → " + ReportFile);
        }

        // ---------------------------------------------------------------- data from the rig and the mesh
        static HandData Load(ProtagonistHandsPresentation hands, bool left, StringBuilder sb)
        {
            var h = new HandData { side = left ? "L" : "R", hand = left ? hands.LeftHandBone : hands.RightHand };
            h.skin = hands.FpsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => s.name == "Hand_" + h.side + "_FPS");
            if (!h.skin) { sb.AppendLine("sin malla Hand_" + h.side + "_FPS"); return null; }
            var bones = h.skin.bones; var bp = h.skin.sharedMesh.bindposes; var index = new Dictionary<Transform, int>(); for (int i = 0; i < bones.Length; i++) if (bones[i]) index[bones[i]] = i;
            foreach (var t in h.hand.GetComponentsInChildren<Transform>(true))
                if (t != h.hand && t.parent && index.TryGetValue(t, out int bi) && index.TryGetValue(t.parent, out int pi))
                    h.bind[t] = Quaternion.Inverse(bp[pi].inverse.rotation) * bp[bi].inverse.rotation;
            Transform Find(string n) => h.hand.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n + "_" + h.side);
            for (int f = 0; f < 4; f++) h.fingers[f] = new Bone[3];
            for (int f = 0; f < 4; f++) for (int k = 0; k < 3; k++) h.fingers[f][k] = new Bone { t = Find(FingerNames[f] + (k + 1)) };
            for (int k = 0; k < 3; k++) h.thumb[k] = new Bone { t = Find("Thumb" + (k + 1)) };
            foreach (var chain in h.fingers.Append(h.thumb))
                for (int k = 0; k < 3; k++)
                {
                    var b = chain[k]; if (!b.t) { sb.AppendLine("falta hueso " + k + " en " + h.side); return null; }
                    var next = k < 2 ? chain[k + 1].t : null;
                    b.dirLocal = next ? next.localPosition.normalized : chain[k - 1].dirLocal; // X3: same axis as X2 → X3
                }
            Thickness(h, bones, bp);
            return h;
        }
        // vertices grouped by their dominant bone (the skin is blended; the dominant bone decides who owns a vertex)
        static void Thickness(HandData h, Transform[] bones, Matrix4x4[] bp)
        {
            var mesh = h.skin.sharedMesh; var per = mesh.GetBonesPerVertex(); var w = mesh.GetAllBoneWeights();
            h.dominant = new int[mesh.vertexCount]; int idx = 0;
            for (int v = 0; v < h.dominant.Length; v++) { int n = per[v], best = -1; float bw = -1; for (int k = 0; k < n; k++) if (w[idx + k].weight > bw) { bw = w[idx + k].weight; best = w[idx + k].boneIndex; } idx += n; h.dominant[v] = best; }
            h.handBone = System.Array.IndexOf(bones, h.hand);
            int[] Of(Transform t) { int bi = System.Array.IndexOf(bones, t); var list = new List<int>(); for (int v = 0; v < h.dominant.Length; v++) if (h.dominant[v] == bi) list.Add(v); return list.ToArray(); }
            h.palm = Of(h.hand);
            foreach (var chain in h.fingers.Append(h.thumb)) foreach (var b in chain) { h.verts[b.t] = Of(b.t); var next = System.Array.IndexOf(chain, b) < 2 ? chain[System.Array.IndexOf(chain, b) + 1].t : null; b.length = next ? next.localPosition.magnitude : .03f; b.radius = .011f; }
            // fingertip length: furthest X3 vertex along the bone in bind (bone local space from the bindpose)
            var verts = mesh.vertices;
            foreach (var chain in h.fingers.Append(h.thumb)) { var b = chain[2]; int bi = System.Array.IndexOf(bones, b.t); var ids = h.verts[b.t]; if (ids.Length > 0) b.length = ids.Max(v => Vector3.Dot(bp[bi].MultiplyPoint3x4(verts[v]), b.dirLocal)); }
            h.baked = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        }
        // real deformed mesh (world space) for measuring and for the captures
        static void Bake(HandData h)
        {
            h.skin.BakeMesh(h.baked, true); var m = Matrix4x4.TRS(h.skin.transform.position, h.skin.transform.rotation, Vector3.one);
            var v = h.baked.vertices; var n = h.baked.normals; if (h.pos == null || h.pos.Length != v.Length) { h.pos = new Vector3[v.Length]; h.nrm = new Vector3[v.Length]; }
            for (int i = 0; i < v.Length; i++) { h.pos[i] = m.MultiplyPoint3x4(v[i]); h.nrm[i] = m.MultiplyVector(n[i]); }
        }
        // signed gap (metres) from set A to set B: nearest B vertex, negative when the A vertex is behind B's surface
        static float Gap(HandData h, int[] a, int[] b)
        {
            float best = float.MaxValue;
            foreach (int i in a)
            {
                float near = float.MaxValue; int nj = -1; var p = h.pos[i];
                foreach (int j in b) { float d = (p - h.pos[j]).sqrMagnitude; if (d < near) { near = d; nj = j; } }
                if (nj < 0) continue; float signedD = Mathf.Sqrt(near) * (Vector3.Dot(p - h.pos[nj], h.nrm[nj]) < 0 ? -1 : 1);
                if (signedD < best) best = signedD;
            }
            return best;
        }
        static int[] Join(HandData h, params Transform[] bones) => bones.SelectMany(b => h.verts[b]).ToArray();

        static void ToBind(HandData h) { foreach (var kv in h.bind) kv.Key.localRotation = kv.Value; }
        static Vector3 Dir(Bone b) => b.t.rotation * b.dirLocal;
        static Vector3 Tip(Bone[] chain) => chain[2].t.position + Dir(chain[2]) * chain[2].length;
        static Vector3 SegEnd(Bone[] chain, int k) => k < 2 ? chain[k + 1].t.position : Tip(chain);

        // ---------------------------------------------------------------- paso 1: palma y ejes con prueba de signo
        static void Axes(HandData h, StringBuilder sb)
        {
            Vector3 index1 = h.fingers[0][0].t.position, little1 = h.fingers[3][0].t.position, middle1 = h.fingers[1][0].t.position;
            h.L = (index1 - little1).normalized; h.D = (middle1 - h.hand.position).normalized;
            h.N = Vector3.Cross(h.L, h.D).normalized;
            // signo: en la bind los dedos ya se doblan hacia la palma → la punta del medio está del lado +N de la línea del nudillo
            float side = Vector3.Dot(Tip(h.fingers[1]) - middle1, h.N); bool flipped = side < 0; if (flipped) h.N = -h.N;
            var nLocal = Quaternion.Inverse(h.hand.rotation) * h.N;
            Bake(h); Vector3 knuckles = (index1 + little1) * .5f;
            // palm surface: palm vertices on the palm side, near the finger bases
            var palmSide = h.palm.Where(v => Vector3.Dot(h.nrm[v], h.N) > .5f).ToArray(); if (palmSide.Length == 0) palmSide = h.palm;
            h.palmCenter = palmSide.Select(v => h.pos[v]).Aggregate(Vector3.zero, (x, y) => x + y) / palmSide.Length;
            h.palmSurface = Vector3.Dot(h.palmCenter - h.hand.position, h.N);
            sb.AppendLine($"\n== Mano {h.side}: normalPalma=cross(Index1-Little1, Hand→Middle1) {(flipped ? "invertida (apuntaba al dorso)" : "correcta")} N(local mano)={V(nLocal)} superficie palma={h.palmSurface * 1000:0.0}mm");
            for (int f = 0; f < 4; f++)
                for (int k = 0; k < 3; k++)
                {
                    var b = h.fingers[f][k]; var dir = Dir(b);
                    var axisWorld = Vector3.Cross(dir, h.N).normalized; b.axisLocal = Quaternion.Inverse(b.t.rotation) * axisWorld;
                    // prueba de signo: +15° sobre el eje debe acercar la punta del dedo al centro de la palma
                    var keep = b.t.localRotation; float before = Vector3.Distance(Tip(h.fingers[f]), h.palmCenter);
                    b.t.localRotation = keep * Quaternion.AngleAxis(15, b.axisLocal); float after = Vector3.Distance(Tip(h.fingers[f]), h.palmCenter); b.t.localRotation = keep;
                    b.signOk = after < before; if (!b.signOk) b.axisLocal = -b.axisLocal;
                    // lo que la bind ya trae doblado, respecto del hueso padre (X1: respecto de Hand→Middle1)
                    Vector3 parentDir = k == 0 ? h.D : Dir(h.fingers[f][k - 1]);
                    b.bindFlex = Vector3.SignedAngle(parentDir, dir, b.t.rotation * b.axisLocal);
                    b.target = k == 0 ? X1Target[f] : k == 1 ? X2Target : X3Target;
                }
        }
        // ---------------------------------------------------------------- paso 2: flexión solo sobre el eje
        static void Close(HandData h)
        {
            var bindRot = h.fingers.Select(c => c.Select(b => b.t.localRotation).ToArray()).ToArray();
            void Set(int f, float x1, float x2, float x3) { var c = h.fingers[f]; float[] a = { x1, x2, x3 }; for (int k = 0; k < 3; k++) c[k].t.localRotation = bindRot[f][k] * Quaternion.AngleAxis(a[k] - c[k].bindFlex, c[k].axisLocal); }
            // start: every finger at its target, then each finger searched within the asked ranges (tip 0-5 mm from the palm)
            for (int f = 0; f < 4; f++) Set(f, X1Target[f], X2Target, X3Target);
            for (int f = 0; f < 4; f++)
            {
                var tips = h.verts[h.fingers[f][2].t]; float best = float.MaxValue; (float, float, float) pick = (X1Target[f], X2Target, X3Target); float gap = 0; bool inRange = false;
                foreach (bool wide in new[] { false, true })
                {
                    if (wide && best < .0035f) break;
                    float x1a = X1Target[f] - (wide ? 0 : 5), x1b = X1Target[f] + (wide ? 30 : 5), x2a = wide ? 95 : 100, x2b = wide ? 125 : 105, x3a = wide ? 40 : 60, x3b = wide ? 100 : 70, st = wide ? 5 : 2.5f;
                    for (float x1 = x1a; x1 <= x1b; x1 += st) for (float x2 = x2a; x2 <= x2b; x2 += st) for (float x3 = x3a; x3 <= x3b; x3 += st)
                    {
                        Set(f, x1, x2, x3); Bake(h); float g = Gap(h, tips, h.palm);
                        float score = Mathf.Abs(g - .0025f) + (g < 0 ? 10 * -g : 0);
                        if (score < best) { best = score; pick = (x1, x2, x3); gap = g; inRange = !wide; }
                    }
                }
                Set(f, pick.Item1, pick.Item2, pick.Item3); h.fingerInRange[f] = inRange; h.fingerGap[f, 0] = gap;
            }
            // convergencia: como mucho 6° de aducción hacia el medio sobre normalPalma
            var mid = Dir(h.fingers[1][1]);
            foreach (int f in new[] { 0, 2, 3 })
            {
                var b = h.fingers[f][0]; var dir = Vector3.ProjectOnPlane(Dir(h.fingers[f][1]), h.N); float spread = Vector3.SignedAngle(dir, Vector3.ProjectOnPlane(mid, h.N), h.N);
                if (Mathf.Abs(spread) > 1f) b.t.rotation = Quaternion.AngleAxis(Mathf.Clamp(spread, -6f, 6f), h.N) * b.t.rotation;
            }
            for (int f = 0; f < 4; f++) for (int k = 0; k < 3; k++) { var b = h.fingers[f][k]; b.finalFlex = Vector3.SignedAngle(k == 0 ? h.D : Dir(h.fingers[f][k - 1]), Dir(b), b.t.rotation * b.axisLocal); }
            Bake(h);
        }
        // ---------------------------------------------------------------- paso 3: pulgar por fuera
        static void Thumb(HandData h)
        {
            var t = h.thumb; var fixedRot = t.Select(b => b.t.localRotation).ToArray();
            Bone index2 = h.fingers[0][1], middle2 = h.fingers[1][1];
            // ejes (con la misma prueba de signo, contra el objetivo)
            Vector3 target = (index2.t.position + SegEnd(h.fingers[0], 1) + middle2.t.position + SegEnd(h.fingers[1], 1)) * .25f;
            Vector3 Axis(Bone b, Vector3 world, Vector3 goal)
            {
                var local = Quaternion.Inverse(b.t.rotation) * world.normalized; var keep = b.t.localRotation; float d0 = Vector3.Distance(Tip(t), goal);
                b.t.localRotation = keep * Quaternion.AngleAxis(15, local); float d1 = Vector3.Distance(Tip(t), goal); b.t.localRotation = keep; return d1 < d0 ? local : -local;
            }
            var opp = Axis(t[0], Vector3.Cross(Dir(t[0]), h.N), h.palmCenter); var toIndex = Axis(t[0], h.N, h.fingers[0][0].t.position);
            var flex2 = Axis(t[1], Vector3.Cross(Dir(t[1]), target - t[1].t.position), target); var flex3 = Axis(t[2], Vector3.Cross(Dir(t[2]), target - t[2].t.position), target);
            t[0].axisLocal = opp; t[1].axisLocal = flex2; t[2].axisLocal = flex3;
            var fingerCaps = new List<(Vector3 a, Vector3 b, float r)>();
            for (int f = 0; f < 4; f++) for (int k = 0; k < 3; k++) fingerCaps.Add((h.fingers[f][k].t.position, SegEnd(h.fingers[f], k), h.fingers[f][k].radius));
            var padVerts = h.verts[t[2].t]; var midVerts = Join(h, index2.t, middle2.t); var thumbVerts = Join(h, t[1].t, t[2].t);
            var fingerVerts = h.fingers.SelectMany(c => c).SelectMany(b => h.verts[b.t]).ToArray();
            float best = float.MaxValue; (float a, float b, float c, float d) pick = (40, 25, 38, 28);
            foreach (bool wide in new[] { false, true })
            {
                if (best < .003f) break;
                float a0 = wide ? 15 : 35, a1 = wide ? 75 : 45, b0 = wide ? 0 : 20, b1 = wide ? 50 : 30, c0 = wide ? 10 : 30, c1 = wide ? 75 : 45, d0 = wide ? 0 : 20, d1 = wide ? 70 : 35, step = wide ? 7.5f : 5f;
                for (float a = a0; a <= a1; a += step) for (float b = b0; b <= b1; b += step) for (float c = c0; c <= c1; c += step) for (float d = d0; d <= d1; d += step)
                {
                    t[0].t.localRotation = fixedRot[0] * Quaternion.AngleAxis(a, opp) * Quaternion.AngleAxis(b, toIndex); t[1].t.localRotation = fixedRot[1] * Quaternion.AngleAxis(c, flex2); t[2].t.localRotation = fixedRot[2] * Quaternion.AngleAxis(d, flex3);
                    // cheap capsule pre-check, then the real mesh only for promising candidates
                    float pen = 0; for (int k = 1; k < 3; k++) foreach (var cap in fingerCaps) pen += Mathf.Max(0, (t[k].radius + cap.r) - SegSeg(t[k].t.position, SegEnd(t, k), cap.a, cap.b) - .004f);
                    if (pen > .02f) continue;
                    Bake(h); float clear = Gap(h, padVerts, midVerts); if (Mathf.Abs(clear - .0015f) + (clear < 0 ? 10 * -clear : 0) >= best) continue;
                    float through = Mathf.Min(0, Gap(h, thumbVerts, fingerVerts)) + Mathf.Min(0, Gap(h, thumbVerts, h.palm));
                    float score = Mathf.Abs(clear - .0015f) + 10 * -through + (clear < 0 ? 10 * -clear : 0);
                    if (score < best) { best = score; pick = (a, b, c, d); h.thumbClear = clear; h.thumbInRange = !wide; }
                }
            }
            t[0].t.localRotation = fixedRot[0] * Quaternion.AngleAxis(pick.a, opp) * Quaternion.AngleAxis(pick.b, toIndex); t[1].t.localRotation = fixedRot[1] * Quaternion.AngleAxis(pick.c, flex2); t[2].t.localRotation = fixedRot[2] * Quaternion.AngleAxis(pick.d, flex3);
            Bake(h); h.thumbClear = Gap(h, padVerts, midVerts);
            h.thumbA = pick.a; h.thumbB = pick.b; h.thumbC = pick.c; h.thumbD = pick.d; t[0].finalFlex = pick.a; t[1].finalFlex = pick.c; t[2].finalFlex = pick.d;
        }
        static float Clearance(Vector3 t3, Vector3 tip, float rT, Bone i2, Bone[] index, Bone m2, Bone[] middle)
        {
            float di = SegSeg(t3, tip, i2.t.position, SegEnd(index, 1)) - rT - i2.radius, dm = SegSeg(t3, tip, m2.t.position, SegEnd(middle, 1)) - rT - m2.radius;
            return Mathf.Min(di, dm);
        }
        // ---------------------------------------------------------------- verificación
        static void Measure(HandData h, StringBuilder sb)
        {
            Bake(h);
            sb.AppendLine("dedo   | X1 objetivo→final | X2 | X3 | eje local X1 / X2 / X3 | prueba de signo | punta→palma (malla) | holgura con el dedo vecino (malla)");
            for (int f = 0; f < 4; f++)
            {
                var c = h.fingers[f]; var own = Join(h, c[0].t, c[1].t, c[2].t);
                float tip = Gap(h, h.verts[c[2].t], h.palm);
                string neighbour = f < 3 ? $"{FingerNames[f + 1]} {Gap(h, own, Join(h, h.fingers[f + 1][0].t, h.fingers[f + 1][1].t, h.fingers[f + 1][2].t)) * 1000:0.0} mm" : "-";
                sb.AppendLine($"{FingerNames[f],-6} | {c[0].target:0}→{c[0].finalFlex:0.0}° | {c[1].target:0}→{c[1].finalFlex:0.0}° | {c[2].target:0}→{c[2].finalFlex:0.0}° | {V(c[0].axisLocal)} {V(c[1].axisLocal)} {V(c[2].axisLocal)} | {(c.All(b => b.signOk) ? "pasó" : "invertido y corregido: " + string.Join(",", c.Where(b => !b.signOk).Select(b => b.t.name)))} | {tip * 1000:0.0} mm{(h.fingerInRange[f] ? "" : " (ÁNGULOS FUERA DEL RANGO PEDIDO)")} | {neighbour}");
                sb.AppendLine($"         la bind ya traía: X1 {c[0].bindFlex:0.0}° X2 {c[1].bindFlex:0.0}° X3 {c[2].bindFlex:0.0}°");
            }
            var t = h.thumb; float pad = Gap(h, h.verts[t[2].t], Join(h, h.fingers[0][1].t, h.fingers[1][1].t));
            float thumbIn = Gap(h, Join(h, t[1].t, t[2].t), h.fingers.SelectMany(c => c).SelectMany(b => h.verts[b.t]).ToArray());
            sb.AppendLine($"Pulgar | Thumb1 oposición {h.thumbA:0.0}° + hacia índice {h.thumbB:0.0}° · Thumb2 {h.thumbC:0.0}° · Thumb3 {h.thumbD:0.0}° · {(h.thumbInRange ? "dentro de los rangos pedidos" : "FUERA de los rangos pedidos")} · yema→falange media índice/medio {pad * 1000:0.0} mm · peor penetración del pulgar en los dedos {thumbIn * 1000:0.0} mm");
            sb.AppendLine($"Palma: peor penetración de dedos en la palma {Gap(h, h.fingers.SelectMany(c => c).SelectMany(b => h.verts[b.t]).ToArray(), h.palm) * 1000:0.0} mm (negativo = atraviesa)");
            sb.AppendLine("Medidas sobre la malla deformada real (BakeMesh): vértice más cercano con signo por la normal. Twist: giros solo sobre ejes perpendiculares al hueso. Escalas 1,1,1.");
        }
        static float SegSeg(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2; float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r), s, t;
            if (a < 1e-9f && e < 1e-9f) return r.magnitude;
            if (a < 1e-9f) { s = 0; t = Mathf.Clamp01(f / e); }
            else { float c = Vector3.Dot(d1, r); if (e < 1e-9f) { t = 0; s = Mathf.Clamp01(-c / a); } else { float b = Vector3.Dot(d1, d2), den = a * e - b * b; s = den > 1e-9f ? Mathf.Clamp01((b * f - c * e) / den) : 0; t = (b * s + f) / e; if (t < 0) { t = 0; s = Mathf.Clamp01(-c / a); } else if (t > 1) { t = 1; s = Mathf.Clamp01((b - c) / a); } } }
            return ((p1 + d1 * s) - (p2 + d2 * t)).magnitude;
        }
        // ---------------------------------------------------------------- capturas: nudillos, lado del pulgar, lado del meñique, palma
        static void Shots(HandData h, string tag)
        {
            var center = h.fingers.SelectMany(f => f).Select(b => b.t.position).Aggregate(Vector3.zero, (a, b) => a + b) / 12f;
            int layer = h.skin.gameObject.layer;
            var go = new GameObject("FistBakeCam") { hideFlags = HideFlags.HideAndDontSave }; var cam = go.AddComponent<Camera>(); cam.enabled = false;
            cam.cullingMask = 1 << layer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.34f, .36f, .4f); cam.fieldOfView = 30; cam.nearClipPlane = .01f; cam.farClipPlane = 5;
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var lightGo = new GameObject("FistBakeLight") { hideFlags = HideFlags.HideAndDontSave }; var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.6f; light.cullingMask = 1 << layer;
            // the skin is not re-evaluated between renders in edit mode: draw the baked mesh instead
            Bake(h); var shown = new GameObject("FistBakeMesh") { hideFlags = HideFlags.HideAndDontSave, layer = layer };
            shown.transform.SetPositionAndRotation(h.skin.transform.position, h.skin.transform.rotation);
            shown.AddComponent<MeshFilter>().sharedMesh = h.baked; shown.AddComponent<MeshRenderer>().sharedMaterials = h.skin.sharedMaterials;
            bool wasOn = h.skin.enabled; h.skin.enabled = false;
            var others = h.skin.transform.root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r != h.skin && r.gameObject.layer == layer).ToArray(); foreach (var r in others) r.enabled = false;
            var rt = new RenderTexture(720, 720, 24); var tex = new Texture2D(720, 720, TextureFormat.RGB24, false); var prev = RenderTexture.active;
            try
            {
                foreach (var (name, dir, up) in new[] { ("nudillos", h.D, -h.N), ("pulgar", h.L, h.D), ("menique", -h.L, h.D), ("palma", h.N, h.D) })
                {
                    go.transform.position = center + dir * .28f; go.transform.rotation = Quaternion.LookRotation(center - go.transform.position, up);
                    lightGo.transform.rotation = Quaternion.LookRotation(-dir + up * .5f);
                    cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 720, 720), 0, 0); tex.Apply();
                    File.WriteAllBytes($"{ShotDir}/{h.side}_{tag}_{name}.png", tex.EncodeToPNG());
                }
            }
            finally { h.skin.enabled = wasOn; foreach (var r in others) r.enabled = true; Object.DestroyImmediate(shown); cam.targetTexture = null; RenderTexture.active = prev; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go); Object.DestroyImmediate(lightGo); }
        }
        static string V(Vector3 v) => $"({v.x:0.00},{v.y:0.00},{v.z:0.00})";
    }
}
