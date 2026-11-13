using TMPro;
using UnityEngine;
using UnityEngine.Audio;

namespace IronRequiem
{
    /// Shared look of every Iron Requiem screen (menus, pause, death, HUD, prompts): palette, typography, plate
    /// sprites and interface sounds. One asset (Assets/IronRequiem/Settings/UI/IR_UiTheme.asset) is referenced by the
    /// button effect and applied to the screens by the UI style authoring pass. Presentation only.
    [CreateAssetMenu(menuName = "Iron Requiem/UI Theme", fileName = "IR_UiTheme")]
    public sealed class IronRequiemUiTheme : ScriptableObject
    {
        [Header("Palette")]
        public Color Cyan = new Color(.12f, .82f, .91f);
        public Color Orange = new Color(1f, .6f, .2f);
        public Color Steel = new Color(.82f, .86f, .9f);
        public Color Text = new Color(.88f, .94f, .94f);
        public Color TextDim = new Color(.56f, .66f, .69f);
        public Color Danger = new Color(1f, .3f, .2f);
        public Color Backdrop = new Color(.008f, .016f, .024f, .88f);
        public Color PanelFill = new Color(.03f, .05f, .065f, .94f);
        public Color PanelRim = new Color(.1f, .42f, .48f, 1f);

        [Header("Buttons")]
        public Color ButtonRestFill = new Color(.02f, .04f, .05f, .78f);
        public Color ButtonRestRim = new Color(.1f, .28f, .32f, .9f);
        public Color ButtonHotFill = new Color(.09f, .04f, .012f, .94f);
        public Color ButtonHotRim = new Color(1f, .55f, .14f, 1f);
        public Color ButtonRestText = new Color(.78f, .86f, .88f);
        public Color ButtonHotText = new Color(1f, .9f, .78f);
        [Min(0)] public float ButtonCorner = 14;
        [Min(0)] public float ButtonPressScale = .97f;

        [Header("Typography")]
        public TMP_FontAsset TitleFont;
        public TMP_FontAsset BodyFont;
        [Min(0)] public float TitleSpacing = 6;
        [Min(0)] public float BodySpacing = 3;

        [Header("Sprites")]
        public Sprite Plate;
        public Sprite Hazard;
        public Sprite Lamp;

        [Header("Sounds (play while paused)")]
        public AudioClip Hover;
        public AudioClip Click;
        public AudioClip Back;
        public AudioMixerGroup Output;
        [Range(0, 1)] public float HoverVolume = .25f;
        [Range(0, 1)] public float ClickVolume = .55f;

        static AudioSource source;
        /// One shared 2D source; ignores the listener pause so menus sound while the game is paused.
        public void Play(AudioClip clip, float volume)
        {
            if (!clip || !Application.isPlaying) return;
            if (!source)
            {
                var go = new GameObject("IR_UiAudio") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0; source.ignoreListenerPause = true;
            }
            source.outputAudioMixerGroup = Output;
            source.PlayOneShot(clip, volume);
        }
    }
}
