using UnityEngine;
namespace IronRequiem
{
    /// Chamfered industrial plate. Three looks share one mesh builder:
    /// Standard = the original dark plate with cyan edge slashes (HUD banners, help plates...);
    /// Label    = a Dymo-style embossed tape strip (menu buttons);
    /// Sheet    = a riveted steel sheet with a hazard strip (menu frames).
    /// The style is serialized, so existing assets keep the Standard look until something calls SetStyle.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class IndustrialPanelGraphic : UnityEngine.UI.MaskableGraphic
    {
        public enum PanelStyle { Standard, Label, Sheet }
        [SerializeField] Color rim = new Color(.10f,.55f,.62f,1);
        [SerializeField,Min(0)] float cut = 20;
        [SerializeField] PanelStyle style;
        public PanelStyle Style => style;
        public void Configure(Color fill,Color edge,float corner){color=fill;rim=edge;cut=corner;SetVerticesDirty();}
        public void SetStyle(PanelStyle value){if(style==value)return;style=value;SetVerticesDirty();}

        static readonly Color Hazard=new Color(1f,.62f,.12f,1),HazardDark=new Color(.03f,.03f,.035f,1);
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            if(style==PanelStyle.Label){Label(vh,r);return;}
            if(style==PanelStyle.Sheet){Sheet(vh,r);return;}
            Polygon(vh,r,rim);Polygon(vh,new Rect(r.x+3,r.y+3,r.width-6,r.height-6),color);
            var accent=new Color(.22f,.92f,1,.85f);
            ThermalGaugeGraphic.Quad(vh,new Vector2(r.x+cut,r.yMax-6),new Vector2(r.x+cut,r.yMax-9),new Vector2(r.x+cut+55,r.yMax-9),new Vector2(r.x+cut+60,r.yMax-6),accent);
            ThermalGaugeGraphic.Quad(vh,new Vector2(r.xMax-cut-55,r.y+6),new Vector2(r.xMax-cut-60,r.y+9),new Vector2(r.xMax-cut,r.y+9),new Vector2(r.xMax-cut,r.y+6),accent);
        }

        // ---- Dymo tape: tiny chamfer, raised inner border, light top edge, dark underside
        void Label(UnityEngine.UI.VertexHelper vh,Rect r)
        {
            const float k=3;
            Polygon(vh,r,rim,k);
            var body=new Rect(r.x+1.5f,r.y+1.5f,r.width-3,r.height-3);Polygon(vh,body,color,k);
            var light=new Color(1,1,1,.16f);var shade=new Color(0,0,0,.45f);var rib=new Color(1,1,1,.11f);
            ThermalGaugeGraphic.Quad(vh,new Vector2(body.x+k,body.yMax),new Vector2(body.x+k,body.yMax-1.5f),new Vector2(body.xMax-k,body.yMax-1.5f),new Vector2(body.xMax-k,body.yMax),light);
            ThermalGaugeGraphic.Quad(vh,new Vector2(body.x+k,body.y+2.5f),new Vector2(body.x+k,body.y),new Vector2(body.xMax-k,body.y),new Vector2(body.xMax-k,body.y+2.5f),shade);
            // raised border of the tape, inset 5 px
            float i=5,t=1.5f;var b=new Rect(body.x+i,body.y+i,body.width-2*i,body.height-2*i);
            if(b.width>8&&b.height>8)
            {
                ThermalGaugeGraphic.Quad(vh,new Vector2(b.x,b.yMax),new Vector2(b.x,b.yMax-t),new Vector2(b.xMax,b.yMax-t),new Vector2(b.xMax,b.yMax),rib);
                ThermalGaugeGraphic.Quad(vh,new Vector2(b.x,b.y+t),new Vector2(b.x,b.y),new Vector2(b.xMax,b.y),new Vector2(b.xMax,b.y+t),rib);
                ThermalGaugeGraphic.Quad(vh,new Vector2(b.x,b.yMax-t),new Vector2(b.x,b.y+t),new Vector2(b.x+t,b.y+t),new Vector2(b.x+t,b.yMax-t),rib);
                ThermalGaugeGraphic.Quad(vh,new Vector2(b.xMax-t,b.yMax-t),new Vector2(b.xMax-t,b.y+t),new Vector2(b.xMax,b.y+t),new Vector2(b.xMax,b.yMax-t),rib);
            }
        }

        // ---- Steel sheet: dark edge, beveled face, hazard tape on top, rivets on the corners and edges
        void Sheet(UnityEngine.UI.VertexHelper vh,Rect r)
        {
            float k=Mathf.Min(cut,14);
            Polygon(vh,r,rim,k);
            float x0=r.x+3,x1=r.xMax-3,y0=r.y+3,y1=r.yMax-3;Polygon(vh,new Rect(x0,y0,x1-x0,y1-y0),color,k);
            var hi=new Color(1,1,1,.08f);var lo=new Color(0,0,0,.38f);const float b=2;
            ThermalGaugeGraphic.Quad(vh,new Vector2(x0+k,y1),new Vector2(x0+k,y1-b),new Vector2(x1-k,y1-b),new Vector2(x1-k,y1),hi);
            ThermalGaugeGraphic.Quad(vh,new Vector2(x0+k,y0+b),new Vector2(x0+k,y0),new Vector2(x1-k,y0),new Vector2(x1-k,y0+b),lo);
            ThermalGaugeGraphic.Quad(vh,new Vector2(x0,y1-k),new Vector2(x0,y0+k),new Vector2(x0+b,y0+k),new Vector2(x0+b,y1-k),hi);
            ThermalGaugeGraphic.Quad(vh,new Vector2(x1-b,y1-k),new Vector2(x1-b,y0+k),new Vector2(x1,y0+k),new Vector2(x1,y1-k),lo);
            bool large=r.width>=200&&r.height>=120;if(!large)return;
            // hazard tape: dark base plus orange parallelograms (skew 6, pitch 18)
            float ty1=y1-8,ty0=ty1-10,xs=x0+k+10,xe=x1-k-10;
            ThermalGaugeGraphic.Quad(vh,new Vector2(xs,ty1),new Vector2(xs,ty0),new Vector2(xe,ty0),new Vector2(xe,ty1),HazardDark);
            for(float sx=xs;sx+15<xe;sx+=18)
                ThermalGaugeGraphic.Quad(vh,new Vector2(sx+6,ty1),new Vector2(sx,ty0),new Vector2(sx+9,ty0),new Vector2(sx+15,ty1),Hazard);
            // rivets: four corners (below the tape at the top) and a pair every ~170 px along the sides
            float lx=x0+13,rx=x1-13,top=ty0-14,bottom=y0+13;
            Rivet(vh,new Vector2(lx,top));Rivet(vh,new Vector2(rx,top));Rivet(vh,new Vector2(lx,bottom));Rivet(vh,new Vector2(rx,bottom));
            for(float y=top-170;y>bottom+60;y-=170){Rivet(vh,new Vector2(lx,y));Rivet(vh,new Vector2(rx,y));}
        }

        static void Rivet(UnityEngine.UI.VertexHelper vh,Vector2 c)
        {
            Fan(vh,c+new Vector2(.7f,-.7f),4.2f,new Color(0,0,0,.45f));
            Fan(vh,c,4f,new Color(.27f,.3f,.32f,1));
            Fan(vh,c+new Vector2(-1,1),1.8f,new Color(.62f,.66f,.68f,.9f));
        }
        static void Fan(UnityEngine.UI.VertexHelper vh,Vector2 c,float radius,Color color)
        {
            int n=vh.currentVertCount;vh.AddVert(c,color,Vector2.zero);
            for(int i=0;i<6;i++){float a=i*Mathf.PI/3;vh.AddVert(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);}
            for(int i=0;i<6;i++)vh.AddTriangle(n,n+i+1,n+(i+1)%6+1);
        }

        void Polygon(UnityEngine.UI.VertexHelper vh,Rect r,Color c)=>Polygon(vh,r,c,Mathf.Min(cut,Mathf.Min(r.width,r.height)*.22f));
        static void Polygon(UnityEngine.UI.VertexHelper vh,Rect r,Color c,float k)
        {
            k=Mathf.Min(k,Mathf.Min(r.width,r.height)*.22f);int n=vh.currentVertCount;
            Vector2[] p={new Vector2(r.x+k,r.y),new Vector2(r.xMax-k,r.y),new Vector2(r.xMax,r.y+k),new Vector2(r.xMax,r.yMax-k),new Vector2(r.xMax-k,r.yMax),new Vector2(r.x+k,r.yMax),new Vector2(r.x,r.yMax-k),new Vector2(r.x,r.y+k)};
            vh.AddVert(r.center,c,Vector2.zero);for(int i=0;i<8;i++)vh.AddVert(p[i],c,Vector2.zero);for(int i=0;i<8;i++)vh.AddTriangle(n,n+i+1,n+(i+1)%8+1);
        }
    }
}
