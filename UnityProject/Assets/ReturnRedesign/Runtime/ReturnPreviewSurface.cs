using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Return.Redesign
{
    // Windowless previews use the game camera and the same interface content as OnGUI.
    sealed class ReturnPreviewSurface : IDisposable
    {
        const int Width=1280,Height=720;
        readonly Camera camera;
        readonly bool cameraWasEnabled;
        readonly float originalAspect;
        readonly Material material;
        readonly Mesh mesh;
        readonly ReturnFocus focus;
        readonly Action previousReadbackRender,readbackRender;
        readonly List<Command> commands=new List<Command>();
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Vector2> uvs=new List<Vector2>();
        readonly List<int> triangles=new List<int>();
        public RenderTexture Target {get;}
        public int TextCommands {get;private set;}
        public int DrawCommands=>commands.Count;
        sealed class Command
        {
            public Rect rect,uv;
            public Texture texture;
            public Color color;
            public string text;
            public GUIStyle style;
            public Vector2 from,to;
            public float width;
            public bool line;
        }
        public ReturnPreviewSurface(Camera camera,Action drawInterface)
        {
            this.camera=camera;cameraWasEnabled=camera.enabled;originalAspect=camera.aspect;
            Shader shader=Resources.Load<Shader>("ReturnPreviewUI");
            if(shader==null)throw new InvalidOperationException("Preview interface shader is missing");
            material=new Material(shader);mesh=new Mesh{name="RETURN preview interface"};mesh.MarkDynamic();
            Target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){name="RETURN windowless preview",antiAliasing=1};
            if(!Target.Create())throw new InvalidOperationException("Preview render target could not be created");
            camera.enabled=false;camera.aspect=Width/(float)Height;
            focus=camera.GetComponent<ReturnFocus>();
            if(focus!=null){previousReadbackRender=focus.RenderForReadback;readbackRender=()=>Render(drawInterface);focus.RenderForReadback=readbackRender;}
            Debug.Log("[RETURN offscreen preview] Target ready1280x720; scene and shared interface");
        }
        public void Rect(Rect rect,Texture texture,Color color,Rect? uv=null)
        {commands.Add(new Command{rect=rect,texture=texture,color=color,uv=uv??new Rect(0,0,1,1)});}
        public void Text(Rect rect,string text,GUIStyle style,Color color)
        {if(!string.IsNullOrEmpty(text)){commands.Add(new Command{rect=rect,text=text,style=style,color=color});TextCommands++;}}
        public void Line(Vector2 from,Vector2 to,float width,Color color)
        {commands.Add(new Command{from=from,to=to,width=width,color=color,line=true});}
        public void Render(Action drawInterface)
        {
            commands.Clear();TextCommands=0;drawInterface();
            foreach(var command in commands)if(command.text!=null)
                command.style.font.RequestCharactersInTexture(command.text,command.style.fontSize,command.style.fontStyle);
            RenderTexture previous=RenderTexture.active;bool previousSrgb=GL.sRGBWrite;
            try
            {
                camera.aspect=Width/(float)Height;
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=Target};
                if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("URP offscreen camera request is unsupported");
                RenderPipeline.SubmitRenderRequest(camera,request);
                Graphics.SetRenderTarget(Target);GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0,Width,Height,0);
                    foreach(var command in commands)
                    {
                        vertices.Clear();uvs.Clear();triangles.Clear();
                        if(command.text!=null)DrawText(command);
                        else if(command.line)
                        {
                            Vector2 d=(command.to-command.from).normalized,n=new Vector2(-d.y,d.x)*command.width*.5f;
                            Quad(command.from-n,command.from+n,command.to+n,command.to-n,new Rect(0,0,1,1));
                        }
                        else Quad(new Vector2(command.rect.xMin,command.rect.yMin),new Vector2(command.rect.xMin,command.rect.yMax),new Vector2(command.rect.xMax,command.rect.yMax),new Vector2(command.rect.xMax,command.rect.yMin),command.uv);
                        if(vertices.Count==0)continue;
                        mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);
                        material.SetTexture("_MainTex",command.text!=null?command.style.font.material.mainTexture:command.texture??Texture2D.whiteTexture);
                        material.SetColor("_Tint",command.color);material.SetFloat("_Font",command.text!=null?1:0);
                        if(!material.SetPass(0))throw new InvalidOperationException("Preview interface shader pass is unavailable");
                        Graphics.DrawMeshNow(mesh,Matrix4x4.identity);
                    }
                }
                finally{GL.PopMatrix();}
            }
            finally{GL.sRGBWrite=previousSrgb;RenderTexture.active=previous;}
        }
        void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Rect uv)
        {
            int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            uvs.Add(new Vector2(uv.xMin,uv.yMax));uvs.Add(new Vector2(uv.xMin,uv.yMin));uvs.Add(new Vector2(uv.xMax,uv.yMin));uvs.Add(new Vector2(uv.xMax,uv.yMax));
            triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);triangles.Add(i);triangles.Add(i+2);triangles.Add(i+3);
        }
        float Measure(string text,GUIStyle style)
        {float width=0;foreach(char c in text)if(style.font.GetCharacterInfo(c,out CharacterInfo info,style.fontSize,style.fontStyle))width+=info.advance;return width;}
        void DrawText(Command command)
        {
            GUIStyle style=command.style;Rect r=command.rect;var padding=style.padding;
            r=new Rect(r.x+padding.left,r.y+padding.top,Mathf.Max(1,r.width-padding.horizontal),Mathf.Max(1,r.height-padding.vertical));
            var lines=new List<string>();
            foreach(string paragraph in command.text.Split('\n'))
            {
                string line="";
                foreach(string word in paragraph.Split(' '))
                {
                    string next=line.Length==0?word:line+" "+word;
                    if(style.wordWrap&&line.Length>0&&Measure(next,style)>r.width){lines.Add(line);line=word;}else line=next;
                }
                lines.Add(line);
            }
            float ascent=0,descent=0;
            foreach(char c in command.text)if(style.font.GetCharacterInfo(c,out CharacterInfo glyph,style.fontSize,style.fontStyle)){ascent=Mathf.Max(ascent,glyph.maxY);descent=Mathf.Min(descent,glyph.minY);}
            float glyphHeight=ascent-descent,lineHeight=Mathf.Max(glyphHeight,Mathf.Max(style.fontSize,style.lineHeight)),height=lines.Count*lineHeight;
            int vertical=(int)style.alignment/3,horizontal=(int)style.alignment%3;
            float y=r.y+(vertical==1?Mathf.Max(0,(r.height-height)*.5f):vertical==2?Mathf.Max(0,r.height-height):0);
            foreach(string line in lines)
            {
                float width=Measure(line,style),x=r.x+(horizontal==1?(r.width-width)*.5f:horizontal==2?r.width-width:0),baseline=y+ascent+(lineHeight-glyphHeight)*.5f;
                foreach(char c in line)
                {
                    if(!style.font.GetCharacterInfo(c,out CharacterInfo info,style.fontSize,style.fontStyle))continue;
                    float left=x+info.minX,right=x+info.maxX,top=baseline-info.maxY,bottom=baseline-info.minY;
                    float clippedLeft=Mathf.Max(left,r.xMin),clippedRight=Mathf.Min(right,r.xMax),clippedTop=Mathf.Max(top,r.yMin),clippedBottom=Mathf.Min(bottom,r.yMax);
                    if(clippedRight>clippedLeft&&clippedBottom>clippedTop)
                    {
                        float u0=(clippedLeft-left)/(right-left),u1=(clippedRight-left)/(right-left),v0=(clippedTop-top)/(bottom-top),v1=(clippedBottom-top)/(bottom-top);
                        Vector2 topLeft=Vector2.Lerp(info.uvTopLeft,info.uvTopRight,u0),topRight=Vector2.Lerp(info.uvTopLeft,info.uvTopRight,u1),bottomLeft=Vector2.Lerp(info.uvBottomLeft,info.uvBottomRight,u0),bottomRight=Vector2.Lerp(info.uvBottomLeft,info.uvBottomRight,u1);
                        int i=vertices.Count;vertices.Add(new Vector3(clippedLeft,clippedTop,0));vertices.Add(new Vector3(clippedLeft,clippedBottom,0));vertices.Add(new Vector3(clippedRight,clippedBottom,0));vertices.Add(new Vector3(clippedRight,clippedTop,0));
                        uvs.Add(Vector2.Lerp(topLeft,bottomLeft,v0));uvs.Add(Vector2.Lerp(topLeft,bottomLeft,v1));uvs.Add(Vector2.Lerp(topRight,bottomRight,v1));uvs.Add(Vector2.Lerp(topRight,bottomRight,v0));
                        triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);triangles.Add(i);triangles.Add(i+2);triangles.Add(i+3);
                    }
                    x+=info.advance;
                }
                y+=lineHeight;
            }
        }
        public void Dispose()
        {
            if(focus!=null&&focus.RenderForReadback==readbackRender)focus.RenderForReadback=previousReadbackRender;
            if(camera!=null){camera.enabled=cameraWasEnabled;camera.aspect=originalAspect;}
            if(RenderTexture.active==Target)RenderTexture.active=null;
            Target.Release();UnityEngine.Object.Destroy(Target);UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(material);
        }
    }
}
