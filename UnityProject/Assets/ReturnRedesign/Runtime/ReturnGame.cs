using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Return.Redesign
{
    public struct ReturnInput
    {
        public float move,vertical;
        public Vector2 look;
        public bool jumpPressed,jumpHeld,interact,retry,burstPressed;
    }
    public sealed class ReturnGame : MonoBehaviour
    {
        sealed class MacPreviewActivity : IDisposable
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            IntPtr processInfo,activity;int previousVSync;
            const string ObjCLibrary="/usr/lib/libobjc.A.dylib";
            [System.Runtime.InteropServices.DllImport(ObjCLibrary,EntryPoint="objc_getClass")] static extern IntPtr Class(string name);
            [System.Runtime.InteropServices.DllImport(ObjCLibrary,EntryPoint="sel_registerName")] static extern IntPtr Selector(string name);
            [System.Runtime.InteropServices.DllImport(ObjCLibrary,EntryPoint="objc_msgSend")] static extern IntPtr Message(IntPtr receiver,IntPtr selector);
            [System.Runtime.InteropServices.DllImport(ObjCLibrary,EntryPoint="objc_msgSend")] static extern IntPtr StringMessage(IntPtr receiver,IntPtr selector,[System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)]string value);
            [System.Runtime.InteropServices.DllImport(ObjCLibrary,EntryPoint="objc_msgSend")] static extern IntPtr BeginActivity(IntPtr receiver,IntPtr selector,ulong options,IntPtr reason);
            [System.Runtime.InteropServices.DllImport(ObjCLibrary,EntryPoint="objc_msgSend")] static extern void EndActivity(IntPtr receiver,IntPtr selector,IntPtr token);
#endif
            public MacPreviewActivity()
            {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                processInfo=Message(Class("NSProcessInfo"),Selector("processInfo"));
                IntPtr reason=StringMessage(Class("NSString"),Selector("stringWithUTF8String:"),"RETURN live preview and review");
                // NSActivityUserInitiatedAllowingIdleSystemSleep, from NSProcessInfo.h.
                activity=BeginActivity(processInfo,Selector("beginActivityWithOptions:reason:"),0x00EFFFFFUL,reason);
                if(activity==IntPtr.Zero)throw new InvalidOperationException("macOS preview activity was not created");
                activity=Message(activity,Selector("retain"));
                previousVSync=QualitySettings.vSyncCount;QualitySettings.vSyncCount=0;
                Debug.Log("[RETURN preview sync] vSync="+previousVSync+"->"+QualitySettings.vSyncCount+"; quality="+QualitySettings.GetQualityLevel());
                Debug.Log("[RETURN preview activity] Begin; idle system sleep allowed");
#endif
            }
            public void Dispose()
            {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                if(activity==IntPtr.Zero)return;IntPtr token=activity;activity=IntPtr.Zero;
                try{EndActivity(processInfo,Selector("endActivity:"),token);}
                finally{Message(token,Selector("release"));QualitySettings.vSyncCount=previousVSync;}
                Debug.Log("[RETURN preview sync] Restored vSync="+QualitySettings.vSyncCount+"; quality="+QualitySettings.GetQualityLevel());
                Debug.Log("[RETURN preview activity] End");
#endif
            }
        }
        MacPreviewActivity previewActivity;
        ReturnPreviewSurface previewSurface;
        bool drawingPreviewUI;
        public ReturnMotor motor {get;private set;}
        public ReturnWorld world {get;private set;}
        public Movement Chapter {get;private set;}
        public int Excursion {get;private set;}
        public int Fragments {get;private set;}
        public int Failures {get;private set;}
        public bool Carrying {get;private set;}
        public bool Playing {get;private set;}
        public bool Completed=>Chapter==Movement.Credits;
        public bool Resting {get;private set;}
        public float ColourRecovery {get;private set;}
        public bool ReducedMotion {get;private set;}
        public float ChapterElapsed=>chapterTime;
        public bool BurstUnlocked=>Fragments>0;
        int burstLessonStage;
        public int BurstLessonStage=>burstLessonStage;
        public bool ReturnDescent {get;private set;}
        bool radioPlacementPending;
        int pendingMemoryVoice=-1;
        public bool CanDepositFragment=>Chapter==Movement.Fragments&&!InTransition&&!radioPlacementPending&&!world.PickupAnimationActive&&Carrying&&ReturnDescent&&motor.Grounded&&motor.support==world.surfaces[0]&&Mathf.Abs(motor.position.x-world.radioX)<1.4f;
        public bool CanRememberFragment=>Chapter==Movement.Fragments&&!InTransition&&!Carrying&&motor.Grounded&&Mathf.Abs(motor.position.x-world.goalX)<1.6f&&Mathf.Abs(motor.position.y-(world.goalY-1.35f))<.2f;
        public bool PlaceCaptionShown=>locationSeen;
        public Vector3 PlaceCaptionViewport {get;private set;}
        public bool InTransition=>transition>=0;
        public readonly List<string> journal=new List<string>();
        public readonly List<string> milestones=new List<string>();
        ReturnArt art;
        ReturnSound sound;
        public float MemoryVoiceLevel(int index)=>sound.VoiceLevel("memory-"+index);
        public bool MemoryVoiceOutputObserved(int index)=>sound.VoiceOutputObserved("memory-"+index);
        public float MemoryVoiceAudibleTravel(int index)=>sound.VoiceAudibleTravel("memory-"+index);
        public int MemoryVoiceAudiblePositionSamples(int index)=>sound.VoiceAudiblePositionSamples("memory-"+index);
        public ReturnVoiceOutputMeter.PositionWindow MemoryVoicePositionWindow(int index)=>sound.VoicePositionWindow("memory-"+index);
        public float RuptureVoiceLevel(int index)=>sound.VoiceLevel(RuptureVoiceKeys[index]);
        public bool RuptureVoiceOutputObserved(int index)=>sound.VoiceOutputObserved(RuptureVoiceKeys[index]);
        public bool HospitalVoiceOutputObserved(bool familiar)=>sound.VoiceOutputObserved(familiar?"hospital-mum":"hospital-nurse");
        public bool HospitalVoicesQuiet=>sound.VoiceLevel("hospital-nurse")==0&&sound.VoiceLevel("hospital-mum")==0;
        public bool FinalEscapeVoiceCompleted {get;private set;}
        bool finalEscapeVoicePending;
        public ReturnJourney3D journey {get;private set;}
        Camera cam;
        Rect drawnStoryBounds,drawnHintBounds;
        public bool LowerReturnView=>Chapter==Movement.Fragments&&Carrying&&ReturnDescent;
        public bool LowerReturnReadable()
        {
            if(!LowerReturnView||world.player==null||cam==null)return false;
            bool found=false;Bounds bounds=default;
            foreach(var renderer in world.player.GetComponentsInChildren<MeshRenderer>())if(renderer.enabled&&renderer.gameObject.activeInHierarchy){if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);}
            if(!found)return false;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);Vector2 margin=new Vector2((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f);
            Vector2 minimum=new Vector2(float.PositiveInfinity,float.PositiveInfinity),maximum=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for(int i=0;i<8;i++){Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));Vector3 p=cam.WorldToScreenPoint(corner);if(p.z<=cam.nearClipPlane)return false;Vector2 point=(new Vector2(p.x,Screen.height-p.y)-margin)/scale;minimum=Vector2.Min(minimum,point);maximum=Vector2.Max(maximum,point);}
            Rect actor=Rect.MinMaxRect(minimum.x-8,minimum.y-8,maximum.x+8,maximum.y+8);
            return actor.xMin>32&&actor.xMax<1248&&actor.yMin>224&&actor.yMax<628&&!actor.Overlaps(drawnStoryBounds)&&!actor.Overlaps(drawnHintBounds);
        }
        float elapsed,chapterTime,prologue=-1,fade,transition=-1,retryFlash,accumulator,colourTime,memoryGesture,lineTime,burstLessonFeedback;
        string storyLine="",pendingStoryLine="";
        float pendingStoryDuration;
        bool memoryCaptionProtected,connectionCaptionProtected,pendingStoryProtected;
        public string ActiveStoryLine=>lineTime>0?storyLine:"";
        public bool MemoryCaptionProtected=>memoryCaptionProtected;
        public bool ConnectionCaptionActive=>connectionCaptionProtected&&lineTime>0;
        bool locationSeen,journalOpen;
        int journalPage;
        public bool JournalOpen=>journalOpen;
        public bool PauseMenuOpen=>paused;
        bool MemoryFound(int index)=>Fragments>index||journal.Contains(Memories[index]);
        public int RecoveredVoices {get{int count=0;for(int i=0;i<Memories.Length;i++)if(MemoryFound(i))count++;return count;}}
        public int JournalPageCount=>1+(JourneyNotes().Count+2)/3;
        public int JournalPage=>journalPage;
        public void ToggleJournal(){if(!Playing)return;journalOpen=!journalOpen;journalPage=0;accumulator=0;}
        public void BackFromOverlay(){if(journalOpen){ToggleJournal();return;}if(Playing&&Chapter!=Movement.Credits)paused=!paused;}
        public void MoveJournalPage(int direction){if(journalOpen)journalPage=(journalPage+direction%JournalPageCount+JournalPageCount)%JournalPageCount;}
        List<string> JourneyNotes(){return journal.FindAll(entry=>Array.IndexOf(Memories,entry)<0);}
        int ruptureBeat;
        public int RuptureStoryBeats=>ruptureBeat;
        Movement nextChapter;
        int nextExcursion,transitionFromExcursion;
        public int TuningRoute=>transition>=0&&nextChapter==Movement.Fragments?nextExcursion:-1;
        public float TuningProgress=>TuningRoute>=0?Mathf.Clamp01(transition/3):0;
        public float BranchViewHeading {get;private set;}
        static readonly string[] DirectionNames={"NORTH","EAST","SOUTH","WEST"};
        bool paused,title=true,transitionBuilt;
        Vector2 cameraPosition;
        float cameraLead;
        ReturnSurface previousSafe;
        GUIStyle heading,small,caption,button,hintStyle,memoryText;
        readonly List<Texture2D> uiTextures=new List<Texture2D>();
        Texture2D panelTexture,eyelidTexture;
        string previewRoot;
        int previewProcessId;
        float nextCapture;
        readonly bool measureUpdate=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-measure-update")>=0;
        readonly bool streetWalkProbe=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-street-walk-probe")>=0;int streetWalkProbePhase=-1;
        int measuredUpdateFrames,measuredUpdateFocusedFrames;
        double measuredUpdateMs,measuredFrameMs;
        long previousMeasuredUpdateAt;
        double measuredWallFrameMs,measuredMaxWallFrameMs;
        int measuredSlowWallFrames;
        uint previewFrameSlot;
        ReturnPreviewCapture previewCapture;
        bool captureBusy=>previewCapture!=null&&previewCapture.Busy;
        bool pendingJump,pendingInteract,pendingRetry,pendingBurst;
        ReturnAcceptance acceptance;
        ReturnShoreHandoffReview shoreHandoffReview;
        bool reviewWater,reviewMotion,reviewEscape,reviewFragments;
        bool traceMemoryFrames,traceRuptureFrames,memoryTimingActive,memoryTimingFocused;
        long memoryFrameAt,memoryUpdateEndAt,memoryRenderAt,memoryRenderEndAt,memoryEndAt;
        double memoryRenderSeconds,memorySimulationSeconds;
        float memoryFrameDelta;
        int memoryTimingFrame,memoryTimingRoute;
        readonly int[] memorySlowFrames=new int[4];
        sealed class MemoryLoopMarker {}
        sealed class MemoryLoopStage {public string name;public long started;public double seconds;}
        readonly List<MemoryLoopStage> memoryLoopStages=new List<MemoryLoopStage>();
        UnityEngine.LowLevel.PlayerLoopSystem memoryOriginalLoop;
        bool memoryLoopInstalled;
        MemoryLoopStage memoryCurrentStage;
        System.Threading.Timer memoryStallTimer;
        string memoryStallEvidence,memoryStallFailure;
        int memoryStallRequested,memoryWatchActive,memoryWatchFrame,memoryWatchRoute;
        bool reviewDriftWaterControl,driftWaterControlActive,reviewDriftMeshProbe,reviewDriftOutputControl,reviewDriftSkinControl,reviewDriftCullingControl,reviewDriftSkyControl,reviewDriftDrawControl,reviewDriftTransformControl,reviewDriftRendererControl,reviewDriftListControl,reviewDriftOrderControl;
        bool reviewDriftTransparentControl,reviewDriftTransparentSetControl,reviewDriftInkControl;
        bool reviewReverseVisibility;
        readonly List<ReverseVisibilitySample> reverseVisibilitySamples=new List<ReverseVisibilitySample>();
        bool reviewShoreMotion,reviewShoreOcclusion,reviewIslandReturnMotion;
        bool reviewEyeSurfaces,eyeSurfacesStarted;
        ReturnEyeSurfaceReview eyeSurfaceReview;
        bool reviewCloth,clothReviewStarted;
        ReturnClothSurfaceReview clothSurfaceReview;
        bool reviewDreamerSurfaces,dreamerSurfaceStarted;
        ReturnDreamerSurfaceReview dreamerSurfaceReview;
        public bool MotionReviewActive=>reviewMotion&&(Chapter==Movement.StillWater||Chapter==Movement.Reunion||Chapter==Movement.Hospital||Chapter==Movement.Credits);
        public float MotionReviewPausedSeconds {get;private set;}
        public bool EscapeReviewActive=>reviewEscape&&Chapter==Movement.Rupture;
        public bool EscapeReviewRequested=>reviewEscape;
        public float EscapeReviewPausedSeconds {get;private set;}
        public bool FragmentsReviewActive=>reviewFragments&&(Chapter==Movement.Fragments||firstStageWhiteout&&transition>=0);
        bool firstStageWhiteout;
        public float FirstStageWhiteHoldSeconds {get;private set;}
        public float RadioCameraShakeMagnitude {get;private set;}
        public float FirstStageWhiteAlpha=>!firstStageWhiteout||transition<0?0:transition<1.1f?Mathf.SmoothStep(0,1,transition/1.1f):transition<=3.1f?1:1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.1f,6,transition));
        public bool FragmentsReviewRequested=>reviewFragments;
        public float FragmentsReviewPausedSeconds {get;private set;}
        public static readonly string[] ChapterNames={"FRAGMENTS","RUPTURE","STILL WATER","RETURN","A FAMILIAR ROOM","RETURN"};
        public static readonly string[] RouteNames={"The kitchen · a lamp left on","The stairwell · the weight of expectation","The station · an unsent message","The rooftop · someone stayed"};
        public static readonly string[] Memories={
            "Warmth. Two cups under a lamp.\n“I left it on for you.” For a moment, the cold eases.",
            "Shame. Another landing. My chest tightens.\n“Can you hear me?” I can't tell where the voice is coming from.",
            "Regret. A message left unfinished.\n“I'm still waiting.” I reach for an answer. Nothing comes.",
            "Fear. I need the noise to stop.\n“Stay with me.” I hear the words, but can't place the voice."};
        static readonly string[] ConnectionLines={
            "The warmth stays in my hands. When I let go, the radio answers.",
            "The stairs are gone. Their weight stays. A voice comes through the radio.",
            "I have no words. I send the signal anyway."};
        static readonly float[] RuptureBeatPositions={24,68,116,153,196};
        static readonly string[] RuptureVoiceKeys={"rupture-0","rupture-1","rupture-2","memory-3","rupture-4"};
        static readonly string[] RuptureLines={
            "The radio is still here. This is the same place.\nI can feel it breaking. I do not remember what happened.",
            "Every sound feels like another demand.\nI cannot tell whether it is the voice or what I am feeling.",
            "There was a message I never sent.\nThe silence made me feel further away.",
            "I wanted the pressure to stop.\nA voice kept saying, “Stay with me.”",
            "The noise is fading. That voice is still here.\nOne more jump."};
        public static readonly string[] Places={
            "Two cups. One cold. A voice with no face.",
            "A landing. Another above it. Each step feels heavier.",
            "A clock with no hands. The last message is still waiting to be sent.",
            "The ground drops away. Somewhere beyond it, a voice keeps calling."};
        void Awake()
        {
            Debug.Log("[RETURN startup] Awake entered");
            string[] args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--return-review-client-journey")>=0){reviewMotion=reviewEscape=reviewFragments=true;acceptance=new ReturnAcceptance(this);}
            if(Array.IndexOf(args,"--return-audio-control")>=0)
            {
                gameObject.AddComponent<ReturnAudioControl>();enabled=false;return;
            }
            for(int i=0;i<args.Length;i++)
            {
                if(args[i]=="--return-preview-root"&&i+1<args.Length)previewRoot=args[i+1];
                if(args[i]=="--return-self-test"||args[i]=="--return-review-motion"||args[i]=="--return-review-escape"||args[i]=="--return-review-fragments"||args[i]=="--return-review-cloth")acceptance=new ReturnAcceptance(this);
                if(args[i]=="--return-review-motion")reviewMotion=true;
                if(args[i]=="--return-drift-water-control")reviewDriftWaterControl=true;
                if(args[i]=="--return-drift-mesh-probe")reviewDriftMeshProbe=true;
                if(args[i]=="--return-drift-output-control")reviewDriftOutputControl=true;
                if(args[i]=="--return-drift-skin-control")reviewDriftSkinControl=true;
                if(args[i]=="--return-drift-culling-control")reviewDriftCullingControl=true;
                if(args[i]=="--return-drift-sky-control")reviewDriftSkyControl=true;
                if(args[i]=="--return-drift-draw-control")reviewDriftDrawControl=true;
                if(args[i]=="--return-drift-transform-control"){reviewDriftDrawControl=true;reviewDriftTransformControl=true;}
                if(args[i]=="--return-drift-renderer-control"){reviewDriftDrawControl=true;reviewDriftRendererControl=true;}
                if(args[i]=="--return-drift-list-control"){reviewDriftDrawControl=true;reviewDriftRendererControl=true;reviewDriftListControl=true;}
                if(args[i]=="--return-drift-order-control"){reviewDriftDrawControl=true;reviewDriftRendererControl=true;reviewDriftListControl=true;reviewDriftOrderControl=true;}
                if(args[i]=="--return-drift-transparent-control"){reviewDriftDrawControl=true;reviewDriftRendererControl=true;reviewDriftListControl=true;reviewDriftOrderControl=true;reviewDriftTransparentControl=true;}
                if(args[i]=="--return-drift-transparent-set-control"){reviewDriftWaterControl=true;reviewDriftTransparentSetControl=true;}
                if(args[i]=="--return-drift-ink-control"){reviewDriftWaterControl=true;reviewDriftTransparentSetControl=true;reviewDriftInkControl=true;}
                if(args[i]=="--return-reverse-visibility-review"){reviewDriftWaterControl=true;reviewReverseVisibility=true;}
                if(args[i]=="--return-review-shore-motion")reviewShoreMotion=true;
                if(args[i]=="--return-review-island-return-motion")reviewIslandReturnMotion=true;
                if(args[i]=="--return-review-shore-occlusion")reviewShoreOcclusion=true;
                if(args[i]=="--return-review-escape")reviewEscape=true;
                if(args[i]=="--return-review-fragments")reviewFragments=true;
                if(args[i]=="--return-review-eye-surfaces")reviewEyeSurfaces=true;
                if(args[i]=="--return-review-cloth")reviewCloth=true;
                if(args[i]=="--return-review-dreamer-surfaces")reviewDreamerSurfaces=true;
            }
            traceRuptureFrames=Array.IndexOf(args,"--return-measure-frame-loop")>=0;
            traceMemoryFrames=traceRuptureFrames||(reviewFragments&&Array.IndexOf(args,"--return-review-memory-timing")>=0);
            if(traceMemoryFrames)
            {
                InstallMemoryLoopTiming();
                UnityEngine.Rendering.RenderPipelineManager.beginFrameRendering+=MemoryRenderBegin;
                UnityEngine.Rendering.RenderPipelineManager.endFrameRendering+=MemoryRenderEnd;
                StartCoroutine(MemoryFrameEnd());
            }
            if(!string.IsNullOrEmpty(previewRoot))
            {
                Application.runInBackground=true;
                previewActivity=new MacPreviewActivity();
                using(var process=System.Diagnostics.Process.GetCurrentProcess())previewProcessId=process.Id;
            }
            Debug.Log("[RETURN startup] Preview configured; background="+Application.runInBackground);
            Initialize();
            if(Application.isBatchMode&&!string.IsNullOrEmpty(previewRoot))previewSurface=new ReturnPreviewSurface(cam,DrawPreviewInterface);
            Debug.Log("[RETURN startup] Initialize complete");
            if(reviewDriftWaterControl||reviewDriftMeshProbe||reviewDriftOutputControl||reviewDriftSkinControl||reviewDriftCullingControl||reviewDriftSkyControl||reviewDriftDrawControl){Begin();Fragments=4;Rebuild(Movement.StillWater,0);storyLine="";lineTime=0;reviewWater=true;}
            else if(acceptance!=null)
            {Begin();if(Array.IndexOf(args,"--return-review-escape-only")>=0){Fragments=4;Rebuild(Movement.Rupture,0);}else if(Array.IndexOf(args,"--return-review-traveller-only")>=0||Array.IndexOf(args,"--return-review-street-only")>=0||Array.IndexOf(args,"--return-review-hospital-only")>=0){Fragments=4;Rebuild(Movement.StillWater,0);}}
            else if(Array.IndexOf(args,"--return-play-rupture")>=0){Begin();Fragments=4;Rebuild(Movement.Rupture,0);storyLine="";lineTime=0;}
            else if(Array.IndexOf(args,"--return-play-street")>=0){Begin();Fragments=4;Rebuild(Movement.StillWater,0);storyLine="";lineTime=0;journey.BeginStreetPlaytest();}
            else if(Array.IndexOf(args,"--return-preview-play")>=0)StartOpening();
            else if(Array.IndexOf(args,"--return-play-leaf")>=0){Begin();Fragments=4;Rebuild(Movement.StillWater,0);storyLine="";lineTime=0;reviewWater=true;journey.BeginLeafPlaytest();}
            else if(Array.IndexOf(args,"--return-review-water")>=0){Begin();Fragments=4;Rebuild(Movement.StillWater,0);storyLine="";lineTime=0;reviewWater=true;if(reviewDreamerSurfaces)journey.BeginDreamerSurfaceReview();if(Array.IndexOf(args,"--return-review-shore-handoff")>=0){journey.BeginShoreHandoffReview();shoreHandoffReview=new ReturnShoreHandoffReview(this);}}
        }
        public void Initialize()
        {
            if(art!=null)return;
            Application.targetFrameRate=60;
            art=new ReturnArt();motor=new ReturnMotor();world=new ReturnWorld(art);
            GameObject cameraObject=new GameObject("Return camera");cameraObject.transform.SetParent(transform,false);
            bool hasListener=false;
            foreach(AudioListener listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if(listener.isActiveAndEnabled)hasListener=true;
            if(!hasListener)cameraObject.AddComponent<AudioListener>();
            cam=cameraObject.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=7.2f;cam.clearFlags=CameraClearFlags.SolidColor;cam.nearClipPlane=.1f;cam.farClipPlane=100;
            cam.transform.position=new Vector3(6,3,-30);
            sound=new ReturnSound(gameObject);
            journey=new ReturnJourney3D(cam);
            Rebuild(Movement.Fragments,0);
        }
        void StartOpening()
        {title=false;paused=false;journalOpen=false;Playing=false;prologue=0;accumulator=0;sound.StopSpeech();}
        public void Begin()
        {
            sound.StopSpeech();
            firstStageWhiteout=false;FirstStageWhiteHoldSeconds=0;RadioCameraShakeMagnitude=0;
            radioPlacementPending=false;
            pendingMemoryVoice=-1;
            reviewWater=false;MotionReviewPausedSeconds=EscapeReviewPausedSeconds=FragmentsReviewPausedSeconds=0;
            title=false;paused=false;prologue=-1;Playing=true;Fragments=Excursion=Failures=0;Carrying=Resting=false;ColourRecovery=0;colourTime=0;
            milestones.Clear();journal.Clear();journalOpen=false;elapsed=0;transition=-1;memoryGesture=0;FinalEscapeVoiceCompleted=finalEscapeVoicePending=false;burstLessonStage=0;burstLessonFeedback=0;memoryCaptionProtected=connectionCaptionProtected=pendingStoryProtected=false;pendingStoryLine="";pendingStoryDuration=0;Rebuild(Movement.Fragments,0);fade=1;
            pendingJump=pendingInteract=pendingRetry=pendingBurst=false;accumulator=0;
            Say("My eyes are closed. The voices are not gone.\nSomewhere in the dark, there is a way to reach them.",7);
            milestones.Add("Entered Fragments");
        }
        void Rebuild(Movement chapter,int route)
        {
            bool sameRadio=Chapter==Movement.Fragments&&chapter==Movement.Fragments&&route==Excursion+1&&motor.Grounded&&motor.support==world.surfaces[0]&&Mathf.Abs(motor.position.x-world.radioX)<1.4f;
            Vector2 radioPosition=motor.position;
            if(chapter==Movement.Rupture)sound.ResetVoiceOutputHistory();
            Chapter=chapter;Excursion=route;chapterTime=0;previousSafe=null;ReturnDescent=false;locationSeen=false;PlaceCaptionViewport=Vector3.zero;ruptureBeat=0;
            if(chapter==Movement.StillWater){world.root.gameObject.SetActive(false);journey.Begin();Resting=false;sound.TraceSpeech("still-water-entry pending="+finalEscapeVoicePending);return;}
            journey.Hide();world.Build(chapter,route,Fragments);motor.swimming=false;motor.Reset(sameRadio?radioPosition:world.checkpoint);
            cameraPosition=world.checkpoint+Vector2.up*3;
            if(chapter==Movement.Hospital||chapter==Movement.Credits)cameraPosition=Vector2.zero;
            SetSideViewFrame(SideViewHeading(chapter,route),Vector2.zero);cam.backgroundColor=world.sky;
            Resting=false;
        }
        void Say(string line,float duration=5,bool remember=false,bool protectMemory=false,bool protectConnection=false)
        {
            if(remember){journal.Add(line);milestones.Add("Remembered "+journal.Count);}
            if((memoryCaptionProtected||connectionCaptionProtected)&&lineTime>0&&!protectMemory)
            {
                if(!pendingStoryProtected||protectConnection)
                {pendingStoryLine=line;pendingStoryDuration=duration;pendingStoryProtected=protectConnection;}
                return;
            }
            storyLine=line;lineTime=duration;memoryCaptionProtected=protectMemory;connectionCaptionProtected=protectConnection;pendingStoryLine="";pendingStoryProtected=false;
        }
        ReturnInput ReadInput()
        {
            if(shoreHandoffReview!=null)return shoreHandoffReview.Input();
            ReturnInput input=new ReturnInput();Keyboard k=Keyboard.current;Gamepad g=Gamepad.current;
            if(k!=null)
            {
                input.move=(k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0);
                input.vertical=(k.wKey.isPressed||k.upArrowKey.isPressed?1:0)-(k.sKey.isPressed||k.downArrowKey.isPressed?1:0);
                input.jumpPressed=k.spaceKey.wasPressedThisFrame;input.jumpHeld=k.spaceKey.isPressed;
                input.interact=k.eKey.wasPressedThisFrame||k.enterKey.wasPressedThisFrame;input.retry=k.rKey.wasPressedThisFrame;
                input.burstPressed=k.leftShiftKey.wasPressedThisFrame||k.rightShiftKey.wasPressedThisFrame;
                input.look.x=((k.cKey.isPressed?1:0)-(k.qKey.isPressed?1:0))*Time.unscaledDeltaTime*100;
            }
            if(g!=null)
            {
                if(Mathf.Abs(g.leftStick.x.ReadValue())>.15f)input.move=g.leftStick.x.ReadValue();
                if(Mathf.Abs(g.leftStick.y.ReadValue())>.15f)input.vertical=g.leftStick.y.ReadValue();
                input.jumpPressed|=g.buttonSouth.wasPressedThisFrame;input.jumpHeld|=g.buttonSouth.isPressed;input.interact|=g.buttonWest.wasPressedThisFrame;
                input.burstPressed|=g.rightShoulder.wasPressedThisFrame;
                input.look+=g.rightStick.ReadValue()*Time.unscaledDeltaTime*100;
            }
            if(Mouse.current!=null&&Mouse.current.rightButton.isPressed)input.look+=Mouse.current.delta.ReadValue()*.16f;
            return input;
        }
        void Update()
        {
            if(traceMemoryFrames)MemoryFrameStart();
            long updateStarted=measureUpdate?System.Diagnostics.Stopwatch.GetTimestamp():0;
            sound?.MarkSpeechFrame(Time.frameCount,paused||journalOpen);
            if(previewCapture!=null)while(previewCapture.TryGetError(out string captureError))Debug.LogError("[RETURN capture] FAIL "+captureError);
            if(driftWaterControlActive)return;
            if(eyeSurfaceReview!=null&&eyeSurfaceReview.Active)return;
            if(clothSurfaceReview!=null&&clothSurfaceReview.Active)return;
            if(dreamerSurfaceReview!=null&&dreamerSurfaceReview.Active)return;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.08f),cameraDt=dt;
            Keyboard k=Keyboard.current;Gamepad g=Gamepad.current;
            if((k!=null&&k.escapeKey.wasPressedThisFrame)||(g!=null&&g.startButton.wasPressedThisFrame))
            BackFromOverlay();
            if((k!=null&&k.tabKey.wasPressedThisFrame)||(g!=null&&g.selectButton.wasPressedThisFrame))ToggleJournal();
            if(journalOpen)
            {
                if((k!=null&&k.leftArrowKey.wasPressedThisFrame)||(g!=null&&g.dpad.left.wasPressedThisFrame))MoveJournalPage(-1);
                if((k!=null&&k.rightArrowKey.wasPressedThisFrame)||(g!=null&&g.dpad.right.wasPressedThisFrame))MoveJournalPage(1);
            }
            if(acceptance!=null)acceptance.ReviewJournalFrame();
            ReturnInput input=ReadInput();
            if(streetWalkProbe&&acceptance==null&&journey.Active&&journey.Scene==Movement.Reunion){
                float probeTime=journey.SceneElapsed-ReturnJourney3D.StreetReadyAt;int phase=probeTime<2?0:probeTime<4?1:2;
                if(phase!=streetWalkProbePhase){streetWalkProbePhase=phase;Debug.Log("[RETURN street walk probe] phase="+phase+" x="+journey.Position.x.ToString("F4",System.Globalization.CultureInfo.InvariantCulture));}
                if(phase<2)input=new ReturnInput{move=phase==0?1:-1};
            }
            if(reviewDreamerSurfaces&&!dreamerSurfaceStarted&&journey.Active&&Chapter==Movement.StillWater){Vector2 direction=journey.DirectionTo(new Vector3(-32,0,50));input=new ReturnInput{move=direction.x,vertical=direction.y};}
            if(title&&input.interact)StartOpening();
            if(prologue>=0)
            {prologue+=dt;int stage=sound.PrologueStage(prologue);if(stage>=4||input.interact&&prologue>.5f)Begin();else sound.PrologueVoice(stage);}
            if(acceptance!=null)
            {
                if(MotionReviewActive||EscapeReviewActive||FragmentsReviewActive) {
                    if(paused||journalOpen){cameraDt=0;if(MotionReviewActive)MotionReviewPausedSeconds+=Time.unscaledDeltaTime;else if(EscapeReviewActive)EscapeReviewPausedSeconds+=Time.unscaledDeltaTime;else FragmentsReviewPausedSeconds+=Time.unscaledDeltaTime;}
                    else {
                        // The paced motion review must retain elapsed time across a long render/capture frame.
                        float reviewDt=MotionReviewActive?Time.unscaledDeltaTime:dt;
                        accumulator+=reviewDt;cameraDt=reviewDt;
                        int journeyFrameTicks=0;
                        while(accumulator>=1f/60&&!acceptance.Finished){
                            Movement beforeChapter=Chapter;float beforeSceneElapsed=journey.SceneElapsed;bool beforeFruit=journey.Active&&journey.FruitActionTime>=0;
                            input=acceptance.Input();Tick(1f/60,input);acceptance.Observe(1f/60);accumulator-=1f/60;
                            // Start the recollection on a presented frame, rather than spending its reading time on older backlog.
                            if(MotionReviewActive&&beforeChapter==Movement.Hospital&&beforeSceneElapsed<ReturnJourney3D.HospitalRecollectionAt&&journey.SceneElapsed>=ReturnJourney3D.HospitalRecollectionAt)
                            {accumulator=0;break;}
                            // Present the start of a handoff before consuming the rest of a long frame.
                            if(Chapter!=beforeChapter||(!beforeFruit&&journey.Active&&journey.FruitActionTime>=0)||(MotionReviewActive&&beforeChapter==Movement.Reunion&&Chapter==Movement.Reunion&&beforeSceneElapsed<=2&&journey.SceneElapsed>2))break;
                            // Present required interaction/camera windows; retain the backlog for later ticks.
                            if(acceptance.JourneyFrameRequired&&++journeyFrameTicks>=2)break;
                        }
                    }
                } else {
                    cameraDt=0;
                    for(int i=0;i<24&&!acceptance.Finished;i++){input=acceptance.Input();Tick(1f/60,input);acceptance.Observe(1f/60);cameraDt+=1f/60;if(MotionReviewActive||EscapeReviewActive||FragmentsReviewActive){accumulator=0;break;}}
                }
            }
            else if(Playing&&!paused&&!journalOpen)
            {
                pendingJump|=input.jumpPressed;pendingInteract|=input.interact;pendingRetry|=input.retry;pendingBurst|=input.burstPressed;
                input.jumpPressed=pendingJump;input.interact=pendingInteract;input.retry=pendingRetry;input.burstPressed=pendingBurst;
                accumulator+=dt;
                while(accumulator>=1f/60){Tick(1f/60,input);accumulator-=1f/60;input.look=Vector2.zero;input.interact=input.jumpPressed=input.retry=input.burstPressed=false;pendingJump=pendingInteract=pendingRetry=pendingBurst=false;}
            }
            sound.Pause(paused||journalOpen);
            sound.TickAudioStartup();
            sound.TickSpeechTrace();
            sound.Hospital(dt,!paused&&!journalOpen&&(prologue>=0||Chapter==Movement.Hospital));
            if(title)world.clock+=dt;
            UpdateCamera(cameraDt);
            if(reviewReverseVisibility&&!captureBusy&&reverseVisibilitySamples.Count<3)
            {
                string label=null;
                if(reverseVisibilitySamples.Count==0&&journey.DriftViewObservationComplete)label="Quiet-Drift-Visibility";
                else if(reverseVisibilitySamples.Count==1&&journey.Scene==Movement.Reunion&&journey.SceneElapsed>=ReturnReverseJourney.FlattenAt-.5f)label="Reverse-Before-Reveal";
                else if(reverseVisibilitySamples.Count==2&&journey.Scene==Movement.Reunion&&journey.SceneElapsed>=ReturnReverseJourney.FlattenAt+.3f)label="Reverse-Reveal";
                if(label!=null){driftWaterControlActive=true;StartCoroutine(CaptureReverseVisibility(label));return;}
            }
            if(!reviewReverseVisibility&&(reviewDriftWaterControl||reviewDriftOutputControl||reviewDriftSkinControl||reviewDriftCullingControl||reviewDriftSkyControl||reviewDriftDrawControl)&&journey.DriftViewObservationComplete&&!captureBusy)
            {driftWaterControlActive=true;StartCoroutine(reviewDriftTransparentSetControl?CaptureDriftTransparentSetControl():reviewDriftDrawControl?CaptureDriftDrawControl():reviewDriftSkyControl?CaptureDriftSkyControl():reviewDriftCullingControl?CaptureDriftCullingControl():reviewDriftSkinControl?CaptureDriftSkinControl():reviewDriftOutputControl?CaptureDriftOutputControl():CaptureDriftWaterControl());return;}
            if(reviewDreamerSurfaces&&!dreamerSurfaceStarted&&Chapter==Movement.StillWater&&chapterTime>5&&journey.SwimPhase>.82f&&journey.SwimPhase<.9f&&!captureBusy)
            {
                dreamerSurfaceStarted=true;dreamerSurfaceReview=journey.CreateDreamerSurfaceReview(previewRoot,cam);
                StartCoroutine(dreamerSurfaceReview.Capture(CaptureFrame,()=>captureBusy));return;
            }
            if(reviewCloth&&!clothReviewStarted&&journey.Active&&Chapter==Movement.Hospital&&chapterTime>6&&!captureBusy)
            {
                clothReviewStarted=true;clothSurfaceReview=new ReturnClothSurfaceReview(chapterTime,previewRoot,cam);
                StartCoroutine(clothSurfaceReview.Capture(CaptureFrame,()=>captureBusy));return;
            }
            if(reviewEyeSurfaces&&acceptance!=null&&!eyeSurfacesStarted&&Chapter==Movement.Hospital&&chapterTime>10.2f&&!captureBusy)
            {
                eyeSurfacesStarted=true;eyeSurfaceReview=new ReturnEyeSurfaceReview(chapterTime,previewRoot);
                StartCoroutine(eyeSurfaceReview.Capture(CaptureFrame));return;
            }
            bool shoreMotionWindow=reviewShoreMotion&&journey.Active&&Chapter==Movement.StillWater&&journey.SceneElapsed>=38.5f&&journey.SceneElapsed<=43.2f;
            bool islandReturnMotionWindow=reviewIslandReturnMotion&&journey.Active&&Chapter==Movement.Reunion&&journey.SceneElapsed>=33.5f&&journey.SceneElapsed<=40.5f;
            bool motionWindow=shoreMotionWindow||islandReturnMotionWindow;
            if(!string.IsNullOrEmpty(previewRoot)&&Time.unscaledTime>nextCapture&&(motionWindow?(previewCapture==null||!previewCapture.Full):!captureBusy))
            {nextCapture=Time.unscaledTime+1f/(motionWindow?24:10);StartCoroutine(CaptureFrame());}
            if(measureUpdate){
                measuredUpdateMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-updateStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;
                measuredFrameMs+=Time.unscaledDeltaTime*1000.0;if(Application.isFocused)measuredUpdateFocusedFrames++;
                double wallFrameMs=previousMeasuredUpdateAt>0?(updateStarted-previousMeasuredUpdateAt)*1000.0/System.Diagnostics.Stopwatch.Frequency:Time.unscaledDeltaTime*1000.0;
                previousMeasuredUpdateAt=updateStarted;measuredWallFrameMs+=wallFrameMs;measuredMaxWallFrameMs=Math.Max(measuredMaxWallFrameMs,wallFrameMs);if(wallFrameMs>1000.0/30)measuredSlowWallFrames++;
                if(++measuredUpdateFrames==120){
                    Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,"[RETURN update timing] game-update samples={0} focusedSamples={1} meanMs={2:F3} meanFrameMs={3:F3} frame={4} meanWallFrameMs={5:F3} maxWallFrameMs={6:F3} over33Ms={7} chapter={8}",measuredUpdateFrames,measuredUpdateFocusedFrames,measuredUpdateMs/measuredUpdateFrames,measuredFrameMs/measuredUpdateFrames,Time.frameCount,measuredWallFrameMs/measuredUpdateFrames,measuredMaxWallFrameMs,measuredSlowWallFrames,Chapter));
                    if(previewCapture!=null)Debug.Log("[RETURN preview buffers] allocated="+previewCapture.PixelBufferAllocations+" reused="+previewCapture.PixelBufferReuses);
                    if(previewCapture!=null&&previewCapture.CaptureTimingEnabled)Debug.Log("[RETURN capture stages] "+previewCapture.DescribeStages());
                    measuredUpdateFrames=measuredUpdateFocusedFrames=0;measuredUpdateMs=measuredFrameMs=0;
                    measuredWallFrameMs=measuredMaxWallFrameMs=0;measuredSlowWallFrames=0;
                }
            }
            if(traceMemoryFrames)
            {
                memoryUpdateEndAt=System.Diagnostics.Stopwatch.GetTimestamp();
                memoryTimingRoute=traceRuptureFrames?0:Excursion;
                memoryTimingActive=traceRuptureFrames?Playing&&Chapter==Movement.Rupture&&!paused:FragmentsReviewActive&&Carrying&&Excursion>=2&&Excursion<4&&sound.IsSpeaking("memory-"+Excursion);
                System.Threading.Volatile.Write(ref memoryWatchFrame,memoryTimingFrame);
                System.Threading.Volatile.Write(ref memoryWatchRoute,memoryTimingRoute);
                System.Threading.Volatile.Write(ref memoryWatchActive,memoryTimingActive?1:0);
            }
        }
        void MemoryFrameStart()
        {
            System.Threading.Volatile.Write(ref memoryWatchActive,0);
            string watchFailure=System.Threading.Interlocked.Exchange(ref memoryStallFailure,null);
            if(watchFailure!=null)Debug.LogError("[RETURN memory-loop] Exception in stall watcher: "+watchFailure);
            long now=System.Diagnostics.Stopwatch.GetTimestamp();double frequency=System.Diagnostics.Stopwatch.Frequency;
            if(memoryTimingActive&&(now-memoryFrameAt)/frequency>(traceRuptureFrames ? .1 : .2)&&memorySlowFrames[memoryTimingRoute]++<12)
                Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "[RETURN memory-frame] route={0} frame={1} focused={2} wallSeconds={3:F6} previousDelta={4:F6} nextDelta={5:F6} simulatedSeconds={6:F6} updateSeconds={7:F6} beforeRenderSeconds={8:F6} renderSeconds={9:F6} afterRenderSeconds={10:F6} betweenFramesSeconds={11:F6} slowStages={12}",
                    memoryTimingRoute,memoryTimingFrame,memoryTimingFocused,(now-memoryFrameAt)/frequency,memoryFrameDelta,Time.unscaledDeltaTime,memorySimulationSeconds,
                    (memoryUpdateEndAt-memoryFrameAt)/frequency,memoryRenderAt>0?(memoryRenderAt-memoryUpdateEndAt)/frequency:0,memoryRenderSeconds,
                    memoryEndAt>0&&memoryRenderEndAt>0?(memoryEndAt-memoryRenderEndAt)/frequency:0,memoryEndAt>0?(now-memoryEndAt)/frequency:0,DescribeMemoryStages()));
            foreach(var stage in memoryLoopStages)stage.seconds=0;
            memoryFrameAt=now;memoryUpdateEndAt=now;memoryRenderAt=memoryRenderEndAt=memoryEndAt=0;
            memoryRenderSeconds=memorySimulationSeconds=0;memoryTimingFrame=Time.frameCount;memoryFrameDelta=Time.unscaledDeltaTime;memoryTimingFocused=Application.isFocused;memoryTimingActive=false;
        }
        void MemoryRenderBegin(UnityEngine.Rendering.ScriptableRenderContext context,Camera[] cameras)
        {if(memoryRenderAt==0)memoryRenderAt=System.Diagnostics.Stopwatch.GetTimestamp();}
        void MemoryRenderEnd(UnityEngine.Rendering.ScriptableRenderContext context,Camera[] cameras)
        {memoryRenderEndAt=System.Diagnostics.Stopwatch.GetTimestamp();memoryRenderSeconds=(memoryRenderEndAt-memoryRenderAt)/(double)System.Diagnostics.Stopwatch.Frequency;}
        IEnumerator MemoryFrameEnd()
        {while(traceMemoryFrames){yield return new WaitForEndOfFrame();memoryEndAt=System.Diagnostics.Stopwatch.GetTimestamp();}}
        void InstallMemoryLoopTiming()
        {
            memoryOriginalLoop=UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            var candidate=AddMemoryLoopTiming(memoryOriginalLoop);
            if(memoryLoopStages.Count==0||!SameMemoryLoop(memoryOriginalLoop,RemoveMemoryLoopTiming(candidate)))
                throw new InvalidOperationException("Memory timing markers did not preserve the native player loop");
            UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(candidate);memoryLoopInstalled=true;
            if(!SameMemoryLoop(memoryOriginalLoop,RemoveMemoryLoopTiming(UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop())))
                throw new InvalidOperationException("Installed memory timing markers altered a native player-loop entry");
            memoryStallEvidence=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");
            if(!string.IsNullOrEmpty(memoryStallEvidence))memoryStallTimer=new System.Threading.Timer(ObserveMemoryStall,null,25,25);
            Debug.Log("[RETURN memory-loop] installedStages="+memoryLoopStages.Count+" originalPreserved=true");
        }
        UnityEngine.LowLevel.PlayerLoopSystem AddMemoryLoopTiming(UnityEngine.LowLevel.PlayerLoopSystem system)
        {
            if(system.subSystemList==null)return system;
            bool selected=system.type==typeof(UnityEngine.PlayerLoop.Update)||system.type==typeof(UnityEngine.PlayerLoop.PreLateUpdate)||system.type==typeof(UnityEngine.PlayerLoop.PostLateUpdate);
            var children=new List<UnityEngine.LowLevel.PlayerLoopSystem>();
            foreach(var child in system.subSystemList)
            {
                if(!selected){children.Add(AddMemoryLoopTiming(child));continue;}
                var stage=new MemoryLoopStage{name=system.type.Name+"."+(child.type!=null?child.type.Name:"unnamed")};memoryLoopStages.Add(stage);
                children.Add(new UnityEngine.LowLevel.PlayerLoopSystem{type=typeof(MemoryLoopMarker),updateDelegate=()=>{System.Threading.Interlocked.Exchange(ref stage.started,System.Diagnostics.Stopwatch.GetTimestamp());System.Threading.Volatile.Write(ref memoryCurrentStage,stage);}});
                children.Add(child);
                children.Add(new UnityEngine.LowLevel.PlayerLoopSystem{type=typeof(MemoryLoopMarker),updateDelegate=()=>{stage.seconds+=(System.Diagnostics.Stopwatch.GetTimestamp()-stage.started)/(double)System.Diagnostics.Stopwatch.Frequency;System.Threading.Volatile.Write(ref memoryCurrentStage,null);}});
            }
            system.subSystemList=children.ToArray();return system;
        }
        static UnityEngine.LowLevel.PlayerLoopSystem RemoveMemoryLoopTiming(UnityEngine.LowLevel.PlayerLoopSystem system)
        {
            if(system.subSystemList==null)return system;
            var children=new List<UnityEngine.LowLevel.PlayerLoopSystem>();
            foreach(var child in system.subSystemList)if(child.type!=typeof(MemoryLoopMarker))children.Add(RemoveMemoryLoopTiming(child));
            system.subSystemList=children.ToArray();return system;
        }
        static bool SameMemoryLoop(UnityEngine.LowLevel.PlayerLoopSystem left,UnityEngine.LowLevel.PlayerLoopSystem right)
        {
            if(left.type!=right.type||left.updateFunction!=right.updateFunction||left.updateDelegate!=right.updateDelegate||left.loopConditionFunction!=right.loopConditionFunction)return false;
            int count=left.subSystemList!=null?left.subSystemList.Length:0;
            if(count!=(right.subSystemList!=null?right.subSystemList.Length:0))return false;
            for(int i=0;i<count;i++)if(!SameMemoryLoop(left.subSystemList[i],right.subSystemList[i]))return false;
            return true;
        }
        string DescribeMemoryStages()
        {
            var slow=new List<string>();foreach(var stage in memoryLoopStages)if(stage.seconds>.05)slow.Add(stage.name+"="+stage.seconds.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));
            return string.Join(",",slow);
        }
        void ObserveMemoryStall(object state)
        {
            if(System.Threading.Volatile.Read(ref memoryWatchActive)==0||System.Threading.Volatile.Read(ref memoryStallRequested)!=0)return;
            var stage=System.Threading.Volatile.Read(ref memoryCurrentStage);long now=System.Diagnostics.Stopwatch.GetTimestamp();
            double frameSeconds=(now-System.Threading.Interlocked.Read(ref memoryFrameAt))/(double)System.Diagnostics.Stopwatch.Frequency;
            if(frameSeconds<=.2)return;
            double stageSeconds=stage!=null?(now-System.Threading.Interlocked.Read(ref stage.started))/(double)System.Diagnostics.Stopwatch.Frequency:0;
            if(System.Threading.Volatile.Read(ref memoryWatchActive)==0||!ReferenceEquals(stage,System.Threading.Volatile.Read(ref memoryCurrentStage))||System.Threading.Interlocked.CompareExchange(ref memoryStallRequested,1,0)!=0)return;
            try
            {
                string trigger=string.Format(System.Globalization.CultureInfo.InvariantCulture,"frame={0}\nroute={1}\nstage={2}\nframeSeconds={3:F6}\nstageSeconds={4:F6}\nutc={5:O}\n",System.Threading.Volatile.Read(ref memoryWatchFrame),System.Threading.Volatile.Read(ref memoryWatchRoute),stage!=null?stage.name:"between-loop-steps",frameSeconds,stageSeconds,DateTime.UtcNow);
                File.WriteAllText(Path.Combine(memoryStallEvidence,"memory-stall-trigger.txt"),trigger);
            }
            catch(Exception error){System.Threading.Volatile.Write(ref memoryStallFailure,error.GetType().Name+": "+error.Message);}
        }
        void RemoveMemoryLoopTiming()
        {
            System.Threading.Volatile.Write(ref memoryWatchActive,0);memoryStallTimer?.Dispose();memoryStallTimer=null;
            if(!memoryLoopInstalled)return;
            var restored=RemoveMemoryLoopTiming(UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop());UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(restored);memoryLoopInstalled=false;
            Debug.Log("[RETURN memory-loop] restored="+SameMemoryLoop(memoryOriginalLoop,UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop()));
        }
        public void Tick(float dt,ReturnInput input)
        {
            if(traceMemoryFrames)memorySimulationSeconds+=dt;
            if(!Playing)return;
            if(finalEscapeVoicePending&&sound.HasCompletedSpeech(RuptureVoiceKeys[4]))
            {FinalEscapeVoiceCompleted=true;finalEscapeVoicePending=false;sound.TraceSpeech("final-escape-completed");}
            bool rememberRequested=input.interact&&CanRememberFragment;
            elapsed+=dt;chapterTime+=dt;lineTime=Mathf.Max(0,lineTime-dt);fade=Mathf.Max(0,fade-dt*.8f);retryFlash=Mathf.Max(0,retryFlash-dt*2.8f);world.reform=retryFlash;burstLessonFeedback=Mathf.Max(0,burstLessonFeedback-dt);
            if((memoryCaptionProtected||connectionCaptionProtected)&&lineTime<=0)
            {
                memoryCaptionProtected=connectionCaptionProtected=false;
                if(!string.IsNullOrEmpty(pendingStoryLine))
                {storyLine=pendingStoryLine;lineTime=pendingStoryDuration;connectionCaptionProtected=pendingStoryProtected;pendingStoryLine="";pendingStoryProtected=false;}
            }
            memoryGesture=Mathf.Max(0,memoryGesture-dt);
            world.gesture=Mathf.Sin(Mathf.Clamp01(memoryGesture/2.5f)*Mathf.PI);
            if(radioPlacementPending)
            {
                world.clock+=dt;world.UpdateSignal(dt);sound.SetSpeechPosition(motor.position,0,true);
                if(world.RadioPlacementReady)CommitRadioLight();
                return;
            }
            if(Chapter==Movement.Fragments&&world.PickupAnimationActive)input=new ReturnInput();
            else if(Chapter==Movement.Fragments&&Carrying&&pendingMemoryVoice>=0)
            {sound.Speak("memory-"+pendingMemoryVoice);pendingMemoryVoice=-1;}
            if(transition>=0)
            {
                float beforeTransition=transition;
                transition+=dt;
                bool tuning=nextChapter==Movement.Fragments;
                float duration=firstStageWhiteout?6:tuning?3:1.8f;
                if(firstStageWhiteout)FirstStageWhiteHoldSeconds+=Mathf.Max(0,Mathf.Min(transition,3.1f)-Mathf.Max(beforeTransition,1.1f));
                world.clock+=dt;
                world.UpdateSignal(dt);
                if(!transitionBuilt&&Chapter==Movement.Fragments&&nextChapter==Movement.Rupture)
                    world.PoseRadioAscent(transition/(duration*.4f),ReducedMotion);
                if(transition>=(firstStageWhiteout?3.1f:duration*.5f)&&!transitionBuilt)
                {Rebuild(nextChapter,nextExcursion);transitionBuilt=true;milestones.Add("Entered "+nextChapter+" / "+nextExcursion);Say(tuning?ConnectionLines[Mathf.Clamp(Excursion-1,0,ConnectionLines.Length-1)]:nextChapter==Movement.Rupture?"The voices are clearer. So is the pressure. I do not remember why.":nextChapter==Movement.StillWater?"The noise falls away. I can breathe.":"A room beyond the door.",6,false,false,tuning);}
                if(firstStageWhiteout&&transitionBuilt)world.SetRuptureColour(Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.1f,6,transition)));
                if(transition>=duration){transition=-1;transitionBuilt=false;}
                return;
            }
            if(journey.Active)
            {
                if(Chapter==Movement.Credits){if(input.interact)Begin();return;}
                if(input.retry){Retry();return;}
                journey.NurseSpeech=sound.VoiceLevel("hospital-nurse");journey.MotherSpeech=sound.VoiceLevel("hospital-mum");
                journey.Step(dt,input);Chapter=journey.Scene;chapterTime=journey.SceneElapsed;Resting=journey.Resting;ColourRecovery=journey.Colours;
                shoreHandoffReview?.Observe(dt);
                foreach(string e in journey.events)
                {
                    milestones.Add(e);
                    if(e=="Let the green leaf carry me")Say("For a moment, nothing is asking me to hurry. I let the leaf carry me.",7,true);
                    else if(e=="Ate the blue fruit"){sound.Cue("fruit");Say("The taste is familiar. For the first time, I want to know what comes next.",7,true);}
                    else if(e=="Remembered the hospital cause")Say("The voices belonged to the people beside my bed. After my suicide attempt, I had been in a coma. They kept speaking to me.",ReturnJourney3D.HospitalEyesCloseAt,true);
                    else if(e=="Remembered the voice through the rupture")Say("“Stay with me.” The voice was there even when everything broke.",6,true);
                    else if(e=="Remembered the places behind the fragments")Say("A lamp left on. A message I never sent. I remember more than the pressure now.",6,true);
                    else if(e=="The four routes became city streets")Say("I know these streets. They lead back to the people who stayed.",7,true);
                                        else if(e=="Opened the doorway home")sound.Cue("radio");
                    else if(e=="3D jump")sound.Cue("jump");
                    else if(e=="Credits reached")sound.StopSpeech();
                }
                if(Chapter==Movement.Hospital&&chapterTime>=4&&chapterTime<7)sound.Speak("hospital-nurse");
                else if(Chapter==Movement.Hospital&&chapterTime>=7&&chapterTime<11)sound.Speak("hospital-mum");
                return;
            }
            if(Chapter==Movement.Hospital)
            {
                world.HospitalPose(chapterTime);
                if(chapterTime>=4&&chapterTime<7)sound.Speak("hospital-nurse");
                else if(chapterTime>=7&&chapterTime<11)sound.Speak("hospital-mum");
                if(chapterTime>14){sound.StopSpeech();Chapter=Movement.Credits;chapterTime=0;milestones.Add("Credits reached");}return;
            }
            if(Chapter==Movement.Credits){if(input.interact)Begin();return;}
            if(input.retry){Retry();return;}
            if(Chapter==Movement.StillWater)
            {
                if(ColourRecovery>0){input.move=0;input.jumpPressed=false;input.jumpHeld=false;}
                if(input.interact&&world.leaf!=null&&Mathf.Abs(motor.position.x-world.leaf.x)<3&&motor.position.y>-2&&motor.position.y<1)
                {
                    Resting=!Resting;
                    if(Resting){motor.Reset(new Vector2(world.leaf.x,world.leaf.y));motor.support=world.leaf;world.leafBoarded=true;milestones.Add("Resting on the green leaf");}
                }
                if(Resting&&(Mathf.Abs(input.move)>.15f||input.jumpPressed))Resting=false;
                bool wasSwimming=motor.swimming;
                float waterLine=motor.position.x<64?-.25f:motor.position.x<69?-2.1f:motor.position.x<72?-1.1f:-.3f;
                motor.swimming=motor.position.y<waterLine&&!Resting&&!motor.Grounded;
                // A surface kick carries the body above the shore instead of oscillating below it.
                if(wasSwimming&&!motor.swimming&&input.jumpHeld)motor.velocity.y=Mathf.Max(8,motor.velocity.y);
                if(Resting){input.move=0;input.jumpPressed=false;input.jumpHeld=false;}
            }
            else motor.swimming=false;
            if(Chapter==Movement.Fragments&&Carrying&&input.move>0&&(input.jumpPressed&&motor.Grounded||world.IsForwardBreakaway(motor.support)))world.CollapseForwardReturn();
            bool contact=world.Step(dt,motor);
            bool grounded=motor.Grounded;
            if(input.jumpPressed&&grounded)sound.Cue("jump");
            bool teachingBurst=Chapter==Movement.Fragments&&Excursion==1&&BurstUnlocked&&input.burstPressed&&motor.BurstReady&&!grounded;
            motor.Step(dt,input.move,input.vertical,input.jumpPressed,input.jumpHeld,world.surfaces,input.burstPressed&&BurstUnlocked&&Chapter!=Movement.StillWater&&Chapter!=Movement.Reunion);
            if(motor.BurstStarted)sound.Cue("burst");
            if(Chapter==Movement.Fragments&&Excursion==1)
            {
                if(teachingBurst&&burstLessonStage<3)burstLessonStage=2;
                else if(burstLessonStage==0&&!motor.Grounded&&motor.velocity.y>0)burstLessonStage=1;
                if(burstLessonStage==2&&motor.Grounded&&motor.BurstReady&&!contact)
                {burstLessonStage=3;burstLessonFeedback=4;milestones.Add("Learned air burst and ground renewal");}
                if(world.burstPracticeGuide!=null)world.burstPracticeGuide.gameObject.SetActive(burstLessonStage<3);
            }
            if(!grounded&&motor.Grounded&&motor.LandingImpact>.2f)sound.Cue("land");
            motor.position.x=Mathf.Clamp(motor.position.x,-3,world.boundRight);
            if(Chapter==Movement.StillWater)motor.position.y=Mathf.Max(-7,motor.position.y);
            if(contact||motor.position.y<-12){Retry();return;}
            if(motor.support!=null&&motor.support.kind==SurfaceKind.Stone&&motor.support.width>=4&&motor.support!=previousSafe&&
                (Chapter!=Movement.Rupture||motor.support==world.escapeCheckpoint&&motor.position.x>=(motor.support.Left+motor.support.Right)*.5f))
            {
                previousSafe=motor.support;
                float safeX=Chapter==Movement.Rupture?(motor.support.Left+motor.support.Right)*.5f:Mathf.Clamp(motor.position.x,motor.support.Left+.6f,motor.support.Right-.6f);
                world.checkpoint=new Vector2(safeX,motor.support.y);
            }
            if(Chapter==Movement.Fragments)
            {
                sound.SetSpeechPosition(motor.position,input.move,Carrying);
                ShowPlaceWhenVisible();
                if(!Carrying&&motor.Grounded&&input.interact&&world.CanCallStationTrain(motor.position)&&world.CallStationTrain())
                {sound.Cue("radio");Say("A small signal. Something on the other side answers.",4,true);milestones.Add("Called the station carriage");}
                if(rememberRequested&&CanRememberFragment)
                {Carrying=true;sound.SetSpeechPosition(motor.position,input.move,true);world.OpenReturn();world.BeginPocketPickup();memoryGesture=2.5f;Say(Memories[Excursion],8,true,true);sound.Cue("block");pendingMemoryVoice=Excursion;milestones.Add("Collected fragment "+(Fragments+1));}
                if(Carrying&&!ReturnDescent&&world.IsLowerReturnSupport(motor.support))
                {ReturnDescent=true;world.EnterLowerReturn();milestones.Add("Entered separate return circuit "+Excursion);}
                if(input.interact&&CanDepositFragment)
                {
                    radioPlacementPending=true;world.BeginRadioPlacement(Fragments);
                }
            }
            else if(Chapter==Movement.Rupture)
            {
                if(ruptureBeat<RuptureBeatPositions.Length&&motor.position.x>RuptureBeatPositions[ruptureBeat])
                {
                    Say(RuptureLines[ruptureBeat],6);sound.Speak(RuptureVoiceKeys[ruptureBeat]);
                    milestones.Add("Rupture voice "+(++ruptureBeat));
                }
                // Crossing the opening while airborne is essential: walking cannot complete the phase.
                if(motor.position.x>world.goalX-ReturnWorld.WhiteOpeningHalfWidth&&motor.position.x<world.goalX+ReturnWorld.WhiteOpeningHalfWidth&&motor.position.y>world.goalY+.35f&&!motor.Grounded)
                {FinalEscapeVoiceCompleted=sound.HasCompletedSpeech(RuptureVoiceKeys[4]);finalEscapeVoicePending=!FinalEscapeVoiceCompleted;sound.TraceSpeech("white-opening pending="+finalEscapeVoicePending);milestones.Add("Jumped into white");Transition(Movement.StillWater,0);}
            }
            else if(Chapter==Movement.StillWater)
            {
                if(ColourRecovery>0)
                {
                    colourTime+=dt;ColourRecovery=Mathf.Clamp01(colourTime/9f);
                    if(colourTime>10)
                    {
                        milestones.Add("Yellow, red and blue reunited");Vector2 position=motor.position,view=cameraPosition;
                        Rebuild(Movement.Reunion,0);motor.Reset(position);cameraPosition=view;milestones.Add("Entered Reunion / 0");
                    }
                }
                else if(input.interact&&motor.position.x>72&&motor.Grounded)
                {ColourRecovery=.001f;colourTime=0;sound.Cue("fruit");Say("I cannot undo that night. I can return to the people who stayed.",8,true);milestones.Add("Ate the blue fruit");}
            }
            else if(Chapter==Movement.Reunion&&motor.position.x<8&&input.interact)
            {milestones.Add("Opened the doorway home");sound.Cue("radio");Transition(Movement.Hospital,0);}
            if(Chapter==Movement.StillWater&&ColourRecovery>0)world.gesture=Mathf.Clamp01(1-colourTime/1.3f);
            world.Pose(motor,cameraPosition,Carrying,ColourRecovery,Resting,memoryVoice:MemoryVoiceLevel(Excursion));
        }
        void ShowPlaceWhenVisible()
        {
            if((memoryCaptionProtected||connectionCaptionProtected)&&lineTime>0)return;
            if(locationSeen||Carrying||!motor.Grounded||!world.TryMemoryStoryFocus(out Vector3 focus))return;
            Vector3 viewport=cam.WorldToViewportPoint(focus);
            if(viewport.z<=0||viewport.x<.12f||viewport.x>.8f||viewport.y<.18f||viewport.y>.85f)return;
            locationSeen=true;PlaceCaptionViewport=viewport;Say(Places[Excursion],6,true);
        }
        void CommitRadioLight()
        {
            world.CompleteRadioPlacement();radioPlacementPending=false;Fragments++;Carrying=false;sound.SetSpeechPosition(motor.position,0,false);
            world.SetRadioProgress(Fragments);milestones.Add("Carried fragment "+Fragments+" home");
            world.gesture=1;sound.Cue("radio");milestones.Add("Reconnected fragment "+Fragments);
            if(Fragments==1)Say("The voice is clearer. I still cannot answer, but I can keep moving.",7,true);
            if(Fragments==4)Transition(Movement.Rupture,0);else Transition(Movement.Fragments,Excursion+1);
        }
        void Transition(Movement chapter,int route)
        {if(transition>=0)return;firstStageWhiteout=Chapter==Movement.Fragments&&chapter==Movement.Rupture;transitionFromExcursion=Excursion;nextChapter=chapter;nextExcursion=route;transition=0;transitionBuilt=false;}
        public void Retry()
        {
            if(burstLessonStage<3){burstLessonStage=0;burstLessonFeedback=0;}
            if(journey.Active){journey.RetrySafe();Failures++;retryFlash=1;sound.Cue("reform");milestones.Add("Reformed in three-dimensional Still Water");return;}
            if(Failures<5)Debug.Log("[RETURN] Retry at "+motor.position+" velocity "+motor.velocity+" in "+Chapter+" route "+Excursion);
            Failures++;world.ResetHazards();motor.Reset(world.checkpoint);Resting=false;retryFlash=1;sound.Cue("reform");
            if(Carrying){ReturnDescent=true;world.EnterLowerReturn();}
            milestones.Add("Reformed at safe ground in "+Chapter);
        }
        static float SideViewHeading(Movement chapter,int route)
        {return chapter==Movement.Fragments?(Mathf.Clamp(route,0,3)-1)*90f:chapter==Movement.Rupture?180f:0;}
        void SetSideViewFrame(float heading,Vector2 offset)
        {
            Quaternion section=Quaternion.Euler(0,SideViewHeading(Chapter,Excursion),0),view=Quaternion.Euler(0,heading,0);
            Vector3 pivot=world.beacon!=null?world.beacon.localPosition:new Vector3(6,0,0);
            if(world.root!=null)world.root.SetPositionAndRotation(pivot-section*pivot,section);
            cam.orthographic=true;BranchViewHeading=heading;
            cam.transform.SetPositionAndRotation(pivot+view*(new Vector3(cameraPosition.x+offset.x,cameraPosition.y+offset.y,-30)-pivot),view);
        }
        void UpdateCamera(float dt)
        {
            if(cam==null)return;
            if(journey.Active){journey.UpdateCamera(dt,ReducedMotion);if(acceptance!=null&&!acceptance.Finished)acceptance.ObserveCamera();return;}
            float heading=SideViewHeading(Chapter,Excursion);
            if(Chapter==Movement.Hospital||Chapter==Movement.Credits)
            {cameraPosition=Vector2.Lerp(cameraPosition,Vector2.zero,1-Mathf.Exp(-dt*2));cam.orthographicSize=Mathf.Lerp(cam.orthographicSize,8.5f,dt);}
            else if(transition>=0&&nextChapter==Movement.Fragments)
            {
                float sweep=Mathf.Sin(Mathf.Clamp01(transition/3)*Mathf.PI);
                cameraPosition=Vector2.Lerp(cameraPosition,new Vector2(6,2.4f),1-Mathf.Exp(-dt*5));
                cam.orthographicSize=Mathf.Lerp(7.2f,ReducedMotion?9:24,sweep);
                if(!ReducedMotion)heading=Mathf.Lerp(SideViewHeading(Movement.Fragments,transitionFromExcursion),SideViewHeading(Movement.Fragments,nextExcursion),Mathf.SmoothStep(0,1,Mathf.Clamp01(transition/1.5f)));
            }
            else if(Chapter==Movement.StillWater&&ColourRecovery>0||Chapter==Movement.Reunion&&chapterTime<3)
            {
                float reveal=Chapter==Movement.Reunion?1:Mathf.SmoothStep(0,1,ColourRecovery);
                Vector2 target=Vector2.Lerp(motor.position+Vector2.up*3,new Vector2(41,8),reveal);
                cameraPosition=Vector2.Lerp(cameraPosition,target,1-Mathf.Exp(-dt*3));
                cam.orthographicSize=Mathf.Lerp(7.2f,27,reveal);
            }
            else
            {
                bool lowerCircuit=LowerReturnView;
                float lead=Resting?4:motor.facing*(motor.Grounded?2.4f:3);
                float opening=Chapter==Movement.Rupture?Mathf.SmoothStep(0,1,Mathf.InverseLerp(world.goalX-40,world.goalX-15,motor.position.x)):0;
                bool pursuit=Chapter==Movement.Rupture&&world.frontX>-90;
                if(pursuit)lead=Mathf.Lerp(-6,3,opening);
                cameraLead=Mathf.Lerp(cameraLead,lead,1-Mathf.Exp(-dt*6));
                float height=lowerCircuit?(motor.Grounded?motor.position.y+.9f:Mathf.Clamp(cameraPosition.y,motor.position.y-.3f,motor.position.y+1.6f)):(motor.Grounded?motor.position.y+2.7f:Mathf.Clamp(cameraPosition.y,motor.position.y+1.1f,motor.position.y+4.4f));
                Vector2 target=new Vector2(Mathf.Clamp(motor.position.x+cameraLead,7,world.boundRight-8),lowerCircuit?height:Mathf.Max(height,-.3f));
                if(Chapter==Movement.Reunion)target.y=4;
                cameraPosition=Vector2.Lerp(cameraPosition,target,1-Mathf.Exp(-dt*(lowerCircuit?6.5f:3.7f)));
                float framing=pursuit?Mathf.Lerp(11.5f,7.8f,opening):Chapter==Movement.Reunion?9.5f:7.2f;
                cam.orthographicSize=Mathf.Lerp(cam.orthographicSize,framing,pursuit?1-Mathf.Exp(-dt*.9f):dt*.9f);
            }
            Vector2 shake=Vector2.zero;
            if(Chapter==Movement.Rupture)
            {
                float release=1-Mathf.InverseLerp(world.goalX-55,world.goalX,motor.position.x);
                if(world.frontX>-90&&!ReducedMotion&&!paused)shake=new Vector2(Mathf.Sin(world.clock*37),Mathf.Sin(world.clock*29))*.055f*release;
                cam.backgroundColor=Color.Lerp(world.sky,new Color(.84f,.77f,.68f),1-release);
            }
            else if(Chapter==Movement.StillWater||Chapter==Movement.Reunion)
                cam.backgroundColor=Color.Lerp(world.sky,new Color(.66f,.80f,.76f),ColourRecovery);
            else cam.backgroundColor=world.sky;
            RadioCameraShakeMagnitude=0;
            if(Chapter==Movement.Fragments&&!ReducedMotion&&!paused)
            {
                Vector2 radioShake=new Vector2(Mathf.Sin(world.RadioRevealAge*97),Mathf.Cos(world.RadioRevealAge*73))*world.RadioShakeStrength;
                shake+=radioShake;RadioCameraShakeMagnitude=radioShake.magnitude;
            }
            if(Chapter==Movement.Rupture&&firstStageWhiteout&&transition>=0)
            {float grey=cam.backgroundColor.grayscale;cam.backgroundColor=Color.Lerp(new Color(grey,grey,grey),cam.backgroundColor,world.RuptureColour);}
            SetSideViewFrame(heading,shake);
            world.Pose(motor,cameraPosition,Carrying,ColourRecovery,Resting,cam.orthographicSize,InTransition||radioPlacementPending||world.PickupAnimationActive,MemoryVoiceLevel(Excursion));
            if(acceptance!=null&&!acceptance.Finished)acceptance.ObserveCamera();
        }
        [Serializable] sealed class PreviewCapture {public string message,frame,revision,story;public float elapsed,openingElapsed,frameSeconds,realtime,swimPhase,swimFootSpeed;public bool paused,playing,opening,focused,swimming;public int openingStage,processId,renderedFrameCount,vSyncCount,targetFrameRate,qualityLevel;public bool backgroundRendering,asynchronousCapture;}
        public void RequestFrame(string name){TryRequestFrame(name);}
        [Serializable] sealed class DriftWaterControlReceipt
        {
            public bool passed,waterBaseline,waterControl,waterRestored,reflectionBaseline,reflectionControl,reflectionRestored,leafOpaque,leafMaterialRestored,depthTestBypassed,depthTestRestored,poseSame;
            public string leafControlShader;
            public float depthTestBaseline,depthTestControl;public int materialQueueBefore,materialQueueAfter;
            public float elapsedBefore,elapsedAfter,restBefore,restAfter,fovBefore,fovAfter,scaledTimeBefore,scaledTimeAfter,timeScaleDuring;
            public Vector3 playerBefore,playerAfter,cameraBefore,cameraAfter;
            public Quaternion rotationBefore,rotationAfter;
        }
        IEnumerator CaptureDriftWaterControl()
        {
            var receipt=new DriftWaterControlReceipt{waterBaseline=journey.DriftWaterVisible,elapsedBefore=journey.SceneElapsed,restBefore=journey.RestPoseAmount,fovBefore=journey.CameraFieldOfView,playerBefore=journey.Position,cameraBefore=journey.CameraPosition,rotationBefore=journey.CameraRotation};
            float previousScale=Time.timeScale;bool previousWater=journey.DriftWaterVisible,previousReflection=journey.DriftReflectionEnabled;receipt.reflectionBaseline=previousReflection;
            Material previousLeaf=journey.DriftLeafMaterial,opaqueLeaf=null;
            try
            {
                Time.timeScale=0;receipt.scaledTimeBefore=Time.time;receipt.timeScaleDuring=Time.timeScale;
                yield return CaptureFrame("Leaf-Water-Control-Baseline");while(captureBusy)yield return null;
                journey.DriftWaterVisible=false;receipt.waterControl=journey.DriftWaterVisible;
                yield return CaptureFrame("Leaf-Water-Control-Hidden");while(captureBusy)yield return null;
                journey.DriftReflectionEnabled=false;receipt.reflectionControl=journey.DriftReflectionEnabled;
                yield return CaptureFrame("Leaf-Reflection-Control-Disabled");while(captureBusy)yield return null;
                Shader controlShader=Resources.Load<Shader>("ReturnDriftVisibilityControl");
                if(controlShader==null||!controlShader.isSupported)throw new InvalidOperationException("Drift visibility control shader unavailable");
                opaqueLeaf=new Material(controlShader){renderQueue=5000};journey.DriftLeafMaterial=opaqueLeaf;receipt.leafOpaque=journey.DriftLeafMaterial==opaqueLeaf;receipt.leafControlShader=controlShader.name;receipt.depthTestBaseline=opaqueLeaf.GetFloat("_DepthTest");receipt.materialQueueBefore=opaqueLeaf.renderQueue;
                yield return CaptureFrame("Leaf-Material-Control-Opaque");while(captureBusy)yield return null;
                opaqueLeaf.SetFloat("_DepthTest",(float)UnityEngine.Rendering.CompareFunction.Always);receipt.depthTestControl=opaqueLeaf.GetFloat("_DepthTest");receipt.materialQueueAfter=opaqueLeaf.renderQueue;receipt.depthTestBypassed=receipt.depthTestBaseline==(float)UnityEngine.Rendering.CompareFunction.LessEqual&&receipt.depthTestControl==(float)UnityEngine.Rendering.CompareFunction.Always&&receipt.materialQueueBefore==receipt.materialQueueAfter;
                yield return CaptureFrame("Leaf-Depth-Control-Always");while(captureBusy)yield return null;
                receipt.elapsedAfter=journey.SceneElapsed;receipt.restAfter=journey.RestPoseAmount;receipt.fovAfter=journey.CameraFieldOfView;receipt.playerAfter=journey.Position;receipt.cameraAfter=journey.CameraPosition;receipt.rotationAfter=journey.CameraRotation;receipt.scaledTimeAfter=Time.time;
            }
            finally{journey.DriftLeafMaterial=previousLeaf;if(opaqueLeaf!=null){opaqueLeaf.SetFloat("_DepthTest",(float)UnityEngine.Rendering.CompareFunction.LessEqual);receipt.depthTestRestored=opaqueLeaf.GetFloat("_DepthTest")==receipt.depthTestBaseline;Destroy(opaqueLeaf);}journey.DriftWaterVisible=previousWater;journey.DriftReflectionEnabled=previousReflection;Time.timeScale=previousScale;}
            receipt.waterRestored=journey.DriftWaterVisible;
            receipt.reflectionRestored=journey.DriftReflectionEnabled;
            receipt.leafMaterialRestored=journey.DriftLeafMaterial==previousLeaf;
            receipt.poseSame=receipt.playerBefore==receipt.playerAfter&&receipt.cameraBefore==receipt.cameraAfter&&receipt.rotationBefore==receipt.rotationAfter&&receipt.elapsedBefore==receipt.elapsedAfter&&receipt.restBefore==receipt.restAfter&&receipt.fovBefore==receipt.fovAfter&&receipt.scaledTimeBefore==receipt.scaledTimeAfter;
            receipt.passed=receipt.waterBaseline&&!receipt.waterControl&&receipt.waterRestored&&receipt.reflectionBaseline&&!receipt.reflectionControl&&receipt.reflectionRestored&&receipt.leafOpaque&&receipt.leafMaterialRestored&&receipt.depthTestBypassed&&receipt.depthTestRestored&&receipt.poseSame&&receipt.timeScaleDuring==0;
            string evidence=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(evidence))System.IO.File.WriteAllText(System.IO.Path.Combine(evidence,"drift-water-control.json"),JsonUtility.ToJson(receipt,true));
            Debug.Log("[RETURN drift-water-control] "+JsonUtility.ToJson(receipt));
        }
        [Serializable] sealed class DriftOutputControlReceipt
        {
            public bool passed,poseSame,targetPreserved,targetRestored,projectionSame,viewSame;
            public bool skinBypassBaseline,skinBypassControl,skinBypassRestored,skinTrackingRestored;public int skinFramesBefore,skinFramesBaseline,skinFramesControl,transparentQueueLower,transparentQueueUpper;
            public string cameraName,originalTarget;public int cameraInstanceId,width,height;
            public float elapsedBefore,elapsedAfter,restBefore,restAfter,fovBefore,fovAfter,scaledTimeBefore,scaledTimeAfter;
            public Vector3 playerBefore,playerAfter,cameraBefore,cameraAfter;public Quaternion rotationBefore,rotationAfter;
        }
        [Serializable] sealed class DriftCullingControlReceipt
        {
            public DriftOutputControlReceipt view;public bool passed,controlApplied,parametersBefore,parametersControl,occlusionBefore,occlusionControl,occlusionOptionBefore,occlusionOptionControl,occlusionRestored,layerDistancesSame,spherical;
            public string optionsBefore,optionsControl;public int cullingMask;public float[] layerDistances;
        }
        [Serializable] sealed class DriftSkyControlReceipt
        {
            public DriftOutputControlReceipt view;public bool passed,controlApplied,flagsRestored,reflectionHeld,reflectionRestored,environmentSame;public int flagsBefore,flagsControl;
        }
        [Serializable] sealed class ReverseVisibilitySample
        {
            public string label,scene;public float elapsed,restAmount,fov;public int activeInk;public bool rootActive,atmosphereActive,ordinaryRendering,waterEnabled,reflectionEnabled,frozenWithinCapture;public Vector3 player,camera;public Quaternion cameraRotation;
        }
        [Serializable] sealed class ReverseVisibilityReceipt
        {
            public bool passed;public float revealAt;public ReverseVisibilitySample[] samples;
        }
        IEnumerator CaptureReverseVisibility(string label)
        {
            var sample=new ReverseVisibilitySample{label=label,scene=journey.Scene.ToString(),elapsed=journey.SceneElapsed,restAmount=journey.RestPoseAmount,fov=journey.CameraFieldOfView,activeInk=journey.ReverseInkRendererCount,rootActive=journey.ReverseRoutesActive,atmosphereActive=journey.ReverseAtmosphereActive,ordinaryRendering=!ReturnSkinDiffusionFeature.DriftDrawEnabled&&!ReturnSkinDiffusionFeature.DriftReviewBypass,waterEnabled=journey.DriftWaterVisible,reflectionEnabled=journey.DriftReflectionEnabled,player=journey.Position,camera=journey.CameraPosition,cameraRotation=journey.CameraRotation};
            float previousScale=Time.timeScale;
            try
            {
                Time.timeScale=0;yield return CaptureFrame(label);while(captureBusy)yield return null;
                sample.frozenWithinCapture=sample.elapsed==journey.SceneElapsed&&sample.player==journey.Position&&sample.camera==journey.CameraPosition&&sample.cameraRotation==journey.CameraRotation&&sample.fov==journey.CameraFieldOfView&&sample.activeInk==journey.ReverseInkRendererCount&&sample.rootActive==journey.ReverseRoutesActive&&sample.atmosphereActive==journey.ReverseAtmosphereActive;
            }
            finally{Time.timeScale=previousScale;driftWaterControlActive=false;}
            reverseVisibilitySamples.Add(sample);
            if(reverseVisibilitySamples.Count==3)
            {
                var quiet=reverseVisibilitySamples[0];var before=reverseVisibilitySamples[1];var reveal=reverseVisibilitySamples[2];
                var receipt=new ReverseVisibilityReceipt{revealAt=ReturnReverseJourney.FlattenAt,samples=reverseVisibilitySamples.ToArray()};
                receipt.passed=quiet.scene==Movement.StillWater.ToString()&&!quiet.rootActive&&quiet.activeInk==0&&quiet.restAmount>=.99f&&quiet.waterEnabled&&before.scene==Movement.Reunion.ToString()&&before.elapsed<receipt.revealAt&&before.rootActive&&before.activeInk==0&&reveal.scene==Movement.Reunion.ToString()&&reveal.elapsed>receipt.revealAt&&reveal.rootActive&&reveal.activeInk>0;
                receipt.passed&=!quiet.atmosphereActive&&before.atmosphereActive&&reveal.atmosphereActive;
                foreach(var item in receipt.samples)receipt.passed&=item.ordinaryRendering&&item.reflectionEnabled&&item.frozenWithinCapture;
                string evidence=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(evidence))File.WriteAllText(Path.Combine(evidence,"reverse-visibility-review.json"),JsonUtility.ToJson(receipt,true));
                Debug.Log("[RETURN reverse-visibility-review] "+JsonUtility.ToJson(receipt));
            }
        }
        [Serializable] sealed class DriftTransparentRendererEvidence
        {
            public string name,path;public int instanceId,layer;public string[] materials,shaders;public int[] queues;public bool enabledBefore,enabledRestored,mixedQueues;
        }
        [Serializable] sealed class DriftTransparentSetReceipt
        {
            public DriftOutputControlReceipt view;public bool passed,ordinaryRendering,reflectionHeld,reflectionRestored,renderersRestored;public int eligibleCount,disabledCount,cameraMask;public DriftTransparentRendererEvidence[] renderers;
            public int candidateCount;public bool inkOnly,waterBefore,waterHidden,waterRestored;
        }
        IEnumerator CaptureDriftTransparentSetControl()
        {
            var view=new DriftOutputControlReceipt{cameraName=cam.name,cameraInstanceId=cam.GetInstanceID(),width=Screen.width,height=Screen.height,elapsedBefore=journey.SceneElapsed,restBefore=journey.RestPoseAmount,fovBefore=journey.CameraFieldOfView,playerBefore=journey.Position,cameraBefore=journey.CameraPosition,rotationBefore=journey.CameraRotation};
            var receipt=new DriftTransparentSetReceipt{view=view,cameraMask=cam.cullingMask,inkOnly=reviewDriftInkControl,waterBefore=journey.DriftWaterVisible,ordinaryRendering=!ReturnSkinDiffusionFeature.DriftDrawEnabled&&!ReturnSkinDiffusionFeature.DriftReviewBypass};
            var selected=new List<Renderer>();var items=new List<DriftTransparentRendererEvidence>();
            foreach(var renderer in journey.DriftSceneRenderers)
            {
                if(!renderer.enabled||!renderer.gameObject.activeInHierarchy||(cam.cullingMask&(1<<renderer.gameObject.layer))==0)continue;
                var materials=renderer.sharedMaterials;bool eligible=false,opaque=false,ink=false;
                foreach(var material in materials){if(material==null)continue;if(material.renderQueue>=2501&&material.renderQueue<=5000)eligible=true;else opaque=true;if(material.shader.name=="Return/Ink")ink=true;}
                if(!eligible)continue;
                receipt.candidateCount++;if(reviewDriftInkControl&&!ink)continue;
                var item=new DriftTransparentRendererEvidence{name=renderer.name,instanceId=renderer.GetInstanceID(),layer=renderer.gameObject.layer,enabledBefore=renderer.enabled,mixedQueues=opaque,materials=new string[materials.Length],shaders=new string[materials.Length],queues=new int[materials.Length]};
                item.path=renderer.name;for(var owner=renderer.transform.parent;owner!=null;owner=owner.parent)item.path=owner.name+"/"+item.path;
                for(int i=0;i<materials.Length;i++){var material=materials[i];item.materials[i]=material==null?"none":material.name;item.shaders[i]=material==null?"none":material.shader.name;item.queues[i]=material==null?-1:material.renderQueue;}
                selected.Add(renderer);items.Add(item);
            }
            receipt.renderers=items.ToArray();receipt.eligibleCount=selected.Count;
            float previousScale=Time.timeScale;bool previousReflection=journey.DriftReflectionEnabled;RenderTexture previousTarget=cam.targetTexture;Matrix4x4 previousProjection=cam.projectionMatrix,previousView=cam.worldToCameraMatrix;view.originalTarget=previousTarget==null?"screen":previousTarget.name;
            try
            {
                if(selected.Count==0)throw new InvalidOperationException("No active transparent Still Water renderers found");
                Time.timeScale=0;view.scaledTimeBefore=Time.time;journey.DriftReflectionEnabled=false;receipt.reflectionHeld=!journey.DriftReflectionEnabled;
                yield return CaptureFrame(reviewDriftInkControl?"Leaf-Ink-Control-Baseline":"Leaf-Transparent-Set-Baseline");while(captureBusy)yield return null;
                foreach(var renderer in selected)renderer.enabled=false;
                yield return CaptureFrame(reviewDriftInkControl?"Leaf-Ink-Control-Hidden":"Leaf-Transparent-Set-Hidden");while(captureBusy)yield return null;
                receipt.waterHidden=journey.DriftWaterVisible;
                foreach(var renderer in selected)if(!renderer.enabled)receipt.disabledCount++;
                view.elapsedAfter=journey.SceneElapsed;view.restAfter=journey.RestPoseAmount;view.fovAfter=journey.CameraFieldOfView;view.playerAfter=journey.Position;view.cameraAfter=journey.CameraPosition;view.rotationAfter=journey.CameraRotation;view.scaledTimeAfter=Time.time;view.projectionSame=cam.projectionMatrix==previousProjection;view.viewSame=cam.worldToCameraMatrix==previousView;view.targetPreserved=cam.targetTexture==previousTarget;
                receipt.ordinaryRendering&=!ReturnSkinDiffusionFeature.DriftDrawEnabled&&!ReturnSkinDiffusionFeature.DriftReviewBypass;
            }
            finally
            {
                for(int i=0;i<selected.Count;i++)selected[i].enabled=items[i].enabledBefore;
                journey.DriftReflectionEnabled=previousReflection;Time.timeScale=previousScale;cam.targetTexture=previousTarget;
            }
            receipt.renderersRestored=true;for(int i=0;i<selected.Count;i++){items[i].enabledRestored=selected[i].enabled==items[i].enabledBefore;receipt.renderersRestored&=items[i].enabledRestored;}
            receipt.reflectionRestored=journey.DriftReflectionEnabled==previousReflection;view.targetRestored=cam.targetTexture==previousTarget;
            receipt.waterRestored=journey.DriftWaterVisible==receipt.waterBefore;
            view.poseSame=view.playerBefore==view.playerAfter&&view.cameraBefore==view.cameraAfter&&view.rotationBefore==view.rotationAfter&&view.elapsedBefore==view.elapsedAfter&&view.restBefore==view.restAfter&&view.fovBefore==view.fovAfter&&view.scaledTimeBefore==view.scaledTimeAfter;
            view.passed=view.poseSame&&view.targetPreserved&&view.targetRestored&&view.projectionSame&&view.viewSame;
            receipt.passed=view.passed&&receipt.ordinaryRendering&&receipt.reflectionHeld&&receipt.reflectionRestored&&receipt.renderersRestored&&receipt.eligibleCount>0&&receipt.disabledCount==receipt.eligibleCount;
            receipt.passed&=receipt.waterRestored&&(!reviewDriftInkControl||receipt.waterBefore&&receipt.waterHidden&&receipt.candidateCount>receipt.eligibleCount);
            string evidence=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(evidence))File.WriteAllText(Path.Combine(evidence,reviewDriftInkControl?"drift-ink-control.json":"drift-transparent-set-control.json"),JsonUtility.ToJson(receipt,true));
            Debug.Log("[RETURN drift-transparent-set-control] "+JsonUtility.ToJson(receipt));
        }
        [Serializable] sealed class DriftDrawControlReceipt
        {
            public DriftOutputControlReceipt view;public bool passed,controlApplied,stateRestored,meshMatrixSame,materialDisposed;
            public int vertices,leafPass,markerPass,recordedBefore,recordedAfter,executedBefore,executedAfter;
            public bool builtinUsed,builtinRestored;public int builtinPass,recordedExplicit,executedExplicit;
            public bool rendererUsed,rendererRestored,rendererMeshSame,rendererEnabled,rendererActive;public int rendererInstanceId;
            public bool listUsed,listRestored,layerHeld,layerRestored;public uint layerBefore,layerControl;public string listShaderTag,sourceShader;public int sourceQueue;
            public bool orderRestored,postBefore,postAfter;public int eventBefore,eventAfter,expectedEventBefore,expectedEventAfter;
        }
        IEnumerator CaptureDriftDrawControl()
        {
            var view=new DriftOutputControlReceipt{cameraName=cam.name,cameraInstanceId=cam.GetInstanceID(),width=Screen.width,height=Screen.height,elapsedBefore=journey.SceneElapsed,restBefore=journey.RestPoseAmount,fovBefore=journey.CameraFieldOfView,playerBefore=journey.Position,cameraBefore=journey.CameraPosition,rotationBefore=journey.CameraRotation};
            var receipt=new DriftDrawControlReceipt{view=view};float previousScale=Time.timeScale;RenderTexture previousTarget=cam.targetTexture;Matrix4x4 previousProjection=cam.projectionMatrix,previousView=cam.worldToCameraMatrix;
            bool paired=reviewDriftTransformControl||reviewDriftRendererControl;var renderer=journey.DriftLeafRenderer;var previousRenderer=ReturnSkinDiffusionFeature.DriftDrawRenderer;bool previousUseRenderer=ReturnSkinDiffusionFeature.DriftDrawUseRenderer;
            bool previousUseList=ReturnSkinDiffusionFeature.DriftDrawUseList;uint previousListMask=ReturnSkinDiffusionFeature.DriftDrawLayerMask,previousLayer=renderer.renderingLayerMask;
            bool previousBeforePost=ReturnSkinDiffusionFeature.DriftDrawBeforePost;
            int previousEventOverride=ReturnSkinDiffusionFeature.DriftDrawEventOverride;
            receipt.expectedEventBefore=reviewDriftTransparentControl?(int)UnityEngine.Rendering.Universal.RenderPassEvent.AfterRenderingSkybox+1:(int)UnityEngine.Rendering.Universal.RenderPassEvent.BeforeRenderingPostProcessing-1;
            receipt.expectedEventAfter=reviewDriftTransparentControl?(int)UnityEngine.Rendering.Universal.RenderPassEvent.BeforeRenderingPostProcessing-1:(int)UnityEngine.Rendering.Universal.RenderPassEvent.AfterRenderingPostProcessing;
            var previousCamera=ReturnSkinDiffusionFeature.DriftDrawCamera;var previousMesh=ReturnSkinDiffusionFeature.DriftDrawMesh;var previousMaterial=ReturnSkinDiffusionFeature.DriftDrawMaterial;var previousMatrix=ReturnSkinDiffusionFeature.DriftDrawMatrix;bool previousEnabled=ReturnSkinDiffusionFeature.DriftDrawEnabled,previousBuiltin=ReturnSkinDiffusionFeature.DriftDrawUseBuiltin;
            Mesh mesh=journey.DriftLeafMesh;Matrix4x4 model=journey.DriftLeafMatrix;Material diagnostic=null;view.originalTarget=previousTarget==null?"screen":previousTarget.name;
            try
            {
                Time.timeScale=0;view.scaledTimeBefore=Time.time;ReturnSkinDiffusionFeature.DriftDrawEnabled=false;
                ReturnSkinDiffusionFeature.DriftDrawUseList=reviewDriftOrderControl;ReturnSkinDiffusionFeature.DriftDrawBeforePost=reviewDriftOrderControl;
                ReturnSkinDiffusionFeature.DriftDrawEventOverride=reviewDriftTransparentControl?receipt.expectedEventBefore:-1;
                if(reviewDriftListControl){receipt.layerBefore=previousLayer;receipt.layerControl=1u<<31;renderer.renderingLayerMask=receipt.layerControl;ReturnSkinDiffusionFeature.DriftDrawLayerMask=receipt.layerControl;receipt.listShaderTag="UniversalForward";receipt.sourceShader=renderer.sharedMaterial.shader.name;receipt.sourceQueue=renderer.sharedMaterial.renderQueue;}
                if(!paired){yield return CaptureFrame("Leaf-Draw-Control-Baseline");while(captureBusy)yield return null;}
                var shader=Resources.Load<Shader>("ReturnDriftVisibilityControl");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Drift draw shader unavailable");
                diagnostic=new Material(shader);receipt.vertices=mesh.vertexCount;receipt.leafPass=diagnostic.FindPass("DriftForcedLeaf");receipt.markerPass=diagnostic.FindPass("DriftMarker");
                receipt.builtinPass=diagnostic.FindPass("DriftBuiltinLeaf");
                if(receipt.leafPass<0||receipt.markerPass<0||paired&&receipt.builtinPass<0)throw new InvalidOperationException("Drift draw shader passes unavailable");
                ReturnSkinDiffusionFeature.DriftDrawCamera=cam;ReturnSkinDiffusionFeature.DriftDrawMesh=mesh;ReturnSkinDiffusionFeature.DriftDrawMatrix=model;ReturnSkinDiffusionFeature.DriftDrawMaterial=diagnostic;
                ReturnSkinDiffusionFeature.DriftDrawRenderer=renderer;ReturnSkinDiffusionFeature.DriftDrawUseRenderer=reviewDriftListControl;
                receipt.recordedBefore=ReturnSkinDiffusionFeature.DriftDrawRecorded;receipt.executedBefore=ReturnSkinDiffusionFeature.DriftDrawExecuted;ReturnSkinDiffusionFeature.DriftDrawUseBuiltin=reviewDriftRendererControl;ReturnSkinDiffusionFeature.DriftDrawEnabled=true;receipt.controlApplied=true;
                yield return CaptureFrame(reviewDriftTransparentControl?"Leaf-Transparent-Control-Before":reviewDriftOrderControl?"Leaf-Order-Control-Before":reviewDriftListControl?"Leaf-List-Control-Manual":reviewDriftRendererControl?"Leaf-Renderer-Control-Mesh":reviewDriftTransformControl?"Leaf-Transform-Control-Explicit":"Leaf-Draw-Control-Forced");while(captureBusy)yield return null;
                receipt.recordedExplicit=ReturnSkinDiffusionFeature.DriftDrawRecorded;receipt.executedExplicit=ReturnSkinDiffusionFeature.DriftDrawExecuted;
                receipt.eventBefore=ReturnSkinDiffusionFeature.DriftDrawLastEvent;receipt.postBefore=ReturnSkinDiffusionFeature.DriftDrawLastPostEnabled;
                if(reviewDriftTransformControl){ReturnSkinDiffusionFeature.DriftDrawUseBuiltin=true;receipt.builtinUsed=true;yield return CaptureFrame("Leaf-Transform-Control-Builtin");while(captureBusy)yield return null;}
                if(reviewDriftRendererControl){ReturnSkinDiffusionFeature.DriftDrawUseRenderer=true;receipt.rendererUsed=true;receipt.builtinUsed=true;receipt.rendererInstanceId=renderer.GetInstanceID();receipt.rendererEnabled=renderer.enabled;receipt.rendererActive=renderer.gameObject.activeInHierarchy;if(reviewDriftListControl){ReturnSkinDiffusionFeature.DriftDrawUseList=true;receipt.listUsed=true;}ReturnSkinDiffusionFeature.DriftDrawBeforePost=false;ReturnSkinDiffusionFeature.DriftDrawEventOverride=reviewDriftTransparentControl?receipt.expectedEventAfter:-1;yield return CaptureFrame(reviewDriftTransparentControl?"Leaf-Transparent-Control-After":reviewDriftOrderControl?"Leaf-Order-Control-After":reviewDriftListControl?"Leaf-List-Control-Culled":"Leaf-Renderer-Control-Renderer");while(captureBusy)yield return null;receipt.rendererMeshSame=renderer==journey.DriftLeafRenderer&&renderer.GetComponent<MeshFilter>().sharedMesh==mesh&&renderer.localToWorldMatrix==model;receipt.layerHeld=!reviewDriftListControl||renderer.renderingLayerMask==receipt.layerControl;}
                receipt.recordedAfter=ReturnSkinDiffusionFeature.DriftDrawRecorded;receipt.executedAfter=ReturnSkinDiffusionFeature.DriftDrawExecuted;receipt.meshMatrixSame=mesh==journey.DriftLeafMesh&&model==journey.DriftLeafMatrix;
                receipt.eventAfter=ReturnSkinDiffusionFeature.DriftDrawLastEvent;receipt.postAfter=ReturnSkinDiffusionFeature.DriftDrawLastPostEnabled;
                view.elapsedAfter=journey.SceneElapsed;view.restAfter=journey.RestPoseAmount;view.fovAfter=journey.CameraFieldOfView;view.playerAfter=journey.Position;view.cameraAfter=journey.CameraPosition;view.rotationAfter=journey.CameraRotation;view.scaledTimeAfter=Time.time;view.projectionSame=cam.projectionMatrix==previousProjection;view.viewSame=cam.worldToCameraMatrix==previousView;view.targetPreserved=cam.targetTexture==previousTarget;
            }
            finally
            {
                ReturnSkinDiffusionFeature.DriftDrawBeforePost=previousBeforePost;
                ReturnSkinDiffusionFeature.DriftDrawEventOverride=previousEventOverride;
                ReturnSkinDiffusionFeature.DriftDrawUseList=previousUseList;ReturnSkinDiffusionFeature.DriftDrawLayerMask=previousListMask;renderer.renderingLayerMask=previousLayer;
                ReturnSkinDiffusionFeature.DriftDrawRenderer=previousRenderer;ReturnSkinDiffusionFeature.DriftDrawUseRenderer=previousUseRenderer;
                ReturnSkinDiffusionFeature.DriftDrawEnabled=previousEnabled;ReturnSkinDiffusionFeature.DriftDrawUseBuiltin=previousBuiltin;ReturnSkinDiffusionFeature.DriftDrawCamera=previousCamera;ReturnSkinDiffusionFeature.DriftDrawMesh=previousMesh;ReturnSkinDiffusionFeature.DriftDrawMaterial=previousMaterial;ReturnSkinDiffusionFeature.DriftDrawMatrix=previousMatrix;Time.timeScale=previousScale;cam.targetTexture=previousTarget;
                if(diagnostic!=null){DestroyImmediate(diagnostic);diagnostic=null;receipt.materialDisposed=true;}
            }
            receipt.stateRestored=ReturnSkinDiffusionFeature.DriftDrawEnabled==previousEnabled&&ReturnSkinDiffusionFeature.DriftDrawCamera==previousCamera&&ReturnSkinDiffusionFeature.DriftDrawMesh==previousMesh&&ReturnSkinDiffusionFeature.DriftDrawMaterial==previousMaterial&&ReturnSkinDiffusionFeature.DriftDrawMatrix==previousMatrix;
            view.targetRestored=cam.targetTexture==previousTarget;view.poseSame=view.playerBefore==view.playerAfter&&view.cameraBefore==view.cameraAfter&&view.rotationBefore==view.rotationAfter&&view.elapsedBefore==view.elapsedAfter&&view.restBefore==view.restAfter&&view.fovBefore==view.fovAfter&&view.scaledTimeBefore==view.scaledTimeAfter;
            view.passed=view.poseSame&&view.targetPreserved&&view.targetRestored&&view.projectionSame&&view.viewSame;
            receipt.builtinRestored=ReturnSkinDiffusionFeature.DriftDrawUseBuiltin==previousBuiltin;
            receipt.rendererRestored=ReturnSkinDiffusionFeature.DriftDrawRenderer==previousRenderer&&ReturnSkinDiffusionFeature.DriftDrawUseRenderer==previousUseRenderer;
            receipt.listRestored=ReturnSkinDiffusionFeature.DriftDrawUseList==previousUseList&&ReturnSkinDiffusionFeature.DriftDrawLayerMask==previousListMask;receipt.layerRestored=renderer.renderingLayerMask==previousLayer;
            receipt.orderRestored=ReturnSkinDiffusionFeature.DriftDrawBeforePost==previousBeforePost&&ReturnSkinDiffusionFeature.DriftDrawEventOverride==previousEventOverride;
            receipt.passed=view.passed&&receipt.controlApplied&&receipt.meshMatrixSame&&receipt.stateRestored&&receipt.materialDisposed&&receipt.builtinRestored&&receipt.rendererRestored&&receipt.listRestored&&receipt.layerRestored&&receipt.recordedExplicit>receipt.recordedBefore&&receipt.executedExplicit>receipt.executedBefore&&(!paired||receipt.builtinUsed&&receipt.recordedAfter>receipt.recordedExplicit&&receipt.executedAfter>receipt.executedExplicit)&&(!reviewDriftRendererControl||receipt.rendererUsed&&receipt.rendererMeshSame&&receipt.rendererEnabled&&receipt.rendererActive)&&(!reviewDriftListControl||receipt.listUsed&&receipt.layerHeld);
            receipt.passed&=receipt.orderRestored&&(!reviewDriftOrderControl||receipt.eventBefore==receipt.expectedEventBefore&&receipt.eventAfter==receipt.expectedEventAfter&&receipt.postBefore==receipt.postAfter);
            string evidence=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(evidence))File.WriteAllText(Path.Combine(evidence,reviewDriftTransparentControl?"drift-transparent-control.json":reviewDriftOrderControl?"drift-order-control.json":reviewDriftListControl?"drift-list-control.json":reviewDriftRendererControl?"drift-renderer-control.json":reviewDriftTransformControl?"drift-transform-control.json":"drift-draw-control.json"),JsonUtility.ToJson(receipt,true));
            Debug.Log("[RETURN drift-draw-control] "+JsonUtility.ToJson(receipt));
        }
        IEnumerator CaptureDriftSkyControl()
        {
            var view=new DriftOutputControlReceipt{cameraName=cam.name,cameraInstanceId=cam.GetInstanceID(),width=Screen.width,height=Screen.height,elapsedBefore=journey.SceneElapsed,restBefore=journey.RestPoseAmount,fovBefore=journey.CameraFieldOfView,playerBefore=journey.Position,cameraBefore=journey.CameraPosition,rotationBefore=journey.CameraRotation};
            CameraClearFlags previousFlags=cam.clearFlags;var receipt=new DriftSkyControlReceipt{view=view,flagsBefore=(int)previousFlags};float previousScale=Time.timeScale;bool previousReflection=journey.DriftReflectionEnabled;
            RenderTexture previousTarget=cam.targetTexture;Matrix4x4 previousProjection=cam.projectionMatrix,previousView=cam.worldToCameraMatrix;Material previousSky=RenderSettings.skybox;Texture previousEnvironment=RenderSettings.customReflectionTexture;Color skyAmbient=RenderSettings.ambientSkyColor,equatorAmbient=RenderSettings.ambientEquatorColor,groundAmbient=RenderSettings.ambientGroundColor;view.originalTarget=previousTarget==null?"screen":previousTarget.name;
            try
            {
                Time.timeScale=0;view.scaledTimeBefore=Time.time;journey.DriftReflectionEnabled=false;receipt.reflectionHeld=!journey.DriftReflectionEnabled;
                if(previousFlags==CameraClearFlags.Skybox)
                {
                    yield return CaptureFrame("Leaf-Sky-Control-Baseline");while(captureBusy)yield return null;
                    cam.clearFlags=CameraClearFlags.SolidColor;receipt.controlApplied=true;receipt.flagsControl=(int)cam.clearFlags;
                    yield return CaptureFrame("Leaf-Sky-Control-Plain");while(captureBusy)yield return null;
                }
                view.elapsedAfter=journey.SceneElapsed;view.restAfter=journey.RestPoseAmount;view.fovAfter=journey.CameraFieldOfView;view.playerAfter=journey.Position;view.cameraAfter=journey.CameraPosition;view.rotationAfter=journey.CameraRotation;view.scaledTimeAfter=Time.time;view.projectionSame=cam.projectionMatrix==previousProjection;view.viewSame=cam.worldToCameraMatrix==previousView;view.targetPreserved=cam.targetTexture==previousTarget;
                receipt.environmentSame=RenderSettings.skybox==previousSky&&RenderSettings.customReflectionTexture==previousEnvironment&&RenderSettings.ambientSkyColor==skyAmbient&&RenderSettings.ambientEquatorColor==equatorAmbient&&RenderSettings.ambientGroundColor==groundAmbient;
            }
            finally{cam.clearFlags=previousFlags;journey.DriftReflectionEnabled=previousReflection;Time.timeScale=previousScale;cam.targetTexture=previousTarget;}
            receipt.flagsRestored=cam.clearFlags==previousFlags;receipt.reflectionRestored=journey.DriftReflectionEnabled==previousReflection;view.targetRestored=cam.targetTexture==previousTarget;
            view.poseSame=view.playerBefore==view.playerAfter&&view.cameraBefore==view.cameraAfter&&view.rotationBefore==view.rotationAfter&&view.elapsedBefore==view.elapsedAfter&&view.restBefore==view.restAfter&&view.fovBefore==view.fovAfter&&view.scaledTimeBefore==view.scaledTimeAfter;
            view.passed=view.poseSame&&view.targetPreserved&&view.targetRestored&&view.projectionSame&&view.viewSame;
            receipt.passed=view.passed&&receipt.flagsRestored&&receipt.reflectionHeld&&receipt.reflectionRestored&&receipt.environmentSame&&(!receipt.controlApplied||receipt.flagsControl==(int)CameraClearFlags.SolidColor);
            string evidence=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(evidence))File.WriteAllText(Path.Combine(evidence,"drift-sky-control.json"),JsonUtility.ToJson(receipt,true));
            Debug.Log("[RETURN drift-sky-control] "+JsonUtility.ToJson(receipt));
        }
        IEnumerator CaptureDriftCullingControl()
        {
            var view=new DriftOutputControlReceipt{cameraName=cam.name,cameraInstanceId=cam.GetInstanceID(),width=Screen.width,height=Screen.height,elapsedBefore=journey.SceneElapsed,restBefore=journey.RestPoseAmount,fovBefore=journey.CameraFieldOfView,playerBefore=journey.Position,cameraBefore=journey.CameraPosition,rotationBefore=journey.CameraRotation};
            var receipt=new DriftCullingControlReceipt{view=view,occlusionBefore=cam.useOcclusionCulling,spherical=cam.layerCullSpherical,cullingMask=cam.cullingMask,layerDistances=cam.layerCullDistances};
            receipt.parametersBefore=cam.TryGetCullingParameters(out var baseline);if(receipt.parametersBefore){receipt.optionsBefore=baseline.cullingOptions.ToString();receipt.occlusionOptionBefore=(baseline.cullingOptions&UnityEngine.Rendering.CullingOptions.OcclusionCull)!=0;}
            float previousScale=Time.timeScale;RenderTexture previousTarget=cam.targetTexture;Matrix4x4 previousProjection=cam.projectionMatrix,previousView=cam.worldToCameraMatrix;view.originalTarget=previousTarget==null?"screen":previousTarget.name;
            try
            {
                Time.timeScale=0;view.scaledTimeBefore=Time.time;
                if(receipt.occlusionBefore&&receipt.occlusionOptionBefore)
                {
                    yield return CaptureFrame("Leaf-Culling-Control-Baseline");while(captureBusy)yield return null;
                    cam.useOcclusionCulling=false;receipt.controlApplied=true;receipt.occlusionControl=cam.useOcclusionCulling;
                    receipt.parametersControl=cam.TryGetCullingParameters(out var control);if(receipt.parametersControl){receipt.optionsControl=control.cullingOptions.ToString();receipt.occlusionOptionControl=(control.cullingOptions&UnityEngine.Rendering.CullingOptions.OcclusionCull)!=0;}
                    yield return CaptureFrame("Leaf-Culling-Control-Bypassed");while(captureBusy)yield return null;
                }
                view.elapsedAfter=journey.SceneElapsed;view.restAfter=journey.RestPoseAmount;view.fovAfter=journey.CameraFieldOfView;view.playerAfter=journey.Position;view.cameraAfter=journey.CameraPosition;view.rotationAfter=journey.CameraRotation;view.scaledTimeAfter=Time.time;view.projectionSame=cam.projectionMatrix==previousProjection;view.viewSame=cam.worldToCameraMatrix==previousView;view.targetPreserved=cam.targetTexture==previousTarget;
            }
            finally{cam.useOcclusionCulling=receipt.occlusionBefore;Time.timeScale=previousScale;cam.targetTexture=previousTarget;}
            receipt.occlusionRestored=cam.useOcclusionCulling==receipt.occlusionBefore;receipt.layerDistancesSame=true;var distances=cam.layerCullDistances;for(int i=0;i<distances.Length;i++)receipt.layerDistancesSame&=distances[i]==receipt.layerDistances[i];
            view.targetRestored=cam.targetTexture==previousTarget;view.poseSame=view.playerBefore==view.playerAfter&&view.cameraBefore==view.cameraAfter&&view.rotationBefore==view.rotationAfter&&view.elapsedBefore==view.elapsedAfter&&view.restBefore==view.restAfter&&view.fovBefore==view.fovAfter&&view.scaledTimeBefore==view.scaledTimeAfter;
            view.passed=view.poseSame&&view.targetPreserved&&view.targetRestored&&view.projectionSame&&view.viewSame;
            receipt.passed=view.passed&&receipt.parametersBefore&&receipt.occlusionRestored&&receipt.layerDistancesSame&&(!receipt.controlApplied||receipt.parametersControl&&!receipt.occlusionControl&&!receipt.occlusionOptionControl);
            string evidence=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(evidence))File.WriteAllText(Path.Combine(evidence,"drift-culling-control.json"),JsonUtility.ToJson(receipt,true));
            Debug.Log("[RETURN drift-culling-control] "+JsonUtility.ToJson(receipt));
        }
        IEnumerator CaptureDriftSkinControl()
        {
            var receipt=new DriftOutputControlReceipt{cameraName=cam.name,cameraInstanceId=cam.GetInstanceID(),width=Screen.width,height=Screen.height,elapsedBefore=journey.SceneElapsed,restBefore=journey.RestPoseAmount,fovBefore=journey.CameraFieldOfView,playerBefore=journey.Position,cameraBefore=journey.CameraPosition,rotationBefore=journey.CameraRotation,transparentQueueLower=UnityEngine.Rendering.RenderQueueRange.transparent.lowerBound,transparentQueueUpper=UnityEngine.Rendering.RenderQueueRange.transparent.upperBound};
            float previousScale=Time.timeScale;RenderTexture previousTarget=cam.targetTexture;Matrix4x4 previousProjection=cam.projectionMatrix,previousView=cam.worldToCameraMatrix;bool previousBypass=ReturnSkinDiffusionFeature.DriftReviewBypass,previousTracking=ReturnSkinDiffusionFeature.DriftReviewTracking;receipt.originalTarget=previousTarget==null?"screen":previousTarget.name;
            try
            {
                Time.timeScale=0;receipt.scaledTimeBefore=Time.time;ReturnSkinDiffusionFeature.DriftReviewTracking=true;ReturnSkinDiffusionFeature.DriftReviewBypass=false;receipt.skinBypassBaseline=ReturnSkinDiffusionFeature.DriftReviewBypass;receipt.skinFramesBefore=ReturnSkinDiffusionFeature.DriftReviewFrames;
                yield return CaptureFrame("Leaf-Skin-Control-Baseline");while(captureBusy)yield return null;receipt.skinFramesBaseline=ReturnSkinDiffusionFeature.DriftReviewFrames;
                ReturnSkinDiffusionFeature.DriftReviewBypass=true;receipt.skinBypassControl=ReturnSkinDiffusionFeature.DriftReviewBypass;
                yield return CaptureFrame("Leaf-Skin-Control-Bypassed");while(captureBusy)yield return null;receipt.skinFramesControl=ReturnSkinDiffusionFeature.DriftReviewFrames;
                receipt.elapsedAfter=journey.SceneElapsed;receipt.restAfter=journey.RestPoseAmount;receipt.fovAfter=journey.CameraFieldOfView;receipt.playerAfter=journey.Position;receipt.cameraAfter=journey.CameraPosition;receipt.rotationAfter=journey.CameraRotation;receipt.scaledTimeAfter=Time.time;receipt.projectionSame=cam.projectionMatrix==previousProjection;receipt.viewSame=cam.worldToCameraMatrix==previousView;receipt.targetPreserved=cam.targetTexture==previousTarget;
            }
            finally{ReturnSkinDiffusionFeature.DriftReviewBypass=previousBypass;ReturnSkinDiffusionFeature.DriftReviewTracking=previousTracking;Time.timeScale=previousScale;cam.targetTexture=previousTarget;}
            receipt.skinBypassRestored=ReturnSkinDiffusionFeature.DriftReviewBypass==previousBypass;receipt.skinTrackingRestored=ReturnSkinDiffusionFeature.DriftReviewTracking==previousTracking;receipt.targetRestored=cam.targetTexture==previousTarget;
            receipt.poseSame=receipt.playerBefore==receipt.playerAfter&&receipt.cameraBefore==receipt.cameraAfter&&receipt.rotationBefore==receipt.rotationAfter&&receipt.elapsedBefore==receipt.elapsedAfter&&receipt.restBefore==receipt.restAfter&&receipt.fovBefore==receipt.fovAfter&&receipt.scaledTimeBefore==receipt.scaledTimeAfter;
            receipt.passed=!receipt.skinBypassBaseline&&receipt.skinBypassControl&&receipt.skinFramesBaseline>receipt.skinFramesBefore&&receipt.skinFramesControl==receipt.skinFramesBaseline&&receipt.skinBypassRestored&&receipt.skinTrackingRestored&&receipt.poseSame&&receipt.targetPreserved&&receipt.targetRestored&&receipt.projectionSame&&receipt.viewSame;
            string evidence=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(evidence))File.WriteAllText(Path.Combine(evidence,"drift-skin-control.json"),JsonUtility.ToJson(receipt,true));
            Debug.Log("[RETURN drift-skin-control] "+JsonUtility.ToJson(receipt));
        }
        IEnumerator CaptureDriftOutputControl()
        {
            var receipt=new DriftOutputControlReceipt{cameraName=cam.name,cameraInstanceId=cam.GetInstanceID(),width=Screen.width,height=Screen.height,elapsedBefore=journey.SceneElapsed,restBefore=journey.RestPoseAmount,fovBefore=journey.CameraFieldOfView,playerBefore=journey.Position,cameraBefore=journey.CameraPosition,rotationBefore=journey.CameraRotation};
            float previousScale=Time.timeScale;RenderTexture previousTarget=cam.targetTexture,cameraImage=null;Matrix4x4 previousProjection=cam.projectionMatrix,previousView=cam.worldToCameraMatrix;receipt.originalTarget=previousTarget==null?"screen":previousTarget.name;
            try
            {
                Time.timeScale=0;receipt.scaledTimeBefore=Time.time;
                yield return CaptureFrame("Leaf-Output-Control-Screen");while(captureBusy)yield return null;
                cameraImage=new RenderTexture(receipt.width,receipt.height,24,RenderTextureFormat.ARGB32){name="RETURN drift camera output control"};cameraImage.Create();
                var request=new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=cameraImage};
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam,request);receipt.targetPreserved=cam.targetTexture==previousTarget;
                yield return CaptureFrame("Leaf-Output-Control-Camera",cameraImage);while(captureBusy)yield return null;
                receipt.elapsedAfter=journey.SceneElapsed;receipt.restAfter=journey.RestPoseAmount;receipt.fovAfter=journey.CameraFieldOfView;receipt.playerAfter=journey.Position;receipt.cameraAfter=journey.CameraPosition;receipt.rotationAfter=journey.CameraRotation;receipt.scaledTimeAfter=Time.time;receipt.projectionSame=cam.projectionMatrix==previousProjection;receipt.viewSame=cam.worldToCameraMatrix==previousView;
            }
            finally{cam.targetTexture=previousTarget;Time.timeScale=previousScale;if(cameraImage!=null){cameraImage.Release();Destroy(cameraImage);}}
            receipt.targetRestored=cam.targetTexture==previousTarget;
            receipt.poseSame=receipt.playerBefore==receipt.playerAfter&&receipt.cameraBefore==receipt.cameraAfter&&receipt.rotationBefore==receipt.rotationAfter&&receipt.elapsedBefore==receipt.elapsedAfter&&receipt.restBefore==receipt.restAfter&&receipt.fovBefore==receipt.fovAfter&&receipt.scaledTimeBefore==receipt.scaledTimeAfter;
            receipt.passed=receipt.poseSame&&receipt.targetPreserved&&receipt.targetRestored&&receipt.projectionSame&&receipt.viewSame;
            string evidence=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(evidence))File.WriteAllText(Path.Combine(evidence,"drift-output-control.json"),JsonUtility.ToJson(receipt,true));
            Debug.Log("[RETURN drift-output-control] "+JsonUtility.ToJson(receipt));
        }
        static bool PriorityFrame(string name)=>name=="Return-Handoff"||name=="Return-Pullback";
        public bool TryRequestFrame(string name)
        {if(string.IsNullOrEmpty(previewRoot)||(previewCapture!=null&&!previewCapture.CanReserve(PriorityFrame(name))))return false;StartCoroutine(CaptureFrame(name));return true;}
        IEnumerator CaptureFrame(string requestedName=null)=>CaptureFrame(requestedName,null);
        IEnumerator CaptureFrame(string requestedName,RenderTexture source)
        {
            if(previewCapture==null)previewCapture=new ReturnPreviewCapture();
            if(!previewCapture.TryReserve(PriorityFrame(requestedName)))yield break;
            yield return new WaitForEndOfFrame();
            bool traceMemoryCapture=!string.IsNullOrEmpty(requestedName)&&requestedName.StartsWith("Fragments-Presence-",StringComparison.Ordinal);
            long captureAt=traceMemoryCapture?System.Diagnostics.Stopwatch.GetTimestamp():0,renderedAt=captureAt;
            if(traceMemoryCapture)Debug.Log("[RETURN memory-capture] BEGIN frame="+Time.frameCount+" name="+requestedName+" focused="+Application.isFocused);
            bool reuseScreen=string.IsNullOrEmpty(requestedName)&&source==null&&acceptance==null&&Application.isFocused;
            if(previewSurface!=null&&!reuseScreen)
            {
                bool failed=false;
                try{previewSurface.Render(DrawPreviewInterface);}
                catch(Exception error){failed=true;previewCapture.CancelReservation();Debug.LogError("[RETURN capture] FAIL "+error);}
                if(failed)yield break;
            }
            if(traceMemoryCapture)renderedAt=System.Diagnostics.Stopwatch.GetTimestamp();
            if(reviewShoreOcclusion&&acceptance!=null&&requestedName=="Island-Arrival")journey.WriteShoreViewDiagnostic(previewRoot);
            string name=string.IsNullOrEmpty(requestedName)?"live-"+(previewFrameSlot++%4)+".jpg":requestedName+".png";
            previewCapture.Capture(previewRoot,name,()=>CaptureState(name),reuseScreen?null:source??previewSurface?.Target);
            if(traceMemoryCapture)Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,"[RETURN memory-capture] END frame={0} name={1} renderSeconds={2:F6} dispatchSeconds={3:F6}",Time.frameCount,requestedName,(renderedAt-captureAt)/(double)System.Diagnostics.Stopwatch.Frequency,(System.Diagnostics.Stopwatch.GetTimestamp()-renderedAt)/(double)System.Diagnostics.Stopwatch.Frequency));
        }
        string CaptureState(string name)=>JsonUtility.ToJson(new PreviewCapture{processId=previewProcessId,backgroundRendering=Application.runInBackground,asynchronousCapture=SystemInfo.supportsAsyncGPUReadback,frameSeconds=Time.unscaledDeltaTime,realtime=Time.realtimeSinceStartup,renderedFrameCount=Time.frameCount,focused=Application.isFocused,vSyncCount=QualitySettings.vSyncCount,targetFrameRate=Application.targetFrameRate,qualityLevel=QualitySettings.GetQualityLevel(),message="Playable Unity build · "+(prologue>=0?"HOSPITAL OPENING":ChapterNames[(int)Chapter])+" · "+(acceptance!=null?(reviewMotion?"normal-speed return review":"automated traversal"):reviewWater?"3D review":"live game"),frame=name,revision=DateTime.UtcNow.Ticks.ToString(),elapsed=ChapterElapsed,swimming=journey!=null&&journey.Active&&Chapter==Movement.StillWater&&!journey.Resting&&journey.Position.y<-.25f&&new Vector2(journey.Velocity.x,journey.Velocity.z).magnitude>1,swimPhase=journey!=null&&journey.Active?journey.SwimPhase:0,swimFootSpeed=journey!=null&&journey.Active?journey.SwimFootSpeed:0,paused=paused||journalOpen,playing=Playing,opening=prologue>=0,openingElapsed=prologue,openingStage=prologue>=0?Mathf.Min(3,sound.PrologueStage(prologue)):-1,story=ActiveStoryLine});
        void Styles()
        {
            if(heading!=null)return;
            Font font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            string glyphs="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?'-/()·“”";
            foreach(int size in new[]{12,16,19,66})font.RequestCharactersInTexture(glyphs,size,FontStyle.Normal);
            heading=new GUIStyle{font=font,fontSize=66,fontStyle=FontStyle.Normal,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.92f,.94f,.88f)}};
            small=new GUIStyle{font=font,fontSize=12,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.74f,.80f,.76f)}};
            caption=new GUIStyle{font=font,fontSize=19,alignment=TextAnchor.MiddleCenter,wordWrap=true,normal={textColor=new Color(.89f,.92f,.86f)}};
            memoryText=new GUIStyle(caption){alignment=TextAnchor.UpperLeft};
            hintStyle=new GUIStyle(caption){fontSize=16};
            button=new GUIStyle{font=font,fontSize=16,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(15,15,10,10)};
            button.normal.background=UiTexture(new Color(.035f,.048f,.042f,.96f));button.normal.textColor=new Color(.91f,.88f,.73f);
            button.hover.background=UiTexture(new Color(.13f,.15f,.11f,.97f));button.hover.textColor=new Color(1,.94f,.72f);
            button.active.background=UiTexture(new Color(.22f,.23f,.16f,.98f));button.active.textColor=Color.white;
            button.focused.background=button.hover.background;button.focused.textColor=button.hover.textColor;
            panelTexture=new Texture2D(1,64,TextureFormat.RGBA32,false);panelTexture.filterMode=FilterMode.Bilinear;panelTexture.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<64;y++){float t=y/63f;panelTexture.SetPixel(0,y,new Color(1,1,1,Mathf.SmoothStep(0,1,Mathf.Min(t,1-t)*6)));}
            panelTexture.Apply();uiTextures.Add(panelTexture);
            // A curved, feathered silhouette is shared by both lids; it is generated once.
            eyelidTexture=new Texture2D(512,256,TextureFormat.RGBA32,false);eyelidTexture.filterMode=FilterMode.Bilinear;eyelidTexture.wrapMode=TextureWrapMode.Clamp;
            var lidPixels=new Color32[512*256];
            for(int y=0;y<256;y++)for(int x=0;x<512;x++){
                float across=Mathf.Abs(x/511f*2-1),edge=.60f+.25f*Mathf.Pow(across,1.8f),down=1-y/255f;
                float opacity=1-Mathf.SmoothStep(0,1,(down-edge+.012f)/.024f);
                lidPixels[y*512+x]=new Color32(0,0,0,(byte)Mathf.RoundToInt(opacity*255));
            }
            eyelidTexture.SetPixels32(lidPixels);eyelidTexture.Apply(false,true);uiTextures.Add(eyelidTexture);
        }
        Texture2D UiTexture(Color color)
        {Texture2D t=new Texture2D(1,1);t.SetPixel(0,0,color);t.Apply();uiTextures.Add(t);return t;}
        bool MenuButton(Rect rect,string label,bool primary=false)
        {Shade(rect,new Color(.88f,.81f,.49f,primary?.72f:.3f));Rect inner=new Rect(rect.x+1,rect.y+1,rect.width-2,rect.height-2);if(drawingPreviewUI){previewSurface.Rect(inner,button.normal.background,Color.white);Text(inner,label,button);return false;}return GUI.Button(inner,label,button);}
        void SoftPanel(Rect rect,Color color)
        {if(drawingPreviewUI){previewSurface.Rect(rect,panelTexture,color);return;}Color old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,panelTexture);GUI.color=old;}
        void Shade(Rect r,Color c){if(drawingPreviewUI){previewSurface.Rect(r,Texture2D.whiteTexture,c);return;}Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        void ScreenShade(Color c)
        {
            if(drawingPreviewUI){Shade(new Rect(0,0,1280,720),c);return;}
            Matrix4x4 old=GUI.matrix;GUI.matrix=Matrix4x4.identity;
            Shade(new Rect(0,0,Screen.width,Screen.height),c);GUI.matrix=old;
        }
        void DrawEyelids(float amount)
        {
            if(amount<=0)return;
            amount=Mathf.Clamp01(amount);
            if(amount>=.999f){ScreenShade(Color.black);return;}
            // Shared geometry keeps the app and preview lids aligned without flipped texture UVs.
            for(int x=0;x<1280;x+=20)
            {
                float across=Mathf.Abs((x+10)/640f-1),height=amount*(378+157*Mathf.Pow(across,1.8f));
                float feather=Mathf.Min(2,height),solid=height-feather;
                Shade(new Rect(x,0,20,solid),Color.black);
                Shade(new Rect(x,solid,20,feather),new Color(0,0,0,.6f));
                Shade(new Rect(x,720-solid,20,solid),Color.black);
                Shade(new Rect(x,720-height,20,feather),new Color(0,0,0,.6f));
            }
        }
        void Text(Rect r,string text,GUIStyle style,Color? color=null)
        {if(drawingPreviewUI){previewSurface.Text(r,text,style,color??style.normal.textColor);return;}Color old=style.normal.textColor;if(color.HasValue)style.normal.textColor=color.Value;GUI.Label(r,text,style);style.normal.textColor=old;}
        void DrawJournal()
        {
            Shade(new Rect(0,0,1280,720),new Color(.015f,.024f,.02f,.98f));
            Text(new Rect(180,35,920,55),"The voices that stayed",caption);
            journalPage=Mathf.Clamp(journalPage,0,JournalPageCount-1);
            Text(new Rect(180,91,920,30),journalPage==0?RecoveredVoices+" / 4 voices found":"On the way back",hintStyle,new Color(.69f,.75f,.69f));
            if(journalPage==0)
            {
                string[] feelings={"Warmth","Shame","Regret","Fear"};
                for(int i=0;i<4;i++)
                {
                    bool found=MemoryFound(i);float x=136+(i%2)*516,y=145+(i/2)*199;
                    SoftPanel(new Rect(x,y,488,179),new Color(.09f,.12f,.10f,.9f));
                    Color signal=found?new Color(.88f,.81f,.49f,.9f):new Color(.43f,.48f,.44f,.45f);
                    Shade(new Rect(x+22,y+22,15,15),signal);
                    Text(new Rect(x+50,y+18,415,30),found?feelings[i]:"Signal "+(i+1).ToString("00"),memoryText,found?new Color(.91f,.91f,.80f):new Color(.63f,.69f,.64f));
                    string words=found?Memories[i].Substring(feelings[i].Length+2):"A voice I cannot make out yet.";
                    Text(new Rect(x+22,y+60,444,100),words,memoryText,found?new Color(.85f,.89f,.84f):new Color(.50f,.57f,.51f));
                }
            }
            else
            {
                List<string> notes=JourneyNotes();int first=(journalPage-1)*3;float y=151;
                for(int i=first;i<Mathf.Min(first+3,notes.Count);i++)
                {
                    float height=Mathf.Clamp(memoryText.CalcHeight(new GUIContent(notes[i]),920)+24,62,128);
                    SoftPanel(new Rect(158,y,964,height),new Color(.09f,.12f,.10f,.9f));
                    Text(new Rect(180,y+12,920,height-24),notes[i],memoryText,new Color(.85f,.89f,.84f));y+=height+22;
                }
            }
            Text(new Rect(180,594,920,30),(journalPage+1)+" / "+JournalPageCount, hintStyle,new Color(.69f,.75f,.69f));
            Gamepad pad=Gamepad.current;
            string closeKeys=pad==null?"Tab / Esc":"Tab / Esc / "+pad.selectButton.displayName+" / "+pad.startButton.displayName;
            string pageKeys=pad==null?"← / →":"← / → / D-pad";
            Text(new Rect(180,650,920,35),(JournalPageCount>1?pageKeys+" · pages       ":"")+closeKeys+" · close memories",caption,new Color(.69f,.75f,.69f));
        }
        void DrawStoryCaption(bool pale,Color ui,string fallback="")
        {
            string line=ActiveStoryLine;bool timed=!string.IsNullOrEmpty(line);if(!timed)line=fallback;if(string.IsNullOrEmpty(line))return;
            float h=Mathf.Clamp(caption.CalcHeight(new GUIContent(line),900)+20,52,108);
            float panelY=LowerReturnView?133:616-h;drawnStoryBounds=new Rect(170,panelY,940,h);
            Color backdrop=pale?new Color(.98f,.98f,.95f,.94f):new Color(.004f,.008f,.006f,.94f);
            SoftPanel(drawnStoryBounds,backdrop);
            Shade(new Rect(drawnStoryBounds.x,panelY+7,drawnStoryBounds.width,h-14),backdrop);
            Text(new Rect(190,panelY+7,900,h-14),line,caption,new Color(ui.r,ui.g,ui.b,timed?Mathf.Clamp01(lineTime):1));
        }
        void CompassLine(Vector2 from,Vector2 to,float width,Color color)
        {
            if(drawingPreviewUI){previewSurface.Line(from,to,width,color);return;}
            Matrix4x4 previous=GUI.matrix;Vector2 delta=to-from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,from);
            Shade(new Rect(from.x,from.y-width*.5f,delta.magnitude,width),color);GUI.matrix=previous;
        }
        void OnGUI()
        {if(previewSurface==null)DrawInterface();}
        void DrawPreviewInterface()
        {drawingPreviewUI=true;try{DrawInterface();}finally{drawingPreviewUI=false;}}
        void DrawInterface()
        {
            drawnStoryBounds=drawnHintBounds=default;
            Styles();Matrix4x4 old=Matrix4x4.identity;
            if(!drawingPreviewUI){float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);old=GUI.matrix;GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);}
            if(Playing&&journalOpen){DrawJournal();if(!drawingPreviewUI)GUI.matrix=old;return;}
            if(title)
            {
                Shade(new Rect(0,0,1280,720),new Color(.004f,.008f,.006f,.64f));
                Text(new Rect(0,145,1280,90),"R E T U R N",heading);
                Text(new Rect(0,240,1280,40),"a journey back to the world",caption);
                Text(new Rect(0,305,1280,70),"Somewhere beyond the dark, someone is still calling.",caption,new Color(.65f,.73f,.7f));
                if(MenuButton(new Rect(519,432,242,54),"Begin the journey",true))StartOpening();
                Text(new Rect(0,514,1280,35),"ENTER TO BEGIN",hintStyle,new Color(.64f,.66f,.57f));
                Text(new Rect(260,576,760,42),"Move · A / D       Jump · Space       Interact · E       Memories · Tab",hintStyle,new Color(.6f,.65f,.61f));
                Text(new Rect(42,655,1100,35),"Follow the voices. Bring the colours home.",small,new Color(.48f,.57f,.52f));
            }
            else if(prologue>=0)
            {
                Shade(new Rect(0,0,1280,720),Color.black);
                string[] lines={"A steady monitor. Air moving through a machine.\nI can't open my eyes.","Doctor: “We did what we could. Now we wait.”","Nurse: “Keep talking to him. He may still hear you.”","Mum: “I'm here. Can you find your way back to us?”"};
                Text(new Rect(230,285,820,150),lines[Mathf.Min(3,sound.PrologueStage(prologue))],caption);
                Text(new Rect(530,650,300,30),"Enter to continue",small);
            }
            else if(Playing)
            {
                bool pale=(Chapter==Movement.StillWater||Chapter==Movement.Reunion||Chapter==Movement.Hospital)&&!journey.RecallSideView;
                Color ui=pale?new Color(.18f,.28f,.25f):new Color(.84f,.88f,.84f);
                if(Chapter!=Movement.Credits&&Chapter!=Movement.Hospital)
                {
                    Text(new Rect(42,30,600,24),"0"+((int)Chapter+1)+"  /  "+ChapterNames[(int)Chapter],small,ui);
                    if(Chapter==Movement.Fragments)
                    {
                        Text(new Rect(42,55,600,30),RouteNames[Excursion],small,ui);
                        for(int i=0;i<4;i++)Shade(new Rect(1150+i*22,36,9,9),i<Fragments?new Color(.96f,.84f,.35f):new Color(.37f,.42f,.40f));
                    }
                    if(BurstUnlocked&&Chapter!=Movement.StillWater&&Chapter!=Movement.Reunion)
                    {Shade(new Rect(42,95,90,2),motor.BurstReady?new Color(.96f,.84f,.35f):new Color(.25f,.29f,.27f));Text(new Rect(42,105,150,25),motor.BurstReady?"SHIFT · BURST":"TOUCH GROUND",small,ui);}
                    Text(new Rect(1060,66,190,25),"TAB · MEMORIES",small,ui);
                    string hint=ContextHint();bool returnNarrative=journey.Active&&Chapter==Movement.Reunion&&!journey.CanDoor;
                    if(!returnNarrative&&!string.IsNullOrEmpty(hint))
                    {drawnHintBounds=new Rect(235,650,810,46);SoftPanel(drawnHintBounds,pale?new Color(.98f,.98f,.95f,.78f):new Color(.008f,.011f,.01f,.76f));Text(new Rect(255,653,770,37),hint,hintStyle,ui);}
                    if(transition<0||nextChapter!=Movement.Fragments)DrawStoryCaption(pale,ui,returnNarrative?hint:"");
                }
                if(!journey.Active&&Chapter==Movement.StillWater&&ColourRecovery>0)
                {
                    Color recovered=ColourRecovery<.34f?new Color(.95f,.80f,.32f):ColourRecovery<.67f?new Color(.82f,.28f,.22f):new Color(.3f,.6f,.8f);
                    string line=ColourRecovery<.34f?"The light you carried.":ColourRecovery<.67f?"The fire you crossed.":"The water that held you.";
                    Text(new Rect(200,395,880,65),line,caption,new Color(.17f,.28f,.25f));
                    Shade(new Rect(0,0,1280,720),new Color(recovered.r,recovered.g,recovered.b,.075f));
                }
                if(Chapter==Movement.Hospital)
                {
                    float eyesCloseAt=journey.Active?ReturnJourney3D.HospitalEyesCloseAt:11f;
                    bool recollection=journey.Active&&chapterTime>=ReturnJourney3D.HospitalRecollectionAt&&chapterTime<eyesCloseAt&&!string.IsNullOrEmpty(ActiveStoryLine);
                    string line=chapterTime<4?"Light. A ceiling. The sound was real.":chapterTime<7?"Nurse: “He moved. Can you hear me?”":chapterTime<11?"Mum: “We're here. You don't have to say anything.”":recollection?ActiveStoryLine:"The voices are here. I am here.";
                    float panelHeight=Mathf.Clamp(caption.CalcHeight(new GUIContent(line),840)+14,70,120);
                    SoftPanel(new Rect(205,630-panelHeight,870,panelHeight),new Color(.98f,.97f,.91f,.82f));
                    Text(new Rect(220,630-panelHeight,840,panelHeight),line,caption,new Color(.18f,.25f,.21f));
                    float opening=Mathf.SmoothStep(0,1,(chapterTime-.55f)/2.65f);
                    float closing=Mathf.SmoothStep(0,1,(chapterTime-eyesCloseAt)/3);
                    DrawEyelids(journey.Active?journey.HospitalEyeClosure:Mathf.Max(1-opening,closing));
                    if(journey.Active){if(chapterTime<.6f)ScreenShade(new Color(1,1,1,1-Mathf.SmoothStep(0,1,chapterTime/.6f)));}
                    else if(chapterTime<2)ScreenShade(new Color(1,1,.96f,1-chapterTime/2));
                }
                if(Chapter==Movement.Credits)
                {
                    Shade(new Rect(0,0,1280,720),new Color(.012f,.018f,.016f));
                    Text(new Rect(0,180,1280,100),"R E T U R N",heading);
                    Text(new Rect(0,295,1280,60),"For the voices that stayed.",caption);
                    Text(new Rect(0,410,1280,80),"Original concept · Ray & the July 14 design discussion\nA playable interpretation of the four handwritten sheets",caption,new Color(.55f,.65f,.6f));
                    if(MenuButton(new Rect(535,565,210,48),"Begin again",true))Begin();
                }
                if(paused)
                {
                    Shade(new Rect(0,0,1280,720),new Color(.015f,.025f,.025f,.9f));
                    Text(new Rect(0,150,1280,80),"A moment to breathe",caption);
                    if(MenuButton(new Rect(515,290,250,46),"Continue",true))paused=false;
                    if(MenuButton(new Rect(515,355,250,46),ReducedMotion?"Reduced motion: on":"Reduced motion: off"))ReducedMotion=!ReducedMotion;
                    if(MenuButton(new Rect(515,420,250,46),"Restart the journey"))Begin();
                    if(MenuButton(new Rect(515,482,250,40),sound.Muted?"Sound: off":"Sound: on"))sound.Muted=!sound.Muted;
                    Text(new Rect(230,540,820,120),journey.Active?"W A S D · move     Right-drag / Q C / right stick · look around\nSpace · jump or rise from the water\nE · interact     R · reform     Tab · collected memories":"A / D or arrows · move     Space · hold for height\nShift · burst (after the first signal)\nE · interact     R · reform     Tab · collected memories",caption);
                }
                if(fade>0)Shade(new Rect(0,0,1280,720),new Color(0,0,0,fade));
                if(retryFlash>0)Shade(new Rect(0,0,1280,720),new Color(.89f,.93f,.88f,retryFlash*.65f));
                if(transition>=0)
                {
                    if(nextChapter==Movement.Fragments)
                    {
                        float tuning=Mathf.Sin(Mathf.Clamp01(transition/3)*Mathf.PI);
                        Shade(new Rect(0,0,1280,720),new Color(.002f,.002f,.002f,tuning*.25f));
                        Vector2 pivot=new Vector2(640,228);Color quiet=new Color(.64f,.72f,.68f,tuning*.5f),gold=new Color(.96f,.84f,.35f,tuning);
                        CompassLine(pivot-Vector2.right*45,pivot+Vector2.right*45,1,quiet);CompassLine(pivot-Vector2.up*45,pivot+Vector2.up*45,1,quiet);
                        float direction=(BranchViewHeading+90)*Mathf.Deg2Rad;Vector2 needle=new Vector2(Mathf.Sin(direction),-Mathf.Cos(direction))*44;
                        CompassLine(pivot,pivot+needle,3,gold);
                        Text(new Rect(200,275,880,80),"T U N I N G   /   "+DirectionNames[transitionFromExcursion]+"  →  "+DirectionNames[nextExcursion],caption,gold);
                        Text(new Rect(200,355,880,65),RouteNames[nextExcursion],caption,new Color(.86f,.88f,.8f,tuning));
                        if(!paused&&!journalOpen)DrawStoryCaption(pale,ui);
                    }
                    else
                    {
                        float progress=Mathf.Clamp01(transition/1.8f),flash=Mathf.Sin(progress*Mathf.PI);
                        if(nextChapter==Movement.Rupture&&progress<.5f)
                            flash=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,.5f,progress));
                        if(!firstStageWhiteout)Shade(new Rect(0,0,1280,720),new Color(.97f,.98f,.92f,flash));
                    }
                }
                if(journey.Active&&Chapter==Movement.Reunion&&journey.DoorWhiteout>0)
                    ScreenShade(new Color(1,1,.96f,journey.DoorWhiteout));
            }
            if(Playing&&!paused&&!journalOpen){float white=Mathf.Max(FirstStageWhiteAlpha,journey.Active?journey.FruitWhiteAlpha:0);if(white>0)Shade(new Rect(0,0,1280,720),new Color(1,1,1,white));}
            if(!drawingPreviewUI)GUI.matrix=old;
        }
        public string ContextHint()
        {
            if(journey.Active)return journey.Hint();
            if(Chapter==Movement.Fragments)
            {
                if(world.RadioPlacementActive)return "Returning this light to the radio.";
                if(world.PickupAnimationActive)return "A small light. Keep it close.";
                if(CanRememberFragment)return "E  ·  Remember this voice";
                if(CanDepositFragment)return "E  ·  Place this memory in the radio";
                if(Carrying&&!ReturnDescent)return "Jump right. The bridge will break; a lower road leads back to the radio.";
                if(Carrying)return "“Stay with me.” Follow the gold threads left to bring this voice home.";
                if(!Carrying&&motor.Grounded&&world.CanCallStationTrain(motor.position))return "E  ·  Send a signal to bring the carriage back";
                if(Excursion==2&&world.StationTrainApproaching&&motor.position.x>42&&motor.position.x<48)return "It heard the signal. Wait here, then jump aboard.";
                if(motor.support!=null&&motor.support.kind==SurfaceKind.Ferry)return Excursion==2?"The carriage leaves when you board. Jump toward the far platform when the gap closes.":"Let the platform carry you. Jump when the shore is near.";
                if(Excursion==1)
                {
                    if(burstLessonStage==3&&burstLessonFeedback>0)return "Burst renewed. You can chain jumps and air bursts.";
                    if(burstLessonStage<3&&motor.position.x<16)
                    {
                        if(motor.Grounded)return "Practice here: Space to rise. Near the top, tap Shift; land to renew your burst.";
                        if(!motor.BurstReady)return "Land to renew your burst.";
                        if(motor.velocity.y>2.5f)return "Hold Space to rise. Tap Shift near the top of the jump.";
                        return "Shift  ·  Burst sideways. Landing renews it.";
                    }
                    return "Hold Space on the springs for height; Shift near the top crosses the long gaps.";
                }
                if(Excursion==2)return "Jump before the underline empties. Shift near the peak follows the gold threads; landing renews it.";
                if(Excursion==3)return "Chain jumps and Shift across the gold threads. Land to renew your burst; watch the stone warning.";
                return "A / D  move     Space  jump · hold for height";
            }
            if(Chapter==Movement.Rupture)return motor.position.x>world.goalX-10?"Space  ·  Hold to leap from the last ledge into white":motor.position.x<13?"Leave the radio · the ground behind you will fall":"Keep moving · ground behind you will not return. Read the falling-stone warnings.";
            if(Chapter==Movement.StillWater)
            {
                if(ColourRecovery>0)return "";
                if(motor.position.x>72&&motor.Grounded)return "E  ·  Take the blue fruit";
                if(Resting)return world.leaf.x<59?"Let the leaf carry you. A / D or Space to rise.":"A / D  ·  Step onto the island";
                if(world.leaf!=null&&Mathf.Abs(motor.position.x-world.leaf.x)<3&&motor.position.y>-2&&motor.position.y<1)return "E  ·  Climb onto the leaf and rest";
                return "W / S or arrows  swim     Space  rise · the green leaf is ahead";
            }
            if(Chapter==Movement.Reunion)return motor.position.x<9?"E  ·  Open the door":"Follow the recovered colours to the door on your left.";
            return "";
        }
        void OnApplicationQuit(){previewActivity?.Dispose();}
        void OnApplicationPause(bool value){sound?.ApplicationPause(value);}
        void OnDestroy(){if(traceMemoryFrames){RemoveMemoryLoopTiming();UnityEngine.Rendering.RenderPipelineManager.beginFrameRendering-=MemoryRenderBegin;UnityEngine.Rendering.RenderPipelineManager.endFrameRendering-=MemoryRenderEnd;}previewActivity?.Dispose();previewCapture?.Dispose();previewSurface?.Dispose();journey?.Dispose();sound?.Dispose();art?.Dispose();foreach(Texture2D texture in uiTextures)Destroy(texture);if(world?.root!=null)Destroy(world.root.gameObject);}
    }
}
