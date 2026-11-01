using System.Collections.Generic;
using UnityEngine;

namespace Return.Redesign
{
    public sealed class ReturnArt : System.IDisposable
    {
        readonly Dictionary<Color, Material> inks = new Dictionary<Color, Material>();
        readonly Dictionary<Color, Material> lights = new Dictionary<Color, Material>();
        readonly Dictionary<string, Material> pictures = new Dictionary<string, Material>();
        readonly List<Mesh> owned = new List<Mesh>();
        readonly Shader shader;
        public bool Stark {get;set;}
        readonly Mesh quad, disc;
        public ReturnArt()
        {
            shader = Shader.Find("Return/Ink");
            quad = Mesh(new[] {new Vector2(-.5f,-.5f),new Vector2(.5f,-.5f),new Vector2(.5f,.5f),new Vector2(-.5f,.5f)});
            Vector2[] points = new Vector2[48];
            for (int i=0;i<points.Length;i++) { float a=i*Mathf.PI*2/points.Length; points[i]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.5f; }
            disc=Mesh(points);
        }
        Mesh Mesh(Vector2[] points)
        {
            Mesh mesh = new Mesh(); Vector3[] vertices=new Vector3[points.Length];
            for(int i=0;i<points.Length;i++) vertices[i]=points[i];
            int[] triangles=new int[(points.Length-2)*3];
            for(int i=0;i<points.Length-2;i++) {triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=i+2;}
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateBounds(); owned.Add(mesh);return mesh;
        }
        Material Ink(Color color)
        {
            if(Stark&&!(color.r>.6f&&color.g>.4f&&color.b<.6f))
            {float value=color.maxColorComponent>.45f?1:0;color=new Color(value,value,value,color.a);}
            if(!inks.TryGetValue(color,out Material mat)) {mat=new Material(shader);mat.SetColor("_Color",color);inks[color]=mat;} return mat;
        }
        public Transform Shape(string name, Transform parent, Vector2 at, Vector2 size, Color color, int order, bool round=false)
        {
            GameObject go=new GameObject(name);go.transform.SetParent(parent,false);
            go.transform.localPosition=new Vector3(at.x,at.y,0);go.transform.localScale=new Vector3(size.x,size.y,1);
            go.AddComponent<MeshFilter>().sharedMesh=round?disc:quad;
            MeshRenderer r=go.AddComponent<MeshRenderer>();r.sharedMaterial=Ink(color);r.sortingOrder=order;return go.transform;
        }
        public Transform Polygon(string name, Transform parent, Vector2[] points, Color color, int order)
        {
            GameObject go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=Mesh(points);
            MeshRenderer r=go.AddComponent<MeshRenderer>();r.sharedMaterial=Ink(color);r.sortingOrder=order;return go.transform;
        }
        public Transform Ribbon(string name,Transform parent,Vector2[] points,Color color,int order)
        {
            Transform ribbon=Polygon(name,parent,points,color,order);
            int[] triangles=new int[(points.Length/2-1)*6];
            for(int i=0;i<points.Length/2-1;i++)
            {int a=i*2,k=i*6;triangles[k]=a;triangles[k+1]=a+2;triangles[k+2]=a+1;triangles[k+3]=a+1;triangles[k+4]=a+2;triangles[k+5]=a+3;}
            Mesh mesh=ribbon.GetComponent<MeshFilter>().sharedMesh;mesh.triangles=triangles;
            var uv=new Vector2[points.Length];
            for(int i=0;i<points.Length/2;i++){float along=i/(float)(points.Length/2-1);uv[i*2]=new Vector2(0,along);uv[i*2+1]=new Vector2(1,along);}
            mesh.uv=uv;return ribbon;
        }
        public Transform TexturedMesh(string name,Transform parent,Vector2[] points,int[] triangles,Vector2[] uv,string resource,Vector4 atlas,Color color,int order)
        {
            string key="rock/"+resource+"/"+atlas.ToString("F3")+"/"+color.ToString("F3");
            if(!pictures.TryGetValue(key,out Material material)) {
                var texture=Resources.Load<Texture2D>(resource);if(texture==null)throw new System.InvalidOperationException("Rock texture missing: "+resource);
                material=new Material(shader);material.SetColor("_Color",color);material.SetTexture("_MainTex",texture);material.SetFloat("_AuthoredUV",1);material.SetVector("_RockAtlasRect",atlas);pictures[key]=material;
            }
            var go=new GameObject(name);go.transform.SetParent(parent,false);var mesh=new Mesh{name=name};var vertices=new Vector3[points.Length];for(int i=0;i<points.Length;i++)vertices[i]=points[i];
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();owned.Add(mesh);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;return go.transform;
        }
        public Transform Line(Transform parent, Vector2 from, Vector2 to, float width, Color color, int order)
        {
            Vector2 delta=to-from;Transform t=Shape("thread",parent,(from+to)*.5f,new Vector2(delta.magnitude,width),color,order);
            t.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);return t;
        }
        public Transform Glow(Transform parent, Vector2 at, float radius, Color color, int order)
        {
            Transform root=new GameObject("light bloom").transform;root.SetParent(parent,false);root.localPosition=at;
            if(Stark)return root;
            Color c=color;c.a*=.5f;
            if(!lights.TryGetValue(c,out Material glow)){glow=new Material(shader);glow.SetColor("_Color",c);glow.SetFloat("_Soft",1);lights[c]=glow;}
            Transform halo=Shape("soft halo",root,Vector2.zero,Vector2.one*radius*2,c,order);
            halo.GetComponent<MeshRenderer>().sharedMaterial=glow;
            return root;
        }
        public Transform Bitmap(string name,Transform parent,Vector2 at,Vector2 size,string resource,int order,float clipTop=1)
        {
            string key=resource+"/"+clipTop;
            if(!pictures.TryGetValue(key,out Material material))
            {
                Texture2D texture=Resources.Load<Texture2D>("ReturnArt/"+resource);
                if(texture==null)throw new System.InvalidOperationException("Missing painted RETURN layer: "+resource);
                material=new Material(shader);material.SetColor("_Color",Color.white);material.SetTexture("_MainTex",texture);material.SetFloat("_ClipTop",clipTop);pictures[key]=material;
            }
            Transform t=Shape(name,parent,at,size,Color.white,order);t.GetComponent<MeshRenderer>().sharedMaterial=material;return t;
        }
        static void Release(Object asset){if(Application.isPlaying)Object.Destroy(asset);else Object.DestroyImmediate(asset);}
        public void Dispose() {foreach(var material in inks.Values) Release(material);foreach(var material in lights.Values) Release(material);foreach(var material in pictures.Values) Release(material);foreach(Mesh m in owned) Release(m);inks.Clear();lights.Clear();pictures.Clear();owned.Clear();}
    }
}
