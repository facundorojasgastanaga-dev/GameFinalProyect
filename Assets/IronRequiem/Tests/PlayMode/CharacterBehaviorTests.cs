using System.Collections;
using System.IO;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace IronRequiem.Tests
{
    public sealed class CharacterBehaviorTests
    {
        PlayerCharacterController player;
        EnemyController enemy;
        Keyboard keyboard;
        Mouse mouse;
        float previousTimeScale;
        UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;

        [UnitySetUp]
        public IEnumerator LoadLab()
        {
            previousTimeScale=Time.timeScale;Time.timeScale=1;
            Application.runInBackground=true;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/IronRequiem/Scenes/CharacterLab.unity",new LoadSceneParameters(LoadSceneMode.Single));
#endif
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent();mouse.MakeCurrent();
            previousDevices=InputSystem.actions.devices;
            InputSystem.actions.devices=new InputDevice[]{keyboard,mouse};
            yield return null;yield return null;
            player=Object.FindAnyObjectByType<PlayerCharacterController>();enemy=Object.FindAnyObjectByType<EnemyController>();
            Assert.That(player,Is.Not.Null);Assert.That(enemy,Is.Not.Null);
            player.GetComponent<Health>().Invincible=true;
#if UNITY_EDITOR
            if(!Application.isBatchMode)
                UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
#endif
            Cursor.lockState=CursorLockMode.Locked;
            yield return new WaitForSeconds(.4f);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale=1;
            var scene=SceneManager.GetActiveScene();
            var empty=SceneManager.CreateScene("CharacterTestCleanup_"+Time.frameCount);SceneManager.SetActiveScene(empty);
            if(scene.IsValid() && scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            InputSystem.actions.devices=previousDevices;
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            if(mouse!=null)InputSystem.RemoveDevice(mouse);
            Cursor.lockState=CursorLockMode.None;
            Time.timeScale=previousTimeScale;
            yield return null;
        }

        void Teleport(Vector3 position)
        {
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.SetPositionAndRotation(position,Quaternion.identity);cc.enabled=true;player.CharacterVelocity=Vector3.zero;Physics.SyncTransforms();
        }

        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));

        [UnityTest]
        public IEnumerator ActualInputMovesLooksJumpsCrouchesShootsAndSwitchesWeapons()
        {
            Assert.That(player.GetComponent<PlayerInputHandler>().CanProcessInput(),Is.True,"Input gate must open in the running editor");
            Assert.That(player.IsGrounded,Is.True);
            var start=player.transform.position;var rotation=player.PlayerCamera.transform.rotation;
            Keys(Key.W);InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(6,3)});
            yield return new WaitForSeconds(.25f);
            float straight=Vector3.ProjectOnPlane(player.transform.position-start,Vector3.up).magnitude;
            Assert.That(straight,Is.GreaterThan(.5f));Assert.That(Quaternion.Angle(rotation,player.PlayerCamera.transform.rotation),Is.GreaterThan(.1f));
            Keys();yield return new WaitForSeconds(.2f);Teleport(start);yield return new WaitForSeconds(.1f);
            Keys(Key.W,Key.D);yield return new WaitForSeconds(.25f);
            float diagonal=Vector3.ProjectOnPlane(player.transform.position-start,Vector3.up).magnitude;
            Assert.That(diagonal,Is.LessThan(straight*1.25f),"Diagonal input must remain clamped");
            Keys();yield return new WaitForSeconds(.3f);float y=player.transform.position.y;
            Keys(Key.Space);yield return new WaitForSeconds(.12f);Assert.That(player.transform.position.y,Is.GreaterThan(y+.1f));
            Keys();yield return new WaitForSeconds(1.1f);
            Keys(Key.C);yield return new WaitForSeconds(.2f);Keys();
            Assert.That(player.IsCrouching,Is.True);
            Keys(Key.C);yield return new WaitForSeconds(.2f);Keys();Assert.That(player.IsCrouching,Is.False);
            var manager=player.GetComponent<PlayerWeaponsManager>();var weapon=manager.GetActiveWeapon();int shots=0;weapon.OnShootProcessed+=Count;
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(.4f);
            InputSystem.QueueStateEvent(mouse,new MouseState());weapon.OnShootProcessed-=Count;Assert.That(shots,Is.GreaterThan(0));
            int previous=manager.ActiveWeaponIndex;Keys(Key.Digit2);yield return new WaitForSeconds(1.2f);Keys();
            Assert.That(manager.ActiveWeaponIndex,Is.EqualTo(previous),"The second initial slot was removed by the design update");
            // Preserve the weapon-switch regression using the allowed stolen cannon, rather than a shotgun.
#if UNITY_EDITOR
            Assert.That(manager.AddWeapon(UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponController>("Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary.prefab")),Is.True);
            Keys(Key.Digit2);yield return new WaitForSeconds(2.2f);Keys();
            Assert.That(manager.ActiveWeaponIndex,Is.Not.EqualTo(previous));
#endif
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length,Is.GreaterThan(0));
            Keys(Key.P);yield return new WaitForSecondsRealtime(.15f);Keys();yield return null;
            Assert.That(Time.timeScale,Is.Zero);Assert.That(player.GetComponent<PlayerInputHandler>().CanProcessInput(),Is.False);
            Keys(Key.P);yield return new WaitForSecondsRealtime(.15f);Keys();yield return null;
            Assert.That(Time.timeScale,Is.EqualTo(1));
            void Count() => shots++;
        }

        [UnityTest]
        public IEnumerator EnemyPatrolDetectAttackLoseTargetDetachRetreatAndDie()
        {
            var initial=enemy.transform.position;
            yield return new WaitForSeconds(1.2f);
            Assert.That(enemy.NavMeshAgent.isOnNavMesh,Is.True);
            Assert.That(Vector3.Distance(initial,enemy.transform.position),Is.GreaterThan(.2f),"state="+enemy.GetComponent<EnemyMobile>().AiState+", speed="+enemy.NavMeshAgent.speed+", path="+enemy.NavMeshAgent.pathStatus+", pending="+enemy.NavMeshAgent.pathPending+", destination="+enemy.NavMeshAgent.destination+", patrol="+(enemy.PatrolPath!=null));
            Teleport(new Vector3(0,.05f,2));int shots=0;var weapon=enemy.GetCurrentWeapon();weapon.OnShootProcessed+=Count;
            yield return new WaitForSeconds(1.5f);
            Assert.That(enemy.KnownDetectedTarget,Is.Not.Null);Assert.That(enemy.IsSeeingTarget,Is.True);Assert.That(shots,Is.GreaterThan(0));
            var mobile=enemy.GetComponent<EnemyMobile>();mobile.enabled=false;enemy.NavMeshAgent.ResetPath();
            // Fixed positions put solid cover between the detector and the player.
            enemy.NavMeshAgent.Warp(new Vector3(-4,0,4));Teleport(new Vector3(-4,.05f,-4));
            yield return new WaitForSeconds(2.3f);
            Assert.That(enemy.IsSeeingTarget,Is.False);Assert.That(enemy.KnownDetectedTarget,Is.Null);
            mobile.enabled=true;Teleport(new Vector3(-4,.05f,6));yield return new WaitForSeconds(.3f);
            Assert.That(enemy.KnownDetectedTarget,Is.Not.Null);
            var cannon=enemy.GetComponentInChildren<DetachableCannon>();float health=enemy.GetComponent<Health>().CurrentHealth;
            cannon.GetComponent<Damageable>().InflictDamage(50,false,player.gameObject);yield return null;
            Assert.That(cannon.IsDetached,Is.True);Assert.That(mobile.enabled,Is.False);Assert.That(enemy.GetComponent<Health>().CurrentHealth,Is.EqualTo(health));
            int rigidbodies=Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;
            cannon.Detach();cannon.Detach();yield return null;
            Assert.That(Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length,Is.EqualTo(rigidbodies));
            int before=shots;float distanceBefore=Vector3.Distance(enemy.transform.position,player.transform.position);
            yield return new WaitForSeconds(1.2f);Assert.That(shots,Is.EqualTo(before),"Disarmed enemy must stop ranged attacks");
            Assert.That(Vector3.Distance(enemy.transform.position,player.transform.position),Is.GreaterThan(distanceBefore+.1f),"Disarmed agent must retreat");
            Assert.That(enemy.NavMeshAgent.enabled,Is.True);weapon.OnShootProcessed-=Count;
            float deathDuration=enemy.DeathDuration;enemy.GetComponent<Health>().Kill();
            yield return new WaitForSeconds(Mathf.Max(.2f,deathDuration+.1f));Assert.That(!enemy,Is.True);
            void Count() => shots++;
        }

        [UnityTest]
        public IEnumerator RunningLabAdvancesFramesAndRecordsEditorBaseline()
        {
            yield return new WaitForSeconds(1);
            var times=new float[120];int initialFrame=Time.frameCount;
            using(var cpu=Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal,"Main Thread",120))
            using(var gc=Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory,"GC Allocated In Frame",120))
            {
                for(int i=0;i<times.Length;i++){ yield return null;times[i]=Time.unscaledDeltaTime*1000; }
                System.Array.Sort(times);float sum=0;foreach(var ms in times)sum+=ms;
                var report=new EditorBaseline{unity=Application.unityVersion,graphics=SystemInfo.graphicsDeviceName,width=Screen.width,height=Screen.height,quality=QualitySettings.names[QualitySettings.GetQualityLevel()],vSync=QualitySettings.vSyncCount,enemies=1,frames=Time.frameCount-initialFrame,averageFrameMs=sum/times.Length,p95FrameMs=times[114],lastMainThreadMs=cpu.Valid?cpu.LastValue/1000000f:-1,lastGcAllocatedBytes=gc.Valid?gc.LastValue:-1};
                File.WriteAllText("Tools/IronRequiem/Reports/character-editor-baseline.json",JsonUtility.ToJson(report,true));
            }
            Assert.That(Time.frameCount-initialFrame,Is.GreaterThanOrEqualTo(120));
            var camera=player.PlayerCamera;var target=camera.targetTexture;var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Tools/IronRequiem/Reports/character-lab-playmode.png",image.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=target;rt.Release();Object.Destroy(rt);Object.Destroy(image);
            var display=GameObject.Find("Player_Visual_Inspection");
            var preview=new GameObject("ArtVerificationCamera").AddComponent<Camera>();
            preview.transform.position=display.transform.position+new Vector3(-2.3f,1.5f,-3.5f);preview.transform.LookAt(display.transform.position+Vector3.up);
            preview.fieldOfView=32;preview.clearFlags=CameraClearFlags.SolidColor;preview.backgroundColor=new Color(.08f,.105f,.125f);
            var art=new RenderTexture(900,900,24);preview.targetTexture=art;
            yield return null;yield return null;yield return null;
            previous=RenderTexture.active;RenderTexture.active=art;image=new Texture2D(900,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,900,900),0,0);image.Apply();File.WriteAllBytes("Tools/IronRequiem/Reports/player-robot-v01.png",image.EncodeToPNG());RenderTexture.active=previous;
            preview.targetTexture=null;art.Release();Object.Destroy(art);Object.Destroy(image);Object.Destroy(preview.gameObject);
        }

        [System.Serializable] sealed class EditorBaseline
        {
            public string unity,graphics,quality;public int width,height,vSync,enemies,frames;public float averageFrameMs,p95FrameMs,lastMainThreadMs;public long lastGcAllocatedBytes;
        }
    }
}
