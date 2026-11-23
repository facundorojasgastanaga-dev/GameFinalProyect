using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Cue = IronRequiem.HangarIntroSoundscape.Cue;
using Bed = IronRequiem.HangarIntroSoundscape.Bed;
using DuckWindow = IronRequiem.HangarIntroSoundscape.DuckWindow;
using Bus = IronRequiem.HangarIntroSoundscape.Bus;
using Layer = IronRequiem.HangarIntroSoundscape.Layer;

namespace IronRequiem.Editor.Map01
{
    /// Map01 · Hangar (18/11/2026). Construye la capa sonora de la cinemática de despertar de Room00_Hangar:
    /// una raíz nueva HG_CinematicSoundscape con HangarIntroSoundscape, sus camas (AudioSource en bucle) y emisores 3D
    /// colocados en la posición de la cápsula, los anclajes, la lámpara, la cinta y los montones de chatarra.
    /// No reparenta ni modifica ningún objeto existente: lee posiciones. Idempotente (borra y rehace solo su raíz) y con Undo.
    /// Configura la importación de los WAV de Audio/SFX/HangarIntro_v2 (generados por Tools/IronRequiem/Audio/synth_hangar_intro.py).
    /// Comprueba con un hash que la cinemática (WakeUpCinematic, su línea de tiempo y los objetos que observa) no cambia;
    /// si cambia o falta algo, deshace todo y no guarda. Informe: Tools/IronRequiem/Reports/Map01/Hangar/soundscape_build.txt.
    public static class HangarIntroAudioAuthoring
    {
        const string MenuPath = Map01Paths.MenuRoot + "Hangar/Build cinematic soundscape";
        const string ScenePath = HangarRouteAuthoring.ScenePath;
        const string ReportFolder = HangarRouteAuthoring.ReportFolder;
        public const string RootName = "HG_CinematicSoundscape";
        const string ClipFolder = "Assets/IronRequiem/Audio/SFX/HangarIntro_v2";
        const string OldClips = "Assets/IronRequiem/Audio/SFX/HangarIntro";
        const string V4Clips = "Assets/IronRequiem/Audio/SFX/V4";
        const string MixerPath = "Assets/FPS/Audio/MainAudioMixer.mixer";
        const string PlayablePath = "Assets/IronRequiem/Art/Levels/Campaign/Room00_WakeUp.playable";
        static readonly string[] NewClips =
        {
            "IR2_Amb_HangarTone_Loop", "IR2_Amb_Drip_Loop", "IR2_Amb_MachineFar_Loop", "IR2_Amb_ElecHum_Loop",
            "IR2_Robot_Boot", "IR2_Robot_Heart_Loop", "IR2_Robot_Blip", "IR2_Robot_Scan", "IR2_Robot_StepWeight",
            "IR2_Capsule_Steam", "IR2_Capsule_Clamp", "IR2_Capsule_Creak",
            "IR2_Impact_SubBoom", "IR2_Impact_PreSuck", "IR2_Impact_GlassDebris",
            "IR2_Reveal_Sting", "IR2_Trans_Whoosh", "IR2_Trans_HudOnline",
        };
        static readonly string[] Stereo2D = { "IR2_Amb_HangarTone_Loop", "IR2_Reveal_Sting", "IR2_Trans_Whoosh" };
        // objetos de la escena que la cinemática usa o que esta herramienta solo lee: deben quedar idénticos
        static readonly string[] WatchedUnderSector = { "Capsule_C04", "PodBase", "PodRim", "PodStrut", "OverheadLamp", "ConveyorPit", "ScrapPoints", "ScrapZones", "Bridge" };
        static readonly string[] WatchedRoots = { "WakeUpCinematic", "Scavengers", "RoomEncounter" };

        sealed class Ctx
        {
            public Scene scene; public Transform sector, cine, root;
            public HangarIntroCinematic cinematic; public float duration;
            public readonly StringBuilder log = new StringBuilder();
            public readonly List<string> warnings = new List<string>();
            public readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
            public AudioMixerGroup ambient, general, impact;
        }

        [MenuItem(MenuPath, priority = 12)]
        public static void BuildMenu()
        {
            try { Debug.Log("[Map01 Hangar] Build cinematic soundscape OK -> " + Build()); }
            catch (Exception e)
            {
                Debug.LogError("[Map01 Hangar] Build cinematic soundscape falló (no se guardó la escena): " + e);
                try { WriteReport("soundscape_build_error.txt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + e); } catch (Exception) { }
            }
        }

        /// Devuelve la ruta del informe. Lanza (tras deshacer los cambios de escena) si algo falla.
        public static string Build()
        {
            var c = new Ctx();
            c.log.AppendLine("Map01 Hangar · Build cinematic soundscape · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            GuardScenes();
            Backup(c);
            ConfigureClips(c);
            LoadReusedClips(c);
            LoadMixer(c);
            c.scene = OpenHangar(c.log);
            FindScene(c);
            string before = WatchedHash(c);
            c.log.AppendLine("hash cinemática ANTES   " + before);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build " + RootName);
            try
            {
                foreach (var old in c.scene.GetRootGameObjects().Where(g => g.name == RootName).ToArray())
                { c.log.AppendLine("reconstruye: borra " + RootName + " anterior"); Undo.DestroyObjectImmediate(old); }
                BuildRoot(c);
                string after = WatchedHash(c);
                c.log.AppendLine("hash cinemática DESPUÉS " + after + (after == before ? "  (iguales)" : "  (DISTINTOS)"));
                if (after != before) throw new InvalidOperationException("La cinemática o un objeto observado cambió durante la autoría (" + before + " -> " + after + ").");
                EditorSceneManager.MarkSceneDirty(c.scene);
                if (!EditorSceneManager.SaveScene(c.scene)) throw new InvalidOperationException("No se pudo guardar " + ScenePath);
                Undo.CollapseUndoOperations(group);
                c.log.AppendLine("guardado " + ScenePath);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                c.log.AppendLine("REVERTIDO (Undo); la escena no se guardó");
                try { WriteReport("soundscape_build.txt", c.log.ToString()); } catch (Exception) { }
                throw;
            }
            if (c.warnings.Count > 0) { c.log.AppendLine("AVISOS (" + c.warnings.Count + "):"); foreach (var w in c.warnings) c.log.AppendLine("  " + w); }
            c.log.AppendLine("pendiente: escuchar en Play Mode (Room00 desde JUGAR), con salto (E/Espacio mantenido) y tras morir (reinicio).");
            return WriteReport("soundscape_build.txt", c.log.ToString());
        }

        // ------------------------------------------------------------------------------------------------ escena
        static void GuardScenes()
        {
            Map01Scenes.GuardForeignDirtyScenes();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isDirty) throw new InvalidOperationException("La escena '" + s.path + "' tiene cambios sin guardar. Esta herramienta no guarda otras escenas ni descarta cambios: guárdala o ciérrala tú.");
            }
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Falta " + ScenePath);
        }

        static Scene OpenHangar(StringBuilder log)
        {
            var active = SceneManager.GetActiveScene();
            if (SceneManager.sceneCount == 1 && active.path == ScenePath) { log.AppendLine("escena ya abierta " + ScenePath); return active; }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            log.AppendLine("abierta " + ScenePath + " (Single)");
            return scene;
        }

        static void Backup(Ctx c)
        {
            var dir = "Tools/IronRequiem/Backups/" + DateTime.Now.ToString("yyyyMMdd") + "/hangar-soundscape-" + DateTime.Now.ToString("HHmmss");
            Directory.CreateDirectory(dir);
            var files = new List<string> { ScenePath };
            files.AddRange(NewClips.Select(n => ClipFolder + "/" + n + ".wav"));
            foreach (var f in files)
            {
                if (File.Exists(f + ".meta")) File.Copy(f + ".meta", Path.Combine(dir, Path.GetFileName(f) + ".meta"), true);
                if (f == ScenePath) File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
            }
            c.log.AppendLine("respaldo " + dir + " (escena + .meta de los WAV v2)");
        }

        static Transform Required(Transform t, string what) { if (!t) throw new InvalidOperationException("Room00_Hangar: falta '" + what + "'"); return t; }

        static void FindScene(Ctx c)
        {
            GameObject Root(string n) => c.scene.GetRootGameObjects().FirstOrDefault(g => g.name == n);
            var sector = Root("HangarSector"); c.sector = Required(sector ? sector.transform : null, "HangarSector");
            var cine = Root("WakeUpCinematic"); c.cine = Required(cine ? cine.transform : null, "WakeUpCinematic");
            c.cinematic = c.cine.GetComponent<HangarIntroCinematic>();
            if (!c.cinematic) throw new InvalidOperationException("WakeUpCinematic sin HangarIntroCinematic");
            var director = c.cine.GetComponentInChildren<PlayableDirector>(true);
            c.duration = director && director.playableAsset ? (float)director.playableAsset.duration : 0f;
            if (c.duration <= 0f) throw new InvalidOperationException("WakeUpCinematic: PlayableDirector sin línea de tiempo");
            if (director.playableAsset && AssetDatabase.GetAssetPath(director.playableAsset) != PlayablePath)
                c.warnings.Add("la línea de tiempo es " + AssetDatabase.GetAssetPath(director.playableAsset) + " (se esperaba " + PlayablePath + ")");
            Required(c.sector.Find("Capsule_C04"), "HangarSector/Capsule_C04");
            c.log.AppendLine("línea de tiempo " + PlayablePath + " · duración " + c.duration.ToString("F3", CultureInfo.InvariantCulture) + " s");
        }

        // ------------------------------------------------------------------------------------------------ clips
        static void ConfigureClips(Ctx c)
        {
            if (!AssetDatabase.IsValidFolder(ClipFolder))
            {
                if (!Directory.Exists(ClipFolder)) throw new DirectoryNotFoundException(ClipFolder + " no existe: ejecuta antes  python Tools/IronRequiem/Audio/synth_hangar_intro.py");
                AssetDatabase.ImportAsset(ClipFolder, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive);
            }
            c.log.AppendLine("---- importación " + ClipFolder + " (≤1.5 s: ADPCM + Decompress On Load · ≤12 s: Vorbis 0.70 + Compressed In Memory · >12 s: Vorbis 0.60 + Streaming en segundo plano · mono salvo las 3 capas 2D estéreo)");
            foreach (var name in NewClips)
            {
                string path = ClipFolder + "/" + name + ".wav";
                if (!File.Exists(path)) throw new FileNotFoundException(path + " no existe: ejecuta  python Tools/IronRequiem/Audio/synth_hangar_intro.py");
                if (!(AssetImporter.GetAtPath(path) is AudioImporter)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var imp = AssetImporter.GetAtPath(path) as AudioImporter;
                if (!imp) throw new InvalidOperationException("Sin AudioImporter: " + path);
                float seconds = WavSeconds(path, out int channels, out int rate);
                var s = imp.defaultSampleSettings;
                string beforeText = Describe(imp, s);
                bool mono = !Stereo2D.Contains(name);
                if (seconds <= 1.5f) { s.compressionFormat = AudioCompressionFormat.ADPCM; s.loadType = AudioClipLoadType.DecompressOnLoad; s.quality = 1f; s.preloadAudioData = true; }
                else if (seconds <= 12f) { s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = .7f; s.loadType = AudioClipLoadType.CompressedInMemory; s.preloadAudioData = true; }
                else { s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = .6f; s.loadType = AudioClipLoadType.Streaming; s.preloadAudioData = false; }
                s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                bool background = s.loadType == AudioClipLoadType.Streaming;
                bool changed = imp.forceToMono != mono || imp.loadInBackground != background || !Same(imp.defaultSampleSettings, s);
                if (changed)
                {
                    imp.forceToMono = mono; imp.loadInBackground = background; imp.defaultSampleSettings = s;
                    imp.SaveAndReimport();
                }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (!clip) throw new InvalidOperationException("No se pudo cargar " + path);
                c.clips[name] = clip;
                c.log.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-26} {1,5:F2} s {2} Hz {3} ch · antes [{4}] · {5}[{6}]",
                    name, seconds, rate, channels, beforeText, changed ? "ahora " : "sin cambios ", Describe(imp, imp.defaultSampleSettings)));
            }
        }

        static bool Same(AudioImporterSampleSettings a, AudioImporterSampleSettings b) =>
            a.compressionFormat == b.compressionFormat && a.loadType == b.loadType && Mathf.Approximately(a.quality, b.quality) &&
            a.preloadAudioData == b.preloadAudioData && a.sampleRateSetting == b.sampleRateSetting;

        static string Describe(AudioImporter imp, AudioImporterSampleSettings s) =>
            s.compressionFormat + " q" + s.quality.ToString("F2", CultureInfo.InvariantCulture) + " " + s.loadType + (imp.forceToMono ? " mono" : " estéreo") +
            (s.preloadAudioData ? " precarga" : "") + (imp.loadInBackground ? " fondo" : "");

        /// Duración leída de la cabecera del WAV (no depende del estado de importación).
        static float WavSeconds(string path, out int channels, out int rate)
        {
            using (var r = new BinaryReader(File.OpenRead(path)))
            {
                if (new string(r.ReadChars(4)) != "RIFF") throw new InvalidDataException(path + " no es RIFF");
                r.ReadInt32();
                if (new string(r.ReadChars(4)) != "WAVE") throw new InvalidDataException(path + " no es WAVE");
                channels = 0; rate = 0; int bits = 16;
                while (r.BaseStream.Position < r.BaseStream.Length - 8)
                {
                    string id = new string(r.ReadChars(4)); int size = r.ReadInt32();
                    if (id == "fmt ") { r.ReadInt16(); channels = r.ReadInt16(); rate = r.ReadInt32(); r.ReadInt32(); r.ReadInt16(); bits = r.ReadInt16(); r.BaseStream.Seek(size - 16, SeekOrigin.Current); }
                    else if (id == "data") return channels > 0 && rate > 0 ? size / (float)(channels * rate * (bits / 8)) : 0f;
                    else r.BaseStream.Seek(size + (size & 1), SeekOrigin.Current);
                }
                throw new InvalidDataException(path + " sin bloque data");
            }
        }

        static void LoadReusedClips(Ctx c)
        {
            foreach (var p in new[] { V4Clips + "/IR_Fist_HeavyImpact.wav", V4Clips + "/IR_Mount_Lock.wav", OldClips + "/IR_Hangar_Clatter.wav", OldClips + "/IR_Hangar_Tear.wav", OldClips + "/IR_Hangar_Rummage2.wav" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (!clip) throw new FileNotFoundException("Falta el clip reutilizado " + p);
                c.clips[Path.GetFileNameWithoutExtension(p)] = clip;
            }
            c.log.AppendLine("reutilizados (sin tocar su importación): IR_Fist_HeavyImpact, IR_Mount_Lock, IR_Hangar_Clatter, IR_Hangar_Tear, IR_Hangar_Rummage2");
        }

        static void LoadMixer(Ctx c)
        {
            var groups = AssetDatabase.LoadAllAssetsAtPath(MixerPath).OfType<AudioMixerGroup>().ToArray();
            AudioMixerGroup G(string n) => groups.FirstOrDefault(g => g.name == n) ?? throw new InvalidOperationException(MixerPath + ": falta el grupo " + n);
            c.ambient = G("Ambient"); c.general = G("General"); c.impact = G("Impact");
            c.log.AppendLine("mezclador " + MixerPath + " (sin modificar): camas -> Ambient · robot/cápsula/revelación/transición -> General · impactos y pisadas -> Impact");
        }

        static AudioClip Clip(Ctx c, string name) => c.clips.TryGetValue(name, out var clip) ? clip : throw new InvalidOperationException("clip no cargado " + name);

        // ------------------------------------------------------------------------------------------------ construcción
        static void BuildRoot(Ctx c)
        {
            var rootGo = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(rootGo, c.scene);
            c.root = rootGo.transform; c.root.SetPositionAndRotation(Vector3.zero, Quaternion.identity); c.root.localScale = Vector3.one;

            // ---- posiciones (solo lectura de la escena)
            var capsule = c.sector.Find("Capsule_C04");
            var glassAudio = capsule.Find("CanopyBreak/GlassAudio");
            Vector3 glassPos = glassAudio ? glassAudio.position : capsule.position + Vector3.up * .45f;
            if (!glassAudio) c.warnings.Add("sin Capsule_C04/CanopyBreak/GlassAudio: el emisor del cristal usa el centro de la cápsula");
            var lamp = c.sector.Find("OverheadLamp");
            Vector3 lampPos = lamp ? lamp.position : capsule.position + Vector3.up * 4f;
            if (!lamp) c.warnings.Add("sin HangarSector/OverheadLamp: el zumbido se coloca 4 m sobre la cápsula");
            var gantry = c.sector.Find("ConveyorPit/UnderBeltGantry") ?? c.sector.Find("ConveyorPit");
            Vector3 beltPos = gantry ? gantry.position : c.sector.TransformPoint(new Vector3(2f, 3f, 19f));
            if (!gantry) c.warnings.Add("sin ConveyorPit: la maquinaria usa un punto fijo del sector");
            var points = c.sector.Find("ScrapPoints");
            var stand = c.cine.Find("StandPoint");
            Vector3 standPos = stand ? stand.position : capsule.position;
            Vector3 scrapPos;
            if (points && points.childCount > 0) { scrapPos = Vector3.zero; foreach (Transform p in points) scrapPos += p.position; scrapPos /= points.childCount; scrapPos += Vector3.up; }
            else { scrapPos = standPos + Vector3.forward * -10f; c.warnings.Add("sin HangarSector/ScrapPoints: el acento de los Carroñeros usa un punto a 10 m del StandPoint"); }
            Vector3 dripPos = capsule.position + new Vector3(-4f, 2.5f, -3f);
            var piles = c.sector.Find("ScrapZones");
            if (piles)
            {
                var best = piles.Cast<Transform>().Where(t => t.name.StartsWith("ScrapPile", StringComparison.Ordinal))
                    .Select(t => new { t, d = Vector3.Distance(t.position, standPos) }).Where(x => x.d >= 4f).OrderBy(x => x.d).FirstOrDefault();
                if (best != null) dripPos = best.t.position + Vector3.up * 1.2f;
            }

            var eGlass = Emitter(c, "Emit_CapsuleGlass", glassPos);
            var eClamps = Emitter(c, "Emit_CapsuleClamps", capsule.position);
            var eScrap = Emitter(c, "Emit_ScavengerScrap", scrapPos);

            // ---- camas
            var beds = new List<Bed>
            {
                BedOf(c, "Bed_RoomTone", "IR2_Amb_HangarTone_Loop", c.ambient, null, 0, 0, Keys(0, 0, .6f, .08f, 3.6f, .42f, 18f, .42f, 21.5f, .4f, 25f, .32f, 27.8f, .2f), null, .3f, .1f, true),
                BedOf(c, "Bed_LampHum", "IR2_Amb_ElecHum_Loop", c.ambient, lampPos, 2f, 18f, Keys(0, .25f, .9f, .35f, 3.6f, .6f, 25f, .55f, 27.8f, .35f), null, .5f, .2f, true),
                BedOf(c, "Bed_Drip", "IR2_Amb_Drip_Loop", c.ambient, dripPos, 3f, 25f, Keys(0, 0, 1.5f, 0, 3.6f, .6f, 27.8f, .6f), null, .55f, .2f, true),
                BedOf(c, "Bed_Machinery", "IR2_Amb_MachineFar_Loop", c.ambient, beltPos, 8f, 60f, Keys(0, 0, 3f, 0, 6f, .55f, 18f, .7f, 21.5f, .6f, 27.8f, .45f), null, .6f, .2f, true),
                BedOf(c, "Bed_ReactorHeart", "IR2_Robot_Heart_Loop", c.general, null, 0, 0,
                    Keys(0, 0, 1f, 0, 2.6f, .32f, 4.9f, .42f, 5.45f, .12f, 6.3f, .45f, 13.8f, .5f, 14.5f, .62f, 18f, .45f, 21.7f, .45f, 23.2f, .75f, 24.6f, .6f, 25.6f, .3f, 26.6f, 0),
                    Keys(0, .9f, 5.6f, 1f, 14f, .96f, 21.7f, 1f, 23.2f, 1.15f, 26.6f, 1.12f), 0f, 0f, false),
            };

            // ---- cues (segundos de la línea de tiempo; ver Docs/IronRequiem/Map01/AUDIO_CINEMATICA_INICIO.md)
            var cues = new List<Cue>
            {
                // robot
                Q("R01_arranque", Layer.Robot, .85f, Clip(c, "IR2_Robot_Boot"), Bus.Cue, .75f),
                Q("R02_dedos_blip", Layer.Robot, 2.45f, Clip(c, "IR2_Robot_Blip"), Bus.Cue, .3f),
                Q("R03_dedos_blip", Layer.Robot, 3.2f, Clip(c, "IR2_Robot_Blip"), Bus.Cue, .26f, .84f),
                Q("R04_mano_blip", Layer.Robot, 14.15f, Clip(c, "IR2_Robot_Blip"), Bus.Cue, .35f),
                Q("R05_mano_escaneo", Layer.Robot, 14.5f, Clip(c, "IR2_Robot_Scan"), Bus.Cue, .5f),
                Q("R06_mano_escaneo2", Layer.Robot, 16.25f, Clip(c, "IR2_Robot_Scan"), Bus.Cue, .38f, 1.12f),
                Q("R07_mano_fin", Layer.Robot, 17.7f, Clip(c, "IR2_Robot_Blip"), Bus.Cue, .32f, 1.19f),
                // impactos
                Q("I01_puño_grieta", Layer.Impact, 4f, Clip(c, "IR_Fist_HeavyImpact"), Bus.Impact, .65f, .9f, .02f, eGlass, .7f, 2f, 30f),
                Q("I02_sub_grieta", Layer.Impact, 4f, Clip(c, "IR2_Impact_SubBoom"), Bus.Impact, .4f, 1.3f).Duck(-5f, .02f, .1f, .9f, false),
                Q("I03_succión", Layer.Impact, 4.98f, Clip(c, "IR2_Impact_PreSuck"), Bus.Impact, .75f),
                Q("I04_sub_estallido", Layer.Impact, 5.6f, Clip(c, "IR2_Impact_SubBoom"), Bus.Impact, 1f).Duck(-9f, .02f, .35f, 1.6f, true),
                Q("I05_puño_estallido", Layer.Impact, 5.6f, Clip(c, "IR_Fist_HeavyImpact"), Bus.Impact, .85f, .76f, .02f, eGlass, .7f, 2f, 30f),
                Q("I06_lluvia_cristal", Layer.Impact, 5.68f, Clip(c, "IR2_Impact_GlassDebris"), Bus.Impact, .85f, 1f, .03f, eGlass, .8f, 2.5f, 30f),
                Q("I07_chatarra_cae", Layer.Impact, 5.95f, Clip(c, "IR_Hangar_Clatter"), Bus.Impact, .45f, .9f, .04f, eGlass, .8f, 2.5f, 30f),
                // cápsula
                Q("C01_anclaje", Layer.Capsule, 6.3f, Clip(c, "IR2_Capsule_Clamp"), Bus.Cue, .9f, 1f, .02f, eClamps, .75f, 3f, 35f).Duck(-3f, .02f, .3f, .8f, false),
                Q("C02_cerrojo", Layer.Capsule, 6.3f, Clip(c, "IR_Mount_Lock"), Bus.Cue, .45f, .8f, 0f, eClamps, .75f, 3f, 35f),
                Q("C03_vapor_corto", Layer.Capsule, 6.3f, Clip(c, "IR2_Capsule_Steam"), Bus.Cue, .4f, 1.2f, 0f, eClamps, .75f, 3f, 35f),
                Q("C04_anclaje", Layer.Capsule, 6.46f, Clip(c, "IR2_Capsule_Clamp"), Bus.Cue, .75f, .92f, .02f, eClamps, .75f, 3f, 35f),
                Q("C05_cables", Layer.Capsule, 6.55f, Clip(c, "IR_Hangar_Tear"), Bus.Cue, .4f, 1.15f, .03f, eClamps, .75f, 3f, 35f),
                Q("C06_anclaje", Layer.Capsule, 6.62f, Clip(c, "IR2_Capsule_Clamp"), Bus.Cue, .62f, 1.06f, .02f, eClamps, .75f, 3f, 35f),
                Q("C07_cuna_cruje", Layer.Capsule, 6.9f, Clip(c, "IR2_Capsule_Creak"), Bus.Cue, .65f, 1f, .03f, eClamps, .75f, 3f, 35f),
                Q("C08_vapor", Layer.Capsule, 7.88f, Clip(c, "IR2_Capsule_Steam"), Bus.Cue, .9f, 1f, .02f, eClamps, .75f, 3f, 40f).Duck(-3f, .1f, 1.2f, 1f, false),
                Q("C09_cuna_tope", Layer.Capsule, 8.2f, Clip(c, "IR_Hangar_Clatter"), Bus.Cue, .5f, .7f, .03f, eClamps, .75f, 3f, 35f),
                Q("C10_cuna_peso", Layer.Impact, 8.2f, Clip(c, "IR2_Robot_StepWeight"), Bus.Impact, .6f, .8f, 0f, eClamps, .75f, 3f, 35f),
                // revelación de los Carroñeros
                Q("V01_acento", Layer.Reveal, 21.7f, Clip(c, "IR2_Reveal_Sting"), Bus.Cue, .8f).Duck(-6f, 1.2f, 1f, 2.5f, true),
                Q("V02_rebusque", Layer.Reveal, 22.95f, Clip(c, "IR_Hangar_Rummage2"), Bus.Cue, .9f, .95f, .04f, eScrap, .85f, 4f, 45f),
                Q("V03_fijar", Layer.Reveal, 23.2f, Clip(c, "IR2_Robot_Blip"), Bus.Cue, .4f, .75f),
                Q("V04_fijar", Layer.Reveal, 23.42f, Clip(c, "IR2_Robot_Blip"), Bus.Cue, .4f, .75f),
                Q("V05_arranque_chapa", Layer.Reveal, 23.65f, Clip(c, "IR_Hangar_Tear"), Bus.Cue, .7f, .85f, .04f, eScrap, .85f, 4f, 45f),
                // transición a primera persona
                Q("T01_entrada_cabeza", Layer.Transition, 25f, Clip(c, "IR2_Trans_Whoosh"), Bus.Cue, .85f).Duck(-8f, .8f, 1.8f, .4f, true),
            };
            var ducks = new[]
            {
                new DuckWindow { id = "D1_antes_grieta", start = 3.6f, attack = .3f, hold = .1f, release = .5f, depthDb = -6f, music = false },
                new DuckWindow { id = "D2_vacío_antes_estallido", start = 4.95f, attack = .5f, hold = .15f, release = 1.9f, depthDb = -14f, music = true },
                new DuckWindow { id = "D3_mano_intimo", start = 13.8f, attack = 1f, hold = 3f, release = 1.2f, depthDb = -4f, music = false },
            };
            foreach (var q in cues)
                if (q.time > c.duration + .01f) throw new InvalidOperationException("cue " + q.id + " a " + q.time + " s pasa de la duración " + c.duration);

            // zona del hangar en juego: los límites del horneado original del sector (HangarSectorAuthoring), algo más altos
            var zone = new Bounds(c.sector.TransformPoint(new Vector3(0, 2.5f, 3f)), new Vector3(34f, 10f, 56f));
            zone.Encapsulate(capsule.position);

            var comp = rootGo.AddComponent<HangarIntroSoundscape>();
            comp.Configure(c.cinematic, c.ambient, c.general, c.impact, beds.ToArray(), cues.OrderBy(q => q.time).ToArray(), ducks,
                Clip(c, "IR2_Robot_StepWeight"), Clip(c, "IR2_Trans_HudOnline"), zone);
            EditorUtility.SetDirty(comp);
            Undo.RegisterCreatedObjectUndo(rootGo, "Create " + RootName);

            // ---- informe de contenido
            c.log.AppendLine("---- " + RootName + " · emisores (posición mundo, solo leída de la escena)");
            foreach (Transform t in c.root) c.log.AppendLine("  " + t.name.PadRight(22) + F(t.position));
            c.log.AppendLine("---- camas (cinemática: curva · juego / combate)");
            foreach (var b in beds)
                c.log.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-18} {1,-26} {2} grupo {3,-8} juego {4:F2} combate {5:F2}{6} · claves {7}", b.id, b.source.clip.name,
                    b.source.spatialBlend > 0 ? "3D" : "2D", b.source.outputAudioMixerGroup.name, b.gameplayVolume, b.combatVolume, b.duckable ? " · ducking" : "",
                    string.Join(" ", b.cinematicGain.keys.Select(k => k.time.ToString("0.##", CultureInfo.InvariantCulture) + ":" + k.value.ToString("0.##", CultureInfo.InvariantCulture)))));
            c.log.AppendLine("---- cues (" + cues.Count + ")");
            foreach (var q in cues.OrderBy(q => q.time))
                c.log.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,6:F2} s  {1,-22} {2,-10} {3,-24} vol {4:F2} pitch {5:F2} {6}{7}", q.time, q.id, q.layer, q.clip.name, q.volume, q.pitch,
                    q.emitter ? "3D " + q.emitter.name : "2D", q.duckDb < 0 ? string.Format(CultureInfo.InvariantCulture, " · ducking {0} dB{1}", q.duckDb, q.duckMusic ? " +música" : "") : ""));
            c.log.AppendLine("---- ventanas de dinámica");
            foreach (var d in ducks) c.log.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-28} {1:F2} s  a {2:F2} h {3:F2} r {4:F2}  {5} dB{6}", d.id, d.start, d.attack, d.hold, d.release, d.depthDb, d.music ? " +música" : ""));
            c.log.AppendLine("zona del hangar (juego) centro " + F(zone.center) + " tamaño " + F(zone.size));
        }

        static Cue Q(string id, Layer layer, float time, AudioClip clip, Bus bus, float volume, float pitch = 1f, float jitter = 0f,
            Transform emitter = null, float spatial = 0f, float minDistance = 3f, float maxDistance = 40f) =>
            new Cue
            {
                id = id, layer = layer, time = time, clip = clip, bus = bus, volume = volume, pitch = pitch, pitchJitter = jitter,
                emitter = emitter, spatialBlend = emitter ? spatial : 0f, minDistance = minDistance, maxDistance = maxDistance, duckDb = 0f,
            };

        static Cue Duck(this Cue q, float db, float attack, float hold, float release, bool music)
        { q.duckDb = db; q.duckAttack = attack; q.duckHold = hold; q.duckRelease = release; q.duckMusic = music; return q; }

        static Transform Emitter(Ctx c, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(c.root, false);
            go.transform.position = position;
            return go.transform;
        }

        static Bed BedOf(Ctx c, string name, string clipName, AudioMixerGroup group, Vector3? position, float minDistance, float maxDistance,
            AnimationCurve gain, AnimationCurve pitch, float gameplay, float combat, bool duckable)
        {
            var go = new GameObject(name);
            go.transform.SetParent(c.root, false);
            if (position.HasValue) go.transform.position = position.Value;
            var s = go.AddComponent<AudioSource>();
            s.clip = Clip(c, clipName); s.outputAudioMixerGroup = group;
            s.playOnAwake = false; s.loop = true; s.volume = 0f; s.dopplerLevel = 0f; s.priority = 160;
            if (position.HasValue)
            {
                s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = minDistance; s.maxDistance = maxDistance;
                s.spread = name == "Bed_Machinery" ? 60f : 0f;
            }
            else s.spatialBlend = 0f;
            return new Bed { id = name, source = s, cinematicGain = gain, cinematicPitch = pitch ?? new AnimationCurve(), gameplayVolume = gameplay, combatVolume = combat, duckable = duckable };
        }

        /// Curva con pares (tiempo, valor) y tangentes ClampedAuto (sin rebasar los valores de las claves).
        static AnimationCurve Keys(params float[] tv)
        {
            var keys = new Keyframe[tv.Length / 2];
            for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(tv[2 * i], tv[2 * i + 1]);
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            return curve;
        }

        // ------------------------------------------------------------------------------------------------ hash y salida
        /// Hash de todo lo que la cinemática usa: jerarquía y datos serializados de cada componente de WakeUpCinematic,
        /// los objetos del sector que observa (cápsula, lámpara, cinta, chatarra), los Carroñeros y el archivo .playable.
        static string WatchedHash(Ctx c)
        {
            var sb = new StringBuilder();
            void Add(Transform t)
            {
                foreach (var x in t.GetComponentsInChildren<Transform>(true))
                {
                    sb.Append(PathOf(x)).Append('|').Append(F(x.localPosition, "F5")).Append(F(x.localEulerAngles, "F4")).Append(F(x.localScale, "F5")).Append(x.gameObject.activeSelf ? '1' : '0').Append('\n');
                    foreach (var comp in x.GetComponents<Component>())
                    {
                        if (!comp) { sb.Append("  <missing script>\n"); continue; }
                        if (comp is Transform) continue;
                        string json;
                        try { json = EditorJsonUtility.ToJson(comp); } catch (Exception e) { json = "<" + e.GetType().Name + ">"; }
                        sb.Append("  ").Append(comp.GetType().FullName).Append(' ').Append(json).Append('\n');
                    }
                }
            }
            foreach (var n in WatchedRoots)
            {
                var g = c.scene.GetRootGameObjects().FirstOrDefault(r => r.name == n);
                if (g) Add(g.transform); else sb.Append("missing root ").Append(n).Append('\n');
            }
            foreach (var n in WatchedUnderSector) { var t = c.sector.Find(n); if (t) Add(t); else sb.Append("missing ").Append(n).Append('\n'); }
            sb.Append("playable ").Append(File.Exists(PlayablePath) ? Sha(File.ReadAllBytes(PlayablePath)) : "missing").Append('\n');
            return Sha(Encoding.UTF8.GetBytes(sb.ToString())).Substring(0, 16);
        }

        static string Sha(byte[] data) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", ""); }

        static string PathOf(Transform t) { var parts = new List<string>(); for (var p = t; p; p = p.parent) parts.Add(p.name); parts.Reverse(); return string.Join("/", parts); }

        static string WriteReport(string name, string text)
        {
            Directory.CreateDirectory(ReportFolder);
            var p = ReportFolder + "/" + name; File.WriteAllText(p, text); return p;
        }

        static string F(Vector3 v, string fmt = "F2") => "(" + v.x.ToString(fmt, CultureInfo.InvariantCulture) + "; " + v.y.ToString(fmt, CultureInfo.InvariantCulture) + "; " + v.z.ToString(fmt, CultureInfo.InvariantCulture) + ")";
    }
}
