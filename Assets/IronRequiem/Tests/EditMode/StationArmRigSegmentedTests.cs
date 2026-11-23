using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Tests
{
    /// Mount station art pass (19/11/2026): the StationArmRig options the segmented model needs (hinge-aligned
    /// segments, a grip point ahead of the wrist), their defaults (v7 behaviour unchanged) and the production station
    /// dressed with the segmented model (applied 19/11/2026; the preview variant it was tried on is retired).
    public sealed class StationArmRigSegmentedTests
    {
        const string Station = "Assets/IronRequiem/Prefabs/Levels/MountStation_C04.prefab";
        GameObject root;

        [TearDown] public void TearDown() { if (root) Object.DestroyImmediate(root); }

        // station frame at the origin, one arm hanging from (0.6, 2.4, 0.6) like the segmented arm A
        StationArmRig Build(float upperLen = .83f, float foreLen = .23f)
        {
            root = new GameObject("Station");
            var arm = new GameObject("Arm").transform; arm.SetParent(root.transform, false); arm.localPosition = new Vector3(.6f, 2.4f, .6f);
            var yaw = new GameObject("Yaw").transform; yaw.SetParent(arm, false);
            var upper = new GameObject("Upper").transform; upper.SetParent(yaw, false); upper.localPosition = new Vector3(0, -.27f, 0);
            var fore = new GameObject("Fore").transform; fore.SetParent(yaw, false);
            var grip = new GameObject("Gripper").transform; grip.SetParent(arm, false); grip.localPosition = new Vector3(0, -1.3f, 0);
            var a = new GameObject("JawA").transform; a.SetParent(grip, false); var b = new GameObject("JawB").transform; b.SetParent(grip, false);
            var rig = arm.gameObject.AddComponent<StationArmRig>();
            rig.Configure(yaw, upper, fore, grip, upperLen, foreLen, a, b);
            var so = new SerializedObject(rig); so.FindProperty("obstacles").intValue = 0; so.ApplyModifiedPropertiesWithoutUndo();   // no physics in this test
            return rig;
        }
        static void Set(StationArmRig rig, bool hinge, float gripLength)
        {
            var so = new SerializedObject(rig);
            so.FindProperty("hingeAligned").boolValue = hinge; so.FindProperty("gripLength").floatValue = gripLength;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static Transform T(StationArmRig rig, string field) => (Transform)new SerializedObject(rig).FindProperty(field).objectReferenceValue;
        static Vector3 WristEnd(StationArmRig rig, float foreLen) { var f = T(rig, "fore"); return f.position + f.forward * foreLen; }

        [Test]
        public void DefaultsKeepTheGripperAtTheEndOfTheForeSegment()
        {
            var rig = Build();
            var target = new Vector3(.3f, 1.5f, .1f);
            rig.Pose(target, Vector3.left, 1);
            Assert.That(Vector3.Distance(rig.Gripper.position, WristEnd(rig, .23f)), Is.LessThan(1e-3f), "v7: gripper sits at the wrist");
            Assert.That(Vector3.Distance(rig.Gripper.position, target), Is.LessThan(1e-3f), "reachable target is reached");
        }

        [Test]
        public void GripLengthPutsTheJawsOnTheTargetAndTheWristBehindIt()
        {
            var rig = Build(); Set(rig, false, .24f);
            var target = new Vector3(.25f, 1.45f, .0f); var approach = new Vector3(-1, -.2f, -.1f).normalized;
            rig.Pose(target, approach, 1);
            Assert.That(Vector3.Distance(rig.Gripper.position, target), Is.LessThan(1e-3f), "jaws on the target");
            Assert.That(Vector3.Distance(WristEnd(rig, .23f), target - approach * .24f), Is.LessThan(1e-3f), "wrist one grip length behind, along the approach");
            Assert.That(Vector3.Angle(rig.Gripper.forward, approach), Is.LessThan(.5f), "jaws face the approach");
        }

        [Test]
        public void HingeAlignedSegmentsTurnAboutTheBendAxisWithoutFlipping()
        {
            var rig = Build(); Set(rig, true, .24f);
            Transform upper = T(rig, "upper"), fore = T(rig, "fore");
            Vector3 lastRight = Vector3.zero; float worst = 1;
            // sweep the grip point under and across the shoulder (the vertical case that made LookRotation(dir, up) roll)
            for (int i = 0; i <= 40; i++)
            {
                float k = i / 40f;
                var target = new Vector3(Mathf.Lerp(.9f, .3f, k), Mathf.Lerp(1.3f, 1.5f, k), Mathf.Lerp(.75f, .45f, k));
                rig.Pose(target, Vector3.down, 0);
                var bend = Vector3.Cross(upper.forward, fore.forward);
                if (bend.sqrMagnitude > 1e-4f)
                {
                    Assert.That(Vector3.Angle(upper.right, bend.normalized), Is.LessThan(1f), "upper X = bend axis at step " + i);
                    Assert.That(Vector3.Angle(fore.right, bend.normalized), Is.LessThan(1f), "fore X = bend axis at step " + i);
                }
                Assert.That(float.IsNaN(upper.rotation.x) || float.IsNaN(fore.rotation.x), Is.False);
                if (i > 0) worst = Mathf.Min(worst, Vector3.Dot(lastRight, upper.right));
                lastRight = upper.right;
            }
            Assert.That(worst, Is.GreaterThan(.9f), "the hinge axis turns smoothly (no roll flip) across the sweep");
        }

        // 19/11 refinement: with modelWrist the claw head stays on the tool tip and the claw faces the tool the way the
        // model has it (the arm no longer reads detached), for grips from the side, from above and at rest
        [Test]
        public void ModelWristKeepsTheClawOnTheToolTipAndFacingTheTool()
        {
            var rig = Build(); Set(rig, true, .24f);
            // model-like relation: wrist 0.24 m behind the grip point along the claw axis, tool coming in at ~60 deg
            var wristLocal = new Vector3(.02f, .015f, -.24f); var elbowLocal = wristLocal + new Vector3(.2f, .02f, -.12f);
            var so = new SerializedObject(rig);
            so.FindProperty("modelWrist").boolValue = true; so.FindProperty("wristLocal").vector3Value = wristLocal; so.FindProperty("elbowLocal").vector3Value = elbowLocal;
            so.FindProperty("modelElbowWeight").floatValue = 1; so.FindProperty("rollToTool").floatValue = 1; so.ApplyModifiedPropertiesWithoutUndo();
            var cases = new[] { (new Vector3(.3f, 1.5f, .1f), Vector3.left), (new Vector3(.5f, 1.2f, .4f), Vector3.down), (new Vector3(.4f, 1.5f, .15f), new Vector3(-1, -.5f, .3f)) };
            foreach (var (target, approach) in cases)
            {
                rig.Pose(target, approach, 1, Vector3.up);
                var g = rig.Gripper; var wrist = WristEnd(rig, .23f); var elbow = rig.Elbow;
                Assert.That(Vector3.Distance(g.position + g.rotation * wristLocal, wrist), Is.LessThan(1e-3f), "claw head on the tool tip for " + approach);
                Assert.That(Vector3.Angle(g.forward, approach.normalized), Is.LessThan(.5f), "jaws still face the approach for " + approach);
                Assert.That(Vector3.Distance(g.position, target), Is.LessThan(.03f), "grip point on the target for " + approach);
                var have = Vector3.ProjectOnPlane(elbow - wrist, g.forward); var want = Vector3.ProjectOnPlane(g.rotation * (elbowLocal - wristLocal), g.forward);
                if (have.magnitude > .35f * (elbow - wrist).magnitude && want.magnitude > .35f * (elbowLocal - wristLocal).magnitude)
                    Assert.That(Vector3.Angle(have, want), Is.LessThan(3f), "claw rolled to face the tool for " + approach);
            }
        }

        // 19/11 refinement: tong jaws turn about their pivot (no sliding apart), opening wider than they close
        [Test]
        public void HingedJawsTurnAboutTheClawHeadInsteadOfSliding()
        {
            var rig = Build(); Set(rig, true, .24f);
            var so = new SerializedObject(rig);
            so.FindProperty("jawHingeOpen").floatValue = 16; so.FindProperty("jawHingeClosed").floatValue = 4; so.ApplyModifiedPropertiesWithoutUndo();
            Transform a = T(rig, "jawA"), b = T(rig, "jawB"); var pa = a.localPosition; var pb = b.localPosition;
            rig.Pose(new Vector3(.3f, 1.5f, .1f), Vector3.left, 0);
            Assert.That(a.localPosition, Is.EqualTo(pa), "jaw A stays on its pin"); Assert.That(b.localPosition, Is.EqualTo(pb), "jaw B stays on its pin");
            float open = Quaternion.Angle(a.localRotation, b.localRotation);
            rig.Pose(new Vector3(.3f, 1.5f, .1f), Vector3.left, 1);
            float closed = Quaternion.Angle(a.localRotation, b.localRotation);
            Assert.That(open, Is.EqualTo(32).Within(.1f)); Assert.That(closed, Is.EqualTo(8).Within(.1f));
            Assert.That((a.localRotation * Vector3.forward).x, Is.LessThan(0), "jaw A opens toward -x"); Assert.That((b.localRotation * Vector3.forward).x, Is.GreaterThan(0), "jaw B toward +x");
        }

        [Test]
        public void ProductionStationIsWiredAndFitsTheMap01Walls()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Station);
            Assert.That(prefab, Is.Not.Null, Station);
            var station = prefab.GetComponent<WeaponMountStation>(); Assert.That(station, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Transform>(true).Count(t => t.name == "SA_Frame"), Is.EqualTo(1), "dressed once");
            Assert.That(prefab.GetComponentsInChildren<StationArmRig>(true).Length, Is.EqualTo(2), "two working arms");
            foreach (var rig in prefab.GetComponentsInChildren<StationArmRig>(true))
            {
                var so = new SerializedObject(rig);
                Assert.That(so.FindProperty("hingeAligned").boolValue, Is.True, rig.name);
                Assert.That(so.FindProperty("gripLength").floatValue, Is.GreaterThan(.1f), rig.name);
                Assert.That(so.FindProperty("modelWrist").boolValue, Is.True, rig.name + " model wrist");
                Assert.That(so.FindProperty("jawHingeOpen").floatValue, Is.GreaterThan(so.FindProperty("jawHingeClosed").floatValue), rig.name + " tong jaws");
                Assert.That(so.FindProperty("wristLocal").vector3Value.z, Is.LessThan(-.1f), rig.name + " wrist behind the grip point");
                foreach (var jaw in new[] { "jawA", "jawB" })
                {
                    var t = (Transform)so.FindProperty(jaw).objectReferenceValue;
                    Assert.That(t && t.name.StartsWith("SA_Jaw") && t.GetComponentInChildren<MeshRenderer>(), Is.True, rig.name + " " + jaw + " carries a claw half");
                }
                foreach (var n in new[] { "SA_Hanger", "SA_Link", "SA_Tool", "SA_JawA", "SA_JawB" })
                    Assert.That(rig.GetComponentsInChildren<Transform>(true).Count(t => t.name == n), Is.EqualTo(1), rig.name + " has one " + n);
                foreach (var seg in new[] { "upper", "fore" })
                    Assert.That(((Transform)so.FindProperty(seg).objectReferenceValue).GetComponentsInChildren<MeshRenderer>().Any(r => r.enabled && r.name.StartsWith("SA_")), Is.True, rig.name + " " + seg + " has its model segment");
            }
            foreach (var n in new[] { "Post", "PostHazard", "Lintel", "ArmBeam", "UpperBeam", "ForeBeam", "Elbow", "Palm" })
                foreach (var t in prefab.GetComponentsInChildren<Transform>(true).Where(t => t.name == n))
                    Assert.That(t.GetComponent<Renderer>() == null || !t.GetComponent<Renderer>().enabled, Is.True, "placeholder " + n + " hidden");
            Assert.That(prefab.GetComponentsInChildren<BoxCollider>(true).Count(c => !c.isTrigger && c.name == "Post"), Is.EqualTo(2), "post colliders kept");
            // 19/11 refinement: no hazard-striped blocks on the wrist lock ring (steel pins with a painted cap instead)
            var locks = prefab.transform.Find("WristLocks");
            Assert.That(locks, Is.Not.Null);
            foreach (var r in locks.GetComponentsInChildren<MeshRenderer>(true))
                Assert.That(r.sharedMaterial && r.sharedMaterial.name.Contains("Hazard"), Is.False, r.name + " is not hazard striped");
            Assert.That(locks.GetComponentsInChildren<Transform>(true).Count(t => t.name == "ClampCap"), Is.EqualTo(3), "three painted caps");
            // Map01 S1/S3/S4 have a side wall 2.0 m from the station centre, S2 a back wall 2.0 m behind it
            var inv = prefab.transform.worldToLocalMatrix;
            foreach (var mr in prefab.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.name.StartsWith("SA_")))
            {
                var b = mr.GetComponent<MeshFilter>().sharedMesh.bounds; var m = inv * mr.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    Assert.That(Mathf.Abs(c.x), Is.LessThan(1.98f), mr.name + " inside the side walls");
                    Assert.That(c.z, Is.LessThan(2.0f), mr.name + " in front of the S2 back wall");
                }
            }
        }
    }
}
