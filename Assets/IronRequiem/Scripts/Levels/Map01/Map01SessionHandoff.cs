using System.Collections;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem
{
    /// Map01 session chaining (U2: four sessions). Spec: Docs/IronRequiem/Map01/04_PUERTAS.md §Q1.1.
    /// Departure (S1 at C05, S2 at C13, S3 at C17): the last stage of a session has no CampaignAirlock (airlocks = stages - 1), so
    /// CampaignFlow never opens its exit. This component mirrors CampaignFlow for that one door, with the same door contract:
    ///   1. opens the source door when the last stage is Completed and the flow is not Busy (like CampaignFlow.Update);
    ///   2. when the player is inside its chamber trigger, closes the door behind him (collision from the first frame);
    ///   3. aborts (reopens) if the closed door has the player outside the trigger for more than 1 s (like CampaignFlow.Prepare);
    ///   4. with the door shut and the player inside for >= minCycleSeconds, drains the flow's pending loads and loads the next
    ///      session scene in Single mode. The next session starts in the same chamber (its destination door is the initialDoor).
    /// EndOfMap (S4): when R18 is Completed, shows the end message and loads the win scene after a few seconds.
    /// Only observes public state of CampaignFlow/CampaignRoom; never modifies existing systems.
    public sealed class Map01SessionHandoff : MonoBehaviour
    {
        public enum HandoffMode { Departure, EndOfMap }

        [SerializeField] HandoffMode mode = HandoffMode.Departure;
        [SerializeField] CampaignFlow flow;
        [Header("Departure")]
        [SerializeField] GameObject sourceDoor;
        [Tooltip("Far door of the chamber. It never opens in this session (it is the next session's initialDoor).")]
        [SerializeField] GameObject destinationDoor;
        [SerializeField] AirlockCycle cycle;
        [SerializeField] string nextScene = "";
        [SerializeField, Min(0)] float minCycleSeconds = 1.6f;
        [SerializeField, Min(0)] float abortSeconds = 1f;
        [SerializeField] string departingText = "ESCLUSA SELLADA · CARGANDO EL SIGUIENTE TRAMO…";
        [Header("End of map")]
        [SerializeField] string endText = "BÓVEDA DESPEJADA · MAPA 1 COMPLETADO";
        [SerializeField, Min(0)] float endDelaySeconds = 5f;
        [SerializeField] string winScene = "WinScene";

        float lastContact = float.NegativeInfinity, endAt;
        bool armed, departing, ending;
        Coroutine routine;

        public HandoffMode Mode => mode;
        /// The exit of the last stage has been opened by this component.
        public bool Armed => armed;
        /// The next scene (session or win scene) is being loaded: no abort possible.
        public bool Departing => departing;
        /// The session player touched the chamber trigger during the last physics steps (same rule as CampaignAirlock).
        public bool PlayerInside => Time.time - lastContact <= .25f;
        public GameObject SourceDoor => sourceDoor;
        public GameObject DestinationDoor => destinationDoor;
        public string NextScene => mode == HandoffMode.Departure ? nextScene : winScene;

        /// Editor authoring (Map01 DoorAuthoring / Map01SessionAuthoring).
        public void ConfigureDeparture(CampaignFlow session, GameObject source, GameObject destination, AirlockCycle presentation, string next)
        { mode = HandoffMode.Departure; flow = session; sourceDoor = source; destinationDoor = destination; cycle = presentation; nextScene = next ?? ""; }
        public void ConfigureEnd(CampaignFlow session, string win, string text, float delay)
        { mode = HandoffMode.EndOfMap; flow = session; winScene = win ?? ""; endText = text ?? ""; endDelaySeconds = Mathf.Max(0, delay); sourceDoor = null; destinationDoor = null; cycle = null; nextScene = ""; }

        void Awake()
        {
            if (!flow) flow = FindAnyObjectByType<CampaignFlow>();
            if (mode == HandoffMode.Departure)
            {
                var trigger = GetComponent<Collider>();
                if (!trigger || !trigger.isTrigger) Debug.LogError("Map01SessionHandoff: falta un BoxCollider trigger con Rigidbody cinemático en " + name, this);
                if (!sourceDoor) Debug.LogError("Map01SessionHandoff: falta la puerta de origen en " + name, this);
                if (string.IsNullOrEmpty(nextScene)) Debug.LogError("Map01SessionHandoff: falta la escena siguiente en " + name, this);
            }
        }

        void OnTriggerEnter(Collider other) => Contact(other);
        void OnTriggerStay(Collider other) => Contact(other);
        void Contact(Collider other)
        {
            if (mode != HandoffMode.Departure || !flow || !flow.Player) return;
            if (other.GetComponentInParent<PlayerCharacterController>() != flow.Player) return;
            lastContact = Time.time;
        }

        bool LastStageCompleted()
        {
            if (!flow || flow.Busy) return false;
            var room = flow.CurrentRoom; var stages = flow.StageScenes;
            if (!room || stages == null || stages.Length == 0 || flow.CurrentStage != stages.Length - 1 || !room.Completed) return false;
            if (flow.Menu && !flow.Menu.IsPlaying) return false;
            return flow.Player && !flow.Player.IsDead;
        }

        void Update()
        {
            if (!flow || departing) return;
            if (mode == HandoffMode.EndOfMap) { UpdateEnd(); return; }
            if (!armed)
            {
                if (!LastStageCompleted()) return;
                armed = true;
                AirlockDoorMotion.Set(this, sourceDoor, false);
                return;
            }
            if (routine == null && PlayerInside && flow.Player && !flow.Player.IsDead && (!flow.Menu || flow.Menu.IsPlaying))
                routine = StartCoroutine(Cycle());
        }

        IEnumerator Cycle()
        {
            AirlockDoorMotion.Set(this, sourceDoor, true); // collision from the first frame (AirlockDoorMotion contract)
            if (cycle) cycle.Begin();
            float began = Time.time, outsideSince = -1;
            while (!AirlockDoorMotion.IsClosed(sourceDoor) || !PlayerInside || Time.time - began < minCycleSeconds)
            {
                if (!flow.Player || flow.Player.IsDead) { routine = null; yield break; } // death: CombatLabMenu reloads the session
                if (AirlockDoorMotion.IsClosed(sourceDoor) && !PlayerInside)
                {
                    if (outsideSince < 0) outsideSince = Time.time;
                    else if (Time.time - outsideSince > abortSeconds) { Reopen(); yield break; }
                }
                else outsideSince = -1;
                yield return null;
            }
            if (!Application.CanStreamedLevelBeLoaded(nextScene))
            {
                Debug.LogError("Map01SessionHandoff: la escena '" + nextScene + "' no está en Build Settings.", this);
                if (flow.Message) flow.Message.text = "ERROR · TRAMO NO DISPONIBLE";
                Reopen(); yield break;
            }
            departing = true;
            if (flow.Message) flow.Message.text = departingText;
            yield return flow.DrainPendingLoadsForShutdown();
            CombatLabMenu.ContinueRun(nextScene); // keeps "launched from the main menu" across sessions (CombatLabMenu.cs:29-31)
            SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
        }

        void Reopen()
        {
            AirlockDoorMotion.Set(this, sourceDoor, false);
            if (cycle) cycle.Abort();
            routine = null;
        }

        void UpdateEnd()
        {
            if (!ending)
            {
                if (!LastStageCompleted()) return;
                ending = true; endAt = Time.time + endDelaySeconds;
            }
            // CombatTwoRoomsLab writes its own "COMPLETADO" line in the same frame: keep the end text on screen.
            if (flow.Message && flow.Message.text != endText) flow.Message.text = endText;
            if (Time.time < endAt) return;
            departing = true;
            if (!Application.CanStreamedLevelBeLoaded(winScene)) { Debug.LogError("Map01SessionHandoff: la escena de victoria '" + winScene + "' no está en Build Settings.", this); return; }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            StartCoroutine(LoadWin());
        }

        IEnumerator LoadWin()
        {
            yield return flow.DrainPendingLoadsForShutdown();
            CombatLabMenu.ContinueRun(winScene);
            SceneManager.LoadScene(winScene, LoadSceneMode.Single);
        }
    }
}
