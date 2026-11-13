using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    /// Diagnostics only (added by an editor menu in play mode): logs every shot of this enemy's weapon,
    /// where each projectile starts, which way it flies and where it ends.
    public sealed class EnemyShotProbe : MonoBehaviour
    {
        class Track { public ProjectileBase p; public Vector3 start, last; public float t0; public int id; }
        readonly List<Track> tracks = new List<Track>();
        WeaponController weapon; int shots; float lastLog;

        void Start() { weapon = GetComponentInChildren<WeaponController>(); if (weapon) weapon.OnShootProcessed += Shot; }
        void OnDestroy() { if (weapon) weapon.OnShootProcessed -= Shot; }

        void Shot()
        {
            shots++;
            foreach (var p in FindObjectsByType<ProjectileBase>(FindObjectsSortMode.None))
            {
                if (p.Owner != gameObject || tracks.Exists(x => x.p == p)) continue;
                tracks.Add(new Track { p = p, start = p.transform.position, last = p.transform.position, t0 = Time.time, id = shots });
            }
            var cam = Camera.main ? Camera.main.transform.position : Vector3.zero;
            Debug.Log($"[shot {shots}] t={Time.time:0.0} muzzle={weapon.WeaponMuzzle.position.ToString("F2")} muzzleFwd={weapon.WeaponMuzzle.forward.ToString("F2")} toCam={(cam - weapon.WeaponMuzzle.position).normalized.ToString("F2")}");
        }

        void Update()
        {
            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                var k = tracks[i];
                if (k.p && k.p.gameObject.activeInHierarchy && Time.time - k.t0 < 4) { k.last = k.p.transform.position; continue; }
                var d = k.last - k.start;
                Debug.Log($"[proj {k.id}] lived={Time.time - k.t0:0.00}s start={k.start.ToString("F2")} end={k.last.ToString("F2")} dist={d.magnitude:0.0} dir={d.normalized.ToString("F2")}");
                tracks.RemoveAt(i);
            }
            if (Time.time - lastLog < 3) return; lastLog = Time.time;
            var ec = GetComponent<Unity.FPS.AI.EnemyController>();
            Debug.Log($"[probe] t={Time.time:0.0} shots={shots} seeing={ec?.IsSeeingTarget} inRange={ec?.IsTargetInAttackRange} weaponActive={weapon?.isActiveAndEnabled}");
        }
    }
}
