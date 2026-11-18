using NUnit.Framework;
using UnityEngine;

namespace IronRequiem.Tests
{
    public sealed class HandPoseControllerTests
    {
        GameObject root;
        HandPoseLibrary library;
        Transform finger;
        HandPoseController.Hand hand;

        [SetUp]
        public void SetUp()
        {
            root=new GameObject("Hand");finger=new GameObject("Finger").transform;finger.SetParent(root.transform,false);
            library=ScriptableObject.CreateInstance<HandPoseLibrary>();
            library.Configure(new[]{new HandPoseLibrary.BonePose
            {
                name="Finger",bind=Quaternion.Euler(0,0,28),open=Quaternion.identity,
                relaxed=Quaternion.Euler(0,0,10),fist=Quaternion.Euler(0,0,85),grip=Quaternion.Euler(0,0,50),inspect=Quaternion.Euler(0,0,5)
            }},Vector3.up,Vector3.forward);
            hand=new HandPoseController.Hand();hand.Configure(root.transform,library);hand.Initialize();
        }
        [TearDown]
        public void TearDown(){Object.DestroyImmediate(root);Object.DestroyImmediate(library);}

        [Test]
        public void AnimatedWritesCannotAccumulateIntoTheAbsolutePose()
        {
            hand.Request(HandPoseLibrary.Pose.Fist,.06f);hand.Apply(.03f,null);
            var halfway=finger.localRotation;
            Assert.That(Quaternion.Angle(halfway,Quaternion.Euler(0,0,47.5f)),Is.LessThan(.01f));
            finger.localRotation=Quaternion.Euler(137,-21,89);hand.Apply(0,null);
            Assert.That(Quaternion.Angle(finger.localRotation,halfway),Is.LessThan(.01f));
            hand.Apply(.03f,null);Assert.That(Quaternion.Angle(finger.localRotation,library.Bones[0].fist),Is.LessThan(.01f));
            for(int i=0;i<120;i++){finger.localRotation=Quaternion.Euler(i*7,32,i);hand.Apply(1f/60,null);}
            Assert.That(Quaternion.Angle(finger.localRotation,library.Bones[0].fist),Is.LessThan(.01f));
            Assert.That(hand.Closure,Is.EqualTo(1).Within(.001f));
            Assert.That(Quaternion.Angle(library.Bones[0].bind,Quaternion.Euler(0,0,28)),Is.LessThan(.01f));
        }

        [Test]
        public void ReversingTheBlendReturnsToNamedPosesWithoutChangingScale()
        {
            hand.Request(HandPoseLibrary.Pose.Fist,.06f);hand.Apply(.03f,null);
            hand.Request(HandPoseLibrary.Pose.Relaxed,.15f);hand.Apply(.15f,null);
            Assert.That(Quaternion.Angle(finger.localRotation,library.Bones[0].relaxed),Is.LessThan(.01f));
            Assert.That(hand.Closure,Is.EqualTo(0).Within(.001f));
            hand.Request(HandPoseLibrary.Pose.Inspect,.1f);hand.Apply(.1f,null);
            Assert.That(Quaternion.Angle(finger.localRotation,library.Bones[0].inspect),Is.LessThan(.01f));
            Assert.That(finger.localScale,Is.EqualTo(Vector3.one));
        }

        [Test]
        public void TimelineSamplingDoesNotDependOnFrameTimeOrThePreviousPose()
        {
            hand.Sample(HandPoseLibrary.Pose.Relaxed,HandPoseLibrary.Pose.Inspect,.4f);var expected=finger.localRotation;
            finger.localRotation=Quaternion.Euler(82,136,19);
            hand.Sample(HandPoseLibrary.Pose.Relaxed,HandPoseLibrary.Pose.Inspect,.4f);
            Assert.That(Quaternion.Angle(finger.localRotation,expected),Is.LessThan(.01f));
            hand.Sample(HandPoseLibrary.Pose.Relaxed,HandPoseLibrary.Pose.Fist,1);
            Assert.That(Quaternion.Angle(finger.localRotation,library.Bones[0].fist),Is.LessThan(.01f));
        }
    }
}
