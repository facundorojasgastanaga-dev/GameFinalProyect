using Unity.FPS.Game;
using UnityEngine;
namespace IronRequiem
{
    [RequireComponent(typeof(Health))]
    public sealed class EnemyExecutionSource : MonoBehaviour
    {
        // Game rule for every executable enemy: current health strictly below 75 % of maximum.
        public const float HealthThreshold=.75f;
        [Tooltip("Legacy per-prefab value. It can only make the rule stricter; it never allows executions at 75 % or more.")]
        [SerializeField,Range(.01f,1)] float healthThreshold=.75f;
        public float EffectiveThreshold=>Mathf.Min(healthThreshold,HealthThreshold);
        [SerializeField] StolenCannon modulePrefab;
        [Tooltip("v4: off = this enemy can never be executed (no [E] prompt, no reservation, no module). The module prefab is kept for later use.")]
        [SerializeField] bool executionEnabled=true;
        public bool ExecutionEnabled=>executionEnabled;
        public void SetExecutionEnabled(bool value){executionEnabled=value;if(!value)CancelReservation();}
        Health health; BigBotCombat bigBot; bool reserved,delivered;
        public StolenCannon ModulePrefab=>modulePrefab;
        public bool Delivered=>delivered;
        public bool IsReserved=>reserved;
        public bool Eligible=>executionEnabled&&health&&health.CurrentHealth>0&&health.CurrentHealth<health.MaxHealth*EffectiveThreshold&&!reserved&&!delivered&&modulePrefab&&(!bigBot||bigBot.ActiveCannonCount>0);
        void Awake(){health=GetComponent<Health>();bigBot=GetComponent<BigBotCombat>();EnsureReactionLayers();}
        // Executions v02 (F2): robot-side reaction components, added here when the prefab does not carry them yet
        // (ExecutionsAuthoring.Apply bakes the audio clips into the three robot prefabs). PullIn first: Reaction reads it.
        void EnsureReactionLayers()
        {
            if(!GetComponent<ExecutionPullIn>())gameObject.AddComponent<ExecutionPullIn>();
            if(!GetComponent<ExecutionReaction>())gameObject.AddComponent<ExecutionReaction>();
            if(!GetComponent<ExecutionWeaponDetach>())gameObject.AddComponent<ExecutionWeaponDetach>();
            if(!GetComponent<ExecutionLocalFlash>())gameObject.AddComponent<ExecutionLocalFlash>();
            if(!GetComponent<ExecutionHealthBarHider>())gameObject.AddComponent<ExecutionHealthBarHider>();
            if(!GetComponent<ExecutionRobotAudio>())gameObject.AddComponent<ExecutionRobotAudio>();
            if(!GetComponent<ExecutionDebrisGuard>())gameObject.AddComponent<ExecutionDebrisGuard>();
        }
        public void Configure(StolenCannon prefab)=>modulePrefab=prefab;
        public bool TryReserve(){if(!Eligible)return false;reserved=true;return true;}
        public StolenCannon CreateReservedModule()
        {
            if(!reserved||delivered)return null;
            StolenCannon module;
            if(modulePrefab.GetComponent<BigBotCannonDebris>())
            {
                // The imported cannon joint includes the FBX unit conversion. The enemy root does
                // not: using its scale made the persistent world module 100 times too small.
                var destruction=GetComponent<BigBotDestruction>();var mount=destruction&&destruction.CannonPivot?destruction.CannonPivot:transform;
                var drop=BigBotCannonDebris.SpawnCollectible(modulePrefab,mount,GetComponentsInChildren<Collider>(),destruction?destruction.CannonFrontPivot:null);if(!drop)return null;module=drop.GetComponent<StolenCannon>();
            }
            else module=Instantiate(modulePrefab,transform.position+Vector3.up,Quaternion.identity);
            var body=module.GetComponent<Rigidbody>();if(!body)body=module.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;
            var solid=module.GetComponent<BoxCollider>();if(!solid)solid=module.gameObject.AddComponent<BoxCollider>();module.FitPickupCollider(solid);
            module.MakeAvailable(body,solid,true);return module;
        }
        // Executions v02: presentation beats raised by the player side; robot-side reaction layers listen here.
        public event System.Action<ExecutionBeatInfo> BeatReached;
        public ExecutionBeat LastBeat {get;private set;}
        public bool InExecution=>LastBeat!=ExecutionBeat.None&&LastBeat!=ExecutionBeat.End&&LastBeat!=ExecutionBeat.Cancel;
        public void RaiseBeat(ExecutionBeatInfo info){LastBeat=info.Beat;BeatReached?.Invoke(info);}
        public ExecutionKind Kind=>GetComponent<SawBotCombat>()?ExecutionKind.SawBot:bigBot?ExecutionKind.BigBot:GetComponentInChildren<T07CannonSocket>()?ExecutionKind.T07:ExecutionKind.Other;
        // The real joint the hand closes on: T-07 cannon socket bone, SawBot right saw arm, Big Bot cannon pivot.
        public Transform WeaponJoint
        {
            get
            {
                var socket=GetComponentInChildren<T07CannonSocket>();if(socket&&socket.Socket)return socket.Socket;
                var destruction=GetComponent<BigBotDestruction>();if(destruction&&destruction.CannonPivot)return destruction.CannonPivot;
                var saw=GetComponent<SawBotWeaponSource>();if(saw&&saw.Joint)return saw.Joint;
                return null;
            }
        }
        public void CancelReservation(){if(!delivered)reserved=false;}
        public void Complete(){if(!reserved||delivered)return;delivered=true;reserved=false;health.Kill();}
    }
}
