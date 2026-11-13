using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(Health), typeof(Actor), typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour
    {
        [System.Serializable]
        public struct RendererIndexData
        {
            public Renderer Renderer;
            public int MaterialIndex;

            public RendererIndexData(Renderer renderer, int index)
            {
                Renderer = renderer;
                MaterialIndex = index;
            }
        }

        [Header("Parameters")]
        [Tooltip("The Y height at which the enemy will be automatically killed (if it falls off of the level)")]
        public float SelfDestructYHeight = -20f;

        [Tooltip("The distance at which the enemy considers that it has reached its current path destination point")]
        public float PathReachingRadius = 2f;

        [Tooltip("Skip NavMeshAgent.SetDestination when the requested point moved less than this (metres) and the agent already has a path. 0 keeps the template behaviour (a request every frame).")]
        public float DestinationUpdateThreshold = 0f;

        [Tooltip("With a threshold, the destination is still refreshed at least this often (seconds).")]
        public float DestinationRefreshInterval = .25f;

        [Tooltip("The speed at which the enemy rotates")]
        public float OrientationSpeed = 10f;

        [Tooltip("Delay after death where the GameObject is destroyed (to allow for animation)")]
        public float DeathDuration = 0f;


        [Header("Weapons Parameters")] [Tooltip("Allow weapon swapping for this enemy")]
        public bool SwapToNextWeapon = false;
        [Tooltip("Allow an actor policy to provide melee attacks without a projectile weapon.")]
        public bool AllowWeaponlessPolicy;
        [Tooltip("Require current detection and a clear muzzle-to-target path before each projectile.")]
        public bool RequireVisibleTargetToShoot;
        [Tooltip("Optional enemy windup before a single charged projectile; zero preserves the original attack.")]
        [Min(0)] public float EnemyChargeDuration;
        float enemyChargeStarted = -1, nextEnemyCharge;
        public float EnemyChargeFraction => enemyChargeStarted < 0 || EnemyChargeDuration <= 0 ? 0 : Mathf.Clamp01((Time.time-enemyChargeStarted)/EnemyChargeDuration);
        public void CancelEnemyCharge() { enemyChargeStarted = -1; chargeBlockedSince = -1; }
        [Tooltip("Iron Requiem: once a charge has started, line of sight may be lost for this long (target ducking behind cover, an ally crossing) before the charge is cancelled. A charge that completes while blocked is held and fires as soon as the shot is clear.")]
        [Min(0)] public float EnemyChargeGrace = .45f;
        float chargeBlockedSince = -1;
        bool IsCharging => enemyChargeStarted >= 0;
        // Returns true when the charge was cancelled because it stayed blocked longer than the grace.
        bool BlockCharge()
        {
            if (!IsCharging) return false;
            if (chargeBlockedSince < 0) chargeBlockedSince = Time.time;
            if (Time.time - chargeBlockedSince <= EnemyChargeGrace) return false;
            CancelEnemyCharge(); return true;
        }
        readonly RaycastHit[] m_AttackVisibilityHits = new RaycastHit[32];

        [Tooltip("Time delay between a weapon swap and the next attack")]
        public float DelayAfterWeaponSwap = 0f;

        [Header("Eye color")] [Tooltip("Material for the eye color")]
        public Material EyeColorMaterial;

        [Tooltip("The default color of the bot's eye")] [ColorUsageAttribute(true, true)]
        public Color DefaultEyeColor;

        [Tooltip("The attack color of the bot's eye")] [ColorUsageAttribute(true, true)]
        public Color AttackEyeColor;

        [Header("Flash on hit")] [Tooltip("The material used for the body of the hoverbot")]
        public Material BodyMaterial;

        [Tooltip("The gradient representing the color of the flash on hit")] [GradientUsageAttribute(true)]
        public Gradient OnHitBodyGradient;

        [Tooltip("The duration of the flash on hit")]
        public float FlashOnHitDuration = 0.5f;

        [Header("Sounds")] [Tooltip("Sound played when recieving damages")]
        public AudioClip DamageTick;

        [Header("VFX")] [Tooltip("The VFX prefab spawned when the enemy dies")]
        public GameObject DeathVfx;

        [Tooltip("The point at which the death VFX is spawned")]
        public Transform DeathVfxSpawnPoint;

        [Header("Loot")] [Tooltip("The object this enemy can drop when dying")]
        public GameObject LootPrefab;

        [Tooltip("The chance the object has to drop")] [Range(0, 1)]
        public float DropRate = 1f;

        [Header("Debug Display")] [Tooltip("Color of the sphere gizmo representing the path reaching range")]
        public Color PathReachingRangeColor = Color.yellow;

        [Tooltip("Color of the sphere gizmo representing the attack range")]
        public Color AttackRangeColor = Color.red;

        [Tooltip("Color of the sphere gizmo representing the detection range")]
        public Color DetectionRangeColor = Color.blue;

        public UnityAction onAttack;
        public UnityAction onDetectedTarget;
        public UnityAction onLostTarget;
        public UnityAction onDamaged;

        List<RendererIndexData> m_BodyRenderers = new List<RendererIndexData>();
        MaterialPropertyBlock m_BodyFlashMaterialPropertyBlock;
        float m_LastTimeDamaged = float.NegativeInfinity;

        RendererIndexData m_EyeRendererData;
        MaterialPropertyBlock m_EyeColorMaterialPropertyBlock;

        public PatrolPath PatrolPath { get; set; }
        public GameObject KnownDetectedTarget => DetectionModule.KnownDetectedTarget;
        public bool IsTargetInAttackRange => DetectionModule.IsTargetInAttackRange;
        public bool IsSeeingTarget => DetectionModule.IsSeeingTarget;
        public bool HadKnownTarget => DetectionModule.HadKnownTarget;
        public NavMeshAgent NavMeshAgent { get; private set; }

        // IRON REQUIEM v4: short stagger from a heavy melee hit. No attacks and no path following until it expires.
        public float StaggeredUntil { get; private set; }
        public bool IsStaggered => Time.time < StaggeredUntil;
        bool m_Staggering;
        public void Stagger(float seconds)
        {
            if (seconds <= 0f || !m_Health || m_Health.CurrentHealth <= 0f) return;
            StaggeredUntil = Mathf.Max(StaggeredUntil, Time.time + seconds);
            m_Staggering = true;
            CancelEnemyCharge();
            if (NavMeshAgent && NavMeshAgent.enabled && NavMeshAgent.isOnNavMesh) { NavMeshAgent.velocity = Vector3.zero; NavMeshAgent.isStopped = true; }
        }
        public DetectionModule DetectionModule { get; private set; }

        int m_PathDestinationNodeIndex;
        EnemyManager m_EnemyManager;
        ActorsManager m_ActorsManager;
        Health m_Health;
        Actor m_Actor;
        Collider[] m_SelfColliders;
        GameFlowManager m_GameFlowManager;
        bool m_WasDamagedThisFrame;
        float m_LastTimeWeaponSwapped = Mathf.NegativeInfinity;
        int m_CurrentWeaponIndex;
        WeaponController m_CurrentWeapon;
        WeaponController[] m_Weapons;
        NavigationModule m_NavigationModule;
        Vector3 m_LastNavDestination;
        float m_LastNavDestinationTime;
        bool m_HasNavDestination;

        void Start()
        {
            m_EnemyManager = FindAnyObjectByType<EnemyManager>();
            DebugUtility.HandleErrorIfNullFindObject<EnemyManager, EnemyController>(m_EnemyManager, this);

            m_ActorsManager = FindAnyObjectByType<ActorsManager>();
            DebugUtility.HandleErrorIfNullFindObject<ActorsManager, EnemyController>(m_ActorsManager, this);

            m_EnemyManager.RegisterEnemy(this);

            m_Health = GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, EnemyController>(m_Health, this, gameObject);

            m_Actor = GetComponent<Actor>();
            DebugUtility.HandleErrorIfNullGetComponent<Actor, EnemyController>(m_Actor, this, gameObject);

            NavMeshAgent = GetComponent<NavMeshAgent>();
            m_SelfColliders = GetComponentsInChildren<Collider>();

            m_GameFlowManager = FindAnyObjectByType<GameFlowManager>();
            DebugUtility.HandleErrorIfNullFindObject<GameFlowManager, EnemyController>(m_GameFlowManager, this);

            // Subscribe to damage & death actions
            m_Health.OnDie += OnDie;
            m_Health.OnDamaged += OnDamaged;

            // Find and initialize all weapons
            FindAndInitializeAllWeapons();
            var weapon = GetCurrentWeapon();
            if (weapon) weapon.ShowWeapon(true);

            var detectionModules = GetComponentsInChildren<DetectionModule>();
            DebugUtility.HandleErrorIfNoComponentFound<DetectionModule, EnemyController>(detectionModules.Length, this,
                gameObject);
            DebugUtility.HandleWarningIfDuplicateObjects<DetectionModule, EnemyController>(detectionModules.Length,
                this, gameObject);
            // Initialize detection module
            DetectionModule = detectionModules[0];
            DetectionModule.onDetectedTarget += OnDetectedTarget;
            DetectionModule.onLostTarget += OnLostTarget;
            onAttack += DetectionModule.OnAttack;

            var navigationModules = GetComponentsInChildren<NavigationModule>();
            DebugUtility.HandleWarningIfDuplicateObjects<DetectionModule, EnemyController>(detectionModules.Length,
                this, gameObject);
            // Override navmesh agent data
            if (navigationModules.Length > 0)
            {
                m_NavigationModule = navigationModules[0];
                NavMeshAgent.speed = m_NavigationModule.MoveSpeed;
                NavMeshAgent.angularSpeed = m_NavigationModule.AngularSpeed;
                NavMeshAgent.acceleration = m_NavigationModule.Acceleration;
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    if (EyeColorMaterial && renderer.sharedMaterials[i] == EyeColorMaterial)
                    {
                        m_EyeRendererData = new RendererIndexData(renderer, i);
                    }

                    if (BodyMaterial && renderer.sharedMaterials[i] == BodyMaterial)
                    {
                        m_BodyRenderers.Add(new RendererIndexData(renderer, i));
                    }
                }
            }

            m_BodyFlashMaterialPropertyBlock = new MaterialPropertyBlock();

            // Check if we have an eye renderer for this enemy
            if (m_EyeRendererData.Renderer != null)
            {
                m_EyeColorMaterialPropertyBlock = new MaterialPropertyBlock();
                m_EyeColorMaterialPropertyBlock.SetColor("_EmissionColor", DefaultEyeColor);
                m_EyeRendererData.Renderer.SetPropertyBlock(m_EyeColorMaterialPropertyBlock,
                    m_EyeRendererData.MaterialIndex);
            }
        }

        void Update()
        {
            EnsureIsWithinLevelBounds();
            if (m_Staggering && !IsStaggered)
            {
                m_Staggering = false;
                if (NavMeshAgent && NavMeshAgent.enabled && NavMeshAgent.isOnNavMesh) NavMeshAgent.isStopped = false;
            }

            DetectionModule.HandleTargetDetection(m_Actor, m_SelfColliders);
            if (EnemyChargeDuration > 0 && IsCharging)
            {
                if (!m_Health || m_Health.CurrentHealth <= 0) CancelEnemyCharge();
                else if (!IsSeeingTarget || !IsTargetInAttackRange) BlockCharge();
            }

            Color currentColor = OnHitBodyGradient.Evaluate((Time.time - m_LastTimeDamaged) / FlashOnHitDuration);
            m_BodyFlashMaterialPropertyBlock.SetColor("_EmissionColor", currentColor);
            foreach (var data in m_BodyRenderers)
            {
                if (data.Renderer)
                    data.Renderer.SetPropertyBlock(m_BodyFlashMaterialPropertyBlock, data.MaterialIndex);
            }

            m_WasDamagedThisFrame = false;
        }

        void EnsureIsWithinLevelBounds()
        {
            // at every frame, this tests for conditions to kill the enemy
            if (transform.position.y < SelfDestructYHeight)
            {
                Destroy(gameObject);
                return;
            }
        }

        void OnLostTarget()
        {
            onLostTarget.Invoke();

            // Set the eye attack color and property block if the eye renderer is set
            if (m_EyeRendererData.Renderer != null)
            {
                m_EyeColorMaterialPropertyBlock.SetColor("_EmissionColor", DefaultEyeColor);
                m_EyeRendererData.Renderer.SetPropertyBlock(m_EyeColorMaterialPropertyBlock,
                    m_EyeRendererData.MaterialIndex);
            }
        }

        void OnDetectedTarget()
        {
            onDetectedTarget.Invoke();

            // Set the eye default color and property block if the eye renderer is set
            if (m_EyeRendererData.Renderer != null)
            {
                m_EyeColorMaterialPropertyBlock.SetColor("_EmissionColor", AttackEyeColor);
                m_EyeRendererData.Renderer.SetPropertyBlock(m_EyeColorMaterialPropertyBlock,
                    m_EyeRendererData.MaterialIndex);
            }
        }

        public void OrientTowards(Vector3 lookPosition)
        {
            Vector3 lookDirection = Vector3.ProjectOnPlane(lookPosition - transform.position, Vector3.up).normalized;
            if (lookDirection.sqrMagnitude != 0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation =
                    Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * OrientationSpeed);
            }
        }

        bool IsPathValid()
        {
            return PatrolPath && PatrolPath.PathNodes.Count > 0;
        }

        public void ResetPathDestination()
        {
            m_PathDestinationNodeIndex = 0;
        }

        public void SetPathDestinationToClosestNode()
        {
            if (IsPathValid())
            {
                int closestPathNodeIndex = 0;
                for (int i = 0; i < PatrolPath.PathNodes.Count; i++)
                {
                    float distanceToPathNode = PatrolPath.GetDistanceToNode(transform.position, i);
                    if (distanceToPathNode < PatrolPath.GetDistanceToNode(transform.position, closestPathNodeIndex))
                    {
                        closestPathNodeIndex = i;
                    }
                }

                m_PathDestinationNodeIndex = closestPathNodeIndex;
            }
            else
            {
                m_PathDestinationNodeIndex = 0;
            }
        }

        public Vector3 GetDestinationOnPath()
        {
            if (IsPathValid())
            {
                return PatrolPath.GetPositionOfPathNode(m_PathDestinationNodeIndex);
            }
            else
            {
                return transform.position;
            }
        }

        public void SetNavDestination(Vector3 destination)
        {
            if (NavMeshAgent)
            {
                if (DestinationUpdateThreshold > 0f && m_HasNavDestination && (NavMeshAgent.hasPath || NavMeshAgent.pathPending)
                    && Time.time - m_LastNavDestinationTime < DestinationRefreshInterval
                    && (destination - m_LastNavDestination).sqrMagnitude < DestinationUpdateThreshold * DestinationUpdateThreshold)
                    return;
                m_LastNavDestination = destination; m_LastNavDestinationTime = Time.time; m_HasNavDestination = true;
                NavMeshAgent.SetDestination(destination);
            }
        }

        public void UpdatePathDestination(bool inverseOrder = false)
        {
            if (IsPathValid())
            {
                // Check if reached the path destination
                if ((transform.position - GetDestinationOnPath()).magnitude <= PathReachingRadius)
                {
                    // increment path destination index
                    m_PathDestinationNodeIndex =
                        inverseOrder ? (m_PathDestinationNodeIndex - 1) : (m_PathDestinationNodeIndex + 1);
                    if (m_PathDestinationNodeIndex < 0)
                    {
                        m_PathDestinationNodeIndex += PatrolPath.PathNodes.Count;
                    }

                    if (m_PathDestinationNodeIndex >= PatrolPath.PathNodes.Count)
                    {
                        m_PathDestinationNodeIndex -= PatrolPath.PathNodes.Count;
                    }
                }
            }
        }

        void OnDamaged(float damage, GameObject damageSource)
        {
            // test if the damage source is the player
            if (damageSource && !damageSource.GetComponent<EnemyController>())
            {
                // pursue the player
                DetectionModule.OnDamaged(damageSource);
                
                onDamaged?.Invoke();
                m_LastTimeDamaged = Time.time;
            
                // play the damage tick sound
                if (DamageTick && !m_WasDamagedThisFrame)
                    AudioUtility.CreateSFX(DamageTick, transform.position, AudioUtility.AudioGroups.DamageTick, 0f);
            
                m_WasDamagedThisFrame = true;
            }
        }

        void OnDie()
        {
            CancelEnemyCharge();
            // spawn a particle system when dying
            var presentation = GetComponent<IEnemyDeathPresentation>();
            if (presentation == null || !presentation.TryPlayDeathPresentation())
            {
                var vfx = Instantiate(DeathVfx, DeathVfxSpawnPoint.position, Quaternion.identity);
                Destroy(vfx, 5f);
            }

            // tells the game flow manager to handle the enemy destuction
            m_EnemyManager.UnregisterEnemy(this);

            // loot an object
            if (TryDropItem())
            {
                Instantiate(LootPrefab, transform.position, Quaternion.identity);
            }

            // this will call the OnDestroy function
            Destroy(gameObject, DeathDuration);
        }

        void OnDrawGizmosSelected()
        {
            // Path reaching range
            Gizmos.color = PathReachingRangeColor;
            Gizmos.DrawWireSphere(transform.position, PathReachingRadius);

            if (DetectionModule != null)
            {
                // Detection range
                Gizmos.color = DetectionRangeColor;
                Gizmos.DrawWireSphere(transform.position, DetectionModule.DetectionRange);

                // Attack range
                Gizmos.color = AttackRangeColor;
                Gizmos.DrawWireSphere(transform.position, DetectionModule.AttackRange);
            }
        }

        public void OrientWeaponsTowards(Vector3 lookPosition)
        {
            for (int i = 0; i < m_Weapons.Length; i++)
            {
                // orient weapon towards player
                Vector3 weaponForward = (lookPosition - m_Weapons[i].WeaponRoot.transform.position).normalized;
                m_Weapons[i].transform.forward = weaponForward;
            }
        }

        public bool TryAtack(Vector3 enemyPosition)
        {
            if (!enabled || !m_Health || m_Health.CurrentHealth <= 0 || m_GameFlowManager.GameIsEnding)
                return false;
            if (IsStaggered) return false;

            if (AllowWeaponlessPolicy && m_Weapons.Length == 0) return false;
            OrientWeaponsTowards(enemyPosition);

            if ((m_LastTimeWeaponSwapped + DelayAfterWeaponSwap) >= Time.time)
                return false;

            // Shoot the weapon
            if (!HasClearShot(GetCurrentWeapon()))
            {
                if (EnemyChargeDuration > 0 && IsCharging) BlockCharge(); // hold the charge briefly instead of dropping it
                else CancelEnemyCharge();
                return false;
            }
            chargeBlockedSince = -1;
            if (EnemyChargeDuration > 0)
            {
                var weapon=GetCurrentWeapon();
                if (!weapon.isActiveAndEnabled || Time.time < nextEnemyCharge) return false;
                if (enemyChargeStarted < 0) { enemyChargeStarted=Time.time; return false; }
                if (Time.time-enemyChargeStarted < EnemyChargeDuration) return false;
                bool released=weapon.TryShootSingleWithCharge(1);
                CancelEnemyCharge();nextEnemyCharge=Time.time+weapon.DelayBetweenShots;
                if (released) onAttack?.Invoke();
                return released;
            }
            bool didFire = GetCurrentWeapon().HandleShootInputs(false, true, false);

            if (didFire && onAttack != null)
            {
                onAttack.Invoke();

                if (SwapToNextWeapon && m_Weapons.Length > 1)
                {
                    int nextWeaponIndex = (m_CurrentWeaponIndex + 1) % m_Weapons.Length;
                    SetCurrentWeapon(nextWeaponIndex);
                }
            }

            return didFire;
        }

        // Explicit surviving module selected by an actor policy; the template attack path remains unchanged.
        public bool TryAtack(Vector3 target, WeaponController weapon)
        {
            if (!enabled || !m_Health || m_Health.CurrentHealth <= 0 || m_GameFlowManager.GameIsEnding ||
                !weapon || !weapon.isActiveAndEnabled || weapon.Owner != gameObject) return false;
            if (IsStaggered) return false;
            FindAndInitializeAllWeapons();
            if (System.Array.IndexOf(m_Weapons, weapon) < 0) return false;
            OrientWeaponsTowards(target);
            if (!HasClearShot(weapon)) return false;
            bool fired = weapon.HandleShootInputs(false, true, false);
            if (fired) onAttack?.Invoke();
            return fired;
        }
        bool HasClearShot(WeaponController weapon)
        {
            if (!RequireVisibleTargetToShoot) return true;
            if (!DetectionModule || !IsSeeingTarget || !IsTargetInAttackRange || !KnownDetectedTarget || !weapon || !weapon.WeaponMuzzle) return false;
            var targetActor = KnownDetectedTarget.GetComponentInParent<Actor>();
            var targetHealth = targetActor ? targetActor.GetComponent<Health>() : null;
            if (!targetActor || (targetHealth && targetHealth.CurrentHealth <= 0)) return false;
            Vector3 origin = weapon.WeaponMuzzle.position;
            Vector3 delta = KnownDetectedTarget.transform.position - origin;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, m_AttackVisibilityHits, delta.magnitude, -1, QueryTriggerInteraction.Ignore);
            if (count == m_AttackVisibilityHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var collider = m_AttackVisibilityHits[i].collider;
                if (!collider.transform.IsChildOf(transform) && collider.GetComponentInParent<Actor>() != targetActor) return false;
            }
            return true;
        }
        public bool TryDropItem()
        {
            if (DropRate == 0 || LootPrefab == null)
                return false;
            else if (DropRate == 1)
                return true;
            else
                return (Random.value <= DropRate);
        }

        void FindAndInitializeAllWeapons()
        {
            // Check if we already found and initialized the weapons
            if (m_Weapons == null)
            {
                m_Weapons = GetComponentsInChildren<WeaponController>();
                if (!AllowWeaponlessPolicy || GetComponent<IEnemyMobilePolicy>() == null) DebugUtility.HandleErrorIfNoComponentFound<WeaponController, EnemyController>(m_Weapons.Length, this,
                    gameObject);

                for (int i = 0; i < m_Weapons.Length; i++)
                {
                    m_Weapons[i].Owner = gameObject;
                }
            }
        }

        public WeaponController GetCurrentWeapon()
        {
            FindAndInitializeAllWeapons();
            if (m_Weapons.Length == 0 && AllowWeaponlessPolicy) return null;
            // Check if no weapon is currently selected
            if (m_CurrentWeapon == null)
            {
                // Set the first weapon of the weapons list as the current weapon
                SetCurrentWeapon(0);
            }

            DebugUtility.HandleErrorIfNullGetComponent<WeaponController, EnemyController>(m_CurrentWeapon, this,
                gameObject);

            return m_CurrentWeapon;
        }

        void SetCurrentWeapon(int index)
        {
            m_CurrentWeaponIndex = index;
            m_CurrentWeapon = m_Weapons[m_CurrentWeaponIndex];
            if (SwapToNextWeapon)
            {
                m_LastTimeWeaponSwapped = Time.time;
            }
            else
            {
                m_LastTimeWeaponSwapped = Mathf.NegativeInfinity;
            }
        }
    }
}
