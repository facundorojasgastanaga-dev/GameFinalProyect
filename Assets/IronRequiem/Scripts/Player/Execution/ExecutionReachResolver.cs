using System.Collections.Generic;
using UnityEngine;

namespace IronRequiem
{
    // executions-v02: where the player's hand has to meet the robot's weapon, and how far the robot (and, in band C, the player)
    // must move so the arm never stretches past DisarmMotion.maxGripReachV3 (0.55 m).
    // The grip point is the closest point of the mounted WEAPON's surface (renderer bounds of the weapon views), falling back to the
    // real joint. Retuned after the blind review:
    //   pull = min(d - 0.45, 0.80, nearestBodyCollider - 0.40): the robot is never pulled so close that the camera ends up inside
    //          its body (the weapon and its own arm colliders do not count).
    //   if the hand still cannot reach (d - pull > 0.50): player micro-step <= 0.45 m in 0.12 s (CapsuleCast checked, also bound by
    //          the same 0.40 m body clearance); if still out of reach at the Rip beat, the weapon proxy flies to the hand
    //          (ExecutionDirector decides that from the live distance).
    //   Bands (labels only): A d <= 0.50, B <= 1.25, C beyond.
    // d is the grip point depth along the camera forward at E. Static; allocation-free except the one-off WeaponViews.
    public static class ExecutionReachResolver
    {
        public enum Band { A, B, C }
        public struct Result
        {
            public Band Band;
            public float Depth;
            public float PullDistance, PullSeconds;
            public float StepDistance, StepSeconds;
            public Vector3 StepDirection;
            public float BodyClearance; // nearest camera -> body collider distance at E
        }
        public const float DirectDepth=.50f, PullBandMax=1.20f, TargetDepth=.40f, /* coordinator: .45 -> .40 so the hand reaches tall robots (T-07 Mech) */ MaxPull=.80f, PullTime=.18f, MaxStep=.45f, StepTime=.12f;
        public const float ReachSlack=.50f, BodyMargin=.40f;
        static readonly RaycastHit[] hits=new RaycastHit[24];

        // Renderers of the weapon that is mounted on the robot (hidden on the Rip beat).
        public static Renderer[] WeaponViews(EnemyExecutionSource source)
        {
            var list=new List<Renderer>(8);
            if(!source)return list.ToArray();
            var saw=source.GetComponent<SawBotWeaponSource>();
            if(saw&&saw.RightSawRenderers!=null)AddAll(list,saw.RightSawRenderers);
            else
            {
                var big=source.GetComponent<BigBotDestruction>();
                if(big&&big.CannonViews!=null)AddAll(list,big.CannonViews);
                else
                {
                    Transform mounted=null;
                    foreach(var cannon in source.GetComponentsInChildren<StolenCannon>(true))if(cannon.State==StolenCannonState.Attached){mounted=cannon.transform;break;}
                    if(!mounted)foreach(var t in source.GetComponentsInChildren<Transform>(true))if(t.name=="CannonModule"||t.name=="VPD_CannonModule"||t.name=="T07Mech_Cannon"){mounted=t;break;}
                    if(mounted)AddAll(list,mounted.GetComponentsInChildren<Renderer>(true));
                }
            }
            return list.ToArray();
        }
        static void AddAll(List<Renderer> list,Renderer[] views){foreach(var r in views)if(r&&!(r is ParticleSystemRenderer))list.Add(r);}

        // Closest point of the weapon's surface (renderer bounds) to a world point; the joint when the weapon views are unknown.
        public static Vector3 ClosestWeaponPoint(Vector3 from,Renderer[] views,Transform joint)
        {
            float best=float.MaxValue;Vector3 point=joint?joint.position:from;
            if(views!=null)
                foreach(var r in views)
                {
                    if(!r)continue;
                    var p=r.bounds.ClosestPoint(from);float d=(p-from).sqrMagnitude;
                    if(d<best){best=d;point=p;}
                }
            return point;
        }

        // Big Bot: its cannon IS the right forearm (joint -> Pincer_R, the cannon front used by BigBotDestruction). Its skinned
        // renderer bounds span the whole robot, so the grip aims at the bone segment instead: the point closest to `from`, kept
        // between 20 % and 80 % of the segment (never the elbow, never the claw tip). False when the joint has no Pincer_R.
        public static bool CannonArmPoint(Transform joint,Vector3 from,out Vector3 point)
        {
            point=joint?joint.position:from;
            var tip=joint?joint.Find("Pincer_R"):null;if(!tip)return false;
            var ab=tip.position-point;if(ab.sqrMagnitude<1e-4f)return false;
            point+=ab*Mathf.Clamp(Vector3.Dot(from-point,ab)/ab.sqrMagnitude,.2f,.8f);
            return true;
        }

        // Nearest distance from a point to the robot's BODY colliders (the weapon and its own arm are excluded).
        public static float BodyDistance(Vector3 from,Collider[] robotColliders,Transform joint,Renderer[] weaponViews,out Vector3 nearest)
        {
            float best=float.MaxValue;nearest=from;
            if(robotColliders==null)return 99;
            foreach(var c in robotColliders)
            {
                if(!c||!c.enabled||c.isTrigger||IsWeaponCollider(c,joint,weaponViews))continue;
                var p=(c is MeshCollider m&&!m.convex)?c.bounds.ClosestPoint(from):c.ClosestPoint(from);
                float d=Vector3.Distance(from,p);
                if(d<best){best=d;nearest=p;}
            }
            return best==float.MaxValue?99:best;
        }
        public static bool IsWeaponCollider(Collider c,Transform joint,Renderer[] weaponViews)
        {
            if(joint&&c.transform.IsChildOf(joint))return true;
            if(weaponViews!=null)foreach(var r in weaponViews)if(r&&c.transform.IsChildOf(r.transform))return true;
            return false;
        }

        public static Result Resolve(Camera camera,CharacterController body,Transform joint,Renderer[] weaponViews,Collider[] robotColliders,bool grounded)
        {
            var result=new Result{Band=Band.A,PullSeconds=PullTime,StepSeconds=StepTime,BodyClearance=99};
            if(!camera||!(joint||(weaponViews!=null&&weaponViews.Length>0)))return result;
            var cam=camera.transform;
            var grip=ClosestWeaponPoint(cam.position,weaponViews,joint);
            float d=cam.InverseTransformPoint(grip).z;
            if(d<=0)d=Vector3.Distance(cam.position,grip);
            result.Depth=d;
            result.Band=d<=DirectDepth?Band.A:d<=PullBandMax?Band.B:Band.C;
            float clearance=BodyDistance(cam.position,robotColliders,joint,weaponViews,out _);
            result.BodyClearance=clearance;
            float room=Mathf.Max(0,clearance-BodyMargin);
            float pull=Mathf.Clamp(Mathf.Min(d-TargetDepth,MaxPull,room),0,MaxPull);
            result.PullDistance=pull;
            float need=d-pull-ReachSlack;
            if(need>.02f&&grounded&&body)
            {
                var flat=Vector3.ProjectOnPlane(grip-cam.position,Vector3.up);
                if(flat.sqrMagnitude>1e-4f)
                {
                    flat.Normalize();
                    float want=Mathf.Min(MaxStep,need,Mathf.Max(0,room-pull)); // the step shares the same 0.40 m body clearance
                    want=Clear(body,flat,want,robotColliders);
                    if(want>.02f){result.StepDistance=want;result.StepDirection=flat;}
                }
            }
            return result;
        }

        // Longest distance <= wanted that the player capsule can travel along dir (the robot's own colliders do not block).
        static float Clear(CharacterController body,Vector3 dir,float wanted,Collider[] robotColliders)
        {
            if(wanted<=0)return 0;
            float scale=Mathf.Max(body.transform.lossyScale.y,.01f);
            float radius=body.radius*scale*.9f;
            float half=Mathf.Max(body.height*scale*.5f-radius,0);
            var center=body.transform.TransformPoint(body.center)+Vector3.up*.08f; // lifted so the floor never blocks the cast
            var p1=center+Vector3.up*half;var p2=center-Vector3.up*half;
            int count=Physics.CapsuleCastNonAlloc(p1,p2,radius,dir,hits,wanted+.05f,-1,QueryTriggerInteraction.Ignore);
            float nearest=wanted+.05f;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];var c=hit.collider;
                if(!c||c.transform.IsChildOf(body.transform)||IsRobot(c,robotColliders))continue;
                if(hit.distance<nearest)nearest=hit.distance;
            }
            return Mathf.Clamp(nearest-.05f,0,wanted);
        }

        static bool IsRobot(Collider c,Collider[] robotColliders)
        {
            if(robotColliders==null)return false;
            foreach(var r in robotColliders)if(r==c)return true;
            return false;
        }
    }
}
