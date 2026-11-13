using UnityEngine;

namespace IronRequiem
{
    [DefaultExecutionOrder(110)]
    public sealed class SawMotorPresentation : MonoBehaviour
    {
        [SerializeField] SawBotCombat enemy;
        [SerializeField] SawBotWeaponSource extraction;
        [SerializeField] StolenSawAttack playerAttack;
        [SerializeField] SawChainMotion[] chains;
        [SerializeField] ParticleSystem[] smoke;
        [SerializeField] AudioSource motor;
        bool running;
        public bool IsRunning => running;
        public ParticleSystem[] Smoke => smoke;
        public AudioSource Motor => motor;
        public void Configure(SawBotCombat bot, SawBotWeaponSource source, StolenSawAttack attack, SawChainMotion[] loops, ParticleSystem[] exhaust, AudioSource audio)
        { enemy = bot; extraction = source; playerAttack = attack; chains = loops; smoke = exhaust; motor = audio; }
        void LateUpdate()
        {
            bool active = Time.timeScale > 0 && ((enemy && enemy.IsAttacking && !enemy.IsDead && (!extraction || !extraction.WeaponRemoved)) || (playerAttack && playerAttack.IsRunning));
            if (active != running)
            {
                running = active;
                foreach (var particles in smoke) if (particles)
                { if (active) particles.Play(false); else particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
                if (motor) { if (active) motor.Play(); else motor.Stop(); }
            }
            foreach (var chain in chains) if (chain) chain.Advance(active);
        }
        void OnDisable()
        {
            running = false;
            if (motor) motor.Stop();
            if (smoke != null) foreach (var particles in smoke) if (particles) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
