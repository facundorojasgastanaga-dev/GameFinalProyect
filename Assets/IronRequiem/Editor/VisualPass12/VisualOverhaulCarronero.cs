using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Editor
{
    /// Pase visual 12 · F5 · animación de combate del Carroñero (01e_CARRONERO.md, nivel Mínimo, solo Map01).
    /// Llamable desde la cola visual-overhaul.txt:
    ///   VisualOverhaulCarronero.Apply   añade CarroneroCombatMotion a Carronero_Map01.prefab, le asigna los huesos y desactiva
    ///                                   CarroneroMotion SOLO en esa variante. Idempotente.
    ///   VisualOverhaulCarronero.Report  lista componentes, huesos, niveles de LOD y las guardas de zona protegida. No escribe assets.
    ///   VisualOverhaulCarronero.Revert  deshace exactamente lo que hizo Apply.
    ///   VisualOverhaulCarronero.Tune    "ikMaxLod=0 halfRateFromLod=1": ajusta campos numéricos del componente en la variante.
    /// Nunca toca Carronero.prefab ni CarroneroMotion.cs: comprueba su SHA-256 antes y después y contra el respaldo.
    public static class VisualOverhaulCarronero
    {
        public const string Variant = "Assets/IronRequiem/Prefabs/Map01/Enemies/Carronero_Map01.prefab";
        public const string Base = "Assets/IronRequiem/Prefabs/Campaign/Carronero.prefab";
        const string LegacyScript = "Assets/IronRequiem/Scripts/Enemies/CarroneroMotion.cs";
        const string NewScript = "Assets/IronRequiem/Scripts/Enemies/CarroneroCombatMotion.cs";
        const string Backup = "Tools/IronRequiem/Backups/20261121-visualpass-f2f5/";
        const string Reports = "Tools/IronRequiem/Reports/VisualPassFable/F5/";
        const string Map01Scenes = "Assets/IronRequiem/Scenes/Map01_Bunker";
        static readonly string[] ProtectedScenes = { "Assets/FPS/Scenes/IntroMenu.unity", "Assets/IronRequiem/Scenes/Campaign/Room00_Hangar.unity" };

        /// Bones CarroneroMotion already references (same field name in both components).
        static readonly string[] SharedBones = { "hips", "spine", "leftUpLeg", "leftLeg", "rightUpLeg", "rightLeg", "sawArm", "sawForeArm", "otherArm", "head", "saw" };
        /// Extra bones of the Mínimo level, by rig name (CarroneroRigInfo.bones).
        static readonly (string field, string bone)[] ExtraBones = { ("spine2", "Spine2"), ("neck", "Neck"), ("leftFoot", "LeftFoot"), ("rightFoot", "RightFoot") };
        /// Contact pose and saw speed: copied so the blade is exactly where CarroneroMotion put it while sawing.
        static readonly string[] SharedFloats = { "attackRaise", "attackSaw", "idleSpin", "attackSpin" };

        [MenuItem("Tools/Iron Requiem/Visual Pass 12/Carronero/Apply combat motion (Map01 variant)")] static void MenuApply() => Debug.Log(Apply());
        [MenuItem("Tools/Iron Requiem/Visual Pass 12/Carronero/Report")] static void MenuReport() => Debug.Log(Report());
        [MenuItem("Tools/Iron Requiem/Visual Pass 12/Carronero/Revert combat motion")] static void MenuRevert() => Debug.Log(Revert());

        // ------------------------------------------------------------------ Apply
        public static string Apply()
        {
            var log = new StringBuilder("VisualOverhaulCarronero.Apply " + DateTime.Now.ToString("s") + "\n");
            CheckVariant();
            string baseBefore = Sha(Base), legacyBefore = Sha(LegacyScript);
            EnsureBackup(log);
            bool changed = false;
            var root = PrefabUtility.LoadPrefabContents(Variant);
            try
            {
                var legacy = root.GetComponent<CarroneroMotion>();
                if (!legacy) throw new InvalidOperationException(Variant + ": no tiene CarroneroMotion en la raíz.");
                var info = root.GetComponentInChildren<CarroneroRigInfo>(true);
                if (!info) throw new InvalidOperationException(Variant + ": no tiene CarroneroRigInfo.");
                var motion = root.GetComponent<CarroneroCombatMotion>();
                if (!motion) { motion = root.AddComponent<CarroneroCombatMotion>(); changed = true; log.AppendLine("añadido CarroneroCombatMotion"); }

                var src = new SerializedObject(legacy); var dst = new SerializedObject(motion);
                foreach (var name in SharedBones)
                {
                    var value = Prop(src, name).objectReferenceValue;
                    if (!value) log.AppendLine("AVISO: CarroneroMotion." + name + " está vacío");
                    changed |= SetRef(dst, name, value);
                }
                foreach (var (field, bone) in ExtraBones)
                {
                    var value = info.Bone(bone);
                    if (!value) log.AppendLine("AVISO: el rig no tiene el hueso " + bone + " (el componente baja de nivel solo: sin " + field + ")");
                    changed |= SetRef(dst, field, value);
                }
                foreach (var name in SharedFloats)
                {
                    var from = Prop(src, name); var to = Prop(dst, name);
                    if (!Mathf.Approximately(to.floatValue, from.floatValue)) { to.floatValue = from.floatValue; changed = true; }
                }
                var axisFrom = Prop(src, "sawAxisLocal"); var axisTo = Prop(dst, "sawAxisLocal");
                if (axisTo.vector3Value != axisFrom.vector3Value) { axisTo.vector3Value = axisFrom.vector3Value; changed = true; }

                var views = LodViews(root, info, log);
                var list = Prop(dst, "lodViews");
                bool same = list.arraySize == views.Length;
                for (int i = 0; same && i < views.Length; i++) same = list.GetArrayElementAtIndex(i).objectReferenceValue == views[i];
                if (!same)
                {
                    list.arraySize = views.Length;
                    for (int i = 0; i < views.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
                    changed = true;
                }
                dst.ApplyModifiedPropertiesWithoutUndo();

                if (legacy.enabled) { legacy.enabled = false; changed = true; log.AppendLine("CarroneroMotion desactivado en la variante (sigue en el objeto: su Awake reparte la prioridad de evasión)"); }
                if (!motion.enabled) { motion.enabled = true; changed = true; }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, Variant, out bool saved);
                    if (!saved) throw new InvalidOperationException("No se pudo guardar " + Variant);
                }
                log.AppendLine(changed ? "GUARDADO " + Variant : "sin cambios: la variante ya estaba aplicada");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            CheckVariant();
            Protected(log, baseBefore, legacyBefore);
            log.AppendLine().Append(Describe());
            return Write("carronero-apply.txt", log);
        }

        // ------------------------------------------------------------------ Revert
        public static string Revert()
        {
            var log = new StringBuilder("VisualOverhaulCarronero.Revert " + DateTime.Now.ToString("s") + "\n");
            CheckVariant();
            string baseBefore = Sha(Base), legacyBefore = Sha(LegacyScript);
            bool changed = false;
            var root = PrefabUtility.LoadPrefabContents(Variant);
            try
            {
                foreach (var motion in root.GetComponents<CarroneroCombatMotion>())
                {
                    UnityEngine.Object.DestroyImmediate(motion); changed = true; log.AppendLine("quitado CarroneroCombatMotion");
                }
                var legacy = root.GetComponent<CarroneroMotion>();
                if (legacy)
                {
                    var so = new SerializedObject(legacy); var enabled = so.FindProperty("m_Enabled");
                    if (enabled != null && enabled.prefabOverride)
                    {
                        PrefabUtility.RevertPropertyOverride(enabled, InteractionMode.AutomatedAction);
                        changed = true; log.AppendLine("revertida la sobrescritura m_Enabled de CarroneroMotion");
                    }
                    if (!legacy.enabled) { legacy.enabled = true; changed = true; log.AppendLine("CarroneroMotion activado de nuevo"); }
                }
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, Variant, out bool saved);
                    if (!saved) throw new InvalidOperationException("No se pudo guardar " + Variant);
                }
                log.AppendLine(changed ? "GUARDADO " + Variant : "sin cambios: la variante no tenía el componente");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            CheckVariant();
            Protected(log, baseBefore, legacyBefore);
            string backup = Backup + Variant;
            if (File.Exists(backup)) log.AppendLine("variante " + (Sha(backup) == Sha(Variant) ? "idéntica byte a byte al respaldo" : "equivalente pero no idéntica byte a byte al respaldo (Unity reordena el YAML al guardar)"));
            log.AppendLine().Append(Describe());
            return Write("carronero-revert.txt", log);
        }

        // ------------------------------------------------------------------ Report
        public static string Report()
        {
            var log = new StringBuilder("VisualOverhaulCarronero.Report " + DateTime.Now.ToString("s") + "\n");
            log.Append(Describe());
            Protected(log, Sha(Base), Sha(LegacyScript));
            return Write("carronero-report.txt", log);
        }

        // ------------------------------------------------------------------ Tune
        /// "campo=valor campo=valor": solo campos int o float del componente (por ejemplo ikMaxLod, fineMaxLod, halfRateFromLod, stepHeight).
        public static string Tune(string args)
        {
            var log = new StringBuilder("VisualOverhaulCarronero.Tune " + args + "\n");
            CheckVariant();
            var pairs = (args ?? "").Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (pairs.Length == 0) throw new ArgumentException("Uso: Tune campo=valor [campo=valor ...]");
            var root = PrefabUtility.LoadPrefabContents(Variant);
            try
            {
                var motion = root.GetComponent<CarroneroCombatMotion>();
                if (!motion) throw new InvalidOperationException("Ejecuta primero VisualOverhaulCarronero.Apply");
                var so = new SerializedObject(motion);
                foreach (var pair in pairs)
                {
                    int eq = pair.IndexOf('='); if (eq <= 0) throw new ArgumentException("Se esperaba campo=valor: " + pair);
                    string name = pair.Substring(0, eq), text = pair.Substring(eq + 1);
                    var p = Prop(so, name);
                    if (p.propertyType == SerializedPropertyType.Integer) { log.AppendLine(name + ": " + p.intValue + " -> " + text); p.intValue = int.Parse(text, CultureInfo.InvariantCulture); }
                    else if (p.propertyType == SerializedPropertyType.Float) { log.AppendLine(name + ": " + F(p.floatValue) + " -> " + text); p.floatValue = float.Parse(text, CultureInfo.InvariantCulture); }
                    else throw new ArgumentException(name + " no es int ni float (" + p.propertyType + ")");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, Variant, out bool saved);
                if (!saved) throw new InvalidOperationException("No se pudo guardar " + Variant);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return Write("carronero-tune.txt", log);
        }

        // ------------------------------------------------------------------ helpers
        static void CheckVariant()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Base);
            if (!source) throw new FileNotFoundException("Prefab base ausente: " + Base);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Variant);
            if (!asset) throw new FileNotFoundException("Variante ausente: " + Variant + " (la crea Map01 > Encounters > Create enemy variants)");
            if (PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Variant || PrefabUtility.GetCorrespondingObjectFromSource(asset) != source)
                throw new InvalidOperationException(Variant + " no es una variante directa de " + Base);
        }

        /// The coordinator already copied the files; this only fills the gap if the variant copy is missing. Never overwrites.
        static void EnsureBackup(StringBuilder log)
        {
            foreach (var file in new[] { Variant, Variant + ".meta" })
            {
                string target = Backup + file;
                if (File.Exists(target) || !File.Exists(file)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, false);
                log.AppendLine("respaldo creado: " + target);
            }
        }

        /// One SkinnedMeshRenderer per LOD level of the Carroñero visual (index = LOD), read from its LODGroup.
        static SkinnedMeshRenderer[] LodViews(GameObject root, CarroneroRigInfo info, StringBuilder log)
        {
            var group = info.GetComponent<LODGroup>();
            if (!group || !group.enabled)
                group = root.GetComponentsInChildren<LODGroup>(true).FirstOrDefault(g => g.enabled && g.GetLODs().Any(l => l.renderers.Any(r => r is SkinnedMeshRenderer)));
            if (!group)
            {
                log.AppendLine("AVISO: sin LODGroup con mallas con piel; el componente tratará toda malla visible como LOD0");
                return root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.enabled).ToArray();
            }
            var lods = group.GetLODs(); var views = new SkinnedMeshRenderer[lods.Length];
            for (int i = 0; i < lods.Length; i++)
            {
                views[i] = lods[i].renderers.OfType<SkinnedMeshRenderer>().FirstOrDefault(r => r);
                if (!views[i]) log.AppendLine("AVISO: el LOD" + i + " no tiene SkinnedMeshRenderer");
            }
            return views;
        }

        static string Describe()
        {
            var log = new StringBuilder();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Base); var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Variant);
            if (!source || !asset) { log.AppendLine("falta el prefab base o la variante"); return log.ToString(); }
            bool isVariant = PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant && PrefabUtility.GetCorrespondingObjectFromSource(asset) == source;
            log.AppendLine("variante directa del prefab base: " + (isVariant ? "sí" : "NO"));

            log.AppendLine("[" + Variant + "] componentes de la raíz:");
            foreach (var c in asset.GetComponents<Component>())
            {
                if (!c) { log.AppendLine("  (script ausente)"); continue; }
                string state = c is Behaviour b ? (b.enabled ? "activo" : "DESACTIVADO") : "-";
                string origin = PrefabUtility.IsAddedComponentOverride(c) ? " [añadido en la variante]" : "";
                log.AppendLine("  " + c.GetType().Name + " " + state + origin);
            }
            var motion = asset.GetComponent<CarroneroCombatMotion>(); var legacy = asset.GetComponent<CarroneroMotion>();
            log.AppendLine("CarroneroMotion en la variante: " + (legacy ? (legacy.enabled ? "ACTIVO" : "desactivado") : "ausente"));
            if (!motion) log.AppendLine("CarroneroCombatMotion: ausente (Apply no ejecutado o revertido)");
            else
            {
                var so = new SerializedObject(motion);
                log.AppendLine("CarroneroCombatMotion: " + (motion.enabled ? "activo" : "DESACTIVADO") + ". Huesos:");
                foreach (var name in SharedBones.Concat(ExtraBones.Select(e => e.field)))
                {
                    var value = Prop(so, name).objectReferenceValue as Transform;
                    log.AppendLine("  " + name.PadRight(11) + " = " + (value ? PathOf(value, asset.transform) : "NULO"));
                }
                var list = Prop(so, "lodViews");
                for (int i = 0; i < list.arraySize; i++)
                {
                    var r = list.GetArrayElementAtIndex(i).objectReferenceValue as SkinnedMeshRenderer;
                    log.AppendLine("  LOD" + i + " = " + (r ? r.name + " (" + (r.sharedMesh ? r.sharedMesh.vertexCount + " v" : "sin malla") + ")" : "NULO"));
                }
                log.AppendLine("  ajustes: ikMaxLod=" + Prop(so, "ikMaxLod").intValue + " fineMaxLod=" + Prop(so, "fineMaxLod").intValue + " legsOnlyFromLod=" + Prop(so, "legsOnlyFromLod").intValue + " halfRateFromLod=" + Prop(so, "halfRateFromLod").intValue
                    + " reenterBlend=" + F(Prop(so, "reenterBlend").floatValue) + " stepHeight=" + F(Prop(so, "stepHeight").floatValue)
                    + " attackRaise=" + F(Prop(so, "attackRaise").floatValue) + " attackSaw=" + F(Prop(so, "attackSaw").floatValue) + " sawAxisLocal=" + Prop(so, "sawAxisLocal").vector3Value.ToString("F3"));
            }
            var combat = asset.GetComponent<SawBotCombat>();
            if (combat) log.AppendLine("SawBotCombat (solo lectura): preparation=" + F(combat.Preparation) + " reach=" + F(combat.Reach) + " damagePerSecond=" + F(combat.DamagePerSecond) + " damageInterval=" + F(combat.DamageInterval) + " moveSpeed=" + F(combat.MovementSpeed));

            var baseLegacy = source.GetComponent<CarroneroMotion>();
            log.AppendLine("[" + Base + "] CarroneroCombatMotion: " + (source.GetComponent<CarroneroCombatMotion>() ? "PRESENTE (no debería)" : "ausente") + "; CarroneroMotion: " + (baseLegacy ? (baseLegacy.enabled ? "activo" : "DESACTIVADO (no debería)") : "ausente"));
            return log.ToString();
        }

        /// Protected-zone guards: base prefab and CarroneroMotion.cs untouched, protected scenes free of the new component,
        /// and which Carroñero prefab the Map01 rooms really reference.
        static void Protected(StringBuilder log, string baseBefore, string legacyBefore)
        {
            log.AppendLine("zona protegida:");
            string baseNow = Sha(Base), legacyNow = Sha(LegacyScript);
            log.AppendLine("  " + Base + " sha256 " + baseNow + (baseNow == baseBefore ? " (sin cambios en esta operación)" : " CAMBIÓ EN ESTA OPERACIÓN") + BackupNote(Base, baseNow));
            log.AppendLine("  " + LegacyScript + " sha256 " + legacyNow + (legacyNow == legacyBefore ? " (sin cambios en esta operación)" : " CAMBIÓ EN ESTA OPERACIÓN") + BackupNote(LegacyScript, legacyNow));
            if (baseNow != baseBefore || legacyNow != legacyBefore) throw new InvalidOperationException("Un archivo protegido cambió durante la operación:\n" + log);

            string newGuid = AssetDatabase.AssetPathToGUID(NewScript), variantGuid = AssetDatabase.AssetPathToGUID(Variant), baseGuid = AssetDatabase.AssetPathToGUID(Base);
            foreach (var scene in ProtectedScenes)
            {
                if (!File.Exists(scene)) { log.AppendLine("  " + scene + ": no existe"); continue; }
                string text = File.ReadAllText(scene);
                bool script = newGuid.Length > 0 && text.Contains(newGuid), variant = variantGuid.Length > 0 && text.Contains(variantGuid);
                log.AppendLine("  " + scene + ": " + (script || variant ? "REFERENCIA EL COMPONENTE NUEVO O LA VARIANTE (no debería)" : "no referencia el componente nuevo ni la variante"));
            }
            if (Directory.Exists(Map01Scenes))
            {
                int withVariant = 0, withBase = 0; var baseUsers = new StringBuilder();
                foreach (var scene in Directory.GetFiles(Map01Scenes, "Map01_R*.unity"))
                {
                    string text = File.ReadAllText(scene);
                    if (variantGuid.Length > 0 && text.Contains(variantGuid)) withVariant++;
                    if (baseGuid.Length > 0 && text.Contains(baseGuid)) { withBase++; baseUsers.Append(' ').Append(Path.GetFileNameWithoutExtension(scene)); }
                }
                log.AppendLine("  cuartos de Map01 que referencian la variante: " + withVariant + "; que referencian el prefab base: " + withBase + (withBase > 0 ? " (NO se cambian; anotados:" + baseUsers + ")" : ""));
            }
        }

        static string BackupNote(string path, string current)
        {
            string backup = Backup + path;
            return !File.Exists(backup) ? "; sin copia en el respaldo" : Sha(backup) == current ? "; igual que el respaldo" : "; DISTINTO DEL RESPALDO";
        }

        static SerializedProperty Prop(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            if (p == null) throw new MissingFieldException(so.targetObject.GetType().Name, name);
            return p;
        }

        static bool SetRef(SerializedObject so, string name, UnityEngine.Object value)
        {
            var p = Prop(so, name);
            if (p.objectReferenceValue == value) return false;
            p.objectReferenceValue = value; return true;
        }

        static string PathOf(Transform t, Transform root)
        {
            string path = t.name;
            while (t.parent && t.parent != root) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }

        static string Sha(string path)
        {
            if (!File.Exists(path)) return "(no existe)";
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(File.ReadAllBytes(path)).Select(b => b.ToString("x2")));
        }

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        static string Write(string file, StringBuilder log)
        {
            string text = log.ToString();
            try { Directory.CreateDirectory(Reports); File.WriteAllText(Reports + file, text); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return text;
        }
    }
}
