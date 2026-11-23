using System;
using System.IO;
using System.Linq;
using System.Text;
using IronRequiem.Editor.Map01;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    /// Pase visual 12 · F2 · luminosidad por tramos (decision (a) aprobada: sin linterna). Los multiplicadores ya estan en los datos
    /// (Tools/IronRequiem/VisualPass12/apply_luminosity.py -> Levels/Map01/Data/Lighting/R*.json); esta clase crea los perfiles de exposicion,
    /// reconstruye SOLO la luz de los 18 cuartos y emite el informe. Llamable desde la cola visual-overhaul.txt:
    ///   VisualOverhaulLuminosity.CreateProfiles  ->  VisualOverhaulLuminosity.Apply  ->  VisualOverhaulLuminosity.BakeProbes  ->  VisualOverhaulLuminosity.Report
    /// Ninguna escena abierta con cambios sin guardar se toca: Apply / BakeProbes / Report se niegan y no guardan nada.
    public static class VisualOverhaulLuminosity
    {
        public const string ProfileFolder = "Assets/IronRequiem/Art/VisualPass12";
        /// Exposicion en EV de cada tramo (T1 = R01-R05, T2 = R06-R13, T3 = R14-R18). Misma tabla que apply_luminosity.py.
        static readonly float[] Exposure = { 0.4f, 0.4f, 0.6f }; // 22/11/2026: -0.2 EV on request (were 0.6 / 0.6 / 0.8)

        public static string ProfilePath(int tramo) => ProfileFolder + "/Map01_Exposure_T" + Mathf.Clamp(tramo, 1, 3) + ".asset";
        public static float ExposureOfTramo(int tramo) => Exposure[Mathf.Clamp(tramo, 1, 3) - 1];
        /// Sesion 1 (R01-R05) = tramo 1, sesion 2 (R06-R13) = tramo 2, sesiones 3 y 4 (R14-R18) = tramo 3.
        public static int TramoOfRoom(string roomId) { int s = Map01Paths.SessionOfRoom(roomId); return s <= 0 ? 1 : (s == 1 ? 2 : 3); }

        // ------------------------------------------------------------------ perfiles
        /// Crea (o actualiza) el perfil de exposicion del tramo: un ColorAdjustments con postExposure y overrideState = true,
        /// guardado como sub-asset del perfil (asi guarda URP los overrides). Idempotente: solo escribe si algo cambia.
        public static VolumeProfile EnsureProfile(int tramo)
        {
            tramo = Mathf.Clamp(tramo, 1, 3);
            if (!AssetDatabase.IsValidFolder(ProfileFolder)) AssetDatabase.CreateFolder("Assets/IronRequiem/Art", "VisualPass12");
            string path = ProfilePath(tramo);
            bool changed = false;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                changed = true;
            }
            if (!profile.TryGet(out ColorAdjustments ca)) { ca = profile.Add<ColorAdjustments>(false); changed = true; }
            float ev = ExposureOfTramo(tramo);
            if (!ca.postExposure.overrideState || Mathf.Abs(ca.postExposure.value - ev) > 1e-4f) { ca.postExposure.Override(ev); changed = true; }
            if (!AssetDatabase.Contains(ca)) { ca.name = nameof(ColorAdjustments); AssetDatabase.AddObjectToAsset(ca, profile); changed = true; }
            if (changed) { EditorUtility.SetDirty(ca); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets(); }
            return profile;
        }

        public static string CreateProfiles()
        {
            var sb = new StringBuilder("VisualOverhaulLuminosity.CreateProfiles\n");
            for (int t = 1; t <= 3; t++)
            {
                var p = EnsureProfile(t);
                bool ok = p && p.TryGet(out ColorAdjustments ca) && ca.postExposure.overrideState;
                sb.AppendLine("  T" + t + " " + ProfilePath(t) + " postExposure=" + ExposureOfTramo(t).ToString("0.0") + " EV " + (ok ? "OK" : "FALLO"));
            }
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        // ------------------------------------------------------------------ reconstruccion de la luz
        /// Reconstruye la capa [Lighting] de los 18 cuartos (luces, ambiente, niebla, probes y Volume de exposicion) con los JSON actuales.
        /// No reconstruye Structure, Doors, Encounters ni sesiones. Guarda cada escena de cuarto y restaura las escenas abiertas.
        public static string Apply()
        {
            string refusal = Refusal("Apply"); if (refusal != null) return refusal;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder("VisualOverhaulLuminosity.Apply\n");
            try
            {
                sb.Append(CreateProfiles());
                sb.Append(LightingAuthoring.BuildRoomsOnly());
                AssetDatabase.SaveAssets();
                sb.AppendLine("OK: 18 cuartos reconstruidos y guardados. Los reflection probes se han recreado sin horneado: ejecutar VisualOverhaulLuminosity.BakeProbes.");
            }
            catch (Exception e) { sb.AppendLine("ERROR: " + e); Debug.LogException(e); }
            finally { Restore(setup); }
            return sb.ToString();
        }

        /// Hornea los reflection probes de los 18 cuartos (menu Map01 > Lighting > Bake reflection probes). Hace falta tras Apply.
        public static string BakeProbes()
        {
            string refusal = Refusal("BakeProbes"); if (refusal != null) return refusal;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder("VisualOverhaulLuminosity.BakeProbes\n");
            try { LightingAuthoring.BakeProbesAll(); sb.AppendLine("OK: ver " + Map01Paths.ReportRoot + "/lighting_bake.txt"); }
            catch (Exception e) { sb.AppendLine("ERROR: " + e.Message); }
            finally { Restore(setup); }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ informe
        /// Por cuarto: tramo, exposicion, ambiente, niebla, luces encendidas, intensidad y alcance (de los datos) y el estado del perfil;
        /// despues ejecuta el validador existente (luces por cuarto y por objeto) y adjunta su resultado.
        public static string Report()
        {
            string refusal = Refusal("Report"); if (refusal != null) return refusal;
            var sb = new StringBuilder("VisualOverhaulLuminosity.Report\n");
            for (int t = 1; t <= 3; t++)
            {
                var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath(t));
                string state = !p ? "NO EXISTE " + ProfilePath(t)
                    : p.TryGet(out ColorAdjustments ca) ? "postExposure " + ca.postExposure.value.ToString("0.00") + (ca.postExposure.overrideState ? " (override)" : " (SIN override)")
                    : "sin ColorAdjustments";
                sb.AppendLine("perfil T" + t + ": " + state);
            }
            sb.AppendLine("room | tramo | EV json | EV perfil | ambiente sky / equator / ground | niebla | luces (encendidas) | intensidad min-max | alcance min-max");
            foreach (var id in Map01Paths.RoomIds)
            {
                try
                {
                    var d = Map01Json.Load<LightingAuthoring.RoomData>(Map01Paths.Data("Lighting", id));
                    var e = d.environment; var lit = d.lights.Where(l => l.enabled && l.intensity > 0.001f).ToList();
                    int tramo = d.tramo > 0 ? d.tramo : TramoOfRoom(id);
                    var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath(tramo));
                    string evProfile = prof && prof.TryGet(out ColorAdjustments c2) ? c2.postExposure.value.ToString("0.00") : "-";
                    sb.AppendLine(string.Format("{0} | T{1} | {2:0.00} | {3} | {4} / {5} / {6} | {7:0.0000} | {8} ({9}) | {10:0.#}-{11:0.#} | {12:0.#}-{13:0.#}",
                        id, tramo, d.exposureEV, evProfile, Rgb(e.sky), Rgb(e.equator), Rgb(e.ground), e.fogDensity, d.lights.Count, lit.Count,
                        lit.Count > 0 ? lit.Min(l => l.intensity) : 0, lit.Count > 0 ? lit.Max(l => l.intensity) : 0,
                        lit.Count > 0 ? lit.Min(l => l.range) : 0, lit.Count > 0 ? lit.Max(l => l.range) : 0));
                }
                catch (Exception ex) { sb.AppendLine(id + " | ERROR " + ex.Message); }
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                LightingAuthoring.ValidateAll(); // abre cada cuarto (Single); no guarda nada
                string path = Map01Paths.ReportRoot + "/lighting_validate.txt";
                sb.AppendLine("\n--- validador de iluminacion (" + path + ") ---");
                sb.AppendLine(File.Exists(path) ? File.ReadAllText(path) : "(sin informe)");
            }
            catch (Exception ex) { sb.AppendLine("Validador: ERROR " + ex.Message); }
            finally { Restore(setup); }
            return sb.ToString();
        }

        static string Rgb(float[] c) => c == null || c.Length < 3 ? "?" : "(" + c[0].ToString("0.00") + "," + c[1].ToString("0.00") + "," + c[2].ToString("0.00") + ")";

        // ------------------------------------------------------------------ escenas
        static string Refusal(string what)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "VisualOverhaulLuminosity." + what + ": RECHAZADO, salir de Play Mode.";
            var dirty = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isDirty || string.IsNullOrEmpty(s.path)).Select(s => string.IsNullOrEmpty(s.path) ? "(sin guardar)" : s.path).ToList();
            return dirty.Count == 0 ? null : "VisualOverhaulLuminosity." + what + ": RECHAZADO, escenas abiertas con cambios sin guardar o sin ruta: " + string.Join(", ", dirty) + ". No se guarda ni se descarta nada.";
        }

        static void Restore(SceneSetup[] setup)
        {
            try { if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
            catch (Exception e) { Debug.LogWarning("VisualOverhaulLuminosity: no se pudo restaurar la configuracion de escenas: " + e.Message); }
        }
    }
}
