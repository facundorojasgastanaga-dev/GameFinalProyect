using System;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
namespace IronRequiem
{
    public sealed class CombatTwoRoomsLab : MonoBehaviour
    {
        [Serializable] public sealed class Group {public GameObject Prefab;[Min(1)] public int Count=1;public Transform FixedPoint;}
        [Serializable] public sealed class Composition {public string Name;public Group[] Groups;}
        [Serializable] public sealed class Wave {public Group[] Groups;public Composition[] Variations;}
        [SerializeField] PlayerCharacterController player;
        [SerializeField] Wave[] waves;
        [SerializeField] Transform[] firstRoomSpawns,secondRoomSpawns;
        [SerializeField] Bounds firstRoom=new Bounds(new Vector3(0,3,0),new Vector3(48,14,46)),secondRoom=new Bounds(new Vector3(0,3,58),new Vector3(48,14,46));
        [SerializeField,Min(1)] float playerClearance=6;
        [SerializeField] GameObject gate;
        [SerializeField] TMP_Text message;
        [Header("Optional streamed single arena")]
        [SerializeField] bool singleArena;
        [SerializeField] string arenaTitle="CUARTO";
        [SerializeField,Min(0)] float gatePreparationSeconds;
        float gateReadyAt=-1;
        [Tooltip("Optional. When set, all waves are prewarmed inactive during calm time and enemies are activated a few per frame instead of all in one frame. Empty = previous behaviour.")]
        [SerializeField] EnemySpawnQueue spawnQueue;
        [Header("Varied encounters (optional)")]
        [SerializeField] bool variedSpawns;
        [Tooltip("Zero picks a fresh seed on scene reload. Nonzero reproduces the same compositions and slots.")]
        [SerializeField] int scenarioSeed;
        [SerializeField,Min(0)] float minimumSpawnInterval=.2f;
        [SerializeField,Min(0)] float maximumSpawnInterval=.4f;
        [SerializeField,Min(0)] float separationPadding=.5f;
        readonly Collider[] spawnBlockers=new Collider[64];
        readonly List<Transform> usedSlots=new List<Transform>(),recentSlots=new List<Transform>();
        const float ReservationSeconds=2.5f;readonly List<Vector4> reservations=new List<Vector4>();readonly List<float> reservedAt=new List<float>();
        System.Random random;Group[][] plan;string[] compositions;NavMeshPath spawnPath;bool started;float nextSpawnAt;
        sealed class SpawnOrder {public Group Group;public float RetryAt;}
        public bool UsesVariedSpawns=>variedSpawns;public int RunSeed{get;private set;}public string CurrentComposition=>index>=0&&compositions!=null?compositions[index]:"";
        public bool WaitingForSpawnSpace{get;private set;}
        public void ConfigureVariety(bool enabled,int seed=0){if(started)throw new InvalidOperationException("Configure variety before Start");variedSpawns=enabled;scenarioSeed=seed;}
        readonly HashSet<Health> alive=new HashSet<Health>();readonly Dictionary<Health,UnityAction> callbacks=new Dictionary<Health,UnityAction>();
        Unity.FPS.AI.PatrolPath playerHunt;CombatLabMenu menu;
        int index=-1,pendingSpawns;bool active,firstStarted,secondStarted;
        public int WaveIndex=>index;public int LivingEnemies=>alive.Count;public bool GateOpen {get;private set;}public bool Completed {get;private set;}
        public IReadOnlyCollection<Health> Living=>alive;public int PendingSpawns=>pendingSpawns;public EnemySpawnQueue SpawnQueue=>spawnQueue;
        public void UseSpawnQueue(EnemySpawnQueue queue)=>spawnQueue=queue;
        public void BindSession(PlayerCharacterController owner,TMP_Text label){player=owner;message=label;}
        public void ShowExitStatus(string value){if(Completed&&message)message.text=value;}
        public Bounds FirstRoomBounds=>firstRoom; // v35: the tour benchmark walks the player into the arena
        public Transform[] FirstRoomSpawns=>firstRoomSpawns;
        public void Configure(PlayerCharacterController p,Wave[] sequence,Transform[] a,Transform[] b,GameObject door,TMP_Text text){player=p;waves=sequence;firstRoomSpawns=a;secondRoomSpawns=b;gate=door;message=text;}
        void Start(){started=true;message.text="ENTRA EN LA PRIMERA ARENA";menu=FindAnyObjectByType<CombatLabMenu>();playerHunt=new GameObject("LabPlayerHuntRoute").AddComponent<Unity.FPS.AI.PatrolPath>();playerHunt.transform.SetParent(transform);playerHunt.PathNodes.Add(player.transform);
            RunSeed=scenarioSeed==0?Guid.NewGuid().GetHashCode():scenarioSeed;random=new System.Random(RunSeed);spawnPath=new NavMeshPath();plan=new Group[waves.Length][];compositions=new string[waves.Length];
            for(int i=0;i<waves.Length;i++){var choices=waves[i].Variations;var choice=variedSpawns&&choices!=null&&choices.Length>0?choices[random.Next(choices.Length)]:null;plan[i]=choice!=null?choice.Groups:waves[i].Groups;compositions[i]=choice!=null?choice.Name:"Original";if(spawnQueue)foreach(var g in plan[i])spawnQueue.Prewarm(g.Prefab,g.Count);}}
        // Spawn after projectile Updates; Health/EnemyController Start run before the next combat frame.
        void LateUpdate()
        {
            if(!player||player.IsDead||Completed||(menu&&!menu.IsPlaying))return;
            if(!firstStarted&&firstRoom.Contains(player.transform.position)){firstStarted=true;Spawn(0);}
            if(singleArena)
            {
                if(active){alive.RemoveWhere(h=>!h);if(alive.Count==0&&pendingSpawns==0){active=false;if(index+1<waves.Length)Spawn(index+1);else{Completed=true;message.text=arenaTitle+" COMPLETADO · ACCESO ABIERTO";}}}
                return;
            }
            if(active){alive.RemoveWhere(h=>!h);if(alive.Count==0&&pendingSpawns==0){active=false;if(index<2)Spawn(index+1);else if(index==2){gateReadyAt=Time.time+gatePreparationSeconds;message.text="PREPARANDO CUARTO 2…";}else{Completed=true;message.text="PRUEBA COMPLETADA";}}}
            if(!GateOpen&&gateReadyAt>=0&&Time.time>=gateReadyAt&&(!spawnQueue||spawnQueue.PrewarmPending==0)){GateOpen=true;gate.SetActive(false);message.text="ARENA 1 COMPLETADA · ACCESO ABIERTO";}
            if(GateOpen&&!secondStarted&&secondRoom.Contains(player.transform.position)){secondStarted=true;Spawn(3);}
        }
        void Spawn(int wave)
        {
            if(variedSpawns){SpawnVaried(wave);return;}
            index=wave;var points=new List<Transform>(wave<3?firstRoomSpawns:secondRoomSpawns);points.Sort((a,b)=>(b.position-player.transform.position).sqrMagnitude.CompareTo((a.position-player.transform.position).sqrMagnitude));var used=new HashSet<Transform>();
            foreach(var group in waves[wave].Groups)for(int i=0;i<group.Count;i++)
            {
                Transform slot=group.FixedPoint;
                if(!slot){slot=points.Find(t=>!used.Contains(t)&&Vector3.Distance(t.position,player.transform.position)>=playerClearance);if(!slot)throw new InvalidOperationException("No safe spawn slot for wave "+wave);used.Add(slot);}
                if(!NavMesh.SamplePosition(slot.position,out var hit,1,NavMesh.AllAreas))throw new InvalidOperationException("Spawn outside NavMesh: "+slot.name);
                var facing=Quaternion.Euler(0,Quaternion.LookRotation((player.transform.position-hit.position).normalized).eulerAngles.y,0);
                if(spawnQueue){pendingSpawns++;spawnQueue.Spawn(group.Prefab,hit.position,facing,e=>{pendingSpawns--;Register(e);});}
                else Register(Instantiate(group.Prefab,hit.position,facing));
            }
            active=true;WaveLabel();
        }
        void SpawnVaried(int wave)
        {
            index=wave;active=true;nextSpawnAt=Time.time;WaitingForSpawnSpace=false;
            recentSlots.Clear();recentSlots.AddRange(usedSlots);usedSlots.Clear();reservations.Clear();reservedAt.Clear();
            var orders=new List<SpawnOrder>();foreach(var group in plan[wave])for(int i=0;i<group.Count;i++)orders.Add(new SpawnOrder{Group=group});
            for(int i=orders.Count-1;i>0;i--){int j=random.Next(i+1);var item=orders[i];orders[i]=orders[j];orders[j]=item;}
            if(!spawnQueue)throw new InvalidOperationException("Varied spawns require existing EnemySpawnQueue");
            foreach(var order in orders){pendingSpawns++;spawnQueue.Spawn(order.Group.Prefab,Vector3.zero,Quaternion.identity,enemy=>{pendingSpawns--;Register(enemy);WaveLabel();},()=>ResolveSpawn(order),position=>{var direction=Vector3.ProjectOnPlane(player.transform.position-position,Vector3.up);return direction.sqrMagnitude>.001f?Quaternion.LookRotation(direction):Quaternion.identity;});}
            WaveLabel();
        }
        void WaveLabel(){message.text=singleArena?arenaTitle+" · RONDA "+(index+1)+" / "+waves.Length:index<3?"ARENA 1 · OLEADA "+(index+1)+" / 3":"ARENA 2 · RONDA FINAL";}
        Vector3? ResolveSpawn(SpawnOrder order)
        {
            if(!this||!player||player.IsDead||(menu&&!menu.IsPlaying)||Time.time<nextSpawnAt||Time.time<order.RetryAt)return null;
            var agent=order.Group.Prefab.GetComponent<NavMeshAgent>();float radius=agent?Mathf.Max(.35f,agent.radius):.75f,height=agent?Mathf.Max(radius*2,agent.height):2;
            var points=new List<Transform>(index<3?firstRoomSpawns:secondRoomSpawns);
            for(int i=points.Count-1;i>0;i--){int j=random.Next(i+1);var point=points[i];points[i]=points[j];points[j]=point;}
            if(order.Group.FixedPoint){points.Clear();points.Add(order.Group.FixedPoint);}
            // 12/11/2026: a third pass reuses this round's slots once their reservation expired (horde rounds have more
            // enemies than slots; the physics overlap test still refuses a slot while a body stands on it)
            for(int pass=0;pass<3;pass++)foreach(var point in points)
            {
                if(!point||(pass<2&&usedSlots.Contains(point))||(pass==0&&recentSlots.Contains(point)))continue;
                if(!SafeSpawn(point.position,radius,height,agent?agent.areaMask:NavMesh.AllAreas,out var position))continue;
                usedSlots.Add(point);reservations.Add(new Vector4(position.x,position.y,position.z,radius));reservedAt.Add(Time.time);WaitingForSpawnSpace=false;
                nextSpawnAt=Time.time+Mathf.Lerp(minimumSpawnInterval,Mathf.Max(minimumSpawnInterval,maximumSpawnInterval),(float)random.NextDouble());return position;
            }
            order.RetryAt=Time.time+.25f;WaitingForSpawnSpace=true;message.text="OLEADA EN ESPERA · BUSCANDO ESPACIO SEGURO";return null;
        }
        bool SafeSpawn(Vector3 candidate,float radius,float height,int areaMask,out Vector3 position)
        {
            position=Vector3.zero;
            if(!NavMesh.SamplePosition(candidate,out var hit,.75f,areaMask))return false;position=hit.position;
            if(!(index<3?firstRoom:secondRoom).Contains(position)||Vector3.Distance(position,player.transform.position)<playerClearance+radius)return false;
            for(int r=0;r<reservations.Count;r++){var occupied=reservations[r];if(Time.time-reservedAt[r]>ReservationSeconds)continue;if(Vector3.Distance(position,new Vector3(occupied.x,occupied.y,occupied.z))<radius+occupied.w+separationPadding)return false;}
            if(!NavMesh.SamplePosition(player.transform.position,out var target,3,areaMask)||!NavMesh.CalculatePath(position,target.position,areaMask,spawnPath)||spawnPath.status!=NavMeshPathStatus.PathComplete)return false;
            Vector3 bottom=position+Vector3.up*(radius+.08f),top=position+Vector3.up*(height-radius+.08f);
            int count=Physics.OverlapCapsuleNonAlloc(bottom,top,radius,spawnBlockers,-1,QueryTriggerInteraction.Ignore);if(count==spawnBlockers.Length)return false;
            for(int i=0;i<count;i++){var blocker=spawnBlockers[i];if(blocker.transform.IsChildOf(player.transform)||blocker.bounds.max.y<=position.y+.08f)continue;return false;}
            return true;
        }
        void Register(GameObject enemy)
        {
            var visibility=GetComponent<LabRoomVisibility>();if(visibility)visibility.Track(enemy,index<3?1:2);
            var controller=enemy.GetComponent<Unity.FPS.AI.EnemyController>();if(enemy.GetComponent<Unity.FPS.AI.EnemyMobile>())controller.PatrolPath=playerHunt;if(controller.LootPrefab&&controller.LootPrefab.GetComponentInChildren<HealthPickup>(true))controller.DropRate=0;
            var health=enemy.GetComponent<Health>();alive.Add(health);UnityAction died=()=>alive.Remove(health);callbacks[health]=died;health.OnDie+=died;
        }
        void OnDestroy(){foreach(var pair in callbacks)if(pair.Key)pair.Key.OnDie-=pair.Value;}
    }
}
