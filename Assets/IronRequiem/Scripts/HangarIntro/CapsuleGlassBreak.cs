using UnityEngine;

namespace IronRequiem
{
    /// Room00_Hangar capsule canopy that the protagonist breaks from the inside (12/11/2026).
    /// Crack(): crack decal + dry crack sound + a few sparks. Shatter(): the canopy disappears and pooled shards fly out
    /// with simple physics, bounce and settle, then switch off after a few seconds; dust, sparks and a loud glass sound.
    /// Broken(): the end state without effects (used by the cinematic skip / restart). Shards live under this object, so
    /// they disappear with the sector when it unloads. The menu capsule does not use this component.
    public sealed class CapsuleGlassBreak : MonoBehaviour
    {
        [SerializeField] Renderer canopy;
        [SerializeField] Renderer crack;
        [SerializeField] Rigidbody[] shards = new Rigidbody[0];
        [SerializeField] Transform impact;               // inner face of the glass where the fist lands
        [SerializeField] ParticleSystem dust, sparks;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip crackClip, shatterClip;
        [SerializeField, Min(.5f)] float shardLifetime = 6f;
        [SerializeField] Vector2 shardSpeed = new Vector2(1.8f, 4.2f);
        Vector3[] homePos; Quaternion[] homeRot; float shatteredAt = -1; bool parked;

        public bool IsShattered => shatteredAt >= 0 || !canopy || !canopy.enabled;
        public bool IsCracked => crack && crack.enabled;
        public int ActiveShards { get { int n = 0; foreach (var s in shards) if (s && s.gameObject.activeSelf) n++; return n; } }

        [SerializeField] Collider[] ignore = new Collider[0];   // capsule colliders the shards start inside of
        public void ConfigureIgnore(Collider[] colliders) => ignore = colliders ?? new Collider[0];
        public void Configure(Renderer glass, Renderer crackDecal, Rigidbody[] pieces, Transform hitPoint, ParticleSystem dustFx, ParticleSystem sparkFx, AudioSource source, AudioClip crackSfx, AudioClip shatterSfx)
        { canopy = glass; crack = crackDecal; shards = pieces; impact = hitPoint; dust = dustFx; sparks = sparkFx; audioSource = source; crackClip = crackSfx; shatterClip = shatterSfx; }

        void Awake()
        {
            homePos = new Vector3[shards.Length]; homeRot = new Quaternion[shards.Length];
            for (int i = 0; i < shards.Length; i++)
            {
                if (!shards[i]) continue;
                homePos[i] = shards[i].transform.localPosition; homeRot[i] = shards[i].transform.localRotation;
                shards[i].gameObject.SetActive(false);
            }
            if (crack) crack.enabled = false;
            if (canopy) canopy.enabled = true;
            foreach (var s in shards)
            {
                if (!s) continue; var c = s.GetComponent<Collider>(); if (!c) continue;
                foreach (var other in ignore) if (other) Physics.IgnoreCollision(c, other, true);
            }
        }

        public void Crack()
        {
            if (IsShattered || IsCracked) return;
            if (crack) crack.enabled = true;
            if (sparks && impact) { sparks.transform.position = impact.position; sparks.Emit(6); }
            if (audioSource && crackClip) audioSource.PlayOneShot(crackClip, 1f);
        }

        public void Shatter()
        {
            if (shatteredAt >= 0) return;
            shatteredAt = Time.time; parked = false;
            if (canopy) canopy.enabled = false;
            if (crack) crack.enabled = false;
            var centre = impact ? impact.position : transform.position;
            var outward = canopy ? -canopy.transform.forward : transform.forward; // capsule front (-Z of the pod)
            for (int i = 0; i < shards.Length; i++)
            {
                var s = shards[i]; if (!s) continue;
                var tr = s.transform; tr.localPosition = homePos[i]; tr.localRotation = homeRot[i];
                s.gameObject.SetActive(true); s.isKinematic = false;
                // deterministic spread per shard: away from the impact and out of the capsule
                float h = Mathf.Repeat(i * .618034f, 1f);
                var away = (tr.position - centre); away = away.sqrMagnitude > 1e-4f ? away.normalized : outward;
                var dir = (outward * 1.2f + away + Vector3.up * (.15f + .35f * h)).normalized;
                s.linearVelocity = dir * Mathf.Lerp(shardSpeed.x, shardSpeed.y, h);
                s.angularVelocity = new Vector3(Mathf.Sin(i * 1.7f), Mathf.Cos(i * 2.3f), Mathf.Sin(i * .9f)) * 9f;
            }
            if (dust) { dust.transform.position = centre; dust.Emit(40); }
            if (sparks) { sparks.transform.position = centre; sparks.Emit(18); }
            if (audioSource && shatterClip) audioSource.PlayOneShot(shatterClip, 1f);
        }

        /// End state without effects: canopy gone, no shards (they would have settled and switched off).
        public void Broken()
        {
            shatteredAt = Time.time - shardLifetime - 1; if (canopy) canopy.enabled = false; if (crack) crack.enabled = false;
            Park();
        }

        void Update()
        {
            if (shatteredAt < 0 || parked) return;
            if (Time.time - shatteredAt >= shardLifetime) Park();
        }

        void Park()
        {
            parked = true;
            foreach (var s in shards) if (s) { s.isKinematic = true; s.gameObject.SetActive(false); }
        }
    }
}
