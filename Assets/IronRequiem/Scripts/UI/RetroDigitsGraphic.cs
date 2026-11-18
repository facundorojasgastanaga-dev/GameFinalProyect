using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// <summary>Point-sharp 5x7 bitmap numerals, built as UI geometry without a dynamic font atlas.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RetroDigitsGraphic : MaskableGraphic
    {
        static readonly ulong[] Digits =
        {
            0b01110_10001_10011_10101_11001_10001_01110,
            0b00100_01100_00100_00100_00100_00100_01110,
            0b01110_10001_00001_00010_00100_01000_11111,
            0b11110_00001_00001_01110_00001_00001_11110,
            0b00010_00110_01010_10010_11111_00010_00010,
            0b11111_10000_10000_11110_00001_00001_11110,
            0b01110_10000_10000_11110_10001_10001_01110,
            0b11111_00001_00010_00100_01000_01000_01000,
            0b01110_10001_10001_01110_10001_10001_01110,
            0b01110_10001_10001_01111_00001_00001_01110
        };
        const ulong Percent = 0b11001_11010_00010_00100_01000_01011_10011;
        int number; bool percent;
        protected override void Awake(){base.Awake();useLegacyMeshGeneration=false;}
        public void SetValue(int value, bool showPercent = false)
        {
            value = Mathf.Clamp(value, 0, 9999);
            if (number == value && percent == showPercent) return;
            number = value; percent = showPercent; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            int count = number >= 1000 ? 4 : number >= 100 ? 3 : number >= 10 ? 2 : 1;
            int glyphs = count + (percent ? 1 : 0);
            var bounds = GetPixelAdjustedRect();
            float pixel = Mathf.Max(1, Mathf.Floor(Mathf.Min(bounds.width / (glyphs * 6 - 1), bounds.height / 7)));
            float width = (glyphs * 6 - 1) * pixel;
            float x = bounds.center.x - width * .5f, y = bounds.center.y - pixel * 3.5f;
            int divisor = count == 4 ? 1000 : count == 3 ? 100 : count == 2 ? 10 : 1;
            // O(4*35), at most four digits plus a percent sign. Rebuilt only when value, bounds or colour changes.
            for (int i = 0; i < count; i++)
            {
                Glyph(mesh, Digits[number / divisor % 10], x + i * pixel * 6, y, pixel); divisor /= 10;
            }
            if (percent) Glyph(mesh, Percent, x + count * pixel * 6, y, pixel);
        }
        void Glyph(VertexHelper mesh, ulong mask, float x, float y, float size)
        {
            for (int row = 0; row < 7; row++) for (int col = 0; col < 5; col++)
                if ((mask & (1UL << ((6 - row) * 5 + 4 - col))) != 0)
                {
                    int index = mesh.currentVertCount;
                    var point = new Vector2(x + col * size, y + (6 - row) * size);
                    mesh.AddVert(point, color, Vector2.zero); mesh.AddVert(point + Vector2.right * size, color, Vector2.zero);
                    mesh.AddVert(point + Vector2.one * size, color, Vector2.zero); mesh.AddVert(point + Vector2.up * size, color, Vector2.zero);
                    mesh.AddTriangle(index, index + 2, index + 1); mesh.AddTriangle(index, index + 3, index + 2);
                }
        }
    }
}
