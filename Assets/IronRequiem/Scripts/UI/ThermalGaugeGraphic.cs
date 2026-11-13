using UnityEngine;
namespace IronRequiem
{
    // Baked uGUI mesh: no textures, materials or per-frame mesh rebuild while stable.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ThermalGaugeGraphic : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField, Range(0,1)] float fill;
        [SerializeField] bool locked;
        public void SetState(float value, bool blocked)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(value-fill)<.001f && locked==blocked) return;
            fill=value;locked=blocked;SetVerticesDirty();
        }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear(); var origin = rectTransform.rect.min;
            const int count=48;
            for(int i=0;i<count;i++)
            {
                float a=i/(float)count,b=(i+1)/(float)count;
                Band(vh,origin,a,b,49+(i%7==0?3:0),new Color(.07f,.09f,.105f));
                Band(vh,origin,a,b,40,new Color(i%5==0?.36f:.27f,.29f,.30f));
                Band(vh,origin,a,b,30,new Color(.08f,.085f,.09f));
                Band(vh,origin,a,b,19,new Color(.18f,.045f,.018f));
                if(a<fill)
                {
                    var hot=Color.Lerp(new Color(1,.8f,.10f),new Color(1,.10f,.025f),fill);
                    Band(vh,origin,a,Mathf.Min(b,fill),19,hot);
                    Band(vh,origin,a,Mathf.Min(b,fill),7,Color.Lerp(hot,new Color(1,.95f,.61f),.65f));
                }
            }
            for(int i=0;i<9;i++)
            {
                float t=.035f+i*.115f;Vector2 p=Point(t),d=(p-new Vector2(155,105)).normalized;
                Line(vh,origin+p+d*11,origin+p+d*22+new Vector2(3,-3),2.5f,new Color(.025f,.035f,.04f));
                Line(vh,origin+p+d*22+new Vector2(3,-3),origin+p+d*26+new Vector2(0,4),2,new Color(.025f,.035f,.04f));
            }
        }
        static Vector2 Point(float t){float a=Mathf.Lerp(235,145,t)*Mathf.Deg2Rad;return new Vector2(155,105)+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*94;}
        static void Band(UnityEngine.UI.VertexHelper vh,Vector2 origin,float a,float b,float width,Color c)
        {
            Vector2 p=Point(a),q=Point(b),n=(p-new Vector2(155,105)).normalized*width*.5f,m=(q-new Vector2(155,105)).normalized*width*.5f;
            Quad(vh,origin+p-n,origin+p+n,origin+q+m,origin+q-m,c);
        }
        public static void Quad(UnityEngine.UI.VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color col)
        {
            int n=vh.currentVertCount;vh.AddVert(a,col,Vector2.zero);vh.AddVert(b,col,Vector2.zero);vh.AddVert(c,col,Vector2.zero);vh.AddVert(d,col,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        static void Line(UnityEngine.UI.VertexHelper vh,Vector2 a,Vector2 b,float w,Color c){Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*w*.5f;Quad(vh,a-n,a+n,b+n,b-n,c);}
    }
}
