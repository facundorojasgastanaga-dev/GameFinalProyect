using System.Reflection;
using UnityEngine;

namespace IronRequiem
{
    /// Diagnostics only (added by an editor menu in play mode): keeps a CannonChargeShot charging so the charge
    /// presentation can be captured without real mouse input. Runs after CannonChargeShot.LateUpdate (which
    /// cancels when there is no input) and before CannonChargePresentation (order 100).
    [DefaultExecutionOrder(50)]
    public sealed class ChargeHoldProbe : MonoBehaviour
    {
        static readonly FieldInfo Charging = typeof(CannonChargeShot).GetField("charging", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo Elapsed = typeof(CannonChargeShot).GetField("elapsed", BindingFlags.NonPublic | BindingFlags.Instance);
        CannonChargeShot shot; float t;
        void Awake() => shot = GetComponent<CannonChargeShot>();
        void LateUpdate()
        {
            if (!shot) { Destroy(this); return; }
            t = Mathf.Min(shot.MaximumChargeDuration, t + Time.deltaTime);
            Charging.SetValue(shot, true); Elapsed.SetValue(shot, t);
        }
    }
}
