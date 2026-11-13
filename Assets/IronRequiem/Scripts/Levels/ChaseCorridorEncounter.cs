using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace IronRequiem
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ChaseCorridorEncounter : MonoBehaviour
    {
        [Serializable] public sealed class Ambush
        {
            public float ActivationZ;
            public GameObject[] Prefabs;
            public Transform[] Points;
            [NonSerialized] public bool Triggered;
        }
        [SerializeField] PlayerCharacterController player;
        [SerializeField] CombatLabMenu menu;
        [SerializeField] EnemySpawnQueue queue;
        [SerializeField] GameObject entranceDoor, exitDoor;
        [SerializeField] Transform carriage;
        [SerializeField] Transform[] blades;
        [SerializeField] AudioSource motor;
        [SerializeField] TMP_Text message;
        [SerializeField] Ambush[] ambushes;
        [SerializeField, Min(.1f)] float sawSpeed = 8;
        [SerializeField, Min(0)] float warningSeconds = 2;
        [SerializeField, Min(0)] float bladeRpm = 240;
        [SerializeField] bool counterRotateBlades;
        [SerializeField, Min(0)] float sawContactOffset = .8f;
        [SerializeField, Min(1)] float sawDamage = 100;
        [SerializeField, Min(.1f)] float damageInterval = .35f;
        [SerializeField] float finishZ = 184;
        [SerializeField, Min(1)] float halfWidth = 8;
        [SerializeField] Vector3 courseOffset;
        readonly Collider[] blockers = new Collider[32];
        readonly List<GameObject> spawned = new List<GameObject>();
        Health health; PatrolPath hunt; float beganAt, nextDamage; int pendingZone = -1;
        static bool immediateStart;
        public bool Running { get; private set; }
        public bool Completed { get; private set; }
        public int SpawnedCount { get; private set; }
        public Transform SawCarriage => carriage;
        public GameObject EntranceDoor => entranceDoor;
        public GameObject ExitDoor => exitDoor;
        public AudioSource Motor => motor;
        public Ambush[] Ambushes => ambushes;
        public IReadOnlyList<GameObject> Spawned => spawned;
        public float SawSpeed => sawSpeed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetEntry() => immediateStart = false;
        public static void EnterFromPreviousScene() => immediateStart = true;
        public void BindSession(PlayerCharacterController owner,CombatLabMenu labMenu,TMP_Text label){player=owner;menu=labMenu;message=label;}
        public void Configure(PlayerCharacterController owner, CombatLabMenu labMenu, EnemySpawnQueue spawns, GameObject entrance, GameObject exit, Transform saws, Transform[] rotors, AudioSource sound, TMP_Text label, Ambush[] zones)
        { player = owner; menu = labMenu; queue = spawns; entranceDoor = entrance; exitDoor = exit; carriage = saws; blades = rotors; motor = sound; message = label; ambushes = zones; }
        IEnumerator Start()
        {
            health = player.GetComponent<Health>(); entranceDoor.SetActive(false); exitDoor.SetActive(true); carriage.gameObject.SetActive(false);
            message.text = "SECTOR 03 · ATRAVIESA LA PUERTA";
            hunt = new GameObject("ChasePlayerHuntRoute").AddComponent<PatrolPath>(); hunt.transform.SetParent(transform, false); hunt.PathNodes.Add(player.transform);
            foreach (var zone in ambushes) foreach (var prefab in zone.Prefabs) queue.Prewarm(prefab, 1);
            // The source level loads us directly into play; a standalone lab retains its existing menu.
            bool play = immediateStart; immediateStart = false; yield return null; if (play) menu.BeginGame();
        }
        void OnTriggerEnter(Collider other) => Contact(other);
        void OnTriggerStay(Collider other) => Contact(other);
        void Contact(Collider other)
        {
            if (other.GetComponentInParent<PlayerCharacterController>() != player || Running || Completed || !menu.IsPlaying || player.IsDead) return;
            Running = true; beganAt = Time.time; entranceDoor.SetActive(true); carriage.gameObject.SetActive(true);
            message.text = "¡CORRE! · SALTA LAS GRIETAS · NO TE DETENGAS";
            if (motor && motor.clip) motor.Play();
        }
        void Update()
        {
            if (!Running) return;
            if (player.IsDead || health.CurrentHealth <= 0) { StopPursuit(); message.text = "LAS SIERRAS TE ALCANZARON"; return; }
            if (Time.timeScale <= 0 || !menu.IsPlaying) return;
            if (player.transform.position.z >= finishZ)
            { Completed = true; StopPursuit(); exitDoor.SetActive(false); message.text = "PERSECUCIÓN COMPLETADA · ZONA SEGURA"; return; }
            for (int i = 0; i < blades.Length; i++)
                blades[i].Rotate(Vector3.forward, bladeRpm * 6 * Time.deltaTime * (counterRotateBlades ? i % 2 == 0 ? -1 : 1 : 1), Space.Self);
            if (Time.time >= beganAt + warningSeconds) carriage.position += Vector3.forward * (sawSpeed * Time.deltaTime);
            // A swept cutting plane across the corridor cannot tunnel past the CharacterController.
            var p = player.transform.position;
            if (p.z <= carriage.position.z + sawContactOffset && Mathf.Abs(p.x-courseOffset.x) < halfWidth && p.y > courseOffset.y-7 && p.y < courseOffset.y+6 && Time.time >= nextDamage)
            { nextDamage = Time.time + damageInterval; health.TakeDamage(sawDamage, carriage.gameObject); }
            ProcessAmbushes();
        }
        void StopPursuit()
        { Running = false; queue.CancelPending(); if (motor) motor.Stop(); }
        void ProcessAmbushes()
        {
            float z = player.transform.position.z;
            if (pendingZone >= 0 && queue.Pending > 0 && z > LastZ(ambushes[pendingZone]) - 4) { queue.CancelPending(); pendingZone = -1; }
            for (int i = 0; i < ambushes.Length; i++)
            {
                var zone = ambushes[i]; if (zone.Triggered || z < zone.ActivationZ) continue; zone.Triggered = true;
                if (z > LastZ(zone) - 4) continue; pendingZone = i;
                for (int j = 0; j < zone.Prefabs.Length; j++)
                {
                    var prefab = zone.Prefabs[j]; var point = zone.Points[j];
                    queue.Spawn(prefab, point.position, Quaternion.Euler(0, 180, 0), Register, () => SafePosition(prefab, point), pos => Quaternion.LookRotation(Vector3.ProjectOnPlane(player.transform.position - pos, Vector3.up)));
                }
            }
        }
        static float LastZ(Ambush zone) { float max = float.NegativeInfinity; foreach (var p in zone.Points) max = Mathf.Max(max, p.position.z); return max; }
        Vector3? SafePosition(GameObject prefab, Transform point)
        {
            if (!Running || !menu.IsPlaying || player.IsDead || Time.timeScale <= 0 || point.position.z < player.transform.position.z + 6) return null;
            if (!NavMesh.SamplePosition(point.position, out var hit, .75f, NavMesh.AllAreas)) return null;
            var agent = prefab.GetComponent<NavMeshAgent>(); float radius = agent ? Mathf.Max(.4f, agent.radius) : .75f, height = agent ? Mathf.Max(radius * 2, agent.height) : 2;
            if (Vector3.Distance(player.transform.position, hit.position) < 6 + radius) return null;
            int count = Physics.OverlapCapsuleNonAlloc(hit.position + Vector3.up * (radius + .08f), hit.position + Vector3.up * (height - radius + .08f), radius, blockers, -1, QueryTriggerInteraction.Ignore);
            if (count == blockers.Length) return null;
            for (int i = 0; i < count; i++) if (blockers[i].bounds.max.y > hit.position.y + .08f) return null;
            return hit.position;
        }
        void Register(GameObject enemy)
        {
            spawned.Add(enemy); SpawnedCount++; var controller = enemy.GetComponent<EnemyController>(); if (controller && enemy.GetComponent<EnemyMobile>()) controller.PatrolPath = hunt;
        }
        void OnDisable() { if (motor) motor.Stop(); if (queue) queue.CancelPending(); }
    }
}
