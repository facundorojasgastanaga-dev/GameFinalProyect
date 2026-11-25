using System.Collections;
using NUnit.Framework;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IronRequiem.Tests
{
    /// 24/11: low health shows a broken "INTEGRIDAD CRÍTICA" message instead of the full-screen pulsing vignette.
    public sealed class CriticalDamageMessageTests
    {
        static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }

        [UnityTest]
        public IEnumerator CriticalHealthShowsCrackedMessageNotVignette()
        {
            LogAssert.ignoreFailingMessages = true; // the real session logs unrelated errors while it boots
            SceneManager.LoadScene("Map01_Campaign_S1");
            PlayerCharacterController player = null; float end = Time.realtimeSinceStartup + 60;
            while ((player = Object.FindAnyObjectByType<PlayerCharacterController>()) == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(player, Is.Not.Null, "player in the session");
            yield return Wait(2f);
            LogAssert.ignoreFailingMessages = true;
            CriticalDamageMessage message = null; end = Time.realtimeSinceStartup + 6;
            while ((message = Object.FindAnyObjectByType<CriticalDamageMessage>()) == null && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(message, Is.Not.Null, "message attached to the game HUD");
            var flash = Object.FindAnyObjectByType<FeedbackFlashHUD>();
            if (flash) Assert.That(flash.CriticaHealthVignetteMaxAlpha, Is.EqualTo(0f), "full-screen critical vignette disabled");
            var edgeFx = Object.FindAnyObjectByType<SlideGuardScreenFx>();
            var group = message.GetComponent<CanvasGroup>(); var cracks = message.GetComponentInChildren<CrackGraphic>(true);
            Assert.That(group.alpha, Is.LessThan(.01f), "hidden at full health");

            var health = player.GetComponent<Health>(); bool invincible = health.Invincible; health.Invincible = false;
            health.CurrentHealth = health.MaxHealth * .2f;
            yield return Wait(.6f);
            Assert.That(group.alpha, Is.GreaterThan(.1f), "shown when critical");
            Assert.That(cracks.ImpactCount, Is.GreaterThanOrEqualTo(2), "cracked on appearing");
            if (flash) Assert.That(flash.VignetteCanvasGroup.alpha, Is.LessThan(.01f), "no vignette while critical");
            if (edgeFx) Assert.That(edgeFx.Alpha, Is.LessThan(.01f), "no red screen-edge pulse while critical");
            var rect = ((RectTransform)message.transform).rect;
            Assert.That(rect.width, Is.GreaterThan(100), "visible size");
            int before = cracks.ImpactCount;
            health.TakeDamage(1, null); yield return null; yield return null;
            Assert.That(cracks.ImpactCount, Is.GreaterThan(before), "each hit adds a crack");

            health.CurrentHealth = health.MaxHealth; health.Invincible = invincible;
            yield return Wait(1.2f);
            Assert.That(group.alpha, Is.LessThan(.01f), "hidden again after healing");
            Assert.That(cracks.ImpactCount, Is.EqualTo(0), "cracks reset");
        }
    }
}
