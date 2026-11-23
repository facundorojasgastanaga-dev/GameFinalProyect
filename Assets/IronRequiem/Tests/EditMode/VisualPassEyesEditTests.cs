using NUnit.Framework;
using UnityEngine;

namespace IronRequiem.Tests
{
    /// Visual pass F3: the eye style is data (EnemyEyeStyle), so the approved numbers are tested without a scene.
    public sealed class VisualPassEyesEditTests
    {
        // Door colours of Docs/IronRequiem/Map01/04_PUERTAS.md P2.1 (BLOQUEADA, ABRIENDOSE, ABIERTA) as written there.
        static readonly Color DoorBlocked = new Color(1f, .03f, .02f), DoorAmber = new Color(1f, .36f, .02f), DoorGreen = new Color(.1f, 1f, .22f);
        // CRITERION CHANGE (21/11/2026): the first approved eye red (1; 0.05; 0.12) read pink / magenta under additive blend + bloom, so
        // the base is now pure red (1; 0.04; 0.03), practically the door "BLOQUEADA" red. The eye is no longer told apart from a door by
        // hue but by SHAPE: a small round point with a halo (core <= 0.20 m, never a bar), at head height, moving with the robot, with
        // a slow pulse (<= 2 Hz) only in Alert and none in Patrol / Attack (no rotating beacon). Amber and green stay clearly different.

        static float Distance(Color a, Color b) => new Vector3(a.r - b.r, a.g - b.g, a.b - b.b).magnitude;

        [Test] public void BaseColor_IsPureRedWithoutBlue()
        {
            var c = EnemyEyeStyle.BaseColor;
            Assert.That(c.r, Is.EqualTo(1f).Within(1e-4f)); Assert.That(c.g, Is.LessThanOrEqualTo(.06f)); Assert.That(c.b, Is.LessThanOrEqualTo(.06f), "blue turns it pink");
        }

        [Test] public void Eye_IsToldApartFromDoorsByShapeAndRhythm_NotByHue()
        {
            Assert.That(Distance(EnemyEyeStyle.BaseColor, DoorAmber), Is.GreaterThan(.25f), "door amber");
            Assert.That(Distance(EnemyEyeStyle.BaseColor, DoorGreen), Is.GreaterThan(.25f), "door green");
            Assert.That(EnemyEyeStyle.CoreCapStandard, Is.LessThanOrEqualTo(.20f), "a point, not a strip");
            Assert.That(EnemyEyeStyle.AlertPulseHz, Is.LessThanOrEqualTo(2f), "no beacon rhythm");
            Assert.That(EnemyEyeStyle.PulseMultiplier(EnemyEyeState.Patrol, .37f), Is.EqualTo(1f)); Assert.That(EnemyEyeStyle.PulseMultiplier(EnemyEyeState.Attack, .37f), Is.EqualTo(1f));
            Assert.That(EnemyEyeStyle.HaloSize(EnemyEyeState.Attack, EnemyEyeSize.Standard), Is.GreaterThan(EnemyEyeStyle.CoreSize(EnemyEyeState.Attack, EnemyEyeSize.Standard)), "core + halo, not a flat bar");
        }

        [Test] public void States_DifferBySizeAndRhythm_AtFiveFifteenAndTwentyFiveMetres()
        {
            Assert.That(EnemyEyeStyle.AlertPulseDepth, Is.GreaterThanOrEqualTo(.45f)); Assert.That(EnemyEyeStyle.AlertPulseHz, Is.EqualTo(1.5f));
            float lo = float.MaxValue, hi = 0; for (float t = 0; t < 2f; t += .005f) { float h = EnemyEyeStyle.Hdr(EnemyEyeState.Alert, t); lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h); }
            Assert.That(lo / hi, Is.LessThanOrEqualTo(.56f), "alert pulse is clearly visible");
            Assert.That(EnemyEyeStyle.CoreSize(EnemyEyeState.Attack, EnemyEyeSize.Standard), Is.EqualTo(EnemyEyeStyle.CoreCapStandard).Within(1e-5f), "attack core at the cap");
            foreach (var size in new[] { EnemyEyeSize.Standard, EnemyEyeSize.Large })
                foreach (float d in new[] { 5f, 15f, 25f })
                {
                    float pc = EnemyEyeStyle.FinalCoreSize(EnemyEyeState.Patrol, size, d, 60f), ph = EnemyEyeStyle.FinalHaloSize(EnemyEyeState.Patrol, size, d, 60f);
                    float ac = EnemyEyeStyle.FinalCoreSize(EnemyEyeState.Alert, size, d, 60f), ah = EnemyEyeStyle.FinalHaloSize(EnemyEyeState.Alert, size, d, 60f);
                    float tc = EnemyEyeStyle.FinalCoreSize(EnemyEyeState.Attack, size, d, 60f), th = EnemyEyeStyle.FinalHaloSize(EnemyEyeState.Attack, size, d, 60f);
                    string w = size + " " + d + " m";
                    Assert.That(th, Is.GreaterThanOrEqualTo(2.2f * ph), "attack halo >= 2.2x patrol halo " + w);
                    Assert.That((tc * tc + th * th), Is.GreaterThanOrEqualTo(2.5f * (pc * pc + ph * ph)), "attack on-screen area >= 2.5x patrol " + w);
                    Assert.That(ac, Is.GreaterThanOrEqualTo(pc).And.LessThanOrEqualTo(tc), "alert core in between " + w);
                    Assert.That(ah, Is.GreaterThan(ph).And.LessThan(th), "alert halo in between " + w);
                }
        }

        [Test] public void Hdr_NeverExceedsFour_ForAnyStateAndTime()
        {
            Assert.That(EnemyEyeStyle.MaxHdr, Is.EqualTo(4f));
            foreach (EnemyEyeState s in System.Enum.GetValues(typeof(EnemyEyeState)))
                for (float t = 0; t < 20f; t += .0137f)
                {
                    Assert.That(EnemyEyeStyle.Hdr(s, t), Is.InRange(0f, EnemyEyeStyle.MaxHdr));
                    Assert.That(EnemyEyeStyle.HaloHdr(s, t), Is.InRange(0f, EnemyEyeStyle.MaxHdr));
                    var c = EnemyEyeStyle.EyeColor(s, t, Mathf.Abs(Mathf.Sin(t * 9f)));
                    Assert.That(Mathf.Max(c.r, c.g, c.b), Is.LessThanOrEqualTo(EnemyEyeStyle.MaxHdr + 1e-4f));
                }
        }

        [Test] public void Intensity_GrowsPatrolAlertAttack_AndDeadIsOff()
        {
            // the alert pulse (x0.75..1.25) never crosses the neighbouring states
            float alertMin = float.MaxValue, alertMax = 0;
            for (float t = 0; t < 4f; t += .01f) { float h = EnemyEyeStyle.Hdr(EnemyEyeState.Alert, t); alertMin = Mathf.Min(alertMin, h); alertMax = Mathf.Max(alertMax, h); }
            Assert.That(alertMin, Is.GreaterThan(EnemyEyeStyle.Hdr(EnemyEyeState.Patrol, 0)));
            Assert.That(alertMax, Is.LessThan(EnemyEyeStyle.Hdr(EnemyEyeState.Attack, 0)));
            Assert.That(EnemyEyeStyle.Hdr(EnemyEyeState.Patrol, 3f), Is.EqualTo(EnemyEyeStyle.PatrolHdr).Within(1e-4f));
            Assert.That(EnemyEyeStyle.Hdr(EnemyEyeState.Attack, 3f), Is.EqualTo(EnemyEyeStyle.AttackHdr).Within(1e-4f));
            Assert.That(EnemyEyeStyle.Hdr(EnemyEyeState.Dead, 3f), Is.EqualTo(0f));
            Assert.That(EnemyEyeStyle.HaloSize(EnemyEyeState.Dead, EnemyEyeSize.Standard), Is.EqualTo(0f));
            Assert.That(EnemyEyeStyle.HaloSize(EnemyEyeState.Patrol, EnemyEyeSize.Standard), Is.LessThan(EnemyEyeStyle.HaloSize(EnemyEyeState.Attack, EnemyEyeSize.Standard)));
        }

        [Test] public void AlertPulse_IsSlow()
        {
            Assert.That(EnemyEyeStyle.AlertPulseHz, Is.EqualTo(1.5f));
            // steady states have no pulse at all
            foreach (var s in new[] { EnemyEyeState.Patrol, EnemyEyeState.Attack })
                Assert.That(EnemyEyeStyle.PulseMultiplier(s, .37f), Is.EqualTo(1f));
        }

        [Test] public void Sizes_AreCapped_AtAnyDistance()
        {
            foreach (EnemyEyeSize size in System.Enum.GetValues(typeof(EnemyEyeSize)))
                foreach (EnemyEyeState s in System.Enum.GetValues(typeof(EnemyEyeState)))
                {
                    Assert.That(EnemyEyeStyle.CoreSize(s, size), Is.LessThanOrEqualTo(EnemyEyeStyle.CoreCap(size) + 1e-5f));
                    Assert.That(EnemyEyeStyle.HaloSize(s, size), Is.LessThanOrEqualTo(EnemyEyeStyle.HaloCap(size) + 1e-5f));
                    foreach (float d in new[] { 0f, .3f, 1f, 5f, 25f, 100f, 5000f, float.NaN, float.PositiveInfinity })
                    {
                        float core = EnemyEyeStyle.FinalCoreSize(s, size, d, 60f), halo = EnemyEyeStyle.FinalHaloSize(s, size, d, 60f);
                        Assert.That(float.IsNaN(core) || float.IsInfinity(core) || float.IsNaN(halo) || float.IsInfinity(halo), Is.False, s + " " + size + " d=" + d);
                        Assert.That(core, Is.LessThanOrEqualTo(EnemyEyeStyle.CoreCap(size) + 1e-5f));
                        Assert.That(halo, Is.LessThanOrEqualTo(EnemyEyeStyle.HaloCap(size) + 1e-5f));
                    }
                }
            Assert.That(EnemyEyeStyle.CoreCapStandard, Is.LessThanOrEqualTo(.20f)); // .16 -> .20 on 21/11/2026 so the eye still counts as visible at 25 m
            Assert.That(EnemyEyeStyle.HaloCapStandard, Is.LessThanOrEqualTo(.9f));
        }

        [Test] public void FarEye_KeepsAMinimumAngularSize_AtTwentyFiveMetres()
        {
            float px = EnemyEyeStyle.PixelWorldSize(25f, 60f);
            Assert.That(EnemyEyeStyle.FinalCoreSize(EnemyEyeState.Patrol, EnemyEyeSize.Standard, 25f, 60f), Is.GreaterThanOrEqualTo(2f * px));
            Assert.That(EnemyEyeStyle.FinalHaloSize(EnemyEyeState.Patrol, EnemyEyeSize.Standard, 25f, 60f), Is.GreaterThanOrEqualTo(4f * px));
            Assert.That(EnemyEyeStyle.HaloDistanceFade(25f), Is.GreaterThan(.9f), "the halo does not disappear at 25 m");
            Assert.That(EnemyEyeStyle.HaloDistanceFade(.3f), Is.LessThan(.05f), "a face-to-face halo does not wash the screen");
        }

        [Test] public void ShotWarning_IsTwoFlashesInTheLastThirdOfASecond()
        {
            float lead = EnemyEyeStyle.WarningLead; Assert.That(lead, Is.EqualTo(.35f));
            int humps = 0; bool up = false;
            for (float t = 0; t < lead; t += lead / 400f) { bool on = EnemyEyeStyle.ShotFlash(t, lead) > .5f; if (on && !up) humps++; up = on; }
            Assert.That(humps, Is.EqualTo(2));
            Assert.That(EnemyEyeStyle.ShotFlash(-.1f, lead), Is.EqualTo(0f)); Assert.That(EnemyEyeStyle.ShotFlash(lead + .1f, lead), Is.EqualTo(0f));
            var hot = EnemyEyeStyle.EyeColor(EnemyEyeState.Warning, 0, 1f);
            Assert.That(hot.g, Is.GreaterThan(EnemyEyeStyle.EyeColor(EnemyEyeState.Attack, 0, 0).g), "flashes lean toward white-hot");
        }

        [Test] public void HotCentre_CappedSmallerThanTheRedCore_AndAtLeastThreePixelsUpTo25m()
        {
            foreach (EnemyEyeState s in System.Enum.GetValues(typeof(EnemyEyeState)))
                for (float t = 0; t < 10f; t += .0137f) Assert.That(EnemyEyeStyle.HotHdr(s, t), Is.InRange(0f, EnemyEyeStyle.MaxHdr), "hot HDR " + s);
            Assert.That(EnemyEyeStyle.HotHdr(EnemyEyeState.Dead, 1f), Is.EqualTo(0f));
            Assert.That(EnemyEyeStyle.HotHdr(EnemyEyeState.Patrol, 1f), Is.GreaterThan(1f), "dim but present");
            foreach (EnemyEyeSize size in System.Enum.GetValues(typeof(EnemyEyeSize)))
                foreach (var s in new[] { EnemyEyeState.Patrol, EnemyEyeState.Alert, EnemyEyeState.Attack, EnemyEyeState.Warning })
                {
                    foreach (float d in new[] { .3f, 1f, 5f, 15f, 25f, 60f, 200f, float.NaN })
                    {
                        float hot = EnemyEyeStyle.FinalHotSize(s, size, d, 60f), core = EnemyEyeStyle.FinalCoreSize(s, size, d, 60f), halo = EnemyEyeStyle.FinalHaloSize(s, size, d, 60f);
                        Assert.That(float.IsNaN(hot) || float.IsInfinity(hot), Is.False);
                        Assert.That(hot, Is.LessThanOrEqualTo(core + 1e-5f), "hot <= red core " + s + " d=" + d);
                        Assert.That(hot, Is.LessThanOrEqualTo(EnemyEyeStyle.HotCap(size) + 1e-5f));
                        Assert.That(core * core + halo * halo, Is.GreaterThan(hot * hot), "the red area (core + halo) dominates " + s + " d=" + d);
                    }
                    foreach (float d in new[] { 15f, 25f })
                        Assert.That(EnemyEyeStyle.FinalHotSize(s, size, d, 60f), Is.GreaterThanOrEqualTo(EnemyEyeStyle.HotMinPixels * EnemyEyeStyle.PixelWorldSize(d, 60f) - 1e-4f), "min px at " + d + " m " + s + " " + size);
                }
        }

        static void AssertRed(Color c, string what)
        {
            float peak = Mathf.Max(c.r, c.g, c.b); Assert.That(peak, Is.GreaterThan(0f), what);
            Color.RGBToHSV(new Color(c.r / peak, c.g / peak, c.b / peak), out float h, out float s, out _);
            Assert.That(h < .03f || h > .97f, Is.True, what + " hue " + h);
            Assert.That(s, Is.GreaterThanOrEqualTo(.8f), what + " saturation " + s);
        }

        [Test] public void EyeReadsRed_CoreAndCentre_InEveryState()
        {
            foreach (var s in new[] { EnemyEyeState.Patrol, EnemyEyeState.Alert, EnemyEyeState.Attack })
            {
                AssertRed(EnemyEyeStyle.EyeColor(s, 0f, 0f), "core " + s);
                AssertRed(EnemyEyeStyle.EyeColor(s, .3f, 0f), "core " + s + " (pulse)");
                var hot = EnemyEyeStyle.HotHue; AssertRed(new Color(hot.r * EnemyEyeStyle.HotHdr(s, 0f), hot.g * EnemyEyeStyle.HotHdr(s, 0f), hot.b * EnemyEyeStyle.HotHdr(s, 0f)), "hot centre " + s);
            }
            for (float f = 0; f <= 1f; f += .1f) AssertRed(EnemyEyeStyle.EyeColor(EnemyEyeState.Warning, 0f, f), "warning flash " + f);
            AssertRed(EnemyEyeStyle.BaseColor, "base"); AssertRed(EnemyEyeStyle.WarningHue, "warning hue");
        }

        [Test] public void Sane_RemovesNaNAndInfinity()
        {
            Assert.That(EnemyEyeStyle.Sane(float.NaN, 3f, 0f, 5f), Is.EqualTo(3f));
            Assert.That(EnemyEyeStyle.Sane(float.PositiveInfinity, 3f, 0f, 5f), Is.EqualTo(3f));
            Assert.That(EnemyEyeStyle.Sane(9f, 3f, 0f, 5f), Is.EqualTo(5f));
            Assert.That(EnemyEyeStyle.IsFinite(new Vector3(0, float.NaN, 0)), Is.False);
        }
    }
}
