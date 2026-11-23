using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace IronRequiem.Tests
{
    public sealed class CampaignTests
    {
        const string Main="Assets/IronRequiem/Scenes/RequiemCampaign.unity",Report="Tools/IronRequiem/Reports/Campaign/";
        Keyboard keys;Mouse mouse;bool background;float capture;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
        CampaignFlow flow;PlayerCharacterController player;Health health;
        [UnitySetUp]public IEnumerator Setup()
        {
            background=Application.runInBackground;capture=Time.captureDeltaTime;Application.runInBackground=true;Time.captureDeltaTime=1f/60;Time.timeScale=1;AudioListener.pause=false;devices=InputSystem.actions.devices;keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keys.MakeCurrent();mouse.MakeCurrent();InputSystem.actions.devices=new InputDevice[]{keys,mouse};SceneManager.sceneLoaded+=FixedWaves;
            EditorSceneManager.LoadSceneInPlayMode(Main,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;Bind();yield return Until(()=>flow&&!flow.Busy&&flow.CurrentStage==0,60,"Initial streamed room");flow.Menu.BeginGame();health.Invincible=true;yield return null;
            yield return PassPrologue();
        }
        // 12/11/2026: Room00_Hangar (exploration prologue + wake-up cinematic) comes before the combat rooms.
        IEnumerator PassPrologue()
        {
            if(!flow.CurrentRoom||!flow.CurrentRoom.IsPrologue)yield break;
            var intro=Object.FindAnyObjectByType<HangarIntroCinematic>();Assert.That(intro,Is.Not.Null,"Hangar intro cinematic");
            // v5: the campaign starts with no weapon at all: the right hand strikes (F, and M1 while unarmed)
            AssertUnarmed("Campaign start");
            yield return Until(()=>intro.IsPlaying||intro.Finished,10,"Intro starts");intro.RequestSkip();
            yield return Until(()=>flow.CurrentRoom.Completed&&!PlayerInputLock.Locked,5,"Skip hands control back");
            var glass=Object.FindAnyObjectByType<CapsuleGlassBreak>();if(glass)Assert.That(glass.IsShattered,Is.True,"Skip leaves the canopy broken");
            {   // v5: unarmed, M1 throws the right-hand quick strike; the left hand stays hidden
                var melee=player.GetComponent<ProtagonistMelee>();var hands=Object.FindAnyObjectByType<ProtagonistHandsPresentation>();
                yield return new WaitForSeconds(.3f);Assert.That(hands.RightHandVisible,Is.True,"Unarmed: right hand visible");
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left,true));yield return Until(()=>melee.IsAttacking,1,"M1 strikes while unarmed");
                Assert.That(melee.LeftStrike,Is.False,"Unarmed M1 strike is the right hand");InputSystem.QueueStateEvent(mouse,new MouseState());
                yield return new WaitForSeconds(.1f);Assert.That(hands.RightFistClosure,Is.GreaterThan(.95f),"v7: the striking right hand is a closed fist within 0.1 s");
                yield return null;Assert.That(hands.LeftHandVisible,Is.False,"Left hand hidden during the right strike");
                yield return Until(()=>!melee.IsAttacking,2,"Quick strike resolves");Assert.That(melee.LastWasHeavy,Is.False);
            }
            // connection chamber: the hangar is only unloaded once its door is shut behind the player
            // wait for the hangar door to be fully open, otherwise it closes again in a frame or two and the check is moot
            yield return Until(()=>!flow.Airlocks[0].SourceDoor.activeSelf,10,"Hangar door opens after the prologue");
            var hangar=flow.CurrentRoom.gameObject.scene.name;Place(new Vector3(0,.03f,-80f));
            yield return Until(()=>flow.Busy,8,"Enter connection chamber");
            Assert.That(SceneManager.GetSceneByName(hangar).isLoaded,Is.True,"Hangar stays loaded while its door closes");
            yield return Until(()=>!flow.Busy&&flow.CurrentStage==1,90,"Alarm corridor streams in");
            Assert.That(flow.Airlocks[0].SourceDoor.activeSelf,Is.True,"Hangar door shut");Assert.That(SceneManager.GetSceneByName(hangar).isLoaded,Is.False);
            // alarm corridor: two short waves on the round system, then the lobby airlock opens
            var rounds=flow.CurrentRoom.Rounds;Assert.That(rounds,Is.Not.Null,"Corridor uses the round system");
            Place(new Vector3(0,.03f,-62f));
            // like a player clearing the room: let the whole round come out of the ducts, then clear it. No Freeze() here:
            // the Carroñeros (SawBot rig) resolve their death in SawBotCombat, which Freeze disables.
            for(int wave=0;wave<2;wave++)
            {
                float end=Time.realtimeSinceStartup+45;
                while(!(rounds.WaveIndex==wave&&rounds.PendingSpawns==0&&rounds.LivingEnemies>0)&&!rounds.Completed&&Time.realtimeSinceStartup<end){Assert.That(flow.LoadedStageCount,Is.LessThanOrEqualTo(flow.Busy?2:1));yield return null;}
                Assert.That(rounds.WaveIndex==wave&&rounds.PendingSpawns==0,Is.True,"Corridor wave "+(wave+1)+" spawned (index="+rounds.WaveIndex+" living="+rounds.LivingEnemies+" pending="+rounds.PendingSpawns+")");
                foreach(var h in rounds.Living.ToArray())if(h&&h.CurrentHealth>0)h.Kill();
                end=Time.realtimeSinceStartup+15;
                while(!(rounds.Completed||rounds.WaveIndex>wave)&&Time.realtimeSinceStartup<end)yield return null;
                Assert.That(rounds.Completed||rounds.WaveIndex>wave,Is.True,"Corridor wave "+(wave+1)+" cleared");
            }
            yield return Until(()=>rounds.Completed,8,"Corridor cleared");yield return Until(()=>!flow.Airlocks[1].SourceDoor.activeSelf,8,"Lobby door opens");
            // v5: the montage station in the lobby airlock: E starts the sequence, holding Space skips it (same end state)
            AssertUnarmed("Before the montage station");
            var weapons=player.GetComponent<PlayerWeaponsManager>();var station=Object.FindAnyObjectByType<WeaponMountStation>();Assert.That(station,Is.Not.Null,"Montage station in the lobby");
            Place(station.transform.position-station.transform.forward*.5f+Vector3.up*.03f);yield return new WaitForSeconds(.3f);
            Input(Key.E);yield return null;yield return null;Input();yield return Until(()=>station.Busy,2,"E starts the mounting sequence");
            Assert.That(PlayerInputLock.Locked,Is.True,"Input locked during the sequence");
            yield return new WaitForSeconds(2f);Assert.That(station.SequenceTime,Is.GreaterThan(1.6f),"Sequence advances (hand off)");PresentationShot("station-hand-off");
            Input(Key.Space);yield return Until(()=>!station.Busy,3,"Holding Space skips");Input();
            Assert.That(station.WasSkipped&&station.Granted,Is.True,"Skipped and granted");Assert.That(PlayerInputLock.Locked,Is.False,"Control back");
            Assert.That(weapons.HasWeapon(station.WeaponPrefab),Is.Not.Null,"Cannon in the inventory");Assert.That(weapons.GetActiveWeapon().SourcePrefab,Is.SameAs(station.WeaponPrefab.gameObject),"Cannon selected after mounting");
            Assert.That(weapons.GetActiveWeapon().IsWeaponActive,Is.True,"Cannon up, no drop/raise");
            int slots=Enumerable.Range(0,9).Count(i=>weapons.GetWeaponAtSlotIndex(i));Assert.That(slots,Is.EqualTo(1),"Only the cannon");
            Input(Key.E);yield return null;yield return null;Input();yield return new WaitForSeconds(.3f);
            Assert.That(station.Busy,Is.False,"Station never repeats");Assert.That(Enumerable.Range(0,9).Count(i=>weapons.GetWeaponAtSlotIndex(i)),Is.EqualTo(1),"Station is idempotent");
            Place(new Vector3(0,.03f,-30.5f));yield return Until(()=>!flow.Busy&&flow.CurrentStage==flow.PrologueStages,90,"Lobby airlock streams Room01");
        }
        void AssertUnarmed(string label)
        {
            var weapons=player.GetComponent<PlayerWeaponsManager>();var owned=Enumerable.Range(0,9).Select(weapons.GetWeaponAtSlotIndex).Where(w=>w).ToArray();
            Assert.That(owned.Length,Is.EqualTo(0),label+": no weapon ("+string.Join(",",owned.Select(w=>w.name))+")");Assert.That(weapons.GetActiveWeapon(),Is.Null);
            var heat=Object.FindAnyObjectByType<WeaponHeatHud>();if(heat){var group=heat.GetComponent<CanvasGroup>();if(group)Assert.That(group.alpha,Is.LessThan(.05f),label+": no heat bar unarmed");}
        }
        static void FixedWaves(Scene scene,LoadSceneMode mode)
        {
            if(scene.name=="Room00b_AlarmCorridor")return; // fixed-point prologue waves: no variety to pin down
            foreach(var root in scene.GetRootGameObjects())foreach(var waves in root.GetComponentsInChildren<CombatTwoRoomsLab>())
            {
                var data=new SerializedObject(waves);var list=data.FindProperty("waves");for(int i=0;i<list.arraySize;i++)list.GetArrayElementAtIndex(i).FindPropertyRelative("Variations").arraySize=0;data.ApplyModifiedPropertiesWithoutUndo();waves.ConfigureVariety(true,17);
            }
        }
        void Bind(){flow=Object.FindAnyObjectByType<CampaignFlow>();if(flow){player=flow.Player;health=player.GetComponent<Health>();}}
        [UnityTearDown]public IEnumerator Cleanup()
        {
            Input();Time.timeScale=1;AudioListener.pause=false;SceneManager.sceneLoaded-=FixedWaves;var clean=SceneManager.CreateScene("CampaignCleanup"+Time.frameCount);SceneManager.SetActiveScene(clean);
            var session=Object.FindAnyObjectByType<CampaignFlow>();if(session)yield return session.DrainPendingLoadsForShutdown();
            foreach(var queue in Object.FindObjectsByType<EnemySpawnQueue>()){queue.CancelPending();queue.enabled=false;}foreach(var waves in Object.FindObjectsByType<CombatTwoRoomsLab>())waves.enabled=false;Freeze();foreach(var bar in Object.FindObjectsByType<WorldspaceHealthBar>())bar.enabled=false;
            var loaded=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s!=clean&&s.isLoaded).OrderBy(s=>s.name=="RequiemCampaign"?1:0).ToArray();foreach(var scene in loaded)if(scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);yield return null;
            Time.captureDeltaTime=capture;InputSystem.actions.devices=devices;InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);Cursor.lockState=CursorLockMode.None;Application.runInBackground=background;
        }
        void Input(params Key[] held)=>InputSystem.QueueStateEvent(keys,new KeyboardState(held));
        void Place(Vector3 point,float yaw=0){player.GetComponent<AdvancedPlayerMovement>().ResetForCheckpoint();player.TeleportToCheckpoint(point);player.transform.rotation=Quaternion.Euler(0,yaw,0);player.PlayerCamera.transform.localRotation=Quaternion.identity;Physics.SyncTransforms();}
        void Freeze()
        {
            foreach(var enemy in Object.FindObjectsByType<EnemyController>())
            {
                foreach(var component in enemy.GetComponents<MonoBehaviour>())if(component is EnemyController||component is EnemyMobile||component is EnemyTurret||component is BigBotCombat||component is SawBotCombat)component.enabled=false;
                var agent=enemy.NavMeshAgent;if(agent&&agent.enabled&&agent.isOnNavMesh){agent.isStopped=true;agent.velocity=Vector3.zero;}
            }
        }
        IEnumerator Until(Func<bool> condition,float seconds,string label)
        {
            float end=Time.realtimeSinceStartup+seconds;
            while(!condition()&&Time.realtimeSinceStartup<end){Freeze();if(flow)Assert.That(flow.LoadedStageCount,Is.LessThanOrEqualTo(flow.Busy?2:1),"Never two gameplay rooms loaded (two only for the frames of a swap behind a shut airlock door)");yield return null;}
            Assert.That(condition(),Is.True,label+" stage="+(flow?flow.CurrentStage:-99)+" player="+(player?player.transform.position.ToString():"none"));
        }
        void Capture(string name)
        {
            var camera=player.PlayerCamera;var rt=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);var previous=RenderTexture.active;var target=camera.targetTexture;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Report+name+".png",texture.EncodeToPNG());}
            finally{camera.targetTexture=target;RenderTexture.active=previous;Object.Destroy(rt);Object.Destroy(texture);}
        }
        void CheckStage(StringBuilder log,int index)
        {
            Assert.That(flow.LoadedStageCount,Is.EqualTo(1));Assert.That(Object.FindObjectsByType<PlayerCharacterController>().Length,Is.EqualTo(1));Assert.That(Object.FindObjectsByType<AudioListener>().Length,Is.EqualTo(1));
            var room=flow.CurrentRoom;var data=new SerializedObject(room.Rounds);Assert.That(data.FindProperty("waves").arraySize,Is.EqualTo(3));Assert.That(data.FindProperty("singleArena").boolValue,Is.True);
            var lights=room.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).ToArray();Assert.That(lights.Length,Is.EqualTo(4));
            foreach(var lamp in lights){Assert.That(lamp.shadows,Is.EqualTo(LightShadows.None));Assert.That(lamp.intensity,Is.EqualTo(lights[0].intensity).Within(.001f).And.InRange(4f,8f),"Same mounted fixture intensity (4.4 before the visual pass, 6.0 now)");Assert.That(Physics.Raycast(lamp.transform.position,Vector3.up,out var hit,1,-1,QueryTriggerInteraction.Ignore),Is.True,"Mounted fixture must have physical ceiling");Assert.That(lamp.transform.IsChildOf(hit.transform),Is.True,"Fixture attached to its real ceiling");}
            log.AppendLine("Room"+(index+1)+" loaded="+flow.LoadedStageCount+" pointLights="+lights.Length+" monoMB="+Profiler.GetMonoUsedSizeLong()/1048576f+" totalEditorMB="+Profiler.GetTotalAllocatedMemoryLong()/1048576f);
        }
        // enemies a round spawns, read from the room's own wave data (variations are cleared by FixedWaves)
        static int Planned(CombatTwoRoomsLab rounds,int wave)
        {
            var groups=new SerializedObject(rounds).FindProperty("waves").GetArrayElementAtIndex(wave).FindPropertyRelative("Groups");int total=0;
            for(int i=0;i<groups.arraySize;i++)total+=groups.GetArrayElementAtIndex(i).FindPropertyRelative("Count").intValue;return total;
        }
        [UnityTest]public IEnumerator TwelveRoundsAirlocksInventoryAndPursuit()
        {
            var log=new StringBuilder("Editor functional streaming test; capture1280x720; quality="+QualitySettings.names[QualitySettings.GetQualityLevel()]+" GPU="+SystemInfo.graphicsDeviceName+". AI frozen after spawning for deterministic flow; memory counters include Editor; no Player/FPS claim.\n");
            int[][] counts={new[]{6,4,5},new[]{6,5,5},new[]{6,5,7},new[]{6,5,6}};StolenCannon held=null;float energy=0;WeaponController equipped=null;
            int o=flow.PrologueStages;
            for(int stage=0;stage<4;stage++)
            {
                var rounds=flow.CurrentRoom.Rounds;CheckStage(log,stage);Assert.That(flow.TryTransition(stage+o),Is.False,"Cannot skip unfinished rounds");Assert.That(flow.Airlocks[stage+o].SourceDoor.activeSelf,Is.True);
                if(stage==0)Place(new Vector3(0,.03f,-20));else if(stage==1)
                {
                    Place(new Vector3(0,8.03f,-4));Input(Key.W,Key.Space);yield return null;yield return null;Input(Key.W);yield return Until(()=>player.transform.position.z>5,12,"Original upper floor gap jump");Input();
                }
                else Place(new Vector3(0,8.03f,(stage==2?39:103)+7));
                yield return null;Capture("room"+(stage+1));
                for(int wave=0;wave<3;wave++)
                {
                    int expected=Planned(rounds,wave);if(expected!=counts[stage][wave])log.AppendLine("Room"+(stage+1)+" round"+(wave+1)+" planned "+expected+" (was "+counts[stage][wave]+" before the v24 enemy additions)");int slots=new SerializedObject(rounds).FindProperty("firstRoomSpawns").arraySize;
                    if(expected>slots)
                    {
                        // horde round (more enemies than spawn slots): it arrives in batches; clear each batch, without
                        // freezing the AI, until the last enemies are out, then check the round end like the others
                        int w=wave;yield return Until(()=>rounds.WaveIndex==w&&rounds.LivingEnemies>0,45,"Room"+(stage+1)+" horde round"+(wave+1)+" starts");
                        float hordeEnd=Time.realtimeSinceStartup+90;
                        while(rounds.PendingSpawns>0&&Time.realtimeSinceStartup<hordeEnd){if(rounds.WaitingForSpawnSpace||rounds.LivingEnemies>=slots)foreach(var h in rounds.Living.ToArray())if(h&&h.CurrentHealth>0)h.Kill();yield return null;}
                        Assert.That(rounds.PendingSpawns,Is.Zero,"Room"+(stage+1)+" horde round"+(wave+1)+" fully spawned ("+expected+" planned, "+slots+" slots)");
                        if(rounds.LivingEnemies==0){yield return new WaitForSeconds(.5f);}
                        log.AppendLine("Room"+(stage+1)+" round"+(wave+1)+" horde "+expected+" through "+slots+" slots");
                        if(rounds.WaveIndex!=wave||rounds.LivingEnemies==0)continue;
                    }
                    else yield return Until(()=>rounds.WaveIndex==wave&&rounds.PendingSpawns==0&&rounds.LivingEnemies==expected,45,"Room"+(stage+1)+" round"+(wave+1)+" expected"+expected);
                    if(stage==0&&wave==0)
                    {
                        var victim=rounds.Living.First(h=>h.GetComponent<EnemyExecutionSource>());victim.TakeDamage(victim.MaxHealth*.3f,player.gameObject);var box=victim.GetComponent<Collider>();player.enabled=false;Place(new Vector3(box.bounds.center.x,.03f,box.bounds.min.z-1));player.PlayerCamera.transform.LookAt(box.bounds.center);yield return null;Input(Key.E);yield return null;yield return null;Input();yield return new WaitForSeconds(3.5f);var carrier=player.GetComponent<StolenWeaponCarrier>();held=carrier.Held;Assert.That(held,Is.Not.Null);held.Ammo.Drain(5);energy=held.Ammo.Remaining;equipped=held.EquippedWeapon;player.enabled=true;Place(new Vector3(0,.03f,-20));
                    }
                    var living=rounds.Living.ToArray();for(int i=0;i<living.Length-1;i++)living[i].Kill();yield return null;Assert.That(rounds.WaveIndex,Is.EqualTo(wave),"Last living enemy keeps round open");living.Last().Kill();yield return null;log.AppendLine("Room"+(stage+1)+" round"+(wave+1)+" count="+expected);
                }
                yield return Until(()=>rounds.Completed,8,"All three rounds complete");int a=stage+o;yield return Until(()=>!flow.Airlocks[a].SourceDoor.activeSelf,8,"Exit door opens (animated)");
                var oldRoom=flow.CurrentRoom.gameObject;string oldName=oldRoom.scene.name;Vector3 transit=stage==0?new Vector3(31,.03f,25):new Vector3(0,8.03f,new[]{0f,27f,91f,155f}[stage]+6);Place(transit,stage==0?180:0);
                yield return Until(()=>flow.Busy,8,"Enter physical airlock");Assert.That(flow.Airlocks[stage+o].SourceDoor.activeSelf,Is.True);Assert.That(flow.Airlocks[stage+o].DestinationDoor.activeSelf,Is.True);float began=Time.realtimeSinceStartup;
                yield return Until(()=>!flow.Busy&&flow.CurrentStage==stage+o+1,90,"Next room prewarmed and door ready");Assert.That(Time.realtimeSinceStartup-began,Is.GreaterThan(1.3f));Assert.That(!oldRoom,Is.True,"Old room native objects unloaded");Assert.That(SceneManager.GetSceneByName(oldName).isLoaded,Is.False);Assert.That(flow.Airlocks[stage+o].SourceDoor.activeSelf,Is.True);yield return Until(()=>!flow.Airlocks[a].DestinationDoor.activeSelf,8,"Destination door opens (animated)");
                var carrierNow=player.GetComponent<StolenWeaponCarrier>();Assert.That(carrierNow.Held,Is.SameAs(held));Assert.That(held.gameObject.scene,Is.EqualTo(flow.gameObject.scene));Assert.That(held.Ammo.Remaining,Is.EqualTo(energy).Within(.001));Assert.That(player.GetComponent<PlayerWeaponsManager>().GetActiveWeapon(),Is.SameAs(equipped));
                if(stage==0){Input(Key.W);yield return Until(()=>player.transform.position.y>7.9f&&player.transform.position.z<-9,18,"Walk original40 stairs while lower room unloaded");Input();Place(new Vector3(31,8.03f,-11),270);Input(Key.W);yield return Until(()=>player.transform.position.x<25,8,"Upper door clear");Input();}
            }
            Assert.That(flow.CurrentRoom.Pursuit,Is.Not.Null);Assert.That(flow.LoadedStageCount,Is.EqualTo(1));var pursuit=flow.CurrentRoom.Pursuit;Place(new Vector3(0,8.03f,193));Input(Key.W);yield return Until(()=>pursuit.Running,8,"Connected crusher starts");Input();Assert.That(pursuit.EntranceDoor.activeSelf,Is.True);Capture("pursuit");
            Input(Key.W);foreach(float gap in new[]{233f,283f,333f}){yield return Until(()=>player.transform.position.z>=gap-2,15,"Approach shifted pursuit gap");Input(Key.W,Key.Space);yield return null;yield return null;Input(Key.W);yield return Until(()=>player.transform.position.z>gap+9,10,"Jump shifted gap");}yield return Until(()=>pursuit.Completed,12,"Reach pursuit exit");Input();Assert.That(pursuit.SpawnedCount,Is.EqualTo(5));Assert.That(pursuit.Motor.isPlaying,Is.False);
            var carrierFinal=player.GetComponent<StolenWeaponCarrier>();Assert.That(carrierFinal.ThrowHeld(),Is.True);Assert.That(held.gameObject.scene,Is.EqualTo(flow.CurrentRoom.gameObject.scene),"Thrown inventory belongs to current room");held.Explosion.TryDetonate();yield return null;Assert.That(carrierFinal.Held,Is.Null);
            File.WriteAllText(Report+"flow.txt",log+"Twelve rounds; one loaded gameplay scene at every sampled frame; previous scene/native objects destroyed; active player/weapon/energy survive all4 transitions; real E;40 stair traversal; four mounted lights per room; final3 jumps,5 original ambush enemies and crusher completion; thrown module rehomed to current room.\n");
        }
        static void PresentationShot(string name)
        {
            SessionState.SetString("IR.HandsHud.CaptureTag",name);var began=DateTime.UtcNow;
            Assert.That(EditorApplication.ExecuteMenuItem("Tools/Iron Requiem/Hands HUD Campaign/Capture gameplay state"),Is.True);
            string path="Tools/IronRequiem/Reports/HandsHudCampaign/"+name+".png";
            Assert.That(File.Exists(path)&&File.GetLastWriteTimeUtc(path)>=began.AddSeconds(-1),Is.True,"Fresh 1080p evidence: "+name);
        }

        [UnityTest]public IEnumerator PresentationHandsMountedArmsAndOverdriveHud()
        {
            var weapons=player.GetComponent<PlayerWeaponsManager>();var melee=player.GetComponent<ProtagonistMelee>();
            var station=Object.FindAnyObjectByType<WeaponMountStation>();var native=weapons.GetActiveWeapon();
            Assert.That(native,Is.Not.Null);weapons.RemoveWeapon(native);yield return new WaitForSeconds(.4f);
            Assert.That(melee.Unarmed,Is.True);PresentationShot("unarmed-rest");
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left,true));yield return new WaitForSeconds(.05f);
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.18f);PresentationShot("unarmed-quick-impact");
            yield return Until(()=>!melee.IsAttacking,2,"Quick punch recovery");
            Input(Key.F);yield return Until(()=>melee.IsCharging,1,"Unarmed F charge");yield return new WaitForSeconds(melee.MaxChargeSeconds+.15f);
            Assert.That(melee.FullyCharged&&melee.LeftStrike,Is.True);PresentationShot("unarmed-full-charge");
            Input();yield return new WaitForSeconds(.11f);PresentationShot("unarmed-heavy-impact");yield return Until(()=>!melee.IsAttacking,3,"Heavy punch recovery");
            weapons.AddWeapon(station.WeaponPrefab);var equipped=weapons.HasWeapon(station.WeaponPrefab);
            int slot=Enumerable.Range(0,9).Single(i=>weapons.GetWeaponAtSlotIndex(i)==equipped);weapons.SwitchToWeaponIndex(slot,true);yield return new WaitForSeconds(.5f);
            Assert.That(player.GetComponent<FirstPersonUpperArms>(),Is.Not.Null);PresentationShot("mounted-rest");
            Input(Key.F);yield return Until(()=>melee.IsCharging,1,"Mounted F charge");yield return new WaitForSeconds(melee.MaxChargeSeconds+.15f);
            Assert.That(melee.ArmedStrike,Is.True);Assert.That(Object.FindAnyObjectByType<ProtagonistHandsPresentation>().LeftHandVisible,Is.False);PresentationShot("mounted-full-charge");
            Input();yield return new WaitForSeconds(.11f);PresentationShot("mounted-impact");yield return Until(()=>!melee.IsAttacking,3,"Mounted bash recovery");
            player.SetViewAngles(0,-40);InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left,true));yield return new WaitForSeconds(.12f);
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.08f);PresentationShot("mounted-fire");player.SetViewAngles(0,0);
            var rounds=flow.CurrentRoom.Rounds;Place(new Vector3(0,.03f,-20));yield return Until(()=>rounds.PendingSpawns==0&&rounds.LivingEnemies>0,45,"Enemies available for real extraction");
            var victim=rounds.Living.First(h=>h&&h.GetComponent<EnemyExecutionSource>());victim.TakeDamage(victim.MaxHealth*.3f,player.gameObject);
            var box=victim.GetComponent<Collider>();player.enabled=false;Place(new Vector3(box.bounds.center.x,.03f,box.bounds.min.z-1));player.PlayerCamera.transform.LookAt(box.bounds.center);yield return null;
            Input(Key.E);yield return null;yield return null;Input();yield return new WaitForSeconds(3.5f);
            var held=player.GetComponent<StolenWeaponCarrier>().Held;Assert.That(held,Is.Not.Null);player.enabled=true;Place(new Vector3(0,.03f,-20));player.SetViewAngles(0,0);
            Input(Key.F);yield return Until(()=>melee.IsCharging,1,"Stolen forearm charge");yield return new WaitForSeconds(melee.MaxChargeSeconds+.15f);PresentationShot("stolen-full-charge");
            Input();yield return new WaitForSeconds(.11f);PresentationShot("stolen-impact");yield return Until(()=>!melee.IsAttacking,3,"Stolen bash recovery");
            held.Ammo.Drain(Mathf.Max(0,held.Ammo.Remaining-held.Ammo.Capacity*.15f));Assert.That(held.Overdrive.IsArmed,Is.True);
            float previousScale=Time.timeScale;Time.timeScale=0;yield return null;
            try{Assert.That(EditorApplication.ExecuteMenuItem("Tools/Iron Requiem/Hands HUD Campaign/Capture HUD resolutions"),Is.True);}
            finally{Time.timeScale=previousScale;}
        }

        [UnityTest]public IEnumerator IntroMenuStartsCampaignAndRestartReturnsToRoomOne()
        {
            SceneManager.LoadScene("IntroMenu",LoadSceneMode.Single);yield return null;yield return null;
            // v30: the animated Hangar C-04 menu must be what IntroMenu shows (3D set + JUGAR launch sequence registered)
            var hangarSet=GameObject.Find("HangarSet");Assert.That(hangarSet&&hangarSet.activeInHierarchy,Is.True,"IntroMenu shows the animated Hangar C-04 set");
            var launch=Object.FindAnyObjectByType<HangarLaunchSequence>();Assert.That(launch&&launch.isActiveAndEnabled,Is.True,"JUGAR plays the hangar launch sequence");
            Assert.That(SceneUtility.GetScenePathByBuildIndex(0),Is.EqualTo("Assets/FPS/Scenes/IntroMenu.unity"),"IntroMenu is the first scene in Build Settings");
            var views=Object.FindObjectsByType<RequiemMenuView>();var view=views.Single(v=>v.gameObject.scene.name=="IntroMenu");var loader=(LoadSceneButton)new SerializedObject(view).FindProperty("sceneLoader").objectReferenceValue;Assert.That(loader.SceneName,Is.EqualTo("RequiemCampaign"));
            {   // v5: OPCIONES opens a large centred console (about 76 % of the screen height); VOLVER and Escape return home
                var vso=new SerializedObject(view);var opts=(GameObject)vso.FindProperty("options").objectReferenceValue;var backButton=(UnityEngine.UI.Button)vso.FindProperty("optionsBack").objectReferenceValue;
                view.OpenOptions();yield return null;yield return null;Canvas.ForceUpdateCanvases();
                var console=opts.transform.Find("OptionsConsole") as RectTransform;Assert.That(console,Is.Not.Null,"Options console wrapped in the full-screen overlay");
                var canvasRect=(RectTransform)console.GetComponentInParent<Canvas>().rootCanvas.transform;float frac=console.rect.height*console.localScale.y/canvasRect.rect.height;
                // 76 % of the height on 16:9 (1280x720 / 1920x1080 / 2560x1440); on a narrower view the 92 % width cap wins
                float expected=Mathf.Min(.76f,.92f*canvasRect.rect.width/console.rect.width*console.rect.height/canvasRect.rect.height);
                Assert.That(frac,Is.EqualTo(expected).Within(.03f),"Options console fit ("+frac+" of the height, view "+canvasRect.rect.size+")");
                Assert.That(Mathf.Abs(console.position.x-canvasRect.position.x),Is.LessThan(canvasRect.rect.width*canvasRect.lossyScale.x*.02f),"Options console centred");
                backButton.onClick.Invoke();yield return null;Assert.That(opts.activeSelf,Is.False,"VOLVER closes options");
            }
            view.Primary();yield return null;yield return null;Bind();yield return Until(()=>{if(!flow)Bind();return flow&&!flow.Busy&&flow.CurrentStage==0;},60,"Main menu launch");Assert.That(flow.Menu.IsPlaying,Is.True);Assert.That(flow.Menu.IsMainMenu,Is.False);Assert.That(flow.LoadedStageCount,Is.EqualTo(1));
            flow.Menu.RestartFromFirstRoom();yield return null;yield return null;Bind();Assert.That(flow.Menu.IsPlaying,Is.True);
            yield return null;yield return null;yield return null;
            AssertUnarmed("After restart of a run that never reached the station");
            flow.Menu.ReturnToMainMenu();yield return null;yield return null;yield return new WaitForSecondsRealtime(2);Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("IntroMenu"));Assert.That(Object.FindAnyObjectByType<CampaignRoom>(),Is.Null,"No orphan room after return while initial load pending");
            File.WriteAllText(Report+"menu.txt","Actual IntroMenu Primary uses RequiemCampaign, skips duplicate start menu, room1 loaded once. Restart reloads campaign in play; returning immediately to IntroMenu during initial async load leaves no orphan room/player.");
        }
        [UnityTest]public IEnumerator DeathScreenButtonsReceiveClicks()
        {
            // v6: REINICIAR and VOLVER AL MENÚ must be the first thing under the pointer (the v5 restyle left them unclickable)
            player.GetComponent<Health>().Kill();yield return null;yield return null;yield return null;
            var lab=Object.FindAnyObjectByType<CombatLabMenu>();Assert.That(lab.IsDeathMenu,Is.True,"Death menu shown");
            var death=(GameObject)new SerializedObject(lab).FindProperty("deathPanel").objectReferenceValue;var buttons=death.GetComponentsInChildren<UnityEngine.UI.Button>();
            Assert.That(buttons.Length,Is.EqualTo(2),"Two death buttons");
            foreach(var b in buttons)
            {
                Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,((RectTransform)b.transform).position);
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=screen},hits);
                Assert.That(hits.Count,Is.GreaterThan(0),b.name+" has something under the pointer");
                Assert.That(hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>(),Is.SameAs(b),b.name+" receives the click (first hit: "+hits[0].gameObject.name+")");
            }
        }
        [UnityTest]public IEnumerator EquippedExplosionCleansInventoryInAdditiveRoom()
        {
            Place(new Vector3(0,.03f,-20));var rounds=flow.CurrentRoom.Rounds;yield return Until(()=>rounds.PendingSpawns==0&&rounds.LivingEnemies==6,45,"First wave for inventory scene ownership");var victim=rounds.Living.First(h=>h.GetComponent<EnemyExecutionSource>());victim.TakeDamage(victim.MaxHealth*.3f,player.gameObject);var box=victim.GetComponent<Collider>();player.enabled=false;Place(new Vector3(box.bounds.center.x,.03f,box.bounds.min.z-1));player.PlayerCamera.transform.LookAt(box.bounds.center);yield return null;Input(Key.E);yield return null;yield return null;Input();yield return new WaitForSeconds(3.5f);var carrier=player.GetComponent<StolenWeaponCarrier>();var held=carrier.Held;Assert.That(held,Is.Not.Null);var weapon=held.WeaponPrefab;held.Ammo.Drain(held.Ammo.Remaining);Assert.That(held.Overdrive.IsArmed,Is.True);Assert.That(held.Explosion.HasDetonated,Is.True,"Zero energy already detonated synchronously");Assert.That(held.Explosion.TryDetonate(),Is.False,"Do not detonate twice");yield return null;yield return null;Assert.That(!carrier.Held,Is.True);Assert.That(!player.GetComponent<PlayerWeaponsManager>().HasWeapon(weapon),Is.True,"Destroying a room-owned module must remove the persistent player's inventory instance");File.WriteAllText(Report+"inventory.txt","Real E equips a module owned by additive room; drain and native critical detonation remove both module and persistent inventory entry. No active-scene teardown guard suppresses cleanup.");
        }
        [UnityTest]public IEnumerator CurrentRobotsEyesAndExecutionOnlyWeapons()
        {
            foreach(string name in new[]{"T07_Current_Repaired","BigBot_Current","HoverBot_Current","Turret_Current","SawBot_Current"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Campaign/"+name+".prefab");Assert.That(prefab,Is.Not.Null);var robot=Object.Instantiate(prefab,new Vector3(0,0,12),Quaternion.identity);yield return null;yield return null;Freeze();var hp=robot.GetComponent<Health>();var eyes=robot.GetComponent<EnemyPowerIndicators>();Assert.That(eyes&&eyes.Indicators.Length>0,Is.True,name+" real eye/status geometry");Assert.That(robot.GetComponentsInChildren<Light>(true).Length,Is.Zero,"No dynamic light per enemy");var source=robot.GetComponent<EnemyExecutionSource>();
                // executions-v02 (22/11/2026, user approval): the Big Bot is executable again, with the same strict 75 % rule
                if(name=="BigBot_Current")Assert.That(source&&source.ExecutionEnabled,Is.True,"executions-v02: Big Bot executable");
                if(source){hp.TakeDamage(hp.MaxHealth*.25f,player.gameObject);Assert.That(source.Eligible,Is.False,"75% exact not executable");hp.TakeDamage(.1f,player.gameObject);Assert.That(source.Eligible,Is.True,"Strictly less than75% executable");}
                hp.TakeDamage(hp.CurrentHealth-hp.MaxHealth*.15f,player.gameObject);yield return null;hp.Kill();yield return null;yield return null;
                Assert.That(eyes.IsPoweredOff,Is.True,"Eyes off after death");if(!eyes.UsesMaterialSlots)Assert.That(eyes.Indicators.All(r=>!r||!r.enabled),Is.True);Assert.That(Object.FindObjectsByType<StolenCannon>(FindObjectsInactive.Include).Any(c=>c.State==StolenCannonState.Available||c.State==StolenCannonState.Thrown),Is.False,"Normal death/critical loss never produces a collectible weapon");Object.Destroy(robot);yield return null;
            }
            File.WriteAllText(Report+"robots.txt","All5 current prefab families: eye/status renderers exist, shared emissive colors, no native Light per robot, eyes off on Health death. T07/SawBot/BigBot strict75% threshold (BigBot executable again, executions-v02); critical damage and normal death create no Available/Thrown modules. Drone/turret retain no invented module.");
        }
        [UnityTest]public IEnumerator V4FistChargeSlideGuardAndBigBotBlast()
        {
            var log=new StringBuilder("v4 combat checks\n");
            var weapons=player.GetComponent<PlayerWeaponsManager>();var melee=player.GetComponent<ProtagonistMelee>();var movement=player.GetComponent<AdvancedPlayerMovement>();
            Assert.That(Enumerable.Range(0,9).Any(i=>{var w=weapons.GetWeaponAtSlotIndex(i);return w&&w.PermanentMelee;}),Is.False,"v5: no fist slot");
            // Big Bot data: +50% shell damage, bigger radius, blast presentation, no execution
            var big=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab");var baseBig=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/VisualOverhaul/BigBot_Burst_v10_NavThrottle_VO.prefab");
            var combat=big.GetComponentInChildren<BigBotCombat>(true);var baseCombat=baseBig.GetComponentInChildren<BigBotCombat>(true);
            Assert.That(combat.Damage,Is.EqualTo(baseCombat.Damage*1.5f).Within(.01f),"Big Bot +50% damage");Assert.That(combat.ExplosionRadius,Is.GreaterThan(baseCombat.ExplosionRadius*1.4f),"Bigger blast radius");Assert.That(combat.ExplosiveShots,Is.True);
            var cannons=(WeaponController[])typeof(BigBotCombat).GetField("cannons",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(combat);
            Assert.That(cannons.Length,Is.GreaterThan(0));foreach(var c in cannons)Assert.That(c.ProjectilePrefab.GetComponent<BigBotBlastFx>(),Is.Not.Null,"Big Bot shells use the heavier blast");
            Assert.That(big.GetComponent<EnemyExecutionSource>().ExecutionEnabled,Is.True,"executions-v02: Big Bot executable again");
            log.AppendLine("BigBot damage "+baseCombat.Damage+" -> "+combat.Damage+", radius "+baseCombat.ExplosionRadius+" -> "+combat.ExplosionRadius);
            // slide guard: a real sprint + C slide opens a short window in which damage is ignored (health untouched)
            Place(new Vector3(0,.03f,-20));yield return null;int ignored=0;UnityEngine.Events.UnityAction<float,GameObject> count=(d,s)=>ignored++;health.OnDamageIgnored+=count;
            Input(Key.W,Key.LeftShift);yield return new WaitForSeconds(.9f);Input(Key.W,Key.LeftShift,Key.C);
            float end=Time.realtimeSinceStartup+2;while(!movement.IsSliding&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(movement.IsSliding,Is.True,"Sprint + C starts a slide");Assert.That(movement.SlideGuardActive,Is.True,"Slide opens the guard window");
            health.Invincible=false;float before=health.CurrentHealth;
            health.TakeDamage(15,player.gameObject);Assert.That(health.CurrentHealth,Is.EqualTo(before).Within(.001f),"Hit during the window is ignored");Assert.That(ignored,Is.EqualTo(1),"Ignored hit is reported for sparks/sound");
            Assert.That(movement.SlideInvulnerability,Is.InRange(.35f,.5f));yield return new WaitForSeconds(movement.SlideInvulnerability+.1f);Input();
            Assert.That(movement.SlideGuardActive,Is.False,"Window is shorter than the slide");health.TakeDamage(15,player.gameObject);Assert.That(health.CurrentHealth,Is.LessThan(before),"Damage counts again after the window");
            health.OnDamageIgnored-=count;health.Heal(100);health.Invincible=true;log.AppendLine("slide guard "+movement.SlideInvulnerability+" s, ignored="+ignored);
            yield return new WaitForSeconds(.4f);
            // With a mounted cannon, F charges a right-forearm bash and leaves the unused left hand hidden.
            var rounds=flow.CurrentRoom.Rounds;yield return Until(()=>rounds.LivingEnemies>0&&rounds.PendingSpawns==0,45,"Room01 enemies for the punch");
            var victim=rounds.Living.First(h=>h&&h.CurrentHealth>h.MaxHealth*.5f);var enemy=victim.GetComponent<EnemyController>();var box=victim.GetComponent<Collider>();
            Place(new Vector3(box.bounds.center.x,.03f,box.bounds.min.z-1.2f));player.PlayerCamera.transform.LookAt(box.bounds.center);Physics.SyncTransforms();
            var armed=weapons.GetActiveWeapon();Assert.That(armed,Is.Not.Null,"Cannon in hand for the bash");var heatBefore=armed.GetComponent<WeaponHeat>();float heat0=heatBefore?heatBefore.CurrentHeat:0;
            float hp=victim.CurrentHealth;Input(Key.F);
            yield return Until(()=>melee.IsCharging,1,"F starts the charge");Assert.That(melee.ArmedStrike,Is.True,"F uses the mounted forearm");Assert.That(melee.LeftStrike,Is.False);
            {var hands=Object.FindAnyObjectByType<ProtagonistHandsPresentation>();yield return new WaitForSeconds(.12f);Assert.That(hands.LeftHandVisible,Is.False,"Unused left hand remains hidden during the mounted bash");Assert.That(hands.GetComponent<FirstPersonUpperArms>(),Is.Not.Null,"Mounted bash retains the complete upper arm");}
            yield return new WaitForSeconds(melee.MaxChargeSeconds+.15f);Assert.That(melee.FullyCharged,Is.True,"Full charge after holding F");
            Place(new Vector3(box.bounds.center.x,.03f,box.bounds.min.z-1.2f));player.PlayerCamera.transform.LookAt(box.bounds.center);Physics.SyncTransforms();
            Input();yield return Until(()=>!melee.IsAttacking,2,"Heavy bash resolves");
            Assert.That(melee.LastWasHeavy&&melee.LastWasFull,Is.True,"Release after a full charge is heavy");Assert.That(melee.LastHitCount,Is.GreaterThan(0),"Heavy bash connects");
            Assert.That(victim.CurrentHealth,Is.LessThan(hp-melee.HeavyDamage*.5f),"Heavy damage");if(enemy&&victim.CurrentHealth>0)Assert.That(enemy.StaggeredUntil,Is.GreaterThan(0),"Enemy staggered");
            Assert.That(Time.timeScale,Is.EqualTo(1).Within(.001f),"Hit-stop and slow-motion restore time");
            Assert.That(weapons.GetActiveWeapon(),Is.SameAs(armed),"Bash never unequips");if(heatBefore)Assert.That(heatBefore.CurrentHeat,Is.LessThanOrEqualTo(heat0+.001f),"Bash adds no heat");
            log.AppendLine("heavy bash: hp "+hp+" -> "+victim.CurrentHealth);
            // quick tap of F stays a quick strike
            Input(Key.F);yield return null;yield return null;Input();
            yield return Until(()=>!melee.IsAttacking,2,"Quick strike resolves");Assert.That(melee.LastWasHeavy,Is.False,"Tap = quick strike");
            // v5: restarting after the montage station keeps the cannon and the station does not run again
            flow.Menu.RestartFromFirstRoom();yield return null;yield return null;Bind();yield return Until(()=>{if(!flow)Bind();return flow&&!flow.Busy&&flow.CurrentStage==0;},60,"Restart reloads the campaign");
            {
                var st=Object.FindAnyObjectByType<WeaponMountStation>();yield return Until(()=>{st=st?st:Object.FindAnyObjectByType<WeaponMountStation>();return st&&st.Granted;},5,"Station already done after restart");
                var w=player.GetComponent<PlayerWeaponsManager>();Assert.That(w.HasWeapon(st.WeaponPrefab),Is.Not.Null,"Restart after the station keeps the cannon");
                Assert.That(Enumerable.Range(0,9).Count(i=>w.GetWeaponAtSlotIndex(i)),Is.EqualTo(1),"Only the cannon");Assert.That(st.Busy,Is.False,"No sequence after restart");
            }
            File.WriteAllText(Report+"v4.txt",log.ToString());
        }
    }
}
