using Unity.FPS.Game;
using UnityEngine;

namespace IronRequiem
{
    /// T2: light pooled smoke puff at the weapon muzzle on every shot, same frame as the flash, recoil and sound
    /// (WeaponController.OnShootProcessed). Small and short so it never covers the view or the target.
    [RequireComponent(typeof(WeaponController))]
    public sealed class MuzzleShotFx : MonoBehaviour
    {
        [SerializeField] GameObject smokePrefab;
        [SerializeField, Min(.05f)] float lifetime = .9f;
        [SerializeField, Min(.1f)] float scale = 1f;
        WeaponController weapon;

        public void Configure(GameObject smoke, float size) { smokePrefab = smoke; scale = size; }

        void Awake() => weapon = GetComponent<WeaponController>();
        void OnEnable() { if (weapon) weapon.OnShootProcessed += Shot; }
        void OnDisable() { if (weapon) weapon.OnShootProcessed -= Shot; }

        void Shot()
        {
            if (!smokePrefab || !weapon.WeaponMuzzle) return;
            var go = FxPool.Spawn(smokePrefab, weapon.WeaponMuzzle.position, weapon.WeaponMuzzle.rotation, lifetime);
            if (go) go.transform.localScale = Vector3.one * scale;
        }
    }
}
