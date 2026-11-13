using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Options slider: screen shake 0–100 %, persisted through GameFeel.
    [RequireComponent(typeof(Slider))]
    public sealed class ShakeSliderBinding : MonoBehaviour
    {
        [SerializeField] TMP_Text value;
        Slider slider;
        public void Configure(TMP_Text label) => value = label;
        void Awake()
        {
            slider = GetComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1; slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(Changed);
        }
        void OnEnable() { slider.SetValueWithoutNotify(GameFeel.ShakeScale); Label(GameFeel.ShakeScale); }
        void OnDestroy() { if (slider) slider.onValueChanged.RemoveListener(Changed); }
        void Changed(float v) { GameFeel.ShakeScale = v; Label(v); }
        void Label(float v) { if (value) value.SetText("{0}%", Mathf.RoundToInt(v * 100)); }
    }
}
