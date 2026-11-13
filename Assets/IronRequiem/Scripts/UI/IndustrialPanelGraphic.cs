using UnityEngine;
namespace IronRequiem
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class IndustrialPanelGraphic : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField] Color rim = new Color(.10f,.55f,.62f,1);
        [SerializeField,Min(0)] float cut = 20;
        public void Configure(Color fill,Color edge,float corner){color=fill;rim=edge;cut=corner;SetVerticesDirty();}
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;Polygon(vh,r,rim);Polygon(vh,new Rect(r.x+3,r.y+3,r.width-6,r.height-6),color);
            var accent=new Color(.22f,.92f,1,.85f);
            ThermalGaugeGraphic.Quad(vh,new Vector2(r.x+cut,r.yMax-6),new Vector2(r.x+cut,r.yMax-9),new Vector2(r.x+cut+55,r.yMax-9),new Vector2(r.x+cut+60,r.yMax-6),accent);
            ThermalGaugeGraphic.Quad(vh,new Vector2(r.xMax-cut-55,r.y+6),new Vector2(r.xMax-cut-60,r.y+9),new Vector2(r.xMax-cut,r.y+9),new Vector2(r.xMax-cut,r.y+6),accent);
        }
        void Polygon(UnityEngine.UI.VertexHelper vh,Rect r,Color c)
        {
            float k=Mathf.Min(cut,Mathf.Min(r.width,r.height)*.22f);int n=vh.currentVertCount;
            Vector2[] p={new Vector2(r.x+k,r.y),new Vector2(r.xMax-k,r.y),new Vector2(r.xMax,r.y+k),new Vector2(r.xMax,r.yMax-k),new Vector2(r.xMax-k,r.yMax),new Vector2(r.x+k,r.yMax),new Vector2(r.x,r.yMax-k),new Vector2(r.x,r.y+k)};
            vh.AddVert(r.center,c,Vector2.zero);for(int i=0;i<8;i++)vh.AddVert(p[i],c,Vector2.zero);for(int i=0;i<8;i++)vh.AddTriangle(n,n+i+1,n+(i+1)%8+1);
        }
    }
}
