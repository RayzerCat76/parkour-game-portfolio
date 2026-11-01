using System.Collections.Generic;
using UnityEngine;

namespace Return.Redesign
{
    public enum Movement { Fragments, Rupture, StillWater, Reunion, Hospital, Credits }
    public sealed class ReturnHazard
    {
        public float x,y,timer=-1;
        public bool debris;
        public ReturnSurface forecastSurface;
        public Transform visual,warning,impact,ejection,stone;
        public bool impactCounted;
    }
    public sealed class ReturnWorld
    {
        public readonly List<ReturnSurface> surfaces=new List<ReturnSurface>();
        public readonly List<ReturnHazard> hazards=new List<ReturnHazard>();
        sealed class RuptureRock
        {
            public readonly List<Transform> pieces=new List<Transform>(),cracks=new List<Transform>();
            public readonly List<Vector3> origins=new List<Vector3>();
            public float damage;
        }
        readonly Dictionary<ReturnSurface,RuptureRock> ruptureRocks=new Dictionary<ReturnSurface,RuptureRock>();
        readonly Dictionary<ReturnSurface,Transform> memoryFuses=new Dictionary<ReturnSurface,Transform>();
        public readonly List<Transform> motes=new List<Transform>();
        public Transform root,far,near,player,head,body,legA,legB,scarf,fragment,beacon,door,fruit,water,collapse;
        public float goalX,goalY,frontX=-100,clock;
        public const float WhiteOpeningHalfWidth=1.4f;
        internal const float DebrisWarningSeconds=1.4f,DebrisDropHeight=11,DebrisFallSpeed=16,DebrisImpactRadius=1.6f;
        internal const float DebrisImpactTime=DebrisWarningSeconds+DebrisDropHeight/DebrisFallSpeed;
        const float DebrisLeadDistance=ReturnMotor.Speed*(DebrisWarningSeconds+(DebrisDropHeight-ReturnMotor.Height*.5f)/DebrisFallSpeed);
        uint meteorRandom=0x7254u;
        float nextMeteorAt=.45f;
        readonly List<ReturnSurface> meteorTargets=new List<ReturnSurface>();
        public int MeteorForecasts {get;private set;}
        public int MeteorImpacts {get;private set;}
        public int VolcanoEjections {get;private set;}
        public float LastMeteorX {get;private set;}
        public string LastContactCause {get;private set;}="";
        float MeteorRandom(){meteorRandom=meteorRandom*1664525u+1013904223u;return (meteorRandom>>8)*(1f/16777216);}
        void ScheduleMeteorRain(ReturnMotor motor)
        {
            if(clock<nextMeteorAt)return;
            nextMeteorAt=clock+.9f+MeteorRandom()*.45f;
            ReturnHazard available=null;
            foreach(ReturnHazard hazard in hazards)if(hazard.debris&&(hazard.timer<0||hazard.timer>DebrisImpactTime+.35f)){available=hazard;break;}
            if(available==null)return;
            // The viewport selects meaningful terrain; elapsed time alone launches the rain.
            float left=motor.position.x+3,right=Mathf.Min(escapeLedge.Left-1,motor.position.x+19);
            meteorTargets.Clear();
            foreach(ReturnSurface surface in surfaces)
                if(surface.solid&&surface.Right>left+.4f&&surface.Left<right-.4f&&surface.width>=3)meteorTargets.Add(surface);
            if(meteorTargets.Count==0)return;
            ReturnSurface target=meteorTargets[Mathf.Min(meteorTargets.Count-1,(int)(MeteorRandom()*meteorTargets.Count))];
            float min=Mathf.Max(left,target.Left+.5f),max=Mathf.Min(right,target.Right-.5f);
            if(max<min)return;
            available.x=Mathf.Lerp(min,max,MeteorRandom());available.y=target.y;available.timer=0;available.impactCounted=false;available.forecastSurface=target;
            available.warning.localPosition=new Vector3(available.x,available.y+.12f,0);
            available.impact.localPosition=new Vector3(available.x,available.y+.08f,0);
            LastMeteorX=available.x;MeteorForecasts++;VolcanoEjections++;
        }
        public ReturnSurface leaf,escapeLedge,escapeCheckpoint;
        public Transform burstPracticeGuide;
        public bool leafBoarded;
        public bool returnOpen;
        bool recalledReturn,lowerReturnEntered;
        ReturnSurface departureShelf;
        readonly List<ReturnSurface> forwardBreakaways=new List<ReturnSurface>(),lowerReturnSurfaces=new List<ReturnSurface>();
        public bool ForwardReturnCollapsed {get;private set;}
        public bool IsForwardBreakaway(ReturnSurface surface)=>surface!=null&&forwardBreakaways.Contains(surface);
        public float SignalProgress {get;private set;}=-1;
        Vector3 signalFrom,signalTarget;
        Transform signalThread;
        int signalDeposits;
        public float radioX=6,boundRight,gesture,reform;
        public Vector2 returnEntry;
        public ReturnFigure figure;
        ReturnScenery memoryScenery;
        readonly List<Transform> upperRouteGuides=new List<Transform>();
        public IReadOnlyList<Transform> UpperRouteGuides=>upperRouteGuides;
        public IReadOnlyList<Transform> ShelfMemoryRoots=>memoryScenery?.ShelfMemoryRoots;
        ReturnSurface stationFerry;
        Transform[] stationFerryWheels;
        Transform stationFerryRail;
        Transform stationSignalLight;
        int stationTravelState;
        float stationTravelTime,stationTravelStart;
        public bool StationTrainApproaching=>stationFerry!=null&&stationTravelState==1;
        public bool CanCallStationTrain(Vector2 position)=>chapter==Movement.Fragments&&branch==2&&!returnOpen&&stationFerry!=null&&
            (stationTravelState==0||stationTravelState==4||stationTravelState==5)&&Mathf.Abs(position.x-45)<1.65f&&Mathf.Abs(position.y-4.3f)<.8f;
        public bool TryMemoryStoryFocus(out Vector3 position)
        {
            position=Vector3.zero;
            return memoryScenery!=null&&memoryScenery.TryMemoryStoryFocus(out position);
        }
        public Transform rememberedPerson,ceiling,bed,hand,family,nurse,radioNeedle;
        Transform yellowMemory,redMemory,blueMemory;
        readonly List<Material> hospitalPaint=new List<Material>();
        Material motherPaint,nursePaint,handPaint;
        float restBlend;
        readonly List<Transform> radioBlocks=new List<Transform>();
        readonly List<MeshRenderer> radioSurfaces=new List<MeshRenderer>();
        readonly List<Color> radioBaseColours=new List<Color>();
        readonly MaterialPropertyBlock radioColour=new MaterialPropertyBlock();
        Transform radioBloom;
        MeshRenderer radioHalo;
        Transform horizon,ruptureSky;
        Material horizonPaint;
        Renderer[] recallAtmosphereRenderers;
        MaterialPropertyBlock recallAtmosphere;
        int reconnected;
        float lastPoseClock,lastImpact;
        bool wasBursting;
        readonly Transform[] dust=new Transform[16];
        readonly Vector2[] dustVelocity=new Vector2[16];
        readonly float[] dustAge=new float[16];
        int dustCursor;
        public Vector2 checkpoint=new Vector2(6,0);
        public Movement chapter;
        public int branch;
        public Color sky;
        readonly ReturnArt art;
        readonly List<Transform> branches=new List<Transform>();
        static readonly Color White=new Color(.94f,.94f,.94f), Ink=new Color(.025f,.025f,.025f);
        public ReturnWorld(ReturnArt a) {art=a;}
        Transform Group(string name,Transform parent=null)
        {Transform t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
        static readonly float[] branchLights={.0035f,.018f,.065f,.22f,1};
        readonly MaterialPropertyBlock recoveryBlock=new MaterialPropertyBlock();
        bool recoveryReady;
        float radioRevealAge=-1,radioRevealFrom,radioRevealTo,ruptureColour=1;
        public int RecoveryReceiverCount {get;private set;}
        public float BranchLuminosity=>branchLights[Mathf.Clamp(reconnected,0,4)];
        public float RadioRevealAge=>radioRevealAge;
        public bool RadioRevealActive=>radioRevealAge>=0&&radioRevealAge<1.4f;
        public float RadioRevealRadius=>radioRevealAge<0?10000:Mathf.Min(10000,radioRevealAge*65);
        public float RadioShakeStrength=>radioRevealAge>=0&&radioRevealAge<.24f?Mathf.Sin(radioRevealAge/.24f*Mathf.PI)*.09f:0;
        public float RuptureColour=>ruptureColour;
        public float GreyAtDistance(float distance)=>Mathf.Lerp(radioRevealFrom,radioRevealTo,Mathf.SmoothStep(0,1,Mathf.InverseLerp(distance-1.5f,distance+1.5f,RadioRevealRadius)));
        public void SetRuptureColour(float value){ruptureColour=Mathf.Clamp01(value);Shader.SetGlobalFloat("_ReturnRuptureColour",ruptureColour);}
        void ConfigureRecoveryLighting()
        {
            radioRevealFrom=radioRevealTo=BranchLuminosity;radioRevealAge=-1;RecoveryReceiverCount=0;
            RegisterRecoveryRenderers(root);
            recoveryReady=true;UpdateRecoveryLighting(0);
        }
        void RegisterRecoveryRenderers(Transform owner)
        {
            foreach(Renderer renderer in owner.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.sharedMaterial==null||renderer.sharedMaterial.shader.name!="Return/Ink"||renderer.transform.IsChildOf(player))continue;
                bool cue=chapter==Movement.Fragments&&(renderer==radioHalo||fragment!=null&&renderer.transform.IsChildOf(fragment));
                if(chapter==Movement.Fragments)
                {
                    foreach(Transform light in radioBlocks)cue|=renderer.transform.IsChildOf(light);
                    foreach(Transform guide in upperRouteGuides)cue|=renderer.transform.IsChildOf(guide);
                    foreach(Transform breath in dust)cue|=breath!=null&&renderer.transform.IsChildOf(breath);
                }
                if(cue)continue;
                renderer.GetPropertyBlock(recoveryBlock);
                recoveryBlock.SetFloat("_StageReveal",chapter==Movement.Fragments?1:0);
                recoveryBlock.SetFloat("_RuptureReveal",chapter==Movement.Rupture?1:0);
                renderer.SetPropertyBlock(recoveryBlock);RecoveryReceiverCount++;
            }
        }
        void UpdateRecoveryLighting(float dt)
        {
            if(chapter!=Movement.Fragments||!recoveryReady)return;
            if(RadioRevealActive)radioRevealAge=Mathf.Min(1.4f,radioRevealAge+dt);
            float radius=radioRevealAge>=1.4f?10000:RadioRevealRadius;
            Shader.SetGlobalVector("_ReturnGreyWave",new Vector4(radioRevealFrom,radioRevealTo,radius,1));
            float skyLight=Mathf.Lerp(radioRevealFrom,radioRevealTo,radioRevealAge<0?1:Mathf.SmoothStep(0,1,radioRevealAge/.7f))*.02f;
            sky=new Color(skyLight,skyLight,skyLight);
        }
        public void Build(Movement movement,int excursion,int deposits)
        {
            if(root!=null){if(Application.isPlaying)Object.Destroy(root.gameObject);else Object.DestroyImmediate(root.gameObject);}
            surfaces.Clear();hazards.Clear();motes.Clear();branches.Clear();memoryFuses.Clear();ruptureRocks.Clear();upperRouteGuides.Clear();
            forwardBreakaways.Clear();lowerReturnSurfaces.Clear();ForwardReturnCollapsed=false;
            SignalProgress=-1;signalThread=null;recoveryReady=false;radioRevealAge=-1;RecoveryReceiverCount=0;
            meteorRandom=0x7254u;nextMeteorAt=.45f;MeteorForecasts=MeteorImpacts=VolcanoEjections=0;
            Shader.SetGlobalVector("_ReturnGreyWave",new Vector4(1,1,10000,0));Shader.SetGlobalVector("_ReturnFooting",Vector4.zero);SetRuptureColour(1);
            hospitalPaint.Clear();restBlend=0;
            radioBlocks.Clear();radioSurfaces.Clear();radioBaseColours.Clear();radioBloom=null;radioHalo=null;
            horizon=ruptureSky=null;horizonPaint=null;recallAtmosphereRenderers=null;reconnected=deposits;lastPoseClock=lastImpact=0;wasBursting=false;dustCursor=0;
            player=fragment=beacon=door=fruit=water=collapse=null;yellowMemory=redMemory=blueMemory=radioNeedle=null;leaf=escapeLedge=escapeCheckpoint=null;leafBoarded=false;returnOpen=false;recalledReturn=lowerReturnEntered=false;departureShelf=null;figure=null;memoryScenery=null;burstPracticeGuide=null;clock=0;frontX=-100;branch=excursion;chapter=movement;gesture=reform=0;
            root=Group("RETURN · "+movement);far=Group("far atmosphere",root);near=Group("near silhouettes",root);
            if(movement==Movement.Hospital||movement==Movement.Credits){BuildHospital();return;}
            sky=movement==Movement.Fragments?new Color(.012f,.012f,.012f):movement==Movement.Rupture?new Color(.19f,.033f,.044f):new Color(.95f,.96f,.93f);
            art.Stark=movement==Movement.Fragments;if(art.Stark)sky=Color.black;
            Background(movement);
            if(movement==Movement.Fragments)BuildExcursion(excursion,deposits);
            else if(movement==Movement.Rupture)BuildEscape();
            else BuildIsland(movement==Movement.Reunion);
            boundRight=goalX+14;
            BuildPlayer(movement==Movement.Fragments||movement==Movement.Rupture?White:Ink);
            if(movement==Movement.Fragments)BuildFootingHint();
            Color dustInk=movement==Movement.Fragments||movement==Movement.Rupture?new Color(.92f,.92f,.87f,.38f):new Color(.18f,.30f,.24f,.32f);
            for(int i=0;i<dust.Length;i++){dust[i]=art.Shape("breath of movement",root,Vector2.zero,Vector2.one*.07f,dustInk,19,true);dustAge[i]=1;dust[i].gameObject.SetActive(false);}
            for(int i=0;i<48;i++)
            {
                float x=(i*17.31f)%36-18,y=(i*7.27f)%19-7;
                Color c=movement==Movement.Rupture?new Color(1,.41f,.26f,.22f):movement==Movement.Fragments?new Color(.8f,.8f,.8f,.2f):new Color(.72f,.87f,.79f,.2f);
                motes.Add(art.Shape("drifting mote",near,new Vector2(x,y),Vector2.one*(.026f+(i%3)*.012f),c,-12,true));
            }
            ConfigureRecoveryLighting();
        }
        ReturnSurface Ground(float left,float right,float top,SurfaceKind kind=SurfaceKind.Stone,bool station=false,bool breakaway=false)
        {
            ReturnSurface s=new ReturnSurface((left+right)*.5f,top,right-left,kind);surfaces.Add(s);
            Transform group=Group(kind+" shelf "+surfaces.Count,root);group.localPosition=new Vector3(s.x,s.y,0);s.visual=group;
            Color floor=chapter==Movement.Fragments?new Color(.055f,.055f,.055f):chapter==Movement.Rupture?new Color(.04f,.036f,.045f):new Color(.09f,.19f,.17f);
            Color edge=chapter==Movement.Rupture?new Color(.71f,.44f,.37f):White;
            if(kind==SurfaceKind.Fading)edge=new Color(.75f,.75f,.75f);
            if(kind==SurfaceKind.Collapse)edge=chapter==Movement.Fragments?new Color(.82f,.82f,.82f):new Color(.92f,.77f,.68f);
            float w=s.width*.5f;
            if(breakaway)BuildMemoryBreakaway(s,group,edge);
            else if(chapter==Movement.Rupture)BuildRuptureRock(s,group,edge);
            else if(station)BuildStationFerry(s,group);
            else
            {
                art.Polygon("cut rock",group,new[]{new Vector2(-w,0),new Vector2(w,0),new Vector2(w-.3f,-.65f),new Vector2(w*.65f,-2.8f),new Vector2(-w*.3f,-4.1f),new Vector2(-w,-.8f)},floor,0);
                art.Polygon("paper facet",group,new[]{new Vector2(-w,-.12f),new Vector2(w-.3f,-.12f),new Vector2(w*.65f,-2.8f),new Vector2(w*.2f,-1.4f)},new Color(floor.r+.025f,floor.g+.025f,floor.b+.025f),1);
                art.Shape("readable lip",group,new Vector2(0,.025f),new Vector2(s.width,.075f),edge,2);
                for(int i=0;i<Mathf.CeilToInt(s.width/2);i++)
                {
                    float xx=-w+.4f+i*2;float h=.28f+(i%4)*.34f;
                    art.Line(group,new Vector2(xx,-.12f),new Vector2(xx+.35f,-h),.025f,new Color(edge.r,edge.g,edge.b,.12f),1);
                }
            }
            if(chapter==Movement.Fragments&&(kind==SurfaceKind.Fading||kind==SurfaceKind.Collapse))
            {
                float width=s.width-.3f;
                art.Shape("memory ground fuse track",group,new Vector2(0,-.23f),new Vector2(width,.10f),new Color(.3f,.3f,.3f),3);
                memoryFuses.Add(s,art.Shape("memory ground fuse",group,new Vector2(0,-.23f),new Vector2(width,.10f),White,4));
            }
            if(kind==SurfaceKind.Spring)
            {
                art.Glow(group,new Vector2(0,.25f),1.2f,White,3);
                for(int i=0;i<3;i++)art.Line(group,new Vector2(-.55f,.2f+i*.15f),new Vector2(.55f,.2f+i*.15f),.04f,White,3);
            }
            if(kind==SurfaceKind.Ferry&&!station)
            {
                art.Line(group,new Vector2(-w,0),new Vector2(w,0),.14f,White,4);
                art.Glow(group,new Vector2(0,-.45f),.85f,White,3);
            }
            if(s.width>=5&&kind==SurfaceKind.Stone)
            {
                float at=w-.7f;art.Line(group,new Vector2(at,0),new Vector2(at,1.1f),.025f,new Color(White.r,White.g,White.b,.4f),3);
                art.Glow(group,new Vector2(at,1.1f),.45f,edge,4);
            }
            if(recoveryReady)RegisterRecoveryRenderers(group);
            return s;
        }
        void BuildStationFerry(ReturnSurface surface,Transform deck)
        {
            stationFerry=surface;surface.span=3.5f;surface.x=surface.originX+surface.span;deck.localPosition=new Vector3(surface.x,surface.y,0);float half=surface.width*.5f;
            Color metal=new Color(.75f,.79f,.77f,.5f),dark=new Color(.055f,.055f,.055f);
            art.Shape("station platform deck",deck,new Vector2(0,-.16f),new Vector2(surface.width,.32f),dark,0);
            art.Shape("readable station edge",deck,new Vector2(0,.025f),new Vector2(surface.width,.10f),White,4);
            art.Line(deck,new Vector2(-half,-.32f),new Vector2(half,-.32f),.03f,metal,2);
            art.Line(deck,new Vector2(-half,0),new Vector2(-half,-.32f),.03f,metal,2);
            art.Line(deck,new Vector2(half,0),new Vector2(half,-.32f),.03f,metal,2);
            stationFerryWheels=new Transform[2];
            for(int i=0;i<2;i++)
            {
                float x=(i==0?-1:1)*surface.width*.27f;
                art.Line(deck,new Vector2(x,-.32f),new Vector2(x,-.48f),.025f,metal,2);
                Transform wheel=Group("station wheel "+i,deck);wheel.localPosition=new Vector3(x,-.48f,0);stationFerryWheels[i]=wheel;
                art.Shape("wheel rim",wheel,Vector2.zero,Vector2.one*.24f,metal,3,true);
                art.Shape("wheel centre",wheel,Vector2.zero,Vector2.one*.18f,dark,4,true);
                art.Line(wheel,new Vector2(-.09f,0),new Vector2(.09f,0),.016f,metal,5);
                art.Line(wheel,new Vector2(0,-.09f),new Vector2(0,.09f),.016f,metal,5);
            }
            Transform rail=Group("rail between station stops",root);rail.localPosition=new Vector3(surface.originX,surface.y-.60f,0);stationFerryRail=rail;
            Color guide=new Color(.75f,.79f,.77f,.28f);
            for(float x=-half;x<surface.span+half;x+=.45f)
                art.Line(rail,new Vector2(x,0),new Vector2(Mathf.Min(x+.30f,surface.span+half),0),.018f,guide,-1);
            for(int i=0;i<2;i++)
            {
                float x=i*surface.span;art.Line(rail,new Vector2(x,-.18f),new Vector2(x,.15f),.025f,guide,1);
                art.Shape("station stop mark",rail,new Vector2(x,-.22f),Vector2.one*.10f,metal,2,true);
            }
            Transform signal=Group("a signal left unanswered",root);signal.localPosition=new Vector3(45,4.3f,0);upperRouteGuides.Add(signal);
            art.Line(signal,Vector2.zero,new Vector2(0,.8f),.04f,metal,6);
            art.Shape("station call button",signal,new Vector2(0,.85f),Vector2.one*.36f,White,7,true);
            art.Shape("station call face",signal,new Vector2(0,.85f),Vector2.one*.24f,dark,8,true);
            stationSignalLight=art.Shape("signal acknowledged",signal,new Vector2(0,.85f),Vector2.one*.12f,White,9,true);
            stationSignalLight.gameObject.SetActive(false);
        }
        public bool CallStationTrain()
        {
            if(stationFerry==null||stationTravelState==1||stationTravelState==2||stationTravelState==3)return false;
            stationTravelState=1;stationTravelTime=0;stationTravelStart=stationFerry.x;
            stationSignalLight.gameObject.SetActive(true);Debug.Log("[RETURN station] Called");return true;
        }
        void StepStationTrain(float dt,ReturnMotor motor)
        {
            if(stationTravelState==1||stationTravelState==4)
            {
                stationTravelTime+=dt;float u=Mathf.Clamp01(stationTravelTime/2);
                float eased=u*u*u*(u*(u*6-15)+10),destination=stationFerry.originX+(stationTravelState==4?stationFerry.span:0);
                stationFerry.x=Mathf.Lerp(stationTravelStart,destination,eased);
                if(u>=1){bool arriving=stationTravelState==1;stationTravelState=arriving?2:5;stationTravelTime=0;Debug.Log(arriving?"[RETURN station] Ready to board":"[RETURN station] Reached far stop");}
            }
            else if(stationTravelState==2&&motor.support==stationFerry)
            {stationTravelState=3;stationTravelTime=0;Debug.Log("[RETURN station] Boarded");}
            else if(stationTravelState==3)
            {
                stationTravelTime+=dt;
                if(stationTravelTime>=.55f){stationTravelState=4;stationTravelTime=0;stationTravelStart=stationFerry.x;Debug.Log("[RETURN station] Departed");}
            }
        }
        void BuildExcursion(int index,int deposits)
        {
            stationFerry=null;stationFerryWheels=null;stationFerryRail=null;stationSignalLight=null;stationTravelState=0;stationTravelTime=stationTravelStart=0;
            Ground(-5,index==1?16:13,0);checkpoint=new Vector2(6,0);
            Radio(deposits);
            if(index==1)
            {
                burstPracticeGuide=Group("The first signal carries me further",root);
                upperRouteGuides.Add(burstPracticeGuide);
                Color gold=new Color(.96f,.84f,.35f,.35f);
                art.Line(burstPracticeGuide,new Vector2(7,.25f),new Vector2(7,2.5f),.025f,gold,5);
                art.Line(burstPracticeGuide,new Vector2(6.8f,2.2f),new Vector2(7,2.5f),.025f,gold,5);
                art.Line(burstPracticeGuide,new Vector2(7.2f,2.2f),new Vector2(7,2.5f),.025f,gold,5);
                for(int i=0;i<5;i++)art.Line(burstPracticeGuide,new Vector2(7.5f+i*.5f,2.5f),new Vector2(7.8f+i*.5f,2.5f),.025f,gold,5);
                art.Line(burstPracticeGuide,new Vector2(10,2.3f),new Vector2(10.4f,2.5f),.025f,gold,5);
                art.Line(burstPracticeGuide,new Vector2(10,2.7f),new Vector2(10.4f,2.5f),.025f,gold,5);
            }
            if(index==0)
            {
                // Shorter landing lips reward committed jumps; the final pair teaches linked takeoffs.
                Ground(17.6f,20,1.2f);Ground(25,27.1f,2.2f);Ground(32,33.9f,3.3f);
                Ground(38.9f,40.7f,4.4f,SurfaceKind.Fading);Ground(45.7f,47.5f,4.4f,SurfaceKind.Fading);Ground(52.4f,57,4.4f);
            }
            else if(index==1)
            {
                Ground(17.3f,19.5f,1.1f);Ground(24.7f,26.4f,1.1f,SurfaceKind.Spring);Ground(31.9f,33.7f,5.9f);Ground(38.6f,41.2f,7.1f,SurfaceKind.Collapse);
                Ground(48.2f,49.8f,7.1f);Ground(55,62,8.2f);Gate(40.2f,7.1f);
            }
            else if(index==2)
            {
                Ground(17.4f,19.2f,1,SurfaceKind.Fading);Ground(24.4f,26.1f,2,SurfaceKind.Fading);
                // The long burst ends beside a moving bar; leave room beyond its full sweep to recover.
                Ground(34,39.5f,3);
                Ground(43.2f,45.3f,4.3f);Ground(51,54,4.3f,SurfaceKind.Ferry,station:true);
                Ground(61,68,5.3f);Gate(36,3);BurstThread(27,34,2);
            }
            else
            {
                // Renew the burst on each crumbling lip, then brake into a short controlled hop.
                Ground(17.3f,19.2f,1,SurfaceKind.Collapse);Ground(27.15f,28.85f,2,SurfaceKind.Collapse);
                Ground(36.65f,38.35f,3.2f,SurfaceKind.Collapse);Ground(43.7f,45.7f,3.6f,SurfaceKind.Collapse);
                Ground(50.6f,52.4f,4.5f,SurfaceKind.Spring);Ground(58.4f,60.6f,8);Ground(67,68.7f,7,SurfaceKind.Collapse);
                // Keep debris on the approach, with a quiet pickup area beyond its collision band.
                Ground(74,83,6);Debris(77,6);BurstThread(19.5f,27,1);BurstThread(29,36.5f,2);
            }
            ReturnSurface last=surfaces[surfaces.Count-1];goalX=last.Right-2.6f;goalY=last.y+1.35f;
            departureShelf=last;
            returnEntry=new Vector2(last.Right+9,last.y-3.4f);
            for(int i=0;i<3;i++)forwardBreakaways.Add(Ground(last.Right+.4f+i*2.6f,last.Right+2.6f+i*2.6f,last.y,SurfaceKind.Collapse,breakaway:true));
            memoryScenery=new ReturnScenery(art,root);memoryScenery.Memory(index,goalX,last.y);
            fragment=Group("yellow memory light",root);fragment.localPosition=new Vector3(goalX,goalY,0);
            Color memory=new Color(.96f,.84f,.35f);
            art.Glow(fragment,Vector2.zero,1.1f,memory,5);
            art.Shape("yellow light point",fragment,Vector2.zero,Vector2.one*.14f,memory,6,true);
        }
        void BurstThread(float from,float to,float height)
        {
            Transform thread=Group("a remembered long crossing",root);
            upperRouteGuides.Add(thread);
            Color gold=new Color(.96f,.84f,.35f,.32f);
            Vector2 previous=new Vector2(from-.6f,height+.3f);
            for(int i=1;i<=6;i++)
            {
                float t=i/6f;Vector2 next=new Vector2(from-.6f+t*2.8f,height+.3f+2.3f*(1-(1-t)*(1-t)));
                art.Line(thread,previous,next,.025f,gold,5);previous=next;
            }
            float top=height+2.6f;
            for(float x=from+2.6f;x<to+.2f;x+=.9f)art.Line(thread,new Vector2(x,top),new Vector2(Mathf.Min(x+.45f,to+.2f),top),.025f,gold,5);
            art.Line(thread,new Vector2(to-.2f,top-.17f),new Vector2(to+.2f,top),.025f,gold,5);
            art.Line(thread,new Vector2(to-.2f,top+.17f),new Vector2(to+.2f,top),.025f,gold,5);
        }
        void Radio(int deposits)
        {
            beacon=Group("the radio",root);beacon.localPosition=new Vector3(radioX,0,0);
            art.Polygon("radio case",beacon,new[]{new Vector2(-1.35f,.15f),new Vector2(1.35f,.15f),new Vector2(1.25f,1.7f),new Vector2(-1.25f,1.7f)},new Color(.18f,.19f,.18f),4);
            art.Line(beacon,new Vector2(-1.15f,1.75f),new Vector2(1.15f,1.75f),.045f,White,5);
            art.Line(beacon,new Vector2(-.7f,1.75f),new Vector2(-.3f,3.65f),.04f,White,5);
            for(int i=0;i<9;i++)art.Line(beacon,new Vector2(-1.05f,.43f+i*.12f),new Vector2(-.05f,.43f+i*.12f),.025f,new Color(.51f,.56f,.51f),5);
            art.Shape("frequency glass",beacon,new Vector2(.65f,1.2f),new Vector2(.86f,.48f),new Color(.04f,.06f,.05f),5);
            for(int i=0;i<6;i++)art.Line(beacon,new Vector2(.3f+i*.13f,1.05f),new Vector2(.3f+i*.13f,1.32f),.018f,new Color(.72f,.75f,.61f),6);
            radioNeedle=art.Shape("tuning needle",beacon,new Vector2(.35f,1.18f),new Vector2(.022f,.33f),new Color(.96f,.84f,.35f),7);
            art.Shape("tuning dial",beacon,new Vector2(.65f,.56f),Vector2.one*.27f,White,6,true);
            // The radio carries light even while the surrounding world remains stark.
            bool stark=art.Stark;art.Stark=false;
            try{radioBloom=art.Glow(beacon,new Vector2(0,1.2f),2.5f,new Color(.9f,.88f,.64f,.55f),3);}
            finally{art.Stark=stark;}
            radioHalo=radioBloom.GetComponentInChildren<MeshRenderer>();
            foreach(var renderer in beacon.GetComponentsInChildren<MeshRenderer>())
            {
                if(renderer.transform.IsChildOf(radioBloom))continue;
                radioSurfaces.Add(renderer);radioBaseColours.Add(renderer.sharedMaterial.GetColor("_Color"));
            }
            for(int i=0;i<4;i++)
            {
                Vector2 p=new Vector2(-.6f+i*.4f,2.1f);
                art.Shape("empty radio socket "+i,beacon,p,Vector2.one*.2f,new Color(.14f,.15f,.14f),6);
                radioBlocks.Add(art.Shape("reconnected light "+i,beacon,p,Vector2.one*.16f,new Color(.96f,.84f,.35f),7,true));
            }
            SetRadioProgress(deposits);
            for(int i=0;i<3;i++)art.Line(root,new Vector2(10+i*.7f,2.6f+i*.35f),new Vector2(11+i*.7f,3+i*.35f),.018f,new Color(.85f,.86f,.74f,.3f),4);
        }
        public Vector3 RecallBlockPosition(int index)=>radioBlocks[index].position;
        public void SetRecallBlocksVisible(bool visible){foreach(var block in radioBlocks)block.gameObject.SetActive(visible);}
        public void SetRadioProgress(int deposits)
        {
            int previous=reconnected;
            reconnected=deposits;
            float charge=Mathf.Clamp(deposits,0,4)*.25f;
            for(int i=0;i<radioSurfaces.Count;i++)
            {
                radioColour.SetColor("_Color",Color.Lerp(radioBaseColours[i],Color.white,charge));
                radioColour.SetFloat("_StageReveal",chapter==Movement.Fragments?1:0);radioColour.SetFloat("_RuptureReveal",chapter==Movement.Rupture?1:0);
                radioSurfaces[i].SetPropertyBlock(radioColour);
            }
            if(radioHalo!=null)
            {
                Color light=Color.Lerp(new Color(.9f,.88f,.64f),Color.white,charge);light.a=Mathf.Lerp(.04f,.48f,charge);
                radioColour.SetFloat("_StageReveal",0);radioColour.SetFloat("_RuptureReveal",0);
                radioColour.SetColor("_Color",light);radioHalo.SetPropertyBlock(radioColour);
            }
            for(int i=0;i<radioBlocks.Count;i++)radioBlocks[i].gameObject.SetActive(i<deposits);
            if(radioBloom!=null)radioBloom.localScale=Vector3.one*(1+Mathf.Clamp(deposits,0,4)*.25f);
            if(chapter==Movement.Fragments&&recoveryReady&&deposits>previous)
            {
                radioRevealFrom=branchLights[Mathf.Clamp(previous,0,4)];radioRevealTo=BranchLuminosity;radioRevealAge=0;
                Shader.SetGlobalVector("_ReturnGreyOrigin",beacon.position);UpdateRecoveryLighting(0);
            }
        }
        public void PoseRadioAscent(float progress,bool reducedMotion)
        {
            if(beacon==null||chapter!=Movement.Fragments)return;
            float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01(progress));
            beacon.localPosition=new Vector3(radioX,(reducedMotion?.65f:2.1f)*rise,0);
        }
        public void OpenReturn(bool recalled=false)
        {
            if(returnOpen)return;
            returnOpen=true;recalledReturn=recalled;
            if(!recalled&&stationFerryRail!=null)stationFerryRail.gameObject.SetActive(false);
            if(!recalled)foreach(Transform guide in upperRouteGuides)guide.gameObject.SetActive(false);
            int count=surfaces.Count;
            // The block opens a separate lower circuit. Previously crossed shelves become memories.
            for(int i=1;i<count;i++)
            {
                if(surfaces[i]==departureShelf||IsForwardBreakaway(surfaces[i]))continue;
                surfaces[i].solid=false;
                if(!recalled&&surfaces[i]!=departureShelf)surfaces[i].visual.gameObject.SetActive(false);
            }
            departureShelf.solid=true;departureShelf.returnPath=true;
            foreach(ReturnSurface surface in forwardBreakaways)surface.returnPath=true;
            // A continuous drop at the far end leads into the new return path.
            Ground(returnEntry.x-4,returnEntry.x+2,returnEntry.y);
            float end=returnEntry.x-7;
            if(branch==0)
            {
                Ground(end-2,end+2,-2.5f);ReturnSurface ferry=Ground(49,52,-2.5f,SurfaceKind.Ferry);ferry.span=6;
                Ground(40,45,-1.7f);Ground(31,36,-.7f);Ground(23,27,.3f);Ground(16,19,1.1f);
            }
            else if(branch==1)
            {
                Ground(61,65,1.8f);Ground(52,56,-.2f,SurfaceKind.Collapse);Ground(42,47,-2,SurfaceKind.Collapse);
                Ground(32,37,-1.3f);Ground(22,26,-.6f,SurfaceKind.Collapse);Ground(15,18,.2f);
            }
            else if(branch==2)
            {
                Ground(64,68,-.6f);Ground(55,58,-2,SurfaceKind.Fading);Ground(45,49,-2,SurfaceKind.Fading);
                Ground(34,39,-1.5f);ReturnSurface ferry=Ground(24,27,-.5f,SurfaceKind.Ferry);ferry.span=4;
                Ground(15,18,.2f);
            }
            else
            {
                Ground(returnEntry.x-12,returnEntry.x-8,returnEntry.y-1.7f);
                Ground(72,76,-.3f);Ground(62,66,-2,SurfaceKind.Collapse);Ground(52,56,-2.5f,SurfaceKind.Collapse);
                Ground(41,46,-1.6f);Ground(30,34,-.7f,SurfaceKind.Fading);Ground(21,25,.4f);Ground(15,17,1);
            }
            for(int i=count;i<surfaces.Count;i++)
            {surfaces[i].returnPath=true;lowerReturnSurfaces.Add(surfaces[i]);art.Line(surfaces[i].visual,new Vector2(-.8f,.22f),new Vector2(.8f,.22f),.022f,new Color(.96f,.84f,.35f,.5f),4);if(!recalled)surfaces[i].visual.gameObject.SetActive(false);}
            foreach(ReturnHazard h in hazards){h.visual.gameObject.SetActive(false);if(h.warning!=null)h.warning.gameObject.SetActive(false);if(h.impact!=null)h.impact.gameObject.SetActive(false);}
            checkpoint=new Vector2(returnEntry.x-1,returnEntry.y);
        }
        public bool IsLowerReturnSupport(ReturnSurface surface)=>returnOpen&&!recalledReturn&&surface!=null&&lowerReturnSurfaces.Contains(surface);
        public void CollapseForwardReturn()
        {
            if(!returnOpen||recalledReturn||ForwardReturnCollapsed)return;
            ForwardReturnCollapsed=true;
            foreach(ReturnSurface surface in forwardBreakaways){surface.age=1.55f;surface.solid=false;}
            foreach(ReturnSurface surface in lowerReturnSurfaces)surface.visual.gameObject.SetActive(true);
        }
        public void EnterLowerReturn()
        {
            if(!returnOpen||recalledReturn||lowerReturnEntered)return;
            CollapseForwardReturn();
            lowerReturnEntered=true;departureShelf.returnPath=false;departureShelf.solid=false;departureShelf.visual.gameObject.SetActive(false);memoryScenery?.RetireShelfMemories();
        }
        public void SendSignal(int deposits)
        {
            signalFrom=fragment.localPosition;
            signalTarget=root.InverseTransformPoint(radioBlocks[deposits-1].position);
            signalDeposits=deposits;SignalProgress=0;
            signalThread=Group("thread to the radio",root);
            art.Line(signalThread,signalFrom,signalTarget,.025f,new Color(.96f,.84f,.35f,.6f),5);
        }
        public void UpdateSignal(float dt)
        {
            UpdateRecoveryLighting(dt);
            if(SignalProgress<0||SignalProgress>=1)return;
            SignalProgress=Mathf.Min(1,SignalProgress+dt/1.15f);
            float t=SignalProgress,ease=Mathf.Sin(t*Mathf.PI*.5f);
            fragment.localPosition=Vector3.Lerp(signalFrom,signalTarget,ease)+Vector3.up*Mathf.Sin(t*Mathf.PI)*2.2f;
            fragment.localScale=Vector3.one*Mathf.Lerp(1,.55f,t);
            if(t>=1){SetRadioProgress(signalDeposits);fragment.gameObject.SetActive(false);signalThread.gameObject.SetActive(false);}
        }
        float RuptureRockBottom(float x,float half)
        {
            float along=Mathf.InverseLerp(-half,half,x),depth=Mathf.Min(3.3f,half*.55f+1);
            return -.32f-depth*Mathf.Pow(Mathf.Max(0,Mathf.Sin(along*Mathf.PI)),.72f);
        }
        void BuildMemoryBreakaway(ReturnSurface surface,Transform parent,Color edge)
        {
            var rock=new RuptureRock();ruptureRocks.Add(surface,rock);
            for(int i=0;i<5;i++)
            {
                float left=-surface.width*.5f+surface.width*i/5,right=-surface.width*.5f+surface.width*(i+1)/5,centre=(left+right)*.5f;
                Transform piece=Group("breaking memory stratum",parent);piece.localPosition=new Vector3(centre,0,0);rock.pieces.Add(piece);rock.origins.Add(piece.localPosition);
                art.Polygon("memory stone shard",piece,new[]{new Vector2(left-centre,0),new Vector2(right-centre,0),new Vector2(right-centre,-1.25f-i*.06f),new Vector2(left-centre,-1.4f+i*.04f)},new Color(.065f,.065f,.065f),0);
                art.Line(piece,new Vector2(left-centre,.025f),new Vector2(right-centre,.025f),.06f,edge,2);
                if(i>0){Transform crack=Group("memory fissure",parent);art.Line(crack,new Vector2(left,0),new Vector2(left+.06f,-1.35f),.022f,new Color(.012f,.012f,.012f),3);rock.cracks.Add(crack);}
            }
        }
        void BuildRuptureRock(ReturnSurface surface,Transform parent,Color edge)
        {
            var rock=new RuptureRock();ruptureRocks.Add(surface,rock);float half=surface.width*.5f,lightDepth=Mathf.Abs(RuptureRockBottom(0,surface.width*.5f));
            float[] cuts={0,.17f,.39f,.57f,.8f,1};
            for(int i=0;i<cuts.Length-1;i++)
            {
                float left=Mathf.Lerp(-half,half,cuts[i]),right=Mathf.Lerp(-half,half,cuts[i+1]),center=(left+right)*.5f;
                float a=left-center,b=right-center,lowA=RuptureRockBottom(left,half),lowB=RuptureRockBottom(right,half);
                Transform piece=Group("fractured volcanic stratum",parent);piece.localPosition=new Vector3(center,0,0);rock.pieces.Add(piece);rock.origins.Add(piece.localPosition);
                const int sections=6;var face=new Vector2[sections+3];face[0]=new Vector2(a,0);face[1]=new Vector2(b,0);
                for(int section=0;section<=sections;section++) {
                    float x=Mathf.Lerp(right,left,section/(float)sections),bottom=RuptureRockBottom(x,half)+.08f*Mathf.Sin(x*9.1f)*Mathf.Sin(Mathf.PI*Mathf.InverseLerp(-half,half,x));
                    face[section+2]=new Vector2(x-center,bottom);
                }
                RuptureForm("rounded basalt body",piece,face,new Color(.21f,.23f,.26f),new Color(.47f,.45f,.41f),0,false,lightDepth);
                RuptureForm("softly lit basalt plane",piece,new[]{new Vector2(a,-.18f),new Vector2(b,-.18f),new Vector2(b,lowB),new Vector2(Mathf.Lerp(a,b,.38f),Mathf.Lerp(lowA,lowB,.38f))},new Color(.22f,.24f,.27f),new Color(.50f,.48f,.44f),1,false,lightDepth);
                art.Polygon("stone bevel",piece,new[]{new Vector2(a,0),new Vector2(b,0),new Vector2(b,-.18f),new Vector2(a,-.18f)},new Color(.63f,.60f,.53f),1);
                art.Shape("readable lip",piece,new Vector2(0,.025f),new Vector2(right-left,.075f),Color.Lerp(edge,new Color(.82f,.78f,.67f),.8f),2);
                if(i==0)continue;
                Transform crack=Group("spreading ground fissure",parent);crack.localPosition=new Vector3(left,-.015f,0);rock.cracks.Add(crack);
                Color fracture=new Color(.012f,.009f,.01f,.88f);float depth=Mathf.Abs(lowA);
                art.Line(crack,Vector2.zero,new Vector2(.12f,-depth*.25f),.025f,fracture,4);
                art.Line(crack,new Vector2(.12f,-depth*.25f),new Vector2(-.09f,-depth*.63f),.02f,fracture,4);
                art.Line(crack,new Vector2(-.09f,-depth*.63f),new Vector2(.04f,-depth),.016f,fracture,4);
                art.Line(crack,new Vector2(.12f,-depth*.25f),new Vector2(.4f,-depth*.42f),.013f,fracture,4);
                crack.gameObject.SetActive(false);
            }
        }
        void UpdateRuptureRock(ReturnSurface surface,float damage,float fallAge)
        {
            if(!ruptureRocks.TryGetValue(surface,out RuptureRock rock))return;rock.damage=damage;
            for(int i=0;i<rock.cracks.Count;i++)
            {
                float reveal=Mathf.Clamp01(damage*1.6f-i*.12f);Transform crack=rock.cracks[i];
                crack.gameObject.SetActive(reveal>.08f&&fallAge<=0);crack.localScale=new Vector3(Mathf.Lerp(.35f,1.4f,reveal),1,1);
            }
            for(int i=0;i<rock.pieces.Count;i++)
            {
                float t=Mathf.Max(0,fallAge-i*.045f),sign=i%2==0?-1:1;Transform piece=rock.pieces[i];
                float stress=Mathf.SmoothStep(0,1,damage);
                piece.localPosition=rock.origins[i]+new Vector3(sign*(t*.35f+.018f*stress),-Mathf.Min(24,5.5f*t*t)-(i%3)*.016f*stress+Mathf.Sin(clock*(18+i))*.004f*stress,0);
                piece.localRotation=Quaternion.Euler(0,0,sign*((8+i*3)*t*t+.6f*stress));
            }
        }
        public bool IsRuptureFracturing(ReturnSurface surface)
        {
            return surface.solid&&surface.visual.gameObject.activeInHierarchy&&ruptureRocks.TryGetValue(surface,out RuptureRock rock)&&rock.damage>.35f&&rock.damage<.95f;
        }
        void Gate(float x,float y)
        {
            ReturnHazard h=new ReturnHazard{x=x,y=y};h.visual=Group("sweeping pressure",root);
            art.Shape("bar",h.visual,Vector2.up*.7f,new Vector2(.24f,1.35f),new Color(.67f,.72f,.71f),6);
            art.Glow(h.visual,Vector2.up*1.3f,.5f,White,7);hazards.Add(h);
            art.Line(root,new Vector2(x-2,y+.15f),new Vector2(x+2,y+.15f),.025f,new Color(.4f,.51f,.52f,.5f),3);
        }
        void Debris(float x,float y)
        {
            ReturnHazard h=new ReturnHazard{x=x,y=y,debris=true};h.visual=Group("falling rupture",root);h.visual.gameObject.SetActive(false);
            Transform rock;
            if(chapter==Movement.Rupture)
            {
                art.Polygon("fading ember wake",h.visual,new[]{new Vector2(-.27f,.12f),new Vector2(-.19f,1.04f),new Vector2(.06f,2.0f),new Vector2(.24f,.94f),new Vector2(.29f,.12f)},new Color(.88f,.18f,.065f,.24f),4);
                art.Polygon("rising molten trail",h.visual,new[]{new Vector2(-.17f,.12f),new Vector2(-.09f,1.0f),new Vector2(.05f,1.49f),new Vector2(.15f,.66f),new Vector2(.19f,.12f)},new Color(1,.39f,.10f,.68f),5);
                rock=art.Polygon("incandescent volcanic fragment",h.visual,new[]{new Vector2(-.29f,.21f),new Vector2(-.08f,.39f),new Vector2(.23f,.26f),new Vector2(.32f,-.11f),new Vector2(.05f,-.43f),new Vector2(-.31f,-.17f)},new Color(.71f,.19f,.065f),6);
                art.Polygon("hot leading edge",rock,new[]{new Vector2(-.31f,-.17f),new Vector2(-.15f,-.26f),new Vector2(.05f,-.36f),new Vector2(.27f,-.11f),new Vector2(.32f,-.11f),new Vector2(.05f,-.43f)},new Color(1,.62f,.25f),7);
                art.Line(rock,new Vector2(-.12f,.28f),new Vector2(.04f,.03f),.045f,new Color(1,.49f,.16f),8);
                art.Line(rock,new Vector2(.04f,.03f),new Vector2(-.08f,-.22f),.035f,new Color(1,.62f,.25f),8);
            }
            else
            {
                rock=art.Polygon("falling volcanic stone",h.visual,new[]{new Vector2(-.38f,.28f),new Vector2(-.12f,.59f),new Vector2(.31f,.42f),new Vector2(.47f,-.12f),new Vector2(.11f,-.56f),new Vector2(-.43f,-.3f)},new Color(.075f,.058f,.056f),6);
                art.Polygon("fresh stone fracture",rock,new[]{new Vector2(-.12f,.59f),new Vector2(.31f,.42f),new Vector2(.11f,-.56f)},new Color(.18f,.12f,.1f),7);
            }
            h.stone=rock;
            h.warning=Group("impact warning",root);h.warning.localPosition=new Vector3(x,y+.12f,0);
            art.Shape("red forecast area",h.warning,Vector2.zero,new Vector2(3.2f,.20f),chapter==Movement.Rupture?new Color(.94f,.07f,.07f,.65f):new Color(.028f,.014f,.012f,.7f),4,true);
            art.Line(h.warning,new Vector2(-1.6f,0),new Vector2(1.6f,0),.05f,chapter==Movement.Rupture?new Color(1,.18f,.12f,.9f):new Color(1,.73f,.5f,.8f),5);
            h.impact=Group("stone impact dust",root);h.impact.localPosition=new Vector3(x,y+.08f,0);
            art.Shape("low dust cloud",h.impact,new Vector2(0,.13f),new Vector2(3.2f,.36f),new Color(.35f,.22f,.16f,.38f),5,true);
            for(int i=0;i<5;i++)art.Polygon("ejected stone",h.impact,new[]{new Vector2(-.09f,-.04f),new Vector2(0,.09f),new Vector2(.08f,-.03f)},new Color(.13f,.085f,.065f),6).localPosition=new Vector3((i-2)*.58f,.1f+(i%2)*.09f,0);
            h.impact.gameObject.SetActive(false);
            h.warning.gameObject.SetActive(false);
            if(chapter==Movement.Rupture)
            {
                h.ejection=Group("volcano ejects this stone",far);h.ejection.gameObject.SetActive(false);
                art.Shape("glowing volcanic stone",h.ejection,Vector2.zero,new Vector2(.30f,.26f),new Color(.96f,.34f,.12f,.9f),-30,true);
                art.Line(h.ejection,new Vector2(-.9f,-.3f),Vector2.zero,.10f,new Color(.95f,.18f,.08f,.5f),-31);
            }
            hazards.Add(h);
        }
        void BuildEscape()
        {
            Ground(-5,13,0);
            Radio(4); // Rupture reveals the same space in which the four signals were gathered.
            // Climb: precise ascending supports, then a broad shelter with a falling-stone warning.
            Ground(17,21,1.2f);Ground(25,29,2.4f,SurfaceKind.Collapse);Ground(34,38,3.6f);
            Ground(44,54,4.6f);Debris(49,4.6f);
            // Traverse: a spring vault over the fissure and a moving support under the volcano.
            Ground(59,62,4,SurfaceKind.Spring);Ground(67,71,8.2f);Ground(77,81,7.4f,SurfaceKind.Collapse);
            ReturnSurface ferry=Ground(86,89,6.2f,SurfaceKind.Ferry);ferry.span=5;
            escapeCheckpoint=Ground(99,110,5);Debris(104,5);
            // Descent: longer directional leaps and crumbling narrow landings, then breathing room.
            Ground(116,120,3.6f,SurfaceKind.Collapse);Ground(127,131,2,SurfaceKind.Collapse);
            Ground(139,143,.6f,SurfaceKind.Collapse);Ground(150,161,0);Debris(155,0);
            Ground(166,170,1);Ground(175,179,2,SurfaceKind.Collapse);Ground(185,190,3.4f);
            escapeLedge=Ground(196,201,4.4f);goalX=205;goalY=4.4f;
            Vector2 takeoff=new Vector2(escapeLedge.Right-.7f,goalY+.18f),rise=new Vector2(escapeLedge.Right+.9f,goalY+3.4f),arrival=new Vector2(goalX-WhiteOpeningHalfWidth,goalY+1.8f);
            for(int i=0;i<9;i++)
            {
                float a=i/9f,b=(i+.45f)/9f;
                Vector2 from=(1-a)*(1-a)*takeoff+2*(1-a)*a*rise+a*a*arrival;
                Vector2 to=(1-b)*(1-b)*takeoff+2*(1-b)*b*rise+b*b*arrival;
                art.Line(root,from,to,.035f,new Color(.97f,.97f,.93f,.28f),5);
            }
            door=Group("white opening",root);door.localPosition=new Vector3(goalX,2,0);
            door.localPosition=new Vector3(goalX,goalY,0);
            art.Glow(door,new Vector2(0,3.8f),8,White,3);
            art.Shape("threshold",door,new Vector2(0,3.8f),new Vector2(WhiteOpeningHalfWidth*2,7.6f),new Color(.98f,.98f,.91f),6);
            for(int i=0;i<5;i++)art.Line(door,new Vector2(-2-i*.25f,0),new Vector2(-2-i*.25f,7-i*.4f),.04f,new Color(.88f,.87f,.78f,.22f),3);
            collapse=Group("advancing rupture",root);
            // A dense ash front has a soft, irregular edge rather than repeating red teeth.
            var ash=art.Shape("Dense volcanic ash",collapse,new Vector2(-18,0),new Vector2(48,160),new Color(.17f,.105f,.11f,.94f),9);
            ash.GetComponent<Renderer>().sharedMaterial.SetFloat("_SmokeEdge",1);
            for(int i=0;i<24;i++) {
                float y=-15+i*1.3f,x=-1.8f-Mathf.Sin(i*2.4f)*.7f;
                art.Glow(collapse,new Vector2(x,y),4.2f+(i%4)*.45f,new Color(.34f,.18f,.17f,.48f),10);
                art.Glow(collapse,new Vector2(x-2.2f,y+.5f),5.8f,new Color(.11f,.09f,.10f,.55f),10);
            }
            for(int i=0;i<38;i++) {
                float x=-.8f-(i%7)*.85f,y=-13+(i*3.71f)%28;
                art.Shape("Windborne ember",collapse,new Vector2(x,y),new Vector2(.025f,.065f),new Color(.94f,.48f,.21f,.38f),11,true);
            }
            checkpoint=new Vector2(6,0);
        }
        void BuildIsland(bool reunion)
        {
            goalX=76;goalY=0;checkpoint=new Vector2(5,-3);
            if(reunion)
            {
                Ground(-5,82,0);checkpoint=new Vector2(73,0);
                door=Group("assembled doorway",root);door.localPosition=new Vector3(6,0,0);
                art.Glow(door,new Vector2(0,3),5,White,3);
                art.Shape("door",door,new Vector2(0,2.8f),new Vector2(2.5f,5.6f),new Color(.99f,.98f,.9f),4);
                art.Shape("frosted hospital window",door,new Vector2(0,3.65f),new Vector2(1.25f,1.1f),new Color(.65f,.78f,.74f),5);
                art.Line(door,new Vector2(1.4f,0),new Vector2(1.4f,5.8f),.12f,new Color(.32f,.45f,.41f),6);
                art.Shape("door handle",door,new Vector2(.8f,2.5f),new Vector2(.28f,.08f),new Color(.38f,.51f,.46f),6);
                // Familiar forms reappear together: the red volcano, first connection and blue water.
                art.Polygon("remembered volcano",far,new[]{new Vector2(35,-3),new Vector2(45,5),new Vector2(47,4),new Vector2(49,5),new Vector2(62,-3)},new Color(.59f,.29f,.23f,.7f),-20);
                Radio(4);beacon.localPosition=new Vector3(27,0,0);
                for(int i=0;i<12;i++)art.Line(root,new Vector2(40+i*3,-.22f),new Vector2(42+i*3,-.22f),.04f,new Color(.28f,.57f,.7f,.6f),3);
                Color[] colors={new Color(.93f,.8f,.39f),new Color(.78f,.31f,.26f),new Color(.35f,.62f,.72f)};
                for(int i=0;i<12;i++)
                {
                    float x=12+i*4.8f;Transform t=art.Shape("reunited fragment",root,new Vector2(x,2.5f+Mathf.Sin(i)*1.1f),new Vector2(.35f,.5f),colors[i%3],3);t.localRotation=Quaternion.Euler(0,0,35+i*12);
                    art.Line(root,new Vector2(x,1),new Vector2(x+2,2),.018f,new Color(.4f,.57f,.51f,.25f),2);
                }
            }
            else
            {
                Ground(64,69,-1.8f);Ground(69,72,-.8f);Ground(72,80,0);
                leaf=new ReturnSurface(10,.08f,4.4f,SurfaceKind.Ferry);leaf.visual=Group("green floating leaf",root);leaf.visual.localPosition=new Vector3(10,.08f,0);surfaces.Add(leaf);
                art.Polygon("leaf",leaf.visual,new[]{new Vector2(-2.2f,0),new Vector2(-1.4f,.23f),new Vector2(0,.08f),new Vector2(2.2f,0),new Vector2(1,-.25f),new Vector2(-1,-.25f)},new Color(.33f,.49f,.29f),5);
                art.Line(leaf.visual,new Vector2(-1.9f,-.02f),new Vector2(1.9f,-.02f),.025f,new Color(.61f,.7f,.43f),6);
                water=art.Shape("pale liquid",root,new Vector2(30,-5),new Vector2(145,10),new Color(.83f,.89f,.88f,.6f),-2);
                for(int i=0;i<16;i++)art.Line(root,new Vector2(i*6-8,0),new Vector2(i*6-4,0),.024f,new Color(.97f,.99f,.94f,.6f),2);
                for(int i=0;i<25;i++)
                {float x=i*3.7f,y=-2-(i%4)*1.4f;art.Shape("underwater seed",root,new Vector2(x,y),Vector2.one*.07f,new Color(1,1,.94f,.6f),1,true);}
                // Earlier places emerge in order while the camera pulls back, before the continuous reunion.
                Radio(4);beacon.localPosition=new Vector3(27,0,0);yellowMemory=beacon;yellowMemory.gameObject.SetActive(false);
                redMemory=Group("the fire remembered without its pressure",root);
                art.Polygon("remembered volcano",redMemory,new[]{new Vector2(35,-3),new Vector2(45,5),new Vector2(47,4),new Vector2(49,5),new Vector2(62,-3)},new Color(.59f,.29f,.23f,.7f),-20);
                redMemory.gameObject.SetActive(false);
                blueMemory=Group("the water that held the traveller",root);
                for(int i=0;i<12;i++)art.Line(blueMemory,new Vector2(40+i*3,-.22f),new Vector2(42+i*3,-.22f),.04f,new Color(.28f,.57f,.7f,.6f),3);
                blueMemory.gameObject.SetActive(false);
            }
            Tree(76,0);
            fruit=Group("blue fruit",root);fruit.localPosition=new Vector3(75.4f,2.1f,0);
            art.Glow(fruit,Vector2.zero,1.8f,new Color(.28f,.62f,.81f),8);
            art.Shape("fruit",fruit,Vector2.zero,Vector2.one*.45f,new Color(.22f,.53f,.76f),9,true);
        }
        void Tree(float x,float y)
        {
            Transform tree=Group("small living island",root);tree.localPosition=new Vector3(x,y,0);
            art.Polygon("grassy crest",tree,new[]{new Vector2(-4,0),new Vector2(-3,.23f),new Vector2(1,.15f),new Vector2(4,0)},new Color(.26f,.4f,.26f),4);
            art.Line(tree,Vector2.zero,new Vector2(-.5f,4.5f),.32f,new Color(.2f,.24f,.16f),5);
            for(int i=0;i<7;i++)
            {
                float dir=i%2==0?1:-1,h=1.7f+i*.42f;
                Vector2 end=new Vector2(dir*(1.6f+(i%3)*.4f),h+1.3f);
                art.Line(tree,new Vector2(-.15f,h),end,.11f,new Color(.21f,.28f,.18f),5);
                branches.Add(art.Shape("soft leaf crown",tree,end,new Vector2(3.4f,2.1f),new Color(.32f+(i%2)*.08f,.44f+(i%3)*.035f,.27f,.64f),4,true));
            }
            for(int i=0;i<15;i++)art.Line(tree,new Vector2(-4+i*.65f,0),new Vector2(-4+i*.65f+.1f,.25f+(i%3)*.1f),.035f,new Color(.4f,.53f,.28f),5);
        }
        float pickupStarted=-1,placementStarted=-1;
        bool lightCollected;
        Vector3 pickupFrom;
        int placementSocket;
        public bool PickupAnimationActive=>chapter==Movement.Fragments&&pickupStarted>=0&&clock-pickupStarted<.9f;
        public float PickupAge=>pickupStarted<0?-1:clock-pickupStarted;
        public float PlacementAge=>placementStarted<0?-1:clock-placementStarted;
        public bool RadioPlacementActive=>placementStarted>=0;
        public bool RadioPlacementReady=>placementStarted>=0&&PlacementAge>=1.4f;
        public bool MemoryLightVisible=>fragment!=null&&fragment.gameObject.activeInHierarchy;
        public float LightPocketGap=>fragment==null||figure==null?100:Vector3.Distance(fragment.position,player.TransformPoint(figure.PocketPosition));
        public float LightRadioGap=>placementStarted<0?100:Vector3.Distance(fragment.position,radioBlocks[placementSocket].position);
        public void BeginPocketPickup(){lightCollected=true;pickupStarted=clock;placementStarted=-1;pickupFrom=fragment.localPosition;}
        public void BeginRadioPlacement(int socket){placementSocket=Mathf.Clamp(socket,0,3);placementStarted=clock;fragment.gameObject.SetActive(true);}
        public void CompleteRadioPlacement(){fragment.position=radioBlocks[placementSocket].position;fragment.gameObject.SetActive(false);placementStarted=-1;figure.KeyHandPose(Vector2.zero,false);}
        Vector3 PlayerPoint(Vector2 local)=>root.InverseTransformPoint(player.TransformPoint(local));
        void PosePocketLight(bool carrying)
        {
            Vector2 pocket=figure.PocketPosition,hand=new Vector2(.34f,1.13f);
            bool pickup=PickupAnimationActive,placing=RadioPlacementActive;
            if(placing)
            {
                float t=PlacementAge;
                Vector2 handPose=Vector2.Lerp(pocket,hand,Mathf.SmoothStep(0,1,t/.65f));
                figure.KeyHandPose(handPose,true);fragment.gameObject.SetActive(true);
                Vector3 held=PlayerPoint(handPose),socket=root.InverseTransformPoint(radioBlocks[placementSocket].position);
                fragment.localPosition=t<.8f?held:Vector3.Lerp(held,socket,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1.35f,t)));
                fragment.localScale=Vector3.one*Mathf.SmoothStep(0,1,t/.2f);
            }
            else if(carrying&&pickup)
            {
                float t=PickupAge;
                Vector2 pose=Vector2.Lerp(hand,pocket,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.9f,t)));
                figure.KeyHandPose(pose,true);fragment.gameObject.SetActive(true);
                fragment.localPosition=t<.35f?Vector3.Lerp(pickupFrom,PlayerPoint(hand),Mathf.SmoothStep(0,1,t/.35f)):PlayerPoint(pose);
                fragment.localScale=Vector3.one*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,.9f,t)));
            }
            else
            {
                figure.KeyHandPose(Vector2.zero,false);fragment.gameObject.SetActive(!lightCollected);fragment.localScale=Vector3.one;
                if(!lightCollected)fragment.localPosition=new Vector3(goalX,goalY+Mathf.Sin(clock*2)*.12f,0);
            }
        }
        void BuildFootingHint()
        {
            foreach(ReturnSurface surface in surfaces)
            {
                foreach(Renderer renderer in surface.visual.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.GetPropertyBlock(recoveryBlock);recoveryBlock.SetFloat("_FootingReveal",1);renderer.SetPropertyBlock(recoveryBlock);
                }
            }
        }
        void PoseFootingHint(ReturnMotor motor)
        {
            if(chapter!=Movement.Fragments)return;
            const float radius=1.15f;
            ReturnSurface surface=motor.support;
            // Project the small light onto nearby ground during a jump; never draw a floor across a gap.
            if(surface==null)
            {
                float nearest=float.PositiveInfinity;
                foreach(ReturnSurface candidate in surfaces)
                {
                    if(!candidate.solid||!candidate.visual.gameObject.activeInHierarchy||candidate.y>motor.position.y+.2f||motor.position.y-candidate.y>4)continue;
                    float horizontal=Mathf.Abs(motor.position.x-Mathf.Clamp(motor.position.x,candidate.Left,candidate.Right));
                    float distance=horizontal+Mathf.Abs(motor.position.y-candidate.y)*.05f;
                    if(horizontal<radius&&distance<nearest){surface=candidate;nearest=distance;}
                }
            }
            bool visible=surface!=null&&surface.solid&&surface.visual.gameObject.activeInHierarchy&&BranchLuminosity<.11f;
            Vector3 centre=root.TransformPoint(new Vector3(motor.position.x,surface==null?motor.position.y:surface.y,0));
            Vector3 axis=root.right;
            Shader.SetGlobalVector("_ReturnFooting",new Vector4(centre.x,centre.y,centre.z,visible?1:0));
            Shader.SetGlobalVector("_ReturnFootingAxis",new Vector4(axis.x,axis.y,axis.z,radius));
        }
        void BuildPlayer(Color color)
        {
            pickupStarted=placementStarted=-1;
            lightCollected=false;
            figure=new ReturnFigure(art,root,color,cleanTraveller:chapter==Movement.Fragments||chapter==Movement.Rupture);player=figure.root;
        }
        void Background(Movement movement)
        {
            if(movement==Movement.Fragments)
            {
                for(int i=0;i<8;i++){float x=i*13-20,y=10+(i*11%6);Transform shard=art.Shape("unconnected memory",far,new Vector2(x,y),new Vector2(.09f,.4f),Color.white,-24);shard.localRotation=Quaternion.Euler(0,0,i*17);}
                return;
            }
            if(movement==Movement.Rupture){BuildRuptureLandscape();return;}
            string picture=movement==Movement.Fragments?"dream-dark":movement==Movement.Rupture?"dream-fire":"dream-water";
            horizon=art.Bitmap("painted dream atmosphere",root,Vector2.zero,new Vector2(32,18),picture,-50);
            horizonPaint=horizon.GetComponent<MeshRenderer>().sharedMaterial;
            if(movement==Movement.Fragments)
            {
                art.Glow(far,new Vector2(18,11),11,new Color(.55f,.65f,.62f,.45f),-35);
                for(int i=0;i<8;i++)
                {
                    float x=i*13-20,y=10+(i*11%6);
                    art.Line(far,new Vector2(x,y),new Vector2(x+.5f,y-3),.016f,new Color(.75f,.75f,.75f,.22f),-25);
                    Transform shard=art.Shape("unconnected memory",far,new Vector2(x+.5f,y-3),new Vector2(.14f,.5f),new Color(.8f,.8f,.8f,.35f),-24);shard.localRotation=Quaternion.Euler(0,0,i*17);
                }
            }
        }
        void RuptureTone(Transform form,Color bottom,Color top)
        {
            var renderer=form.GetComponent<Renderer>();renderer.GetPropertyBlock(recoveryBlock);
            recoveryBlock.SetFloat("_SkyWash",1);recoveryBlock.SetColor("_SkyBottom",bottom);recoveryBlock.SetColor("_SkyTop",top);renderer.SetPropertyBlock(recoveryBlock);
        }
        Transform RuptureForm(string label,Transform parent,Vector2[] points,Color bottom,Color top,int order,bool ribbon=false,float lightDepth=0)
        {
            Vector2 min=points[0],max=points[0];
            foreach(Vector2 point in points){min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
            Vector2 center=(min+max)*.5f,size=max-min;size.x=Mathf.Max(.001f,size.x);size.y=Mathf.Max(.001f,size.y);
            var normalized=new Vector2[points.Length];
            for(int i=0;i<points.Length;i++)normalized[i]=new Vector2((points[i].x-center.x)/size.x,(points[i].y-center.y)/size.y);
            Transform form=ribbon?art.Ribbon(label,parent,normalized,Color.white,order):art.Polygon(label,parent,normalized,Color.white,order);
            form.localPosition=new Vector3(center.x,center.y,0);form.localScale=new Vector3(size.x,size.y,1);RuptureTone(form,bottom,top);
            var renderer=form.GetComponent<Renderer>();renderer.GetPropertyBlock(recoveryBlock);
            recoveryBlock.SetVector("_WashMap",lightDepth>0?new Vector4(size.y/lightDepth,1+min.y/lightDepth,0,0):new Vector4(1,0,0,0));
            recoveryBlock.SetFloat("_RibbonSoft",ribbon?1:0);renderer.SetPropertyBlock(recoveryBlock);return form;
        }
        void BuildRuptureLandscape()
        {
            // Soft stone and directional shading carry the island's material language into side view.
            // The crater retains the same vent position as the actual meteor ejections.
            ruptureSky=Group("kiln sky",far);
            Transform wash=art.Shape("continuous ash sky",ruptureSky,new Vector2(0,.625f),new Vector2(44,36),Color.white,-60);
            RuptureTone(wash,new Color(.72f,.66f,.59f),new Color(.39f,.41f,.46f));
            art.Glow(ruptureSky,new Vector2(7,1),18,new Color(.95f,.82f,.65f,.16f),-59);
            RuptureRidge(-7,3.2f,new Color(.55f,.53f,.51f),-52,.3f);
            RuptureRidge(-9,5.1f,new Color(.44f,.45f,.47f),-51,1.7f);

            Transform crater=Group("sculpted caldera",far);
            RuptureForm("long volcanic shoulder",crater,new[]{new Vector2(-8,-12),new Vector2(5,-5),new Vector2(14,.6f),new Vector2(20,5.5f),new Vector2(28,5.5f),new Vector2(37,-.7f),new Vector2(56,-12)},new Color(.33f,.35f,.38f),new Color(.56f,.53f,.48f),-46);
            RuptureForm("lit shoulder of the cone",crater,new[]{new Vector2(20,5.5f),new Vector2(23,4.8f),new Vector2(20,-3.5f),new Vector2(6,-8),new Vector2(14,.6f)},new Color(.39f,.41f,.42f),new Color(.62f,.58f,.52f),-45);
            RuptureForm("shaded shoulder of the cone",crater,new[]{new Vector2(25,4.8f),new Vector2(28,5.5f),new Vector2(37,-.7f),new Vector2(47,-8),new Vector2(29,-5)},new Color(.29f,.32f,.36f),new Color(.44f,.44f,.43f),-45);
            for(int i=0;i<3;i++)
            {
                float y=2.8f-i*2.2f,w=3.7f+i*3.8f;
                art.Polygon("quiet stone terrace",crater,new[]{new Vector2(24-w,y+.35f),new Vector2(23,y-.2f),new Vector2(24+w,y+.15f),new Vector2(24+w+.5f,y-.2f),new Vector2(24,y-.6f),new Vector2(23-w,y-.1f)},new Color(.65f,.61f,.55f,.13f),-44);
            }
            RuptureTone(art.Shape("rounded crater lip",crater,new Vector2(24,5.1f),new Vector2(8.2f,1.5f),Color.white,-43,true),new Color(.40f,.41f,.42f),new Color(.69f,.64f,.55f));
            RuptureTone(art.Shape("lit crater rim",crater,new Vector2(24,5.42f),new Vector2(8.2f,1.3f),Color.white,-42,true),new Color(.59f,.56f,.51f),new Color(.78f,.72f,.62f));
            RuptureTone(art.Shape("crater depth",crater,new Vector2(24,5.58f),new Vector2(6.9f,.88f),Color.white,-41,true),new Color(.36f,.23f,.18f),new Color(.25f,.27f,.29f));
            art.Shape("crater fire",crater,new Vector2(24,5.45f),new Vector2(5.3f,.32f),new Color(.94f,.31f,.16f),-40,true);
            art.Glow(crater,new Vector2(24,5.5f),3.5f,new Color(1,.35f,.15f,.22f),-39);
            var anchors=new[]{new Vector2(24,5.5f),new Vector2(24.2f,7.2f),new Vector2(25,8.8f),new Vector2(26.6f,10.15f),new Vector2(29,11.15f),new Vector2(32.1f,11.75f),new Vector2(35.8f,11.9f)};
            var centres=new Vector2[(anchors.Length-1)*4+1];
            for(int i=0;i<centres.Length;i++)
            {
                float at=i/(float)(centres.Length-1)*(anchors.Length-1);int a=Mathf.Min(anchors.Length-2,Mathf.FloorToInt(at));float t=at-a;
                Vector2 p0=anchors[Mathf.Max(0,a-1)],p1=anchors[a],p2=anchors[a+1],p3=anchors[Mathf.Min(anchors.Length-1,a+2)];
                centres[i]=.5f*(2*p1+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t);
            }
            for(int layer=0;layer<2;layer++)
            {
                var plume=new Vector2[centres.Length*2];
                for(int i=0;i<centres.Length;i++)
                {
                    Vector2 direction=(centres[Mathf.Min(i+1,centres.Length-1)]-centres[Mathf.Max(0,i-1)]).normalized;
                    Vector2 normal=new Vector2(-direction.y,direction.x);float width=(.52f+.96f*i/(centres.Length-1))*(layer==0?1.3f:1);
                    plume[i*2]=centres[i]-normal*width;plume[i*2+1]=centres[i]+normal*width;
                }
                Color bottom=layer==0?new Color(.48f,.49f,.50f,.26f):new Color(.24f,.26f,.29f,.92f);
                Color top=layer==0?new Color(.57f,.58f,.59f,0):new Color(.48f,.50f,.52f,.06f);
                RuptureForm("continuous wind-carried ash",crater,plume,bottom,top,-38+layer,true);
            }
            RuptureRidge(-13,5.8f,new Color(.34f,.36f,.39f),-33,3.1f);
        }
        void RuptureRidge(float floor,float height,Color colour,int order,float phase)
        {
            // Monotone strips triangulate from the lower edge without overlapping the skyline.
            for(int i=0;i<32;i++)
            {
                float x=-56+i*6.5f;
                float a=floor+height*(.55f+.23f*Mathf.Sin(i*1.25f+phase)+.18f*Mathf.Sin(i*.59f-phase));
                float b=floor+height*(.55f+.23f*Mathf.Sin((i+1)*1.25f+phase)+.18f*Mathf.Sin((i+1)*.59f-phase));
                art.Polygon("distant basalt ridge",far,new[]{new Vector2(x,-35),new Vector2(x+6.5f,-35),new Vector2(x+6.5f,b),new Vector2(x,a)},colour,order);
            }
        }
        Transform Painted(string label,Transform parent,Vector2 at,Vector2 size,string resource,int order,float clipTop=1)
        {
            Transform t=art.Bitmap(label,parent,at,size,resource,order,clipTop);
            hospitalPaint.Add(t.GetComponent<MeshRenderer>().sharedMaterial);return t;
        }
        void BuildHospital()
        {
            sky=new Color(.85f,.84f,.76f);clock=0;
            Painted("the room beyond the dream",root,Vector2.zero,new Vector2(30.4f,17.1f),"hospital-room",-10);
            family=Group("mother approaches the bedside",root);
            motherPaint=Painted("a familiar face",family,new Vector2(0,-1),new Vector2(7.4f,8.9f),"mother-presence",2).GetComponent<MeshRenderer>().sharedMaterial;
            nurse=Group("nurse beside the bed",root);
            nursePaint=Painted("a reassuring presence",nurse,new Vector2(0,-.75f),new Vector2(6.2f,7.45f),"nurse-presence",1).GetComponent<MeshRenderer>().sharedMaterial;
            // Re-render the blanket portion of the same full image in front of the torso cutouts.
            bed=Group("foreground blanket",root);
            Painted("blanket in front of the figures",bed,Vector2.zero,new Vector2(30.4f,17.1f),"hospital-room",8,.22f);
            hand=Group("the familiar hand reaches in",root);
            handPaint=Painted("a hand resting on the blanket",hand,new Vector2(-.6f,-3.5f),new Vector2(5.3f,3.53f),"familiar-hand",12).GetComponent<MeshRenderer>().sharedMaterial;
            ceiling=Group("ceiling first seen through half-open eyes",root);
            Painted("softly focused ceiling",ceiling,Vector2.zero,new Vector2(30.4f,17.1f),"awakening-ceiling",30);
            HospitalPose(0);
        }
        public void HospitalPose(float time)
        {
            if(ceiling==null)return;
            float focus=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,7,time));
            foreach(Material paint in hospitalPaint)paint.SetFloat("_Blur",Mathf.Lerp(16,0,focus));
            ceiling.gameObject.SetActive(time<4);
            ceiling.localPosition=new Vector3(0,Mathf.SmoothStep(0,19,Mathf.InverseLerp(2,4,time)),0);
            float approach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,7,time));
            float breath=Mathf.Sin(time*1.7f)*.018f;
            float motherScale=.64f+.36f*approach;
            family.localScale=new Vector3(motherScale*(1+breath*.12f),motherScale,1);
            family.localPosition=new Vector3(-4.5f+approach*1.8f,-4.8f+5.45f*motherScale+breath,0);
            float nurseScale=.74f+.16f*approach;
            nurse.localScale=Vector3.one*nurseScale;
            nurse.localPosition=new Vector3(5.2f-approach*.5f,-4.8f+4.475f*nurseScale-breath*.5f,0);
            float reveal=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,4.5f,time));
            motherPaint.SetColor("_Color",new Color(1,1,1,reveal));nursePaint.SetColor("_Color",new Color(1,1,1,reveal));
            float reach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(5,8,time));
            hand.localPosition=Vector3.Lerp(new Vector3(-1.2f,1.1f,0),Vector3.zero,reach);
            hand.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-8,0,reach));hand.gameObject.SetActive(time>5);
            handPaint.SetColor("_Color",new Color(1,1,1,Mathf.Clamp01((time-5)*1.6f)));
        }
        public bool Step(float dt,ReturnMotor motor)
        {
            clock+=dt;bool hit=false;LastContactCause="";
            if(chapter==Movement.Rupture)
            {
                ScheduleMeteorRain(motor);
                if(frontX<-90&&motor.position.x>13)frontX=-5;
                if(frontX>-90)
                {
                    // Keep pressure after fast stretches, with time to read the next landing.
                    frontX=Mathf.Min(goalX-24,Mathf.Max(frontX+5.2f*dt,motor.position.x-24));
                    if(motor.position.x<frontX+.4f){hit=true;LastContactCause="pursuit";}
                }
                collapse.localPosition=new Vector3(frontX,0,0);
            }
            foreach(ReturnSurface s in surfaces)
            {
                if(returnOpen&&!recalledReturn&&!ForwardReturnCollapsed&&lowerReturnSurfaces.Contains(s))continue;
                if(returnOpen&&s!=surfaces[0]&&(!s.returnPath||lowerReturnEntered&&s==departureShelf))continue;
                float old=s.x;
                if(s==leaf){if(motor.support==leaf)leafBoarded=true;if(leafBoarded)s.x=Mathf.MoveTowards(s.x,62,2.7f*dt);}
                else if(s==stationFerry)
                {
                    StepStationTrain(dt,motor);
                    foreach(Transform wheel in stationFerryWheels)wheel.localRotation=Quaternion.Euler(0,0,-(s.x-s.originX)/.12f*Mathf.Rad2Deg);
                }
                else if(s.kind==SurfaceKind.Ferry)s.x=s.originX+Mathf.PingPong(clock*2.4f,s.span);
                if(motor.support==s)motor.position.x+=s.x-old;
                if(s.kind==SurfaceKind.Fading||s.kind==SurfaceKind.Collapse)
                {
                    if(s.age>=0)s.age+=dt;
                    float life=s.kind==SurfaceKind.Fading?1.1f:1.55f;
                    if(chapter==Movement.Fragments&&!s.returnPath)life=s.kind==SurfaceKind.Fading?.65f:.85f;
                    s.solid=s.age<life;
                    if(s.age>life+2.1f&&chapter!=Movement.Rupture&&!(ForwardReturnCollapsed&&IsForwardBreakaway(s))){s.age=-1;s.solid=true;}
                    float tremor=s.age>=0&&s.age<life?Mathf.Sin(s.age*45)*.025f:0;
                    s.visual.localPosition=new Vector3(s.x,s.y+tremor,0);
                    s.visual.gameObject.SetActive(s.solid);
                    if(memoryFuses.TryGetValue(s,out Transform fuse))
                    {
                        float remaining=s.age<0?1:Mathf.Clamp01(1-s.age/life),width=s.width-.3f;
                        fuse.localScale=new Vector3(width*remaining,.10f,1);
                        fuse.localPosition=new Vector3(-width*.5f+width*remaining*.5f,-.23f,0);
                        fuse.gameObject.SetActive(!s.returnPath&&s.solid);
                    }
                }
                else s.visual.localPosition=new Vector3(s.x,s.y,0);
                if(chapter==Movement.Rupture&&frontX>-90&&s.Right<frontX)
                {
                    // Passed ground falls permanently. A checkpoint retry restores it.
                    s.solid=false;
                    float fallen=frontX-s.Right;
                    s.visual.localPosition=new Vector3(s.x,s.y-Mathf.Min(18,fallen*fallen*.2f),0);
                    s.visual.gameObject.SetActive(fallen<12);
                }
                if(chapter==Movement.Rupture||IsForwardBreakaway(s))
                {
                    float timedDamage=s.kind==SurfaceKind.Collapse&&s.age>=0?Mathf.Clamp01(s.age/1.55f):0;
                    float frontDamage=frontX>-90?Mathf.InverseLerp(s.Left-2,s.Right,frontX):0;
                    float timedFall=s.kind==SurfaceKind.Collapse&&s.age>=1.55f?s.age-1.55f:-1;
                    float frontFall=frontX>s.Right?(frontX-s.Right)/5.2f:-1,fallAge=Mathf.Max(timedFall,frontFall);
                    if(fallAge>=0){s.visual.localPosition=new Vector3(s.x,s.y,0);s.visual.gameObject.SetActive(fallAge<2.1f);}
                    UpdateRuptureRock(s,Mathf.Max(timedDamage,frontDamage),fallAge);
                }
            }
            foreach(ReturnHazard h in hazards)
            {
                if(returnOpen)continue;
                if(h.debris)
                {
                    // Rupture requires a reaction; the Fragments memory shelf retains its original timing.
                    bool ruptureDebris=chapter==Movement.Rupture;
                    float leadDistance=ruptureDebris?DebrisLeadDistance:10;
                    if(!ruptureDebris&&h.timer<0&&Mathf.Abs(motor.position.x-h.x)<leadDistance)h.timer=0;
                    if(h.timer>=0)h.timer+=dt;
                    bool warningVisible=h.timer>=0&&h.timer<DebrisImpactTime;
                    float warningScale=1;
                    if(ruptureDebris&&h.forecastSurface!=null)
                    {
                        // Keep the forecast at the fixed impact point, clipped to the current support.
                        // A moving or collapsing shelf must not leave a floating red strip behind.
                        ReturnSurface target=h.forecastSurface;
                        float left=Mathf.Max(h.x-DebrisImpactRadius,target.Left),right=Mathf.Min(h.x+DebrisImpactRadius,target.Right);
                        warningVisible&=target.solid&&target.visual.gameObject.activeInHierarchy&&right>left;
                        warningScale=Mathf.Max(0,right-left)/(DebrisImpactRadius*2);
                        h.warning.localPosition=new Vector3((left+right)*.5f,h.y+.12f,0);
                    }
                    h.warning.gameObject.SetActive(warningVisible);
                    h.warning.localScale=new Vector3(warningScale,1+.18f*Mathf.Sin(Mathf.Max(0,h.timer)*9),1);
                    h.visual.gameObject.SetActive(h.timer>=DebrisWarningSeconds&&h.timer<DebrisImpactTime);
                    float y=h.y+DebrisDropHeight-(h.timer-DebrisWarningSeconds)*DebrisFallSpeed;
                    h.visual.localPosition=new Vector3(h.x,y,0);
                    Quaternion spin=Quaternion.Euler(0,0,Mathf.Max(0,h.timer-DebrisWarningSeconds)*95);
                    h.visual.localRotation=ruptureDebris?Quaternion.identity:spin;
                    if(ruptureDebris&&h.stone!=null)h.stone.localRotation=spin;
                    if(h.ejection!=null)
                    {
                        bool launching=h.timer>=0&&h.timer<1.15f;h.ejection.gameObject.SetActive(launching);
                        float age=Mathf.Max(0,h.timer),direction=h.x%2<1?-1:1;
                        h.ejection.localPosition=new Vector3(24+direction*age*10,5.5f+age*15-age*age*9,0);
                        h.ejection.localRotation=Quaternion.Euler(0,0,age*150);
                    }
                    float impactAge=h.timer-DebrisImpactTime;bool impactActive=impactAge>=0&&impactAge<.24f;
                    bool impactNow=ruptureDebris&&impactAge>=0&&!h.impactCounted;
                    if(impactNow){h.impactCounted=true;MeteorImpacts++;}
                    h.impact.gameObject.SetActive(impactActive);h.impact.localScale=new Vector3(1+Mathf.Max(0,impactAge)*1.5f,1+Mathf.Max(0,impactAge)*2,1);
                    if(h.visual.gameObject.activeSelf&&Mathf.Abs(motor.position.x-h.x)<.7f&&y<motor.position.y+ReturnMotor.Height&&y>motor.position.y-.5f)
                    {hit=true;if(LastContactCause.Length==0)LastContactCause="falling meteor x="+h.x.ToString("F2")+", age="+h.timer.ToString("F3")+", feet="+motor.position;}
                    if(impactNow&&Mathf.Abs(motor.position.x-h.x)<DebrisImpactRadius&&motor.position.y<h.y+.45f&&motor.position.y+ReturnMotor.Height>h.y-.5f)
                    {hit=true;if(LastContactCause.Length==0)LastContactCause="meteor impact x="+h.x.ToString("F2")+", age="+h.timer.ToString("F3")+", feet="+motor.position;}
                }
                else
                {
                    float x=h.x+Mathf.Sin(clock*1.3f)*1.7f;h.visual.localPosition=new Vector3(x,h.y,0);
                    if(Mathf.Abs(motor.position.x-x)<.36f&&motor.position.y<h.y+1.3f&&motor.position.y+ReturnMotor.Height>h.y+.1f)hit=true;
                }
            }
            return hit;
        }
        public void ResetHazards()
        {
            pickupStarted=placementStarted=-1;figure?.KeyHandPose(Vector2.zero,false);
            nextMeteorAt=.45f;meteorRandom=0x7254u;
            clock=0;leafBoarded=false;frontX=chapter==Movement.Rupture&&checkpoint.x>13?checkpoint.x-24:-100;
            lastPoseClock=lastImpact=0;wasBursting=false;
            foreach(Transform mote in dust)if(mote!=null)mote.gameObject.SetActive(false);
            for(int i=0;i<dustAge.Length;i++)dustAge[i]=1;
            foreach(ReturnSurface s in surfaces){s.Reset();UpdateRuptureRock(s,0,-1);bool retired=returnOpen&&s!=surfaces[0]&&(!s.returnPath||lowerReturnEntered&&s==departureShelf);bool broken=ForwardReturnCollapsed&&IsForwardBreakaway(s);if(retired||broken)s.solid=false;if(broken){s.age=4;UpdateRuptureRock(s,1,2.45f);}s.visual.gameObject.SetActive(!broken&&(recalledReturn||!retired));}
            foreach(ReturnHazard h in hazards){h.timer=-1;h.impactCounted=false;h.forecastSurface=null;if(h.warning!=null)h.warning.gameObject.SetActive(false);if(h.impact!=null)h.impact.gameObject.SetActive(false);if(h.ejection!=null)h.ejection.gameObject.SetActive(false);}
        }
        void MovementBreath(Vector2 position,float facing,bool burst)
        {
            int count=burst?10:8;
            for(int i=0;i<count;i++)
            {
                int index=dustCursor++%dust.Length;float angle=Mathf.PI*i/(count-1);
                dust[index].gameObject.SetActive(true);dust[index].localPosition=position;
                dustAge[index]=0;dustVelocity[index]=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(.7f+i%3*.35f)+Vector2.left*(burst?facing*2:0);
            }
        }
        public void SetRecallAtmosphere(float opacity)
        {
            bool visible=opacity>.001f;
            if(recallAtmosphereRenderers==null)
            {
                var renderers=new List<Renderer>();
                if(far!=null)renderers.AddRange(far.GetComponentsInChildren<Renderer>(true));
                if(near!=null)renderers.AddRange(near.GetComponentsInChildren<Renderer>(true));
                if(horizon!=null)renderers.Add(horizon.GetComponent<Renderer>());
                recallAtmosphereRenderers=renderers.ToArray();recallAtmosphere=new MaterialPropertyBlock();
            }
            recallAtmosphere.SetFloat("_RecallOpacity",Mathf.Clamp01(opacity));
            foreach(var renderer in recallAtmosphereRenderers)renderer.SetPropertyBlock(recallAtmosphere);
            if(far!=null)far.gameObject.SetActive(visible);
            if(near!=null)near.gameObject.SetActive(visible);
            if(horizon!=null)horizon.gameObject.SetActive(visible);
        }
        public void Pose(ReturnMotor motor,Vector2 cameraPosition,bool carrying,float colourRecovery,bool resting=false,float framing=7.2f,bool suspended=false,float memoryVoice=0)
        {
            if(player==null)return;
            float dt=clock<lastPoseClock?0:Mathf.Min(clock-lastPoseClock,.1f);lastPoseClock=clock;
            restBlend=Mathf.MoveTowards(restBlend,resting?1:0,dt*3.8f);
            player.localPosition=motor.position+new Vector2(-.5f*restBlend,.18f*restBlend);player.localScale=new Vector3(motor.facing,1,1);
            PoseFootingHint(motor);
            player.localRotation=Quaternion.Euler(0,0,-90*Mathf.SmoothStep(0,1,restBlend));
            if(chapter==Movement.Fragments)PosePocketLight(carrying);else if(figure.CleanTraveller)figure.KeyHandPose(Vector2.zero,false);
            figure.Pose(clock,suspended?Vector2.zero:new Vector2(motor.velocity.x*motor.facing,motor.velocity.y),motor.Grounded,carrying,resting,motor.Bursting,motor.LandingImpact,gesture);
            memoryScenery?.PoseMemory(clock,memoryVoice);
            if(reform>0)player.localScale*=Mathf.Lerp(.2f,1,1-reform);
            if(horizon!=null)
            {
                horizon.localPosition=new Vector3(cameraPosition.x+(Mathf.PingPong(cameraPosition.x*.035f,2)-1),cameraPosition.y,0);
                horizon.localScale=new Vector3(32*framing/7.2f,18*framing/7.2f,1);
                float light=chapter==Movement.Fragments?.46f+.13f*reconnected:.92f;
                float release=chapter==Movement.Rupture?Mathf.InverseLerp(goalX-50,goalX,motor.position.x):0;
                horizonPaint.SetColor("_Color",new Color(light,light,light,chapter==Movement.Rupture?Mathf.Lerp(1,.07f,release):chapter==Movement.Reunion?.48f:.96f));
            }
            if(dt>0)
            {
                if(motor.LandingImpact>.12f&&motor.LandingImpact>lastImpact+.05f)MovementBreath(motor.position,motor.facing,false);
                if(motor.Bursting&&!wasBursting)MovementBreath(motor.position+Vector2.up*.7f,motor.facing,true);
                lastImpact=motor.LandingImpact;wasBursting=motor.Bursting;
                for(int i=0;i<dust.Length;i++)
                {
                    if(dustAge[i]>=.48f){dust[i].gameObject.SetActive(false);continue;}
                    dustAge[i]+=dt;dust[i].localPosition+=(Vector3)(dustVelocity[i]*dt);dustVelocity[i].y-=dt*1.7f;
                    dust[i].localScale=Vector3.one*(.08f*Mathf.Pow(Mathf.Max(0,1-dustAge[i]/.48f),1.3f));
                }
            }
            far.localPosition=new Vector3(cameraPosition.x*.68f,cameraPosition.y*.3f,0);
            if(ruptureSky!=null)
            {
                ruptureSky.localPosition=new Vector3(cameraPosition.x*.32f,cameraPosition.y*.7f,0);
                ruptureSky.localScale=Vector3.one*(framing/7.2f);
            }
            near.localPosition=new Vector3(cameraPosition.x,cameraPosition.y,0);
            for(int i=0;i<motes.Count;i++)
            {Transform m=motes[i];m.localPosition=new Vector3((i*17.31f+clock*(chapter==Movement.Rupture?-2:.18f))%36-18,(i*7.27f+Mathf.Sin(clock*.5f+i)*.2f)%19-7,0);}
            if(fragment!=null&&SignalProgress<0&&chapter!=Movement.Fragments)
            {
                fragment.localPosition=carrying?new Vector3(motor.position.x+motor.facing*.44f,motor.position.y+1.07f+Mathf.Sin(clock*3)*.025f,0):new Vector3(goalX,goalY+Mathf.Sin(clock*2)*.18f,0);
                fragment.localRotation=Quaternion.Euler(0,0,Mathf.Sin(clock)*7);
            }
            if(fruit!=null)
            {
                float eating=colourRecovery*9;
                Vector2 mouth=motor.position+new Vector2(motor.facing*.1f,1.18f);
                fruit.localPosition=Vector2.Lerp(new Vector2(75.4f,2.1f),mouth,Mathf.SmoothStep(0,1,eating/.75f));
                fruit.localScale=Vector3.one*(1-Mathf.InverseLerp(.85f,1.3f,eating));
                fruit.gameObject.SetActive(eating<1.3f);
            }
            if(yellowMemory!=null)yellowMemory.gameObject.SetActive(colourRecovery>.08f);
            if(redMemory!=null)redMemory.gameObject.SetActive(colourRecovery>.36f);
            if(blueMemory!=null)blueMemory.gameObject.SetActive(colourRecovery>.69f);
            for(int i=0;i<branches.Count;i++)branches[i].localRotation=Quaternion.Euler(0,0,Mathf.Sin(clock*.65f+i)*2.5f);
            if(radioNeedle!=null)radioNeedle.localPosition=new Vector3(.32f+branch*.19f+Mathf.Sin(clock*2)*.025f,1.18f,0);
        }
    }
}
