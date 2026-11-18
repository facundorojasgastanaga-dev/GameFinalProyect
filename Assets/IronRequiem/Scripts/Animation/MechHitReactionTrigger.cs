using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    // Presentation only (robot anim test, 17/11/2026). The T-07 Mech's EnemyMobile leaves damage to external
    // presentation (UseExternalDamagePresentation), so nothing fires the Animator's OnDamaged reaction. This
    // fires it, rate-limited, and briefly hands the cannon arm from T07CannonSocket's aim to the clip; it also
    // yields the arm right after each shot so the recoil clip reads. No damage, AI or navigation changes.
    [DisallowMultipleComponent]
    public sealed class MechHitReactionTrigger : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] T07CannonSocket cannonSocket;
        [Header("Hit reaction")]
        [SerializeField, Min(0)] float minInterval = .7f;
        [SerializeField, Min(0)] float minDamage;
        [SerializeField, Min(0)] float hitArmYield = .5f;
        [Header("Shot recoil")]
        [SerializeField, Min(0)] float shotArmYield = .3f;

        static readonly int OnDamagedId = Animator.StringToHash("OnDamaged");
        Health health; EnemyMobile mobile; EnemyController enemy; T07AnimationDriver driver;
        float nextReaction; bool subscribed;

        public void Configure(Animator target, T07CannonSocket socket) { animator = target; cannonSocket = socket; }

        void Awake()
        {
            health = GetComponent<Health>(); mobile = GetComponent<EnemyMobile>(); enemy = GetComponent<EnemyController>(); driver = GetComponent<T07AnimationDriver>();
            if (!animator && driver) animator = driver.Animator;
            if (!cannonSocket) cannonSocket = GetComponentInChildren<T07CannonSocket>(true);
        }

        void OnEnable()
        {
            if (subscribed) return;
            if (health) health.OnDamaged += Damaged;
            if (enemy) enemy.onAttack += Fired;
            subscribed = true;
        }

        void OnDisable()
        {
            if (!subscribed) return;
            if (health) health.OnDamaged -= Damaged;
            if (enemy) enemy.onAttack -= Fired;
            subscribed = false;
        }

        void Damaged(float amount, GameObject source)
        {
            if (!animator || !isActiveAndEnabled) return;
            if (mobile && !mobile.UseExternalDamagePresentation) return; // EnemyMobile already fires OnDamaged
            if (health.CurrentHealth <= 0 || (driver && driver.IsDead)) return;
            if (amount < minDamage || Time.time < nextReaction) return;
            nextReaction = Time.time + minInterval;
            animator.SetTrigger(OnDamagedId);
            if (cannonSocket) cannonSocket.YieldAim(hitArmYield);
        }

        void Fired()
        {
            if (cannonSocket && isActiveAndEnabled && !(driver && driver.IsDead)) cannonSocket.YieldAim(shotArmYield);
        }
    }
}
