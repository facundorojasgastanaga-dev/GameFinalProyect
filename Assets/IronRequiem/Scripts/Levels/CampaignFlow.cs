using System.Collections;
using TMPro;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem
{
    public sealed class CampaignFlow : MonoBehaviour
    {
        [SerializeField] PlayerCharacterController player;
        [SerializeField] CombatLabMenu menu;
        [SerializeField] TMP_Text message;
        [SerializeField] string[] stageScenes;
        [SerializeField] CampaignAirlock[] airlocks;
        [SerializeField] GameObject initialDoor;
        [SerializeField,Min(0)] float minimumPreparationSeconds=1.5f;
        [Tooltip("Leading exploration stages (Room00_Hangar) that are not counted in the SECTOR numbering.")]
        [SerializeField,Min(0)] int prologueStages;
        [Tooltip("Black cover shown while the first stage loads; the prologue releases it when its own fade owns the screen.")]
        [SerializeField] CanvasGroup startCover;
        static AsyncOperation outstanding,orphanCleanup;
        static CampaignFlow instance;
        public static bool Owns(PlayerCharacterController owner)=>owner&&instance&&instance.player==owner&&owner.gameObject.scene.isLoaded;
        public static void PlaceWorldModule(GameObject module)
        {
            if(instance&&instance.CurrentRoom&&module.transform.parent==null&&module.scene!=instance.CurrentRoom.gameObject.scene)SceneManager.MoveGameObjectToScene(module,instance.CurrentRoom.gameObject.scene);
        }
        public PlayerCharacterController Player=>player;
        public CombatLabMenu Menu=>menu;
        public TMP_Text Message=>message;
        public CampaignRoom CurrentRoom {get;private set;}
        public int CurrentStage {get;private set;}=-1;
        public bool Busy {get;private set;}
        public string[] StageScenes=>stageScenes;
        public CampaignAirlock[] Airlocks=>airlocks;
        public int LoadedStageCount {get {int count=0;foreach(string name in stageScenes)if(SceneManager.GetSceneByName(name).isLoaded)count++;return count;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){outstanding=null;orphanCleanup=null;instance=null;}
        void Awake(){instance=this;if(startCover){startCover.gameObject.SetActive(prologueStages>0);startCover.alpha=1;}}
        void OnDestroy(){if(instance==this)instance=null;}
        public int PrologueStages=>prologueStages;
        public void ConfigurePrologue(int stages,CanvasGroup cover){prologueStages=stages;startCover=cover;}
        // Called by the prologue once its own fade covers the screen (or right after the first stage when there is none).
        public void ReleaseStartCover(){if(startCover){startCover.alpha=0;startCover.blocksRaycasts=false;startCover.gameObject.SetActive(false);}}
        string Sector(int target)=>"SECTOR "+(target+1-prologueStages);
        public void Configure(PlayerCharacterController owner,CombatLabMenu lab,TMP_Text label,string[] scenes,CampaignAirlock[] doors,GameObject first){player=owner;menu=lab;message=label;stageScenes=scenes;airlocks=doors;initialDoor=first;}
        IEnumerator Start(){yield return Prepare(0);}
        void Update()
        {
            if(Busy||!CurrentRoom||!menu.IsPlaying||player.IsDead)return;
            if(CurrentStage<airlocks.Length&&CurrentRoom.Completed)AirlockDoorMotion.Set(this,airlocks[CurrentStage].SourceDoor,false);
        }
        public bool TryTransition(int source)
        {
            if(Busy||source!=CurrentStage||!CurrentRoom||!CurrentRoom.Completed||!menu.IsPlaying||player.IsDead||Time.timeScale<=0)return false;
            StartCoroutine(Prepare(source+1));return true;
        }
        // v3 (12/11/2026): clean zone changes. The next stage is preloaded (activation held) as soon as the player steps
        // into the airlock, while the door closes; it is activated only once the door behind the player is shut, then the
        // old stage is unloaded. Fog/ambient/reflections, global post volumes and looping ambience are blended across the
        // change instead of snapping. The longest frame of every transition is logged ([Campaign] transition ...).
        [Tooltip("Seconds to blend fog, ambient light and post-processing between zones.")]
        [SerializeField,Min(0)] float environmentBlendSeconds=1.6f;
        readonly ZoneEnvironment.SceneFade fadeOut=new ZoneEnvironment.SceneFade(),fadeIn=new ZoneEnvironment.SceneFade();
        ZoneEnvironment.Settings neutralEnv;bool neutralCaptured;
        [Tooltip("Preload the next stage while the airlock door closes (off = v2 order: unload, then load).")]
        [SerializeField] bool preloadNextStage=true;
        float transitionPeak;readonly float[] phasePeak=new float[4];int phase,peakFrame,frames;float peakProgress;AsyncOperation tracked;
        public float LastTransitionPeakMs{get;private set;}
        public bool PreloadNextStage{get=>preloadNextStage;set=>preloadNextStage=value;}

        IEnumerator Prepare(int target)
        {
            Busy=true;float began=Time.realtimeSinceStartup;transitionPeak=0;System.Array.Clear(phasePeak,0,phasePeak.Length);phase=0;frames=0;peakFrame=0;peakProgress=0;tracked=null;bool preload=preloadNextStage;
            var sessionScene=gameObject.scene;
            if(!neutralCaptured){neutralEnv=ZoneEnvironment.Capture();neutralCaptured=true;}
            // preload the next stage in the background, activation held until the door behind the player is shut
            if(!Application.CanStreamedLevelBeLoaded(stageScenes[target])){message.text="ERROR · FASE NO DISPONIBLE";Debug.LogError(message.text+": "+stageScenes[target],this);yield break;}
            while(outstanding!=null&&!outstanding.isDone||orphanCleanup!=null&&!orphanCleanup.isDone)yield return null;
            var previousPriority=Application.backgroundLoadingPriority;Application.backgroundLoadingPriority=ThreadPriority.Low;
            string expected=stageScenes[target];AsyncOperation load=null;
            AsyncOperation StartLoad(bool hold){var op=SceneManager.LoadSceneAsync(expected,LoadSceneMode.Additive);op.allowSceneActivation=!hold;outstanding=op;op.completed+=_=>{if(!this){var orphan=SceneManager.GetSceneByName(expected);if(orphan.isLoaded)orphanCleanup=SceneManager.UnloadSceneAsync(orphan);}};return op;}
            // the first stage loads at once (behind the start cover); later stages start loading once the door behind the
            // player is shut, so even an Editor load (synchronous there) happens out of sight, and a player build streams
            // it in the background during the airlock cycle
            if(preload&&CurrentStage<0){load=StartLoad(true);tracked=load;}
            Scene oldScene=CurrentRoom?CurrentRoom.gameObject.scene:default;
            ZoneEnvironment.Settings fromEnv=ZoneEnvironment.Capture();
            if(CurrentStage>=0)
            {
                // Never unload a sector with its door open or in view: shut the door behind the player and wait until it is
                // fully closed with the player inside the airlock volume (same rule for every double-door airlock).
                var transit=airlocks[CurrentStage];AirlockDoorMotion.Set(this,transit.SourceDoor,true);
                if(transit.Cycle)transit.Cycle.Begin();
                fadeOut.Bind(oldScene);float closing=Time.time;
                float outsideSince=-1;
                while(!AirlockDoorMotion.IsClosed(transit.SourceDoor)||!transit.PlayerInside||load!=null&&load.progress<.9f||Time.time-closing<environmentBlendSeconds)
                {
                    // the old zone's post and ambience fade out while the door closes; fog/ambient settle on the airlock's (session) values
                    float k=Mathf.SmoothStep(0,1,(Time.time-closing)/environmentBlendSeconds);fadeOut.Set(1-k);ZoneEnvironment.Apply(fromEnv,neutralEnv,k);
                    // the player stepped back out while the door shut: reopen it, drop the preloaded stage and abort
                    if(AirlockDoorMotion.IsClosed(transit.SourceDoor)&&!transit.PlayerInside){if(outsideSince<0)outsideSince=Time.time;else if(Time.time-outsideSince>1f)
                    {
                        AirlockDoorMotion.Set(this,transit.SourceDoor,false);if(transit.Cycle)transit.Cycle.Abort();fadeOut.Set(1);ZoneEnvironment.Apply(fromEnv,fromEnv,1);
                        if(load!=null){load.allowSceneActivation=true;while(!load.isDone)yield return null;}var dropped=SceneManager.GetSceneByName(expected);if(dropped.isLoaded)yield return SceneManager.UnloadSceneAsync(dropped);
                        Application.backgroundLoadingPriority=previousPriority;Busy=false;yield break;
                    }}
                    else outsideSince=-1;
                    TrackPeak();yield return null;
                }
                fadeOut.Set(0);ZoneEnvironment.Apply(fromEnv,neutralEnv,1);fromEnv=neutralEnv;
                if(preload){load=StartLoad(true);tracked=load;while(load.progress<.9f){TrackPeak();yield return null;}}
                began=Time.realtimeSinceStartup;
            }
            // The session and equipped inventory stay loaded. Discard only the room-owned corpses, drops and pools.
            var carrier=player.GetComponent<StolenWeaponCarrier>();
            if(carrier&&carrier.Held&&carrier.Held.transform.parent==null&&carrier.Held.gameObject.scene!=sessionScene)SceneManager.MoveGameObjectToScene(carrier.Held.gameObject,sessionScene);
            SceneManager.SetActiveScene(sessionScene);
            if(load==null)
            {
                // v2 order (measurement baseline): unload the old stage, then load the next one
                phase=2;if(CurrentRoom){CurrentRoom=null;if(oldScene.IsValid()&&oldScene.isLoaded){var u=SceneManager.UnloadSceneAsync(oldScene);while(!u.isDone){TrackPeak();yield return null;}}}
                load=StartLoad(false);
            }
            // staged: activate the preloaded stage (its objects wake up behind the closed door), next frame unload the old one
            phase=1;load.allowSceneActivation=true;
            while(!load.isDone){message.text=target<prologueStages?"":"PREPARANDO "+Sector(target)+"…";TrackPeak();yield return null;}
            Application.backgroundLoadingPriority=previousPriority;
            var scene=SceneManager.GetSceneByName(expected);
            CampaignRoom next=null;
            foreach(var root in scene.GetRootGameObjects()){var room=root.GetComponentInChildren<CampaignRoom>(true);if(room){next=room;break;}}
            if(!next){message.text="ERROR · FASE INCOMPLETA";Debug.LogError(message.text,this);yield break;}
            // the new zone's own render settings become the blend target; start from where the old zone faded to
            SceneManager.SetActiveScene(scene);var toEnv=ZoneEnvironment.Capture();ZoneEnvironment.Apply(fromEnv,toEnv,0);
            fadeIn.Bind(scene);fadeIn.Set(0);
            TrackPeak();yield return null;
            phase=2;if(CurrentRoom){CurrentRoom=null;if(oldScene.IsValid()&&oldScene.isLoaded){var unload=SceneManager.UnloadSceneAsync(oldScene);while(!unload.isDone){TrackPeak();yield return null;}}}
            CurrentRoom=next;phase=3;
            TrackPeak();yield return null;
            while(!CurrentRoom.Ready||Time.realtimeSinceStartup-began<(target<prologueStages?0:minimumPreparationSeconds)){message.text=target<prologueStages?"":"PREPARANDO "+Sector(target)+"…";TrackPeak();yield return null;}
            // a full asset sweep is a long frame: only between combat sectors, where the "PREPARANDO" wait already covers it
            if(target>prologueStages||!preload&&target>0)yield return Resources.UnloadUnusedAssets();
            TrackPeak();
            CurrentStage=target;Busy=false;
            LastTransitionPeakMs=transitionPeak*1000f;Debug.Log("[Campaign] transition to "+expected+(preload?" (preload)":" (v2 order)")+" peak frame "+LastTransitionPeakMs.ToString("0.0")+" ms · door+preload "+(phasePeak[0]*1000).ToString("0")+" / activate "+(phasePeak[1]*1000).ToString("0")+" / unload "+(phasePeak[2]*1000).ToString("0")+" / settle "+(phasePeak[3]*1000).ToString("0")+" · peak at frame "+peakFrame+"/"+frames+" load "+peakProgress.ToString("0.00"),this);
            StartCoroutine(BlendIn(toEnv));
            if(target==0)AirlockDoorMotion.Set(this,initialDoor,false);
            else
            {
                var arrived=airlocks[target-1];AirlockDoorMotion.Set(this,arrived.DestinationDoor,false);
                foreach(var extra in arrived.ExtraDestinationDoors)AirlockDoorMotion.Set(this,extra,false);
                if(arrived.Cycle)arrived.Cycle.Complete();
            }
            message.text=CurrentRoom.HasTitle?CurrentRoom.PrologueTitle:target==stageScenes.Length-1?"PERSECUCIÓN · ATRAVIESA LA PUERTA":Sector(target)+" LISTO · TRES RONDAS";
            if(!CurrentRoom.IsPrologue)ReleaseStartCover();
        }
        void TrackPeak(){frames++;float dt=Time.unscaledDeltaTime;if(Time.frameCount<=2)return;if(dt>transitionPeak){transitionPeak=dt;peakFrame=frames;peakProgress=tracked!=null?tracked.progress:-1;}if(dt>phasePeak[phase])phasePeak[phase]=dt;}
        IEnumerator BlendIn(ZoneEnvironment.Settings toEnv)
        {
            var fromEnv=ZoneEnvironment.Capture();float start=Time.time;
            while(Time.time-start<environmentBlendSeconds){float k=Mathf.SmoothStep(0,1,(Time.time-start)/environmentBlendSeconds);ZoneEnvironment.Apply(fromEnv,toEnv,k);fadeIn.Set(k);yield return null;}
            ZoneEnvironment.Apply(fromEnv,toEnv,1);fadeIn.Set(1);
        }
    }
}
