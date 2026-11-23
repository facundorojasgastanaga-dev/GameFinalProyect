using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace IronRequiem.EditorTools
{
    /// 19/11/2026, user request «quita ese sonido como un chirrido robótico»: removes every IR_Hangar_Servo clip from the
    /// wake-up cinematic's "Sfx" audio track (it fired 9 times, the loudest clip of the scene, on top of the glass punches
    /// and while the robot barely moves). Every other clip on that track (power-up, hydraulics, spark) is kept.
    /// Backup of the .playable is taken by the coordinator before running; the result is logged.
    public static class ArmsFixPassAudio
    {
        const string Timeline = "Assets/IronRequiem/Art/Levels/Campaign/Room00_WakeUp.playable";
        const string Servo = "Assets/IronRequiem/Audio/SFX/HangarIntro/IR_Hangar_Servo.wav";
        const string Report = "Tools/IronRequiem/Reports/ArmsFixPass/intro-servo-removal.txt";

        [MenuItem("Tools/Iron Requiem/Arms Fix Pass/Intro audio: remove servo screech from wake-up timeline")]
        static void RemoveServo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[ArmsFix] stop Play Mode first"); return; }
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(Timeline);
            var servo = AssetDatabase.LoadAssetAtPath<AudioClip>(Servo);
            var log = new StringBuilder($"[intro servo removal] {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
            if (!timeline || !servo) { log.AppendLine("missing timeline or servo clip"); Write(log); return; }
            int removed = 0, kept = 0;
            foreach (var track in timeline.GetOutputTracks().OfType<AudioTrack>())
            {
                foreach (var clip in track.GetClips().ToArray())
                {
                    if (clip.asset is AudioPlayableAsset a && a.clip == servo)
                    {
                        log.AppendLine($"  removed {track.name}: servo at {clip.start:0.00} s ({clip.duration:0.00} s)");
                        Undo.RecordObject(track, "Remove servo clip");
                        if (track.DeleteClip(clip)) removed++;
                    }
                    else kept++;
                }
            }
            EditorUtility.SetDirty(timeline); AssetDatabase.SaveAssets();
            int left = timeline.GetOutputTracks().OfType<AudioTrack>().SelectMany(t => t.GetClips()).Count(c => c.asset is AudioPlayableAsset p && p.clip == servo);
            log.AppendLine($"removed={removed} keptOtherAudioClips={kept} servoLeft={left}");
            Write(log);
        }

        static void Write(StringBuilder log)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Report)); File.WriteAllText(Report, log.ToString()); Debug.Log(log.ToString());
        }
    }
}
