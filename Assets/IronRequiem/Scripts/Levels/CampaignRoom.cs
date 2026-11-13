using System.Collections;
using UnityEngine;

namespace IronRequiem
{
    [DefaultExecutionOrder(-200)]
    public sealed class CampaignRoom : MonoBehaviour
    {
        [SerializeField] CombatTwoRoomsLab rounds;
        [SerializeField] ChaseCorridorEncounter pursuit;
        [SerializeField] EnemySpawnQueue queue;
        [Tooltip("Exploration prologue (no rounds): the exit opens once Release() is called, e.g. after the intro cinematic.")]
        [SerializeField] string prologueTitle="";
        bool initialized,released;
        public bool IsPrologue=>!rounds&&!pursuit;
        public string PrologueTitle=>prologueTitle;
        // A stage title overrides the "SECTOR n" banner (prologue rooms and unnumbered transit encounters).
        public bool HasTitle=>!string.IsNullOrEmpty(prologueTitle);
        public void ConfigureTitle(string title)=>prologueTitle=title;
        public void Release()=>released=true;
        public CombatTwoRoomsLab Rounds=>rounds;
        public ChaseCorridorEncounter Pursuit=>pursuit;
        public bool Completed=>rounds?rounds.Completed:pursuit?pursuit.Completed:released;
        public bool Ready=>initialized&&(!queue||queue.PrewarmPending==0);
        public void Configure(CombatTwoRoomsLab waves,ChaseCorridorEncounter chase,EnemySpawnQueue spawns){rounds=waves;pursuit=chase;queue=spawns;}
        public void ConfigurePrologue(string title){rounds=null;pursuit=null;queue=null;prologueTitle=title;}
        void Awake()
        {
            var session=FindAnyObjectByType<CampaignFlow>();
            if(!session){if(rounds)rounds.enabled=false;if(pursuit)pursuit.enabled=false;enabled=false;return;}
            if(rounds)rounds.BindSession(session.Player,session.Message);
            if(pursuit)pursuit.BindSession(session.Player,session.Menu,session.Message);
            foreach(var gap in gameObject.scene.GetRootGameObjects())foreach(var recovery in gap.GetComponentsInChildren<GapFallRecovery>(true))recovery.BindSession(session.Player);
        }
        IEnumerator Start(){yield return null;initialized=true;}
    }
}
