using UnityEngine;

namespace Return.Redesign
{
    // An ink silhouette articulated at shoulders, elbows, hips, knees and feet.
    public sealed class ReturnFigure
    {
        public readonly Transform root;
        readonly Transform torso,head,scarf;
        readonly bool traveller;
        readonly bool cleanTraveller;
        bool keyHands;
        Vector2 keyHand;
        public bool CleanTraveller=>cleanTraveller;
        public Vector2 PocketPosition=>new Vector2(.06f,.66f);
        public bool HandsVisible=>strokes[4].gameObject.activeInHierarchy||strokes[5].gameObject.activeInHierarchy||strokes[6].gameObject.activeInHierarchy||strokes[7].gameObject.activeInHierarchy;
        public void KeyHandPose(Vector2 position,bool visible){keyHand=position;keyHands=visible;}
        Transform memoryProp;
        const int SteamSegments=7;
        Transform[] steam;
        Renderer[] steamRenderers;
        MaterialPropertyBlock steamBlock;
        Color steamInk;
        readonly Transform[] strokes=new Transform[13];
        readonly Transform[] ends=new Transform[26];
        readonly float[] widths={.07f,.06f,.07f,.06f,.055f,.045f,.055f,.045f,.032f,.032f,.055f,.055f,.04f};
        float previousTime=-1,stride,speedBlend,walkBlend;
        public ReturnFigure(ReturnArt art,Transform parent,Color ink,int order=20,int rememberedIdentity=-1,bool cleanTraveller=false)
        {
            root=new GameObject("articulated traveller").transform;root.SetParent(parent,false);
            traveller=rememberedIdentity<0;
            this.cleanTraveller=cleanTraveller&&traveller;
            if(traveller){widths[0]=widths[2]=.17f;widths[1]=widths[3]=.11f;widths[4]=widths[6]=.105f;widths[5]=widths[7]=.075f;widths[12]=.08f;}
            Color fill=new Color(ink.r,ink.g,ink.b,.14f);
            Vector2[] garment=traveller?new[]{new Vector2(-.13f,0),new Vector2(.13f,0),new Vector2(.17f,.34f),new Vector2(.11f,.42f),new Vector2(-.11f,.42f),new Vector2(-.17f,.34f)}:new[]{new Vector2(-.15f,0),new Vector2(.15f,0),new Vector2(.11f,.42f),new Vector2(-.11f,.42f)};
            if(this.cleanTraveller)garment=new[]{new Vector2(-.23f,-.31f),new Vector2(.23f,-.31f),new Vector2(.22f,.22f),new Vector2(.17f,.42f),new Vector2(-.17f,.42f),new Vector2(-.22f,.22f)};
            torso=art.Polygon(this.cleanTraveller?"white travelling coat":"ink garment",root,garment,this.cleanTraveller?ink:traveller?new Color(ink.r,ink.g,ink.b,ink.a*.88f):fill,order+(traveller?(this.cleanTraveller?3:2):0));
            head=new GameObject("outlined face").transform;head.SetParent(root,false);
            if(this.cleanTraveller)DressQuietTraveller(art,ink,order);
            else if(traveller)DressTraveller(art,ink,order);
            else
            {
                art.Shape("quiet face",head,Vector2.zero,new Vector2(.24f,.28f),new Color(ink.r,ink.g,ink.b,.1f),order+1,true);
                for(int i=0;i<24;i++)
                {
                    float a=i*Mathf.PI*2/24,b=(i+1)*Mathf.PI*2/24;
                    art.Line(head,new Vector2(Mathf.Cos(a)*.12f,Mathf.Sin(a)*.14f),new Vector2(Mathf.Cos(b)*.12f,Mathf.Sin(b)*.14f),.025f,ink,order+2);
                }
            }
            scarf=art.Polygon("loose thread",root,new[]{new Vector2(-.04f,.05f),new Vector2(-.34f,.04f),new Vector2(-.62f,-.02f),new Vector2(-.50f,-.09f),new Vector2(-.04f,-.03f)},new Color(ink.r,ink.g,ink.b,.4f),order);
            if(this.cleanTraveller)scarf.gameObject.SetActive(false);
            for(int i=0;i<strokes.Length;i++)
            {
                bool behind=i<2||i==6||i==7||i==10;
                Color limb=traveller?new Color(ink.r,ink.g,ink.b,ink.a*(behind?.48f:.94f)):ink;
                int layer=traveller?order+(behind?0:4):order+1;
                strokes[i]=traveller?art.Polygon("tapered limb",root,new[]{new Vector2(-.5f,-.35f),new Vector2(.37f,-.28f),new Vector2(.5f,0),new Vector2(.37f,.28f),new Vector2(-.5f,.35f)},limb,layer):art.Line(root,Vector2.zero,Vector2.up,widths[i],ink,layer);
                ends[i*2]=art.Shape("rounded joint",root,Vector2.zero,Vector2.one*widths[i]*(traveller?.7f:1),limb,layer,true);
                ends[i*2+1]=art.Shape("rounded joint",root,Vector2.zero,Vector2.one*widths[i]*(traveller?.56f:1),limb,layer,true);
                if(traveller&&(i==8||i==9)){strokes[i].gameObject.SetActive(false);ends[i*2].gameObject.SetActive(false);ends[i*2+1].gameObject.SetActive(false);}
            }
            Pose(0,Vector2.zero,true,false,false,false,0,0);
            if(rememberedIdentity>=0)DressMemory(art,ink,order,rememberedIdentity);
        }
        void DressQuietTraveller(ReturnArt art,Color ink,int order)
        {
            art.Polygon("white hood",head,new[]{new Vector2(-.13f,-.13f),new Vector2(.12f,-.13f),new Vector2(.14f,.06f),new Vector2(.08f,.15f),new Vector2(-.08f,.15f),new Vector2(-.14f,.06f)},ink,order+5);
            art.Polygon("shadowed profile",head,new[]{new Vector2(.045f,-.055f),new Vector2(.13f,-.04f),new Vector2(.13f,.05f),new Vector2(.045f,.065f)},new Color(.045f,.045f,.045f),order+6);
            art.Shape("quiet eye",head,new Vector2(.097f,.015f),Vector2.one*.018f,ink,order+7,true);
            art.Line(torso,new Vector2(0,-.27f),new Vector2(0,.28f),.012f,new Color(.68f,.68f,.68f),order+4);
        }
        void DressTraveller(ReturnArt art,Color ink,int order)
        {
            Color shade=new Color(.025f,.025f,.025f,ink.a*.8f);
            art.Polygon("traveller profile",head,new[]{new Vector2(-.09f,-.12f),new Vector2(.04f,-.14f),new Vector2(.105f,-.07f),new Vector2(.115f,.035f),new Vector2(.07f,.13f),new Vector2(-.045f,.145f),new Vector2(-.115f,.07f),new Vector2(-.12f,-.035f)},ink,order+5);
            art.Polygon("profile nose",head,new[]{new Vector2(.1f,-.025f),new Vector2(.155f,0),new Vector2(.105f,.04f)},ink,order+5);
            art.Polygon("hair crown",head,new[]{new Vector2(-.115f,.035f),new Vector2(-.11f,.09f),new Vector2(-.045f,.145f),new Vector2(.07f,.13f),new Vector2(.1f,.075f),new Vector2(-.035f,.055f)},shade,order+6);
            art.Shape("quiet eye",head,new Vector2(.075f,.02f),new Vector2(.022f,.014f),shade,order+7,true);
            art.Line(head,new Vector2(.065f,-.075f),new Vector2(.092f,-.069f),.009f,shade,order+7);
            art.Line(torso,new Vector2(-.045f,.40f),new Vector2(.01f,.35f),.018f,shade,order+3);
            art.Line(torso,new Vector2(.01f,.35f),new Vector2(.065f,.40f),.018f,shade,order+3);
            art.Line(torso,new Vector2(-.13f,.045f),new Vector2(.13f,.045f),.014f,shade,order+3);
            art.Line(torso,new Vector2(.075f,.12f),new Vector2(.06f,.25f),.009f,new Color(0,0,0,ink.a*.3f),order+3);
        }
        static Vector2 GroundFoot(float phase,float weight,float idle,float cycleDistance,out Vector2 toe)
        {
            float cycle=Mathf.Repeat(phase/(Mathf.PI*2),1),stance=Mathf.Clamp(.48f/cycleDistance,.16f,.67f),x,y,angle;
            // The planted foot moves backward by the distance travelled during contact.
            if(cycle<stance)
            {
                float contact=cycle/stance;x=Mathf.Lerp(.24f,-.24f,contact);y=0;
                angle=12*(1-Mathf.SmoothStep(0,1,contact/.18f))-22*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,contact));
            }
            else
            {
                float swing=(cycle-stance)/(1-stance);x=Mathf.Lerp(-.24f,.24f,Mathf.SmoothStep(0,1,swing));y=Mathf.Sin(swing*Mathf.PI)*.12f;
                angle=Mathf.Lerp(-22,12,Mathf.SmoothStep(0,1,swing));
            }
            float pitch=angle*weight*Mathf.Deg2Rad;toe=new Vector2(Mathf.Cos(pitch),Mathf.Sin(pitch))*.12f;
            // Roll onto the toe at push-off without drawing it below the contact plane.
            return new Vector2(Mathf.Lerp(idle,x,weight),y*weight+Mathf.Max(0,-toe.y));
        }
        static Vector2 Knee(Vector2 hip,Vector2 foot)
        {
            Vector2 delta=foot-hip;float distance=Mathf.Clamp(delta.magnitude,.001f,.599f);
            Vector2 direction=delta.normalized;
            float bend=Mathf.Sqrt(Mathf.Max(0,.30f*.30f-distance*distance*.25f));
            return hip+direction*(distance*.5f)+new Vector2(-direction.y,direction.x)*bend;
        }
        void DressMemory(ReturnArt art,Color ink,int order,int identity)
        {
            scarf.gameObject.SetActive(false);
            Color cloth=new Color(ink.r,ink.g,ink.b,.17f);
            float hem=identity==0?-.22f:identity==1?-.08f:-.31f;
            art.Polygon("remembered clothing",torso,new[]{new Vector2(-.2f,hem),new Vector2(.2f,hem),new Vector2(.13f,.4f),new Vector2(-.13f,.4f)},cloth,order);
            art.Line(torso,new Vector2(-.2f,hem),new Vector2(-.13f,.4f),.024f,ink,order+1);
            art.Line(torso,new Vector2(.2f,hem),new Vector2(.13f,.4f),.024f,ink,order+1);
            art.Line(torso,new Vector2(-.2f,hem),new Vector2(.2f,hem),.023f,ink,order+1);
            art.Polygon("remembered profile",head,new[]{new Vector2(.095f,.025f),new Vector2(.15f,-.015f),new Vector2(.1f,-.04f)},ink,order+2);
            if(identity==0)
            {
                art.Line(torso,new Vector2(-.09f,.38f),new Vector2(.09f,.38f),.025f,ink,order+2);
                art.Line(torso,new Vector2(-.13f,.12f),new Vector2(.13f,.12f),.023f,ink,order+2);
                memoryProp=new GameObject("the warm cup").transform;memoryProp.SetParent(root,false);
                art.Shape("cup body",memoryProp,new Vector2(0,.06f),new Vector2(.15f,.13f),cloth,order+3);
                art.Line(memoryProp,new Vector2(-.075f,.13f),new Vector2(.075f,.13f),.018f,ink,order+4);
                art.Line(memoryProp,new Vector2(-.075f,.13f),new Vector2(-.06f,0),.018f,ink,order+4);
                art.Line(memoryProp,new Vector2(-.06f,0),new Vector2(.06f,0),.018f,ink,order+4);
                art.Line(memoryProp,new Vector2(.06f,0),new Vector2(.075f,.13f),.018f,ink,order+4);
                art.Line(memoryProp,new Vector2(.075f,.1f),new Vector2(.13f,.09f),.018f,ink,order+4);
                art.Line(memoryProp,new Vector2(.13f,.09f),new Vector2(.075f,.035f),.018f,ink,order+4);
                steam=new Transform[SteamSegments*3];steamRenderers=new Renderer[steam.Length];steamBlock=new MaterialPropertyBlock();
                steamInk=new Color(ink.r,ink.g,ink.b,ink.a*.75f);
                for(int i=0;i<steam.Length;i++)
                {
                    steam[i]=art.Line(memoryProp,Vector2.zero,Vector2.up,.018f,steamInk,order+5);
                    steam[i].name="rising warmth "+i;steamRenderers[i]=steam[i].GetComponent<Renderer>();
                }
            }
            else if(identity==1)
            {
                art.Line(torso,new Vector2(-.1f,.38f),new Vector2(.02f,.2f),.024f,ink,order+2);
                art.Line(torso,new Vector2(.1f,.38f),new Vector2(.02f,.2f),.024f,ink,order+2);
                art.Line(head,new Vector2(-.08f,.015f),new Vector2(.12f,.015f),.032f,ink,order+3);
            }
            else if(identity==2)
            {
                art.Line(torso,new Vector2(0,hem),new Vector2(0,.34f),.024f,ink,order+2);
                memoryProp=new GameObject("the unsent message").transform;memoryProp.SetParent(root,false);
                art.Shape("phone edge",memoryProp,new Vector2(0,.055f),new Vector2(.13f,.22f),ink,order+3);
                art.Shape("unanswered screen",memoryProp,new Vector2(0,.055f),new Vector2(.095f,.16f),new Color(.05f,.06f,.06f,1),order+4);
                art.Line(memoryProp,new Vector2(-.025f,.09f),new Vector2(.025f,.09f),.012f,ink,order+5);
            }
            else
            {
                art.Line(torso,new Vector2(0,hem),new Vector2(0,.4f),.025f,ink,order+2);
                art.Line(head,new Vector2(-.14f,-.1f),new Vector2(-.17f,.12f),.04f,ink,order+1);
                art.Line(head,new Vector2(-.17f,.12f),new Vector2(-.02f,.18f),.04f,ink,order+1);
                art.Line(head,new Vector2(-.02f,.18f),new Vector2(.12f,.11f),.04f,ink,order+1);
            }
        }
        void Bone(int i,Vector2 a,Vector2 b)
        {
            Vector2 d=b-a;strokes[i].localPosition=(a+b)*.5f;strokes[i].localScale=new Vector3(d.magnitude,widths[i],1);
            strokes[i].localRotation=Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
            ends[i*2].localPosition=a;ends[i*2+1].localPosition=b;
        }
        static Vector2 SteamPoint(float u,float age,int wisp,float time)
        {
            float drift=Mathf.Sin(time*1.1f+wisp*2.1f+u*4.7f)*(.008f+.025f*u);
            return new Vector2((wisp-1)*.035f+drift,.145f+age*.16f+u*.28f);
        }
        void PoseSteam(float time)
        {
            if(steam==null)return;
            for(int wisp=0;wisp<3;wisp++)
            {
                float age=Mathf.Repeat(time*.38f+wisp/3f,1),fade=Mathf.Sin(age*Mathf.PI);
                for(int segment=0;segment<SteamSegments;segment++)
                {
                    int i=wisp*SteamSegments+segment;float u=segment/(float)SteamSegments,v=(segment+1f)/SteamSegments,mid=(u+v)*.5f;
                    Vector2 a=SteamPoint(u,age,wisp,time),b=SteamPoint(v,age,wisp,time),d=b-a;
                    steam[i].localPosition=(a+b)*.5f;steam[i].localScale=new Vector3(d.magnitude,.018f*(1-mid*.5f),1);
                    steam[i].localRotation=Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
                    Color ink=steamInk;ink.a*=fade*(1-mid*.85f);steamBlock.SetColor("_Color",ink);steamRenderers[i].SetPropertyBlock(steamBlock);
                }
            }
        }
        public void PoseMemory(float time,int identity,float speaking)
        {
            Pose(time,Vector2.zero,true,false,false,false,0,0);
            float breath=Mathf.Sin(time*2.4f)*.014f,voice=Mathf.Clamp01(speaking*2.4f);
            Vector2 hip=new Vector2(0,.56f),shoulder=new Vector2(0,.98f+breath);
            Vector2 kneeA=new Vector2(.04f,.28f),footA=new Vector2(.14f,0),kneeB=new Vector2(-.08f,.28f),footB=new Vector2(-.14f,0);
            Vector2 elbowA,handA,elbowB,handB;float tilt;
            switch(identity)
            {
                case 0:
                    shoulder.x=.035f;elbowA=new Vector2(.18f,.77f);handA=new Vector2(.35f,.88f+voice*.035f);
                    elbowB=new Vector2(.12f,.75f);handB=new Vector2(.29f,.83f+voice*.035f);tilt=-3;break;
                case 1:
                    shoulder=new Vector2(.08f,.92f+breath);elbowA=new Vector2(.2f,.73f);handA=new Vector2(-.04f,.82f);
                    elbowB=new Vector2(-.12f,.75f);handB=Vector2.Lerp(new Vector2(.15f,.86f),new Vector2(.38f,.9f),voice);tilt=9-voice*7;break;
                case 2:
                    hip=new Vector2(-.03f,.47f);shoulder=new Vector2(-.07f,.89f+breath);
                    kneeA=new Vector2(.27f,.43f);footA=new Vector2(.42f,0);kneeB=new Vector2(.31f,.4f);footB=new Vector2(.56f,0);
                    elbowA=new Vector2(.17f,.68f);handA=new Vector2(.3f,.65f);elbowB=new Vector2(.09f,.66f);handB=new Vector2(.24f,.65f);tilt=17-voice*13;break;
                default:
                    shoulder.x=.08f;elbowA=new Vector2(.28f,.92f);handA=new Vector2(.65f+voice*.035f,1.02f);
                    elbowB=new Vector2(.25f,.79f);handB=new Vector2(.55f+voice*.035f,.83f);tilt=-7;break;
            }
            Bone(0,hip,kneeA);Bone(1,kneeA,footA);Bone(2,hip,kneeB);Bone(3,kneeB,footB);
            Bone(4,shoulder,elbowA);Bone(5,elbowA,handA);Bone(6,shoulder,elbowB);Bone(7,elbowB,handB);
            Bone(8,hip+Vector2.left*.15f,shoulder+Vector2.left*.1f);Bone(9,hip+Vector2.right*.15f,shoulder+Vector2.right*.1f);
            Bone(10,footA,footA+Vector2.right*.12f);Bone(11,footB,footB+Vector2.right*.12f);
            torso.localPosition=hip;Vector2 spine=shoulder-hip;
            torso.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(spine.x,spine.y)*Mathf.Rad2Deg);torso.localScale=new Vector3(1,spine.magnitude/.42f,1);
            head.localPosition=shoulder+new Vector2(.015f,.21f);head.localRotation=Quaternion.Euler(0,0,tilt+Mathf.Sin(time*1.6f)*1.5f);
            Bone(12,shoulder+Vector2.up*.02f,(Vector2)head.localPosition+Vector2.down*.14f);
            if(memoryProp!=null){memoryProp.localPosition=handA+new Vector2(.015f,.015f);memoryProp.localRotation=Quaternion.Euler(0,0,identity==2?14:-3);}
            PoseSteam(time);
        }
        public void Pose(float time,Vector2 velocity,bool grounded,bool carrying,bool resting,bool bursting,float landing,float reach)
        {
            float dt=previousTime<0||time<previousTime?0:Mathf.Min(time-previousTime,.1f);previousTime=time;
            speedBlend=Mathf.Lerp(speedBlend,Mathf.Clamp01(Mathf.Abs(velocity.x)/6.8f),1-Mathf.Exp(-dt*16));
            float cycleDistance=Mathf.Lerp(.9f,2.9f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.5f,6.8f,Mathf.Abs(velocity.x))));
            walkBlend=Mathf.MoveTowards(walkBlend,grounded&&Mathf.Abs(velocity.x)>.025f?1:0,dt*8);
            if(grounded)stride+=velocity.x*dt*Mathf.PI*2/cycleDistance;
            float speed=speedBlend,gait=Mathf.Sin(stride)*speed;
            float lift=grounded?Mathf.Abs(gait)*(traveller?.015f:.055f):0,breath=Mathf.Sin(time*2.4f)*.014f;
            float squash=landing*.14f;
            Vector2 hip=new Vector2(grounded?speed*(traveller?.02f:.07f):bursting?.22f:.05f,(traveller&&grounded?.59f:.56f)+lift-squash);
            Vector2 shoulder=hip+new Vector2(speed*.06f, .42f+breath-squash*.3f);
            if(bursting)shoulder.x+=.26f;
            Vector2 leftKnee,rightKnee,leftFoot,rightFoot,leftToe=Vector2.right*.12f,rightToe=Vector2.right*.12f;
            if(grounded)
            {
                leftFoot=new Vector2(gait*.28f,Mathf.Max(0,gait)*.18f);rightFoot=new Vector2(-gait*.28f,Mathf.Max(0,-gait)*.18f);
                leftKnee=Vector2.Lerp(hip,leftFoot,.52f)+Vector2.right*.1f;rightKnee=Vector2.Lerp(hip,rightFoot,.52f)+Vector2.right*.1f;
                if(traveller)
                {
                    leftFoot=GroundFoot(stride,walkBlend,-.08f,cycleDistance,out leftToe);rightFoot=GroundFoot(stride+Mathf.PI,walkBlend,.09f,cycleDistance,out rightToe);
                    // Fit the pelvis to two .30-unit leg segments instead of bending oversized legs forward.
                    const float legReach=.598f;
                    float leftHeight=leftFoot.y+Mathf.Sqrt(Mathf.Max(0,legReach*legReach-(leftFoot.x-hip.x)*(leftFoot.x-hip.x)));
                    float rightHeight=rightFoot.y+Mathf.Sqrt(Mathf.Max(0,legReach*legReach-(rightFoot.x-hip.x)*(rightFoot.x-hip.x)));
                    float drop=Mathf.Max(0,hip.y-Mathf.Min(leftHeight,rightHeight));hip.y-=drop;shoulder.y-=drop;
                    leftKnee=Knee(hip,leftFoot);rightKnee=Knee(hip,rightFoot);gait=(leftFoot.x-rightFoot.x)*1.6f;
                }
            }
            else
            {
                float rising=Mathf.Clamp(velocity.y/12,-1,1);
                leftKnee=hip+new Vector2(-.17f,-.19f);leftFoot=leftKnee+new Vector2(-.15f,-.23f);
                rightKnee=hip+new Vector2(.21f,-.1f+rising*.08f);rightFoot=rightKnee+new Vector2(-.12f,-.27f);
            }
            Vector2 elbowA=shoulder+new Vector2(-gait*.15f,-.22f),handA=elbowA+new Vector2(-gait*.12f,-.18f);
            Vector2 elbowB=shoulder+new Vector2(gait*.15f,-.22f),handB=elbowB+new Vector2(gait*.12f,-.18f);
            if(carrying&&!cleanTraveller){elbowA=shoulder+new Vector2(.2f,-.12f);handA=shoulder+new Vector2(.46f,.1f);elbowB=shoulder+new Vector2(.12f,-.21f);handB=handA+Vector2.down*.1f;}
            if(reach>0&&!cleanTraveller){elbowA=Vector2.Lerp(elbowA,shoulder+new Vector2(.24f,.1f),reach);handA=Vector2.Lerp(handA,shoulder+new Vector2(.38f,.38f),reach);}
            if(cleanTraveller&&keyHands){handA=keyHand;elbowA=Vector2.Lerp(shoulder,handA,.52f)+Vector2.down*.06f;handB=handA+new Vector2(-.035f,-.055f);elbowB=Vector2.Lerp(shoulder,handB,.48f)+Vector2.down*.08f;}
            if(resting){leftKnee=hip+new Vector2(.08f,-.27f);rightKnee=hip+new Vector2(-.02f,-.28f);leftFoot=leftKnee+Vector2.down*.26f;rightFoot=rightKnee+Vector2.down*.24f;}
            Bone(0,hip,leftKnee);Bone(1,leftKnee,leftFoot);Bone(2,hip,rightKnee);Bone(3,rightKnee,rightFoot);
            Bone(4,shoulder,elbowA);Bone(5,elbowA,handA);Bone(6,shoulder,elbowB);Bone(7,elbowB,handB);
            Bone(8,hip+Vector2.left*.15f,shoulder+Vector2.left*.1f);Bone(9,hip+Vector2.right*.15f,shoulder+Vector2.right*.1f);
            Bone(10,leftFoot,leftFoot+leftToe);Bone(11,rightFoot,rightFoot+rightToe);
            head.localPosition=shoulder+new Vector2(.01f,.21f);head.localRotation=Quaternion.Euler(0,0,grounded?Mathf.Sin(time*2)*2:velocity.y*-.5f);
            Bone(12,shoulder+Vector2.up*.02f,(Vector2)head.localPosition+Vector2.down*.14f);
            Vector2 spine=shoulder-hip;
            torso.localPosition=hip;torso.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(spine.x,spine.y)*Mathf.Rad2Deg);torso.localScale=new Vector3(1,spine.magnitude/.42f,1);
            scarf.localPosition=shoulder;
            scarf.localRotation=Quaternion.Euler(0,0,Mathf.Sin(time*7)*5+velocity.y*1.2f+(bursting?-15:0));
            if(cleanTraveller)
                for(int i=4;i<=7;i++){strokes[i].gameObject.SetActive(keyHands);ends[i*2].gameObject.SetActive(keyHands);ends[i*2+1].gameObject.SetActive(keyHands);}
        }
    }
}
