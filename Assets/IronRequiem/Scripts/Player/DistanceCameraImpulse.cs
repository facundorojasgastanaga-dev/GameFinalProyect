using System.Collections.Generic;
using UnityEngine;

namespace IronRequiem
{
    [DefaultExecutionOrder(-300)]
    public sealed class DistanceCameraImpulse : MonoBehaviour
    {
        [SerializeField, Min(0)] float maximumTranslation=.06f;
        [SerializeField, Min(0)] float maximumRotation=1.2f;
        [Header("Directional kick (melee, disarm)")]
        [SerializeField, Range(8,60)] float kickFrequency=26f;
        [SerializeField, Range(.2f,1.2f)] float kickDamping=.7f;
        [SerializeField, Min(0)] float maximumKickRotation=6f;
        [SerializeField, Min(0)] float maximumKickTranslation=.08f;
        static readonly List<DistanceCameraImpulse> Cameras=new List<DistanceCameraImpulse>(2);
        Vector3 basePosition;
        Quaternion baseRotation;
        bool applied;
        float remaining,duration,strength;
        Vector3 kickAngle,kickAngleVelocity,kickOffset,kickOffsetVelocity;
        public bool IsShaking => remaining>0;
        // Directional kicks are reported separately so distance-shake checks keep their meaning.
        public bool IsKicking => kickAngle.sqrMagnitude>1e-4f||kickAngleVelocity.sqrMagnitude>1e-3f||kickOffset.sqrMagnitude>1e-8f;
        public Vector3 KickAngle => kickAngle;
        public Vector3 CurrentOffset {get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetRegistry()=>Cameras.Clear();
        void OnEnable(){if(!Cameras.Contains(this))Cameras.Add(this);}
        void OnDisable(){Restore();Cameras.Remove(this);remaining=0;kickAngle=kickAngleVelocity=kickOffset=kickOffsetVelocity=Vector3.zero;}
        void Update()=>Restore();
        public static void Emit(Vector3 position,float radius,float amplitude,float seconds)
        {
            if(Cameras.Count==0)
            {
                var main=Camera.main;
                if(main && !main.GetComponent<DistanceCameraImpulse>())main.gameObject.AddComponent<DistanceCameraImpulse>();
            }
            foreach(var camera in Cameras)
            {
                if(!camera || !camera.isActiveAndEnabled)continue;
                float falloff=Mathf.Clamp01(1-Vector3.Distance(camera.transform.position,position)/Mathf.Max(.1f,radius));
                if(falloff<=0)continue;
                camera.strength=Mathf.Clamp01(camera.strength+amplitude*falloff*falloff);
                camera.duration=Mathf.Max(.01f,seconds);camera.remaining=camera.duration;
            }
        }
        // Spring-driven kick on a specific camera. euler: approximate peak degrees (x pitch, y yaw, z roll);
        // offset: approximate peak local translation in metres. Additive and self-settling.
        public static void Kick(Camera target,Vector3 euler,Vector3 offset)
        {
            if(!target)return;
            var impulse=target.GetComponent<DistanceCameraImpulse>();
            if(!impulse)impulse=target.gameObject.AddComponent<DistanceCameraImpulse>();
            impulse.AddKick(euler,offset);
        }
        public void AddKick(Vector3 euler,Vector3 offset)
        {
            if(!isActiveAndEnabled)return;
            // For an underdamped spring an initial velocity v peaks near v/(omega*1.7).
            float gain=kickFrequency*1.7f;
            kickAngleVelocity+=euler*gain;kickOffsetVelocity+=offset*gain;
        }
        void LateUpdate()
        {
            if(Time.timeScale<=0)return;
            bool shaking=remaining>0;
            if(!shaking&&!IsKicking)return;
            basePosition=transform.localPosition;baseRotation=transform.localRotation;
            Vector3 translation=Vector3.zero,rotation=Vector3.zero;
            if(shaking)
            {
                float envelope=Mathf.Pow(remaining/duration,2)*strength;
                float t=Time.time*34;
                Vector3 noise=new Vector3(Mathf.PerlinNoise(t,7)-.5f,Mathf.PerlinNoise(11,t)-.5f,Mathf.PerlinNoise(t,21)-.5f)*2;
                translation=noise*(maximumTranslation*envelope);rotation=noise*(maximumRotation*envelope);
                remaining=Mathf.Max(0,remaining-Time.deltaTime);
                if(remaining==0)strength=0;
            }
            StepKick(Time.deltaTime);
            translation+=kickOffset;rotation+=kickAngle;
            // F4: player option (Opciones > Temblor de pantalla), 0 = off.
            float feel=GameFeel.ShakeScale;translation*=feel;rotation*=feel;
            CurrentOffset=translation;
            transform.localPosition=basePosition+translation;
            transform.localRotation=baseRotation*Quaternion.Euler(rotation);
            applied=true;
        }
        void StepKick(float dt)
        {
            float k=kickFrequency*kickFrequency,c=2*kickDamping*kickFrequency;
            // Semi-implicit Euler with a few sub-steps keeps the stiff spring stable at low frame rates.
            int steps=Mathf.Clamp(Mathf.CeilToInt(dt/.008f),1,8);float h=dt/steps;
            for(int i=0;i<steps;i++)
            {
                kickAngleVelocity+=(-kickAngle*k-kickAngleVelocity*c)*h;kickAngle+=kickAngleVelocity*h;
                kickOffsetVelocity+=(-kickOffset*k-kickOffsetVelocity*c)*h;kickOffset+=kickOffsetVelocity*h;
            }
            kickAngle=Vector3.ClampMagnitude(kickAngle,maximumKickRotation);kickOffset=Vector3.ClampMagnitude(kickOffset,maximumKickTranslation);
            if(!IsKicking){kickAngle=kickAngleVelocity=kickOffset=kickOffsetVelocity=Vector3.zero;}
        }
        void Restore()
        {
            if(applied){transform.localPosition=basePosition;transform.localRotation=baseRotation;applied=false;}
            CurrentOffset=Vector3.zero;
        }
    }
}
