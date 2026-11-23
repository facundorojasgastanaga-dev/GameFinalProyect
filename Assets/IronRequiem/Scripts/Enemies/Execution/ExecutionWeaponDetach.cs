using System.Collections.Generic;
using UnityEngine;

namespace IronRequiem
{
    // Executions v02 (F2): hides the executed robot's mounted weapon on the Rip beat (the same frame the player shows its proxy)
    // and publishes the weapon's pose at that instant so the proxy can be born exactly there.
    //  - T-07 / Mech: the mounted StolenCannon child (state Attached) or, failing that, a child named *CannonModule*.
    //  - SawBot: SawBotWeaponSource.Rip() (the saw stays visible from E until Rip).
    //  - Big Bot: BigBotDestruction.CannonViews, plus the existing CannonBreak burst (Emit 18) of DetachCannon.
    // Restored on Cancel / disable if the robot survives; stays hidden once the execution delivered the weapon.
    [DefaultExecutionOrder(130)]
    [RequireComponent(typeof(EnemyExecutionSource))]
    public sealed class ExecutionWeaponDetach : MonoBehaviour
    {
        EnemyExecutionSource source; ExecutionKind kind; SawBotWeaponSource sawSource; BigBotDestruction big; ParticleSystem breakSparks;
        readonly List<Renderer> hidden = new List<Renderer>(16); readonly List<bool> previous = new List<bool>(16);
        bool subscribed;

        // Pose of the mounted weapon at Rip (valid when HasRipPose): world centre of its renderers, rotation of the joint.
        public bool HasRipPose { get; private set; }
        public Vector3 RipPosition { get; private set; }
        public Quaternion RipRotation { get; private set; }
        public Bounds RipBounds { get; private set; }
        public Transform RipJoint { get; private set; }
        public bool WeaponHidden { get; private set; }

        void Awake()
        {
            source = GetComponent<EnemyExecutionSource>(); kind = source.Kind; sawSource = GetComponent<SawBotWeaponSource>(); big = GetComponent<BigBotDestruction>();
            if (kind == ExecutionKind.BigBot) foreach (var ps in GetComponentsInChildren<ParticleSystem>(true)) if (ps.name == "CannonBreak") { breakSparks = ps; break; }
        }
        void OnEnable() { if (source) { source.BeatReached += OnBeat; subscribed = true; } }
        void OnDisable() { if (subscribed && source) source.BeatReached -= OnBeat; subscribed = false; Restore(); }

        void OnBeat(ExecutionBeatInfo info)
        {
            switch (info.Beat)
            {
                case ExecutionBeat.Begin: HasRipPose = false; break;
                case ExecutionBeat.Rip: Hide(); break;
                case ExecutionBeat.Cancel: Restore(); HasRipPose = false; break;
            }
        }

        void Hide()
        {
            if (WeaponHidden) return;
            Bounds bounds = default; bool any = false; RipJoint = source.WeaponJoint;
            void Add(Renderer r)
            {
                if (!r || r is ParticleSystemRenderer) return;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
                hidden.Add(r); previous.Add(r.forceRenderingOff); r.forceRenderingOff = true;
            }
            if (kind == ExecutionKind.SawBot && sawSource)
            {
                foreach (var r in sawSource.RightSawRenderers) Add(r); // already counted; the source hides them (kept in the list only for bounds)
                RestorePreviousOnly();
                sawSource.Rip();
            }
            else if (kind == ExecutionKind.BigBot && big)
            {
                foreach (var r in big.CannonViews) Add(r);
                if (breakSparks) breakSparks.Emit(18);
                big.MarkCannonTakenByExecution(true); // round 3: its death must not throw a second cannon
            }
            else
            {
                var mounted = FindMountedCannon();
                if (mounted) foreach (var r in mounted.GetComponentsInChildren<Renderer>(true)) Add(r);
            }
            if (any)
            {
                RipBounds = bounds; RipPosition = bounds.center; RipRotation = RipJoint ? RipJoint.rotation : transform.rotation; HasRipPose = true;
            }
            else if (RipJoint) { RipPosition = RipJoint.position; RipRotation = RipJoint.rotation; RipBounds = new Bounds(RipPosition, Vector3.one * .3f); HasRipPose = true; }
            WeaponHidden = true;
        }

        Transform FindMountedCannon()
        {
            foreach (var cannon in GetComponentsInChildren<StolenCannon>(true))
                if (cannon.State == StolenCannonState.Attached) return cannon.transform;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "CannonModule" || t.name == "VPD_CannonModule" || t.name == "T07Mech_Cannon") return t;
            return null;
        }

        // SawBot: the bounds pass above toggled forceRenderingOff; the real owner is SawBotWeaponSource, so undo that toggle.
        void RestorePreviousOnly()
        {
            for (int i = 0; i < hidden.Count; i++) if (hidden[i]) hidden[i].forceRenderingOff = previous[i];
            hidden.Clear(); previous.Clear();
        }

        void Restore()
        {
            if (!WeaponHidden) return;
            WeaponHidden = false;
            if (source && source.Delivered) return; // The weapon was taken: it stays gone with the dead robot.
            for (int i = 0; i < hidden.Count; i++) if (hidden[i]) hidden[i].forceRenderingOff = previous[i];
            hidden.Clear(); previous.Clear();
            if (sawSource) sawSource.ReleaseRip();
            if (big) big.MarkCannonTakenByExecution(false);
        }
    }
}
