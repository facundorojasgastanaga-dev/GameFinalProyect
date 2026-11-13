using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CampaignAirlock : MonoBehaviour
    {
        [SerializeField] CampaignFlow flow;
        [SerializeField] int sourceStage;
        [SerializeField] GameObject sourceDoor,destinationDoor;
        [Tooltip("Optional doors/shutters that open together with the destination door (e.g. an inner blast shutter).")]
        [SerializeField] GameObject[] extraDestinationDoors=new GameObject[0];
        [Tooltip("Optional decontamination / pressurisation presentation while the transfer runs.")]
        [SerializeField] AirlockCycle cycle;
        float lastContact=float.NegativeInfinity;
        public GameObject SourceDoor=>sourceDoor;
        public GameObject DestinationDoor=>destinationDoor;
        public GameObject[] ExtraDestinationDoors=>extraDestinationDoors;
        public AirlockCycle Cycle=>cycle;
        public int SourceStage=>sourceStage;
        // The session player was inside the trigger volume during the last physics steps.
        public bool PlayerInside=>Time.time-lastContact<=.25f;
        public void ConfigureExtras(GameObject[] extras,AirlockCycle presentation){extraDestinationDoors=extras??new GameObject[0];cycle=presentation;}
        public void Configure(CampaignFlow session,int index,GameObject entrance,GameObject exit){flow=session;sourceStage=index;sourceDoor=entrance;destinationDoor=exit;}
        void OnTriggerEnter(Collider other)=>Contact(other);
        void OnTriggerStay(Collider other)=>Contact(other);
        void Contact(Collider other){if(other.GetComponentInParent<PlayerCharacterController>()!=flow.Player)return;lastContact=Time.time;flow.TryTransition(sourceStage);}
    }
}
