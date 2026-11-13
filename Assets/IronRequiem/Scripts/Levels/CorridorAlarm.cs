using TMPro;
using UnityEngine;

namespace IronRequiem
{
    /// Alarm presentation for the transit corridor between the hangar and Room01 (12/11/2026). It only watches the
    /// existing round system (CombatTwoRoomsLab): the first wave trips the alarm (siren through the mixer, rotating red
    /// beacons, red pulsing lights, wall screens, "ALERTA · INTRUSIÓN DETECTADA"); clearing the last wave silences it.
    public sealed class CorridorAlarm : MonoBehaviour
    {
        [SerializeField] CombatTwoRoomsLab rounds;
        [SerializeField] Light[] alarmLights = new Light[0];
        [SerializeField] Transform[] beacons = new Transform[0];
        [SerializeField] Light[] beaconLights = new Light[0];
        [SerializeField] Renderer[] beaconHeads = new Renderer[0];
        [SerializeField] AudioSource siren;
        [SerializeField] AudioClip klaxon;
        [SerializeField] TMP_Text[] screens = new TMP_Text[0];
        [SerializeField] string alertText = "ALERTA · INTRUSIÓN DETECTADA", clearText = "ZONA DESPEJADA · ESCLUSA ABIERTA";
        [SerializeField, Min(0)] float alarmIntensity = 3.5f, beaconIntensity = 14f, beaconDegreesPerSecond = 240f, bannerSeconds = 2.8f;
        [SerializeField, ColorUsage(false, true)] Color headOn = new Color(2.2f, .15f, .08f), headOff = new Color(.12f, .01f, .01f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        enum State { Idle, Alarm, Cleared }
        State state; float since, bannerUntil; TMP_Text message; string restore; MaterialPropertyBlock block;

        public bool Alarmed => state == State.Alarm;
        public bool Cleared => state == State.Cleared;

        public void Configure(CombatTwoRoomsLab waves, Light[] red, Transform[] spinners, Light[] spinLights, Renderer[] heads, AudioSource sirenSource, AudioClip klaxonClip, TMP_Text[] walls)
        { rounds = waves; alarmLights = red; beacons = spinners; beaconLights = spinLights; beaconHeads = heads; siren = sirenSource; klaxon = klaxonClip; screens = walls; }

        void Start()
        {
            block = new MaterialPropertyBlock();
            var flow = FindAnyObjectByType<CampaignFlow>(); if (flow) message = flow.Message;
            SetLights(0, 0); SetHeads(headOff);
            foreach (var s in screens) if (s) s.enabled = false;
        }

        void Update()
        {
            if (!rounds) return;
            switch (state)
            {
                case State.Idle:
                    if (rounds.WaveIndex >= 0) Trip();
                    break;
                case State.Alarm:
                    float t = Time.time - since;
                    float pulse = .55f + .45f * Mathf.Sin(t * 6.2f);
                    SetLights(alarmIntensity * pulse * (Mathf.Repeat(t * 9.1f, 1f) < .93f ? 1 : .25f), beaconIntensity);
                    for (int i = 0; i < beacons.Length; i++) if (beacons[i]) beacons[i].Rotate(0, beaconDegreesPerSecond * Time.deltaTime, 0, Space.Self);
                    bool blink = Mathf.Repeat(t * 1.6f, 1f) < .7f;
                    foreach (var s in screens) if (s) s.enabled = blink;
                    if (message && bannerUntil > 0 && Time.time >= bannerUntil) { if (message.text == alertText && restore != null) message.text = restore; bannerUntil = 0; }
                    if (rounds.Completed) Clear();
                    break;
            }
        }

        void Trip()
        {
            state = State.Alarm; since = Time.time;
            if (siren) { siren.loop = true; siren.Play(); if (klaxon) siren.PlayOneShot(klaxon, .9f); }
            SetHeads(headOn);
            foreach (var s in screens) if (s) { s.text = alertText; s.enabled = true; }
            if (message) { restore = message.text; message.text = alertText; bannerUntil = Time.time + bannerSeconds; }
        }

        void Clear()
        {
            state = State.Cleared;
            if (siren) siren.Stop();
            SetLights(0, 0); SetHeads(headOff);
            foreach (var s in screens) if (s) { s.text = clearText; s.color = new Color(.3f, 1f, .5f); s.enabled = true; }
        }

        void SetLights(float red, float beacon)
        {
            foreach (var l in alarmLights) if (l) l.intensity = red;
            foreach (var l in beaconLights) if (l) l.intensity = beacon;
        }

        void SetHeads(Color c)
        {
            if (beaconHeads.Length == 0) return;
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, c);
            foreach (var r in beaconHeads) if (r) r.SetPropertyBlock(block);
        }
    }
}
