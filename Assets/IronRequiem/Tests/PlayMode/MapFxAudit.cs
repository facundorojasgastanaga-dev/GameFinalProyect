using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace IronRequiem.Tests
{
    // MAP_FX_AUDIT (17/11/2026), phase 1, read only: walks the campaign in Play Mode, captures every room and the
    // effects, dumps lights/probes/volumes/NavMesh checks and samples Editor profiler counters in loaded fights.
    // Nothing is saved: rooms stream additively in the Test Runner scene and the temporary audit Volume is a
    // runtime-only profile. Results go to Tools/IronRequiem/Reports/MapFxAudit/.
    public sealed class MapFxAudit
    {
        const string Main="Assets/IronRequiem/Scenes/RequiemCampaign.unity",Report="Tools/IronRequiem/Reports/MapFxAudit/";
        Keyboard keys;Mouse mouse;bool background;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
        CampaignFlow flow;PlayerCharacterController player;Health health;StringBuilder log;

        [UnitySetUp]public IEnumerator Setup()
        {
            Directory.CreateDirectory(Report);
            background=Application.runInBackground;Application.runInBackground=true;Time.timeScale=1;AudioListener.pause=false;devices=InputSystem.actions.devices;keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keys.MakeCurrent();mouse.MakeCurrent();InputSystem.actions.devices=new InputDevice[]{keys,mouse};
            EditorSceneManager.LoadSceneInPlayMode(Main,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
            flow=Object.FindAnyObjectByType<CampaignFlow>();player=flow.Player;health=player.GetComponent<Health>();
            yield return Until(()=>!flow.Busy&&flow.CurrentStage==0,60,"Initial streamed room");flow.Menu.BeginGame();health.Invincible=true;yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            Input();Mouse(false);Time.timeScale=1;AudioListener.pause=false;var clean=SceneManager.CreateScene("AuditCleanup"+Time.frameCount);SceneManager.SetActiveScene(clean);
            var session=Object.FindAnyObjectByType<CampaignFlow>();if(session)yield return session.DrainPendingLoadsForShutdown();
            foreach(var queue in Object.FindObjectsByType<EnemySpawnQueue>()){queue.CancelPending();queue.enabled=false;}foreach(var waves in Object.FindObjectsByType<CombatTwoRoomsLab>())waves.enabled=false;Freeze();
            var loaded=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s!=clean&&s.isLoaded).OrderBy(s=>s.name=="RequiemCampaign"?1:0).ToArray();foreach(var scene in loaded)if(scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);yield return null;
            InputSystem.actions.devices=devices;InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);Cursor.lockState=CursorLockMode.None;Application.runInBackground=background;
        }

        // ---------- A: prologue (hangar, alarm corridor, lobby) + rooms 01-04 + pursuit, frozen AI, captures and map checks
        [UnityTest]public IEnumerator A_RouteCapturesAndMapChecks()
        {
            log=new StringBuilder("MAP audit, Editor Play Mode (Test Runner). screen="+Screen.width+"x"+Screen.height+" quality="+QualitySettings.names[QualitySettings.GetQualityLevel()]+" GPU="+SystemInfo.graphicsDeviceName+"\n");
            DumpRender();
            // Hangar (prologue)
            var intro=Object.FindAnyObjectByType<HangarIntroCinematic>();
            if(flow.CurrentRoom&&flow.CurrentRoom.IsPrologue&&intro)
            {
                yield return Until(()=>intro.IsPlaying||intro.Finished,10,"Intro starts");yield return new WaitForSeconds(1.5f);Shot("r00-hangar-intro-cinematic");
                intro.RequestSkip();yield return Until(()=>flow.CurrentRoom.Completed&&!PlayerInputLock.Locked,5,"Skip");yield return new WaitForSeconds(.5f);
                RoomPass("r00-hangar");
                yield return Until(()=>!flow.Airlocks[0].SourceDoor.activeSelf,10,"Hangar door opens");Route("r00-hangar",flow.Airlocks[0]);
                Place(new Vector3(0,.03f,-80f));yield return Until(()=>flow.Busy,8,"Enter connection chamber");yield return new WaitForSeconds(.6f);Shot("r00-airlock-cycle");
                yield return Until(()=>!flow.Busy&&flow.CurrentStage==1,90,"Alarm corridor");
                // alarm corridor
                Place(new Vector3(0,.03f,-62f));yield return new WaitForSeconds(1.2f);RoomPass("r00b-corridor");
                var rounds=flow.CurrentRoom.Rounds;
                for(int wave=0;wave<2;wave++)
                {
                    int w=wave;yield return Until(()=>rounds.Completed||(rounds.WaveIndex==w&&rounds.PendingSpawns==0&&rounds.LivingEnemies>0),45,"Corridor wave "+(w+1));
                    if(wave==0){Freeze();Shot("r00b-corridor-wave-alarm");}
                    foreach(var h in rounds.Living.ToArray())if(h&&h.CurrentHealth>0)h.Kill();yield return new WaitForSeconds(.4f);if(wave==0)Shot("r00b-corridor-deaths");
                    float end=Time.realtimeSinceStartup+15;while(!(rounds.Completed||rounds.WaveIndex>w)&&Time.realtimeSinceStartup<end)yield return null;
                }
                yield return Until(()=>rounds.Completed,8,"Corridor cleared");yield return Until(()=>!flow.Airlocks[1].SourceDoor.activeSelf,8,"Lobby door");Route("r00b-corridor",flow.Airlocks[1]);
                var station=Object.FindAnyObjectByType<WeaponMountStation>();
                Place(station.transform.position-station.transform.forward*.5f+Vector3.up*.03f);yield return new WaitForSeconds(.3f);Shot("r00c-lobby-station");
                Input(Key.E);yield return null;yield return null;Input();yield return Until(()=>station.Busy,2,"Station");yield return new WaitForSeconds(1.5f);Shot("r00c-lobby-station-sequence");
                Input(Key.Space);yield return Until(()=>!station.Busy,3,"Skip station");Input();
                Place(new Vector3(0,.03f,-30.5f));yield return Until(()=>!flow.Busy&&flow.CurrentStage==flow.PrologueStages,90,"Room01");
            }
            int o=flow.PrologueStages;string[] names={"r01-foundry","r02-breach","r03-assembly","r04-reactor"};
            for(int stage=0;stage<4;stage++)
            {
                var rounds=flow.CurrentRoom.Rounds;
                if(stage==0)Place(new Vector3(0,.03f,-20));else if(stage==1)Place(new Vector3(0,8.03f,-4));else Place(new Vector3(0,8.03f,(stage==2?39:103)+7));
                yield return new WaitForSeconds(.5f);RoomPass(names[stage]);
                if(stage==1){Shot("r02-breach-gap-before-jump");Input(Key.W,Key.Space);yield return null;yield return null;Input(Key.W);yield return Until(()=>player.transform.position.z>5,12,"Upper floor gap jump");Input();log.AppendLine("  gap jump ok, landed "+player.transform.position.ToString("F1"));}
                for(int wave=0;wave<3;wave++)
                {
                    int w=wave;yield return Until(()=>rounds.WaveIndex==w&&rounds.LivingEnemies>0,90,names[stage]+" round"+(w+1));
                    float hordeEnd=Time.realtimeSinceStartup+90;
                    if(wave==0){yield return Until(()=>rounds.PendingSpawns==0||rounds.WaitingForSpawnSpace,45,"spawned");Reach(names[stage]+" round1",rounds);Shot(names[stage]+"-round1");}
                    while(rounds.WaveIndex==w&&!rounds.Completed&&Time.realtimeSinceStartup<hordeEnd){foreach(var h in rounds.Living.ToArray())if(h&&h.CurrentHealth>0)h.Kill();yield return null;}
                }
                yield return Until(()=>rounds.Completed,10,"Rounds complete");int a=stage+o;yield return Until(()=>!flow.Airlocks[a].SourceDoor.activeSelf,8,"Exit door");
                yield return new WaitForSeconds(.4f);Route(names[stage],flow.Airlocks[a]);
                Vector3 transit=stage==0?new Vector3(31,.03f,25):new Vector3(0,8.03f,new[]{0f,27f,91f,155f}[stage]+6);Place(transit,stage==0?180:0);
                yield return Until(()=>flow.Busy,8,"Enter airlock");yield return new WaitForSeconds(.5f);Shot(names[stage]+"-airlock-inside");
                yield return Until(()=>!flow.Busy&&flow.CurrentStage==stage+o+1,90,"Next room");yield return Until(()=>!flow.Airlocks[a].DestinationDoor.activeSelf,8,"Destination door");yield return new WaitForSeconds(.4f);Shot(names[stage]+"-airlock-exit");
                if(stage==0){Input(Key.W);yield return Until(()=>player.transform.position.y>7.9f&&player.transform.position.z<-9,18,"Stairs");Input();Place(new Vector3(31,8.03f,-11),270);Input(Key.W);yield return Until(()=>player.transform.position.x<25,8,"Upper door");Input();}
            }
            var pursuit=flow.CurrentRoom.Pursuit;
            Place(new Vector3(0,8.03f,186));yield return new WaitForSeconds(.5f);RoomPass("r05-pursuit");
            Place(new Vector3(0,8.03f,193));Input(Key.W);yield return Until(()=>pursuit.Running,8,"Crusher starts");Input();yield return new WaitForSeconds(.3f);Shot("r05-pursuit-start");
            Input(Key.W);int g=0;foreach(float gap in new[]{233f,283f,333f}){yield return Until(()=>player.transform.position.z>=gap-6,15,"Approach gap");Shot("r05-pursuit-gap"+(++g));yield return Until(()=>player.transform.position.z>=gap-2,15,"Gap edge");Input(Key.W,Key.Space);yield return null;yield return null;Input(Key.W);yield return Until(()=>player.transform.position.z>gap+9,10,"Jump gap");}
            yield return Until(()=>pursuit.Completed,12,"Pursuit exit");Input();Shot("r05-pursuit-exit");
            File.WriteAllText(Report+"map.txt",log.ToString());
        }

        // ---------- B: Room01 loaded fight with live AI, cannon fire, execution, overdrive, slide guard; profiler counters
        [UnityTest]public IEnumerator B_FightEffectsAndProfiler()
        {
            log=new StringBuilder("FX/PERF audit, Editor Play Mode (Test Runner). Editor numbers include Editor overhead; trend only, not Player FPS. screen="+Screen.width+"x"+Screen.height+" vSync="+QualitySettings.vSyncCount+" targetFrameRate="+Application.targetFrameRate+"\n");
            yield return SkipPrologue();
            var rounds=flow.CurrentRoom.Rounds;var weapons=player.GetComponent<PlayerWeaponsManager>();
            Place(new Vector3(0,.03f,-20));
            yield return Until(()=>rounds.WaveIndex==0&&rounds.PendingSpawns==0&&rounds.LivingEnemies>0,45,"Room01 wave1");
            // idle baseline with live AI before any shot
            yield return Sample("room01-wave1-idle-live-ai",3,null);
            // execution -> extraction arc, stolen module equipped
            var victim=rounds.Living.First(h=>h&&h.GetComponent<EnemyExecutionSource>());victim.TakeDamage(victim.MaxHealth*.3f,player.gameObject);
            var box=victim.GetComponent<Collider>();player.enabled=false;Place(new Vector3(box.bounds.center.x,.03f,box.bounds.min.z-1));player.PlayerCamera.transform.LookAt(box.bounds.center);yield return null;
            Input(Key.E);yield return null;yield return null;Input();
            for(int i=0;i<6;i++){yield return new WaitForSeconds(.45f);Shot("fx-execution-extraction-"+i,false);}
            yield return new WaitForSeconds(.8f);player.enabled=true;Place(new Vector3(0,.03f,-20));
            var held=player.GetComponent<StolenWeaponCarrier>().Held;log.AppendLine("execution: held="+(held?held.name:"none"));
            // loaded fight: live AI, keep firing at the nearest enemy, spawn the next waves without killing by script
            yield return Sample("room01-fight-live",14,"fight");
            // charge cannon (switch to the montage cannon if the stolen module is active)
            var cannon=Enumerable.Range(0,9).Select(weapons.GetWeaponAtSlotIndex).FirstOrDefault(w=>w&&w.GetComponentInChildren<CannonChargePresentation>(true));
            if(cannon){int slot=Enumerable.Range(0,9).First(i=>weapons.GetWeaponAtSlotIndex(i)==cannon);weapons.SwitchToWeaponIndex(slot,true);yield return new WaitForSeconds(.6f);
                Aim();Mouse(true);for(int i=0;i<5;i++){yield return new WaitForSeconds(.25f);Aim();Shot("fx-cannon-charge-"+i,false);}Mouse(false);for(int i=0;i<5;i++){yield return new WaitForSeconds(.06f);Shot("fx-cannon-release-"+i,false);}}
            // stolen module overdrive + overheat vent
            held=player.GetComponent<StolenWeaponCarrier>().Held;
            if(held)
            {
                var hs=Enumerable.Range(0,9).Select(weapons.GetWeaponAtSlotIndex).FirstOrDefault(w=>w&&w==held.EquippedWeapon);if(hs){int slot=Enumerable.Range(0,9).First(i=>weapons.GetWeaponAtSlotIndex(i)==hs);weapons.SwitchToWeaponIndex(slot,true);yield return new WaitForSeconds(.6f);}
                Aim();Mouse(true);for(int i=0;i<6;i++){yield return new WaitForSeconds(.12f);Aim();Shot("fx-stolen-fire-"+i,false);}Mouse(false);
                held.Ammo.Drain(Mathf.Max(0,held.Ammo.Remaining-held.Ammo.Capacity*.15f));yield return new WaitForSeconds(.3f);log.AppendLine("overdrive armed="+held.Overdrive.IsArmed);
                Aim();Mouse(true);for(int i=0;i<6;i++){yield return new WaitForSeconds(.15f);Aim();Shot("fx-overdrive-"+i,false);}Mouse(false);
                yield return Sample("room01-overdrive-fight",6,"fight");
            }
            // slide guard screen fx
            Place(new Vector3(0,.03f,-20));var movement=player.GetComponent<AdvancedPlayerMovement>();
            Input(Key.W,Key.LeftShift);yield return new WaitForSeconds(.9f);Input(Key.W,Key.LeftShift,Key.C);float end=Time.realtimeSinceStartup+2;while(!movement.IsSliding&&Time.realtimeSinceStartup<end)yield return null;
            health.Invincible=false;health.TakeDamage(10,player.gameObject);health.Invincible=true;for(int i=0;i<4;i++){yield return new WaitForSeconds(.08f);Shot("fx-slide-guard-"+i,false);}Input();
            // damage overlay (C2 in VISUAL_AUDIT_F0)
            yield return new WaitForSeconds(.6f);health.Invincible=false;health.TakeDamage(25,player.gameObject);health.Invincible=true;for(int i=0;i<3;i++){yield return new WaitForSeconds(.1f);Shot("fx-damage-overlay-"+i,false);}health.Heal(100);
            // deaths in sequence: kill living enemies in front of the camera
            Freeze();var targets=rounds.Living.Where(h=>h&&h.CurrentHealth>0).ToArray();
            if(targets.Length>0){var t=targets[0];var c=t.GetComponent<Collider>();player.enabled=false;var p=c.bounds.center;Place(new Vector3(p.x,.03f,p.z-6));player.PlayerCamera.transform.LookAt(p);yield return null;
                t.TakeDamage(t.MaxHealth*10,player.gameObject);for(int i=0;i<6;i++){yield return new WaitForSeconds(.1f);Shot("fx-death-"+i,false);}player.enabled=true;}
            // blur A/B: same turn speed, current post vs motion blur/DoF/chromatic/lens off (runtime-only profile)
            yield return BlurAB();
            File.WriteAllText(Report+"fx-perf.txt",log.ToString());
        }

        // ---------- C: horde fight in Room03 (most enemies) for the heaviest sample
        [UnityTest]public IEnumerator C_HordeProfilerRoom03()
        {
            log=new StringBuilder("Horde sample (Room03), Editor Play Mode. Editor numbers, trend only.\n");
            yield return SkipPrologue();int o=flow.PrologueStages;
            for(int stage=0;stage<2;stage++)
            {
                var rounds=flow.CurrentRoom.Rounds;Place(stage==0?new Vector3(0,.03f,-20):new Vector3(0,8.03f,-4));
                if(stage==1){Input(Key.W,Key.Space);yield return null;yield return null;Input(Key.W);yield return Until(()=>player.transform.position.z>5,12,"Upper floor gap jump");Input();}
                for(int wave=0;wave<3;wave++){int w=wave;yield return Until(()=>rounds.WaveIndex==w&&rounds.LivingEnemies>0,45,"round");float e=Time.realtimeSinceStartup+90;while(rounds.WaveIndex==w&&!rounds.Completed&&Time.realtimeSinceStartup<e){foreach(var h in rounds.Living.ToArray())if(h&&h.CurrentHealth>0)h.Kill();yield return null;}}
                yield return Until(()=>rounds.Completed,10,"done");int a=stage+o;yield return Until(()=>!flow.Airlocks[a].SourceDoor.activeSelf,8,"door");
                Place(stage==0?new Vector3(31,.03f,25):new Vector3(0,8.03f,33),stage==0?180:0);yield return Until(()=>flow.Busy,8,"airlock");yield return Until(()=>!flow.Busy&&flow.CurrentStage==a+1,90,"next");yield return Until(()=>!flow.Airlocks[a].DestinationDoor.activeSelf,8,"dest");
            }
            var r3=flow.CurrentRoom.Rounds;Place(new Vector3(0,8.03f,46));
            for(int wave=0;wave<3;wave++)
            {
                int w=wave;yield return Until(()=>r3.WaveIndex==w&&r3.LivingEnemies>0,45,"Room03 round"+(w+1));
                yield return Sample("room03-round"+(wave+1)+"-live",wave==2?14:8,"fight");
                float e=Time.realtimeSinceStartup+90;while(r3.WaveIndex==w&&!r3.Completed&&Time.realtimeSinceStartup<e){foreach(var h in r3.Living.ToArray())if(h&&h.CurrentHealth>0)h.Kill();yield return null;}
            }
            File.WriteAllText(Report+"horde.txt",log.ToString());
        }

        // ---------- D: sector grade Volume weight over time after entering Room01 and Room02 (stuck at 0 or still fading?)
        [UnityTest]public IEnumerator D_SectorVolumeWeightOverTime()
        {
            log=new StringBuilder("Sector grade weight after room activation (VolumeFadeIn + CampaignFlow BlendIn)\n");
            yield return SkipPrologue();
            for(int stage=0;stage<2;stage++)
            {
                var vol=flow.CurrentRoom.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Volume>(true)).FirstOrDefault(v=>v.name.Contains("SectorGrade"));
                float t0=Time.realtimeSinceStartup;var line=new StringBuilder("  "+flow.CurrentRoom.gameObject.scene.name+": ");
                for(int i=0;i<=8;i++){line.Append("t="+(Time.realtimeSinceStartup-t0).ToString("F1")+"s w="+(vol?vol.weight.ToString("F2"):"none")+" ambient="+RenderSettings.ambientMode+"; ");yield return new WaitForSeconds(.75f);}
                log.AppendLine(line.ToString());DumpVolumeStack("  ");
                if(stage==1)break;
                var rounds=flow.CurrentRoom.Rounds;Place(new Vector3(0,.03f,-20));
                float e=Time.realtimeSinceStartup+120;while(!rounds.Completed&&Time.realtimeSinceStartup<e){foreach(var h in rounds.Living.ToArray())if(h&&h.CurrentHealth>0)h.Kill();yield return null;}
                int a=flow.PrologueStages;yield return Until(()=>!flow.Airlocks[a].SourceDoor.activeSelf,8,"door");Place(new Vector3(31,.03f,25),180);yield return Until(()=>flow.Busy,8,"airlock");yield return Until(()=>!flow.Busy&&flow.CurrentStage==a+1,90,"next");
            }
            File.WriteAllText(Report+"volume-weight.txt",log.ToString());
        }

        IEnumerator SkipPrologue()
        {
            var intro=Object.FindAnyObjectByType<HangarIntroCinematic>();if(!flow.CurrentRoom||!flow.CurrentRoom.IsPrologue||!intro)yield break;
            yield return Until(()=>intro.IsPlaying||intro.Finished,10,"Intro");intro.RequestSkip();yield return Until(()=>flow.CurrentRoom.Completed&&!PlayerInputLock.Locked,5,"Skip");
            yield return Until(()=>!flow.Airlocks[0].SourceDoor.activeSelf,10,"Hangar door");Place(new Vector3(0,.03f,-80f));yield return Until(()=>!flow.Busy&&flow.CurrentStage==1,90,"Corridor");
            var rounds=flow.CurrentRoom.Rounds;Place(new Vector3(0,.03f,-62f));
            float end=Time.realtimeSinceStartup+90;while(!rounds.Completed&&Time.realtimeSinceStartup<end){foreach(var h in rounds.Living.ToArray())if(h&&h.CurrentHealth>0)h.Kill();yield return null;}
            yield return Until(()=>!flow.Airlocks[1].SourceDoor.activeSelf,8,"Lobby door");var station=Object.FindAnyObjectByType<WeaponMountStation>();
            Place(station.transform.position-station.transform.forward*.5f+Vector3.up*.03f);yield return new WaitForSeconds(.3f);Input(Key.E);yield return null;yield return null;Input();yield return Until(()=>station.Busy,2,"Station");Input(Key.Space);yield return Until(()=>!station.Busy,3,"Skip station");Input();
            Place(new Vector3(0,.03f,-30.5f));yield return Until(()=>!flow.Busy&&flow.CurrentStage==flow.PrologueStages,90,"Room01");
        }

        // ---------- room inspection
        void RoomPass(string name)
        {
            var scene=flow.CurrentRoom.gameObject.scene;var roots=scene.GetRootGameObjects();
            var renderers=roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&!(r is ParticleSystemRenderer)).ToArray();
            var b=new Bounds(player.transform.position,Vector3.one);foreach(var r in renderers)if(r.bounds.size.magnitude<200)b.Encapsulate(r.bounds);
            log.AppendLine("== "+name+" scene="+scene.name+" bounds center="+b.center.ToString("F1")+" size="+b.size.ToString("F1")+" renderers="+renderers.Length);
            // lights
            foreach(var l in roots.SelectMany(r=>r.GetComponentsInChildren<Light>(true)))log.AppendLine("  light "+Path(l.transform)+" "+l.type+" "+l.lightmapBakeType+" I="+l.intensity.ToString("F2")+" range="+l.range.ToString("F1")+" col="+ColorUtility.ToHtmlStringRGB(l.color)+" shadows="+l.shadows+" active="+l.isActiveAndEnabled);
            foreach(var p in roots.SelectMany(r=>r.GetComponentsInChildren<ReflectionProbe>(true)))log.AppendLine("  probe "+Path(p.transform)+" mode="+p.mode+" refresh="+p.refreshMode+" res="+p.resolution+" baked="+(p.bakedTexture?p.bakedTexture.name:"none")+" box="+p.bounds.size.ToString("F1")+" hdr="+p.hdr);
            foreach(var v in roots.SelectMany(r=>r.GetComponentsInChildren<Volume>(true)))log.AppendLine("  volume "+Path(v.transform)+" global="+v.isGlobal+" prio="+v.priority+" weight="+v.weight.ToString("F2")+" profile="+(v.sharedProfile?v.sharedProfile.name:"none")+" active="+v.isActiveAndEnabled);
            foreach(var mb in roots.SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).Where(m=>m&&(m.GetType().Name is "ZoneEnvironment" or "SectorLightingBlend" or "GapFallRecovery" or "LabRoomVisibility" or "CorridorAlarm" or "LightFlicker" or "EnemyStateLights")))log.AppendLine("  comp "+mb.GetType().Name+" on "+Path(mb.transform)+" enabled="+mb.isActiveAndEnabled);
            log.AppendLine("  ambient mode="+RenderSettings.ambientMode+" sky="+ColorUtility.ToHtmlStringRGB(RenderSettings.ambientSkyColor)+" I="+RenderSettings.ambientIntensity.ToString("F2")+" fog="+RenderSettings.fog+" fogMode="+RenderSettings.fogMode+" fogCol="+ColorUtility.ToHtmlStringRGB(RenderSettings.fogColor)+" density="+RenderSettings.fogDensity+" lin="+RenderSettings.fogStartDistance+"-"+RenderSettings.fogEndDistance);
            // missing / error materials and missing scripts
            int pink=0,nullMat=0,missingScripts=0;var badShaders=new HashSet<string>();
            foreach(var r in renderers)foreach(var m in r.sharedMaterials){if(!m){nullMat++;continue;}if(!m.shader||m.shader.name.Contains("InternalErrorShader")||!m.shader.isSupported){pink++;badShaders.Add(m.name+"("+(m.shader?m.shader.name:"null")+") on "+Path(r.transform));}}
            foreach(var t in roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)))missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            log.AppendLine("  errorMaterials="+pink+" nullMaterialSlots="+nullMat+" missingScripts="+missingScripts);foreach(var s in badShaders.Take(15))log.AppendLine("    bad "+s);
            var shaders=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m&&m.shader).GroupBy(m=>m.shader.name).Select(gp=>gp.Key+"x"+gp.Count());log.AppendLine("  shaders "+string.Join(", ",shaders));
            var mats=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m).GroupBy(m=>m.name).OrderByDescending(gp=>gp.Count()).Take(8).Select(gp=>gp.Key+"x"+gp.Count());log.AppendLine("  topMaterials "+string.Join(", ",mats));
            // NavMesh over no collider (fall-through risk) on a 1.5 m grid at each walk level found in the room
            var tri=NavMesh.CalculateTriangulation();var levels=tri.vertices.Where(v=>b.Contains(v)).Select(v=>Mathf.Round(v.y*2)/2).Distinct().OrderBy(y=>y).ToArray();
            int samples=0,holes=0;var holeList=new List<string>();
            foreach(float y in levels.Take(6))for(float x=b.min.x;x<=b.max.x;x+=1.5f)for(float z=b.min.z;z<=b.max.z;z+=1.5f)
            {
                if(!NavMesh.SamplePosition(new Vector3(x,y,z),out var hit,.4f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-y)>.3f)continue;samples++;
                if(!Physics.Raycast(hit.position+Vector3.up*.5f,Vector3.down,1.5f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)){holes++;if(holeList.Count<12)holeList.Add(hit.position.ToString("F1"));}
            }
            log.AppendLine("  navmesh levels="+string.Join("/",levels.Take(8))+" samples="+samples+" navWithoutFloorCollider="+holes+" "+string.Join(" ",holeList));
            // floating props: collider bottoms > 0.12 m above the first surface below
            var floating=new List<string>();
            foreach(var c in roots.SelectMany(r=>r.GetComponentsInChildren<Collider>()).Where(c=>!c.isTrigger&&c.enabled&&c.bounds.size.y<3&&c.bounds.size.x<4&&c.bounds.size.z<4))
            {
                if(c.GetComponentInParent<Light>()||c.GetComponentInParent<Health>())continue;var bb=c.bounds;var start=new Vector3(bb.center.x,bb.min.y-.02f,bb.center.z);
                if(Physics.Raycast(start,Vector3.down,out var below,6,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&below.distance>.12f&&below.distance<5.9f&&!Physics.CheckBox(bb.center-Vector3.up*(bb.extents.y+.06f),new Vector3(bb.extents.x*.9f,.05f,bb.extents.z*.9f),Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    floating.Add(Path(c.transform)+" gap="+below.distance.ToString("F2"));
            }
            log.AppendLine("  floatingCandidates="+floating.Count);foreach(var f in floating.Take(12))log.AppendLine("    float "+f);
            DumpVolumeStack("  ");
            // captures: 4 headings at eye height from the entry point, two high corner overviews
            var eye=player.PlayerCamera.transform;var savedPos=eye.position;var savedRot=eye.rotation;
            for(int i=0;i<4;i++){eye.rotation=Quaternion.Euler(5,i*90,0);Shot(name+"-pano-"+(i*90),false);}
            float top=Mathf.Min(b.max.y-.8f,player.transform.position.y+9);
            var c1=new Vector3(b.min.x+1.5f,top,b.min.z+1.5f);var c2=new Vector3(b.max.x-1.5f,top,b.max.z-1.5f);var center=new Vector3(b.center.x,player.transform.position.y,b.center.z);
            eye.position=c1;eye.LookAt(center);Shot(name+"-overview-a",false);eye.position=c2;eye.LookAt(center);Shot(name+"-overview-b",false);
            eye.position=savedPos;eye.rotation=savedRot;
        }
        void Route(string name,CampaignAirlock airlock)
        {
            // from the room entry, look at the exit door: does the exit read?
            var eye=player.PlayerCamera.transform;var savedRot=eye.rotation;var door=airlock.SourceDoor.transform.position;
            eye.LookAt(door);Shot(name+"-route-exit",false);eye.rotation=savedRot;
            log.AppendLine("  route "+name+" exitDoor="+door.ToString("F1")+" distance="+Vector3.Distance(eye.position,door).ToString("F1")+" visible="+!Physics.Linecast(eye.position,door+Vector3.up*1.5f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore));
        }
        void Reach(string label,CombatTwoRoomsLab rounds)
        {
            int ok=0,partial=0,none=0,off=0;
            foreach(var h in rounds.Living){if(!h)continue;var agent=h.GetComponentInParent<NavMeshAgent>();if(!agent||!agent.enabled)continue;if(!agent.isOnNavMesh){off++;continue;}
                var path=new NavMeshPath();NavMesh.SamplePosition(player.transform.position,out var hit,2,NavMesh.AllAreas);agent.CalculatePath(hit.position,path);if(path.status==NavMeshPathStatus.PathComplete)ok++;else if(path.status==NavMeshPathStatus.PathPartial)partial++;else none++;}
            log.AppendLine("  reach "+label+": complete="+ok+" partial="+partial+" invalid="+none+" offNavMesh="+off);
        }

        // ---------- profiler sample with optional live fire at the nearest enemy
        IEnumerator Sample(string label,float seconds,string mode)
        {
            using var main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",15);
            using var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",15);
            using var batches=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count",15);
            using var setpass=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count",15);
            using var tris=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",15);
            var dts=new List<float>();var mains=new List<double>();var gcs=new List<long>();long maxB=0,maxS=0,maxT=0;int maxParticles=0,maxLights=0,maxSystems=0;int frame=0,shots=0;
            var start=Time.realtimeSinceStartup;float nextShot=0;bool firing=false;
            var enemiesBefore=Object.FindObjectsByType<NavMeshAgent>().ToDictionary(a=>a,a=>a.transform.position);
            while(Time.realtimeSinceStartup-start<seconds)
            {
                yield return null;frame++;dts.Add(Time.unscaledDeltaTime*1000);if(main.Valid&&main.LastValue>0)mains.Add(main.LastValue/1e6);if(gc.Valid)gcs.Add(gc.LastValue);
                maxB=Math.Max(maxB,batches.LastValue);maxS=Math.Max(maxS,setpass.LastValue);maxT=Math.Max(maxT,tris.LastValue);
                if(frame%10==0){var ps=Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);maxSystems=Math.Max(maxSystems,ps.Count(p=>p.IsAlive()));maxParticles=Math.Max(maxParticles,ps.Sum(p=>p.particleCount));maxLights=Math.Max(maxLights,Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l=>l.isActiveAndEnabled&&l.intensity>0&&l.type!=LightType.Directional));}
                if(mode=="fight")
                {
                    Aim();float t=Time.realtimeSinceStartup-start;bool want=(t%1.0f)<.7f;if(want!=firing){Mouse(want);firing=want;}
                    if(t>nextShot&&shots<5){Shot("seq-"+label+"-"+shots,false);shots++;nextShot=t+seconds/5.5f;}
                }
            }
            if(firing)Mouse(false);
            int stuck=enemiesBefore.Count(kv=>kv.Key&&kv.Key.isActiveAndEnabled&&Vector3.Distance(kv.Key.transform.position,kv.Value)<.5f);
            dts.Sort();mains.Sort();float P(List<float> l,float q)=>l.Count==0?0:l[Mathf.Clamp((int)(l.Count*q),0,l.Count-1)];double PD(List<double> l,float q)=>l.Count==0?0:l[Mathf.Clamp((int)(l.Count*q),0,l.Count-1)];
            log.AppendLine("["+label+"] frames="+frame+" dt avg="+dts.Average().ToString("F2")+"ms p50="+P(dts,.5f).ToString("F2")+" p95="+P(dts,.95f).ToString("F2")+" p99="+P(dts,.99f).ToString("F2")+" max="+dts.Last().ToString("F2")
                +" | mainThread p50="+PD(mains,.5f).ToString("F2")+" p95="+PD(mains,.95f).ToString("F2")+" | GC/frame avg="+(gcs.Count>0?gcs.Average()/1024:0).ToString("F1")+"KB max="+(gcs.Count>0?gcs.Max()/1024:0).ToString("F1")+"KB framesWithGC="+gcs.Count(x=>x>0)
                +" | maxBatches="+maxB+" maxSetPass="+maxS+" maxTris="+maxT+" | maxParticles="+maxParticles+" aliveSystems="+maxSystems+" activePointSpotLights="+maxLights+" | agents="+enemiesBefore.Count+" unmovedAgents="+stuck+" living="+(flow.CurrentRoom&&flow.CurrentRoom.Rounds?flow.CurrentRoom.Rounds.LivingEnemies:-1));
        }
        void Aim()
        {
            var rounds=flow.CurrentRoom?flow.CurrentRoom.Rounds:null;if(!rounds)return;var eye=player.PlayerCamera.transform.position;
            var target=rounds.Living.Where(h=>h&&h.CurrentHealth>0).OrderBy(h=>Vector3.Distance(h.transform.position,eye)).FirstOrDefault();if(!target)return;
            var c=target.GetComponentInChildren<Collider>();var d=((c?c.bounds.center:target.transform.position+Vector3.up)-eye).normalized;
            player.SetViewAngles(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,Mathf.Asin(-d.y)*Mathf.Rad2Deg);
        }
        IEnumerator BlurAB()
        {
            var cam=player.PlayerCamera;var data=cam.GetUniversalAdditionalCameraData();
            log.AppendLine("camera AA="+data.antialiasing+" quality="+data.antialiasingQuality+" postFX="+data.renderPostProcessing+" stack="+data.cameraStack.Count+" fov="+cam.fieldOfView.ToString("F1"));
            Place(new Vector3(0,.03f,-20));Freeze();yield return new WaitForSeconds(.3f);
            yield return Turn("blur-A-current");
            var go=new GameObject("MapFxAudit_NoBlur");var vol=go.AddComponent<Volume>();vol.isGlobal=true;vol.priority=1000;var profile=ScriptableObject.CreateInstance<VolumeProfile>();vol.sharedProfile=profile;
            var mb=profile.Add<MotionBlur>();mb.intensity.Override(0);var dof=profile.Add<DepthOfField>();dof.mode.Override(DepthOfFieldMode.Off);var ca=profile.Add<ChromaticAberration>();ca.intensity.Override(0);var ld=profile.Add<LensDistortion>();ld.intensity.Override(0);var pp=profile.Add<PaniniProjection>();pp.distance.Override(0);
            yield return null;yield return Turn("blur-B-noMotionBlurDofCaLens");
            var aa=data.antialiasing;data.antialiasing=AntialiasingMode.None;yield return null;yield return Turn("blur-C-alsoNoAA");data.antialiasing=aa;
            Object.Destroy(go);Object.Destroy(profile);
        }
        IEnumerator Turn(string name)
        {
            // still frame, then a 180 deg/s turn captured from the screen (includes HUD and real motion vectors)
            player.SetViewAngles(0,5);for(int i=0;i<10;i++)yield return null;yield return new WaitForEndOfFrame();Grab(name+"-still");
            float yaw=0;for(int i=0;i<20;i++){yaw+=Time.unscaledDeltaTime*180;player.SetViewAngles(yaw,5);yield return null;}yield return new WaitForEndOfFrame();Grab(name+"-turning");
        }
        void Grab(string name){var tex=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Report+name+".png",tex.EncodeToPNG());log.AppendLine("  screen "+name+" "+Lum(tex));}finally{Object.Destroy(tex);}}

        // ---------- capture: player camera at 1920x1080 through URP (post-processing as configured), plus exposure stats
        void Shot(string name,bool keep=true)
        {
            var camera=player.PlayerCamera;var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);var previous=RenderTexture.active;var target=camera.targetTexture;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(Report+name+".png",tex.EncodeToPNG());log.AppendLine("  shot "+name+" "+Lum(tex));}
            finally{camera.targetTexture=target;RenderTexture.active=previous;Object.Destroy(rt);Object.Destroy(tex);}
        }
        static string Lum(Texture2D tex)
        {
            var px=tex.GetPixels32();double sum=0;int dark=0,blown=0,n=0;
            for(int i=0;i<px.Length;i+=7){var c=px[i];float l=(.2126f*c.r+.7152f*c.g+.0722f*c.b)/255f;sum+=l;if(l<.04f)dark++;if(c.r>=250&&c.g>=250&&c.b>=250)blown++;n++;}
            return "lum="+(sum/n).ToString("F3")+" dark%="+(100f*dark/n).ToString("F1")+" white%="+(100f*blown/n).ToString("F2");
        }
        void DumpRender()
        {
            var asset=UniversalRenderPipeline.asset;
            log.AppendLine("URP asset="+asset.name+" renderScale="+asset.renderScale+" upscaling="+asset.upscalingFilter+" msaa="+asset.msaaSampleCount+" hdr="+asset.supportsHDR+" shadowDist="+asset.shadowDistance+" cascades="+asset.shadowCascadeCount+" mainShadowRes="+asset.mainLightShadowmapResolution+" addLightShadowRes="+asset.additionalLightsShadowmapResolution+" maxAddLights="+asset.maxAdditionalLightsCount+" softShadows="+asset.supportsSoftShadows);
        }
        void DumpVolumeStack(string indent)
        {
            var s=VolumeManager.instance.stack;
            var bloom=s.GetComponent<Bloom>();var tm=s.GetComponent<Tonemapping>();var ca=s.GetComponent<ColorAdjustments>();var mb=s.GetComponent<MotionBlur>();var dof=s.GetComponent<DepthOfField>();var vg=s.GetComponent<Vignette>();var chr=s.GetComponent<ChromaticAberration>();var ld=s.GetComponent<LensDistortion>();var fg=s.GetComponent<FilmGrain>();
            log.AppendLine(indent+"stack bloom active="+bloom.IsActive()+" thr="+bloom.threshold.value+" int="+bloom.intensity.value+" scatter="+bloom.scatter.value+" | tonemap="+tm.mode.value+" | exposure="+ca.postExposure.value+" contrast="+ca.contrast.value+" sat="+ca.saturation.value+" filter="+ColorUtility.ToHtmlStringRGB(ca.colorFilter.value)
                +" | motionBlur active="+mb.IsActive()+" int="+mb.intensity.value+" q="+mb.quality.value+" | dof="+dof.mode.value+" | vignette="+vg.intensity.value+" | chromatic="+chr.intensity.value+" | lens="+ld.intensity.value+" | grain="+fg.intensity.value);
        }

        // ---------- E: door inventory and state sequence (closed -> opening -> open -> closing) for every airlock door.
        // Read only: AirlockDoorMotion is driven at runtime and each door is returned to its initial state; nothing is saved.
        [UnityTest]public IEnumerator E_DoorStatesAndInventory()
        {
            log=new StringBuilder("DOOR audit, Editor Play Mode. stage="+flow.CurrentStage+" airlocks="+flow.Airlocks.Length+"\n");Directory.CreateDirectory(Report+"doors/");
            var pc=player.PlayerCamera;var go=new GameObject("DoorAuditCamera");var cam=go.AddComponent<Camera>();cam.CopyFrom(pc);cam.enabled=false;
            var data=go.AddComponent<UniversalAdditionalCameraData>();var src=pc.GetComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=true;if(src){data.volumeLayerMask=src.volumeLayerMask;data.antialiasing=src.antialiasing;}data.volumeTrigger=go.transform;
            var seen=new HashSet<GameObject>();
            for(int a=0;a<flow.Airlocks.Length;a++)
            {
                var air=flow.Airlocks[a];if(!air){log.AppendLine("airlock["+a+"] missing");continue;}
                log.AppendLine("airlock["+a+"] "+Path(air.transform)+" sourceStage="+air.SourceStage+" pos="+air.transform.position+" cycle="+(air.Cycle?Path(air.Cycle.transform):"none")+" trigger="+Describe(air.GetComponents<Collider>()));
                if(air.Cycle)log.AppendLine("  cycle lights="+Count(air.Cycle,"lights")+" lamps="+Count(air.Cycle,"lamps")+" steam="+Count(air.Cycle,"steam")+" audio="+Ref(air.Cycle,"audioSource")+" clips="+Ref(air.Cycle,"cycleClip")+"/"+Ref(air.Cycle,"doneClip"));
                var doors=new List<(string role,GameObject door)>{("source",air.SourceDoor),("destination",air.DestinationDoor)};foreach(var x in air.ExtraDestinationDoors)doors.Add(("extra",x));
                foreach(var (role,door) in doors)
                {
                    if(!door){log.AppendLine("  "+role+" = null");continue;}
                    string tag="a"+a+"-"+role+"-"+door.name;
                    log.AppendLine("  "+role+" "+Path(door.transform)+" scene="+door.scene.name+(seen.Add(door)?"":" (SHARED with another airlock)")+" active="+door.activeSelf+" layer="+LayerMask.LayerToName(door.layer)+" static="+door.isStatic);
                    log.AppendLine("    renderers: "+string.Join("; ",door.GetComponentsInChildren<Renderer>(true).Select(r=>r.name+"["+string.Join(",",r.sharedMaterials.Select(m=>m?m.name+"<"+m.shader.name+">":"null"))+"] bounds="+r.bounds.size.ToString("F2"))));
                    log.AppendLine("    colliders: "+Describe(door.GetComponentsInChildren<Collider>(true))+" | navObstacle: "+string.Join(",",door.GetComponentsInChildren<NavMeshObstacle>(true).Select(o=>"carve="+o.carving+" shape="+o.shape+" size="+o.size)));
                    log.AppendLine("    audio: "+door.GetComponentsInChildren<AudioSource>(true).Length+" lights: "+door.GetComponentsInChildren<Light>(true).Length+" particles: "+door.GetComponentsInChildren<ParticleSystem>(true).Length+" scripts: "+string.Join(",",door.GetComponentsInChildren<MonoBehaviour>(true).Select(m=>m?m.GetType().Name:"MISSING").Distinct()));
                    bool initial=door.activeSelf;
                    // aim from the room side (away from the airlock centre)
                    var b=DoorBounds(door);var dir=b.center-air.transform.position;dir.y=0;if(dir.sqrMagnitude<.01f)dir=door.transform.forward;dir.Normalize();
                    go.transform.position=b.center+dir*Mathf.Max(6f,b.size.magnitude*1.2f)+Vector3.up*.2f;go.transform.LookAt(b.center);
                    AirlockDoorMotion.Set(flow,door,true);yield return Until(()=>AirlockDoorMotion.IsClosed(door),5,"close "+tag);yield return new WaitForSeconds(.2f);Cap(cam,tag+"-1-closed",door);
                    if(air.Cycle){air.Cycle.Begin();yield return new WaitForSeconds(.35f);Cap(cam,tag+"-1b-cycle-amber",door);air.Cycle.Abort();}
                    AirlockDoorMotion.Set(flow,door,false);yield return new WaitForSeconds(.25f);Cap(cam,tag+"-2-opening-0.25s",door);yield return new WaitForSeconds(.25f);Cap(cam,tag+"-2-opening-0.5s",door);
                    yield return Until(()=>!door.activeSelf,4,"open "+tag);yield return new WaitForSeconds(.1f);Cap(cam,tag+"-3-open",door);
                    AirlockDoorMotion.Set(flow,door,true);bool firstFrame=door.activeSelf&&door.GetComponentsInChildren<Collider>().Any(c=>c.enabled&&!c.isTrigger);yield return new WaitForSeconds(.2f);Cap(cam,tag+"-4-closing-0.2s",door);
                    yield return Until(()=>AirlockDoorMotion.IsClosed(door),4,"reclose "+tag);
                    log.AppendLine("    contract: open ends inactive="+true+" closing collider on first frame="+firstFrame);
                    AirlockDoorMotion.Set(flow,door,initial);yield return new WaitForSeconds(1f);
                }
            }
            // other door-like objects in the loaded scenes that the airlocks do not drive
            var rx=new System.Text.RegularExpressions.Regex("door|shutter|gate|persiana|compuerta|blast",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>rx.IsMatch(t.name)&&!seen.Contains(t.gameObject)&&(!t.parent||!rx.IsMatch(t.parent.name))).OrderBy(t=>t.gameObject.scene.name).ThenBy(t=>t.name))
                log.AppendLine("other: "+t.gameObject.scene.name+" / "+Path(t)+" active="+t.gameObject.activeInHierarchy+" colliders="+Describe(t.GetComponentsInChildren<Collider>(true)));
            Object.Destroy(go);File.WriteAllText(Report+"doors/doors.txt",log.ToString());
        }
        // ---------- F: physical checks of three collision suspects from the static review (read only, raycasts/bounds)
        [UnityTest]public IEnumerator F_CollisionSuspects()
        {
            log=new StringBuilder("COLLISION suspects, Editor Play Mode\n");
            // CampaignFlow keeps a preload parked at 90 % (allowSceneActivation=false), which blocks any other async load.
            yield return flow.DrainPendingLoadsForShutdown();flow.enabled=false;
            foreach(var room in new[]{"Room01_Foundry","Room02_Breach","Room04_Reactor"})
            {
                bool pre=SceneManager.GetSceneByName(room).isLoaded;if(!pre)EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/IronRequiem/Scenes/Campaign/"+room+".unity",new LoadSceneParameters(LoadSceneMode.Additive));
                yield return Until(()=>SceneManager.GetSceneByName(room).isLoaded,30,"load "+room);yield return new WaitForSeconds(.5f);
                var scene=SceneManager.GetSceneByName(room);foreach(var r in scene.GetRootGameObjects())r.SetActive(true);yield return null;Physics.SyncTransforms();
                var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
                Transform Find(string n)=>all.FirstOrDefault(t=>t.name==n);
                if(room=="Room02_Breach")
                {
                    int miss=0,hit=0;var misses=new List<string>();
                    for(float x=-26;x<=26;x+=2)for(float z=-33;z<=-26.5f;z+=1.5f){if(Physics.Raycast(new Vector3(x,12,z),Vector3.down,out var h,90,~0,QueryTriggerInteraction.Ignore)&&h.point.y>-10){hit++;string k=h.collider.name+"@y"+h.point.y.ToString("F1");if(!misses.Contains(k)&&misses.Count<14)misses.Add(k);}else{miss++;if(misses.Count<8)misses.Add("("+x+","+z+")");}}
                    var near=Find("UpperNearBank");log.AppendLine("Room02 south strip x[-26,26] z[-33,-26.5]: ground hits="+hit+" misses="+miss+" e.g. "+string.Join(" ",misses)+" | UpperNearBank bounds="+(near?BStr(near):"missing"));
                }
                if(room=="Room01_Foundry")
                {
                    var wall=Find("SouthWall");var lobby=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="LobbyRear");var station=Object.FindAnyObjectByType<WeaponMountStation>(FindObjectsInactive.Include);
                    log.AppendLine("Room01 SouthWall="+(wall?BStr(wall):"missing")+" LobbyRear="+(lobby?BStr(lobby):"not loaded")+" MountStation="+(station?BStr(station.transform):"not loaded"));
                    if(wall&&station)log.AppendLine("  SouthWall intersects station: "+B(wall).Intersects(B(station.transform)));
                }
                // geometry dump around each suspect (renderer/collider bounds), for the _v2 fixes
                var region=room=="Room01_Foundry"?new Bounds(new Vector3(0,4,-32),new Vector3(70,12,8)):room=="Room02_Breach"?new Bounds(new Vector3(0,9,-29),new Vector3(70,14,12)):new Bounds(new Vector3(0,12,126),new Vector3(70,24,16));
                foreach(var t in all.Where(t=>(t.GetComponent<Collider>()||t.GetComponent<Renderer>())&&B(t).Intersects(region)).OrderBy(t=>t.name).Take(60))
                    log.AppendLine("  geo "+room+" "+Path(t)+" "+BStr(t)+" col="+(t.GetComponent<Collider>()?t.GetComponent<Collider>().GetType().Name:"none")+" mat="+(t.GetComponent<Renderer>()&&t.GetComponent<Renderer>().sharedMaterial?t.GetComponent<Renderer>().sharedMaterial.name:"-")+" pos="+t.position.ToString("F2")+" scale="+t.lossyScale.ToString("F2"));
                if(room=="Room04_Reactor")
                {
                    foreach(var col in all.Where(t=>t.name.StartsWith("CeilingSupport")))
                    {
                        var cb=B(col);var ramps=all.Where(t=>t!=col&&t.name.ToLower().Contains("ramp")&&t.GetComponent<Collider>()&&B(t).Intersects(cb)).Select(t=>t.name).ToArray();
                        log.AppendLine("Room04 "+col.name+" "+BStr(col)+" intersects ramps: "+(ramps.Length==0?"none":string.Join(",",ramps)));
                    }
                }
                if(!pre)yield return SceneManager.UnloadSceneAsync(room);
            }
            File.WriteAllText(Report+"collision-suspects.txt",log.ToString());
        }
        // ---------- G: M7/M8/M9 before (original rooms) and after (_v2 copies): captures, a real walk with the player and
        // NavMesh paths. Rooms are loaded additively by path next to the persistent campaign; nothing is saved.
        [UnityTest,Timeout(600000)]public IEnumerator G_RoomFixesBeforeAfter()
        {
            log=new StringBuilder("ROOM FIXES before/after, Editor Play Mode\n");Directory.CreateDirectory(Report+"fixes/");
            var intro=Object.FindAnyObjectByType<HangarIntroCinematic>();if(intro){yield return Until(()=>intro.IsPlaying||intro.Finished,10,"intro");intro.RequestSkip();}
            yield return Until(()=>!PlayerInputLock.Locked,15,"input unlocked");
            yield return flow.DrainPendingLoadsForShutdown();flow.enabled=false;
            foreach(var room in new[]{"Room02_Breach","Room01_Foundry","Room04_Reactor"})
                foreach(var v in new[]{"","_v2"})
                {
                    string name=room+v,tag=(v==""?"before":"after");
                    EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/IronRequiem/Scenes/Campaign/"+name+".unity",new LoadSceneParameters(LoadSceneMode.Additive));
                    yield return Until(()=>SceneManager.GetSceneByName(name).isLoaded,30,"load "+name);var scene=SceneManager.GetSceneByName(name);
                    foreach(var r in scene.GetRootGameObjects())r.SetActive(true);foreach(var e in Object.FindObjectsByType<EnemyController>())e.gameObject.SetActive(false);
                    yield return new WaitForSeconds(.6f);Physics.SyncTransforms();
                    if(room=="Room02_Breach")
                    {
                        Place(new Vector3(0,8.03f,-18),180);yield return new WaitForSeconds(.3f);player.SetViewAngles(180,20);yield return null;Shot("fixes/m7-room02-south-"+tag);
                        foreach(float wx in new[]{0f,-15f,18f})
                        {
                            Place(new Vector3(wx,8.03f,-18),180);yield return new WaitForSeconds(.2f);float minY=99,minZ=99;Input(Key.W);for(float end=Time.realtimeSinceStartup+3.5f;Time.realtimeSinceStartup<end;){yield return null;minY=Mathf.Min(minY,player.transform.position.y);minZ=Mathf.Min(minZ,player.transform.position.z);if(minY<-5)break;}Input();Time.timeScale=1;
                            log.AppendLine(name+" M7 walk south at x="+wx+" from z -18 for 3.5 s: min y="+minY.ToString("F2")+" min z="+minZ.ToString("F2")+" final="+player.transform.position.ToString("F2")+(minY<0?" -> FELL INTO THE VOID":minY<7.9f?" -> dropped to a lower surface":" -> stayed on the floor"));
                        }
                        Path3(name,new Vector3(0,8,-18),new Vector3(0,8,-31));
                    }
                    if(room=="Room01_Foundry")
                    {
                        var station=Object.FindAnyObjectByType<WeaponMountStation>(FindObjectsInactive.Include);var sb=B(station.transform);
                        var hits=Physics.OverlapBox(sb.center,sb.extents*.98f,Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Where(c=>c.gameObject.scene==scene).Select(c=>c.name).Distinct().ToArray();
                        log.AppendLine(name+" M8 room colliders overlapping the mount station: "+(hits.Length==0?"none":string.Join(",",hits)));
                        Place(new Vector3(0,.03f,-28),180);yield return new WaitForSeconds(.3f);player.SetViewAngles(180,8);yield return null;Shot("fixes/m8-room01-station-"+tag);
                        Place(new Vector3(-12,.03f,-28),180);yield return new WaitForSeconds(.2f);player.SetViewAngles(160,5);yield return null;Shot("fixes/m8-room01-southwest-"+tag);
                        float minZ=99;Place(new Vector3(-17,.03f,-30),180);Input(Key.W);for(float end=Time.realtimeSinceStartup+2f;Time.realtimeSinceStartup<end;){yield return null;minZ=Mathf.Min(minZ,player.transform.position.z);}Input();
                        log.AppendLine(name+" M8 walk into the west span of the south wall: min z="+minZ.ToString("F2")+(minZ<-33.6f?" -> PASSED THROUGH":" -> blocked by wall"));
                    }
                    if(room=="Room04_Reactor")
                    {
                        Place(new Vector3(-23.8f,8.03f,117.5f),0);yield return new WaitForSeconds(.3f);player.SetViewAngles(0,-12);yield return null;Shot("fixes/m9-room04-ramp-"+tag);
                        player.SetViewAngles(0,0);float maxY=0;bool onCatwalk=false;Input(Key.W);for(float end=Time.realtimeSinceStartup+5f;Time.realtimeSinceStartup<end;){yield return null;var q=player.transform.position;maxY=Mathf.Max(maxY,q.y);if(q.y>11.4f&&q.z>131.5f)onCatwalk=true;}Input();
                        var p=player.transform.position;log.AppendLine(name+" M9 walk up the west ramp for 5 s: max y="+maxY.ToString("F2")+" final="+p.ToString("F2")+(onCatwalk?" -> reached the catwalk":" -> did NOT reach the catwalk"));
                        Place(new Vector3(20,8.03f,118f),-20);yield return new WaitForSeconds(.2f);player.SetViewAngles(-20,-8);yield return null;Shot("fixes/m9-room04-east-"+tag);
                        Path3(name,new Vector3(-23.8f,8,118),new Vector3(-23.75f,11.8f,140));Path3(name,new Vector3(23.8f,8,118),new Vector3(23.75f,11.8f,140));
                    }
                    File.WriteAllText(Report+"fixes/fixes.txt",log.ToString());Place(new Vector3(0,.03f,-31),0);yield return SceneManager.UnloadSceneAsync(scene);yield return null;
                }
            File.WriteAllText(Report+"fixes/fixes.txt",log.ToString());
        }
        // ---------- H: P-E2a. During the wake-up cinematic HG_PostVolume keeps HG_MenuVolume and the gameplay overlay
        // contributes nothing; after the cinematic the overlay ramps to the source weight. Captures for before/after.
        [UnityTest,Timeout(300000)]public IEnumerator H_HangarPostAfterCinematic()
        {
            log=new StringBuilder("P-E2a hangar post, Editor Play Mode\n");Directory.CreateDirectory(Report+"fixes/");
            var intro=Object.FindAnyObjectByType<HangarIntroCinematic>();Assert.That(intro,Is.Not.Null);
            var hg=Object.FindObjectsByType<Volume>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(v=>v.name=="HG_PostVolume"&&v.gameObject.scene.name=="Room00_Hangar");
            yield return Until(()=>intro.IsPlaying||intro.Finished,10,"intro starts");yield return new WaitForSeconds(1.5f);
            var post=Object.FindAnyObjectByType<HangarGameplayPost>();
            log.AppendLine("cinematic t=1.5s: HG_PostVolume profile="+hg.sharedProfile.name+" weight="+hg.weight.ToString("F2")+" overlay="+(post?post.Overlay.sharedProfile.name+" prio="+post.Overlay.priority+" weight="+post.Overlay.weight.ToString("F3"):"none"));
            Assert.That(hg.sharedProfile.name,Is.EqualTo("HG_MenuVolume"));Assert.That(!post||post.Overlay.weight<.001f,Is.True,"overlay silent during the cinematic");
            var cam=Camera.allCameras.Where(c=>c.isActiveAndEnabled).OrderByDescending(c=>c.depth).First();CapCam(cam,"fixes/pe2a-cinematic-1.5s");
            yield return new WaitForSeconds(2f);CapCam(Camera.allCameras.Where(c=>c.isActiveAndEnabled).OrderByDescending(c=>c.depth).First(),"fixes/pe2a-cinematic-3.5s");
            log.AppendLine("cinematic t=3.5s overlay weight="+(post?post.Overlay.weight.ToString("F3"):"none"));
            intro.RequestSkip();yield return Until(()=>intro.Finished&&!PlayerInputLock.Locked,15,"cinematic done");
            post=Object.FindAnyObjectByType<HangarGameplayPost>();Assert.That(post,Is.Not.Null,"overlay created by ZoneEnvironment.Bind");
            float t0=Time.realtimeSinceStartup;yield return Until(()=>post.Ramp>=1,5,"ramp");log.AppendLine("ramp to 1 in "+(Time.realtimeSinceStartup-t0).ToString("F2")+" s after Finished; overlay weight="+post.Overlay.weight.ToString("F2")+" source weight="+hg.weight.ToString("F2")+" HG_PostVolume profile still="+hg.sharedProfile.name+" overlay scene="+post.gameObject.scene.name+" root="+(post.transform.parent==null));
            Assert.That(hg.sharedProfile.name,Is.EqualTo("HG_MenuVolume"));Assert.That(post.Overlay.weight,Is.EqualTo(hg.weight).Within(.01f));
            yield return new WaitForSeconds(.5f);DumpVolumeStack("  gameplay stack: ");Shot("fixes/pe2a-hangar-gameplay");
            File.WriteAllText(Report+"fixes/pe2a.txt",log.ToString());
        }
        void CapCam(Camera cam,string name)
        {
            var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);var prev=RenderTexture.active;var target=cam.targetTexture;
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(Report+name+".png",tex.EncodeToPNG());log.AppendLine("  shot "+name+" cam="+cam.name+" "+Lum(tex));}
            finally{cam.targetTexture=target;RenderTexture.active=prev;Object.Destroy(rt);Object.Destroy(tex);}
        }

        // ---------- I: E7. Two prefabs prepared in one scene keep their own burst sets; the legacy call uses the first.
        [UnityTest]public IEnumerator I_BurstPoolPerPrefab()
        {
            log=new StringBuilder("E7 burst pool per prefab\n");
            var orig=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/VFX/RobotBodyBurst_CombatPolish.prefab");var ir=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/VisualOverhaul/VFX/RobotBodyBurst_CombatPolish_IR.prefab");
            Assert.That(orig&&ir,Is.True);
            var scene=SceneManager.CreateScene("E7Pool"+Time.frameCount);
            // the campaign may already own a pool from its enemies: count relative to it
            int prefabs0=RobotDestructionPool.BurstPrefabs,inUse0=RobotDestructionPool.BurstsInUse;
            RobotDestructionPool.Prepare(orig,scene);RobotDestructionPool.Prepare(ir,scene);RobotDestructionPool.Prepare(ir,scene);
            log.AppendLine("prefab sets "+prefabs0+" -> "+RobotDestructionPool.BurstPrefabs);
            Assert.That(RobotDestructionPool.Burst(ir,null,player.transform.position+player.transform.forward*6,null,1),Is.True);
            Assert.That(RobotDestructionPool.Burst(orig,null,player.transform.position+player.transform.forward*6+Vector3.right*2,null,1),Is.True);
            yield return null;
            var active=Object.FindObjectsByType<RobotBodyBurst>(FindObjectsSortMode.None).Where(b=>b.InUse).Select(b=>b.name).ToArray();
            log.AppendLine("active bursts: "+string.Join(", ",active));
            Assert.That(active.Any(n=>n.StartsWith("RobotBodyBurst_CombatPolish_IR")),Is.True,"IR burst played");
            Assert.That(active.Any(n=>n.StartsWith("RobotBodyBurst_CombatPolish(")),Is.True,"original burst played");
            Assert.That(RobotDestructionPool.BurstsInUse,Is.EqualTo(inUse0+2));
            yield return new WaitForSeconds(3.5f);Assert.That(RobotDestructionPool.BurstsInUse,Is.EqualTo(inUse0));
            File.WriteAllText(Report+"fixes/e7.txt",log.ToString());
        }
        void Path3(string name,Vector3 a,Vector3 b)
        {
            var path=new NavMeshPath();bool sa=NavMesh.SamplePosition(a,out var ha,2,NavMesh.AllAreas),sbb=NavMesh.SamplePosition(b,out var hb,2,NavMesh.AllAreas);
            bool ok=sa&&sbb&&NavMesh.CalculatePath(ha.position,hb.position,NavMesh.AllAreas,path);
            log.AppendLine("  "+name+" navmesh "+a+" -> "+b+": sampleA="+sa+" sampleB="+sbb+" status="+(ok?path.status.ToString():"none")+" corners="+path.corners.Length);
        }
        static Bounds B(Transform t){var cs=t.GetComponentsInChildren<Collider>(true);if(cs.Length>0){var b=cs[0].bounds;foreach(var c in cs)b.Encapsulate(c.bounds);return b;}return DoorBounds(t.gameObject);}
        static string BStr(Transform t){var b=B(t);return "min"+b.min.ToString("F1")+" max"+b.max.ToString("F1");}
        void Cap(Camera cam,string name,GameObject door)
        {
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);var prev=RenderTexture.active;
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(Report+"doors/"+name+".png",tex.EncodeToPNG());log.AppendLine("    shot "+name+" active="+door.activeSelf+" y="+door.transform.position.y.ToString("F2")+" "+Lum(tex));}
            finally{cam.targetTexture=null;RenderTexture.active=prev;Object.Destroy(rt);Object.Destroy(tex);}
        }
        static Bounds DoorBounds(GameObject g){var rs=g.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)return new Bounds(g.transform.position,Vector3.one*3);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        static string Describe(Collider[] cs)=>cs.Length==0?"none":string.Join(",",cs.Select(c=>c.GetType().Name+(c.isTrigger?"(trigger)":"")+(c.enabled?"":"(off)")+" "+c.bounds.size.ToString("F1")));
        static string Count(Object o,string field){var p=new SerializedObject(o).FindProperty(field);return p!=null&&p.isArray?p.arraySize.ToString():"?";}
        static string Ref(Object o,string field){var p=new SerializedObject(o).FindProperty(field);return p!=null&&p.objectReferenceValue?p.objectReferenceValue.name:"none";}

        // ---------- helpers (same harness as CampaignTests)
        void Input(params Key[] held)=>InputSystem.QueueStateEvent(keys,new KeyboardState(held));
        void Mouse(bool left)=>InputSystem.QueueStateEvent(mouse,left?new MouseState().WithButton(MouseButton.Left,true):new MouseState());
        void Place(Vector3 point,float yaw=0){player.GetComponent<AdvancedPlayerMovement>().ResetForCheckpoint();player.TeleportToCheckpoint(point);player.SetViewAngles(yaw,0);Physics.SyncTransforms();}
        static string Path(Transform t){var p=t.name;for(int i=0;i<3&&t.parent;i++){t=t.parent;p=t.name+"/"+p;}return p;}
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
            float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;
            if(!condition()){if(log!=null)File.WriteAllText(Report+"partial-"+TestContext.CurrentContext.Test.MethodName+".txt",log.ToString());Assert.Fail(label+" stage="+(flow?flow.CurrentStage:-99)+" player="+(player?player.transform.position.ToString():"none"));}
        }
    }
}
