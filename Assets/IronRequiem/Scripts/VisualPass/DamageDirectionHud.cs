using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// Directional damage: red arc around the crosshair pointing at the attacker, tracks it while fading.
    /// Four pooled wedges, built once; zero alloc per frame.
    public sealed class DamageDirectionHud : MonoBehaviour
    {
        [SerializeField, Min(.1f)] float seconds = .85f;
        [SerializeField] Color color = new Color(1f, .12f, .05f, .85f);
        const float radius = 70f;
        const int Pool = 4;
        readonly DamageWedgeGraphic[] wedges = new DamageWedgeGraphic[Pool];
        readonly Vector3[] sources = new Vector3[Pool];
        readonly float[] age = new float[Pool], power = new float[Pool];
        Health health; Transform view; int next;

        void Awake()
        {
            for (int i = 0; i < Pool; i++)
            {
                var go = new GameObject("VP_DamageArc" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(DamageWedgeGraphic));
                var r = (RectTransform)go.transform; r.SetParent(transform, false);
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.sizeDelta = new Vector2(radius * 2 + 40, radius * 2 + 40);
                wedges[i] = go.GetComponent<DamageWedgeGraphic>(); wedges[i].raycastTarget = false; wedges[i].Radius = radius; wedges[i].color = color; wedges[i].enabled = false;
                age[i] = 1;
            }
        }
        void Start()
        {
            var pc = FindAnyObjectByType<PlayerCharacterController>();
            if (pc) { health = pc.GetComponent<Health>(); view = pc.PlayerCamera ? pc.PlayerCamera.transform : pc.transform; }
            if (health) health.OnDamaged += Damaged;
        }
        void OnDestroy() { if (health) health.OnDamaged -= Damaged; }

        void Damaged(float damage, GameObject source)
        {
            if (!source || !view) return;
            Vector3 at = source.transform.position;
            if ((at - view.position).sqrMagnitude < .01f) return;
            int slot = next; next = (next + 1) % Pool;
            for (int i = 0; i < Pool; i++) if (age[i] < 1 && (sources[i] - at).sqrMagnitude < 1f) { slot = i; break; } // same attacker: refresh
            sources[slot] = at; age[slot] = 0; power[slot] = Mathf.Clamp(damage / 25f, .45f, 1f);
            wedges[slot].enabled = true;
        }

        void LateUpdate()
        {
            if (!view) return;
            Vector3 f = view.forward; f.y = 0; Vector3 r = view.right; r.y = 0;
            for (int i = 0; i < Pool; i++)
            {
                if (age[i] >= 1) continue;
                age[i] = Mathf.Min(1, age[i] + Time.deltaTime / seconds);
                if (age[i] >= 1) { wedges[i].enabled = false; continue; }
                Vector3 d = sources[i] - view.position; d.y = 0;
                float angle = Mathf.Atan2(Vector3.Dot(d, r), Vector3.Dot(d, f)) * Mathf.Rad2Deg;
                wedges[i].rectTransform.localRotation = Quaternion.Euler(0, 0, -angle);
                wedges[i].canvasRenderer.SetAlpha(power[i] * (1 - age[i] * age[i]));
            }
        }
    }
}
