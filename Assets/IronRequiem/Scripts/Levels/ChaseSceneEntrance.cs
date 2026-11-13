using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ChaseSceneEntrance : MonoBehaviour
    {
        [SerializeField] CombatTwoRoomsLab prerequisite;
        [SerializeField] GameObject sealedDoor;
        [SerializeField] string destination = "ChaseCorridorLab";
        [SerializeField, Min(0)] float minimumPreparationSeconds = 1.5f;
        AsyncOperation loading;
        float preparationStarted;
        bool committed;
        public bool IsPreparing => loading != null && !IsOpen && !committed;
        public float LoadingProgress => loading == null ? 0 : Mathf.Clamp01(loading.progress / .9f);
        public bool IsOpen => prerequisite && prerequisite.Completed && loading != null && loading.progress >= .9f && Time.time - preparationStarted >= minimumPreparationSeconds;
        public void Configure(CombatTwoRoomsLab level, GameObject door) { prerequisite = level; sealedDoor = door; }
        void Update()
        {
            if (!loadingStarted && prerequisite && prerequisite.Completed && Time.timeScale > 0)
            {
                loadingStarted = true;
                if (!Application.CanStreamedLevelBeLoaded(destination)) { Debug.LogError("Missing corridor in Build Settings: " + destination, this); return; }
                prerequisite.ShowExitStatus("ARENA 2 COMPLETADA · PREPARANDO PERSECUCIÓN…");
                preparationStarted = Time.time;
                loading = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
                loading.allowSceneActivation = false;
            }
            if (sealedDoor && sealedDoor.activeSelf == IsOpen) sealedDoor.SetActive(!IsOpen);
            if (IsOpen && !readyAnnounced) { readyAnnounced = true; prerequisite.ShowExitStatus("SECTOR 03 LISTO · ENTRA EN LA PUERTA DE PERSECUCIÓN"); }
        }
        bool loadingStarted, readyAnnounced;
        void OnTriggerEnter(Collider other) => Enter(other);
        void OnTriggerStay(Collider other) => Enter(other);
        void Enter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerCharacterController>();
            if (committed || !IsOpen || !player || player.IsDead || Time.timeScale <= 0) return;
            committed = true; ChaseCorridorEncounter.EnterFromPreviousScene(); Time.timeScale = 1; AudioListener.pause = false; loading.allowSceneActivation = true;
        }
        // Scene loads cannot be cancelled. A restart/menu reload must not leave an activation barrier in Unity's queue.
        void OnDestroy() { if (loading != null && !loading.isDone) loading.allowSceneActivation = true; }
    }
}
