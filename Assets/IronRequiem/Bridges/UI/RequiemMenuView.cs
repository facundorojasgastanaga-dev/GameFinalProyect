using System;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace IronRequiem
{
    // View and settings binding only. Existing InGameMenuManager owns pause; LoadSceneButton owns loading.
    public sealed class RequiemMenuView : MonoBehaviour
    {
        [SerializeField] InGameMenuManager pauseOwner;
        [SerializeField] GameObject home, options, credits;
        [SerializeField] UnityEngine.UI.Button primary, optionsButton, creditsButton, quitButton, optionsBack, creditsBack, menuButton;
        [SerializeField] UnityEngine.UI.Slider volume, sensitivity;
        [SerializeField] UnityEngine.UI.Toggle fullscreen;
        [SerializeField] LoadSceneButton sceneLoader;
        [SerializeField, TextArea(3,12)] string creditsText="Créditos pendientes de completar.";
        [SerializeField] TMPro.TMP_Text creditsLabel, volumeValue, sensitivityValue;
        public bool IsOptionsOpen=>options.activeSelf;
        public void Configure(InGameMenuManager owner,GameObject homePanel,GameObject optionsPanel,GameObject creditsPanel,
            UnityEngine.UI.Button play,UnityEngine.UI.Button opts,UnityEngine.UI.Button creds,UnityEngine.UI.Button quit,
            UnityEngine.UI.Button backOptions,UnityEngine.UI.Button backCredits,UnityEngine.UI.Button returnMenu,
            UnityEngine.UI.Slider vol,UnityEngine.UI.Slider sens,UnityEngine.UI.Toggle full,LoadSceneButton loader,
            TMPro.TMP_Text creditsBody,TMPro.TMP_Text volValue,TMPro.TMP_Text sensValue)
        {pauseOwner=owner;home=homePanel;options=optionsPanel;credits=creditsPanel;primary=play;optionsButton=opts;creditsButton=creds;quitButton=quit;optionsBack=backOptions;creditsBack=backCredits;menuButton=returnMenu;volume=vol;sensitivity=sens;fullscreen=full;sceneLoader=loader;creditsLabel=creditsBody;volumeValue=volValue;sensitivityValue=sensValue;}
        [SerializeField] MonoBehaviour labMenuSource;
        ICombatLabMenuFlow LabFlow=>labMenuSource as ICombatLabMenuFlow;
        public void ConfigureLab(MonoBehaviour source)=>labMenuSource=source;
        MenuNavigation navigation;
        Vector2 layoutViewportSize;
        void Start()
        {
            navigation=GetComponent<MenuNavigation>();if(!navigation)navigation=FindAnyObjectByType<MenuNavigation>();
            if(!pauseOwner){if(LabFlow==null)ReleaseSession();RequiemSettings.Apply();}
            else {pauseOwner.PauseChanged+=OnPause;pauseOwner.BackHandler=HandleBack;RequiemSettings.ApplyVolume();}
            NormalizePauseViewport();RefreshSettings();FitLayout();
            volume.onValueChanged.AddListener(ChangeVolume);sensitivity.onValueChanged.AddListener(ChangeSensitivity);fullscreen.onValueChanged.AddListener(RequiemSettings.SetFullscreen);
            primary.onClick.AddListener(Primary);optionsButton.onClick.AddListener(OpenOptions);optionsBack.onClick.AddListener(ShowHome);
            if(creditsButton)creditsButton.onClick.AddListener(OpenCredits);if(creditsBack)creditsBack.onClick.AddListener(ShowHome);
            if(quitButton)quitButton.onClick.AddListener(Quit);if(menuButton)menuButton.onClick.AddListener(ReturnToMenu);
            if(creditsLabel)creditsLabel.text=creditsText;
            ShowHome();
        }
        void OnDestroy(){if(pauseOwner){pauseOwner.PauseChanged-=OnPause;if(pauseOwner.BackHandler==(System.Func<bool>)HandleBack)pauseOwner.BackHandler=null;}}
        // v5: Escape / VOLVER from options or credits returns to this menu's home (main menu and pause alike).
        public bool IsSubScreenOpen=>(options&&options.activeSelf)||(credits&&credits.activeSelf);
        bool HandleBack(){if(!IsSubScreenOpen)return false;ShowHome();return true;}
        void Update(){if(!pauseOwner&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame&&IsSubScreenOpen)ShowHome();if(home&&home.transform.parent.parent is RectTransform viewport&&viewport.gameObject.activeInHierarchy&&viewport.rect.size!=layoutViewportSize)FitLayout();}
        void OnPause(bool paused){if(paused){NormalizePauseViewport();RefreshSettings();Canvas.ForceUpdateCanvases();}ShowHome();}
        public void ShowHome(){home.SetActive(true);options.SetActive(false);if(credits)credits.SetActive(false);FitLayout();if(navigation)navigation.DefaultSelection=primary;if(primary.gameObject.activeInHierarchy)primary.Select();}
        public void OpenOptions(){RefreshSettings();home.SetActive(false);options.SetActive(true);if(credits)credits.SetActive(false);FitLayout();if(navigation)navigation.DefaultSelection=volume;volume.Select();}
        public void OpenCredits(){home.SetActive(false);options.SetActive(false);credits.SetActive(true);if(navigation)navigation.DefaultSelection=creditsBack;creditsBack.Select();}
        public void ChangeVolume(float value){RequiemSettings.SetVolume(value,pauseOwner&&pauseOwner.IsPaused?pauseOwner.VolumeWhenMenuOpen:1);UpdateLabels();}
        public void ChangeSensitivity(float value){RequiemSettings.SetSensitivity(value);var p=FindAnyObjectByType<PlayerInputHandler>();if(p)p.LookSensitivity=RequiemSettings.Sensitivity();UpdateLabels();}
        void UpdateLabels(){volumeValue.SetText("{0}%",Mathf.RoundToInt(volume.value*100));sensitivityValue.SetText("{0:2}",sensitivity.value);}
        void RefreshSettings()
        {
            volume.SetValueWithoutNotify(RequiemSettings.Volume);
            var input=FindAnyObjectByType<PlayerInputHandler>();
            sensitivity.SetValueWithoutNotify(RequiemSettings.Sensitivity(input?input.LookSensitivity:1));
            fullscreen.SetIsOnWithoutNotify(RequiemSettings.Fullscreen);UpdateLabels();
        }
        void NormalizePauseViewport()
        {
            if(!pauseOwner||!pauseOwner.MenuRoot)return;
            var root=pauseOwner.MenuRoot.transform as RectTransform;if(!root)return;
            var canvas=root.GetComponent<Canvas>();bool automatic=canvas&&canvas.isRootCanvas&&canvas.renderMode!=RenderMode.WorldSpace;
            if(!automatic){root.localScale=Vector3.one;root.localRotation=Quaternion.identity;if(root.parent is RectTransform){root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;root.anchoredPosition3D=Vector3.zero;}}
            // The industrial shell supplies the backdrop; the old template panel must not cover it.
            var background=root.GetComponent<UnityEngine.UI.Image>();if(background&&root.Find("IndustrialShell"))background.enabled=false;
        }
        void FitLayout()
        {
            if(!home)return;var frame=home.transform.parent as RectTransform;var viewport=frame?frame.parent as RectTransform:null;
            if(!viewport||viewport.rect.width<=0||viewport.rect.height<=0)return;
            float scale=Mathf.Min(1,Mathf.Min(viewport.rect.width/1280f,viewport.rect.height/900f));
            frame.localScale=Vector3.one*Mathf.Max(.01f,scale);layoutViewportSize=viewport.rect.size;
        }
        void OnRectTransformDimensionsChange(){FitLayout();}
        public void Primary(){if(pauseOwner)pauseOwner.ClosePauseMenu();else if(LabFlow!=null)LabFlow.BeginGame();else BeginLaunch();}
        // 11/11/2026: the hangar intro plays its wake-up cinematic first when one is registered.
        bool launching;
        void BeginLaunch()
        {
            if(launching)return;launching=true;
            var sequence=MenuLaunchRegistry.Active;
            if(sequence!=null){foreach(var b in new[]{primary,optionsButton,creditsButton,quitButton})if(b)b.interactable=false;sequence.Play(LoadGame);}
            else LoadGame();
        }
        void LoadGame(){ReleaseSession();MainMenuLaunch.FromMenu(gameObject.scene.name,sceneLoader.SceneName);sceneLoader.LoadTargetScene();}
        public void ReturnToMenu(){if(pauseOwner)pauseOwner.ClosePauseMenu();if(LabFlow!=null){LabFlow.ReturnToMainMenu();return;}ReleaseSession();sceneLoader.LoadTargetScene();}
        public static void ReleaseSession(){Time.timeScale=1;AudioListener.pause=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        public void Quit()
        {
            ReleaseSession();
#if UNITY_EDITOR
            Debug.Log("SALIR: cierre de aplicación omitido en el Editor.");
#else
            Application.Quit();
#endif
        }
    }
}
