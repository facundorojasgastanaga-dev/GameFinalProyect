using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IronRequiem.Editor
{
    // 17/11/2026 (MAP_FX_AUDIT_20261117 E2): gameplay copy of the menu post profile for Room00_Hangar. The hangar's
    // HG_PostVolume keeps HG_MenuVolume in the scene (shared with IntroMenu); ZoneEnvironment swaps in this copy at runtime.
    // Invoked through the VisualOverhaulDriver queue: "IronCampaignMapFxHangar.CreateGameplayProfile". Idempotent.
    public static class IronCampaignMapFxHangar
    {
        public const string MenuProfile = "Assets/IronRequiem/Art/Menu/HG_MenuVolume.asset";
        public const string Folder = "Assets/IronRequiem/Resources/IronRequiem";
        public const string GameplayProfile = Folder + "/HG_GameplayVolume.asset";
        const float ChromaticIntensity = 0f, GrainIntensity = .1f, VignetteIntensity = .2f, BloomIntensity = .25f;

        public static string CreateGameplayProfile()
        {
            var log = new StringBuilder();
            if (!AssetDatabase.LoadAssetAtPath<VolumeProfile>(MenuProfile)) throw new System.IO.FileNotFoundException("Menu profile missing", MenuProfile);
            EnsureFolder(Folder);
            if (!AssetDatabase.LoadAssetAtPath<VolumeProfile>(GameplayProfile))
            {
                if (!AssetDatabase.CopyAsset(MenuProfile, GameplayProfile)) throw new System.InvalidOperationException("CopyAsset failed: " + MenuProfile + " -> " + GameplayProfile);
                log.AppendLine("created " + GameplayProfile + " from " + MenuProfile);
            }
            else log.AppendLine("exists " + GameplayProfile + " (values reapplied)");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GameplayProfile);
            if (profile.TryGet<ChromaticAberration>(out var ca)) ca.intensity.Override(ChromaticIntensity); else log.AppendLine("WARN no ChromaticAberration");
            if (profile.TryGet<FilmGrain>(out var grain)) grain.intensity.Override(GrainIntensity); else log.AppendLine("WARN no FilmGrain");
            if (profile.TryGet<Vignette>(out var vig)) vig.intensity.Override(VignetteIntensity); else log.AppendLine("WARN no Vignette");
            if (profile.TryGet<Bloom>(out var bloom)) bloom.intensity.Override(BloomIntensity); else log.AppendLine("WARN no Bloom");
            foreach (var c in profile.components) EditorUtility.SetDirty(c);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            log.AppendLine("guid=" + AssetDatabase.AssetPathToGUID(GameplayProfile));
            if (ca) log.AppendLine("ChromaticAberration intensity=" + ca.intensity.value.ToString("F2") + " override=" + ca.intensity.overrideState);
            if (grain) log.AppendLine("FilmGrain intensity=" + grain.intensity.value.ToString("F2") + " type=" + grain.type.value + " response=" + grain.response.value.ToString("F2"));
            if (vig) log.AppendLine("Vignette intensity=" + vig.intensity.value.ToString("F2") + " smoothness=" + vig.smoothness.value.ToString("F2"));
            if (bloom) log.AppendLine("Bloom intensity=" + bloom.intensity.value.ToString("F2") + " threshold=" + bloom.threshold.value.ToString("F2") + " scatter=" + bloom.scatter.value.ToString("F2") + " clamp=" + bloom.clamp.value.ToString("F1"));
            log.Append("components=" + profile.components.Count + " runtime load: Resources.Load<VolumeProfile>(\"IronRequiem/HG_GameplayVolume\")");
            return log.ToString();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
