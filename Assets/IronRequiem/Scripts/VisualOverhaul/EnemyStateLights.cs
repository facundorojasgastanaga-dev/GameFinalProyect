using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    // Visual overhaul (07/11/2026): eye / status light colour per AI state, read-only on the existing AI.
    //   Patrol  (no known target)            cold teal
    //   Alert   (target known, out of range)  amber
    //   Attack  (target in attack range)      red
    //   Warning (shot imminent)               white-hot blink: the T-07 charge (EnemyChargeFraction) or, for weapons
    //                                         without charge, the last part of DelayBetweenShots after the last attack.
    //   Dead                                  off (EnemyPowerIndicators also disables the renderers on death).
    // Never changes behaviour, damage or AI. MaterialPropertyBlock only, written on change (blink: while warning).
    [RequireComponent(typeof(EnemyController))]
    public sealed class EnemyStateLights : MonoBehaviour
    {
        public enum LightState { Patrol, Alert, Attack, Warning, Dead }
        [SerializeField] Renderer[] lights = new Renderer[0];
        [SerializeField, ColorUsage(false, true)] Color patrol = new Color(.1f, 1.4f, 1.2f), alert = new Color(2.2f, 1.1f, .08f), attack = new Color(2.8f, .12f, .05f), warning = new Color(5f, 3.6f, 3f);
        [SerializeField, Min(0)] float warningLead = .35f, blinkHz = 14f;
        public LightState State { get; private set; }
        public Renderer[] Lights => lights;
        public int StateChanges { get; private set; }
        public Color CurrentColor { get; private set; }
        public void Configure(Renderer[] renderers) => lights = renderers;

        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock block;
        EnemyController enemy; Health health; WeaponController weapon; float lastAttack = -10; bool dead; Color applied = new Color(-1, -1, -1, -1);

        void Awake()
        {
            enemy = GetComponent<EnemyController>(); health = GetComponent<Health>();
            if (block == null) block = new MaterialPropertyBlock();
        }
        void OnEnable()
        {
            if (VisualOverhaulAblation.Has("states")) { enabled = false; return; }
            dead = false; State = LightState.Patrol; applied = new Color(-1, -1, -1, -1);
            enemy.onAttack += OnAttack; if (health) health.OnDie += OnDie;
        }
        void OnDisable() { Bind(null); if (!enemy) return; enemy.onAttack -= OnAttack; if (health) health.OnDie -= OnDie; }
        // The warning is timed from the weapon's real shots (not from AI attack requests, which can repeat without firing).
        void OnAttack() => lastAttack = Time.time; // EnemyController raises onAttack only when a shot was really fired
        void OnShot() => lastAttack = Time.time;
        void Bind(WeaponController w) { if (weapon == w) return; if (weapon) weapon.OnShootProcessed -= OnShot; weapon = w; if (weapon) weapon.OnShootProcessed += OnShot; }
        void OnDie() { dead = true; Set(LightState.Dead, Color.black); }

        void Update()
        {
            if (dead) return;
            if (!enemy || !enemy.isActiveAndEnabled) { if (!enemy) { dead = true; Set(LightState.Dead, Color.black); } return; } // controller removed on death
            var detection = enemy.DetectionModule;
            LightState next = !detection || !enemy.HadKnownTarget ? LightState.Patrol : enemy.IsTargetInAttackRange ? LightState.Attack : LightState.Alert;
            if (next == LightState.Attack && ShotImminent()) next = LightState.Warning;
            Color c = next == LightState.Patrol ? patrol : next == LightState.Alert ? alert : next == LightState.Attack ? attack
                : Color.Lerp(attack, warning, Mathf.PingPong(Time.time * blinkHz, 1f));
            Set(next, c);
        }

        bool ShotImminent()
        {
            if (enemy.EnemyChargeDuration > 0) return enemy.EnemyChargeFraction > 0;
            Bind(enemy.GetCurrentWeapon()); // the weapon the AI actually fires (robots carry several WeaponControllers)
            if (!weapon) return false;
            float since = Time.time - lastAttack;
            return since >= weapon.DelayBetweenShots - warningLead && since < weapon.DelayBetweenShots + .6f;
        }

        void Set(LightState next, Color c)
        {
            if (next != State) { State = next; StateChanges++; }
            CurrentColor = c;
            if ((c - applied).maxColorComponent < .02f && (applied - c).maxColorComponent < .02f) return;
            applied = c;
            foreach (var r in lights)
            {
                if (!r) continue;
                r.GetPropertyBlock(block); block.SetColor(Emission, c); block.SetColor(BaseColor, new Color(Mathf.Min(1, c.r), Mathf.Min(1, c.g), Mathf.Min(1, c.b), 1)); r.SetPropertyBlock(block);
            }
        }
    }
}
