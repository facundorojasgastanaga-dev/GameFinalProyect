using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    // Executions v02 (F2): the Big Bot's world-space health bar covered ~15 % of the view while the player hangs on its
    // shoulder. Between Begin and End/Cancel the bar's component (template WorldspaceHealthBar, found by type name because
    // fps.UI is not referenced) is disabled and its pivot deactivated; afterwards both are restored (if the robot lives).
    // Only the Big Bot is affected: T-07 and SawBot keep their bars.
    [RequireComponent(typeof(EnemyExecutionSource))]
    public sealed class ExecutionHealthBarHider : MonoBehaviour
    {
        sealed class Entry { public Behaviour Bar; public Transform Pivot; public bool BarWasEnabled, PivotWasActive; }
        readonly List<Entry> entries = new List<Entry>(2);
        EnemyExecutionSource source; Health health; bool subscribed, hidden;

        void Awake() { source = GetComponent<EnemyExecutionSource>(); health = GetComponent<Health>(); }
        void OnEnable() { if (source) { source.BeatReached += OnBeat; subscribed = true; } }
        void OnDisable() { if (subscribed && source) source.BeatReached -= OnBeat; subscribed = false; Restore(); }

        void OnBeat(ExecutionBeatInfo info)
        {
            if (info.Beat == ExecutionBeat.Begin && source.Kind == ExecutionKind.BigBot) Hide();
            else if (info.Beat == ExecutionBeat.End || info.Beat == ExecutionBeat.Cancel) Restore();
        }

        void Hide()
        {
            if (hidden) return;
            entries.Clear();
            foreach (var behaviour in GetComponentsInChildren<Behaviour>(true))
            {
                if (!behaviour || behaviour.GetType().Name != "WorldspaceHealthBar") continue;
                Transform pivot = null;
                var field = behaviour.GetType().GetField("HealthBarPivot");
                if (field != null) pivot = field.GetValue(behaviour) as Transform;
                entries.Add(new Entry { Bar = behaviour, Pivot = pivot, BarWasEnabled = behaviour.enabled, PivotWasActive = pivot && pivot.gameObject.activeSelf });
            }
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "HealthBarPivot") continue;
                bool known = false; foreach (var e in entries) if (e.Pivot == t) { known = true; break; }
                if (!known) entries.Add(new Entry { Pivot = t, PivotWasActive = t.gameObject.activeSelf });
            }
            foreach (var e in entries)
            {
                if (e.Bar) e.Bar.enabled = false;
                if (e.Pivot) e.Pivot.gameObject.SetActive(false);
            }
            hidden = true;
        }

        void Restore()
        {
            if (!hidden) return;
            hidden = false;
            bool alive = health && health.CurrentHealth > 0;
            foreach (var e in entries)
            {
                if (!alive) continue; // A dead robot keeps its bar off (the death presentation already hides it).
                if (e.Bar) e.Bar.enabled = e.BarWasEnabled;
                if (e.Pivot) e.Pivot.gameObject.SetActive(e.PivotWasActive);
            }
            entries.Clear();
        }
    }
}
