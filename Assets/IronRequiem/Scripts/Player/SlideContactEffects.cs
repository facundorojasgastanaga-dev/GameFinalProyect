using Unity.FPS.Gameplay;
using UnityEngine;
namespace IronRequiem
{
    [DefaultExecutionOrder(130)]
    [RequireComponent(typeof(AdvancedPlayerMovement))]
    public sealed class SlideContactEffects : MonoBehaviour
    {
        [SerializeField] ParticleSystem contactParticles;
        [SerializeField] ParticleSystem secondContact;
        [SerializeField,Min(.1f)] float rayLength=1.2f;
        PlayerCharacterController player;AdvancedPlayerMovement movement;MechanicInput input;
        readonly RaycastHit[] groundHits=new RaycastHit[12];
        public ParticleSystem ContactParticles=>contactParticles;
        public ParticleSystem SecondContact=>secondContact;
        public void Configure(ParticleSystem particles)=>contactParticles=particles;
        public void ConfigureContacts(ParticleSystem left,ParticleSystem right){contactParticles=left;secondContact=right;}
        void Awake(){player=GetComponent<PlayerCharacterController>();movement=GetComponent<AdvancedPlayerMovement>();input=GetComponent<MechanicInput>();}
        void LateUpdate()
        {
            bool sliding=movement.IsSliding&&player.IsGrounded&&!player.HasJumpedThisFrame&&!player.IsDead&&input.CanAct;
            UpdateContact(contactParticles,secondContact?-.18f:0,sliding);UpdateContact(secondContact,.18f,sliding);
        }
        void UpdateContact(ParticleSystem particles,float side,bool sliding)
        {
            if(!particles)return;
            Vector3 direction=Vector3.ProjectOnPlane(player.CharacterVelocity,Vector3.up).normalized;if(direction.sqrMagnitude<.01f)direction=transform.forward;
            var right=Vector3.Cross(Vector3.up,direction);Vector3 origin=transform.position+direction*(secondContact?(side<0?.45f:.9f):.65f)+right*side+Vector3.up*.65f;
            int count=sliding?Physics.RaycastNonAlloc(origin,Vector3.down,groundHits,rayLength,player.GroundCheckLayers,QueryTriggerInteraction.Ignore):0;
            float nearest=float.PositiveInfinity;RaycastHit contact=default;bool found=false;
            for(int i=0;i<count;i++)if(!groundHits[i].collider.transform.IsChildOf(transform)&&groundHits[i].normal.y>.2f&&groundHits[i].distance<nearest){contact=groundHits[i];nearest=contact.distance;found=true;}
            if(!found){Stop(particles);return;}
            Vector3 backward=Vector3.ProjectOnPlane(-direction,contact.normal).normalized;
            particles.transform.SetPositionAndRotation(contact.point+contact.normal*.018f,Quaternion.LookRotation((backward+contact.normal*.28f).normalized,contact.normal));
            if(!particles.isEmitting)particles.Play(true);
        }
        static void Stop(ParticleSystem particles){if(particles&&(particles.isPlaying||particles.particleCount>0))particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        void OnDisable(){Stop(contactParticles);Stop(secondContact);}
    }
}
