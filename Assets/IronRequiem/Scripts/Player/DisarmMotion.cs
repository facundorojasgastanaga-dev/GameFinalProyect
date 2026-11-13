using System;
using UnityEngine;

namespace IronRequiem
{
    // Timeline for the left-arm "grab, wrench the weapon off, then punch the robot" presentation.
    // Plain data + math; the owner (ProtagonistHandsPresentation) steps it, fits the arm and fires camera
    // kicks on the beats. All points are in camera-local metres. Field names are versioned (v2) so values
    // serialized by the first, faster version are not reused.
    [Serializable]
    public sealed class DisarmMotion
    {
        public enum Beat { None, Reach, Strain, Yank, WindUp, Strike, Impact, Recover, Done }
        [Min(.02f)] public float reachTimeV2=.18f, strainTimeV2=.16f, yankTimeV2=.18f, windUpTimeV2=.14f, strikeTimeV2=.09f, impactTimeV2=.08f, recoverTimeV2=.34f;
        [Tooltip("Hand shake amplitude while straining against the joint (metres).")]
        public float strainShakeV2=.012f;
        public Vector3 pullPointV2=new Vector3(-.19f,-.21f,.24f);
        public Vector3 windUpPointV2=new Vector3(-.26f,-.17f,.16f);
        public Vector3 strikePointV2=new Vector3(-.03f,-.08f,.80f);
        public Vector3 reachElbowV2=new Vector3(-.30f,-.40f,.10f);
        public Vector3 pullElbowV2=new Vector3(-.38f,-.42f,-.06f);
        public Vector3 windUpElbowV2=new Vector3(-.42f,-.30f,-.10f);
        public Vector3 strikeElbowV2=new Vector3(-.20f,-.34f,.26f);
        public Vector3 restElbowV2=new Vector3(-.24f,-.45f,.02f);
        float elapsed=-1;
        Vector3 grip;
        Beat last;
        public bool Active=>elapsed>=0;
        float StrainEnd=>reachTimeV2+strainTimeV2;
        float YankEnd=>StrainEnd+yankTimeV2;
        float WindUpEnd=>YankEnd+windUpTimeV2;
        float StrikeEnd=>WindUpEnd+strikeTimeV2;
        float ImpactEnd=>StrikeEnd+impactTimeV2;
        public float Duration=>ImpactEnd+recoverTimeV2;
        // Moment the fist lands; the robot knockback is scheduled for it.
        public float StrikeTime=>WindUpEnd+strikeTimeV2*.8f;
        public float YankTime=>StrainEnd;
        public Vector3 Grip=>grip;

        public void Begin(Vector3 gripLocal)
        {
            // Keep the grip inside a believable arm envelope even if the joint is off-axis or far away.
            grip=new Vector3(Mathf.Clamp(gripLocal.x,-.38f,.12f),Mathf.Clamp(gripLocal.y,-.32f,.06f),Mathf.Clamp(gripLocal.z,.42f,.78f));
            elapsed=0;last=Beat.None;
        }
        public void Cancel(){elapsed=-1;last=Beat.None;}

        Beat At(float t)=>t<reachTimeV2?Beat.Reach:t<StrainEnd?Beat.Strain:t<YankEnd?Beat.Yank:t<WindUpEnd?Beat.WindUp:
            t<StrikeEnd?Beat.Strike:t<ImpactEnd?Beat.Impact:t<Duration?Beat.Recover:Beat.Done;

        // Advances time and returns the furthest beat entered this step (None when unchanged).
        public Beat Step(float dt)
        {
            if(!Active)return Beat.None;
            elapsed+=dt;
            var now=At(elapsed);
            if(now==Beat.Done){Cancel();return Beat.Done;}
            if(now==last)return Beat.None;
            last=now;return now;
        }

        // weight blends from the owner's normal wrist/elbow to the disarm pose and back.
        public void Evaluate(out Vector3 wrist,out Vector3 elbow,out float weight,out bool closedHand)
        {
            float t=Mathf.Max(0,elapsed);
            closedHand=t>=reachTimeV2*.85f;
            weight=1;
            switch(At(t))
            {
                case Beat.Reach:
                {
                    float k=EaseOut(t/reachTimeV2);
                    wrist=grip;elbow=Vector3.Lerp(restElbowV2,reachElbowV2,k);weight=k;return;
                }
                case Beat.Strain:
                {
                    // Locked on the joint: small back-pull and a tremble as the arm fights the mount.
                    float k=(t-reachTimeV2)/strainTimeV2;
                    var shake=new Vector3(Mathf.Sin(t*71),Mathf.Sin(t*53+1.3f),0)*strainShakeV2*Mathf.Sin(k*Mathf.PI);
                    wrist=Vector3.Lerp(grip,pullPointV2,.12f*k)+shake;elbow=Vector3.Lerp(reachElbowV2,pullElbowV2,.25f*k);return;
                }
                case Beat.Yank:
                {
                    float k=EaseIn((t-StrainEnd)/yankTimeV2);
                    var from=Vector3.Lerp(grip,pullPointV2,.12f);
                    wrist=Vector3.Lerp(from,pullPointV2,k);elbow=Vector3.Lerp(Vector3.Lerp(reachElbowV2,pullElbowV2,.25f),pullElbowV2,k);return;
                }
                case Beat.WindUp:
                {
                    float k=EaseInOut((t-YankEnd)/windUpTimeV2);
                    wrist=Vector3.Lerp(pullPointV2,windUpPointV2,k);elbow=Vector3.Lerp(pullElbowV2,windUpElbowV2,k);return;
                }
                case Beat.Strike:
                {
                    float k=EaseIn((t-WindUpEnd)/strikeTimeV2);
                    wrist=Vector3.Lerp(windUpPointV2,strikePointV2,k);elbow=Vector3.Lerp(windUpElbowV2,strikeElbowV2,k);return;
                }
                case Beat.Impact:
                    wrist=strikePointV2-Vector3.forward*.02f*Mathf.Sin((t-StrikeEnd)/impactTimeV2*Mathf.PI);elbow=strikeElbowV2;return;
                default:
                {
                    float r=Mathf.Clamp01((t-ImpactEnd)/recoverTimeV2);
                    wrist=strikePointV2;elbow=strikeElbowV2;weight=1-EaseInOut(r);return;
                }
            }
        }
        static float EaseIn(float t)=>t*t*t;
        static float EaseOut(float t)=>1-(1-t)*(1-t)*(1-t);
        static float EaseInOut(float t)=>t*t*(3-2*t);
    }
}
