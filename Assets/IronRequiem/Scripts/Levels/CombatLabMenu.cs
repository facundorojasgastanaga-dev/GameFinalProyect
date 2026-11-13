using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace IronRequiem
{
 public sealed class CombatLabMenu : MonoBehaviour,ICombatLabMenuFlow
 {
  [SerializeField] PlayerCharacterController player;
  [SerializeField] GameObject mainPanel,deathPanel;
  [SerializeField] MonoBehaviour pauseManager;
  static bool startAfterReload;
  public void ConfigurePause(MonoBehaviour manager)=>pauseManager=manager;
  public bool IsPlaying {get;private set;}
  public bool IsDeathMenu=>deathPanel&&deathPanel.activeSelf;
  public bool IsMainMenu=>mainPanel&&mainPanel.activeSelf;
  // 08/11/2026: when the lab is entered from the main menu (IntroMenu > JUGAR), the lab's own start menu is skipped and
  // "volver al menu" goes back to IntroMenu. Opened any other way (Editor, Build Settings), the lab behaves as before.
  public const string MainMenuScene=MainMenuLaunch.MainMenuScene;
  static bool fromMainMenu;static string lastScene="";
  public static bool LaunchedFromMainMenu=>fromMainMenu;
  // 12/11/2026: true when this session started from "reiniciar" after a death; intro cinematics are skipped then.
  public static bool ResumedAfterRestart {get;private set;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetState(){startAfterReload=false;ResumedAfterRestart=false;fromMainMenu=false;lastScene="";MainMenuLaunch.Clear();SceneManager.sceneLoaded-=Track;SceneManager.sceneLoaded+=Track;}
  // The main menu's JUGAR (RequiemMenuView) or a level continuing the run names the target first (MainMenuLaunch).
  // A later level of the same run keeps the flag: call ContinueRun(target) before loading it.
  public static void ContinueRun(string target){if(fromMainMenu)MainMenuLaunch.Continue(target);}
  static void Track(Scene s,LoadSceneMode mode){if(mode!=LoadSceneMode.Single)return;var pending=MainMenuLaunch.Consume();if(pending!=null&&s.name==pending)fromMainMenu=true;else if(s.name!=lastScene)fromMainMenu=false;lastScene=s.name;}
  public void Configure(PlayerCharacterController p,GameObject menu,GameObject death){player=p;mainPanel=menu;deathPanel=death;}
  void Start(){ResumedAfterRestart=startAfterReload;if(startAfterReload||fromMainMenu){startAfterReload=false;BeginGame();}else ShowMain();}
  void LateUpdate(){if(pauseManager)pauseManager.enabled=IsPlaying&&player&&!player.IsDead;if(!IsPlaying){Pause();return;}if(IsPlaying&&player&&player.GetComponent<Health>().CurrentHealth<=0){IsPlaying=false;mainPanel.SetActive(false);deathPanel.SetActive(true);Pause();}}
  void Pause(){AudioListener.pause=true;Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
  void ShowMain(){IsPlaying=false;mainPanel.SetActive(true);deathPanel.SetActive(false);Pause();}
  public void BeginGame(){mainPanel.SetActive(false);deathPanel.SetActive(false);IsPlaying=true;if(pauseManager)pauseManager.enabled=true;AudioListener.pause=false;Time.timeScale=1;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
  public void RestartFromFirstRoom(){Reload(true);}
  public void ReturnToMainMenu(){if(fromMainMenu){Time.timeScale=1;AudioListener.pause=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;SceneManager.LoadScene(MainMenuScene,LoadSceneMode.Single);return;}Reload(false);}
  void Reload(bool play){startAfterReload=play;Time.timeScale=1;SceneManager.LoadScene(gameObject.scene.name,LoadSceneMode.Single);}
  void OnDestroy(){Time.timeScale=1;AudioListener.pause=false;}
 }
}
