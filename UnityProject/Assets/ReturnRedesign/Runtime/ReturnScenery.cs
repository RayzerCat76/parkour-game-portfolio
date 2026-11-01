using UnityEngine;

namespace Return.Redesign
{
    public sealed class ReturnScenery
    {
        readonly ReturnArt art;
        readonly Transform root;
        ReturnFigure memoryFigure;
        Transform memoryStoryFocus;
        Vector2 memoryStoryOffset;
        readonly System.Collections.Generic.List<Transform> shelfMemories=new System.Collections.Generic.List<Transform>();
        public System.Collections.Generic.IReadOnlyList<Transform> ShelfMemoryRoots=>shelfMemories;
        int memoryIdentity;
        float lastMemoryTime=-1,voiceBlend;
        static readonly Color Chalk=new Color(.75f,.79f,.77f,.24f),Dark=new Color(.027f,.035f,.039f);
        public ReturnScenery(ReturnArt a,Transform r){art=a;root=r;}
        Transform Group(string name,Vector2 at)
        {Transform t=new GameObject(name).transform;t.SetParent(root,false);t.localPosition=at;return t;}
        void Outline(Transform p,Vector2 at,float w,float h,Color c)
        {
            art.Line(p,at,at+Vector2.up*h,.035f,c,-9);art.Line(p,at+Vector2.up*h,at+new Vector2(w,h),.035f,c,-9);
            art.Line(p,at+new Vector2(w,h),at+Vector2.right*w,.035f,c,-9);art.Line(p,at+Vector2.right*w,at,.035f,c,-9);
        }
        Transform ShelfMemory(string name,Vector2 at){var memory=Group(name,at);shelfMemories.Add(memory);return memory;}
        public void RetireShelfMemories(){foreach(var memory in shelfMemories)memory.gameObject.SetActive(false);}
        public bool TryMemoryStoryFocus(out Vector3 position)
        {
            position=Vector3.zero;
            if(memoryStoryFocus==null||!memoryStoryFocus.gameObject.activeInHierarchy)return false;
            position=memoryStoryFocus.TransformPoint(memoryStoryOffset);return true;
        }
        public void Memory(int index,float goal,float floor)
        {
            if(index==0)
            {
                Transform room=Group("a kitchen remembered",new Vector2(22,-2));
                art.Shape("deep domestic wall",room,new Vector2(11,6),new Vector2(39,21),Dark,-18);
                for(int i=0;i<3;i++)
                {
                    Vector2 p=new Vector2(i*12-4,5);Outline(room,p,7,9,Chalk);
                    art.Line(room,p+new Vector2(3.5f,0),p+new Vector2(3.5f,9),.03f,Chalk,-8);
                    art.Line(room,p+new Vector2(0,5),p+new Vector2(7,5),.03f,Chalk,-8);
                    art.Polygon("window light",room,new[]{p,p+new Vector2(7,0),p+new Vector2(10,-7),p+new Vector2(-3,-7)},new Color(.7f,.8f,.75f,.027f),-16);
                }
                Transform lamp=ShelfMemory("the lamp left on",new Vector2(goal-4,floor));
                art.Line(lamp,new Vector2(0,0),new Vector2(0,3),.04f,Chalk,-5);
                art.Polygon("lamp shade",lamp,new[]{new Vector2(-.7f,2.3f),new Vector2(.7f,2.3f),new Vector2(.35f,3),new Vector2(-.35f,3)},new Color(.85f,.88f,.82f,.5f),-4);
                art.Glow(lamp,new Vector2(0,2.3f),4,new Color(.9f,.9f,.8f,.4f),-6);
                art.Line(lamp,new Vector2(-1.3f,0),new Vector2(1.3f,0),.04f,Chalk,-5);
                art.Polygon("light kept for the empty chair",lamp,new[]{new Vector2(-.55f,2.3f),new Vector2(.55f,2.3f),new Vector2(4.9f,0),new Vector2(-1.2f,0)},new Color(.8f,.83f,.8f,.045f),-10);
                Transform table=ShelfMemory("a cold cup and an empty chair",new Vector2(goal-.6f,floor));
                memoryStoryFocus=table;memoryStoryOffset=new Vector2(-.55f,1.1f);
                art.Line(table,new Vector2(-1.35f,.95f),new Vector2(.8f,.95f),.07f,Chalk*1.6f,-5);
                art.Line(table,new Vector2(-1.05f,.95f),new Vector2(-1.05f,0),.055f,Chalk,-5);
                art.Line(table,new Vector2(.55f,.95f),new Vector2(.55f,0),.055f,Chalk,-5);
                Outline(table,new Vector2(-.55f,.95f),.23f,.26f,Chalk*2);
                art.Line(table,new Vector2(-.32f,1.16f),new Vector2(-.23f,1.13f),.025f,Chalk*2,-5);
                art.Line(table,new Vector2(-.23f,1.13f),new Vector2(-.32f,1.02f),.025f,Chalk*2,-5);
                art.Line(table,new Vector2(-1.7f,.65f),new Vector2(-1.7f,1.5f),.055f,Chalk,-5);
                art.Line(table,new Vector2(-1.7f,.65f),new Vector2(-1.15f,.65f),.055f,Chalk,-5);
                art.Line(table,new Vector2(-1.7f,.65f),new Vector2(-1.7f,0),.055f,Chalk,-5);
                art.Line(table,new Vector2(-1.15f,.65f),new Vector2(-1.15f,0),.055f,Chalk,-5);
            }
            else if(index==1)
            {
                for(int i=0;i<6;i++)
                {
                    Transform tower=Group("stairwell wall",new Vector2(12+i*10,-8));
                    float height=13+i*2;art.Shape("tower",tower,new Vector2(4,height*.5f),new Vector2(8,height),Dark,-18);
                    for(int j=0;j<4;j++)Outline(tower,new Vector2(1,3+j*4),2.5f,2.7f,Chalk);
                    art.Line(tower,new Vector2(7,0),new Vector2(7,height),.025f,Chalk,-8);
                }
                Transform stairs=Group("stairs that never ended",new Vector2(26,-4));
                for(int i=0;i<18;i++)
                {art.Line(stairs,new Vector2(i*2,i*.7f),new Vector2(i*2+2,i*.7f),.04f,Chalk,-8);art.Line(stairs,new Vector2(i*2+2,i*.7f),new Vector2(i*2+2,(i+1)*.7f),.04f,Chalk,-8);}
                Transform threshold=ShelfMemory("the landing outside the door",new Vector2(goal+.4f,floor));
                memoryStoryFocus=threshold;memoryStoryOffset=new Vector2(.95f,1.8f);
                Outline(threshold,new Vector2(-1.35f,0),3,4.2f,Chalk*1.5f);
                Outline(threshold,new Vector2(-1.15f,.14f),2.6f,3.85f,Chalk);
                art.Line(threshold,new Vector2(.95f,1.8f),new Vector2(1.2f,1.8f),.055f,Chalk*2,-5);
                art.Shape("door number plate",threshold,new Vector2(.15f,3.3f),new Vector2(.48f,.22f),Chalk,-5);
                art.Line(threshold,new Vector2(-1.6f,.08f),new Vector2(1.9f,.08f),.045f,Chalk,-5);
            }
            else if(index==2)
            {
                for(int i=0;i<7;i++)
                {
                    Transform station=Group("station arch",new Vector2(8+i*10,-2));
                    art.Line(station,new Vector2(0,0),new Vector2(0,13),.14f,Chalk,-7);
                    art.Line(station,new Vector2(0,13),new Vector2(8,13),.1f,Chalk,-7);
                    art.Line(station,new Vector2(8,13),new Vector2(10,11),.1f,Chalk,-7);
                }
                Transform clock=Group("a clock without hands",new Vector2(goal-6,9));
                memoryStoryFocus=clock;memoryStoryOffset=Vector2.zero;
                art.Shape("clock disc",clock,Vector2.zero,Vector2.one*2.4f,Chalk,-7,true);
                art.Shape("clock void",clock,Vector2.zero,Vector2.one*2.2f,Dark,-6,true);
                for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));art.Line(clock,d*.8f,d, .035f,Chalk,-5);}
                Transform bench=ShelfMemory("someone is still waiting",new Vector2(goal+1,floor));
                art.Line(bench,new Vector2(-1.3f,.775f),new Vector2(1.5f,.775f),.075f,Chalk*1.7f,-4);
                art.Line(bench,new Vector2(-1.3f,1.35f),new Vector2(1.5f,1.35f),.06f,Chalk,-4);
                art.Line(bench,new Vector2(-1.1f,1.35f),new Vector2(-1.1f,0),.06f,Chalk,-4);
                art.Line(bench,new Vector2(1.25f,1.35f),new Vector2(1.25f,0),.06f,Chalk,-4);
            }
            else
            {
                for(int i=0;i<8;i++)
                {
                    Transform building=Group("rooftop city",new Vector2(5+i*10,-7));
                    float h=7+(i*7%8);art.Polygon("roof",building,new[]{new Vector2(0,0),new Vector2(0,h),new Vector2(3,h+2),new Vector2(8,h),new Vector2(8,0)},Dark,-18);
                    art.Line(building,new Vector2(0,h),new Vector2(3,h+2),.04f,Chalk,-8);
                    art.Line(building,new Vector2(3,h+2),new Vector2(8,h),.04f,Chalk,-8);
                    for(int j=0;2+j*3+1.6f<h-.25f;j++)Outline(building,new Vector2(1.5f,2+j*3),1.4f,1.6f,Chalk);
                }
                for(int i=0;i<5;i++)art.Line(root,new Vector2(20+i*10,15),new Vector2(27+i*10,4),.018f,new Color(.83f,.8f,.76f,.14f),-6);
                Transform roof=ShelfMemory("a hand across the rooftop",new Vector2(goal,floor));
                art.Line(roof,new Vector2(-3.7f,.62f),new Vector2(2.25f,.62f),.06f,Chalk,-7);
                for(int i=0;i<5;i++)art.Line(roof,new Vector2(-3.5f+i*1.35f,0),new Vector2(-3.5f+i*1.35f,.62f),.04f,Chalk,-7);
                Outline(roof,new Vector2(-4,0),1.5f,3.2f,Chalk);
                for(int i=0;i<8;i++)art.Line(roof,new Vector2(-5+i*1.1f,5.5f),new Vector2(-4.7f+i*1.1f,4.8f),.016f,Chalk*.7f,-6);
            }
            Transform presence=Presence(new Vector2(goal+1,floor),index);
            if(index==3){memoryStoryFocus=presence;memoryStoryOffset=new Vector2(.65f,1.02f);}
        }
        public void PoseMemory(float time,float voice)
        {
            if(memoryFigure==null)return;
            float dt=lastMemoryTime<0||time<lastMemoryTime?0:Mathf.Min(time-lastMemoryTime,.1f);lastMemoryTime=time;
            voiceBlend=Mathf.Lerp(voiceBlend,Mathf.Clamp01(voice),1-Mathf.Exp(-dt*14));
            memoryFigure.PoseMemory(time,memoryIdentity,voiceBlend);
        }
        public Transform Presence(Vector2 at,int identity)
        {
            Transform person=ShelfMemory("remembered presence "+identity,at);
            memoryFigure=new ReturnFigure(art,person,new Color(.78f,.82f,.79f,.58f),-3,identity);memoryIdentity=identity;
            person.localScale=new Vector3(-1.65f,1.65f,1);
            memoryFigure.PoseMemory(0,identity,0);
            return person;
        }
    }
}
