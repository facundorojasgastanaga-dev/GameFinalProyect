using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
namespace IronRequiem.Tests
{
    public sealed class NewRobotAnimationTests
    {
        NewRobotAnimationPresentation robot;Animator animator;Transform root;LODGroup lod;
        [UnitySetUp]public IEnumerator Setup()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/NewRobotAnimationLab.unity",new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;yield return null;
            Object.FindAnyObjectByType<NewRobotAnimationPreview>().enabled=false;
            robot=Object.FindAnyObjectByType<NewRobotAnimationPresentation>();animator=robot.Animator;root=robot.transform.parent;root.SetPositionAndRotation(Vector3.zero,Quaternion.identity);lod=root.GetComponent<LODGroup>();robot.CancelShot();robot.SetMotion(0);animator.Update(.2f);yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(lod)lod.ForceLOD(-1);yield return null;}
        Transform Bone(string name)=>robot.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        void Step(float duration){for(float t=0;t<duration-.0001f;t+=.02f)animator.Update(Mathf.Min(.02f,duration-t));}
        [UnityTest]public IEnumerator ScaleSharedRigAndReferencesAreCorrect()
        {
            var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>();Assert.That(skins.Length,Is.EqualTo(45));Assert.That(animator.applyRootMotion,Is.False);Assert.That(animator.avatar&&animator.avatar.isValid,Is.True);
            Assert.That(root.GetComponentsInChildren<NewRobotAnimationPreview>().Length,Is.Zero,"Preview driver must remain outside reusable prefab");
            int[] counts={24104,12376,5494};var shared=skins[0].sharedMaterial;
            for(int level=0;level<3;level++)
            {
                var rs=skins.Where(s=>s.name.EndsWith("_LOD"+level)).ToArray();Assert.That(rs.Length,Is.EqualTo(15));long tris=0;
                foreach(var r in rs){for(int sub=0;sub<r.sharedMesh.subMeshCount;sub++)tris+=(long)r.sharedMesh.GetIndexCount(sub)/3;Assert.That(r.sharedMaterial,Is.SameAs(shared));Assert.That(r.bones.Length,Is.EqualTo(r.sharedMesh.bindposeCount));Assert.That(r.bones.All(b=>b),Is.True);}
                Assert.That(tris,Is.EqualTo(counts[level]));
            }
            var bounds=skins.First(s=>s.name.EndsWith("_LOD0")).bounds;foreach(var s in skins.Where(s=>s.name.EndsWith("_LOD0")))bounds.Encapsulate(s.bounds);
            // Renderer bounds deliberately include a margin for arm lift; measure the presentation scale against the preserved source height.
            Assert.That(root.localScale.y*.8617969858f,Is.EqualTo(3).Within(.03f));Assert.That(lod.size*root.lossyScale.y,Is.InRange(3f,6f),"LOD bounds must follow robot size, not FBX scale factor");Assert.That(root.position,Is.EqualTo(Vector3.zero));Assert.That(robot.Muzzle,Is.Not.Null);Assert.That(Vector3.Dot(robot.Muzzle.forward,root.forward),Is.GreaterThan(.98f));yield return null;
        }
        [UnityTest]public IEnumerator WalkPlantsStanceFootAndTurnsWithoutRootMotion()
        {
            robot.SetMotion(robot.ReferenceWalkSpeed);animator.Play("Base Layer.Locomotion",0,0);animator.Update(0);var foot=Bone("Foot_L");var origin=foot.position;var first=Bone("Shin_L").rotation;
            for(int i=0;i<10;i++){root.position+=root.forward*robot.ReferenceWalkSpeed*.02f;animator.Update(.02f);}
            Assert.That(Vector3.Distance(origin,foot.position),Is.LessThan(.025f),"Stance sole must stay planted at reference locomotion speed");Assert.That(Quaternion.Angle(first,Bone("Shin_L").rotation),Is.GreaterThan(2));
            var held=root.position;robot.SetMotion(0,-1);Step(.3f);Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash,Is.EqualTo(Animator.StringToHash("TurnLeft")));robot.SetMotion(0,1);Step(.6f);Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash,Is.EqualTo(Animator.StringToHash("TurnRight")));robot.SetMotion(0,0);Step(.3f);Assert.That(root.position,Is.EqualTo(held));yield return null;
        }
        [UnityTest]public IEnumerator ShotLayersRepeatAndInterruptWithoutDuplicateMoments()
        {
            animator.SetFloat("ShotSpeed",5);robot.SetMotion(robot.ReferenceWalkSpeed);int baseline=robot.ShotCount;Assert.That(robot.RequestShot(),Is.True);Assert.That(robot.RequestShot(),Is.False);var leg=Bone("Thigh_L").rotation;var barrel=Bone("Forearm_R").rotation;var muzzleUp=Vector3.Dot(robot.Muzzle.forward,root.up);Step(.4f);Assert.That(Quaternion.Angle(barrel,Bone("Forearm_R").rotation),Is.GreaterThan(1.3f),"Shot must animate the actual cannon arm");Assert.That(Vector3.Dot(robot.Muzzle.forward,root.up),Is.GreaterThan(muzzleUp+.02f),"Barrel must tilt upwards");Assert.That(robot.ShotCount,Is.EqualTo(baseline+1));Assert.That(Quaternion.Angle(leg,Bone("Thigh_L").rotation),Is.GreaterThan(2),"Lower-body locomotion continues during shot");
            Step(1.2f);Assert.That(robot.IsShooting,Is.False);Assert.That(robot.RequestShot(),Is.True);Step(.04f);robot.CancelShot();Step(.3f);Assert.That(robot.ShotCount,Is.EqualTo(baseline+1),"Cancel before visual discharge suppresses the event");Assert.That(robot.IsShooting,Is.False);
            Assert.That(robot.RequestShot(),Is.True);Step(.4f);Assert.That(robot.ShotCount,Is.EqualTo(baseline+2));robot.CancelShot();Step(.3f);Assert.That(robot.ShotCount,Is.EqualTo(baseline+2));Assert.That(robot.IsShooting,Is.False);Assert.That(animator.GetCurrentAnimatorStateInfo(1).shortNameHash,Is.EqualTo(Animator.StringToHash("Empty")));yield return null;
        }
        [UnityTest]public IEnumerator LodsSelectOneLevelAndAllFollowSameAnimatedBones()
        {
            robot.SetMotion(robot.ReferenceWalkSpeed);Step(.24f);var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach(string part in new[]{"Forearm_R","Thigh_L","Foot_R"})
            {
                var a=skins.Single(s=>s.name==part+"_LOD0");foreach(int i in new[]{1,2}){var b=skins.Single(s=>s.name==part+"_LOD"+i);Assert.That(b.bones.Select(t=>t.name),Is.EquivalentTo(a.bones.Select(t=>t.name)));foreach(var bone in b.bones)Assert.That(a.bones,Does.Contain(bone),"All three LODs must share the exact animated joint transforms");}
            }
            var camera=Object.FindObjectsByType<Camera>().Single();
            for(int i=0;i<3;i++)
            {
                lod.ForceLOD(i);yield return new WaitForEndOfFrame();camera.Render();yield return new WaitForEndOfFrame();
                var visible=skins.Where(r=>r.isVisible).ToArray();Assert.That(visible.Length,Is.EqualTo(15),"Forced LOD should render only fifteen pieces");Assert.That(visible.All(r=>r.name.EndsWith("_LOD"+i)),Is.True);
            }
        }
    }
}
