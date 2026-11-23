using System.Collections.Generic;
using System.Linq;
using IronRequiem.Editor;
using IronRequiem.Editor.Map01;
using NUnit.Framework;
using UnityEngine;

namespace IronRequiem.Tests
{
    /// Pase visual 12 · F2 · datos de luminosidad por tramos (Levels/Map01/Data/Lighting/R*.json). Solo lee datos: no abre escenas.
    public sealed class VisualPassLightingTests
    {
        static readonly string[] Rooms = Enumerable.Range(1, 18).Select(i => "R" + i.ToString("00")).ToArray();

        static LightingAuthoring.RoomData Load(string id) => Map01Json.Load<LightingAuthoring.RoomData>(Map01Paths.Data("Lighting", id));

        [Test]
        public void EveryRoomKeepsAtMostEightLights()
        {
            foreach (var id in Rooms)
            {
                var d = Load(id);
                Assert.That(d.lights.Count, Is.LessThanOrEqualTo(8), id + ": entradas de luz (el pase visual no anade luces)");
                Assert.That(d.lights.Count(l => l.enabled && l.intensity > 0.001f), Is.LessThanOrEqualTo(8), id + ": luces encendidas");
            }
        }

        [Test]
        public void ExposureNeverDropsAlongTheRoute()
        {
            float previous = 0f; int previousTramo = 0;
            foreach (var id in Rooms)
            {
                var d = Load(id);
                Assert.That(d.exposureEV, Is.GreaterThan(0f), id + ": exposureEV presente");
                Assert.That(d.exposureEV, Is.GreaterThanOrEqualTo(previous), id + ": exposicion >= la del cuarto anterior");
                Assert.That(d.tramo, Is.InRange(1, 3), id + ": tramo");
                Assert.That(d.tramo, Is.GreaterThanOrEqualTo(previousTramo), id + ": el tramo no baja");
                Assert.That(d.exposureEV, Is.EqualTo(VisualOverhaulLuminosity.ExposureOfTramo(d.tramo)).Within(1e-4f), id + ": el JSON coincide con el perfil del tramo");
                Assert.That(d.tramo, Is.EqualTo(VisualOverhaulLuminosity.TramoOfRoom(id)), id + ": tramo = sesion de la ruta");
                previous = d.exposureEV; previousTramo = d.tramo;
            }
            Assert.That(Load("R18").exposureEV, Is.GreaterThan(Load("R01").exposureEV), "el tramo final es mas luminoso que el inicial");
        }

        // Colores de estado de las puertas (04_PUERTAS §P2.1): BLOQUEADA rojo (1; 0,03; 0,02), ABRIENDOSE ambar (1; 0,36; 0,02), ABIERTA verde (0,1; 1; 0,22).
        // Una luz "choca" si su saturacion es >= 0,85 y su matiz esta a menos de la tolerancia del de la puerta: 0,025 en rojo y ambar (el sodio calido
        // de las zonas, saturacion 0,5-0,8 o matiz ~0,08-0,10, no cuenta) y 0,04 en verde. Mide el matiz, no la intensidad.
        static readonly (string name, Color color, float tol)[] DoorColours =
        {
            ("rojo", new Color(1f, .03f, .02f), .025f), ("ambar", new Color(1f, .36f, .02f), .025f), ("verde", new Color(.1f, 1f, .22f), .04f),
        };
        const float MinSaturation = .85f;

        // EXCEPCIONES CONOCIDAS (datos del chat 9, no se cambian en este pase): luces rojas / ambar de peligro y de hito, y las luces de alarma / fase que
        // arrancan apagadas (intensidad 0) y las mueven CorridorAlarm / la iluminacion del jefe. R13.A4 es el verde de las bombas, casi igual al verde de
        // puerta abierta; el radar de R07 (A5-A7) es el rojo de la alarma. Cualquier luz NUEVA que choque con un color de puerta hace fallar la prueba.
        static readonly HashSet<string> KnownExceptions = new HashSet<string>
        {
            "R03.A7", "R06.A5", "R06.A6", "R07.A5", "R07.A6", "R07.A7", "R10.A2", "R10.A3", "R10.A4", "R12.A5", "R12.A6", "R13.A4", "R13.A5", "R13.A6", "R14.A5", "R14.A6", "R18.A5", "R18.A6",
        };

        static string ClashingDoorColour(float[] rgb)
        {
            Color.RGBToHSV(new Color(rgb[0], rgb[1], rgb[2]), out float h, out float s, out _);
            if (s < MinSaturation) return null;
            foreach (var (name, color, tol) in DoorColours)
            {
                Color.RGBToHSV(color, out float dh, out _, out _);
                float dist = Mathf.Abs(h - dh); dist = Mathf.Min(dist, 1f - dist);
                if (dist <= tol) return name;
            }
            return null;
        }

        [Test]
        public void RoomLightsAvoidTheDoorStateColoursExceptKnownCases()
        {
            var clashes = new List<string>(); var used = new HashSet<string>();
            foreach (var id in Rooms)
                foreach (var l in Load(id).lights)
                {
                    string clash = ClashingDoorColour(l.color);
                    if (clash == null) continue;
                    if (KnownExceptions.Contains(l.anchor)) { used.Add(l.anchor); continue; }
                    clashes.Add(l.anchor + " (" + clash + ", " + string.Join(",", l.color) + ")");
                }
            Assert.That(clashes, Is.Empty, "luces con color de puerta no listadas como excepcion conocida: " + string.Join("; ", clashes));
            var stale = KnownExceptions.Where(a => !used.Contains(a)).ToArray();
            if (stale.Length > 0) Debug.Log("[VisualPassLightingTests] excepciones conocidas que ya no chocan (se pueden quitar de la lista): " + string.Join(", ", stale));
        }

        [Test]
        public void TheLuminosityDataIsWellFormed()
        {
            foreach (var id in Rooms)
            {
                var d = Load(id); var e = d.environment;
                Assert.That(e.sky.Concat(e.equator).Concat(e.ground).All(c => c >= 0f && c <= 2f), Is.True, id + ": ambiente en rango razonable");
                Assert.That(e.fogDensity, Is.InRange(0.001f, 0.03f), id + ": densidad de niebla");
                foreach (var l in d.lights) { Assert.That(l.intensity, Is.GreaterThanOrEqualTo(0f), l.anchor); Assert.That(l.range, Is.GreaterThan(0f), l.anchor); }
            }
        }
    }
}
