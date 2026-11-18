using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] ShaderVariantCollection startupVariants;
        public void ConfigureShaderWarmup(ShaderVariantCollection variants)=>startupVariants=variants;
        static AsyncOperation outstanding,orphanCleanup;
        static CampaignFlow instance;
        ThreadPriority previousCampaignLoadingPriority;
        bool shuttingDown;
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
        void Awake(){instance=this;
            previousCampaignLoadingPriority=Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority=ThreadPriority.Low;
            // v35: bigger persistent upload ring and a 3 ms slice: room textures/meshes stream in during the airlock without sync uploads
            QualitySettings.asyncUploadBufferSize=Mathf.Max(QualitySettings.asyncUploadBufferSize,32);QualitySettings.asyncUploadTimeSlice=3;QualitySettings.asyncUploadPersistentBuffer=true;
            if(startCover){startCover.gameObject.SetActive(prologueStages>0);startCover.alpha=1;}
            if(startupVariants)startupVariants.WarmUp();}
        void OnDestroy()
        {
            if(instance==this)instance=null;
            Application.backgroundLoadingPriority=previousCampaignLoadingPriority;
            // A held async scene load blocks Unity's async queue. Release it on restart so its orphan callback can unload it.
            if(outstanding!=null&&!outstanding.isDone)outstanding.allowSceneActivation=true;
        }
        public IEnumerator DrainPendingLoadsForShutdown()
        {
            shuttingDown=true;StopAllCoroutines();
            var pending=outstanding;backgroundLoad=null;backgroundScene=null;
            if(pending!=null&&!pending.isDone)
            {
                pending.allowSceneActivation=true;
                while(!pending.isDone)yield return null;
            }
            while(orphanCleanup!=null&&!orphanCleanup.isDone)yield return null;
            Busy=false;
        }
        // v35: the HUD fonts are dynamic TMP atlases: a sector name or "PREPARANDO…" with glyphs not seen yet rasterised
        // them in the activation frame (FontEngine + atlas upload + canvas rebuild ≈ 40 ms). Fill every font the session
        // uses with the whole Spanish charset once, behind the start cover.
        const string WarmGlyphs=" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~¡¿·…—–«»ºª°×ÁÉÍÓÚÜÑáéíóúüñ";
        static bool glyphsWarmed;
        IEnumerator WarmFonts()
        {
            // Player only: in the Editor this would write glyphs into the font assets on disk
            if(glyphsWarmed||Application.isEditor)yield break;yield return null;glyphsWarmed=true;
            var fonts=new HashSet<TMP_FontAsset>();
            void Add(TMP_FontAsset f){if(!f||!fonts.Add(f))return;if(f.fallbackFontAssetTable!=null)foreach(var fb in f.fallbackFontAssetTable)Add(fb);}
            Add(TMP_Settings.defaultFontAsset);
            if(TMP_Settings.fallbackFontAssets!=null)foreach(var f in TMP_Settings.fallbackFontAssets)Add(f);
            foreach(var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))Add(t.font);
            foreach(var f in fonts){if(f.atlasPopulationMode!=AtlasPopulationMode.Static)f.TryAddCharacters(WarmGlyphs,out _);yield return null;}
        }
        public int PrologueStages=>prologueStages;
        public void ConfigurePrologue(int stages,CanvasGroup cover){prologueStages=stages;startCover=cover;}
        // Called by the prologue once its own fade covers the screen (or right after the first stage when there is none).
        public void ReleaseStartCover(){if(startCover){startCover.alpha=0;startCover.blocksRaycasts=false;startCover.gameObject.SetActive(false);}}
        string Sector(int target)=>"SECTOR "+(target+1-prologueStages);
        public void Configure(PlayerCharacterController owner,CombatLabMenu lab,TMP_Text label,string[] scenes,CampaignAirlock[] doors,GameObject first){player=owner;menu=lab;message=label;stageScenes=scenes;airlocks=doors;initialDoor=first;}
        IEnumerator Start(){StartCoroutine(WarmFonts());yield return Prepare(0);}
        void Update()
        {
            if(shuttingDown||Busy||!CurrentRoom||!menu.IsPlaying||player.IsDead)return;
            if(CurrentStage<airlocks.Length&&CurrentRoom.Completed)AirlockDoorMotion.Set(this,airlocks[CurrentStage].SourceDoor,false);
        }
        public bool TryTransition(int source)
        {
            if(shuttingDown||Busy||source!=CurrentStage||!CurrentRoom||!CurrentRoom.Completed||!menu.IsPlaying||player.IsDead||Time.timeScale<=0)return false;
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
        AsyncOperation backgroundLoad;
        string backgroundScene;

        void PreloadFollowingStage()
        {
            int following=CurrentStage+1;
            if(shuttingDown||!preloadNextStage||following>=stageScenes.Length||backgroundLoad!=null&&!backgroundLoad.isDone)return;
            backgroundScene=stageScenes[following];
            if(!Application.CanStreamedLevelBeLoaded(backgroundScene))return;
            string expected=backgroundScene;
            backgroundLoad=SceneManager.LoadSceneAsync(expected,LoadSceneMode.Additive);
            backgroundLoad.allowSceneActivation=false;outstanding=backgroundLoad;
            backgroundLoad.completed+=_=>{if(!this){var orphan=SceneManager.GetSceneByName(expected);if(orphan.isLoaded)orphanCleanup=SceneManager.UnloadSceneAsync(orphan);}};
        }
        float transitionPeak;readonly float[] phasePeak=new float[4];int phase,peakFrame,frames;float peakProgress;AsyncOperation tracked;
        public float LastTransitionPeakMs{get;private set;}
        // v35: timings of the last stage change (seconds), for the campaign tour benchmark
        public struct TransitionReport{public int target;public string scene;public float doorSec,loadSec,activateSec,unloadSec,settleSec,sweepSec,totalSec,peakMs,doorPeakMs,activatePeakMs,unloadPeakMs,settlePeakMs;public int frames;}
        public TransitionReport LastReport{get;private set;}
        public int TransitionSerial{get;private set;}
        // v35: the old room is behind a shut door: destroy it in slices (a few hundred transforms per frame) so the
        // final UnloadSceneAsync only frees an empty scene instead of tearing down the whole room in one frame
        public static bool SkipWarm;
        public static int TeardownBudget=120;
        readonly List<Transform> teardownBuffer=new List<Transform>(8192);
        readonly List<GameObject> teardownRoots=new List<GameObject>(64);
        IEnumerator Teardown(Scene scene)
        {
            if(TeardownBudget<=0)yield break;
            teardownRoots.Clear();scene.GetRootGameObjects(teardownRoots);
            int spent=0;
            for(int i=0;i<teardownRoots.Count;i++)
            {
                var root=teardownRoots[i];if(!root)continue;
                // Stop room-owned behaviours before their referenced children disappear. Keep the disable
                // frame separate from destruction, including roots whose single child is a whole robot/kit.
                root.SetActive(false);TrackPeak();yield return null;
                teardownBuffer.Clear();root.GetComponentsInChildren(true,teardownBuffer);
                // GetComponentsInChildren is preorder: reversing it schedules children before their parents.
                // Every object counts against the budget; a large direct child can no longer bypass it.
                for(int node=teardownBuffer.Count-1;node>=0;node--)
                {
                    var item=teardownBuffer[node];if(!item)continue;
                    Destroy(item.gameObject);spent++;
                    if(spent>=TeardownBudget){spent=0;TrackPeak();yield return null;}
                }
            }
            TrackPeak();yield return null;
            teardownRoots.Clear();teardownBuffer.Clear();
        }
        IEnumerator WarmRoom(CampaignRoom room)
        {
            if(SkipWarm)yield break;
            var source=player?player.PlayerCamera:null;if(!source||!room)yield break;
            var rends=new List<Renderer>();foreach(var root in room.gameObject.scene.GetRootGameObjects())root.GetComponentsInChildren(false,rends);
            if(rends.Count==0)yield break;Bounds b=rends[0].bounds;foreach(var r in rends)if(!(r is ParticleSystemRenderer))b.Encapsulate(r.bounds);
            var go=new GameObject("V35_RoomWarmup");var cam=go.AddComponent<Camera>();cam.CopyFrom(source);cam.enabled=false;cam.fieldOfView=100;cam.farClipPlane=Mathf.Max(cam.farClipPlane,b.size.magnitude);
            var rt=RenderTexture.GetTemporary(320,180,24);cam.targetTexture=rt;
            var eye=new Vector3(b.center.x,Mathf.Min(b.center.y,b.min.y+3f),b.center.z);
            try{foreach(var dir in WarmDirections){cam.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(dir));cam.Render();TrackPeak();yield return null;}}
            finally{cam.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Destroy(go);}
        }
        static readonly Vector3[] WarmDirections={Vector3.forward,Vector3.back,Vector3.left,Vector3.right,new Vector3(0,.6f,1),new Vector3(0,.6f,-1)};
        public int Phase=>Busy?phase:-1; // v35: 0 door/load, 1 activation, 2 unload, 3 settle
        public bool PreloadNextStage{get=>preloadNextStage;set=>preloadNextStage=value;}

        IEnumerator Prepare(int target)
        {
            Busy=true;float began=Time.realtimeSinceStartup;float tStart=began,tLoad=0,tLoaded=0,tActivate=0,tActivated=0,tUnload=0,tUnloaded=0,tSettled=0,tSweep=0,tSwept=0;transitionPeak=0;System.Array.Clear(phasePeak,0,phasePeak.Length);phase=0;frames=0;peakFrame=0;peakProgress=0;tracked=null;bool preload=preloadNextStage;
            var sessionScene=gameObject.scene;
            if(!neutralCaptured){neutralEnv=ZoneEnvironment.Capture();neutralCaptured=true;}
            // preload the next stage in the background, activation held until the door behind the player is shut
            if(!Application.CanStreamedLevelBeLoaded(stageScenes[target])){message.text="ERROR · FASE NO DISPONIBLE";Debug.LogError(message.text+": "+stageScenes[target],this);yield break;}
            bool adopting=preload&&backgroundLoad!=null&&backgroundScene==stageScenes[target];
            while(!adopting&&outstanding!=null&&!outstanding.isDone||orphanCleanup!=null&&!orphanCleanup.isDone)yield return null;
            var previousPriority=Application.backgroundLoadingPriority;Application.backgroundLoadingPriority=ThreadPriority.Low;
            string expected=stageScenes[target];AsyncOperation load=adopting?backgroundLoad:null;
            backgroundLoad=null;backgroundScene=null;if(load!=null)tracked=load;
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
                tLoad=Time.realtimeSinceStartup;
                if(preload){if(load==null)load=StartLoad(true);tracked=load;while(load.progress<.9f){TrackPeak();yield return null;}}
                tLoaded=Time.realtimeSinceStartup;
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
            phase=1;tActivate=Time.realtimeSinceStartup;load.allowSceneActivation=true;
            while(!load.isDone){message.text=target<prologueStages?"":"PREPARANDO "+Sector(target)+"…";TrackPeak();yield return null;}
            tActivated=Time.realtimeSinceStartup;
            Application.backgroundLoadingPriority=previousPriority;
            var scene=SceneManager.GetSceneByName(expected);
            CampaignRoom next=null;
            foreach(var root in scene.GetRootGameObjects()){var room=root.GetComponentInChildren<CampaignRoom>(true);if(room){next=room;break;}}
            if(!next){message.text="ERROR · FASE INCOMPLETA";Debug.LogError(message.text,this);yield break;}
            // the new zone's own render settings become the blend target; start from where the old zone faded to
            SceneManager.SetActiveScene(scene);var toEnv=ZoneEnvironment.Capture();ZoneEnvironment.Apply(fromEnv,toEnv,0);
            fadeIn.Bind(scene);fadeIn.Set(0);
            TrackPeak();yield return null;
            phase=2;tUnload=Time.realtimeSinceStartup;if(CurrentRoom){CurrentRoom=null;if(oldScene.IsValid()&&oldScene.isLoaded){yield return Teardown(oldScene);var unload=SceneManager.UnloadSceneAsync(oldScene);while(!unload.isDone){TrackPeak();yield return null;}}}
            tUnloaded=Time.realtimeSinceStartup;
            CurrentRoom=next;phase=3;
            TrackPeak();yield return null;
            // v35: the doors are still shut: draw the new room once, a sixth per frame, from a hidden low-res camera so its
            // shaders/PSOs and textures are ready before the player sees it (the first-sight hitch moves behind the door)
            yield return WarmRoom(next);
            while(!CurrentRoom.Ready||Time.realtimeSinceStartup-began<(target<prologueStages?0:minimumPreparationSeconds)){message.text=target<prologueStages?"":"PREPARANDO "+Sector(target)+"…";TrackPeak();yield return null;}
            // Keep loaded asset templates resident. A full sweep here synchronizes with the main thread and can trigger
            // collection just as the door opens. Scene-owned instances are already torn down in bounded slices above.
            tSettled=Time.realtimeSinceStartup;tSweep=tSettled;
            tSwept=Time.realtimeSinceStartup;
            TrackPeak();
            CurrentStage=target;Busy=false;
            LastTransitionPeakMs=transitionPeak*1000f;
            LastReport=new TransitionReport{target=target,scene=expected,doorSec=tLoad>0?tLoad-tStart:0,loadSec=tLoad>0?tLoaded-tLoad:0,activateSec=tActivated-tActivate,unloadSec=tUnloaded-tUnload,settleSec=tSettled-tUnloaded,sweepSec=tSwept-tSweep,totalSec=tSwept-tStart,peakMs=transitionPeak*1000f,doorPeakMs=phasePeak[0]*1000f,activatePeakMs=phasePeak[1]*1000f,unloadPeakMs=phasePeak[2]*1000f,settlePeakMs=phasePeak[3]*1000f,frames=frames};TransitionSerial++;Debug.Log("[Campaign] transition to "+expected+(preload?" (preload)":" (v2 order)")+" peak frame "+LastTransitionPeakMs.ToString("0.0")+" ms · door+preload "+(phasePeak[0]*1000).ToString("0")+" / activate "+(phasePeak[1]*1000).ToString("0")+" / unload "+(phasePeak[2]*1000).ToString("0")+" / settle "+(phasePeak[3]*1000).ToString("0")+" · peak at frame "+peakFrame+"/"+frames+" load "+peakProgress.ToString("0.00"),this);
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
            PreloadFollowingStage();
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
