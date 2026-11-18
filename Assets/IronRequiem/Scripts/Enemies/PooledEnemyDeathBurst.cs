using Unity.FPS.Game;
using UnityEngine;
namespace IronRequiem
{
    // Uses EnemyController's existing death presentation contract; no damage, loot or AI ownership.
    public sealed class PooledEnemyDeathBurst : MonoBehaviour,IEnemyDeathPresentation
    {
        [SerializeField] GameObject effectPrefab;
        [SerializeField] Transform anchor;
        [SerializeField,Range(.25f,3)] float size=1;
        bool requested;
        public int PresentationCount {get;private set;}
        public void Configure(GameObject effect,Transform origin,float scale){effectPrefab=effect;anchor=origin;size=scale;}
        void Awake()=>RobotDestructionPool.Prepare(effectPrefab,gameObject.scene);
        public bool TryPlayDeathPresentation()
        {
            if(requested)return true;
            requested=true;PresentationCount++;
            // The template enemy is removed immediately; the pool owns the visual after this call.
            RobotDestructionPool.Burst(effectPrefab,null,anchor?anchor.position:transform.position+Vector3.up,null,size);
            return true;
        }
    }
}