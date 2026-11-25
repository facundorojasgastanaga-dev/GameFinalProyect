using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools
{
    /// Mount station art pass (19/11/2026): dresses the montage station with the segmented gantry model
    /// (Art/Levels/MountStation/MountStation_Segmented.fbx, prepared by Tools/IronRequiem/Blender/mount_station_prep.py).
    /// The station's animation is unchanged: WeaponMountStation still drives the two StationArmRig arms; this tool only
    /// moves the rig joints onto the model's joints, hangs each model segment under its rig transform (hanger on the
    /// arm root, link on Upper, tool on Fore, the two claw halves on new jaw transforms) and hides the placeholder cubes
    /// (their colliders stay, refitted to the model's posts).
    /// Applied to production (MountStation_C04.prefab) on 19/11/2026 with the user's approval; the preview variant
    /// MountStation_C04_Segmented.prefab was retired then (backup: Tools/IronRequiem/Backups/20261119-mount-station-art/
    /// apply-production). Re-applying is idempotent: the SA_* art is rebuilt from the model, the rig fields re-set.
    public static class MountStationArtBuilder
    {
        const string Menu = MountStationArtTools.Menu;
        internal const string Fbx = "Assets/IronRequiem/Art/Levels/MountStation/MountStation_Segmented.fbx";
        const string MatDir = "Assets/IronRequiem/Art/Levels/MountStation/Materials";
        const string PlateDark = "Assets/IronRequiem/Art/Menu/Materials/HG_PlateDark.mat";
        internal const string BasePrefab = "Assets/IronRequiem/Prefabs/Levels/MountStation_C04.prefab";
        // the retired preview variant (only "Apply ... approved" touches it: moves it to the OS trash if still there)
        const string VariantPrefab = "Assets/IronRequiem/Prefabs/Levels/MountStation_C04_Segmented.prefab";

        // fit, in station-local metres (x across the gate, y up, z into the station; the player stands at z = -0.55)
        const float FrameScale = 3.8f;   // model is 1 unit wide -> 3.8 m: fits the 2.0 m side walls of Map01 S1/S3/S4
        const float ArmScale = .70f;     // all four arms relative to the frame, scaled about their carriage pivot (view coverage)
        const float FrameZ = .95f;       // model centre depth: back of the frame at 2.0 m (Map01 S2 back wall)
        static readonly Vector3 RackLocal = new Vector3(-.78f, 1.16f, .25f);
        const float JawOpen = .055f, JawClosed = .012f;
        // parked rest of each gripper relative to the model's hanging pose (x outward, y up, z back): the arms fold
        // up and out of the centre of the view while idle (the model's own hang fills ~22% of the player's frame)
        // 19/11 refinement: was (.16, .34, .12); lifting 0.34 m put the wrist closer to the shoulder than the arm can fold
        // (upper - fore = 0.6 m), so at rest the arm folded shut and the claw hung back along the link (145 deg at the
        // wrist). Out and back instead keeps the wrist at ~0.8 m, close to the model's own hang.
        static readonly Vector3 ParkOffset = new Vector3(.22f, .10f, .18f);
        // preferred elbow bend in the rig's convention (x toward the station centre, y up, z toward the back): out and
        // back, so the elbow swings away from the camera while the gripper works at the player's wrist
        static readonly Vector3 ElbowHint = new Vector3(-.45f, .25f, .85f);
        // the working arms hang this much further out along the carriage rail than in the model (still under the rail),
        // so arm A's link enters the player's view from its edge
        const float CarriageShift = .12f;
        // 19/11 refinement: how far each elbow leans from ElbowHint toward the model's own elbow for the claw pose
        // (StationArmRig.modelWrist); the claw also rolls to face the tool, so it never reads detached from the arm
        const float ModelElbowWeight = .8f;

        // material name in the FBX -> (base colour, metallic, smoothness) on a copy of the hangar's dark plate
        static readonly (string name, Color color, float metal, float smooth)[] Mats =
        {
            // readable in the dark Map01 vestibule: brighter paint, low metallic (a metal that mirrors a dark room reads black)
            ("IR_MS_Steel", new Color(.42f, .43f, .46f), .3f, .55f),
            ("IR_MS_Paint", new Color(.95f, .5f, .12f), .1f, .5f),
            ("IR_MS_Bolt", new Color(.55f, .56f, .58f), .6f, .7f),
            ("IR_MS_Hose", new Color(.05f, .05f, .055f), 0f, .35f),
            ("IR_MS_Brass", new Color(.62f, .46f, .2f), .55f, .65f),
            // the parked back arms: same paint under the plate's grime, so the two working arms stand out
            ("IR_MS_PaintParked", new Color(.7f, .36f, .1f), .15f, .45f),
        };

        static string ReportDir => MountStationArtTools.ReportDir;
        static void Log(string s) { Directory.CreateDirectory(ReportDir); File.AppendAllText(ReportDir + "/log.txt", DateTime.Now.ToString("HH:mm:ss ") + s + "\n"); Debug.Log("[MountStationArt] " + s); }

        // ---------------------------------------------------------------------------------------------- import
        [MenuItem(Menu + "1 Import model + materials")]
        static void SetupImport()
        {
            Directory.CreateDirectory(MatDir);
            var plate = AssetDatabase.LoadAssetAtPath<Material>(PlateDark) ?? throw new InvalidOperationException("missing " + PlateDark);
            var mats = new Dictionary<string, Material>();
            foreach (var (name, color, metal, smooth) in Mats)
            {
                var path = MatDir + "/" + name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(plate) { name = name }; AssetDatabase.CreateAsset(m, path); }
                m.CopyPropertiesFromMaterial(plate);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", Vector2.one);
                // the arms' paint is a clean industrial orange: the plate's dark grime map would turn it brown
                if (name == "IR_MS_Paint" && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
                EditorUtility.SetDirty(m); mats[name] = m;
            }
            AssetDatabase.SaveAssets();
            var mi = AssetImporter.GetAtPath(Fbx) as ModelImporter ?? throw new InvalidOperationException("missing " + Fbx);
            mi.globalScale = 1; mi.useFileScale = true; mi.bakeAxisConversion = false;
            mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None; mi.importBlendShapes = false;
            mi.importCameras = false; mi.importLights = false; mi.importVisibility = false;
            mi.importNormals = ModelImporterNormals.Import; mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.meshCompression = ModelImporterMeshCompression.Off; mi.isReadable = false; mi.addCollider = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var kv in mats) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            mi.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var sb = new StringBuilder("import: root rot " + model.transform.localEulerAngles + " children:");
            foreach (Transform c in model.transform)
            {
                sb.Append($"\n  {c.name} pos {c.localPosition.ToString("F3")} rot {c.localEulerAngles.ToString("F1")} scale {c.localScale.ToString("F2")}");
                var mf = c.GetComponent<MeshFilter>(); var mr = c.GetComponent<MeshRenderer>();
                if (mf && mf.sharedMesh) sb.Append($" mesh v={mf.sharedMesh.vertexCount} sub={mf.sharedMesh.subMeshCount} bounds c{mf.sharedMesh.bounds.center.ToString("F3")} s{mf.sharedMesh.bounds.size.ToString("F3")}");
                if (mr) sb.Append(" mats=" + string.Join("/", mr.sharedMaterials.Select(x => x ? x.name : "null")));
            }
            Log(sb.ToString());
        }

        // ---------------------------------------------------------------------------------------------- build
        [MenuItem(Menu + "Apply to production prefab (MountStation_C04)")]
        static void ApplyProduction()
        {
            if (!EditorUtility.DisplayDialog("Mount station art", "Dress MountStation_C04.prefab (production, used by RequiemCampaign and Map01 S1-S4) with the segmented model?", "Apply", "Cancel")) return;
            ApplyProductionNoPrompt();
        }
        // the user approved applying in chat on 19/11/2026 ("Sí, aplícalo"); same as above without the dialog, for the
        // file bridge. Retires the preview variant first: with the art in its base it would carry the arms twice.
        [MenuItem(Menu + "Apply to production prefab (approved 19-11, no dialog)")]
        static void ApplyProductionApproved()
        {
            MountStationArtTools.RequireCleanScenes();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(VariantPrefab)) Log("retire variant " + VariantPrefab + " -> OS trash: " + AssetDatabase.MoveAssetToTrash(VariantPrefab));
            ApplyProductionNoPrompt();
            for (int i = 0; i < EditorSceneManager.sceneCount; i++) { var sc = EditorSceneManager.GetSceneAt(i); Log("  open scene " + sc.path + " dirty=" + sc.isDirty); }
        }
        internal static void ApplyProductionNoPrompt()
        {
            var root = PrefabUtility.LoadPrefabContents(BasePrefab);
            try
            {
                var log = new StringBuilder("apply production:");
                ApplyArt(root, log);
                PrefabUtility.SaveAsPrefabAsset(root, BasePrefab, out bool ok);
                Log(log + "\n  saved " + BasePrefab + " ok=" + ok);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        sealed class Model
        {
            public Transform root;
            public Matrix4x4 M(string n) { var t = Find(n); return root.worldToLocalMatrix * t.localToWorldMatrix; }
            public Vector3 P(string n) => M(n).GetColumn(3);
            public Transform Find(string n) => root.Find(n) ?? throw new InvalidOperationException("model has no " + n);
            public bool Has(string n) => root.Find(n);
        }

        const bool BuildParkedArms = false;

        static void ApplyArt(GameObject stationRoot, StringBuilder log)
        {
            var model = new Model { root = (AssetDatabase.LoadAssetAtPath<GameObject>(Fbx) ?? throw new InvalidOperationException("import the model first")).transform };
            var st = stationRoot.transform;
            var old = st.Find("SA_Frame"); if (old) Object.DestroyImmediate(old.gameObject);

            // model -> station axes: front arms toward the player (-z), up = +y
            bool frontIsPlusZ = model.P("FR_P1").z > (model.P("Frame_All_Min").z + model.P("Frame_All_Max").z) * .5f;
            var rc = frontIsPlusZ ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
            if (model.P("FR_P0").y < model.P("FR_P4").y) throw new InvalidOperationException("model is not y-up after import");
            var frameM = Matrix4x4.TRS(new Vector3(0, 0, FrameZ), rc, Vector3.one * FrameScale);
            Vector3 F(string n) => frameM.MultiplyPoint3x4(model.P(n));
            log.Append($"\n  axes: frontIsPlusZ={frontIsPlusZ} frame scale {FrameScale} z {FrameZ} arms x{ArmScale}");

            // static frame
            var frame = Holder("SA_Frame", st);
            foreach (var n in new[] { "Static" }) Visual(model, n, frame, frameM, true);
            // the two back arms (parked, never move) at the same scale as the working front arms.
            // 24/11/2026: removed at the user's request ("the station model has two arms too many"); kept behind a switch.
            if (BuildParkedArms) foreach (var b in new[] { "BR", "BL" })
            {
                if (!model.Has(b + "_Arm")) continue;
                var p0 = model.P(b + "_P0");
                var back = Visual(model, b + "_Arm", frame, frameM * Matrix4x4.Translate(p0) * Matrix4x4.Scale(Vector3.one * ArmScale) * Matrix4x4.Translate(-p0), true);
                var parked = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/IR_MS_PaintParked.mat"); var r = back.GetComponent<MeshRenderer>();
                if (parked) r.sharedMaterials = r.sharedMaterials.Select(m => m && m.name == "IR_MS_Paint" ? parked : m).ToArray();
            }

            // posts: the placeholder cubes keep their colliders, refitted to the model's posts
            var posts = Children(st, "Post").OrderBy(p => p.localPosition.x).ToList();
            var boxes = new[] { ("Frame_PostL", F("Frame_PostL_Min"), F("Frame_PostL_Max")), ("Frame_PostR", F("Frame_PostR_Min"), F("Frame_PostR_Max")) }
                .Select(b => (b.Item1, lo: Vector3.Min(b.Item2, b.Item3), hi: Vector3.Max(b.Item2, b.Item3))).OrderBy(b => b.lo.x).ToList();
            for (int i = 0; i < posts.Count && i < boxes.Count; i++)
            {
                posts[i].localPosition = (boxes[i].lo + boxes[i].hi) * .5f; posts[i].localRotation = Quaternion.identity; posts[i].localScale = boxes[i].hi - boxes[i].lo;
                log.Append($"\n  post {i} collider -> c{posts[i].localPosition.ToString("F2")} s{posts[i].localScale.ToString("F2")}");
            }
            foreach (var n in new[] { "Post", "PostHazard", "Lintel", "ArmBeam", "Housing", "UpperBeam", "UpperPiston", "ForeBeam", "Elbow", "Palm", "JawA", "JawB" })
                foreach (var t in Children(st, n)) { var r = t.GetComponent<Renderer>(); if (r) r.enabled = false; }

            // lamp under the lintel's front edge, sign above it
            var lintelLo = Vector3.Min(F("Frame_Lintel_Min"), F("Frame_Lintel_Max")); var lintelHi = Vector3.Max(F("Frame_Lintel_Min"), F("Frame_Lintel_Max"));
            var lamp = st.Find("StatusLamp"); if (lamp) lamp.localPosition = new Vector3(0, lintelLo.y - .05f, lintelLo.z + .12f);
            var sign = st.Find("Sign");
            if (sign)
            {   // the sign stays inside the frame's width (the placeholder's 6 m text box reached the Map01 side walls)
                sign.localPosition = new Vector3(0, lintelHi.y + .22f, lintelLo.z + .1f);
                var tmp = sign.GetComponent<TMPro.TextMeshPro>();
                if (tmp) { tmp.fontSize = 2.1f; tmp.rectTransform.sizeDelta = new Vector2(lintelHi.x - lintelLo.x - .3f, .6f); tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap; }
            }

            // rack inside the opening, barrel pointing into the station (arm B takes it by the muzzle from behind)
            var rack = st.Find("CannonRack"); if (rack) { rack.localPosition = RackLocal; rack.localRotation = Quaternion.identity; }
            var shelf = st.Find("RackShelf"); if (shelf) { shelf.localPosition = new Vector3(RackLocal.x, 1.02f, RackLocal.z); shelf.localScale = new Vector3(.5f, .06f, .75f); }
            var leg = st.Find("RackLeg"); if (leg) leg.localPosition = new Vector3(RackLocal.x, .5f, RackLocal.z);
            var steel = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/IR_MS_Steel.mat");
            foreach (var t in new[] { shelf, leg }) if (t && steel) t.GetComponent<Renderer>().sharedMaterial = steel;

            // 19/11 refinement: the wrist lock ring's three hazard-striped blocks become steel locking pins, radial to the
            // forearm, with an orange painted cap (same paint as the working arms)
            var locks = st.Find("WristLocks");
            if (locks)
            {
                var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
                var bolt = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/IR_MS_Bolt.mat"); var paint = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/IR_MS_Paint.mat");
                int pins = 0;
                foreach (var clamp in Children(locks, "Clamp").ToList())
                {
                    clamp.GetComponent<MeshFilter>().sharedMesh = cylinder; var r = clamp.GetComponent<MeshRenderer>(); if (bolt) r.sharedMaterial = bolt;
                    clamp.localPosition = new Vector3(0, .048f, 0); clamp.localRotation = Quaternion.identity; clamp.localScale = new Vector3(.018f, .013f, .018f);
                    var cap = clamp.parent.Find("ClampCap");
                    if (!cap)
                    {
                        cap = Holder("ClampCap", clamp.parent);
                        cap.gameObject.AddComponent<MeshFilter>(); var cr = cap.gameObject.AddComponent<MeshRenderer>(); cr.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    cap.GetComponent<MeshFilter>().sharedMesh = cylinder; if (paint) cap.GetComponent<MeshRenderer>().sharedMaterial = paint;
                    cap.localPosition = new Vector3(0, .063f, 0); cap.localRotation = Quaternion.identity; cap.localScale = new Vector3(.024f, .004f, .024f);
                    pins++;
                }
                log.Append($"\n  wrist locks: {pins} hazard blocks -> steel pins with a painted cap");
            }

            // arms: the one whose shoulder lands at +x takes the hand (ArmA), the other the cannon (ArmB)
            string handSide = frameM.MultiplyPoint3x4(model.P("FR_P1")).x > 0 ? "FR" : "FL", cannonSide = handSide == "FR" ? "FL" : "FR";
            float innerX = Mathf.Min(Mathf.Abs(Vector3.Max(F("Frame_PostL_Min"), F("Frame_PostL_Max")).x), Mathf.Abs(Vector3.Min(F("Frame_PostR_Min"), F("Frame_PostR_Max")).x));
            DressArm(model, st, "ArmA_Hand", handSide, frameM, rc, innerX, lintelLo.y, log);
            DressArm(model, st, "ArmB_Cannon", cannonSide, frameM, rc, innerX, lintelLo.y, log);
            var allLo = Vector3.Min(F("Frame_All_Min"), F("Frame_All_Max")); var allHi = Vector3.Max(F("Frame_All_Min"), F("Frame_All_Max"));
            log.Append($"\n  frame bounds {allLo.ToString("F2")} .. {allHi.ToString("F2")}; post inner face |x| {innerX:0.00}; lintel underside {lintelLo.y:0.00}");
        }

        static void DressArm(Model model, Transform st, string armName, string side, Matrix4x4 frameM, Quaternion rc, float innerX, float lintelY, StringBuilder log)
        {
            var arm = st.Find(armName) ?? throw new InvalidOperationException("station has no " + armName);
            var rig = arm.GetComponent<StationArmRig>(); var so = new SerializedObject(rig);
            Transform R(string f) => (Transform)so.FindProperty(f).objectReferenceValue;
            Transform yaw = R("yaw"), upper = R("upper"), fore = R("fore"), gripper = R("gripper");
            foreach (var n in new[] { "SA_Hanger", "SA_Link", "SA_Tool", "SA_JawA", "SA_JawB" })
                foreach (var t in Children(arm, n).ToList()) Object.DestroyImmediate(t.gameObject);

            var p0m = model.P(side + "_P0");
            // arm pieces: frame transform, then scaled about the carriage pivot P0
            var shift = new Vector3(Mathf.Sign(frameM.MultiplyPoint3x4(p0m).x) * CarriageShift, 0, 0);
            var armM = Matrix4x4.Translate(shift) * frameM * Matrix4x4.Translate(p0m) * Matrix4x4.Scale(Vector3.one * ArmScale) * Matrix4x4.Translate(-p0m);
            Vector3 A(string k) => armM.MultiplyPoint3x4(model.P(side + k));
            Vector3 P0 = A("_P0"), P1 = A("_P1"), P2 = A("_P2"), P3 = A("_P3"), P4 = A("_P4"), P5 = A("_P5"), P6 = A("_P6");

            arm.localPosition = P0; arm.localRotation = Quaternion.identity; arm.localScale = Vector3.one;
            yaw.localPosition = Vector3.zero; yaw.localRotation = Quaternion.identity;
            upper.localPosition = P1 - P0;
            var zu = (P2 - P1).normalized; var zf = (P3 - P2).normalized; var bend = Vector3.Cross(zu, zf).normalized;
            var ru = Quaternion.LookRotation(zu, Vector3.Cross(zu, bend)); var rf = Quaternion.LookRotation(zf, Vector3.Cross(zf, bend));
            upper.localRotation = ru; fore.localPosition = P2 - P0; fore.localRotation = rf;
            // gripper at the grip point in the rest orientation the rig gives it (look down, top toward the gate)
            var rg = Quaternion.LookRotation(Vector3.down, Vector3.back);
            var park = new Vector3(Mathf.Sign(P1.x) * ParkOffset.x, ParkOffset.y, ParkOffset.z);
            gripper.localPosition = P4 + park - P0; gripper.localRotation = rg;

            Visual(model, side + "_Hanger", arm, armM, false, "SA_Hanger", Matrix4x4.TRS(P0, Quaternion.identity, Vector3.one));
            Visual(model, side + "_Upper", upper, armM, false, "SA_Link", Matrix4x4.TRS(P1, ru, Vector3.one));
            Visual(model, side + "_Fore", fore, armM, false, "SA_Tool", Matrix4x4.TRS(P2, rf, Vector3.one));

            // claw: its axis (top -> tip) becomes the gripper's approach, the cut between the halves the jaw axis
            var zc = (P5 - P6).normalized;   // claw axis: top (P6) -> tip (P5)
            var cn = MeshCentre(model, side + "_ClawN", armM); var cp = MeshCentre(model, side + "_ClawP", armM);
            var xc = Vector3.ProjectOnPlane(cp - cn, zc).normalized; var clawFrame = Quaternion.LookRotation(zc, Vector3.Cross(zc, xc));
            // the claw frame (at P4) is mapped onto the gripper frame: visual = gripper * inverse(clawFrame) * placement
            var clawToGripper = Matrix4x4.TRS(P4, rg, Vector3.one) * Matrix4x4.TRS(P4, clawFrame, Vector3.one).inverse;
            // 19/11 refinement: each jaw hangs from the claw head (P6) and turns about it like tongs
            var invClaw = Quaternion.Inverse(clawFrame); var pivot = invClaw * (P6 - P4);
            var jawA = Holder("SA_JawA", gripper); var jawB = Holder("SA_JawB", gripper);
            jawA.localPosition = pivot; jawB.localPosition = pivot;
            var jawRest = Matrix4x4.TRS(P4, rg, Vector3.one) * Matrix4x4.Translate(pivot);
            Visual(model, side + "_ClawN", jawA, clawToGripper * armM, false, "SA_ClawN", jawRest);
            Visual(model, side + "_ClawP", jawB, clawToGripper * armM, false, "SA_ClawP", jawRest);
            float pinToGrip = Mathf.Max(.05f, (P4 - P6).magnitude);

            float upperLen = (P2 - P1).magnitude, foreLen = (P3 - P2).magnitude, gripLen = (P4 - P3).magnitude;
            // preferred bend from the model's own rest elbow, in the rig's convention (x toward the station centre)
            var wrist = P4 + Vector3.up * gripLen; var line = (wrist - P1).normalized;
            var h = Vector3.ProjectOnPlane(P2 - P1, line).normalized; float inward = P1.x > 0 ? -1 : 1;
            so.FindProperty("upperLength").floatValue = upperLen; so.FindProperty("foreLength").floatValue = foreLen;
            so.FindProperty("jawA").objectReferenceValue = jawA; so.FindProperty("jawB").objectReferenceValue = jawB;
            so.FindProperty("jawOpen").floatValue = JawOpen; so.FindProperty("jawClosed").floatValue = JawClosed;
            so.FindProperty("elbowHint").vector3Value = ElbowHint.normalized;
            so.FindProperty("elbowMaxSide").floatValue = innerX - .2f;
            so.FindProperty("elbowDepth").vector2Value = new Vector2(-.45f, 1.1f);
            so.FindProperty("elbowMaxHeight").floatValue = lintelY - .25f;
            so.FindProperty("beamRadius").floatValue = .1f;
            so.FindProperty("hingeAligned").boolValue = true; so.FindProperty("gripLength").floatValue = gripLen;
            so.FindProperty("jawHingeOpen").floatValue = Mathf.Atan2(JawOpen, pinToGrip) * Mathf.Rad2Deg;
            so.FindProperty("jawHingeClosed").floatValue = Mathf.Atan2(JawClosed, pinToGrip) * Mathf.Rad2Deg;
            // model wrist: the tool tip and the elbow as the model has them around this claw (claw frame = gripper frame)
            so.FindProperty("modelWrist").boolValue = true;
            so.FindProperty("wristLocal").vector3Value = invClaw * (P3 - P4);
            so.FindProperty("elbowLocal").vector3Value = invClaw * (P2 - P4);
            so.FindProperty("modelElbowWeight").floatValue = ModelElbowWeight;
            so.FindProperty("rollToTool").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Append($"\n  {armName} model wrist {(invClaw * (P3 - P4)).ToString("F3")} elbow {(invClaw * (P2 - P4)).ToString("F3")} jaw pivot {pivot.ToString("F3")}"
                + $" tongs {Mathf.Atan2(JawOpen, pinToGrip) * Mathf.Rad2Deg:0.0}/{Mathf.Atan2(JawClosed, pinToGrip) * Mathf.Rad2Deg:0.0} deg");
            log.Append($"\n  {armName} <- {side}: P0 {P0.ToString("F2")} P1 {P1.ToString("F2")} P2 {P2.ToString("F2")} P3 {P3.ToString("F2")} grip {P4.ToString("F2")}"
                + $" upper {upperLen:0.00} fore {foreLen:0.00} grip {gripLen:0.00} reach {upperLen + foreLen:0.00} model bend {new Vector3(h.x * inward, h.y, h.z).ToString("F2")} hint {ElbowHint.normalized.ToString("F2")}");
        }

        static Vector3 MeshCentre(Model model, string n, Matrix4x4 place)
        {
            var mf = model.Find(n).GetComponent<MeshFilter>();
            return (place * model.M(n)).MultiplyPoint3x4(mf.sharedMesh.bounds.center);
        }

        // a renderer for model piece n under parent, placed so that it lands where `place * model` puts it while the
        // parent sits at parentRest (station-local); returns the visual
        static Transform Visual(Model model, string n, Transform parent, Matrix4x4 place, bool shadows, string name = null, Matrix4x4? parentRest = null)
        {
            var src = model.Find(n); var mf = src.GetComponent<MeshFilter>(); var mr = src.GetComponent<MeshRenderer>();
            var go = new GameObject(name ?? ("SA_" + n)); go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var world = place * model.M(n);                          // station-local placement
            var local = (parentRest ?? Matrix4x4.identity).inverse * world;
            go.transform.localPosition = local.GetColumn(3); go.transform.localRotation = local.rotation; go.transform.localScale = local.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = mr.sharedMaterials;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = true;
            return go.transform;
        }

        static Transform Holder(string name, Transform parent)
        {
            var t = new GameObject(name).transform; t.gameObject.layer = parent.gameObject.layer; t.SetParent(parent, false); return t;
        }
        static IEnumerable<Transform> Children(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name);

        // ---------------------------------------------------------------------------------------------- stills
        [MenuItem(Menu + "3 Render production stills (edit mode)")]
        static void StillsBase() => RenderStills(BasePrefab, "stills-production");

        static void RenderStills(string prefabPath, string sub)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) ?? throw new InvalidOperationException("missing " + prefabPath);
            var scene = EditorSceneManager.NewPreviewScene();
            var dir = ReportDir + "/" + sub; Directory.CreateDirectory(dir);
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                var lightGo = new GameObject("Key"); SceneManager_Move(lightGo, scene);
                var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.6f; lightGo.transform.rotation = Quaternion.Euler(40, -35, 0);
                var fillGo = new GameObject("Fill"); SceneManager_Move(fillGo, scene);
                var fill = fillGo.AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .5f; fillGo.transform.rotation = Quaternion.Euler(20, 150, 0);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Quad); SceneManager_Move(floor, scene);
                floor.transform.rotation = Quaternion.Euler(90, 0, 0); floor.transform.localScale = Vector3.one * 12;
                var camGo = new GameObject("Cam"); SceneManager_Move(camGo, scene);
                var cam = camGo.AddComponent<Camera>(); cam.scene = scene; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.09f, .1f, .12f);
                cam.fieldOfView = 60; cam.nearClipPlane = .05f; cam.farClipPlane = 60;
                var rigs = inst.GetComponentsInChildren<StationArmRig>(true);
                var st = inst.transform;
                StationArmRig hand = rigs.FirstOrDefault(r => r.name == "ArmA_Hand"), cannon = rigs.FirstOrDefault(r => r.name == "ArmB_Cannon");
                // poses: rest, and two working poses near the player's wrist / the rack muzzle (approximate)
                var poses = new (string name, Action pose)[]
                {
                    ("rest", () => { }),
                    ("reach", () =>
                    {
                        if (hand) hand.Pose(st.TransformPoint(new Vector3(.12f, 1.42f, -.12f)), st.TransformDirection(Vector3.left), 1, Vector3.up);
                        if (cannon) cannon.Pose(st.TransformPoint(new Vector3(-.2f, 1.45f, .35f)), st.TransformDirection(Vector3.back), 1, Vector3.up);
                    }),
                };
                var restGrips = rigs.ToDictionary(r => r, r => r.Gripper.position);
                var views = new (string name, Vector3 pos, Vector3 look, float fov)[]
                {
                    ("player", new Vector3(0, 1.62f, -.55f), new Vector3(0, 1.35f, .6f), 60),
                    ("front", new Vector3(0, 1.9f, -5.5f), new Vector3(0, 1.7f, .8f), 50),
                    ("p34", new Vector3(3.6f, 2.6f, -3.8f), new Vector3(0, 1.5f, .8f), 50),
                    ("side", new Vector3(5.5f, 1.8f, .8f), new Vector3(0, 1.6f, .8f), 45),
                };
                foreach (var pose in poses)
                {
                    foreach (var r in rigs) { r.ResetSolver(); r.Pose(restGrips[r], Vector3.down, 0); }
                    pose.pose();
                    foreach (var v in views)
                    {
                        camGo.transform.position = st.TransformPoint(v.pos); camGo.transform.LookAt(st.TransformPoint(v.look)); cam.fieldOfView = v.fov;
                        var rt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt; cam.Render();
                        var prev = RenderTexture.active; RenderTexture.active = rt;
                        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                        RenderTexture.active = prev; cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
                        File.WriteAllBytes($"{dir}/{pose.name}_{v.name}.png", tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                    }
                }
                var sb = new StringBuilder("stills " + prefabPath + " -> " + dir);
                foreach (var r in rigs) sb.Append($"\n  {r.name}: gripper rest {st.InverseTransformPoint(restGrips[r]).ToString("F2")} elbow {st.InverseTransformPoint(r.Elbow).ToString("F2")}");
                Log(sb.ToString());
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }
}
