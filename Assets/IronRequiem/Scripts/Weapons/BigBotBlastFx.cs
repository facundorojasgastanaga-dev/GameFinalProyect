using Unity.FPS.Game;
using UnityEngine;
namespace IronRequiem
{
    /// v4 Big Bot shell: heavier explosion presentation on every impact (fire, smoke, shockwave, sparks,
    /// scorch, thick boom from the pooled blast prefab) and a camera shake that falls off with distance.
    /// Damage stays in ProjectileStandard/DamageArea; this only listens to ProjectileBase.OnImpact.
    [RequireComponent(typeof(ProjectileBase))]
    public sealed class BigBotBlastFx : MonoBehaviour
    {
        [SerializeField] GameObject blastPrefab;
        [SerializeField,Min(.1f)] float blastLifetime=3.2f;
        [Tooltip("Camera shake: full amplitude at the impact, fading to zero at this radius (metres).")]
        [SerializeField,Min(1)] float shakeRadius=16f;
        [SerializeField,Min(0)] float shakeAmplitude=1f;
        [SerializeField,Min(.05f)] float shakeSeconds=.45f;
        ProjectileBase projectile;
        public static int ImpactCount {get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()=>ImpactCount=0;
        public void Configure(GameObject prefab,float radius,float amplitude,float seconds){blastPrefab=prefab;shakeRadius=radius;shakeAmplitude=amplitude;shakeSeconds=seconds;}
        void Awake(){projectile=GetComponent<ProjectileBase>();projectile.OnImpact+=OnImpact;}
        void OnDestroy(){if(projectile)projectile.OnImpact-=OnImpact;}
        void OnImpact(Vector3 point,Vector3 normal)
        {
            ImpactCount++;
            if(normal.sqrMagnitude<1e-4f)normal=Vector3.up;
            if(blastPrefab)FxPool.Spawn(blastPrefab,point+normal*.05f,Quaternion.LookRotation(normal),blastLifetime);
            if(shakeAmplitude>0)DistanceCameraImpulse.Emit(point,shakeRadius,shakeAmplitude,shakeSeconds);
        }
    }
}
