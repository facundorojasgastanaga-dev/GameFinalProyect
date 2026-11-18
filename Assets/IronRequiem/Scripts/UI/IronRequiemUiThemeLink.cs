using UnityEngine;

namespace IronRequiem
{
    /// Runtime pointer to the shared UI theme. The theme asset itself lives in Assets/IronRequiem/Settings/UI and is not a
    /// Resources asset; this small link (Resources/IronRequiem/HudThemeLink.asset) lets runtime-built HUDs reach it.
    [CreateAssetMenu(menuName = "Iron Requiem/UI Theme Link", fileName = "HudThemeLink")]
    public sealed class IronRequiemUiThemeLink : ScriptableObject
    {
        public const string ResourcePath = "IronRequiem/HudThemeLink";
        [SerializeField] IronRequiemUiTheme theme;
        public IronRequiemUiTheme Theme => theme;

        /// Theme referenced by the Resources link, or null when the link (or its theme) is missing.
        public static IronRequiemUiTheme Load()
        {
            var link = Resources.Load<IronRequiemUiThemeLink>(ResourcePath);
            return link ? link.theme : null;
        }
    }
}
