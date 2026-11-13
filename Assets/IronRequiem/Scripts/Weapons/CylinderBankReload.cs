using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    // Magazine-style reload for the secondary shotgun: no trickle regeneration (AutomaticReload is off on the
    // variant). The existing WeaponFuelCellHandler still raises one cell per shot. After the fourth shot the
    // four raised cells are pressed back down into the body together, vent with a decompression hiss and
    // steam, seat with a small bounce and the bank is full again. Shooting is blocked during the stroke.
    // The timer only runs while the weapon is shown: switching away pauses it; switching back resumes it.
    [RequireComponent(typeof(WeaponController))]
    public sealed class CylinderBankReload : MonoBehaviour
    {
        [SerializeField, Min(.2f)] float duration = 1.1f;
        [Tooltip("Seconds the bank waits after the last shot before pressing down (lets the last cell pop up).")]
        [SerializeField, Min(0)] float pause = .15f;
        [Tooltip("Extra depth past the rest position when the cells seat (cell local units).")]
        [SerializeField] Vector3 seat = new Vector3(0, -.035f, 0);
        [SerializeField] AudioClip ventSound;
        [SerializeField] ParticleSystem ventSteam;
        WeaponController weapon; WeaponFuelCellHandler cells; float elapsed = -1; bool vented;
        public bool IsReloading => elapsed >= 0;
        public float Duration => pause + duration;
        public bool Vented => vented;
        public ParticleSystem Steam => ventSteam;

        public void Configure(AudioClip sound, ParticleSystem steam, float seconds) { ventSound = sound; ventSteam = steam; duration = seconds; }

        void Awake() { weapon = GetComponent<WeaponController>(); cells = GetComponent<WeaponFuelCellHandler>(); }

        void Update()
        {
            if (!weapon.IsWeaponActive) return;                      // paused while holstered; resumes on re-equip
            if (!IsReloading && weapon.GetCurrentAmmo() <= 0 && weapon.MaxAmmo > 0) { elapsed = 0; vented = false; }
            if (!IsReloading) return;
            weapon.SetAmmo(0);                                       // the bank stays empty (no shot) during the stroke
            elapsed += Time.deltaTime;
            if (elapsed < pause) return;                             // fuel cell handler finishes raising the last cell
            if (cells && cells.enabled) cells.enabled = false;
            float t = Mathf.Clamp01((elapsed - pause) / duration);
            const float press = .35f, bounce = .55f;
            Vector3 up = cells ? cells.FuelCellUsedPosition : Vector3.zero, rest = cells ? cells.FuelCellUnusedPosition : Vector3.zero;
            Vector3 p = t < press ? Vector3.Lerp(up, rest + seat, t / press * (t / press))      // accelerating press
                      : t < bounce ? rest + seat                                                    // seated, venting
                      : Vector3.Lerp(rest + seat, rest, EaseOut((t - bounce) / (1 - bounce)));      // settle
            if (!vented && t >= press) { vented = true; Vent(); }
            if (cells) foreach (var cell in cells.FuelCells) if (cell) cell.transform.localPosition = p;
            if (t >= 1) { elapsed = -1; weapon.SetAmmo(weapon.MaxAmmo); if (cells) cells.enabled = true; }
        }

        void Vent()
        {
            if (ventSound) AudioUtility.CreateSFX(ventSound, transform.position, AudioUtility.AudioGroups.WeaponShoot, 0);
            if (ventSteam) { ventSteam.Clear(true); ventSteam.Play(true); }
        }

        static float EaseOut(float t) => 1 - (1 - t) * (1 - t);
    }
}
