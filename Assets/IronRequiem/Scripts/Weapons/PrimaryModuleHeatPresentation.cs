using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    // Presentation only. Native ammo, capsule motion, steam and cooldown remain owned by the template.
    [RequireComponent(typeof(WeaponController), typeof(WeaponFuelCellHandler), typeof(OverheatBehavior))]
    public sealed class PrimaryModuleHeatPresentation : MonoBehaviour
    {
        Renderer[] cells;
        Color[][] baseColors;
        WeaponController weapon;
        OverheatBehavior heat;
        MaterialPropertyBlock block;
        float previousRatio = -1;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), EmissionColor = Shader.PropertyToID("_EmissionColor");
        void Awake()
        {
            weapon = GetComponent<WeaponController>(); heat = GetComponent<OverheatBehavior>(); block = new MaterialPropertyBlock();
            var controller = GetComponent<WeaponFuelCellHandler>(); cells = new Renderer[controller.FuelCells.Length]; baseColors = new Color[cells.Length][];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = controller.FuelCells[i].GetComponent<Renderer>(); var materials = cells[i].sharedMaterials;
                baseColors[i] = new Color[materials.Length];
                for (int j = 0; j < materials.Length; j++) baseColors[i][j] = materials[j].GetColor(BaseColor);
            }
        }
        void LateUpdate()
        {
            float ratio = weapon.CurrentAmmoRatio; if (Mathf.Approximately(previousRatio, ratio)) return; previousRatio = ratio;
            float amount = 1 - ratio; var emission = heat.OverheatGradient.Evaluate(amount);
            float tint = Mathf.SmoothStep(0, .8f, Mathf.InverseLerp(.35f, 1, amount));
            for (int i = 0; i < cells.Length; i++) if (cells[i]) for (int j = 0; j < baseColors[i].Length; j++)
            {
                cells[i].GetPropertyBlock(block, j); var original = baseColors[i][j];
                var hot = new Color(.7f, .065f, .018f, original.a);
                block.SetColor(BaseColor, Color.Lerp(original, hot, tint)); block.SetColor(EmissionColor, emission);
                cells[i].SetPropertyBlock(block, j);
            }
        }
        void OnDisable()
        {
            previousRatio = -1; if (cells == null) return;
            for (int i = 0; i < cells.Length; i++) if (cells[i]) for (int j = 0; j < baseColors[i].Length; j++) cells[i].SetPropertyBlock(null, j);
        }
    }
}
