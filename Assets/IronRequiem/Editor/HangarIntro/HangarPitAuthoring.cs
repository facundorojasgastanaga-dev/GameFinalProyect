using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// v4 (12/11/2026) — Room00_Hangar: the floor under and in front of the back conveyor becomes a deep, dark
    /// industrial pit. Clear edge (lip step, hazard stripes, a broken railing with an open gap), structure below
    /// (pillar and column shafts, beams, hanging cables, distant lights, fog), and a GapFallRecovery volume that
    /// reuses the gap system: damage + return to a safe point on the yard side. Called from HangarSectorAuthoring
    /// after the shell colliders and before the scrap yard and the NavMesh bake (so no NavMesh over the void).
    /// Hangar-local coordinates (the menu environment: floor plane z -35..55, bridge/belt at z 19, back blocker z 30).
    internal static class HangarPitAuthoring
    {
        internal const float EdgeZ = 13.2f, BackZ = 30.5f, Half = 16.5f, Depth = 42f, RailZ = 12.7f;
        const string CutFloorMaterial = "Assets/IronRequiem/Art/Menu/HG_Floor_PitCut.mat";

        /// True when a hangar-local XZ point (plus a radius) would sit over the void.
        internal static bool OverPit(Vector3 local, float radius) => local.z + radius > EdgeZ - .6f && local.z - radius < BackZ + .5f && Mathf.Abs(local.x) < Half + 1f;

        internal static void Build(Transform set, HangarMenuAuthoring.Mats m, StringBuilder log)
        {
            var env = set.Find("Environment");
            var root = HangarMenuAuthoring.Group("ConveyorPit", set);

            // 1) floor: the plane and its collider stop at the edge; a back strip keeps the terrace beyond the back blocker
            var floor = env.Find("Floor");
            var floorMat = CutMaterial(m.floor, 4.8f / 9f);
            floor.localPosition = new Vector3(0, 0, (-35f + EdgeZ) * .5f); floor.localScale = new Vector3(9, 1, (EdgeZ + 35f) / 10f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
            var back = HangarMenuAuthoring.Prim(PrimitiveType.Plane, "FloorBackStrip", root, new Vector3(0, 0, (BackZ + 55f) * .5f), new Vector3(9, 1, (55f - BackZ) / 10f), CutMaterial(m.floor, (55f - BackZ) / 10f / 9f, "_Back"), null, false);
            back.GetComponent<MeshRenderer>().receiveShadows = true;
            var floorCol = env.Find("FloorCollider");
            floorCol.localPosition = new Vector3(0, -.25f, (-25f + EdgeZ) * .5f); floorCol.GetComponent<BoxCollider>().size = new Vector3(Half * 2 + 1, .5f, EdgeZ + 25f);

            // 2) whatever rested on the floor over the void goes (menu clutter, floor steam, puddles); structure stays
            int hidden = 0; var hiddenNames = new List<string>();
            foreach (var t in set.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (!t || t == set || t.IsChildOf(root) || t.name == "Environment" || t.name == "FloorCollider" || t.name == "Floor") continue;
                if (t.parent != env && (t.parent == null || t.parent.parent != env)) continue; // direct environment props and their groups
                var n = t.name;
                if (n.StartsWith("Wall") || n.StartsWith("Column") || n.StartsWith("Girder") || n.StartsWith("Ceiling") || n.StartsWith("City") || n.StartsWith("Window") || n.StartsWith("Banner")) continue;
                var local = set.InverseTransformPoint(t.position);
                var r = t.GetComponent<Renderer>(); var ps = t.GetComponent<ParticleSystem>();
                if (!r && !ps) continue;
                float bottom = r ? set.InverseTransformPoint(r.bounds.min).y : local.y;
                if (bottom > 1.2f || local.y > 3f) continue;
                if (local.z <= EdgeZ + .2f || local.z >= BackZ || Mathf.Abs(local.x) >= Half) continue;
                t.gameObject.SetActive(false); hidden++; if (hiddenNames.Count < 12) hiddenNames.Add(n);
            }

            var dark = m.plateDark; var steel = m.plate; var trim = m.trim;
            float zMid = (EdgeZ + BackZ) * .5f, zLen = BackZ - EdgeZ;
            // 3) the shaft: front face under the edge, back face, side faces under the hall walls, a dark bottom
            Solid(HangarMenuAuthoring.Box("PitWallFront", root, new Vector3(0, -Depth * .5f - .02f, EdgeZ + .2f), new Vector3(Half * 2, Depth, .4f), dark, null, false));
            Solid(HangarMenuAuthoring.Box("PitWallBack", root, new Vector3(0, -Depth * .5f - .02f, BackZ), new Vector3(Half * 2, Depth, .4f), dark, null, false));
            foreach (var s in new[] { -1, 1 }) Solid(HangarMenuAuthoring.Box(s < 0 ? "PitWallL" : "PitWallR", root, new Vector3(s * (Half + .5f), -Depth * .5f - .02f, zMid), new Vector3(1, Depth, zLen + .4f), dark, null, false));
            HangarMenuAuthoring.Box("PitBottom", root, new Vector3(0, -Depth, zMid), new Vector3(Half * 2, .5f, zLen), dark, null, false);
            // ribs on the shaft walls every 3 m of depth catch the distant lights and sell the depth
            for (float y = -2.5f; y > -Depth + 2; y -= 3.2f)
            {
                HangarMenuAuthoring.Box("RibFront", root, new Vector3(0, y, EdgeZ + .45f), new Vector3(Half * 2, .18f, .12f), trim, null, false);
                HangarMenuAuthoring.Box("RibBack", root, new Vector3(0, y - 1.1f, BackZ - .25f), new Vector3(Half * 2, .18f, .12f), trim, null, false);
            }

            // 4) bridge pillars and hall columns continue down into the shaft
            foreach (float x in new[] { -11f, 3f, 13f }) HangarMenuAuthoring.Box("PillarShaft", root, new Vector3(x, -Depth * .5f, 19f), new Vector3(.9f, Depth, .9f), dark, null, false);
            foreach (float z in new[] { 19f, 26f }) foreach (var s in new[] { -1, 1 }) HangarMenuAuthoring.Box("ColumnShaft", root, new Vector3(s * 15.8f, -Depth * .5f, z), new Vector3(1.4f, Depth, 1.4f), steel, null, false);

            // 5) beams across and along the shaft at several depths, a few sagging or snapped
            var rnd = new System.Random(4013);
            float R(float a, float c) => a + (float)rnd.NextDouble() * (c - a);
            var beams = new[] { (-3.6f, 16.2f), (-6.2f, 23.4f), (-9.8f, 18.6f), (-14.5f, 27.6f), (-19f, 15.4f), (-25f, 22f), (-31f, 19.2f) };
            for (int i = 0; i < beams.Length; i++)
            {
                var (y, z) = beams[i];
                bool snapped = i == 1 || i == 4;
                if (!snapped) IBeam(root, "BeamX" + i, new Vector3(0, y, z), new Vector3(Half * 2, .45f, .35f), Vector3.zero, steel, trim);
                else
                {
                    IBeam(root, "BeamXBrokenA" + i, new Vector3(-Half * .55f, y - .4f, z), new Vector3(Half * .95f, .45f, .35f), new Vector3(0, 0, -9f), steel, trim);
                    IBeam(root, "BeamXBrokenB" + i, new Vector3(Half * .62f, y - 1.6f, z), new Vector3(Half * .8f, .45f, .35f), new Vector3(0, 0, 21f), steel, trim);
                }
            }
            foreach (var (x, y) in new[] { (-6.5f, -7.5f), (8.5f, -12f), (-2f, -20f), (11f, -27f) })
                IBeam(root, "BeamZ", new Vector3(x, y, zMid), new Vector3(.35f, .45f, zLen), Vector3.zero, steel, trim);
            // a service gantry hanging under the belt (reads as structure from the yard)
            HangarMenuAuthoring.Box("UnderBeltGantry", root, new Vector3(2f, 4.3f, 19.2f), new Vector3(24f, .25f, 1.6f), trim, null, false);
            for (int i = 0; i < 7; i++) HangarMenuAuthoring.Box("GantryHanger", root, new Vector3(-10f + i * 4f, 4.85f, 19.2f), new Vector3(.08f, .85f, .08f), trim, null, false);

            // 6) cables hanging from the edge, the gantry and the beams, a few swaying slightly off vertical
            for (int i = 0; i < 18; i++)
            {
                float x = R(-15f, 15f), z = i < 6 ? EdgeZ + .55f : R(EdgeZ + 1.5f, BackZ - 1f), top = i < 6 ? -.1f : (i < 11 ? 4.2f : R(-12f, -3f)), len = R(6f, 22f);
                var cable = HangarMenuAuthoring.Prim(PrimitiveType.Cylinder, "Cable", root, new Vector3(x, top - len * .5f, z), new Vector3(.05f, len * .5f, .05f), m.rubber, new Vector3(R(-4f, 4f), 0, R(-4f, 4f)), false);
                if (i % 5 == 0) HangarMenuAuthoring.Box("CableLoad", root, new Vector3(x, top - len, z), new Vector3(.35f, .5f, .35f), trim, null, false);
            }

            // 7) distant lights deep down: small emissive housings and three dim point lights (no shadows)
            var lights = new[] { (-9f, -11f, 15.4f, true), (7f, -17f, 29.6f, false), (-3f, -24f, 14.2f, true), (12f, -29f, 22f, false), (-13f, -34f, 27f, true), (4f, -8f, 29.5f, false), (14f, -21f, 14.2f, true) };
            int pointLights = 0;
            foreach (var (x, y, z, warm) in lights)
            {
                HangarMenuAuthoring.Box("PitLamp", root, new Vector3(x, y, z), new Vector3(.5f, .14f, .14f), warm ? m.orangeGlow : m.redGlow, null, false);
                if (pointLights < 4 && warm)
                {
                    HangarMenuAuthoring.MakeLight("PitGlow", root, LightType.Point, new Vector3(x, y + .3f, z + (z < zMid ? .8f : -.8f)), Vector3.zero, new Color(1f, .45f, .16f), 16f, 24f);
                    pointLights++;
                }
            }

            // 8) fog drifting a few metres below the edge, thicker deeper down
            foreach (var (name, y, alpha, rate) in new[] { ("PitFogHigh", -4f, .34f, 6f), ("PitFogLow", -11f, .5f, 5f) })
            {
                HangarMenuAuthoring.PS(name, root, m.fxSmoke, ps =>
                {
                    ps.transform.localPosition = new Vector3(0, y, zMid);
                    var main = ps.main; main.loop = true; main.playOnAwake = true; main.prewarm = true; main.duration = 10;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f); main.startSpeed = new ParticleSystem.MinMaxCurve(.05f, .25f);
                    main.startSize = new ParticleSystem.MinMaxCurve(6f, 11f); main.maxParticles = 70; main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                    main.startColor = new Color(.42f, .45f, .52f, alpha);
                    var em = ps.emission; em.rateOverTime = rate;
                    var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(Half * 2 - 2, 2.5f, zLen - 1.5f);
                    var col = ps.colorOverLifetime; col.enabled = true; col.color = HangarMenuAuthoring.Fade(Color.white);
                    var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.08f, .08f);
                    var rr = ps.GetComponent<ParticleSystemRenderer>(); rr.shadowCastingMode = ShadowCastingMode.Off; rr.receiveShadows = false; rr.sortingFudge = 2;
                });
            }

            // 9) the edge: a lip step, hazard stripes and a broken railing (posts + rails, an open gap at the centre)
            Solid(HangarMenuAuthoring.Box("PitLip", root, new Vector3(0, .06f, EdgeZ - .14f), new Vector3(Half * 2, .12f, .28f), trim, null, false));
            HangarMenuAuthoring.Prim(PrimitiveType.Quad, "PitHazardStripes", root, new Vector3(0, .012f, EdgeZ - .62f), new Vector3(Half * 2, .6f, 1), m.hazard, new Vector3(90, 0, 0), false);
            var rail = HangarMenuAuthoring.Group("BrokenRailing", root);
            int posts = 0, rails = 0;
            for (float x = -16f; x <= 16.01f; x += 2f)
            {
                bool missing = Mathf.Approximately(x, 0f) || Mathf.Approximately(x, 10f);
                if (missing) continue;
                bool bent = Mathf.Approximately(x, 6f);
                var post = HangarMenuAuthoring.Box("Post", rail, new Vector3(x, .55f, RailZ), new Vector3(.08f, 1.1f, .08f), m.hazard, bent ? new Vector3(28f, 0, 6f) : (Vector3?)null, false);
                Solid(post); posts++;
            }
            for (float x = -16f; x < 16f; x += 2f)
            {
                float cx = x + 1f;
                if (cx > -2.1f && cx < 2.1f) continue;                 // the open gap (a clean way into the pit)
                if (cx > 8.9f && cx < 11.1f) continue;                 // torn away next to the missing post
                if (cx > 4.9f && cx < 7.1f)
                {   // bent down towards the void
                    HangarMenuAuthoring.Box("RailBent", rail, new Vector3(cx + .2f, .62f, RailZ + .32f), new Vector3(2f, .06f, .06f), trim, new Vector3(0, 9f, -24f), false);
                    continue;
                }
                Solid(HangarMenuAuthoring.Box("Rail", rail, new Vector3(cx, 1.05f, RailZ), new Vector3(2f, .06f, .06f), trim, null, false));
                HangarMenuAuthoring.Box("RailMid", rail, new Vector3(cx, .55f, RailZ), new Vector3(2f, .04f, .04f), trim, null, false);
                rails++;
            }

            // 10) fall recovery: reuses the gap system (damage + teleport back to a safe point on the yard side)
            var fall = new GameObject("PitFallRecovery"); fall.transform.SetParent(root, false);
            fall.transform.localPosition = new Vector3(0, -5.5f, zMid);
            var box = fall.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(Half * 2, 3f, zLen);
            var body = fall.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var returns = new List<Transform>();
            foreach (var x in new[] { -8f, 0f, 8f })
            {
                var p = new GameObject("PitSafePoint").transform; p.SetParent(root, false);
                p.localPosition = new Vector3(x, .05f, 9f); p.localRotation = Quaternion.LookRotation(Vector3.back);
                returns.Add(p);
            }
            var recovery = fall.AddComponent<GapFallRecovery>();
            recovery.Configure(null, returns.ToArray(), new Transform[0]);
            var so = new SerializedObject(recovery);
            so.FindProperty("damage").floatValue = 20f;
            so.FindProperty("upperFloorHeight").floatValue = -1000f;   // no far bank: always back to the yard side
            so.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine($"v4 pit: edge z={EdgeZ} back z={BackZ} depth={Depth} hidden floor props={hidden} ({string.Join(",", hiddenNames)}) posts={posts} rails={rails} point lights={pointLights}");
        }

        static void IBeam(Transform parent, string name, Vector3 pos, Vector3 size, Vector3 euler, Material web, Material flange)
        {
            var g = HangarMenuAuthoring.Group(name, parent); g.localPosition = pos; g.localRotation = Quaternion.Euler(euler);
            bool alongX = size.x > size.z;
            HangarMenuAuthoring.Box("Web", g, Vector3.zero, alongX ? new Vector3(size.x, size.y, .06f) : new Vector3(.06f, size.y, size.z), web, null, false);
            foreach (var s in new[] { -1, 1 })
                HangarMenuAuthoring.Box("Flange", g, new Vector3(0, s * size.y * .5f, 0), alongX ? new Vector3(size.x, .05f, size.z) : new Vector3(size.x, .05f, size.z), flange, null, false);
        }

        static GameObject Solid(GameObject g) { if (!g.GetComponent<Collider>()) g.AddComponent<BoxCollider>(); g.layer = 0; return g; }

        // A copy of the floor material with its V tiling scaled for the shortened plane (keeps the tile size).
        static Material CutMaterial(Material source, float vScale, string suffix = "")
        {
            var path = suffix.Length == 0 ? CutFloorMaterial : CutFloorMaterial.Replace(".mat", suffix + ".mat");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(source); AssetDatabase.CreateAsset(mat, path); }
            else mat.CopyPropertiesFromMaterial(source);
            foreach (var prop in new[] { "_BaseMap", "_BumpMap", "_MainTex", "_OcclusionMap", "_MetallicGlossMap" })
            {
                if (!source.HasProperty(prop)) continue;
                var scale = source.GetTextureScale(prop); scale.y *= vScale; mat.SetTextureScale(prop, scale);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
