using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Return.Redesign
{
    // Opt-in standalone traversal using the same inputs, motor, collisions, rides and interactions as play.
    public sealed class ReturnAcceptance
    {
        [Serializable] sealed class IslandScaleEvidence {public bool passed;public float islandSpan,leafSpan,ratio,leafNoseZ;}
        bool yellowFrame,redFrame,blueFrame,flatteningFrame,stageHandoffFrame,flatFrame,fragmentsFrame,doorFrame,doorOpeningFrame,uniteFrame,assemblingFrame,depthFrame,footingGroundFrame,footingJumpFrame;
        readonly bool[] radioChargeFrames=new bool[5];
        bool reverseSideView,returnedToVolume,architectureFrame,architectureFlattened,architectureRestored;float reverseStartX,reverseEndX,lastReverseX,solidHouseDepth,paperHouseOpacity;
        readonly ReturnGame game;
        [Serializable] struct FragmentsInputSample
        {
            public float time,clock,move,supportLeft,supportRight,supportY,supportAge;
            public Vector2 position,velocity;
            public int frame,route,support,kind,lesson;
            public bool grounded,jumpPressed,jumpHeld,burstPressed,burstReady;
        }
        [Serializable] struct FragmentsTerrainSample
        {
            public int index,kind;public float left,right,y,age;public bool solid,returnPath;
        }
        [Serializable] sealed class FragmentsRetryTrace
        {
            public string scope="fragments-pre-retry",contactCause;
            public int route,failures;public Vector2 postRetryPosition;
            public FragmentsInputSample[] samples;
            public FragmentsTerrainSample[] terrainAtRetry;
        }
        readonly FragmentsInputSample[] fragmentsInputTrace=new FragmentsInputSample[240];
        int fragmentsTraceCursor,fragmentsTraceCount;
        readonly bool clientJourney=Array.IndexOf(Environment.GetCommandLineArgs(),"--return-review-client-journey")>=0;
        bool clientStartVerified,clientFragmentsComplete,clientEscapeComplete;
        readonly bool fragmentsOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"--return-review-fragments-only")>=0;
        readonly bool escapeOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"--return-review-escape-only")>=0;
        readonly bool travellerOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"--return-review-traveller-only")>=0;
        readonly bool hospitalOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"--return-review-hospital-only")>=0;
        readonly bool streetOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"--return-review-street-only")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"--return-review-hospital-only")>=0;
        bool streetCompleted,hospitalClosedSeen,hospitalOpeningSeen,hospitalOpenSeen,hospitalBlinkOneSeen,hospitalBlinkTwoSeen,hospitalFinalClosedSeen;
        bool hospitalCeilingSeen,hospitalCameraTracking,hospitalNurseOutput,hospitalMumOutput,hospitalStorySeen,hospitalOpenFrame,hospitalFinalClosedFrame;
        bool hospitalOpeningMonotone=true;Vector3 hospitalEyeStart;float priorHospitalClosure=1,hospitalEyeDrift,hospitalOpenSeconds;
        bool landscapeObserved;
        int streetPhase;float streetPauseAt=-1,streetPauseX,streetLastX,streetStartZ,streetStartY,streetRightTravel,streetLeftTravel,streetPauseError,streetPlaneError,streetWhiteSeconds;
        bool meteorContactChecked,fallingMeteorFrame;int streetStepSamples;float streetMinimumStep=float.PositiveInfinity,streetMaximumStep,streetMaximumLateralRatio;
        readonly bool[] streetFootHeld=new bool[2];readonly int[] streetPlantCounts=new int[2];readonly Vector3[] streetPreviousPlant=new Vector3[2],streetPreviousTarget=new Vector3[2];readonly List<StreetStepSample> streetStepTrace=new List<StreetStepSample>();
        readonly bool traceStreetLandings=Array.IndexOf(Environment.GetCommandLineArgs(),"--return-trace-street-landings")>=0;readonly List<StreetTargetFrame> streetTargetTrace=new List<StreetTargetFrame>();
        readonly bool reviewStreetGeometry=Array.IndexOf(Environment.GetCommandLineArgs(),"--return-review-street-geometry")>=0;float nextStreetGeometry,streetSupportKneeSum,streetTorsoLeanSum;readonly StreetGeometryReview streetGeometry=new StreetGeometryReview();
        bool streetTracking,streetWhiteFrame,streetYellowSeen,streetRedSeen,streetBlueSeen,streetConnectorLocked=true,streetSequence=true;
        Vector3 previousStreetChannels;
        bool streetCameraObserved,streetRightFrame,streetLeftFrame;Vector3 streetCameraOffset;float streetCameraError;
        bool travellerVolumeObserved,travellerWalkHidden,travellerSwimHidden,travellerBoardHands,travellerFruitHands;
        readonly float travellerWallStart=Time.realtimeSinceStartup;
        bool rainWhileStationary,redForecastObserved,ejectionObserved,variedMeteorTargets;
        int observedMeteorForecasts,observedMeteorImpacts,observedVolcanoEjections;
        int meteorWarningSamples,clippedMeteorWarningSamples;
        float maximumMeteorWarningOverhang;
        bool unsupportedMeteorWarning;
        float priorMeteorX=float.NaN;
        readonly float[] branchLuminosities=new float[4];
        readonly bool[] radioRevealOutward=new bool[4],radioShakeObserved=new bool[4];
        readonly bool[] pickupHandsObserved=new bool[4],pocketApproachObserved=new bool[4],pocketHiddenObserved=new bool[4],
            placementHandsObserved=new bool[4],placementAtRadioObserved=new bool[4],cleanFigureObserved=new bool[4];
        readonly bool[] returnGreyCoverageObserved=new bool[4];
        readonly int[] returnGreyRenderers=new int[4],returnGoldGuides=new int[4];
        readonly MaterialPropertyBlock returnReceiver=new MaterialPropertyBlock();
        float whiteHoldWallStart=-1,whiteHoldWallEnd,lastRuptureColour=-1;
        int whiteHoldFrames,colourReturnFrames;
        bool ruptureColourStartedGrey,colourReturnMonotonic=true,colourReturnPresented;
        readonly bool[] radioHomeFrames=new bool[4],radioHomeGrounded=new bool[4],forwardReturnBroken=new bool[4];
        readonly Vector2[] radioHomePositions=new Vector2[4];
        readonly float[] returnTravel=new float[4],maximumCarryStep=new float[4],minimumReturnTravel=new float[4];
        int carryRoute=-1,carryFailures;Vector2 carryPosition;
        readonly bool reviewJournal=Array.IndexOf(Environment.GetCommandLineArgs(),"--return-review-memories")>=0;
        int journalReviewStage,journalReviewPages,journalReviewVoices;float journalCloseAt;
        bool journalReviewOwnsOpen;float journalFragmentsPauseAtOpen,journalReturnPauseAtOpen,journalFragmentsPaused,journalReturnPaused;
        [Serializable] sealed class MemoryScreenReview {public bool passed;public int pages,recoveredVoices;public float fragmentCapturePauseSeconds,returnCapturePauseSeconds;}
        ReturnSurface landing,gateShelf;
        ReturnSurface ferryHopTarget;float ferryHopFraction;bool ferryHopLogged;
        bool springForecastLogged,springAimLogged,springLaunchRejectionLogged;
        bool checkpointReplayHoldLogged;
        bool unsafeReplayHoldLogged;
        float simulated,completeTime,returnSimulated,returnWallStart=-1;
        float escapeSimulated,escapeWallStart=-1,escapeWallSeconds;
        bool escapeThreatVisible,openingInFrame,whiteLeapFrame,ruptureFractureFrame,crumblingStayedBroken;
        float whiteJumpGap;
        float fragmentsSimulated,fragmentsWallStart=-1,fragmentsWallSeconds,relayWait;
        int relayRoute=-1,airborneAttemptRoute=-1;
        int memoryPickupRoute=-1,memoryWaitRoute=-1,memoryPickupAttemptRoute=-1,memoryAirborneAttemptRoute=-1;
        float memoryPickupWait;
        readonly bool[] deliberateMemories=new bool[4];
        readonly float[] memoryWaitSeconds=new float[4];
        bool memoryProbeStarted,memoryAirborneRejected,memoryPromptFrame;
        ReturnSurface longGapDeparture;
        float longGapDepartureRight;
        int longGapRoute=-1;
        bool longGapBurst,burstCrossingFrame,burstChainFrame;
        bool returnBridgeFrame;
        bool cityFrame,cityRecoveryFrame,cityPassStarted,cityRecoveryStarted,cityRecoveryCompleted,cityInputIgnored,hospitalNurseFramed,hospitalReachFramed,lowerBridgeReadable,lowerCircuitReadable;int hospitalClearSamples;float cityPassTravel,hospitalHandStartDistance=-1,hospitalHandAdvance;bool hospitalReachStartFrame,hospitalHandAdvanceObserved;Vector3 cityLastCamera;
        float cityLastElapsed,cityCornerMinimumSpeed=float.MaxValue;int cityCornerSamples;
        [Serializable] class CityCameraPassReview {public bool passed;public float seconds,minimumCornerSpeed;public int cornerSamples;}
        int hospitalFaceSamples;float hospitalMinimumFaceGap=10000;
        bool hospitalFamilyFramed;
        readonly float[] hospitalFaceAt={10.4f,11.5f,12.2f,16,18.2f},hospitalClearAt={6,9,10.4f,12,18};
        readonly int[] longBurstCrossings=new int[4];
        readonly bool[] airborneRejected=new bool[4],transmissionFrames=new bool[3],transmissionsArrived=new bool[3];
        bool finalReturnFrame,fadingFootingFrame,collapseFootingFrame;
        readonly bool[] memoryVoiceOutput=new bool[4],memoryPresenceFrames=new bool[4],memoryVoiceMovement=new bool[4];
        readonly ReturnVoiceOutputMeter.PositionWindow[] memoryVoiceWindows=new ReturnVoiceOutputMeter.PositionWindow[4];
        [Serializable] sealed class VoicePositionWindows {public ReturnVoiceOutputMeter.PositionWindow[] memories;}
        readonly float[] memoryVoiceTravel=new float[4],memoryCaptionSeconds=new float[4];
        readonly bool[] placeCaptionFrames=new bool[4];
        readonly int[] memoryVoiceAudioSamples=new int[4];
        readonly bool[] memoryCaptionStarted=new bool[4],memoryCaptionFull=new bool[4],branchGestureClear=new bool[3];
        int memoryCaptionRoute=-1;
        float memoryCaptionStartedAt;
        string rememberedCaption;
        bool memoryHandoffFrame;
        readonly float[] connectionCaptionSeconds=new float[3];
        readonly bool[] connectionCaptionFrames=new bool[3];
        readonly bool[] branchTurnFrames=new bool[3],branchTurnComplete=new bool[3];
        readonly float[] branchTurnDegrees=new float[3];
        int burstPracticeStep;
        bool burstPracticeBurstFrame,burstPracticeRenewFrame;
        float burstPracticeStartX,burstPracticeTravel;
        readonly bool reviewBurstBuffer=Array.IndexOf(Environment.GetCommandLineArgs(),"--return-review-burst-buffer")>=0;
        int burstBufferCase,burstBufferStep;
        bool burstBufferDone,burstBufferReset;
        float burstBufferCaseAt,burstBufferQueuedAt,burstBufferLandedAt=-1,burstBufferStartedAt=-1,burstBufferResetAt;
        [Serializable] sealed class BurstBufferCase {public string name;public bool passed,queuedAirborne,landedBeforeStart,reset;public int startsAfterPress;public float queueToLandingSeconds=-1,queueToStartSeconds=-1;}
        [Serializable] sealed class BurstBufferReview {public bool passed;public BurstBufferCase[] samples;}
        BurstBufferCase burstBufferSample;
        readonly List<BurstBufferCase> burstBufferSamples=new List<BurstBufferCase>();
        readonly bool reviewAirBrake=Array.IndexOf(Environment.GetCommandLineArgs(),"--return-review-air-brake")>=0;
        int airBrakeCase,airBrakeStep;
        bool airBrakeDone;
        float airBrakeAt,airBrakeStartX,airBrakeStartY,airBrakeFarthestX;
        [Serializable] sealed class AirBrakeCase {public string name;public bool passed,airborne;public float initialSpeed,seconds,forwardDrift,finalSpeed;}
        [Serializable] sealed class AirBrakeReview {public bool passed;public AirBrakeCase[] samples;}
        AirBrakeCase airBrakeSample;
        readonly List<AirBrakeCase> airBrakeSamples=new List<AirBrakeCase>();
        readonly bool reviewLowerEntry=Array.IndexOf(Environment.GetCommandLineArgs(),"--return-review-lower-entry")>=0;
        bool lowerEntryStarted,lowerEntryDone,lowerEntryWalkOff;
        float lowerEntryAt,lowerEntryLandedAt=-1,lowerEntryX;
        ReturnSurface lowerEntryTarget;
        Transform lowerEntryRoot;
        readonly List<ReturnSurface> lowerEntryUpperSurfaces=new List<ReturnSurface>();
        readonly List<Transform> lowerEntryUpperRoots=new List<Transform>(),lowerEntryMemoryRoots=new List<Transform>(),lowerEntryGuideRoots=new List<Transform>();
        [Serializable] sealed class LowerEntryReview
        {
            public bool passed,walkedOff,supportTriggered;
            public float entryX,formerPositionGateX,settledSeconds,targetLeft,targetRight;
            public int upperSurfaces,solidUpperSurfaces,registeredUpperRenderers,activeUpperRenderers,memoryRoots,registeredMemoryRenderers,activeMemoryRenderers,guideRoots,registeredGuideRenderers,activeGuideRenderers;
        }
        int previousFailures;
        bool wasCarrying;
        bool awakeFrame,touchFrame,sittingFrame,recliningFrame,risingFrame,hospitalRecollectionFrame,hospitalOpeningFrame,hospitalClosingFrame,hospitalReassuranceFrame,hospitalReliefBlinkFrame,hospitalMotherTurnFrame,hospitalOfferFrame;
        float hospitalRecollectionSeconds,hospitalRecollectionWallStart=-1,hospitalRecollectionWallSeconds;
        bool retryChecked;
        bool ruptureWaiting,ruptureRetryChecked,rupturePersistent,ruptureStoryChecked,chaseFrame,collapseFrame,openingFrame;
        bool ruptureCheckpointReached,ruptureMidpointRetryChecked;
        int ruptureStartCheckpointSamples,ruptureMidpointCheckpointSamples;
        float ruptureStartRetryDistance,ruptureMidpointRetryDistance;
        Vector2 lastRupturePosition,ruptureMidpointCheckpoint;
        bool openingRainProbeComplete;
        float openingRainAt=-1,openingRainSeconds,openingRainDrift;Vector2 openingRainPosition;
        int openingRainForecastStart,openingRainImpactStart,openingRainForecasts,openingRainImpacts,openingRainFrames;
        readonly bool[] ruptureVoiceOutput=new bool[5];
        bool ruptureSpeechStoppedOnWater;
        int ruptureWaitFailures;
        float firstCollapsedAt=-1;
        bool rested3D,orbitChecked,orbitFrame,islandFrame,reunionFrame;float furthestZ,furthestX,maxOrbit;
        bool waterEntered,waterSwimFrame,waterHandStrokeFrame,leafReadySeen,leafReadyFrame,leafDriftFrame,islandApproachFrame;
        readonly bool[] swimCycleFrames=new bool[3];readonly float[] swimCyclePhases=new float[3],swimCameraDistances=new float[3],swimFootSpeeds=new float[3],swimPlanarSpeeds=new float[3];readonly Vector4[] swimCycleShapes=new Vector4[3],swimPostures=new Vector4[3];
        readonly ReturnPerson3D.SwimFootReport[] swimFootPoses=new ReturnPerson3D.SwimFootReport[3];
        [Serializable] sealed class SwimFeetReview {public ReturnPerson3D.SwimFootReport[] samples;}
        float swimReviewSeconds;
        bool warmCupFrame,stationApproachFrame,stationRideFrame;
        bool islandWalkTracking,islandWalkFrame;Vector3 islandWalkPrevious;float islandWalkSeconds,islandWalkDistance,islandWalkMaxSpeed;
        readonly List<ReturnPerson3D.GroundPoseReport> islandSupportPoses=new List<ReturnPerson3D.GroundPoseReport>();float nextIslandSupportProbe;
        [Serializable] class IslandWeightTransferReview {public bool passed;public ReturnPerson3D.GroundPoseReport[] samples;}
        [Serializable] class IslandSupportDiagnostic {public string label;public bool rejected,shoePenetration,weightBiasFailed,counterRotationFailed,chestTurnTooSmall;public float supportLocalX,biasProduct,requiredBiasProduct,chestRelativeYaw;public ReturnPerson3D.GroundPoseReport pose;}
        bool boardingRootReady,boardingSeen,boardingStartFrame,boardingBraceFrame,boardingSettledFrame;float boardingSeconds,boardingMaximumTravel,boardingMaximumAngle;Vector3 boardingPreviousRoot,boardingPreviousFocus;Quaternion boardingPreviousRotation;Vector2 boardingHandGaps,boardingShoeGaps;
        [Serializable] class LeafBoardingReview {public bool passed,startFrame,braceFrame,settledFrame;public float seconds,maximumRootOrBodyStep,maximumRootAngle;public Vector2 handClearance,shoeClearance;}
        bool shoreTracking,shoreLeafFrame,shoreArrivalFrame;float shoreLeafSeconds,shoreIslandSeconds,shoreMaximumSpeed,shoreMaximumTravel,shoreSupportGapSeconds;Vector3 shorePreviousPosition,shorePreviousRoot,shorePreviousFocus;Vector2 shoreArrivalShoeGaps;
        [Serializable] class ShoreWalkReview {public bool passed,leafFrame,arrivalFrame;public float leafSeconds,islandSeconds,maximumSpeed,maximumRootOrBodyStep,supportGapSeconds;public Vector2 arrivalShoeClearance;}
        [Serializable] class ShoreWalkFailure {public float dt,speed,rootStep,bodyStep,maximumSpeed,maximumRootOrBodyStep;public bool leaf,island,crossingGap;public Vector3 previousPosition,position,velocity,previousRoot,root,previousBody,body;public ReturnPerson3D.GroundPoseReport pose;}
        bool ReviewShoreWalk(ReturnJourney3D j,float dt){
            if(!j.Active||j.Scene!=Movement.StillWater||j.LeafZ<ReturnVolumeArt.LeafDockZ-.002f||j.Resting||j.RestPoseAmount>0||j.FruitActionTime>=0){shoreTracking=false;return true;}
            bool leaf=j.OnLeafSurface,island=j.OnIsland;float moving=new Vector2(j.Velocity.x,j.Velocity.z).magnitude;
            bool crossingGap=!leaf&&!island&&shoreLeafSeconds>0&&!shoreArrivalFrame&&j.Position.y>-.25f;
            if((!leaf&&!island&&!crossingGap)||moving<=.1f){shoreTracking=false;return true;}
            if(crossingGap)shoreSupportGapSeconds+=dt;
            Vector3 root=j.CharacterRootPosition,focus=j.CharacterBodyFocus;
            if(shoreTracking&&dt>0){
                Vector3 delta=j.Position-shorePreviousPosition;float speed=new Vector2(delta.x,delta.z).magnitude/dt,rootStep=Vector3.Distance(root,shorePreviousRoot),bodyStep=Vector3.Distance(focus,shorePreviousFocus);
                shoreMaximumSpeed=Mathf.Max(shoreMaximumSpeed,speed);shoreMaximumTravel=Mathf.Max(shoreMaximumTravel,rootStep,bodyStep);
                if(shoreMaximumSpeed>1.55f||shoreMaximumTravel>.15f){
                    string directory=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(directory))directory=Path.Combine(Application.persistentDataPath,"ReturnEvidence");Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory,"shore-walk-failure.json"),JsonUtility.ToJson(new ShoreWalkFailure{dt=dt,speed=speed,rootStep=rootStep,bodyStep=bodyStep,maximumSpeed=shoreMaximumSpeed,maximumRootOrBodyStep=shoreMaximumTravel,leaf=leaf,island=island,crossingGap=crossingGap,previousPosition=shorePreviousPosition,position=j.Position,velocity=j.Velocity,previousRoot=shorePreviousRoot,root=root,previousBody=shorePreviousFocus,body=focus,pose=j.DescribeFruitGroundPose("Shore walking failure")},true));
                    Finish(false,"Leaf-to-island walking surged or jumped");return false;
                }
            }
            if(leaf){shoreLeafSeconds+=dt;if(shoreLeafSeconds>=.15f&&!shoreLeafFrame)shoreLeafFrame=game.TryRequestFrame("Leaf-Approach-Shore");}
            if(island&&shoreLeafFrame){shoreIslandSeconds+=dt;if(shoreIslandSeconds>=.15f&&!shoreArrivalFrame){shoreArrivalShoeGaps=j.ShoeClearance();if(shoreArrivalShoeGaps.x<-.002f||shoreArrivalShoeGaps.y<-.002f){Finish(false,"Island arrival shoes penetrate the shore");return false;}shoreArrivalFrame=game.TryRequestFrame("Island-Arrival");}}
            shoreTracking=true;shorePreviousPosition=j.Position;shorePreviousRoot=root;shorePreviousFocus=focus;return true;
        }
        bool ReviewLeafBoarding(ReturnJourney3D j,float dt){
            if(!j.Active||j.Scene!=Movement.StillWater){boardingRootReady=false;return true;}
            Vector3 root=j.CharacterRootPosition,focus=j.CharacterBodyFocus;Quaternion rotation=j.CharacterRootRotation;
            if(j.LeafBoarding||(boardingSeen&&!boardingSettledFrame)){
                if(boardingRootReady){boardingMaximumTravel=Mathf.Max(boardingMaximumTravel,Vector3.Distance(root,boardingPreviousRoot),Vector3.Distance(focus,boardingPreviousFocus));boardingMaximumAngle=Mathf.Max(boardingMaximumAngle,Quaternion.Angle(rotation,boardingPreviousRotation));if(boardingMaximumTravel>.15f||boardingMaximumAngle>8){Finish(false,"Swimming-to-leaf body handoff jumped");return false;}}
                if(j.LeafBoarding){
                    boardingSeen=true;boardingSeconds+=dt;float u=j.LeafBoardingProgress;
                    if(u>.04f&&!boardingStartFrame)boardingStartFrame=game.TryRequestFrame("Leaf-Boarding-Start");
                    if(u>.14f&&u<.18f&&!boardingBraceFrame){boardingHandGaps=j.BoardingHandClearance();if(boardingHandGaps.x<-.002f||boardingHandGaps.y<-.002f||boardingHandGaps.x>.04f||boardingHandGaps.y>.04f){Finish(false,"Boarding hands are not supported above the leaf");return false;}boardingBraceFrame=game.TryRequestFrame("Leaf-Brace");}
                }else if(j.LeafBoardingProgress>=1&&!boardingSettledFrame){boardingShoeGaps=j.ShoeClearance();if(boardingShoeGaps.x<-.002f||boardingShoeGaps.y<-.002f||boardingShoeGaps.x>.02f||boardingShoeGaps.y>.02f){Finish(false,"Boarding shoes did not settle onto the leaf");return false;}boardingSettledFrame=game.TryRequestFrame("Leaf-Boarded");}
            }
            boardingRootReady=true;boardingPreviousRoot=root;boardingPreviousFocus=focus;boardingPreviousRotation=rotation;return true;
        }
        bool ReviewIslandWeightTransfer(ReturnJourney3D j){
            if(islandSupportPoses.Count==2||j.SceneElapsed<nextIslandSupportProbe)return true;
            nextIslandSupportProbe=j.SceneElapsed+.07f;var pose=j.DescribeFruitGroundPose("Island support");
            if(pose.airborne||!pose.groundSurfaceProvided||pose.walkWeight<.99f||Mathf.Abs(pose.pelvisYaw)<.5f)return true;
            int support=-1;for(int leg=0;leg<2;leg++)if(pose.feet[leg].minimumShoeGap<=.014f&&pose.feet[1-leg].minimumShoeGap>.025f)support=leg;
            if(support<0)return true;
            string label=support==0?"Island-Support-Right":"Island-Support-Left";if(islandSupportPoses.Exists(p=>p.label==label))return true;
            float supportSide=(Quaternion.Inverse(pose.rootRotation)*(pose.feet[support].actual-pose.rootPosition)).x;
            pose.label=label;
            float chestRelativeYaw=Mathf.DeltaAngle(pose.pelvisYaw,pose.chestYaw);
            bool shoePenetration=pose.feet[support].minimumShoeGap<-.002f,weightBiasFailed=pose.pelvisOffsetLocal.x*supportSide<=.0007f,counterRotationFailed=pose.pelvisYaw*chestRelativeYaw>=0,chestTurnTooSmall=Mathf.Abs(chestRelativeYaw)<.2f;
            bool rejected=shoePenetration||weightBiasFailed||counterRotationFailed||chestTurnTooSmall;
            string directory=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");
            if(!string.IsNullOrEmpty(directory))File.WriteAllText(Path.Combine(directory,"island-support-diagnostic.json"),JsonUtility.ToJson(new IslandSupportDiagnostic{label=label,rejected=rejected,shoePenetration=shoePenetration,weightBiasFailed=weightBiasFailed,counterRotationFailed=counterRotationFailed,chestTurnTooSmall=chestTurnTooSmall,supportLocalX=supportSide,biasProduct=pose.pelvisOffsetLocal.x*supportSide,requiredBiasProduct=.0007f,chestRelativeYaw=chestRelativeYaw,pose=pose},true));
            if(rejected){Finish(false,"Island body weight or torso rotation does not follow the planted foot at "+label);return false;}
            if(game.TryRequestFrame(label)){pose.label=label;islandSupportPoses.Add(pose);}return true;
        }
        bool waterBodyMotionObserved,leafWaterMotionObserved,leafIdleMotionQuiet;
        bool fruitTasteDelayed,fruitBiteFrame,fruitScalePlausible,fruitTreeTouchObserved,fruitPickFrame,fruitAnticipationFrame,fruitTasteFrame;float fruitHeldSeconds,fruitMouthSeconds;
        readonly System.Collections.Generic.List<ReturnPerson3D.GroundPoseReport> fruitGroundPoses=new System.Collections.Generic.List<ReturnPerson3D.GroundPoseReport>();
        bool RecordFruitGroundPose(string label){
         if(fruitGroundPoses.Exists(p=>p.label==label))return true;var pose=game.journey.DescribeFruitGroundPose(label);fruitGroundPoses.Add(pose);string directory=System.Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(directory))System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"fruit-ground-poses.json"),JsonUtility.ToJson(new FruitGroundPoseReport{samples=fruitGroundPoses.ToArray()},true));
         bool natural=!pose.airborne&&pose.groundSurfaceProvided&&pose.walkWeight<.001f&&pose.feet.Length==2&&Mathf.Abs(pose.feet[0].actualSurface-pose.feet[1].actualSurface)<=.025f&&Mathf.Abs(pose.feet[0].kneeFlexDegrees-pose.feet[1].kneeFlexDegrees)<=12;
         foreach(var foot in pose.feet)natural&=!float.IsNaN(foot.minimumShoeGap)&&!float.IsInfinity(foot.minimumShoeGap)&&foot.minimumShoeGap>=-.002f&&foot.minimumShoeGap<=.02f&&foot.targetError<=.002f&&foot.kneeFlexDegrees<=40;
         if(!natural){Finish(false,"Fruit standing pose remains deeply bent or unsupported at "+label);return false;}return true;
        }
        [System.Serializable] public sealed class FruitGroundPoseReport {public ReturnPerson3D.GroundPoseReport[] samples;}
        bool cameraSampleReady,cameraWasFruit,fruitCameraContinuous,returnCameraContinuous,returnHandoffFrame,returnPullbackFrame;Movement cameraScene;Vector3 cameraLastPosition;Quaternion cameraLastRotation;float cameraLastFov;
        ReturnJourney3D.GroundSkinComparison savedSkinLeaf,savedSkinIsland;float skinSavedAt=-1;
        [Serializable] sealed class GroundSkinObservation {public bool passed;public string detail;public int leafFrames,islandFrames;}
        float fruitCameraAngleDegrees,fruitCameraLensDegrees,fruitCameraTravel,returnCameraAngleDegrees,returnCameraLensDegrees,returnCameraTravel;
        float waterEnteredZ,waterSwimDistance,leafReadyZ,leafIdleSeconds,leafIdleAdvance,leafRestCaptionSeconds,leafDriftDistance;
        readonly Dictionary<string,float> splits=new Dictionary<string,float>();
        string last;
        bool framePending;
        public bool Finished {get;private set;}
        public bool CityCornerFrameRequired=>game.journey.Active&&game.journey.Scene==Movement.Reunion&&game.journey.SceneElapsed>=29.3f&&game.journey.SceneElapsed<=29.7f;
        public bool JourneyFrameRequired {
            get {
                var j=game.journey;if(!j.Active)return false;
                if(j.Scene==Movement.StillWater)return j.FruitActionTime>=.88f&&j.FruitActionTime<=1.08f;
                if(j.Scene!=Movement.Reunion)return false;float t=j.SceneElapsed;
                return CityCornerFrameRequired||(t>=9.6f&&t<=9.95f)||(t>=23.15f&&t<=23.5f)||(t>=ReturnReverseJourney.CityPassAt&&t<=ReturnReverseJourney.CityPassAt+.2f);
            }
        }
        public ReturnAcceptance(ReturnGame g){game=g;if(clientJourney){fragmentsOnly=true;escapeOnly=true;hospitalOnly=true;streetOnly=true;}}
        public void ReviewJournalFrame()
        {
            if(!reviewJournal||Finished)return;
            if(game.PauseMenuOpen||game.JournalOpen&&!journalReviewOwnsOpen){Finish(false,"Unexpected pause outside the memory-screen capture exercise");return;}
            if(journalReviewStage>=3)return;
            if(journalCloseAt>0)
            {
                if(Time.realtimeSinceStartup>=journalCloseAt)
                {
                    if(!game.JournalOpen){Finish(false,"Memory screen closed before its capture completed");return;}
                    journalFragmentsPaused+=game.FragmentsReviewPausedSeconds-journalFragmentsPauseAtOpen;
                    journalReturnPaused+=game.MotionReviewPausedSeconds-journalReturnPauseAtOpen;
                    game.BackFromOverlay();
                    if(game.JournalOpen||game.PauseMenuOpen){Finish(false,"Back from memories did not return to the journey");return;}
                    journalReviewOwnsOpen=false;journalCloseAt=0;journalReviewStage++;
                    if(journalReviewStage==3)
                    {
                        string directory=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");
                        if(!string.IsNullOrEmpty(directory))File.WriteAllText(Path.Combine(directory,"memory-screen-review.json"),JsonUtility.ToJson(new MemoryScreenReview{passed=true,pages=journalReviewPages,recoveredVoices=journalReviewVoices,fragmentCapturePauseSeconds=journalFragmentsPaused,returnCapturePauseSeconds=journalReturnPaused},true));
                    }
                }
                return;
            }
            if(game.InTransition)return;
            bool ready=journalReviewStage==0?game.Chapter==Movement.Fragments&&game.RecoveredVoices==0&&game.ChapterElapsed>1.2f:
                journalReviewStage==1?game.Chapter==Movement.StillWater&&game.RecoveredVoices==4&&game.ChapterElapsed>1.2f:
                game.Chapter==Movement.Hospital&&game.ChapterElapsed>13&&game.journal.Exists(entry=>entry.Contains("coma"));
            if(!ready)return;
            if(!game.JournalOpen)
            {
                game.ToggleJournal();journalReviewOwnsOpen=true;
                journalFragmentsPauseAtOpen=game.FragmentsReviewPausedSeconds;journalReturnPauseAtOpen=game.MotionReviewPausedSeconds;
                if(journalReviewStage==2)for(int i=1;i<game.JournalPageCount;i++)game.MoveJournalPage(1);
            }
            string frame=journalReviewStage==0?"Memories-Unclear":journalReviewStage==1?"Memories-Four-Voices":"Memories-Journey";
            if(game.TryRequestFrame(frame))
            {
                journalCloseAt=Time.realtimeSinceStartup+.4f;
                if(journalReviewStage==2){journalReviewPages=game.JournalPageCount;journalReviewVoices=game.RecoveredVoices;}
            }
        }
        public ReturnInput Input()
        {
            ReturnInput input=ChooseInput();
            if(!Finished&&game.FragmentsReviewRequested&&game.Chapter==Movement.Fragments&&!game.InTransition)
            {
                ReturnMotor motor=game.motor;ReturnSurface support=motor.support;
                fragmentsInputTrace[fragmentsTraceCursor]=new FragmentsInputSample{
                    time=simulated,clock=game.world.clock,frame=Time.frameCount,route=game.Excursion,lesson=game.BurstLessonStage,
                    position=motor.position,velocity=motor.velocity,grounded=motor.Grounded,burstReady=motor.BurstReady,
                    support=game.world.surfaces.IndexOf(support),kind=support==null?-1:(int)support.kind,
                    supportLeft=support?.Left??0,supportRight=support?.Right??0,supportY=support?.y??0,supportAge=support?.age??-1,
                    move=input.move,jumpPressed=input.jumpPressed,jumpHeld=input.jumpHeld,burstPressed=input.burstPressed};
                fragmentsTraceCursor=(fragmentsTraceCursor+1)%fragmentsInputTrace.Length;
                fragmentsTraceCount=Mathf.Min(fragmentsTraceCount+1,fragmentsInputTrace.Length);
            }
            return input;
        }
        void WriteFragmentsRetryTrace()
        {
            try
            {
                var trace=new FragmentsRetryTrace{route=game.Excursion,failures=game.Failures,postRetryPosition=game.motor.position,
                    contactCause=game.world.LastContactCause,samples=new FragmentsInputSample[fragmentsTraceCount],
                    terrainAtRetry=new FragmentsTerrainSample[game.world.surfaces.Count]};
                int start=(fragmentsTraceCursor-fragmentsTraceCount+fragmentsInputTrace.Length)%fragmentsInputTrace.Length;
                for(int i=0;i<fragmentsTraceCount;i++)trace.samples[i]=fragmentsInputTrace[(start+i)%fragmentsInputTrace.Length];
                for(int i=0;i<trace.terrainAtRetry.Length;i++)
                {ReturnSurface s=game.world.surfaces[i];trace.terrainAtRetry[i]=new FragmentsTerrainSample{index=i,kind=(int)s.kind,left=s.Left,right=s.Right,y=s.y,age=s.age,solid=s.solid,returnPath=s.returnPath};}
                string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");
                if(string.IsNullOrEmpty(output))output=Path.Combine(Application.persistentDataPath,"ReturnEvidence");
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"fragments-retry-trace.json"),JsonUtility.ToJson(trace,true));
            }
            catch(Exception error){Debug.LogWarning("[RETURN retry trace] "+error.Message);}
        }
        ReturnInput ChooseInput()
        {
            if(clientJourney&&simulated<.1f)clientStartVerified|=game.Chapter==Movement.Fragments&&game.Fragments==0&&game.Excursion==0&&!game.journey.Active;
            if(escapeOnly&&game.Chapter==Movement.Rupture&&!openingRainProbeComplete)
            {
                if(game.InTransition||clientJourney&&!clientFragmentsComplete||!game.motor.Grounded)return new ReturnInput();
                if(openingRainAt<0){openingRainAt=game.world.clock;openingRainPosition=game.motor.position;openingRainForecastStart=game.world.MeteorForecasts;openingRainImpactStart=game.world.MeteorImpacts;}
                if(game.world.clock-openingRainAt<4.5f)return new ReturnInput();
                openingRainProbeComplete=true;
                if(!rainWhileStationary){Finish(false,"Visible-entry stationary hold did not observe fresh forecast and impact rain");return new ReturnInput();}
            }
            if(fragmentsOnly&&!clientJourney&&game.Chapter==Movement.Rupture)return new ReturnInput();
            ReturnInput input=new ReturnInput{jumpHeld=true};
            ReturnMotor m=game.motor;ReturnWorld w=game.world;
            if(game.Chapter==Movement.Hospital||game.Chapter==Movement.Credits)return input;
            if(game.Chapter==Movement.StillWater)
            {
                if(game.ColourRecovery>0)return new ReturnInput();
                var j=game.journey;
                if(!rested3D)
                {
                 if(j.Resting)rested3D=true;
                 else if(waterEntered)
                 {
                  if(game.MotionReviewActive&&(swimReviewSeconds<8.5f||!System.Array.TrueForAll(swimCycleFrames,captured=>captured)))
                  {
                   Vector3 reviewPoint=new Vector3(-32,0,waterEnteredZ+6);
                   Vector2 stroke=j.DirectionTo(reviewPoint);input.move=stroke.x;input.vertical=stroke.y;
                  }
                  else if(j.CanRest)input.interact=leafIdleSeconds>=.75f;
                  else{Vector2 approach=j.DirectionTo(new Vector3(0,0,j.LeafZ));input.move=approach.x;input.vertical=approach.y;}
                 }
                 return input;
                }
                if(j.Resting&&j.LeafZ<ReturnVolumeArt.LeafDockZ-.05f)
                {if(j.RestPoseAmount<1)return input;if(!orbitChecked){input.look=new Vector2(j.Yaw<60?3:-3,0);if(j.Yaw>=60)orbitChecked=true;}else if(j.Yaw>0)input.look=new Vector2(-Mathf.Min(3,j.Yaw),0);return input;}
                Vector2 d=j.DirectionTo(new Vector3(1,0,26));input.move=d.x;input.vertical=d.y;input.interact=j.CanFruit;return input;
            }
             if(game.Chapter==Movement.Reunion){
                  if(streetOnly)return StreetReviewInput();
                 var j=game.journey;
                 if(j.SceneElapsed<ReturnReverseJourney.DoorAt){input.move=1;input.vertical=-1;input.interact=true;}
                 else {
                     if(game.MotionReviewActive&&j.CanDoor&&!doorFrame){
                         doorFrame=game.TryRequestFrame("Return-Island-Doorway");
                          return input;
                     }
                     input.interact=j.CanDoor;
                 }
                 return input;
             }
            if(game.InTransition)return new ReturnInput();
            if(game.FragmentsReviewActive&&game.Excursion==1&&!game.Carrying&&burstPracticeStep<3)
            {
                if(burstPracticeStep==0)
                {if(m.Grounded){burstPracticeStartX=m.position.x;burstPracticeStep=1;input.jumpPressed=true;}return input;}
                if(burstPracticeStep==1)
                {if(!m.Grounded&&m.velocity.y<=2.5f&&m.position.y>w.surfaces[0].y+2){input.move=1;input.burstPressed=true;bursts++;burstPracticeStep=2;}return input;}
                if(m.Grounded&&m.BurstReady)burstPracticeStep=3;
                return input;
            }
            if(reviewBurstBuffer&&game.FragmentsReviewActive&&game.Excursion==1&&!game.Carrying&&burstPracticeStep==3&&burstPracticeRenewFrame&&!burstBufferDone)
                return BurstBufferInput();
            if(reviewAirBrake&&game.FragmentsReviewActive&&game.Excursion==1&&!game.Carrying&&burstPracticeStep==3&&burstPracticeRenewFrame&&!airBrakeDone)
                return AirBrakeInput();
            if(game.Chapter==Movement.Fragments&&!game.Carrying)
            {
                if(game.Excursion==2&&m.Grounded)
                {
                    if(w.CanCallStationTrain(m.position)){input.interact=true;input.move=0;input.jumpHeld=false;return input;}
                    if(w.StationTrainApproaching&&m.position.x>=43&&m.position.x<=47){input.move=0;input.jumpHeld=false;return input;}
                }
                if(game.FragmentsReviewActive&&game.Excursion==0&&memoryProbeStarted&&!m.Grounded)
                {
                    input.jumpHeld=false;
                    if(!memoryAirborneRejected){input.interact=true;memoryAirborneAttemptRoute=0;}
                    return input;
                }
                if(game.CanRememberFragment)
                {
                    if(game.FragmentsReviewActive&&game.Excursion==0&&!memoryProbeStarted)
                    {memoryProbeStarted=true;input.jumpPressed=true;return input;}
                    if(memoryPickupRoute!=game.Excursion){memoryPickupRoute=game.Excursion;memoryPickupWait=0;}
                    memoryPickupWait+=1f/60;input.jumpHeld=false;
                    if(memoryPickupWait<.6f){memoryWaitRoute=game.Excursion;return input;}
                    input.interact=true;memoryPickupAttemptRoute=game.Excursion;return input;
                }
            }
            if(reviewLowerEntry&&game.FragmentsReviewActive&&game.Excursion==3&&game.Carrying&&!lowerEntryDone)
                return LowerEntryInput();
            if(game.CanDepositFragment)
            {
                if(relayRoute!=game.Excursion){relayRoute=game.Excursion;relayWait=0;}
                int route=game.Excursion;relayWait+=1f/60;input.move=0;input.jumpHeld=false;
                radioHomePositions[route]=m.position;radioHomeGrounded[route]=m.Grounded&&m.support==w.surfaces[0];
                minimumReturnTravel[route]=(w.returnEntry.x-w.radioX)*.6f;forwardReturnBroken[route]=w.ForwardReturnCollapsed;
                input.interact=relayWait>=.7f;return input;
            }
            if(game.Chapter==Movement.Fragments&&game.Carrying&&!m.Grounded&&m.velocity.y>2&&m.position.x>w.goalX+3&&m.position.y>w.returnEntry.y+2&&!airborneRejected[game.Excursion])
            {input.interact=true;airborneAttemptRoute=game.Excursion;}
            // Stand clear of the marked debris and let the real pursuit exercise failure/retry.
            if(game.Chapter==Movement.Rupture&&!ruptureRetryChecked&&m.Grounded&&m.support.kind==SurfaceKind.Stone&&Mathf.Abs(m.support.Left-44)<.1f&&m.position.x>44&&m.position.x<m.support.Right-.75f&&(ruptureWaiting||PursuitStandClear(m,w)))
            {
                if(!ruptureWaiting){ruptureWaiting=true;ruptureWaitFailures=game.Failures;Debug.Log("[RETURN acceptance] Pursuit wait at "+m.position);}
                return new ReturnInput();
            }
            int direction=game.Chapter==Movement.Fragments&&game.Carrying&&game.ReturnDescent?-1:1;
            float target=direction<0?6:game.Carrying?w.returnEntry.x-1:w.goalX;
            if(game.Chapter==Movement.Rupture&&m.Grounded&&m.support==w.escapeLedge&&m.position.x>w.escapeLedge.Right-.75f){input.jumpPressed=true;input.move=1;landing=null;return input;}
            input.move=direction;
            if(!w.returnOpen)foreach(ReturnHazard h in w.hazards)
            {
                float forecastDistance=h.x-m.position.x;
                bool timedReplaySupport=game.Chapter==Movement.Rupture&&ruptureMidpointRetryChecked&&m.Grounded&&m.support.kind==SurfaceKind.Collapse;
                float holdDistance=timedReplaySupport?ReturnWorld.DebrisImpactRadius+.02f+Mathf.Max(0,m.velocity.x)*Mathf.Max(0,m.velocity.x)/(2*ReturnMotor.GroundBrake):game.Chapter==Movement.Rupture?ReturnWorld.DebrisImpactRadius+.25f:1.3f;
                bool stableSupport=m.Grounded&&(m.support.kind==SurfaceKind.Stone||game.Chapter==Movement.Rupture&&m.support==w.surfaces[0]||timedReplaySupport);
                if(h.debris&&h.timer>=0&&h.timer<ReturnWorld.DebrisImpactTime+.24f&&forecastDistance>holdDistance&&forecastDistance<5&&stableSupport)
                {
                    // Clear the entire forecast zone before the stone reaches standing height.
                    float entry=h.timer+Mathf.Max(0,forecastDistance-ReturnWorld.DebrisImpactRadius-.15f)/ReturnMotor.Speed;
                    float exit=h.timer+(forecastDistance+ReturnWorld.DebrisImpactRadius+.15f)/ReturnMotor.Speed;
                    float dangerStarts=ReturnWorld.DebrisImpactTime-ReturnMotor.Height/ReturnWorld.DebrisFallSpeed-.1f;
                    if(game.Chapter!=Movement.Rupture||exit>=dangerStarts&&entry<ReturnWorld.DebrisImpactTime+.24f)
                    {
                        if(timedReplaySupport&&!ForecastHoldingClear(m,ReturnWorld.DebrisImpactTime+.29f-h.timer))continue;
                        input.move=0;return input;
                    }
                }
                if(!h.debris&&m.Grounded&&h.x>m.support.Left&&h.x<m.support.Right&&m.position.x<m.support.Right-1)
                {
                    // The approach waits for clearance before landing beside this sweep.
                    // A timed ledge needs a forward exit before it disappears beneath a same-ledge hop.
                    input.jumpPressed=true;gateShelf=m.support;
                    bool timedGate=gateShelf.kind==SurfaceKind.Collapse||gateShelf.kind==SurfaceKind.Fading;
                    landing=timedGate?(Next(gateShelf,direction)??gateShelf):gateShelf;
                    return input;
                }
            }
            if(m.Grounded)
            {
                landing=gateShelf=ferryHopTarget=null;ReturnSurface dest=Next(m.support,direction);
                if(dest==null){input.move=Mathf.Abs(target-m.position.x)<.3f?0:Mathf.Sign(target-m.position.x);return input;}
                float edge=direction>0?m.support.Right:m.support.Left;
                float destEdge=direction>0?dest.Left:dest.Right;
                float distanceToEdge=(edge-m.position.x)*direction;
                // Long rising burst crossings need the remaining takeoff room on a timed ledge.
                bool longRisingBurst=game.Chapter==Movement.Fragments&&game.BurstUnlocked&&m.support.kind==SurfaceKind.Collapse&&
                    dest.y>m.support.y+.2f&&(destEdge-edge)*direction>6.2f;
                float launchMargin=longRisingBurst?.1f:.34f;
                if(m.support.kind==SurfaceKind.Ferry)
                {
                    input.move=0;
                    if(game.Chapter==Movement.Rupture&&(destEdge-edge)*direction>=(game.BurstUnlocked?6.2f:4.2f)&&TryFerryForecastHop(m,m.support,out float fraction))
                    {
                        ferryHopTarget=landing=m.support;ferryHopFraction=fraction;
                        float aim=Mathf.Lerp(m.support.Left+ReturnMotor.Radius+.18f,m.support.Right-ReturnMotor.Radius-.18f,fraction);
                        input=SteerLanding(input,m,m.support,null,direction,game.BurstUnlocked,aim);input.jumpPressed=true;
                        if(!ferryHopLogged){ferryHopLogged=true;Debug.Log("[RETURN acceptance] Ferry forecast hop at "+m.position+", fraction="+fraction.ToString("F2")+", forecasts="+MeteorForecastSummary(w));}
                        return input;
                    }
                    // Move to the departure edge once the ferry brings it within jumping distance.
                    if((destEdge-edge)*direction<(game.BurstUnlocked?6.2f:4.2f))
                    {input.move=direction;if(distanceToEdge<.4f){input.jumpPressed=true;landing=dest;}}
                }
                else if(distanceToEdge<launchMargin)
                {
                    if(!TimedSweepClear(m,dest,direction)){input.move=0;return input;}
                    if(!MeteorLandingClear(m,dest,direction))
                    {
                        bool timedReplay=ruptureMidpointRetryChecked&&m.support.kind==SurfaceKind.Collapse;
                        if(!timedReplay||ForecastHoldingClear(m,ForecastWaitDuration(m,dest))){input.move=0;return input;}
                        if(!unsafeReplayHoldLogged){unsafeReplayHoldLogged=true;Debug.Log("[RETURN acceptance] Unsafe checkpoint hold avoided at "+m.position+"; departing for a forecast-aware landing");}
                    }
                    foreach(ReturnHazard h in w.hazards)
                    {
                        if(w.returnOpen||m.support.kind==SurfaceKind.Fading||m.support.kind==SurfaceKind.Collapse||h.debris||h.x<=dest.Left||h.x>=dest.Right)continue;
                        float discriminant=ReturnMotor.JumpSpeed*ReturnMotor.JumpSpeed-2*ReturnMotor.Gravity*(dest.y-m.support.y);
                        if(discriminant<=0)continue;
                        float flight=(ReturnMotor.JumpSpeed+Mathf.Sqrt(discriminant))/ReturnMotor.Gravity;
                        float entryX=Mathf.Clamp(m.position.x+ReturnMotor.Speed*flight,dest.Left+.6f,dest.Right-.6f);
                        float entryPhase=(w.clock+flight)*1.3f;
                        float exitPhase=(w.clock+flight+2*ReturnMotor.JumpSpeed/ReturnMotor.Gravity-.15f)*1.3f;
                        float sweepEntry=h.x+Mathf.Sin(entryPhase)*1.7f,sweepExit=h.x+Mathf.Sin(exitPhase)*1.7f;
                        // Land left of the returning bar, then jump over it while it clears the far edge.
                        if(sweepEntry-entryX<1.7f||Mathf.Cos(entryPhase)>0||dest.Right-.5f-sweepExit<.8f)
                        {input.move=0;return input;}
                    }
                    input.jumpPressed=true;landing=dest;
                }
            }
            else
            {
                foreach(ReturnSurface s in w.surfaces)
                    if(s.solid&&s.kind==SurfaceKind.Spring&&m.velocity.y>14&&m.position.x>=s.Left-.3f&&m.position.x<=s.Right+.3f)
                    {
                        landing=Next(s,direction,true);
                        if(game.Chapter==Movement.Rupture&&!springForecastLogged)
                        {springForecastLogged=true;Debug.Log("[RETURN acceptance] Spring departure clock="+w.clock.ToString("F3")+", feet="+m.position+", forecasts="+MeteorForecastSummary(w));}
                    }
                if(landing!=null)
                {
                    float aim=landing==ferryHopTarget?Mathf.Lerp(landing.Left+ReturnMotor.Radius+.18f,landing.Right-ReturnMotor.Radius-.18f,ferryHopFraction):ForecastLandingAim(m,landing,direction);
                    input=SteerLanding(input,m,landing,gateShelf,direction,game.BurstUnlocked,aim,game.Chapter==Movement.Rupture&&landing.kind==SurfaceKind.Spring);
                    bool timedGateExit=gateShelf!=null&&landing!=gateShelf&&(gateShelf.kind==SurfaceKind.Collapse||gateShelf.kind==SurfaceKind.Fading);
                    // A level crossing gains range by retaining the first part of its descending arc.
                    if(timedGateExit&&landing.y<=gateShelf.y+.2f&&m.velocity.y>-2)input.burstPressed=false;
                    if(input.burstPressed)bursts++;
                }
            }
            return input;
        }
        ReturnInput StreetReviewInput()
        {
            var j=game.journey;var input=new ReturnInput{vertical=1,jumpPressed=true,jumpHeld=true,look=new Vector2(12,12)};
            if(!j.StreetPlayable){input.move=1;return input;}
            if(streetPhase==0){input.move=1;if(j.Position.x>=j.StreetStartX+4){streetPhase=1;streetPauseAt=j.SceneElapsed;streetPauseX=j.Position.x;input.move=0;}}
            else if(streetPhase==1){input.move=0;if(j.SceneElapsed-streetPauseAt>=.75f)streetPhase=2;}
            else if(streetPhase==2){input.move=-1;if(j.Position.x<=streetPauseX-2)streetPhase=3;}
            else {input.move=1;input.interact=j.CanDoor;}
            return input;
        }
        ReturnInput LowerEntryInput()
        {
            ReturnWorld w=game.world;ReturnMotor m=game.motor;
            if(!lowerEntryStarted)
            {
                if(!w.returnOpen||!m.Grounded){Finish(false,"Alternate lower entry did not begin at the real grounded pickup");return new ReturnInput();}
                foreach(ReturnSurface s in w.surfaces)
                {
                    if(w.IsLowerReturnSupport(s)&&s.x<w.goalX&&(lowerEntryTarget==null||s.x>lowerEntryTarget.x))lowerEntryTarget=s;
                    if(s!=w.surfaces[0]&&!w.IsLowerReturnSupport(s)){lowerEntryUpperSurfaces.Add(s);lowerEntryUpperRoots.Add(s.visual);}
                }
                if(lowerEntryTarget==null){Finish(false,"No lower landing left of the former position gate exists");return new ReturnInput();}
                if(w.ShelfMemoryRoots!=null)lowerEntryMemoryRoots.AddRange(w.ShelfMemoryRoots);
                lowerEntryGuideRoots.AddRange(w.UpperRouteGuides);lowerEntryRoot=w.root;lowerEntryAt=simulated;lowerEntryStarted=true;
            }
            if(game.ReturnDescent)return new ReturnInput();
            // Walk off the left edge, then brake toward the real lower shelf; no placement or jump.
            ReturnInput input=new ReturnInput{move=-1,jumpHeld=true};
            return m.Grounded?input:SteerLanding(input,m,lowerEntryTarget,null,-1,false);
        }
        static int ActiveEntryRenderers(List<Transform> roots,out int registered)
        {
            int active=0;registered=0;
            foreach(Transform root in roots)foreach(Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {registered++;if(renderer.enabled&&renderer.gameObject.activeInHierarchy)active++;}
            return active;
        }
        bool ObserveLowerEntry()
        {
            if(!lowerEntryStarted||lowerEntryDone)return true;
            ReturnWorld w=game.world;ReturnMotor m=game.motor;
            if(game.Chapter!=Movement.Fragments||game.Excursion!=3||w.root!=lowerEntryRoot||!lowerEntryRoot.gameObject.activeInHierarchy)
            {Finish(false,"Alternate lower entry changed or hid its actual scene");return false;}
            if(!m.Grounded&&!game.ReturnDescent)lowerEntryWalkOff=true;
            if(game.ReturnDescent&&lowerEntryLandedAt<0)
            {
                if(!lowerEntryWalkOff||!m.Grounded||m.support!=lowerEntryTarget||m.position.x>w.goalX+3)
                {Finish(false,"Alternate lower entry did not walk to the target left of the former position gate");return false;}
                lowerEntryLandedAt=simulated;lowerEntryX=m.position.x;
            }
            if(lowerEntryLandedAt<0)
            {if(simulated-lowerEntryAt>5){Finish(false,"Alternate lower landing did not activate return mode");return false;}return true;}
            if(simulated-lowerEntryLandedAt<.9f)return true;
            var report=new LowerEntryReview{passed=true,walkedOff=lowerEntryWalkOff,supportTriggered=game.ReturnDescent,
                entryX=lowerEntryX,formerPositionGateX=w.goalX+3,settledSeconds=simulated-lowerEntryLandedAt,
                targetLeft=lowerEntryTarget.Left,targetRight=lowerEntryTarget.Right,upperSurfaces=lowerEntryUpperSurfaces.Count,
                memoryRoots=lowerEntryMemoryRoots.Count,guideRoots=lowerEntryGuideRoots.Count};
            foreach(ReturnSurface s in lowerEntryUpperSurfaces)if(s.solid)report.solidUpperSurfaces++;
            report.activeUpperRenderers=ActiveEntryRenderers(lowerEntryUpperRoots,out report.registeredUpperRenderers);
            report.activeMemoryRenderers=ActiveEntryRenderers(lowerEntryMemoryRoots,out report.registeredMemoryRenderers);
            report.activeGuideRenderers=ActiveEntryRenderers(lowerEntryGuideRoots,out report.registeredGuideRenderers);
            if(report.upperSurfaces==0||report.registeredUpperRenderers==0||report.registeredMemoryRenderers==0||report.registeredGuideRenderers==0||
                report.solidUpperSurfaces!=0||report.activeUpperRenderers!=0||report.activeMemoryRenderers!=0||report.activeGuideRenderers!=0)
            {Finish(false,"Retired upper collision, scenery or guide render owners remain active after alternate entry");return false;}
            lowerEntryDone=true;string directory=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");
            if(!string.IsNullOrEmpty(directory))File.WriteAllText(Path.Combine(directory,"lower-entry-review.json"),JsonUtility.ToJson(report,true));
            Debug.Log("[RETURN lower-entry] "+JsonUtility.ToJson(report));return true;
        }
        ReturnInput BurstBufferInput()
        {
            ReturnMotor m=game.motor;ReturnSurface floor=game.world.surfaces[0];
            ReturnInput input=new ReturnInput{jumpHeld=true};
            if(burstBufferStep==0)
            {
                // The opt-in review uses the live player, ordinary input path and real floor.
                m.Reset(new Vector2(6,floor.y));m.support=floor;
                burstBufferSample=new BurstBufferCase{name=burstBufferCase==0?"landing":burstBufferCase==1?"expiry":"reset"};
                burstBufferCaseAt=simulated;burstBufferLandedAt=burstBufferStartedAt=-1;burstBufferReset=false;
                burstBufferStep=1;input.jumpPressed=true;
            }
            else if(burstBufferStep==1&&m.velocity.y<=2.5f&&m.position.y>floor.y+2)
            {burstBufferStep=2;input.move=1;input.burstPressed=true;}
            else if(burstBufferStep==2&&!m.Bursting&&m.velocity.y<0&&(burstBufferCase==1?m.position.y>floor.y+1.8f:m.position.y<floor.y+.55f))
            {
                burstBufferSample.queuedAirborne=!m.Grounded&&!m.BurstReady;
                burstBufferQueuedAt=simulated;burstBufferStep=3;input.move=-1;input.burstPressed=true;
            }
            else if(burstBufferStep==3)
            {
                if(burstBufferCase==0)input.move=1; // Change facing after the press to check captured direction.
                else if(burstBufferCase==2&&!burstBufferReset)
                {m.Reset(new Vector2(6,floor.y));m.support=floor;burstBufferReset=true;burstBufferResetAt=simulated;burstBufferSample.reset=true;}
            }
            return input;
        }
        bool ObserveBurstBuffer()
        {
            if(burstBufferSample==null||burstBufferDone)return true;
            if(simulated-burstBufferCaseAt>4){Finish(false,"Burst buffer review did not finish its landing cycle");return false;}
            if(burstBufferStep!=3)return true;
            ReturnMotor m=game.motor;
            if(!burstBufferSample.queuedAirborne){Finish(false,"Burst buffer was not pressed airborne before recharge");return false;}
            if(m.BurstStarted)
            {
                burstBufferSample.startsAfterPress++;
                if(burstBufferCase!=0||burstBufferSample.startsAfterPress!=1||burstBufferLandedAt<0||m.velocity.x>=0)
                {Finish(false,"Queued burst repeated, fired without landing, survived expiry/reset or lost its direction");return false;}
                burstBufferSample.landedBeforeStart=true;burstBufferStartedAt=simulated;
                burstBufferSample.queueToStartSeconds=simulated-burstBufferQueuedAt;
                if(burstBufferSample.queueToStartSeconds>.12f){Finish(false,"Queued burst fired after its input window");return false;}
            }
            if(m.Grounded&&burstBufferLandedAt<0)
            {burstBufferLandedAt=simulated;if(!burstBufferReset)burstBufferSample.queueToLandingSeconds=simulated-burstBufferQueuedAt;}
            bool settled=burstBufferCase==0?burstBufferStartedAt>=0&&simulated-burstBufferStartedAt>.35f:
                burstBufferCase==1?burstBufferLandedAt>=0&&simulated-burstBufferLandedAt>.35f:
                burstBufferReset&&simulated-burstBufferResetAt>.35f;
            if(!settled)return true;
            if(burstBufferCase==1&&burstBufferSample.queueToLandingSeconds<=.12f)
            {Finish(false,"Expiry review landed while the press was still valid");return false;}
            burstBufferSample.passed=true;burstBufferSamples.Add(burstBufferSample);burstBufferSample=null;burstBufferStep=0;burstBufferCase++;
            if(burstBufferCase==3)
            {
                burstBufferDone=true;m.Reset(new Vector2(6,game.world.surfaces[0].y));m.support=game.world.surfaces[0];
                var report=new BurstBufferReview{passed=true,samples=burstBufferSamples.ToArray()};
                string directory=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");
                if(!string.IsNullOrEmpty(directory))File.WriteAllText(Path.Combine(directory,"burst-buffer-review.json"),JsonUtility.ToJson(report,true));
                Debug.Log("[RETURN burst-buffer] "+JsonUtility.ToJson(report));
            }
            return true;
        }
        ReturnInput AirBrakeInput()
        {
            ReturnMotor m=game.motor;ReturnSurface floor=game.world.surfaces[0];
            ReturnInput input=new ReturnInput{jumpHeld=true};
            if(airBrakeStep==0)
            {
                // Opt-in only: use the actual player and normal input tick above its real floor.
                m.Reset(new Vector2(6,floor.y));m.support=floor;
                input.move=1;input.jumpPressed=true;airBrakeStep=1;
            }
            else if(airBrakeStep==1)
            {
                input.move=1;
                if(!m.Grounded&&m.velocity.x>=6.7f&&m.position.y>floor.y+1.2f)
                {
                    airBrakeAt=simulated;airBrakeStartX=airBrakeFarthestX=m.position.x;airBrakeStartY=m.position.y;
                    airBrakeSample=new AirBrakeCase{name=airBrakeCase==0?"release":"reverse",initialSpeed=m.velocity.x};
                    airBrakeStep=2;input.move=airBrakeCase==0?0:-1;
                }
            }
            else input.move=airBrakeCase==0?0:-1;
            return input;
        }
        bool ObserveAirBrake()
        {
            if(airBrakeSample==null||airBrakeDone)return true;
            ReturnMotor m=game.motor;float elapsed=simulated-airBrakeAt;
            airBrakeFarthestX=Mathf.Max(airBrakeFarthestX,m.position.x);
            if(m.Grounded||m.Bursting||elapsed>.45f)
            {Finish(false,"Air steering review lost ordinary airborne control");return false;}
            bool controlled=airBrakeCase==0?Mathf.Abs(m.velocity.x)<.1f:m.velocity.x<=-6.7f;
            if(!controlled)return true;
            airBrakeSample.seconds=elapsed;airBrakeSample.forwardDrift=airBrakeFarthestX-airBrakeStartX;
            airBrakeSample.finalSpeed=m.velocity.x;airBrakeSample.airborne=!m.Grounded;
            if(elapsed>(airBrakeCase==0?.14f:.30f)||airBrakeSample.forwardDrift>.4f||m.position.y<=airBrakeStartY)
            {Finish(false,"Air steering overshot its precision-landing response window");return false;}
            airBrakeSample.passed=true;airBrakeSamples.Add(airBrakeSample);airBrakeSample=null;airBrakeStep=0;airBrakeCase++;
            if(airBrakeCase==2)
            {
                airBrakeDone=true;m.Reset(new Vector2(6,game.world.surfaces[0].y));m.support=game.world.surfaces[0];
                var report=new AirBrakeReview{passed=true,samples=airBrakeSamples.ToArray()};
                string directory=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");
                if(!string.IsNullOrEmpty(directory))File.WriteAllText(Path.Combine(directory,"air-brake-review.json"),JsonUtility.ToJson(report,true));
                Debug.Log("[RETURN air-brake] "+JsonUtility.ToJson(report));
            }
            return true;
        }
        static ReturnInput SteerLanding(ReturnInput input,ReturnMotor m,ReturnSurface target,ReturnSurface gate,int direction,bool burstUnlocked,float aim=float.NaN,bool springApproach=false)
        {
            float x=target.x;
            if(target.kind==SurfaceKind.Stone&&Mathf.Abs(m.position.y-target.y)<3.5f&&target==gate)x=target.Right-.5f;
            if(!float.IsNaN(aim))x=aim;
            if(springApproach)x=direction>0?target.Right-ReturnMotor.Radius-.18f:target.Left+ReturnMotor.Radius+.18f;
            float d=x-m.position.x,stopping=m.velocity.x*m.velocity.x/(2*ReturnMotor.AirBrake);
            input.move=Mathf.Abs(d)<.3f||Mathf.Sign(d)==Mathf.Sign(m.velocity.x)&&Mathf.Abs(d)<stopping+.2f?0:Mathf.Sign(d);
            float leading=direction>0?target.Left:target.Right;
            // Follow the near-peak cue: an early burst discards height needed by a long rising crossing.
            if(burstUnlocked&&m.BurstReady&&m.velocity.y<.25f&&m.position.y>target.y+.25f&&((leading-m.position.x)*direction>2.2f||target.kind==SurfaceKind.Ferry&&d*direction>2.8f))
            {input.burstPressed=true;input.move=direction;}
            return input;
        }
        static string MeteorForecastSummary(ReturnWorld world)
        {
            string summary="";
            foreach(ReturnHazard h in world.hazards)
                if(h.debris&&h.timer>=0&&h.timer<ReturnWorld.DebrisImpactTime+.24f)summary+="[x="+h.x.ToString("F2")+", age="+h.timer.ToString("F3")+"]";
            return summary;
        }
        [Serializable] sealed class ForecastCandidateReview
        {public float aim;public bool tested,clear;public string detail;}
        [Serializable] sealed class ForecastLandingReview
        {
            public Vector2 feet,velocity;public bool bursting,burstReady,hasChosen;
            public float worldClock,targetLeft,targetRight,targetY,chosenAim;public string forecasts;
            public ForecastCandidateReview[] candidates;
        }
        float ForecastLandingAim(ReturnMotor actual,ReturnSurface target,int direction)
        {
            if(game.Chapter!=Movement.Rupture||target.kind==SurfaceKind.Spring)return float.NaN;
            ReturnWorld w=game.world;var camera=game.GetComponentInChildren<Camera>();
            var forecasts=new System.Collections.Generic.List<ReturnHazard>();
            foreach(ReturnHazard h in w.hazards)
            {
                if(!h.debris||h.timer<0||h.timer>=ReturnWorld.DebrisImpactTime+.24f||h.x+ReturnWorld.DebrisImpactRadius<target.Left||h.x-ReturnWorld.DebrisImpactRadius>target.Right)continue;
                Vector3 screen=camera.WorldToViewportPoint(h.warning.position);
                if(screen.z>0&&screen.x>0&&screen.x<1&&screen.y>0&&screen.y<1)forecasts.Add(h);
            }
            if(forecasts.Count==0)return float.NaN;
            float left=target.Left+ReturnMotor.Radius+.18f,right=target.Right-ReturnMotor.Radius-.18f;
            float best=float.NaN,cost=float.PositiveInfinity;
            bool diagnose=springForecastLogged&&!springAimLogged;
            var candidates=diagnose?new System.Collections.Generic.List<ForecastCandidateReview>():null;
            for(int i=0;i<=32;i++)
            {
                float candidate=Mathf.Lerp(left,right,i/32f),distance=Mathf.Abs(candidate-target.x);
                if(distance>=cost)
                {if(diagnose)candidates.Add(new ForecastCandidateReview{aim=candidate,tested=false,detail="Farther than an already safe aim"});continue;}
                bool clear=ForecastLandingClear(actual,target,direction,candidate,forecasts,out string detail);
                if(diagnose)candidates.Add(new ForecastCandidateReview{aim=candidate,tested=true,clear=clear,detail=detail});
                if(!clear)continue;
                best=candidate;cost=distance;
            }
            if(diagnose)
            {
                springAimLogged=true;Debug.Log("[RETURN acceptance] Spring landing aim="+best.ToString("F3")+", feet="+actual.position+", forecasts="+MeteorForecastSummary(w));
                var review=new ForecastLandingReview{feet=actual.position,velocity=actual.velocity,bursting=actual.Bursting,burstReady=actual.BurstReady,worldClock=w.clock,
                    targetLeft=target.Left,targetRight=target.Right,targetY=target.y,hasChosen=!float.IsNaN(best),chosenAim=float.IsNaN(best)?0:best,forecasts=MeteorForecastSummary(w),candidates=candidates.ToArray()};
                string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))output=Path.Combine(Application.persistentDataPath,"ReturnEvidence");
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"landing-candidates.json"),JsonUtility.ToJson(review,true));
            }
            return best;
        }
        bool ForecastLandingClear(ReturnMotor actual,ReturnSurface target,int direction,float aim,System.Collections.Generic.List<ReturnHazard> forecasts,out string detail)
        {
            detail="No destination contact within prediction horizon";
            ReturnWorld w=game.world;var ground=new System.Collections.Generic.List<ReturnSurface>();
            foreach(ReturnSurface s in w.surfaces)ground.Add(new ReturnSurface(s.x,s.y,s.width,s.kind){age=s.age,solid=s.solid,originX=s.originX,originY=s.originY,span=s.span});
            ReturnMotor probe=actual.Snapshot();if(probe.support!=null)probe.support=ground[w.surfaces.IndexOf(probe.support)];
            ReturnSurface destination=ground[w.surfaces.IndexOf(target)],planned=destination;bool landed=false;const float dt=1f/60;
            for(int step=0;step<180;step++)
            {
                float elapsed=(step+1)*dt;
                foreach(ReturnSurface s in ground)
                {
                    float old=s.x;
                    if(s.kind==SurfaceKind.Ferry)s.x=s.originX+Mathf.PingPong((w.clock+elapsed)*2.4f,s.span);
                    if(probe.support==s)probe.position.x+=s.x-old;
                    if(s.kind==SurfaceKind.Fading||s.kind==SurfaceKind.Collapse){if(s.age>=0)s.age+=dt;s.solid=s.age<(s.kind==SurfaceKind.Fading?1.1f:1.55f);}
                }
                foreach(ReturnHazard h in forecasts)
                {
                    float age=h.timer+elapsed,impactAge=age-ReturnWorld.DebrisImpactTime;
                    float y=h.y+ReturnWorld.DebrisDropHeight-(age-ReturnWorld.DebrisWarningSeconds)*ReturnWorld.DebrisFallSpeed;
                    if(age>=ReturnWorld.DebrisWarningSeconds&&age<ReturnWorld.DebrisImpactTime&&Mathf.Abs(probe.position.x-h.x)<.7f&&y<probe.position.y+ReturnMotor.Height&&y>probe.position.y-.5f)
                    {detail="Falling forecast x="+h.x.ToString("F2")+", elapsed="+elapsed.ToString("F3")+", feet="+probe.position;return false;}
                    if(impactAge>=0&&impactAge<.24f&&Mathf.Abs(probe.position.x-h.x)<ReturnWorld.DebrisImpactRadius&&probe.position.y<h.y+.45f&&probe.position.y+ReturnMotor.Height>h.y-.5f)
                    {detail="Forecast impact x="+h.x.ToString("F2")+", elapsed="+elapsed.ToString("F3")+", feet="+probe.position;return false;}
                }
                ReturnInput input=new ReturnInput{move=direction,jumpHeld=true};
                if(probe.Grounded)
                {
                    landed|=probe.support==destination;
                    ReturnSurface next=Next(w.surfaces[ground.IndexOf(probe.support)],direction);
                    if(next==null){detail=landed?"Landed at terminal surface":"No next surface before destination";return landed;}
                    bool wait=false;
                    if(ruptureMidpointRetryChecked&&probe.support.kind==SurfaceKind.Collapse)
                        foreach(var forecast in forecasts)if(ForecastCrossingNeedsWait(probe,forecast,forecast.timer+elapsed,direction)){wait=true;break;}
                    if(wait)input.move=0;
                    else if((direction>0?probe.support.Right-probe.position.x:probe.position.x-probe.support.Left)<.34f)
                    {input.jumpPressed=true;planned=ground[w.surfaces.IndexOf(next)];}
                }
                else
                {
                    foreach(ReturnSurface s in ground)
                        if(s.solid&&s.kind==SurfaceKind.Spring&&probe.velocity.y>14&&probe.position.x>=s.Left-.3f&&probe.position.x<=s.Right+.3f)
                        {
                            landed|=s==destination;
                            ReturnSurface next=Next(w.surfaces[ground.IndexOf(s)],direction,true);planned=next==null?null:ground[w.surfaces.IndexOf(next)];
                        }
                    if(planned!=null)input=SteerLanding(input,probe,planned,null,direction,game.BurstUnlocked,planned==destination?aim:float.NaN,planned.kind==SurfaceKind.Spring);
                }
                probe.Step(dt,input.move,0,input.jumpPressed,input.jumpHeld,ground,input.burstPressed);
                landed|=probe.support==destination;
                if(landed&&(probe.position.x-(direction>0?destination.Right:destination.Left))*direction>ReturnMotor.Radius){detail="Cleared destination";return true;}
                if(probe.position.y<target.y-5){detail="Fell below destination at "+probe.position;return false;}
            }
            if(landed)detail="Destination touched within prediction horizon";
            return landed;
        }
        bool TryFerryForecastHop(ReturnMotor actual,ReturnSurface ferry,out float fraction)
        {
            fraction=.5f;ReturnWorld w=game.world;var camera=game.GetComponentInChildren<Camera>();
            var forecasts=new System.Collections.Generic.List<ReturnHazard>();
            foreach(ReturnHazard h in w.hazards)
            {
                if(!h.debris||h.timer<0||h.timer>=ReturnWorld.DebrisImpactTime+.24f)continue;
                Vector3 screen=camera.WorldToViewportPoint(h.warning.position);
                if(screen.z>0&&screen.x>0&&screen.x<1&&screen.y>0&&screen.y<1)forecasts.Add(h);
            }
            if(forecasts.Count==0||FerryForecastSafe(actual,ferry,false,.5f,forecasts))return false;
            foreach(float candidate in new[]{0f,.5f,1f})
                if(FerryForecastSafe(actual,ferry,true,candidate,forecasts)){fraction=candidate;return true;}
            return false;
        }
        bool FerryForecastSafe(ReturnMotor actual,ReturnSurface ferry,bool hop,float fraction,System.Collections.Generic.List<ReturnHazard> forecasts)
        {
            ReturnWorld w=game.world;var ground=new System.Collections.Generic.List<ReturnSurface>();
            foreach(ReturnSurface s in w.surfaces)ground.Add(new ReturnSurface(s.x,s.y,s.width,s.kind){age=s.age,solid=s.solid,originX=s.originX,originY=s.originY,span=s.span});
            ReturnMotor probe=actual.Snapshot();ReturnSurface support=ground[w.surfaces.IndexOf(ferry)];probe.support=support;const float dt=1f/60;
            for(int step=0;step<(hop?90:30);step++)
            {
                float elapsed=(step+1)*dt;
                foreach(ReturnSurface s in ground)
                {
                    float old=s.x;
                    if(s.kind==SurfaceKind.Ferry)s.x=s.originX+Mathf.PingPong((w.clock+elapsed)*2.4f,s.span);
                    if(probe.support==s)probe.position.x+=s.x-old;
                    if(s.kind==SurfaceKind.Fading||s.kind==SurfaceKind.Collapse){if(s.age>=0)s.age+=dt;s.solid=s.age<(s.kind==SurfaceKind.Fading?1.1f:1.55f);}
                }
                foreach(ReturnHazard h in forecasts)
                {
                    float age=h.timer+elapsed,impactAge=age-ReturnWorld.DebrisImpactTime;
                    float y=h.y+ReturnWorld.DebrisDropHeight-(age-ReturnWorld.DebrisWarningSeconds)*ReturnWorld.DebrisFallSpeed;
                    if(age>=ReturnWorld.DebrisWarningSeconds&&age<ReturnWorld.DebrisImpactTime&&Mathf.Abs(probe.position.x-h.x)<.7f&&y<probe.position.y+ReturnMotor.Height&&y>probe.position.y-.5f)return false;
                    if(impactAge>=0&&impactAge<.24f&&Mathf.Abs(probe.position.x-h.x)<ReturnWorld.DebrisImpactRadius&&probe.position.y<h.y+.45f&&probe.position.y+ReturnMotor.Height>h.y-.5f)return false;
                }
                ReturnInput input=new ReturnInput{jumpHeld=true};
                if(hop&&(step==0||!probe.Grounded))
                {
                    float aim=Mathf.Lerp(support.Left+ReturnMotor.Radius+.18f,support.Right-ReturnMotor.Radius-.18f,fraction);
                    input=SteerLanding(input,probe,support,null,1,game.BurstUnlocked,aim);input.jumpPressed=step==0;
                }
                probe.Step(dt,input.move,0,input.jumpPressed,input.jumpHeld,ground,input.burstPressed);
                if(hop&&step>0&&probe.Grounded)return probe.support==support;
                if(probe.position.y<ferry.y-5)return false;
            }
            return !hop;
        }
        static bool PursuitStandClear(ReturnMotor motor,ReturnWorld world)
        {
            float stoppingX=motor.position.x+motor.velocity.x*Mathf.Abs(motor.velocity.x)/(2*ReturnMotor.GroundBrake);
            foreach(ReturnHazard h in world.hazards)
                if(h.debris&&h.timer>=0&&h.timer<ReturnWorld.DebrisImpactTime+.24f&&Mathf.Abs(stoppingX-h.x)<ReturnWorld.DebrisImpactRadius+.2f)return false;
            return true;
        }
        [Serializable] sealed class SpringLaunchReview
        {
            public Vector2 actualFeet,actualVelocity,predictedFeet,predictedVelocity;
            public float worldClock,elapsed,actualFront,firstLeft,firstRight,firstY,exitLeft,exitRight,exitY;
            public bool springTouched,onwardTouched;public string detail,forecasts;
        }
        bool RecordSpringLaunchRejection(ReturnMotor actual,ReturnSurface first,ReturnSurface predictionExit,ReturnMotor probe,System.Collections.Generic.List<ReturnSurface> ground,float elapsed,string detail)
        {
            if(actual.support.kind==SurfaceKind.Collapse&&ruptureMidpointRetryChecked&&!checkpointReplayHoldLogged)
            {
                checkpointReplayHoldLogged=true;
                Debug.Log("[RETURN acceptance] Checkpoint replay departure held: "+detail+", predictedSeconds="+elapsed.ToString("F3")+", supportAge="+actual.support.age.ToString("F3")+", feet="+actual.position+", forecasts="+MeteorForecastSummary(game.world));
            }
            if(first.kind==SurfaceKind.Spring&&!springLaunchRejectionLogged)
            {
                springLaunchRejectionLogged=true;ReturnWorld w=game.world;
                var review=new SpringLaunchReview{actualFeet=actual.position,actualVelocity=actual.velocity,predictedFeet=probe.position,predictedVelocity=probe.velocity,
                    worldClock=w.clock,elapsed=elapsed,actualFront=w.frontX,firstLeft=first.Left,firstRight=first.Right,firstY=first.y,exitLeft=predictionExit.Left,exitRight=predictionExit.Right,exitY=predictionExit.y,
                    springTouched=ground[w.surfaces.IndexOf(first)].age>=0,onwardTouched=ground[w.surfaces.IndexOf(predictionExit)].age>=0,detail=detail,forecasts=MeteorForecastSummary(w)};
                string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))output=Path.Combine(Application.persistentDataPath,"ReturnEvidence");
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"spring-launch-rejection.json"),JsonUtility.ToJson(review,true));
                Debug.Log("[RETURN acceptance] Spring launch rejected: "+detail+", elapsed="+elapsed.ToString("F3")+", feet="+probe.position+", springTouched="+review.springTouched+", onwardTouched="+review.onwardTouched);
            }
            return false;
        }
        bool MeteorLandingClear(ReturnMotor actual,ReturnSurface first,int direction)
        {
            bool replayTimedDeparture=ruptureMidpointRetryChecked&&actual.support.kind==SurfaceKind.Collapse;
            if(game.Chapter!=Movement.Rupture||actual.support.kind!=SurfaceKind.Stone&&!replayTimedDeparture)return true;
            ReturnWorld w=game.world;
            ReturnSurface predictionExit=first.kind==SurfaceKind.Spring?Next(first,direction,true):first;
            if(predictionExit==null)return true;
            float corridorLeft=Mathf.Min(actual.position.x,predictionExit.Left),corridorRight=Mathf.Max(actual.position.x,predictionExit.Right);
            bool forecast=false;
            foreach(ReturnHazard h in w.hazards)
                if(h.debris&&h.timer>=0&&h.timer<ReturnWorld.DebrisImpactTime+.24f&&h.x+ReturnWorld.DebrisImpactRadius>corridorLeft&&h.x-ReturnWorld.DebrisImpactRadius<corridorRight)forecast=true;
            if(!forecast)return true;
            // Use the same motor and visible forecasts without advancing the live world.
            var ground=new System.Collections.Generic.List<ReturnSurface>();
            foreach(ReturnSurface s in w.surfaces)
                ground.Add(new ReturnSurface(s.x,s.y,s.width,s.kind){age=s.age,solid=s.solid,originX=s.originX,originY=s.originY,span=s.span});
            var probe=new ReturnMotor();probe.Reset(actual.position);probe.velocity=actual.velocity;probe.facing=actual.facing;
            probe.support=ground[w.surfaces.IndexOf(actual.support)];
            ReturnSurface planned=ground[w.surfaces.IndexOf(first)];float front=w.frontX;const float dt=1f/60;
            int predictionSteps=first.kind==SurfaceKind.Spring?240:150;
            for(int step=0;step<predictionSteps;step++)
            {
                float elapsed=(step+1)*dt;
                if(front<-90&&probe.position.x>13)front=-5;
                if(front>-90)front=Mathf.Min(w.goalX-24,Mathf.Max(front+5.2f*dt,probe.position.x-24));
                foreach(ReturnSurface s in ground)
                {
                    float old=s.x;
                    if(s.kind==SurfaceKind.Ferry)s.x=s.originX+Mathf.PingPong((w.clock+elapsed)*2.4f,s.span);
                    if(probe.support==s)probe.position.x+=s.x-old;
                    if(s.kind==SurfaceKind.Fading||s.kind==SurfaceKind.Collapse)
                    {
                        if(s.age>=0)s.age+=dt;
                        s.solid=s.age<(s.kind==SurfaceKind.Fading?1.1f:1.55f);
                    }
                    if(front>-90&&s.Right<front)s.solid=false;
                }
                if(front>-90&&probe.position.x<front+.4f)return RecordSpringLaunchRejection(actual,first,predictionExit,probe,ground,elapsed,"Predicted pursuit contact");
                foreach(ReturnHazard h in w.hazards)
                {
                    if(!h.debris||h.timer<0)continue;
                    float age=h.timer+elapsed,impactAge=age-ReturnWorld.DebrisImpactTime;
                    float y=h.y+ReturnWorld.DebrisDropHeight-(age-ReturnWorld.DebrisWarningSeconds)*ReturnWorld.DebrisFallSpeed;
                    if(age>=ReturnWorld.DebrisWarningSeconds&&age<ReturnWorld.DebrisImpactTime&&Mathf.Abs(probe.position.x-h.x)<.7f&&y<probe.position.y+ReturnMotor.Height&&y>probe.position.y-.5f)
                    return RecordSpringLaunchRejection(actual,first,predictionExit,probe,ground,elapsed,"Predicted falling meteor x="+h.x.ToString("F2")+", age="+age.ToString("F3"));
                    if(impactAge>=0&&impactAge<.24f&&Mathf.Abs(probe.position.x-h.x)<ReturnWorld.DebrisImpactRadius&&probe.position.y<h.y+.45f&&probe.position.y+ReturnMotor.Height>h.y-.5f)
                    return RecordSpringLaunchRejection(actual,first,predictionExit,probe,ground,elapsed,"Predicted meteor impact x="+h.x.ToString("F2")+", age="+age.ToString("F3"));
                }
                ReturnInput input=new ReturnInput{move=direction,jumpHeld=true};
                if(step==0)input.jumpPressed=true;
                else if(probe.Grounded)
                {
                    ReturnSurface live=w.surfaces[ground.IndexOf(probe.support)];
                    ReturnSurface next=Next(live,direction);
                    if(next==null)return true;
                    if((direction>0?probe.support.Right-probe.position.x:probe.position.x-probe.support.Left)<.34f)
                    {input.jumpPressed=true;planned=ground[w.surfaces.IndexOf(next)];}
                }
                else
                {
                    foreach(ReturnSurface s in ground)
                        if(s.solid&&s.kind==SurfaceKind.Spring&&probe.velocity.y>14&&probe.position.x>=s.Left-.3f&&probe.position.x<=s.Right+.3f)
                        {
                            ReturnSurface next=Next(w.surfaces[ground.IndexOf(s)],direction,true);
                            planned=next==null?null:ground[w.surfaces.IndexOf(next)];
                        }
                    if(planned!=null)input=SteerLanding(input,probe,planned,null,direction,game.BurstUnlocked,float.NaN,planned.kind==SurfaceKind.Spring);
                }
                probe.Step(dt,input.move,0,input.jumpPressed,input.jumpHeld,ground,input.burstPressed);
                if((probe.position.x-(direction>0?predictionExit.Right:predictionExit.Left))*direction>ReturnMotor.Radius)return true;
                if(probe.position.y<actual.position.y-5)return RecordSpringLaunchRejection(actual,first,predictionExit,probe,ground,elapsed,"Predicted fall below departure");
            }
            return true;
        }
        static bool ForecastCrossingNeedsWait(ReturnMotor actual,ReturnHazard forecast,float age,int direction)
        {
            if(!forecast.debris||age<0||age>=ReturnWorld.DebrisImpactTime+.24f)return false;
            float distance=(forecast.x-actual.position.x)*direction,speed=Mathf.Max(0,actual.velocity.x*direction);
            if(distance<=ReturnWorld.DebrisImpactRadius+.02f+speed*speed/(2*ReturnMotor.GroundBrake)||distance>=5)return false;
            float entry=age+Mathf.Max(0,distance-ReturnWorld.DebrisImpactRadius-.15f)/ReturnMotor.Speed;
            float exit=age+(distance+ReturnWorld.DebrisImpactRadius+.15f)/ReturnMotor.Speed;
            return exit>=ReturnWorld.DebrisImpactTime-ReturnMotor.Height/ReturnWorld.DebrisFallSpeed-.1f&&entry<ReturnWorld.DebrisImpactTime+.24f;
        }
        float ForecastWaitDuration(ReturnMotor actual,ReturnSurface destination)
        {
            float duration=.1f,left=Mathf.Min(actual.support.Left,destination.Left),right=Mathf.Max(actual.support.Right,destination.Right);
            foreach(var h in game.world.hazards)
                if(h.debris&&h.timer>=0&&h.timer<ReturnWorld.DebrisImpactTime+.24f&&h.x+ReturnWorld.DebrisImpactRadius>left&&h.x-ReturnWorld.DebrisImpactRadius<right)
                    duration=Mathf.Max(duration,ReturnWorld.DebrisImpactTime+.29f-h.timer);
            return duration;
        }
        bool ForecastHoldingClear(ReturnMotor actual,float duration)
        {
            ReturnWorld w=game.world;var ground=new System.Collections.Generic.List<ReturnSurface>();
            foreach(var s in w.surfaces)ground.Add(new ReturnSurface(s.x,s.y,s.width,s.kind){age=s.age,solid=s.solid,originX=s.originX,originY=s.originY,span=s.span});
            ReturnMotor probe=actual.Snapshot();probe.support=ground[w.surfaces.IndexOf(actual.support)];
            float front=w.frontX;const float dt=1f/60;
            for(int step=0;step<Mathf.CeilToInt(duration/dt);step++)
            {
                float elapsed=(step+1)*dt;
                if(front>-90)front=Mathf.Min(w.goalX-24,Mathf.Max(front+5.2f*dt,probe.position.x-24));
                if(front>-90&&probe.position.x<front+.4f)return false;
                foreach(var s in ground)
                {
                    if(s.kind==SurfaceKind.Collapse||s.kind==SurfaceKind.Fading){if(s.age>=0)s.age+=dt;s.solid=s.age<(s.kind==SurfaceKind.Fading?1.1f:1.55f);}
                    if(front>-90&&s.Right<front)s.solid=false;
                }
                foreach(var h in w.hazards)
                {
                    if(!h.debris||h.timer<0)continue;
                    float age=h.timer+elapsed,impact=age-ReturnWorld.DebrisImpactTime,y=h.y+ReturnWorld.DebrisDropHeight-(age-ReturnWorld.DebrisWarningSeconds)*ReturnWorld.DebrisFallSpeed;
                    if(age>=ReturnWorld.DebrisWarningSeconds&&age<ReturnWorld.DebrisImpactTime&&Mathf.Abs(probe.position.x-h.x)<.7f&&y<probe.position.y+ReturnMotor.Height&&y>probe.position.y-.5f)return false;
                    if(impact>=0&&impact<.24f&&Mathf.Abs(probe.position.x-h.x)<ReturnWorld.DebrisImpactRadius&&probe.position.y<h.y+.45f&&probe.position.y+ReturnMotor.Height>h.y-.5f)return false;
                }
                probe.Step(dt,0,0,false,true,ground,false);
                if(!probe.Grounded)return false;
            }
            return true;
        }
        bool TimedSweepClear(ReturnMotor actual,ReturnSurface first,int direction)
        {
            ReturnWorld w=game.world;
            if(game.Chapter!=Movement.Fragments||game.Carrying||w.returnOpen||direction!=1||actual.support.kind!=SurfaceKind.Stone||first.kind!=SurfaceKind.Fading&&first.kind!=SurfaceKind.Collapse)return true;
            ReturnSurface gateGround=first;
            for(int i=0;i<8&&(gateGround.kind==SurfaceKind.Fading||gateGround.kind==SurfaceKind.Collapse);i++)
            {gateGround=Next(gateGround,direction);if(gateGround==null)return true;}
            ReturnHazard sweep=null;
            foreach(ReturnHazard h in w.hazards)if(!h.debris&&h.x>gateGround.Left&&h.x<gateGround.Right){sweep=h;break;}
            if(gateGround.kind!=SurfaceKind.Stone||sweep==null)return true;
            // Predict with the real motor on private surface copies; no live world or clock changes.
            var ground=new System.Collections.Generic.List<ReturnSurface>();
            foreach(ReturnSurface s in w.surfaces)ground.Add(new ReturnSurface(s.x,s.y,s.width,s.kind){age=s.age,solid=s.solid});
            ReturnMotor probe=new ReturnMotor();probe.Reset(actual.position);probe.velocity=actual.velocity;probe.facing=actual.facing;probe.support=ground[w.surfaces.IndexOf(actual.support)];
            ReturnSurface planned=null,plannedGate=null;const float dt=1f/60;
            for(int step=0;step<360;step++)
            {
                foreach(ReturnSurface s in ground)
                {
                    if(s.kind!=SurfaceKind.Fading&&s.kind!=SurfaceKind.Collapse)continue;
                    if(s.age>=0)s.age+=dt;float life=s.kind==SurfaceKind.Fading?.65f:.85f;
                    s.solid=s.age<life;if(s.age>life+2.1f){s.age=-1;s.solid=true;}
                }
                float pressure=sweep.x+Mathf.Sin((w.clock+(step+1)*dt)*1.3f)*1.7f;
                if(Mathf.Abs(probe.position.x-pressure)<.36f&&probe.position.y<sweep.y+1.3f&&probe.position.y+ReturnMotor.Height>sweep.y+.1f)return false;
                ReturnInput input=new ReturnInput{move=direction,jumpHeld=true};
                if(probe.Grounded)
                {
                    if(!probe.support.solid)return false;
                    ReturnSurface support=w.surfaces[ground.IndexOf(probe.support)];
                    planned=plannedGate=null;
                    if(support==gateGround&&probe.position.x<support.Right-1)
                    {input.jumpPressed=true;planned=plannedGate=support;}
                    else
                    {
                        ReturnSurface next=Next(support,direction);if(next==null)return false;
                        if(support.Right-probe.position.x<.34f){input.jumpPressed=true;planned=next;}
                    }
                }
                else if(planned!=null)input=SteerLanding(input,probe,planned,plannedGate,direction,game.BurstUnlocked);
                probe.Step(dt,input.move,0,input.jumpPressed,input.jumpHeld,ground,input.burstPressed);
                if(probe.position.x>gateGround.Right+ReturnMotor.Radius&&probe.position.y>gateGround.y+1.3f)return true;
                if(probe.position.y<actual.position.y-5)return false;
            }
            return false;
        }
        int bursts;
        ReturnSurface Next(ReturnSurface current,int direction,bool spring=false)
        {
            if(game.Chapter==Movement.Fragments&&game.Carrying&&!game.ReturnDescent&&direction>0&&current.Left<=game.world.goalX&&current.Right>=game.world.goalX)
            {
                // Reach the lower entrance before choosing the path back, including the fourth route.
                foreach(var s in game.world.surfaces)
                    if(s.solid&&s.returnPath&&s.Left<game.world.returnEntry.x&&s.Right>game.world.returnEntry.x&&Mathf.Abs(s.y-game.world.returnEntry.y)<.01f)return s;
            }
            ReturnSurface best=null;float nearest=float.PositiveInfinity;
            foreach(ReturnSurface s in game.world.surfaces)
            {
                if(!s.solid||s==current)continue;
                if(game.Carrying&&game.ReturnDescent&&!s.returnPath&&s!=game.world.surfaces[0])continue;
                if(s.y>current.y+(spring?5.1f:2.65f))continue;
                float d=(s.x-current.x)*direction;
                if(d>.5f&&d<nearest){nearest=d;best=s;}
            }
            return best;
        }
        public void ObserveCamera()
        {
            if(fragmentsOnly&&game.Chapter==Movement.Fragments&&game.Excursion==0&&!game.InTransition)
            {
                float x=game.motor.position.x;
                if(!footingGroundFrame&&game.motor.Grounded&&x>8&&x<10)footingGroundFrame=RequestFootingFrame("Fragments-Footing-Ground");
                if(!footingJumpFrame&&!game.motor.Grounded&&game.motor.velocity.y>0&&x>12&&x<13)footingJumpFrame=RequestFootingFrame("Fragments-Footing-Jump");
            }
            if(hospitalOnly&&game.journey.Active&&(game.Chapter==Movement.Hospital||game.Chapter==Movement.Credits))
            {if(game.Chapter==Movement.Hospital)ObserveHospitalCamera();return;}
            if(streetOnly&&game.journey.Active&&game.Chapter==Movement.Reunion)
            {
                var streetJourney=game.journey;
                if(streetJourney.StreetPlayable){
                    if(streetJourney.CameraIsOrthographic){Finish(false,"Playable street lost its three-dimensional perspective camera");return;}
                    var view=game.GetComponentInChildren<Camera>();Vector3 playerView=view.WorldToViewportPoint(streetJourney.CharacterBodyFocus);
                    if(playerView.z<=0||playerView.x<.08f||playerView.x>.92f||playerView.y<.03f||playerView.y>.97f){Finish(false,"Playable street camera lost the character framing");return;}
                    Vector3 offset=streetJourney.CameraPosition-streetJourney.Position;
                    if(!streetCameraObserved){streetCameraObserved=true;streetCameraOffset=offset;}
                    else streetCameraError=Mathf.Max(streetCameraError,Vector3.Distance(offset,streetCameraOffset));
                    if(streetCameraError>.05f){Finish(false,"Street camera moved independently of horizontal player movement");return;}
                }
                return;
            }
            if(game.Chapter==Movement.Rupture)
            {
                var rain=game.world;
                observedMeteorForecasts=Mathf.Max(observedMeteorForecasts,rain.MeteorForecasts);
                observedMeteorImpacts=Mathf.Max(observedMeteorImpacts,rain.MeteorImpacts);
                observedVolcanoEjections=Mathf.Max(observedVolcanoEjections,rain.VolcanoEjections);
                if(openingRainAt>=0&&!openingRainProbeComplete&&!game.InTransition)
                {
                    openingRainSeconds=rain.clock-openingRainAt;openingRainFrames++;
                    openingRainDrift=Mathf.Max(openingRainDrift,Mathf.Abs(game.motor.position.x-openingRainPosition.x));
                    openingRainForecasts=Mathf.Max(openingRainForecasts,rain.MeteorForecasts-openingRainForecastStart);
                    openingRainImpacts=Mathf.Max(openingRainImpacts,rain.MeteorImpacts-openingRainImpactStart);
                    rainWhileStationary=openingRainSeconds>=3.5f&&openingRainFrames>=10&&openingRainDrift<.05f&&Mathf.Abs(game.motor.velocity.x)<.05f&&openingRainForecasts>=3&&openingRainImpacts>=1;
                }
                if(rain.MeteorForecasts>0){if(!float.IsNaN(priorMeteorX)&&Mathf.Abs(priorMeteorX-rain.LastMeteorX)>.2f)variedMeteorTargets=true;priorMeteorX=rain.LastMeteorX;}
                foreach(ReturnHazard hazard in rain.hazards)if(hazard.debris)
                {
                    var weatherCamera=game.GetComponentInChildren<Camera>();
                    if(hazard.ejection!=null&&hazard.ejection.gameObject.activeInHierarchy)
                    {Vector3 view=weatherCamera.WorldToViewportPoint(hazard.ejection.position);ejectionObserved|=view.z>0&&view.x>0&&view.x<1&&view.y>0&&view.y<1;}
                    if(hazard.warning!=null&&hazard.warning.gameObject.activeInHierarchy)
                        foreach(Renderer renderer in hazard.warning.GetComponentsInChildren<Renderer>())
                        {
                            Color colour=renderer.sharedMaterial.GetColor("_Color");Vector3 view=weatherCamera.WorldToViewportPoint(hazard.warning.position);redForecastObserved|=colour.r>.8f&&colour.g<.3f&&colour.b<.3f&&view.z>0&&view.x>0&&view.x<1&&view.y>0&&view.y<1;
                            meteorWarningSamples++;
                            ReturnSurface support=hazard.forecastSurface;
                            if(support==null||!support.solid||!support.visual.gameObject.activeInHierarchy)unsupportedMeteorWarning=true;
                            else
                            {
                                Bounds bounds=renderer.bounds;
                                float left=rain.root.InverseTransformPoint(bounds.min).x,right=rain.root.InverseTransformPoint(bounds.max).x;
                                maximumMeteorWarningOverhang=Mathf.Max(maximumMeteorWarningOverhang,Mathf.Max(support.Left-left,right-support.Right));
                                if(right-left<ReturnWorld.DebrisImpactRadius*2-.02f)clippedMeteorWarningSamples++;
                            }
                        }
                }
            }
            if(game.Chapter==Movement.Fragments)
            {
                int route=game.Excursion;var interaction=game.world;
                if(game.Carrying&&interaction.returnOpen&&!returnGreyCoverageObserved[route])
                {
                    int grey=0,gold=0;
                    foreach(ReturnSurface surface in interaction.surfaces)
                    {
                        if(!interaction.IsLowerReturnSupport(surface))continue;
                        foreach(Renderer renderer in surface.visual.GetComponentsInChildren<Renderer>(true))
                        {
                            if(renderer.sharedMaterial==null||renderer.sharedMaterial.shader.name!="Return/Ink")continue;
                            Color tint=renderer.sharedMaterial.GetColor("_Color");
                            bool cue=tint.r>.6f&&tint.g>.4f&&tint.b<.6f;
                            renderer.GetPropertyBlock(returnReceiver);float response=returnReceiver.GetFloat("_StageReveal");
                            if(cue){if(response>.5f){Finish(false,"A lower-return gold guide was greyed out");return;}gold++;}
                            else {if(response<.5f){Finish(false,"A newly created lower-return renderer missed the grey effect");return;}grey++;}
                        }
                    }
                    if(grey==0||gold==0){Finish(false,"No actual lower-return geometry and gold-guide receivers were observed");return;}
                    returnGreyRenderers[route]=grey;returnGoldGuides[route]=gold;returnGreyCoverageObserved[route]=true;
                }
                cleanFigureObserved[route]|=interaction.figure.CleanTraveller;
                if(interaction.PickupAnimationActive)
                {
                    pickupHandsObserved[route]|=interaction.MemoryLightVisible&&interaction.figure.HandsVisible&&interaction.PickupAge>.15f;
                    pocketApproachObserved[route]|=interaction.MemoryLightVisible&&interaction.LightPocketGap<.06f&&interaction.PickupAge>.75f;
                }
                else if(interaction.RadioPlacementActive)
                {
                    if(!game.Carrying||game.Fragments!=route){Finish(false,"A radio deposit advanced before the placement animation completed");return;}
                    placementHandsObserved[route]|=interaction.MemoryLightVisible&&interaction.figure.HandsVisible&&interaction.PlacementAge>.25f;
                    placementAtRadioObserved[route]|=interaction.MemoryLightVisible&&interaction.LightRadioGap<.08f&&interaction.PlacementAge>1.15f;
                    if(!radioHomeFrames[route]&&interaction.PlacementAge>1.15f)radioHomeFrames[route]=game.TryRequestFrame("Fragments-Radio-Return-"+route);
                }
                else
                {
                    if(interaction.figure.HandsVisible){Finish(false,"The clean traveller exposes hands outside a key interaction");return;}
                    if(game.Carrying)
                    {
                        if(interaction.MemoryLightVisible){Finish(false,"A pocketed memory remained visibly carried");return;}
                        pocketHiddenObserved[route]=true;
                    }
                }
            }
            if(game.FirstStageWhiteAlpha>=.99999f)
            {
                if(whiteHoldWallStart<0)whiteHoldWallStart=Time.realtimeSinceStartup;
                whiteHoldWallEnd=Time.realtimeSinceStartup;whiteHoldFrames++;
            }
            if(fragmentsOnly&&(!clientJourney||!clientFragmentsComplete)&&game.Chapter==Movement.Rupture)
            {
                float colour=game.world.RuptureColour;
                ruptureColourStartedGrey|=colour<.05f;
                if(lastRuptureColour>=0&&colour<lastRuptureColour-.00001f)colourReturnMonotonic=false;
                lastRuptureColour=colour;
                if(colour>.001f&&colour<.99f)colourReturnFrames++;
                if(!game.InTransition&&colour>=.9999f)colourReturnPresented=true;
            }
            var j=game.journey;if(!j.Active){
                var m=game.motor;var w=game.world;
                if(game.LowerReturnView&&game.Excursion==3&&m.Grounded&&m.support.returnPath){
                    bool bridge=m.support.y>0&&m.support.y<w.returnEntry.y-.2f&&m.support.Right<w.returnEntry.x-4;
                    if(bridge&&!lowerBridgeReadable){if(!game.LowerReturnReadable()){Finish(false,"Lower return bridge player overlaps captions or leaves the action frame");return;}lowerBridgeReadable=true;}
                    if(m.position.x<45&&!lowerCircuitReadable){if(!game.LowerReturnReadable()){Finish(false,"Lower circuit player overlaps captions or leaves the action frame");return;}lowerCircuitReadable=true;}
                }return;
            }
            if(j.SkinReview!=null){
                SaveGroundSkinReview(j.Scene!=Movement.StillWater);
                if(j.Scene==Movement.Reunion){
                    var skin=j.SkinReview;bool covered=skin.leaf!=null&&skin.island!=null&&skin.leafFrames>0&&skin.islandFrames>0;
                    var observation=new GroundSkinObservation{passed=covered,detail=covered?"Leaf and island skin comparisons retained; visual quality is unverified":"Leaf or island skin comparison is missing",leafFrames=skin.leafFrames,islandFrames=skin.islandFrames};
                    string output=GroundSkinEvidenceDirectory();File.WriteAllText(Path.Combine(output,"ground-skin-observation.json"),JsonUtility.ToJson(observation,true));
                    Finished=true;Debug.Log("[RETURN ground skin] "+(covered?"PASS ":"FAIL ")+observation.detail);Application.Quit(covered?0:1);return;
                }
            }
            bool fruit=j.Scene==Movement.StillWater&&j.FruitActionTime>=0;
            if(cameraSampleReady&&fruit&&!cameraWasFruit&&cameraScene==Movement.StillWater){fruitCameraAngleDegrees=Quaternion.Angle(cameraLastRotation,j.CameraRotation);fruitCameraLensDegrees=Mathf.Abs(cameraLastFov-j.CameraFieldOfView);fruitCameraTravel=Vector3.Distance(cameraLastPosition,j.CameraPosition);fruitCameraContinuous=fruitCameraAngleDegrees<1&&fruitCameraLensDegrees<.1f&&fruitCameraTravel<.2f;}
            if(cameraSampleReady&&j.Scene==Movement.Reunion&&cameraScene==Movement.StillWater){returnCameraAngleDegrees=Quaternion.Angle(cameraLastRotation,j.CameraRotation);returnCameraLensDegrees=Mathf.Abs(cameraLastFov-j.CameraFieldOfView);returnCameraTravel=Vector3.Distance(cameraLastPosition,j.CameraPosition);returnCameraContinuous=returnCameraAngleDegrees<1&&returnCameraLensDegrees<.1f&&returnCameraTravel<.2f;}
            if(j.Scene==Movement.Reunion){
                float t=j.SceneElapsed;
                // Camera state belongs to the presented frame, after all catch-up movement ticks.
                if(t>9.6f&&t<23.5f){
                    if(!j.CameraIsOrthographic){Finish(false,"Reverse journey did not become side view");return;}
                    if(!reverseSideView){reverseSideView=true;reverseStartX=lastReverseX=j.CameraPosition.x;}
                    if(j.CameraPosition.x>lastReverseX+.02f){Finish(false,"Reverse journey moved forward through the earlier stages");return;}
                    lastReverseX=reverseEndX=j.CameraPosition.x;
                }
                if(t>30&&!j.CameraIsOrthographic)returnedToVolume=true;
                if(!returnHandoffFrame&&t<.15f){if(!RecordFruitGroundPose("Return-Handoff"))return;returnHandoffFrame=game.TryRequestFrame("Return-Handoff");}if(!returnPullbackFrame&&t>2&&t<2.5f)returnPullbackFrame=game.TryRequestFrame("Return-Pullback");
            }
            if(j.Scene==Movement.Reunion&&j.SceneElapsed>=ReturnReverseJourney.CityPassAt&&j.SceneElapsed<=ReturnReverseJourney.DoorAt){
                if(cityPassStarted){
                    if(j.CameraPosition.x>cityLastCamera.x+.02f){Finish(false,"City camera did not sweep left");return;}
                    float travel=Vector3.Distance(cityLastCamera,j.CameraPosition),elapsed=j.SceneElapsed-cityLastElapsed;cityPassTravel+=travel;
                    if(CityCornerFrameRequired&&elapsed>0){
                        cityCornerMinimumSpeed=Mathf.Min(cityCornerMinimumSpeed,travel/elapsed);cityCornerSamples++;
                        if(cityCornerMinimumSpeed<.65f){Finish(false,"City camera stopped at the crossroads");return;}
                    }
                }
                cityPassStarted=true;cityLastCamera=j.CameraPosition;cityLastElapsed=j.SceneElapsed;
                if(!j.CityCameraOnly||Vector2.Distance(new Vector2(j.Position.x,j.Position.z),new Vector2(j.DoorwayPosition.x,j.DoorwayPosition.z-.7f))>.01f||j.Velocity.sqrMagnitude>.0001f){Finish(false,"City passage accepted walking or exposed the player");return;}
                cityInputIgnored=true;
                Vector4 recovery=Shader.GetGlobalVector("_ReturnCityRecovery");
                if(recovery.w<.99f||Mathf.Abs(recovery.x-j.CityCentre.x)>.01f||Mathf.Abs(recovery.y-j.CityCentre.z)>.01f){Finish(false,"Actual city shader recovery was not enabled");return;}
                if(j.SceneElapsed<ReturnReverseJourney.CityPassAt+.15f&&recovery.z<.01f)cityRecoveryStarted=true;
                if(j.SceneElapsed>29.45f&&recovery.z>.999f)cityRecoveryCompleted=true;
            }
            if(j.Scene==Movement.Hospital){
                if(hospitalFaceSamples<hospitalFaceAt.Length&&j.SceneElapsed>hospitalFaceAt[hospitalFaceSamples]){if(!j.HospitalFaceClear(out float gap)){Finish(false,"Hospital rendered arm overlaps the face or has less than six pixels of separation");return;}hospitalMinimumFaceGap=Mathf.Min(hospitalMinimumFaceGap,gap);hospitalFaceSamples++;}
                if(j.SceneElapsed>8.6f&&hospitalHandStartDistance<0)hospitalHandStartDistance=j.HospitalHandDistance;
                if(j.SceneElapsed>8.6f&&!hospitalReachStartFrame){if(!j.HospitalMotherFaceInView){Finish(false,"Hospital mother head is outside the early reach camera frame");return;}hospitalReachStartFrame=game.TryRequestFrame("Hospital-Reach-Start");}
                if(j.SceneElapsed>12.2f&&!hospitalHandAdvanceObserved){hospitalHandAdvance=hospitalHandStartDistance-j.HospitalHandDistance;if(hospitalHandStartDistance<0||hospitalHandAdvance<.25f){Finish(false,"Hospital reaching hand did not approach the actual patient camera");return;}hospitalHandAdvanceObserved=true;}
                if(!hospitalNurseFramed&&j.SceneElapsed>6){if(!j.HospitalSpeakerVisible(false)){Finish(false,"Hospital nurse is outside the actual camera frame");return;}hospitalNurseFramed=true;}
                if(!hospitalReachFramed&&j.SceneElapsed>12){if(!j.HospitalSpeakerVisible(true)){Finish(false,"Hospital mother or reaching hand is outside the actual camera frame");return;}hospitalReachFramed=true;}
            }
            cameraSampleReady=true;cameraWasFruit=fruit;cameraScene=j.Scene;cameraLastPosition=j.CameraPosition;cameraLastRotation=j.CameraRotation;cameraLastFov=j.CameraFieldOfView;
        }
        string GroundSkinEvidenceDirectory()
        {
            string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");
            if(string.IsNullOrEmpty(output))output=Path.Combine(Application.persistentDataPath,"ReturnEvidence");
            Directory.CreateDirectory(output);return output;
        }
        void SaveGroundSkinReview(bool force)
        {
            var skin=game.journey.SkinReview;
            if(!force&&(skin.leaf==savedSkinLeaf&&skin.island==savedSkinIsland||Time.unscaledTime<skinSavedAt+1))return;
            File.WriteAllText(Path.Combine(GroundSkinEvidenceDirectory(),"ground-skin-review.json"),JsonUtility.ToJson(skin,true));
            savedSkinLeaf=skin.leaf;savedSkinIsland=skin.island;skinSavedAt=Time.unscaledTime;
        }
        public void Observe(float dt)
        {
            simulated+=dt;
            if(escapeOnly&&!meteorContactChecked&&game.Chapter==Movement.Rupture){meteorContactChecked=true;if(!ReviewMeteorContact()){FinishEscape(false,"Meteor landing/aftermath contact regression failed");return;}}
            if(escapeOnly&&!fallingMeteorFrame&&game.Chapter==Movement.Rupture){
                var view=game.GetComponentInChildren<Camera>();if(view!=null)foreach(var hazard in game.world.hazards)if(hazard.debris&&hazard.visual.gameObject.activeInHierarchy){
                    Vector3 screen=view.WorldToViewportPoint(hazard.visual.position);if(screen.z>0&&screen.x>.05f&&screen.x<.95f&&screen.y>.1f&&screen.y<.9f){fallingMeteorFrame=game.TryRequestFrame("Rupture-Falling-Meteor");break;}
                }
            }
            if(hospitalOnly&&game.journey.Active&&game.Chapter==Movement.StillWater&&!landscapeObserved)
                landscapeObserved=game.journey.QuietLandscape&&!game.journey.CameraIsOrthographic;
            if(hospitalOnly&&(game.Chapter==Movement.Hospital||game.Chapter==Movement.Credits)){ObserveHospital(dt);return;}
            if(streetOnly&&game.Chapter==Movement.Reunion){if(!streetCompleted)ObserveStreet(dt);return;}
            if(travellerOnly&&game.Chapter==Movement.Reunion){FinishTraveller(true,"Three-dimensional traveller movement and fruit interaction completed");return;}
            if(travellerOnly&&game.journey.Active&&game.Chapter==Movement.StillWater)
            {
                var j=game.journey;
                if(!j.QuietTraveller||!j.TravellerVolumeVisible||!j.TravellerDetailsConcealed){Finish(false,"Quiet traveller volume or concealed anatomical details missing");return;}
                travellerVolumeObserved|=!j.CameraIsOrthographic&&j.TravellerVolumeVisible;
                if(j.LeafBoarding)travellerBoardHands|=j.TravellerHandsVisible;
                else if(j.FruitActionTime>=0)travellerFruitHands|=j.TravellerHandsVisible;
                else if(j.Velocity.sqrMagnitude>.01f&&!j.Resting)
                {
                    if(j.TravellerHandsVisible){Finish(false,"Hands remained visible during ordinary third-chapter movement");return;}
                    if(j.Position.y<-.25f)travellerSwimHidden=true;else travellerWalkHidden=true;
                }
            }
            if(game.Chapter==Movement.Fragments)
            {
                var lighting=game.world;
                if(!game.InTransition&&lighting.RecoveryReceiverCount>=20)branchLuminosities[game.Excursion]=lighting.BranchLuminosity;
                int deposit=game.Fragments-1;
                if(deposit>=0&&deposit<4&&lighting.RadioRevealActive)
                {
                    radioRevealOutward[deposit]|=lighting.GreyAtDistance(2)>lighting.GreyAtDistance(20)+.00005f;
                    radioShakeObserved[deposit]|=game.RadioCameraShakeMagnitude>.002f;
                }
            }
            if(game.Chapter==Movement.Fragments&&game.Carrying)
            {
                int route=game.Excursion;
                if(carryRoute==route&&carryFailures==game.Failures)
                {
                    float step=Vector2.Distance(game.motor.position,carryPosition);maximumCarryStep[route]=Mathf.Max(maximumCarryStep[route],step);
                    if(game.ReturnDescent)returnTravel[route]+=Mathf.Abs(game.motor.position.x-carryPosition.x);
                }
                carryRoute=route;carryPosition=game.motor.position;carryFailures=game.Failures;
            }
            else carryRoute=-1;
            if(fragmentsOnly&&(!clientJourney||!clientFragmentsComplete)&&game.Chapter==Movement.Rupture&&!game.InTransition&&colourReturnPresented){FinishFirstStage(true,"Four physical returns, gradual grey reveals and the white-to-colour handoff completed");return;}
            if(!ObserveAirBrake())return;
            if(!ObserveBurstBuffer())return;
            if(!ObserveLowerEntry())return;
            if(!ReviewLeafBoarding(game.journey,dt))return;
            if(!ReviewShoreWalk(game.journey,dt))return;
            if(memoryWaitRoute>=0)
            {
                if(game.Carrying||game.world.returnOpen||!game.CanRememberFragment)
                {Finish(false,"A memory was collected without a deliberate grounded interaction");return;}
                memoryWaitSeconds[memoryWaitRoute]+=dt;
                if(memoryWaitRoute==0&&!memoryPromptFrame&&memoryWaitSeconds[0]>=.35f&&game.ContextHint()=="E  ·  Remember this voice")
                {memoryPromptFrame=true;game.RequestFrame("Fragments-Remember");}
                memoryWaitRoute=-1;
            }
            if(memoryAirborneAttemptRoute>=0)
            {
                if(game.motor.Grounded||game.Carrying||game.world.returnOpen)
                {Finish(false,"An airborne memory interaction was accepted");return;}
                memoryAirborneRejected=true;memoryAirborneAttemptRoute=-1;
            }
            if(memoryPickupAttemptRoute>=0)
            {
                if(!game.Carrying||!game.world.returnOpen||memoryWaitSeconds[memoryPickupAttemptRoute]<.55f||game.ActiveStoryLine!=ReturnGame.Memories[memoryPickupAttemptRoute])
                {Finish(false,"The deliberate memory interaction did not trigger its voice and world change");return;}
                deliberateMemories[memoryPickupAttemptRoute]=true;memoryPickupAttemptRoute=-1;
            }
            if(game.FragmentsReviewActive&&game.TuningRoute>0)
            {
                int turn=game.TuningRoute-1;var camera=game.GetComponentInChildren<Camera>();
                float degrees=Mathf.DeltaAngle((game.TuningRoute-2)*90,camera.transform.eulerAngles.y);branchTurnDegrees[turn]=Mathf.Max(branchTurnDegrees[turn],degrees);
                bool upright=camera.orthographic&&Mathf.Abs(Mathf.DeltaAngle(0,camera.transform.eulerAngles.z))<.05f;
                if(!branchTurnFrames[turn]&&degrees>35&&degrees<55&&upright)
                {branchTurnFrames[turn]=true;game.RequestFrame("Fragments-Turn-"+turn);}
                if(game.TuningProgress>.6f&&Mathf.Abs(degrees-90)<.05f&&upright&&(game.world.beacon.position-game.world.beacon.localPosition).sqrMagnitude<.00001f)branchTurnComplete[turn]=true;
            }
            if(game.FragmentsReviewActive&&game.Chapter==Movement.Fragments&&game.Excursion>0&&game.ConnectionCaptionActive&&!string.IsNullOrEmpty(game.ActiveStoryLine))
            {
                int connection=game.Excursion-1;connectionCaptionSeconds[connection]+=dt;
                if(!connectionCaptionFrames[connection]&&connectionCaptionSeconds[connection]>=1)
                {connectionCaptionFrames[connection]=true;game.RequestFrame("Fragments-Connection-"+connection);}
            }
            if(memoryCaptionRoute>=0)
            {
                float age=simulated-memoryCaptionStartedAt;
                bool same=game.MemoryCaptionProtected&&game.ActiveStoryLine==rememberedCaption;
                if(age<7.85f&&!same){Finish(false,"Memory caption replaced before reading time on route "+memoryCaptionRoute);return;}
                if(age<8&&same)memoryCaptionSeconds[memoryCaptionRoute]+=dt;
                if(age>=8.05f)
                {
                    if(game.MemoryCaptionProtected){Finish(false,"Memory caption did not release after reading time");return;}
                    memoryCaptionFull[memoryCaptionRoute]=memoryCaptionSeconds[memoryCaptionRoute]>=7.85f;memoryCaptionRoute=-1;
                }
            }
            if(game.FragmentsReviewRequested&&game.Chapter==Movement.Fragments&&game.Failures>(retryChecked?1:0))
            {WriteFragmentsRetryTrace();Finish(false,"Unplanned Fragments retry at "+game.motor.position+"; stop before repeating the failed path");return;}
            if(game.FragmentsReviewActive)
            {
                if(fragmentsWallStart<0)fragmentsWallStart=Time.realtimeSinceStartup;
                fragmentsSimulated+=dt;fragmentsWallSeconds=Time.realtimeSinceStartup-fragmentsWallStart;
            }
            if(airborneAttemptRoute>=0)
            {
                if(game.Excursion!=airborneAttemptRoute||game.Fragments!=airborneAttemptRoute||game.InTransition)
                {Finish(false,"An airborne interaction deposited a fragment before reaching the radio");return;}
                airborneRejected[airborneAttemptRoute]=true;airborneAttemptRoute=-1;
            }
            if(game.Chapter==Movement.Fragments)
            {
                int route=game.Excursion;var w=game.world;
                int charge=game.Fragments;
                if(game.FragmentsReviewActive&&charge>=0&&charge<radioChargeFrames.Length&&!radioChargeFrames[charge]&&!game.Carrying&&Mathf.Abs(game.motor.position.x-w.radioX)<6&&(charge==4?game.FirstStageWhiteAlpha>=.99999f:!game.InTransition))
                    radioChargeFrames[charge]=game.TryRequestFrame("Radio-Charge-"+charge);
                if(game.FragmentsReviewActive&&!placeCaptionFrames[route]&&game.PlaceCaptionShown)
                {
                    Vector3 point=game.PlaceCaptionViewport;
                    if(game.Carrying||game.ActiveStoryLine!=ReturnGame.Places[route]||point.z<=0||point.x<=0||point.x>=1||point.y<=0||point.y>=1)
                    {Finish(false,"Place caption was not shown with its scene before pickup on route "+route);return;}
                    placeCaptionFrames[route]=true;game.RequestFrame("Fragments-Place-"+route);
                }
                var gapMotor=game.motor;
                if(route==3&&game.Carrying&&game.ReturnDescent&&gapMotor.Grounded&&!returnBridgeFrame&&gapMotor.support.returnPath&&gapMotor.support.y>0&&gapMotor.support.y<w.returnEntry.y-.2f&&gapMotor.support.Right<w.returnEntry.x-4)
                {returnBridgeFrame=true;game.RequestFrame("Fragments-Return-Bridge");}
                if(game.Carrying||game.InTransition){longGapDeparture=null;longGapBurst=false;}
                else if(gapMotor.Grounded)
                {
                    var landed=gapMotor.support;
                    bool timedDeparture=longGapDeparture!=null&&(longGapDeparture.kind==SurfaceKind.Fading||longGapDeparture.kind==SurfaceKind.Collapse);
                    bool challengeLanding=timedDeparture&&longGapRoute==route&&landed!=longGapDeparture&&(landed.kind==SurfaceKind.Stone||landed.kind==SurfaceKind.Collapse)&&landed.Left-longGapDepartureRight>=6.8f&&landed.y-longGapDeparture.y>.8f;
                    if(challengeLanding)
                    {
                        if(!longGapBurst||!gapMotor.BurstReady){Finish(false,"A long rising crossing did not consume and renew the air burst");return;}
                        longBurstCrossings[route]++;
                    }
                    longGapDeparture=landed;longGapDepartureRight=landed.Right;longGapRoute=route;longGapBurst=false;
                }
                else if(longGapDeparture!=null&&longGapRoute==route&&gapMotor.Bursting&&!gapMotor.BurstReady)
                {
                    longGapBurst=true;
                    bool timedDeparture=longGapDeparture.kind==SurfaceKind.Fading||longGapDeparture.kind==SurfaceKind.Collapse;
                    bool challengeFlight=timedDeparture&&landing!=null&&(landing.kind==SurfaceKind.Stone||landing.kind==SurfaceKind.Collapse)&&landing.Left-longGapDepartureRight>=6.8f&&landing.y-longGapDeparture.y>.8f;
                    if(challengeFlight&&route==2&&!burstCrossingFrame){burstCrossingFrame=true;game.RequestFrame("Fragments-Burst-Crossing");}
                    if(challengeFlight&&route==3&&longBurstCrossings[3]>=1&&!burstChainFrame){burstChainFrame=true;game.RequestFrame("Fragments-Burst-Chain");}
                }
                if(game.FragmentsReviewActive&&route==1&&burstPracticeStep>=2&&!burstPracticeRenewFrame)
                {
                    burstPracticeTravel=Mathf.Max(burstPracticeTravel,Mathf.Abs(game.motor.position.x-burstPracticeStartX));
                    if(!burstPracticeBurstFrame&&game.motor.Bursting&&!game.motor.Grounded&&!game.motor.BurstReady)
                    {burstPracticeBurstFrame=true;game.RequestFrame("Fragments-Burst-Practice");}
                    if(!burstPracticeRenewFrame&&game.BurstLessonStage==3&&game.motor.Grounded&&game.motor.BurstReady&&game.motor.support==w.surfaces[0])
                    {burstPracticeRenewFrame=true;game.RequestFrame("Fragments-Burst-Renewed");}
                }
                if(game.FragmentsReviewActive&&game.Carrying&&!memoryCaptionStarted[route])
                {
                    memoryCaptionStarted[route]=true;memoryCaptionRoute=route;memoryCaptionStartedAt=simulated;rememberedCaption=game.ActiveStoryLine;memoryCaptionSeconds[route]=dt;
                    if(!game.MemoryCaptionProtected||string.IsNullOrEmpty(rememberedCaption)){Finish(false,"Pickup did not show its memory caption");return;}
                }
                if(game.FragmentsReviewActive&&route==0&&game.Carrying&&game.MemoryCaptionProtected&&!warmCupFrame)
                    warmCupFrame=game.TryRequestFrame("Fragments-Warm-Cup");
                if(game.FragmentsReviewActive&&route==2&&!game.Carrying)
                {
                    if(!stationApproachFrame&&game.motor.position.x>41&&game.motor.position.x<48)
                        stationApproachFrame=game.TryRequestFrame("Fragments-Station-Approach");
                    if(!stationRideFrame&&game.motor.Grounded&&game.motor.support!=null&&game.motor.support.kind==SurfaceKind.Ferry)
                        stationRideFrame=game.TryRequestFrame("Fragments-Station-Ride");
                }
                if(game.FragmentsReviewActive&&route>0&&!game.InTransition&&!game.Carrying&&game.ChapterElapsed>=1.6f&&game.ChapterElapsed<2)
                {
                    if(Mathf.Abs(w.gesture)>.001f){Finish(false,"Pickup gesture leaked into branch "+route);return;}
                    branchGestureClear[route-1]=true;
                    if(route==1&&game.MemoryCaptionProtected&&!memoryHandoffFrame){memoryHandoffFrame=true;game.RequestFrame("Fragments-Memory-Handoff");}
                }
                if(game.FragmentsReviewActive)
                {
                    for(int i=0;i<memoryVoiceOutput.Length;i++)if(game.MemoryVoiceOutputObserved(i))
                    {
                        memoryVoiceOutput[i]=true;
                        memoryVoiceTravel[i]=Mathf.Max(memoryVoiceTravel[i],game.MemoryVoiceAudibleTravel(i));
                        memoryVoiceAudioSamples[i]=System.Math.Max(memoryVoiceAudioSamples[i],game.MemoryVoiceAudiblePositionSamples(i));
                        var window=game.MemoryVoicePositionWindow(i);
                        if(window!=null&&window.coherentAudioSamples>0&&(memoryVoiceWindows[i]==null||window.coherentAudioSamples>=memoryVoiceWindows[i].coherentAudioSamples))memoryVoiceWindows[i]=window;
                        memoryVoiceMovement[i]|=memoryVoiceTravel[i]>.25f;
                    }
                    if(game.Carrying&&memoryVoiceOutput[route]&&!memoryPresenceFrames[route]){memoryPresenceFrames[route]=true;game.RequestFrame("Fragments-Presence-"+route);}
                }
                if(game.FragmentsReviewActive&&!game.Carrying&&!game.InTransition)
                    foreach(ReturnSurface surface in w.surfaces)
                    {
                        if(surface.returnPath||!surface.solid||surface.age<.2f||surface.age>.5f)continue;
                        Vector3 point=game.GetComponentInChildren<Camera>().WorldToViewportPoint(surface.visual.position);
                        if(point.z<=0||point.x<0||point.x>1||point.y<0||point.y>1)continue;
                        if(route==2&&surface.kind==SurfaceKind.Fading&&!fadingFootingFrame)
                        {fadingFootingFrame=true;game.RequestFrame("Fragments-Fading-Footing");}
                        if(route==3&&surface.kind==SurfaceKind.Collapse&&!collapseFootingFrame)
                        {collapseFootingFrame=true;game.RequestFrame("Fragments-Collapse-Footing");}
                    }
            }
            if(game.EscapeReviewActive)
            {
                if(escapeWallStart<0)escapeWallStart=Time.realtimeSinceStartup;
                escapeSimulated+=dt;escapeWallSeconds=Time.realtimeSinceStartup-escapeWallStart;
            }
            if(game.EscapeReviewRequested&&(game.Chapter==Movement.Rupture||game.Chapter==Movement.StillWater))
                for(int i=0;i<ruptureVoiceOutput.Length;i++)if(game.RuptureVoiceOutputObserved(i))ruptureVoiceOutput[i]=true;
            if(game.EscapeReviewRequested&&game.Chapter==Movement.StillWater&&!ruptureSpeechStoppedOnWater)
            {
                for(int i=0;i<ruptureVoiceOutput.Length;i++)if(game.RuptureVoiceLevel(i)>.003f){Finish(false,"Escape speech continued into Still Water");return;}
                ruptureSpeechStoppedOnWater=true;
            }
            if(escapeOnly&&(!clientJourney||!clientEscapeComplete)&&game.Chapter==Movement.StillWater&&ruptureSpeechStoppedOnWater){FinishEscape(true,"Timed forecast rain, volcano ejection and the escape completed");return;}
            if(game.Chapter==Movement.Rupture&&!game.InTransition)
            {
                var w=game.world;
                if(game.EscapeReviewRequested)
                {
                    if(w.escapeCheckpoint==null){Finish(false,"Rupture midpoint checkpoint is missing");return;}
                    ruptureMidpointCheckpoint=new Vector2((w.escapeCheckpoint.Left+w.escapeCheckpoint.Right)*.5f,w.escapeCheckpoint.y);
                    if(game.motor.support==w.escapeCheckpoint&&game.motor.position.x>=ruptureMidpointCheckpoint.x)ruptureCheckpointReached=true;
                    Vector2 expected=ruptureCheckpointReached?ruptureMidpointCheckpoint:new Vector2(6,0);
                    if((w.checkpoint-expected).sqrMagnitude>.001f){Finish(false,"Rupture saved progress outside the start/midpoint checkpoints: "+w.checkpoint);return;}
                    if(ruptureCheckpointReached)ruptureMidpointCheckpointSamples++;else ruptureStartCheckpointSamples++;
                }
                if(game.EscapeReviewRequested)
                {
                    var fractureCamera=game.GetComponentInChildren<Camera>();
                    foreach(var shelf in w.surfaces)
                    {
                        if(shelf.kind==SurfaceKind.Collapse&&shelf.age>3.65f)
                        {
                            if(shelf.solid){Finish(false,"Crumbling escape platform regenerated before a checkpoint retry");return;}
                            crumblingStayedBroken=true;
                        }
                        if(!ruptureFractureFrame&&w.IsRuptureFracturing(shelf))
                        {
                            Vector3 view=fractureCamera.WorldToViewportPoint(shelf.visual.position+Vector3.down*.7f);
                            if(view.z>0&&view.x>.12f&&view.x<.88f&&view.y>.15f&&view.y<.9f&&game.TryRequestFrame("Rupture-Fracture"))ruptureFractureFrame=true;
                        }
                    }
                    whiteJumpGap=w.goalX-ReturnWorld.WhiteOpeningHalfWidth-w.escapeLedge.Right;
                    if(whiteJumpGap<=0){Finish(false,"Ground still runs through the white opening");return;}
                    if(!whiteLeapFrame&&!game.motor.Grounded&&game.motor.position.x>w.escapeLedge.Right+.25f&&game.motor.position.x<w.goalX-ReturnWorld.WhiteOpeningHalfWidth)
                    {whiteLeapFrame=true;game.RequestFrame("Rupture-White-Leap");}
                }
                // Retry intentionally restores every support; the next Step reapplies the front.
                bool justRetried=game.Failures!=previousFailures;
                if(escapeOnly&&justRetried&&(!ruptureWaiting||ruptureRetryChecked))
                {Finish(false,"Unplanned meteor/chase retry: "+w.LastContactCause);return;}
                if(w.frontX>-90&&!justRetried)
                {
                    foreach(var s in w.surfaces)if(s.Right<w.frontX)
                    {
                        if(s.solid){Finish(false,"Collapsed Rupture ground became solid again");return;}
                        if(firstCollapsedAt<0)firstCollapsedAt=w.clock;
                    }
                    if(firstCollapsedAt>=0&&w.clock-firstCollapsedAt>4)rupturePersistent=true;
                }
                if(ruptureWaiting&&!ruptureRetryChecked&&game.Failures>ruptureWaitFailures)
                {
                    bool restored=true;foreach(var s in w.surfaces)restored&=s.solid&&s.age<0;
                    float checkpointError=(game.motor.position-w.checkpoint).sqrMagnitude;
                    bool frontSafe=w.frontX<=w.checkpoint.x-20;
                    if(w.LastContactCause!="pursuit"||!rupturePersistent||!restored||checkpointError>.001f||!frontSafe)
                    {Finish(false,"Pursuit retry failed: cause="+w.LastContactCause+", persistent="+rupturePersistent+", restored="+restored+", checkpointError="+checkpointError.ToString("F3")+", frontSafe="+frontSafe);return;}
                    ruptureRetryChecked=true;landing=null;
                    ruptureStartRetryDistance=Mathf.Abs(lastRupturePosition.x-game.motor.position.x);
                }
                if(!chaseFrame&&ruptureWaiting&&!ruptureRetryChecked&&w.frontX>game.motor.position.x-12)
                {chaseFrame=true;game.RequestFrame("Rupture-Chase");}
                if(!collapseFrame&&ruptureRetryChecked&&game.motor.position.x>130)
                {
                    var camera=game.GetComponentInChildren<Camera>();
                    Vector3 threat=camera.WorldToViewportPoint(w.root.TransformPoint(new Vector3(w.frontX,game.motor.position.y+1,0)));
                    escapeThreatVisible=threat.z>0&&threat.x>.03f&&threat.x<.97f&&threat.y>0&&threat.y<1;
                    if(game.EscapeReviewRequested&&!escapeThreatVisible){Finish(false,"Collapse remained outside the escape camera");return;}
                    collapseFrame=true;game.RequestFrame("Rupture-Collapse");
                }
                if(game.RuptureStoryBeats==5)
                {
                    var camera=game.GetComponentInChildren<Camera>();
                    Vector3 opening=camera.WorldToViewportPoint(w.door.position+Vector3.up*3.8f);
                    openingInFrame=opening.z>0&&opening.x>.05f&&opening.x<.95f&&opening.y>.05f&&opening.y<.95f;
                    if(game.EscapeReviewRequested&&!openingInFrame){Finish(false,"Final jump voice arrived before the visible opening");return;}
                    ruptureStoryChecked=true;
                    if(!openingFrame){openingFrame=true;game.RequestFrame("Rupture-Opening");}
                }
                if(game.EscapeReviewRequested&&ruptureRetryChecked&&!ruptureMidpointRetryChecked&&game.motor.Grounded&&game.motor.position.x>=152&&game.motor.support.kind==SurfaceKind.Stone&&game.motor.support.width>=10)
                {
                    Vector2 from=game.motor.position;int failures=game.Failures;game.Retry();
                    ruptureMidpointRetryDistance=Mathf.Abs(from.x-game.motor.position.x);
                    bool restored=true;foreach(var surface in w.surfaces)restored&=surface.solid&&surface.age<0;
                    if(!ruptureCheckpointReached||(game.motor.position-ruptureMidpointCheckpoint).sqrMagnitude>.001f||game.Failures!=failures+1||ruptureMidpointRetryDistance<40||!restored||w.frontX>w.checkpoint.x-20)
                    {Finish(false,"Midpoint retry did not restore a meaningful, safe section of Rupture");return;}
                    ruptureMidpointRetryChecked=true;landing=null;
                }
                lastRupturePosition=game.motor.position;
            }
            if(game.MotionReviewActive){if(returnWallStart<0)returnWallStart=Time.realtimeSinceStartup;returnSimulated+=dt;}
            if(game.journey.Active){furthestZ=Mathf.Max(furthestZ,game.journey.Position.z);furthestX=Mathf.Max(furthestX,Mathf.Abs(game.journey.Position.x));maxOrbit=Mathf.Max(maxOrbit,game.journey.Yaw);}
            if(game.journey.Active&&game.Chapter==Movement.StillWater)
            {
             var j=game.journey;
             if(!waterEntered&&j.Position.y<-.25f){waterEntered=true;waterEnteredZ=j.Position.z;}
             if(waterEntered&&!rested3D&&j.Position.y<-.25f)
             {
              if(game.MotionReviewActive)swimReviewSeconds+=dt;
              waterSwimDistance=Mathf.Max(waterSwimDistance,Mathf.Abs(j.Position.z-waterEnteredZ));
              Vector4 bodyMotion=Shader.GetGlobalVector("_ReturnWaterBodyMotion");
              waterBodyMotionObserved|=waterSwimDistance>.75f&&bodyMotion.w>.95f&&new Vector2(bodyMotion.x-j.Velocity.x,bodyMotion.y-j.Velocity.z).sqrMagnitude<.0025f;
              if(!waterSwimFrame&&waterSwimDistance>.75f)waterSwimFrame=game.TryRequestFrame("StillWater-Swimming");
              Vector4 leftHand=Shader.GetGlobalVector("_ReturnWaterHandLeft"),rightHand=Shader.GetGlobalVector("_ReturnWaterHandRight");
              if(!waterHandStrokeFrame&&!game.InTransition&&waterSwimDistance>.75f&&Mathf.Max(leftHand.z*leftHand.w,rightHand.z*rightHand.w)>.12f)waterHandStrokeFrame=game.TryRequestFrame("StillWater-Stroke");
              if(game.MotionReviewActive&&!game.InTransition&&waterSwimDistance>2)
              {
               float phase=j.SwimPhase;int sample=phase>=.16f&&phase<=.22f?0:phase>=.58f&&phase<=.64f?1:phase>=.84f&&phase<=.9f?2:-1;
               if(sample>=0&&!swimCycleFrames[sample]&&game.TryRequestFrame(sample==0?"StillWater-Pull":sample==1?"StillWater-Kick":"StillWater-Glide"))
               {swimCycleFrames[sample]=true;swimCyclePhases[sample]=phase;swimCycleShapes[sample]=j.SwimShape;swimCameraDistances[sample]=j.SwimCameraDistance;swimFootSpeeds[sample]=j.SwimFootSpeed;swimPlanarSpeeds[sample]=new Vector2(j.Velocity.x,j.Velocity.z).magnitude;swimPostures[sample]=j.SwimPosture;swimFootPoses[sample]=j.DescribeSwimFeet();}
              }
             }
             if(j.CanRest&&!j.Resting&&!rested3D)
             {
              if(!leafReadySeen){leafReadySeen=true;leafReadyZ=j.LeafZ;}
              leafIdleSeconds+=dt;leafIdleAdvance=Mathf.Max(leafIdleAdvance,Mathf.Abs(j.LeafZ-leafReadyZ));
              Vector4 idleBody=Shader.GetGlobalVector("_ReturnWaterBodyMotion"),idleLeaf=Shader.GetGlobalVector("_ReturnWaterLeafMotion");
              leafIdleMotionQuiet|=leafIdleSeconds>.45f&&idleBody.w<.01f&&new Vector2(idleLeaf.x,idleLeaf.y).sqrMagnitude<.0001f;
              if(!leafReadyFrame&&leafIdleSeconds>.3f)leafReadyFrame=game.TryRequestFrame("Leaf-Ready");
             }
             if(j.Resting)
             {
              leafDriftDistance=Mathf.Max(leafDriftDistance,j.LeafZ-leafReadyZ);
              Vector4 leafMotion=Shader.GetGlobalVector("_ReturnWaterLeafMotion");
              leafWaterMotionObserved|=leafMotion.y>1.1f&&Mathf.Abs(leafMotion.z-2.1f*ReturnVolumeArt.LeafPlanScale)<.001f&&Mathf.Abs(leafMotion.w-2.9f*ReturnVolumeArt.LeafPlanScale)<.001f;
              if(game.ActiveStoryLine.Contains("I let the leaf carry me."))
              {
               leafRestCaptionSeconds+=dt;
               if(!leafDriftFrame&&j.RestPoseAmount>=.99f)leafDriftFrame=game.TryRequestFrame("Leaf-Drifting");
              }
             }
                if(j.OnIsland&&j.FruitActionTime<0&&new Vector2(j.Velocity.x,j.Velocity.z).magnitude>.1f){if(islandWalkTracking){float distance=new Vector2(j.Position.x-islandWalkPrevious.x,j.Position.z-islandWalkPrevious.z).magnitude;islandWalkSeconds+=dt;islandWalkDistance+=distance;if(islandWalkSeconds>.55f&&dt>0)islandWalkMaxSpeed=Mathf.Max(islandWalkMaxSpeed,distance/dt);}islandWalkTracking=true;islandWalkPrevious=j.Position;if(islandWalkSeconds>.65f&&!islandWalkFrame)islandWalkFrame=game.TryRequestFrame("Island-Walk");if(islandWalkSeconds>.55f&&!ReviewIslandWeightTransfer(j))return;}else islandWalkTracking=false;
             if(rested3D&&!j.Resting&&j.RestPoseAmount==0&&j.LeafZ>=ReturnVolumeArt.LeafDockZ-.05f&&!j.CanFruit&&!islandApproachFrame){
               islandApproachFrame=game.TryRequestFrame("Island-Approach");
               if(islandApproachFrame){
                 var scale=new IslandScaleEvidence{islandSpan=j.IslandSpan,leafSpan=j.LeafSpan,ratio=j.IslandSpan/j.LeafSpan,leafNoseZ=j.LeafNoseZ};
                 scale.passed=scale.leafSpan>0&&scale.leafSpan<2.6f&&scale.ratio>=3&&Mathf.Abs(scale.leafNoseZ-23.105f)<.06f&&ReturnVolumeArt.IslandRadius(0)>=3&&ReturnVolumeArt.IslandRadius(0)<=4;
                 string directory=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(directory))File.WriteAllText(Path.Combine(directory,"island-scale.json"),JsonUtility.ToJson(scale,true));
                 if(!scale.passed){Finish(false,"Island/leaf proportion or preserved dock contact failed");return;}
               }
             }
            }
            if(game.journey.Active&&game.Chapter==Movement.StillWater){float a=game.journey.RestPoseAmount;if(game.journey.Resting&&a>.43f&&a<.48f&&!sittingFrame){sittingFrame=true;game.RequestFrame("Leaf-Sitting");}if(game.journey.Resting&&a>.74f&&a<.8f&&!recliningFrame){recliningFrame=true;game.RequestFrame("Leaf-Reclining");}if(!game.journey.Resting&&a>.42f&&a<.47f&&!risingFrame){risingFrame=true;game.RequestFrame("Leaf-Rising");}}
            if(!orbitFrame&&game.journey.Active&&game.journey.Yaw>=60&&game.journey.RestPoseAmount>=1){orbitFrame=true;game.RequestFrame("StillWater-Orbit");}
            if(game.journey.Active&&(game.Chapter==Movement.StillWater||game.Chapter==Movement.Reunion)){float c=game.journey.Colours;if(c>.3f&&c<.34f&&!yellowFrame){yellowFrame=true;game.RequestFrame("Colour-Yellow");}if(c>.62f&&c<.67f&&!redFrame){redFrame=true;game.RequestFrame("Colour-Red");}if(c>.985f&&!blueFrame){blueFrame=true;game.RequestFrame("Colour-Blue");}}
            if(game.journey.Active&&game.Chapter==Movement.StillWater&&game.journey.FruitActionTime>=0) {
             var j=game.journey;float t=j.FruitActionTime;if(t>=.95f&&!RecordFruitGroundPose("Fruit-Pick"))return;if(t>=3.3f&&!RecordFruitGroundPose("Fruit-Settled"))return;
             fruitScalePlausible|=j.FruitRadius>=.05f&&j.FruitRadius<=.07f;
             if(t>.9f&&t<1.05f){fruitTreeTouchObserved|=j.FruitHandDistance<.085f;if(!fruitPickFrame)fruitPickFrame=game.TryRequestFrame("Fruit-Pick");}
             if(t>.4f&&t<1)fruitTasteDelayed|=!game.milestones.Contains("Ate the blue fruit")&&!game.ActiveStoryLine.Contains("The taste is familiar")&&j.Colours==0;
             if(t>1.2f&&t<2.7f&&j.FruitHandDistance<.075f)fruitHeldSeconds+=dt;
             if(t>2.2f&&t<2.7f){if(j.FruitMouthDistance<.055f&&game.ActiveStoryLine.Contains("The taste is familiar"))fruitMouthSeconds+=dt;}
             if(t>2.98f&&t<3.2f&&!fruitBiteFrame)fruitBiteFrame=game.TryRequestFrame("Fruit-Bite");
             if(game.MotionReviewActive&&t>1.92f&&t<2.05f&&!fruitAnticipationFrame)fruitAnticipationFrame=game.TryRequestFrame("Fruit-Anticipation");
             if(game.MotionReviewActive&&t>3.28f&&t<3.42f&&!fruitTasteFrame)fruitTasteFrame=game.TryRequestFrame("Fruit-Taste");
            }
            if(!islandFrame&&game.journey.Active&&game.journey.CanFruit){islandFrame=true;game.RequestFrame("Island-Fruit");}
            if(!reunionFrame&&game.Chapter==Movement.Reunion&&game.ChapterElapsed>5.5f){reunionFrame=true;game.RequestFrame("Reunion-Worlds");}
            if(game.Chapter==Movement.Reunion) {
                var j=game.journey;float t=game.ChapterElapsed;
                if(t>9.6f&&t<23.5f) {
                     if(j.RecallSolidHouses!=8||j.RecallHouseMinimumDepth>.01f||j.RecallPaperMaximumOpacity<.99f){Finish(false,"Recalled architecture did not flatten with the side view");return;}
                     architectureFlattened=true;
                }
                if(t>27.2f&&!cityFrame){cityFrame=true;game.RequestFrame("City-Crossroads");}
                if(t>29.2f&&!cityRecoveryFrame){cityRecoveryFrame=true;game.RequestFrame("City-Recovery");}
                if(t>7.5f&&!flatteningFrame){flatteningFrame=true;game.RequestFrame("Return-Flattening");}
                if(t>10.4f&&!flatFrame){flatFrame=true;game.RequestFrame("Return-3D-to-2D");}
                if(t>17&&!stageHandoffFrame){stageHandoffFrame=true;game.RequestFrame("Return-Stage-Handoff");}
                if(t>21&&!fragmentsFrame){fragmentsFrame=true;game.RequestFrame("Return-Fragments");}
                 if(t>25&&!depthFrame){depthFrame=true;game.RequestFrame("Return-Regaining-Depth");}
                 if(t>26.2f&&!architectureFrame){architectureFrame=true;solidHouseDepth=j.RecallHouseMinimumDepth;paperHouseOpacity=j.RecallPaperMaximumOpacity;architectureRestored=j.RecallSolidHouses==8&&solidHouseDepth>=4&&paperHouseOpacity<=.001f;if(!architectureRestored){Finish(false,"Recalled houses did not restore their actual rendered depth");return;}game.RequestFrame("Return-Restored-Architecture");}
                if(t>28.5f&&!uniteFrame){uniteFrame=true;game.RequestFrame("Return-Colours-Unite");}
                 if(t>ReturnReverseJourney.IslandReturnAt+.8f&&!assemblingFrame)
                     assemblingFrame=game.TryRequestFrame("Return-Door-Assembling");
                 if(game.MotionReviewActive&&j.DoorOpeningElapsed>.8f&&j.DoorOpeningElapsed<1.3f&&!doorOpeningFrame)
                     doorOpeningFrame=game.TryRequestFrame("Return-Door-Opening");
            }
            string label=game.Chapter+"-"+game.Excursion;
            if(label!=last){last=label;splits[label]=simulated;Debug.Log("[RETURN acceptance] "+label+" at "+simulated.ToString("F2")+" seconds");landing=null;framePending=true;}
            if(framePending&&game.ChapterElapsed>2){game.RequestFrame(label);framePending=false;}
            if(game.Chapter==Movement.Hospital&&hospitalClearSamples<hospitalClearAt.Length&&game.ChapterElapsed>hospitalClearAt[hospitalClearSamples]){bool clear;try{clear=game.journey.HospitalPeopleClearOfBed();}catch(System.Exception error){Finish(false,"Hospital geometry initialization failed: "+error.Message);return;}if(!clear){var contact=game.journey.LastHospitalContact;Finish(false,"Hospital actual bed/rail surface contact by "+contact.person+" near "+contact.nearestRigTransform+" at "+contact.hospitalLocal.ToString("F4"));return;}if(!game.journey.HospitalFeetClearOfFloor()){Finish(false,"Hospital shoe sole crossed the authored room floor");return;}hospitalClearSamples++;if(hospitalClearSamples==1)game.RequestFrame("Hospital-Approach");}
            if(!hospitalMotherTurnFrame&&game.Chapter==Movement.Hospital&&game.ChapterElapsed>9.35f&&game.ChapterElapsed<9.55f)hospitalMotherTurnFrame=game.TryRequestFrame("Hospital-Mother-Turn");
            if(!hospitalReassuranceFrame&&game.Chapter==Movement.Hospital&&game.ChapterElapsed>9.75f&&game.ChapterElapsed<10.1f)hospitalReassuranceFrame=game.TryRequestFrame("Hospital-Reassurance");
             if(!hospitalReliefBlinkFrame&&game.Chapter==Movement.Hospital&&game.ChapterElapsed>10.82f&&game.ChapterElapsed<11)hospitalReliefBlinkFrame=game.TryRequestFrame("Hospital-Relief-Blink");
             if(!hospitalOfferFrame&&game.Chapter==Movement.Hospital&&game.ChapterElapsed>11.25f&&game.ChapterElapsed<11.5f)hospitalOfferFrame=game.TryRequestFrame("Hospital-Hand-Offer");
            if(!hospitalOpeningFrame&&game.Chapter==Movement.Hospital&&game.ChapterElapsed>1.8f&&game.ChapterElapsed<2)hospitalOpeningFrame=game.TryRequestFrame("Hospital-Opening");
            if(!hospitalClosingFrame&&game.Chapter==Movement.Hospital&&game.ChapterElapsed>ReturnJourney3D.HospitalEyesCloseAt+1.2f)hospitalClosingFrame=game.TryRequestFrame("Hospital-Eyes-Closing");
            if(!awakeFrame&&game.Chapter==Movement.Hospital&&game.ChapterElapsed>8){awakeFrame=true;game.RequestFrame("Hospital-Awake");}
            if(!touchFrame&&game.Chapter==Movement.Hospital&&game.ChapterElapsed>12.2f){touchFrame=true;game.RequestFrame("Hospital-Reach");}
            if(!hospitalFamilyFramed&&game.Chapter==Movement.Hospital&&game.ChapterElapsed>=11&&game.ChapterElapsed<18&&game.journey.HospitalFamilyFacesInView){hospitalFamilyFramed=true;Debug.Log("[RETURN] Two family faces framed PASS");}
            if(game.Chapter==Movement.Hospital&&game.ChapterElapsed>=ReturnJourney3D.HospitalRecollectionAt&&game.ChapterElapsed<ReturnJourney3D.HospitalEyesCloseAt&&game.ActiveStoryLine.Contains("After my suicide attempt, I had been in a coma."))
            {
                hospitalRecollectionSeconds+=dt;
                if(hospitalRecollectionWallStart<0)hospitalRecollectionWallStart=Time.realtimeSinceStartup;
                hospitalRecollectionWallSeconds=Time.realtimeSinceStartup-hospitalRecollectionWallStart;
                if(!hospitalRecollectionFrame&&game.ChapterElapsed>12){hospitalRecollectionFrame=true;game.RequestFrame("Hospital-Recollection");}
            }
            if(game.Carrying!=wasCarrying)
            {
                wasCarrying=game.Carrying;landing=null;
                if(wasCarrying)
                {
                    game.RequestFrame("Memory-"+game.Excursion);
                    if(game.Excursion<3)foreach(var s in game.world.surfaces)
                        if(s.solid&&s.Left<=game.world.goalX&&s.Right>=game.world.goalX&&Mathf.Abs(s.y-(game.world.goalY-1.35f))<.01f){landing=s;break;}
                }
            }
            if(game.Failures!=previousFailures){previousFailures=game.Failures;landing=null;}
            // Explicit local failure exercises real reset instead of relying on a clean route only.
            if(!retryChecked&&game.Chapter==Movement.Fragments&&game.motor.position.x>21&&game.motor.Grounded)
            {
                Vector2 safe=game.world.checkpoint;int before=game.Failures;game.Retry();
                if((game.motor.position-safe).sqrMagnitude>.001f||game.Failures!=before+1)Finish(false,"Safe-ground retry failed");
                retryChecked=true;landing=null;
            }
            if(game.Completed)
            {
                completeTime+=dt;
                if(completeTime>3)
                {
                    if(!ruptureRetryChecked||!rupturePersistent||!ruptureStoryChecked)
                    {Finish(false,"Persistent pursuit, checkpoint retry or escape-story progression missing");return;}
                    int sent=game.milestones.FindAll(x=>x.StartsWith("Sent fragment ")).Count;
                    int carried=game.milestones.FindAll(x=>x.StartsWith("Carried fragment ")).Count;
                    if(sent!=0||carried!=4||Array.Exists(airborneRejected,x=>!x)||!PhysicalReturnsVerified())
                    {Finish(false,"Four physical radio returns were not completed");return;}
                    if(game.FragmentsReviewRequested&&(fragmentsSimulated<30||fragmentsWallSeconds<fragmentsSimulated*.85f||game.FragmentsReviewPausedSeconds>journalFragmentsPaused))
                    {Finish(false,"Fragments review was accelerated or paused");return;}
                    if(game.FragmentsReviewRequested&&Array.Exists(placeCaptionFrames,x=>!x))
                    {Finish(false,"A Fragments place was collected without its visible scene caption");return;}
                    if(!architectureFlattened||!architectureRestored||!game.journey.DoorColoursJoined||!game.milestones.Contains("The recovered colours formed the doorway")||!reverseSideView||!returnedToVolume||reverseStartX-reverseEndX<230||game.world.chapter!=Movement.Rupture||game.journey.RecallRuptureSupports!=game.world.surfaces.Count||game.journey.RecallFragmentSupports<15||game.journal.Count<11||bursts<4||game.Fragments!=4||furthestZ<24||furthestX<.3f||maxOrbit<59||!game.milestones.Contains("Remembered the hospital cause")||!game.milestones.Contains("Resting on the green leaf")||!game.milestones.Contains("Jumped into white")||!game.milestones.Contains("Ate the blue fruit"))
                    {Finish(false,"Required source-design interaction missing");return;}
                    if(islandWalkSeconds<.75f||islandWalkDistance<1.1f||islandWalkMaxSpeed<1.2f||islandWalkMaxSpeed>1.45f||!islandWalkFrame){Finish(false,"Original-design slow island walk was not observed in actual movement");return;}
                    if(!fruitCameraContinuous||!returnCameraContinuous||!returnHandoffFrame||!returnPullbackFrame)
                    {Finish(false,"Rendered fruit/return camera handoff changed aim, lens or position abruptly");return;}
                    if(!fruitScalePlausible||!fruitTreeTouchObserved||!fruitPickFrame||!fruitTasteDelayed||fruitHeldSeconds<1.2f||fruitMouthSeconds<.35f||!fruitBiteFrame)
                    {Finish(false,"Fruit hand/mouth contact or taste-after-bite story timing missing");return;}
                    if(!waterBodyMotionObserved||!waterHandStrokeFrame||!leafWaterMotionObserved||!leafIdleMotionQuiet||(game.MotionReviewActive&&!System.Array.TrueForAll(swimCycleFrames,captured=>captured)))
                    {Finish(false,"Water contact or requested pull/kick/glide review did not complete through swimming, idle boarding and leaf travel");return;}
                    if(game.MotionReviewActive&&swimPlanarSpeeds[1]<=swimPlanarSpeeds[2]+.15f)
                    {Finish(false,"Actual swimming speed did not increase during the kick and ease through the glide");return;}
                    if(!waterEntered||waterSwimDistance<2||!waterSwimFrame||leafIdleSeconds<.7f||leafIdleAdvance>.002f||leafDriftDistance<2||leafRestCaptionSeconds<6.85f||!leafReadyFrame||!leafDriftFrame||!islandApproachFrame)
                    {Finish(false,"Water entry, deliberate rest before drifting or island approach was not completed");return;}
                    if(!hospitalRecollectionFrame||hospitalRecollectionSeconds<7.85f||(game.MotionReviewActive&&hospitalRecollectionWallSeconds<hospitalRecollectionSeconds*.85f))
                    {Finish(false,"Hospital recollection did not receive its visible reading interval");return;}
                    if(game.MotionReviewActive&&(returnSimulated<45||Time.realtimeSinceStartup-returnWallStart<returnSimulated*.85f)){Finish(false,"Return motion review was accelerated");return;}
                    if(game.EscapeReviewRequested&&(escapeSimulated<15||escapeWallSeconds<escapeSimulated*.85f||game.EscapeReviewPausedSeconds>0||!escapeThreatVisible||!openingInFrame))
                    {Finish(false,"Escape motion, pursuit framing or final opening cue not verified");return;}
                    Finish(true,"Four outbound memory routes and physical radio returns, collapsing escape, 3D leaf/island/fruit, reverse colour recovery, dimensional reunion, doorway, hospital, retry and restart.");
                }
            }
            else if(simulated>600||game.Failures>25)Finish(false,"Traversal stalled at "+label+" position="+(game.journey.Active?game.journey.Position.ToString():game.motor.position.ToString()));
        }
        [Serializable] sealed class Result
        // Timing and visibility apply only to the opt-in normal-speed escape review.
        {public bool[] swimCycleFrames;public float[] swimCyclePhases,swimCameraDistances,swimFootSpeeds,swimPlanarSpeeds;public Vector4[] swimCycleShapes,swimPostures;public bool memoryVoiceTravelFromAudioBlocks;public int[] memoryVoiceAudioSamples;public bool islandWalkFrame;public float islandWalkSeconds,islandWalkDistance,islandWalkMaxSpeed;public bool hospitalGeometryControlsPassed;public int hospitalBedMeshCount,hospitalSkinVerticesChecked;public ReturnJourney3D.HospitalContact hospitalContact;public int hospitalFaceSamples;public float hospitalMinimumFaceGap;public bool hospitalReachStartFrame,hospitalHandAdvanceObserved;public float hospitalHandAdvance;public bool lowerBridgeReadable,lowerCircuitReadable;public bool hospitalNurseFramed,hospitalReachFramed;public int hospitalClearSamples,cityBuildings;public float cityPassTravel,cityPassSeconds;public bool cityFrame,cityRecoveryFrame,cityRecoveryStarted,cityRecoveryCompleted,cityInputIgnored;public bool returnBridgeFrame;public int[] longBurstCrossings;public bool burstCrossingFrame,burstChainFrame;public bool[] deliberateMemories;public float[] memoryWaitSeconds;public bool memoryAirborneRejected,memoryPromptFrame;public bool fruitCameraContinuous,returnCameraContinuous,returnHandoffFrame,returnPullbackFrame;public float fruitCameraAngleDegrees,fruitCameraLensDegrees,fruitCameraTravel,returnCameraAngleDegrees,returnCameraLensDegrees,returnCameraTravel;public bool fruitScalePlausible,fruitTreeTouchObserved,fruitPickFrame;public bool fruitTasteDelayed,fruitBiteFrame;public float fruitHeldSeconds,fruitMouthSeconds;public bool waterBodyMotionObserved,waterHandStrokeFrame,leafWaterMotionObserved,leafIdleMotionQuiet;public bool waterEntered,waterSwimFrame,leafReadyFrame,leafDriftFrame,islandApproachFrame;public float waterSwimDistance,leafIdleSeconds,leafIdleAdvance,leafDriftDistance,leafRestCaptionSeconds;public bool hospitalRecollectionFrame;public float hospitalRecollectionSeconds,hospitalRecollectionWallSeconds;public bool branchTurnsVerified;public float[] branchTurnDegrees;public bool[] branchTurnFrames,branchTurnComplete;public bool connectionReadingVerified;public float[] connectionCaptionSeconds;public bool[] connectionCaptionFrames;public bool normalSpeedFragments,burstPracticeBurstFrame,burstPracticeRenewFrame,burstLessonComplete;public float burstPracticeTravel;public bool[] memoryVoiceOutput,memoryVoiceMovement,memoryCaptionFull,branchGestureClear,ruptureVoiceOutput;public float[] memoryVoiceTravel,memoryCaptionSeconds;public int activeAudioListeners;public float fragmentsSimulatedSeconds,fragmentsWallSeconds,fragmentsPausedSeconds;public bool[] airborneRejected,transmissionsArrived;public int sentFragments,homeReturns;public bool normalSpeedEscape,escapeThreatVisible,openingInFrame,ruptureSpeechStoppedOnWater,whiteLeapFrame,finalEscapeVoiceCompleted;public bool ruptureFractureFrame,crumblingStayedBroken;public float whiteJumpGap,escapeSimulatedSeconds,escapeWallSeconds,escapePausedSeconds;public bool passed,restartPassed,reverseSideView,returnedToVolume,doorColoursJoined,normalSpeedReturn,architectureFlattened,architectureRestored,ruptureRetryChecked,rupturePersistent,ruptureStoryChecked;public float reverseDistance,returnSimulatedSeconds,returnWallSeconds,reviewPausedSeconds,solidHouseDepth,paperHouseOpacity;public int forwardRuptureSupports,replayedRuptureSupports,replayedFragmentSupports,solidHouses;public string detail;public float simulatedSeconds,spatialDistanceZ,spatialDistanceX,cameraOrbitDegrees;public int failures,bursts,memories;public string[] milestones;}
        [Serializable] sealed class FirstStageResult
        {
            public string scope="fragments",detail;
            public bool passed,physicalReturnsVerified,branchTurnsVerified,connectionReadingVerified,crossingsVerified,
                deliberateMemoriesVerified,voiceMovementVerified,captionsVerified,practiceVerified,normalSpeedVerified,colourEffectsVerified,interactionPosesVerified,returnGreyCoverageVerified;
            public int homeReturns,sentFragments,activeAudioListeners,failures;
            public float simulatedSeconds,wallSeconds,pausedSeconds;
            public bool[] radioHomeGrounded,radioHomeFrames,forwardReturnBroken,airborneRejected;
            public Vector2[] radioHomePositions;
            public float[] returnTravel,minimumReturnTravel,maximumCarryStep,memoryVoiceTravel;
            public bool[] memoryVoiceOutput,memoryVoiceMovement;
            public int[] memoryVoiceAudioSamples;
            public ReturnVoiceOutputMeter.PositionWindow[] memoryVoiceWindows;
            public int[] longBurstCrossings;
            public string[] milestones;
            public float[] branchLuminosities;
            public bool[] radioRevealOutward,radioShakeObserved;
            public float whiteHoldSimulatedSeconds,whiteHoldVisibleWallSeconds;
            public int whiteHoldFrames,colourReturnFrames;
            public bool[] pickupHandsObserved,pocketApproachObserved,pocketHiddenObserved,placementHandsObserved,placementAtRadioObserved,cleanFigureObserved;
            public int[] returnGreyRenderers,returnGoldGuides;
        }
        bool PhysicalReturnsVerified()
        {
            if(game.milestones.FindAll(x=>x.StartsWith("Carried fragment ")).Count!=4||
                game.milestones.Exists(x=>x.StartsWith("Sent fragment ")))return false;
            for(int i=0;i<4;i++)
                if(!radioHomeGrounded[i]||!radioHomeFrames[i]||!forwardReturnBroken[i]||!airborneRejected[i]||
                    Mathf.Abs(radioHomePositions[i].x-6)>=1.4f||Mathf.Abs(radioHomePositions[i].y)>.05f||
                    minimumReturnTravel[i]<=0||returnTravel[i]<minimumReturnTravel[i]||maximumCarryStep[i]>.8f)return false;
            return true;
        }
        void FinishFirstStage(bool success,string detail)
        {
            if(Finished)return;
            bool shades=branchLuminosities[0]>0&&branchLuminosities[0]<=.006f;
            for(int i=1;i<4;i++)shades&=branchLuminosities[i]>branchLuminosities[i-1];
            bool effects=shades&&Array.TrueForAll(radioRevealOutward,x=>x)&&Array.TrueForAll(radioShakeObserved,x=>x)&&
                Mathf.Abs(game.FirstStageWhiteHoldSeconds-2)<.01f&&whiteHoldFrames>=10&&whiteHoldWallEnd-whiteHoldWallStart>=1.8f&&
                radioChargeFrames[4]&&ruptureColourStartedGrey&&colourReturnMonotonic&&colourReturnFrames>=10&&colourReturnPresented;
            bool interactions=Array.TrueForAll(cleanFigureObserved,x=>x)&&Array.TrueForAll(pickupHandsObserved,x=>x)&&
                Array.TrueForAll(pocketApproachObserved,x=>x)&&Array.TrueForAll(pocketHiddenObserved,x=>x)&&
                Array.TrueForAll(placementHandsObserved,x=>x)&&Array.TrueForAll(placementAtRadioObserved,x=>x);
            var result=new FirstStageResult{passed=success,detail=detail,
                colourEffectsVerified=effects,branchLuminosities=branchLuminosities,radioRevealOutward=radioRevealOutward,radioShakeObserved=radioShakeObserved,
                interactionPosesVerified=interactions,pickupHandsObserved=pickupHandsObserved,pocketApproachObserved=pocketApproachObserved,pocketHiddenObserved=pocketHiddenObserved,
                returnGreyCoverageVerified=Array.TrueForAll(returnGreyCoverageObserved,x=>x),returnGreyRenderers=returnGreyRenderers,returnGoldGuides=returnGoldGuides,
                placementHandsObserved=placementHandsObserved,placementAtRadioObserved=placementAtRadioObserved,cleanFigureObserved=cleanFigureObserved,
                whiteHoldSimulatedSeconds=game.FirstStageWhiteHoldSeconds,whiteHoldVisibleWallSeconds=whiteHoldWallStart<0?0:whiteHoldWallEnd-whiteHoldWallStart,
                whiteHoldFrames=whiteHoldFrames,colourReturnFrames=colourReturnFrames,
                physicalReturnsVerified=PhysicalReturnsVerified(),
                branchTurnsVerified=Array.TrueForAll(branchTurnFrames,x=>x)&&Array.TrueForAll(branchTurnComplete,x=>x),
                connectionReadingVerified=Array.TrueForAll(connectionCaptionSeconds,x=>x>=5.85f)&&Array.TrueForAll(connectionCaptionFrames,x=>x),
                crossingsVerified=longBurstCrossings[2]>=1&&longBurstCrossings[3]>=2&&burstCrossingFrame&&burstChainFrame&&returnBridgeFrame,
                deliberateMemoriesVerified=Array.TrueForAll(deliberateMemories,x=>x)&&Array.TrueForAll(memoryWaitSeconds,x=>x>=.55f)&&memoryAirborneRejected&&memoryPromptFrame,
                voiceMovementVerified=Array.TrueForAll(memoryVoiceOutput,x=>x)&&Array.TrueForAll(memoryVoiceMovement,x=>x)&&Array.TrueForAll(memoryVoiceAudioSamples,x=>x>1),
                captionsVerified=Array.TrueForAll(memoryCaptionFull,x=>x)&&Array.TrueForAll(branchGestureClear,x=>x),
                practiceVerified=burstPracticeBurstFrame&&burstPracticeRenewFrame&&burstPracticeTravel>2&&game.BurstLessonStage==3,
                normalSpeedVerified=game.FragmentsReviewRequested&&fragmentsSimulated>=30&&fragmentsWallSeconds>=fragmentsSimulated*.85f&&game.FragmentsReviewPausedSeconds<.05f,
                homeReturns=game.milestones.FindAll(x=>x.StartsWith("Carried fragment ")).Count,
                sentFragments=game.milestones.FindAll(x=>x.StartsWith("Sent fragment ")).Count,failures=game.Failures,
                simulatedSeconds=fragmentsSimulated,wallSeconds=fragmentsWallSeconds,pausedSeconds=game.FragmentsReviewPausedSeconds,
                radioHomeGrounded=radioHomeGrounded,radioHomeFrames=radioHomeFrames,forwardReturnBroken=forwardReturnBroken,
                airborneRejected=airborneRejected,radioHomePositions=radioHomePositions,returnTravel=returnTravel,
                minimumReturnTravel=minimumReturnTravel,maximumCarryStep=maximumCarryStep,memoryVoiceTravel=memoryVoiceTravel,
                memoryVoiceOutput=memoryVoiceOutput,memoryVoiceMovement=memoryVoiceMovement,memoryVoiceAudioSamples=memoryVoiceAudioSamples,memoryVoiceWindows=memoryVoiceWindows,
                longBurstCrossings=longBurstCrossings,milestones=game.milestones.ToArray()};
            foreach(AudioListener listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if(listener.isActiveAndEnabled)result.activeAudioListeners++;
            if(success)
            {
                if(!result.physicalReturnsVerified)result.detail="Four grounded radio returns, broken bridges or continuous carrying travel were not verified";
                else if(!result.branchTurnsVerified||!result.connectionReadingVerified)result.detail="Branch camera turns or consequence-caption reading time were not verified";
                else if(!result.crossingsVerified||!result.practiceVerified)result.detail="Burst practice, long rising crossings or connected lower return were not verified";
                else if(!result.deliberateMemoriesVerified||!result.captionsVerified)result.detail="Deliberate memory interaction, airborne rejection or caption/gesture release were not verified";
                else if(!result.voiceMovementVerified||result.activeAudioListeners!=1)result.detail="Audible story movement on all four routes was not verified";
                else if(!result.normalSpeedVerified)result.detail="The first stage was not observed continuously at normal speed";
                else if(!result.colourEffectsVerified)result.detail="Progressive grey branches, outward radio reveal/shake or the two-second white-to-colour handoff were not verified";
                else if(!result.interactionPosesVerified)result.detail="Visible pickup/stow/retrieval/placement poses and ordinary concealed hands were not verified on every route";
                else if(!result.returnGreyCoverageVerified)result.detail="New lower-return geometry and preserved gold-guide shader receivers were not verified on all four routes";
                result.passed=result.physicalReturnsVerified&&result.branchTurnsVerified&&result.connectionReadingVerified&&
                    result.crossingsVerified&&result.deliberateMemoriesVerified&&result.voiceMovementVerified&&
                    result.captionsVerified&&result.practiceVerified&&result.normalSpeedVerified&&result.colourEffectsVerified&&result.interactionPosesVerified&&result.returnGreyCoverageVerified&&result.activeAudioListeners==1;
            }
            string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");
            if(string.IsNullOrEmpty(output))output=Path.Combine(Application.persistentDataPath,"ReturnEvidence");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"fragments-acceptance.json"),JsonUtility.ToJson(result,true));
            Debug.Log("[RETURN acceptance] "+(result.passed?"PASS":"FAIL")+" "+result.detail);
            if(clientJourney&&result.passed){clientFragmentsComplete=true;return;}Finished=true;Application.Quit(result.passed?0:1);
        }
        [Serializable] sealed class EscapeResult
        {
            public bool passed,rainWhileStationary,redForecastObserved,ejectionObserved,variedMeteorTargets,
                collapseVerified,retryVerified,storyVerified,whiteLeapVerified,normalSpeedVerified,warningsGroundedVerified,sectionCheckpointsVerified;
            public string scope="rupture",detail;
            public int meteorForecasts,meteorImpacts,volcanoEjections,failures;
            public int meteorWarningSamples,clippedMeteorWarningSamples;
            public float maximumMeteorWarningOverhang;
            public float simulatedSeconds,wallSeconds,pausedSeconds;
            public int startCheckpointSamples,midpointCheckpointSamples;
            public float startRetryDistance,midpointRetryDistance;
            public Vector2 midpointCheckpoint;
            public Vector2 stationaryPosition;public float stationarySeconds,stationaryMaximumDrift;public int stationaryForecasts,stationaryImpacts,stationaryFrames;
        }
        void FinishEscape(bool success,string detail)
        {
            if(Finished)return;
            var result=new EscapeResult{passed=success,detail=detail,rainWhileStationary=rainWhileStationary,redForecastObserved=redForecastObserved,
                stationaryPosition=openingRainPosition,stationarySeconds=openingRainSeconds,stationaryMaximumDrift=openingRainDrift,stationaryForecasts=openingRainForecasts,stationaryImpacts=openingRainImpacts,stationaryFrames=openingRainFrames,
                ejectionObserved=ejectionObserved,variedMeteorTargets=variedMeteorTargets,
                warningsGroundedVerified=meteorWarningSamples>0&&clippedMeteorWarningSamples>0&&!unsupportedMeteorWarning&&maximumMeteorWarningOverhang<.01f,
                meteorWarningSamples=meteorWarningSamples,clippedMeteorWarningSamples=clippedMeteorWarningSamples,maximumMeteorWarningOverhang=maximumMeteorWarningOverhang,
                collapseVerified=ruptureFractureFrame&&crumblingStayedBroken&&rupturePersistent,retryVerified=ruptureRetryChecked,
                sectionCheckpointsVerified=ruptureStartCheckpointSamples>0&&ruptureMidpointCheckpointSamples>0&&ruptureCheckpointReached&&ruptureMidpointRetryChecked&&ruptureStartRetryDistance>25&&ruptureMidpointRetryDistance>=40,
                startCheckpointSamples=ruptureStartCheckpointSamples,midpointCheckpointSamples=ruptureMidpointCheckpointSamples,startRetryDistance=ruptureStartRetryDistance,midpointRetryDistance=ruptureMidpointRetryDistance,midpointCheckpoint=ruptureMidpointCheckpoint,
                storyVerified=ruptureStoryChecked&&Array.TrueForAll(ruptureVoiceOutput,x=>x)&&ruptureSpeechStoppedOnWater,
                whiteLeapVerified=whiteJumpGap>0&&whiteLeapFrame&&game.FinalEscapeVoiceCompleted,
                normalSpeedVerified=game.EscapeReviewRequested&&escapeSimulated>=15&&escapeWallSeconds>=escapeSimulated*.85f&&game.EscapeReviewPausedSeconds==0&&escapeThreatVisible&&openingInFrame,
                meteorForecasts=observedMeteorForecasts,meteorImpacts=observedMeteorImpacts,volcanoEjections=observedVolcanoEjections,failures=game.Failures,
                simulatedSeconds=escapeSimulated,wallSeconds=escapeWallSeconds,pausedSeconds=game.EscapeReviewPausedSeconds};
            if(success)
            {
                result.passed=result.rainWhileStationary&&result.redForecastObserved&&result.ejectionObserved&&result.variedMeteorTargets&&result.meteorForecasts>=6&&result.meteorImpacts>=3&&
                    result.collapseVerified&&result.retryVerified&&result.storyVerified&&result.whiteLeapVerified&&result.normalSpeedVerified&&result.warningsGroundedVerified&&result.sectionCheckpointsVerified;
                if(!result.passed)result.detail="Independent forecast rain, grounded warning bounds, eruption, section checkpoints or preserved escape/story/collapse gates were not verified";
            }
            string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))output=Path.Combine(Application.persistentDataPath,"ReturnEvidence");
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"escape-acceptance.json"),JsonUtility.ToJson(result,true));
            Debug.Log("[RETURN acceptance] "+(result.passed?"PASS":"FAIL")+" "+result.detail);if(clientJourney&&result.passed){clientEscapeComplete=true;return;}Finished=true;Application.Quit(result.passed?0:1);
        }
        [Serializable] sealed class FootingFrameSample
        {
            public string label;public bool grounded;public Vector2 player;public Vector3 litScreen,unlitScreen,gapScreen;public Vector4 reveal;public float surfaceLeft,surfaceRight;
        }
        bool RequestFootingFrame(string label)
        {
            if(!game.TryRequestFrame(label))return false;
            var surface=game.world.surfaces[0];var view=game.GetComponentInChildren<Camera>();float x=game.motor.position.x;
            Vector3 lit=new Vector3(Mathf.Min(x+.55f,surface.Right-.08f),surface.y+.025f,0),unlit=new Vector3(x-2,surface.y+.025f,0),gap=new Vector3(surface.Right+.35f,surface.y+.025f,0);
            var sample=new FootingFrameSample{label=label,grounded=game.motor.Grounded,player=game.motor.position,litScreen=view.WorldToScreenPoint(game.world.root.TransformPoint(lit)),unlitScreen=view.WorldToScreenPoint(game.world.root.TransformPoint(unlit)),gapScreen=view.WorldToScreenPoint(game.world.root.TransformPoint(gap)),reveal=Shader.GetGlobalVector("_ReturnFooting"),surfaceLeft=surface.Left,surfaceRight=surface.Right};
            string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))output=Path.Combine(Application.persistentDataPath,"ReturnEvidence");
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,label+".json"),JsonUtility.ToJson(sample,true));return true;
        }
        [Serializable] sealed class MeteorContactResult {public bool passed,fallingHit,landingHit,firstAftermathSafe,laterAftermathSafe,dustVisible;}
        [Serializable] sealed class StreetStepSample {public int leg;public float elapsed,phase,actualStride,plannedStride,landingError;}
        [Serializable] sealed class StreetTargetFrame {public int leg,reviewPhase,episode,cycle;public float elapsed,phase,poseDt;public bool swing,contact,reversed;public Vector3 raw,planned,laneShift,plant,actual,root,forward,velocity,previousVelocity;}
        [Serializable] sealed class StreetGeometryReview {public int samples,supportSamples;public float maximumStretch,minimumRatio=1,maximumKneeFlex,maximumThighTwist,maximumCalfTwist,maximumTargetError,maximumSupportKneeFlex,meanSupportKneeFlex,minimumTorsoLean=180,maximumTorsoLean,meanTorsoLean,maximumGroundDrop,minimumPelvisOffsetY;public ReturnJourney3D.StreetGeometrySample worstSkin,worstTwist,worstKnee,worstSupport;}
        bool ReviewMeteorContact()
        {
            var art=new ReturnArt();var world=new ReturnWorld(art);
            try
            {
                world.Build(Movement.Rupture,0,4);ReturnHazard h=world.hazards.Find(value=>value.debris);
                if(h==null)return false;foreach(var other in world.hazards)other.timer=-1;
                var probe=new ReturnMotor();probe.Reset(new Vector2(h.x,h.y));
                h.timer=ReturnWorld.DebrisImpactTime-.01f;h.impactCounted=false;
                var result=new MeteorContactResult{landingHit=world.Step(.02f,probe)};
                result.firstAftermathSafe=!world.Step(.01f,probe);result.laterAftermathSafe=!world.Step(.10f,probe);result.dustVisible=h.impact.gameObject.activeSelf;
                h.timer=ReturnWorld.DebrisImpactTime-.04f;h.impactCounted=false;result.fallingHit=world.Step(.001f,probe);
                result.passed=result.landingHit&&result.fallingHit&&result.firstAftermathSafe&&result.laterAftermathSafe&&result.dustVisible;
                string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))output=Path.Combine(Application.persistentDataPath,"ReturnEvidence");
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"meteor-contact-regression.json"),JsonUtility.ToJson(result,true));return result.passed;
            }
            finally {if(world.root!=null){world.root.gameObject.SetActive(false);UnityEngine.Object.Destroy(world.root.gameObject);}art.Dispose();}
        }
        void ObserveStreet(float dt)
        {
            var j=game.journey;Vector3 channels=j.StreetColourChannels;
            if(reviewStreetGeometry&&j.StreetPlayable&&j.Velocity.sqrMagnitude>.1f&&j.SceneElapsed>=nextStreetGeometry){
                nextStreetGeometry=j.SceneElapsed+.15f;var sample=j.DescribeStreetGeometry();streetGeometry.samples++;
                streetGeometry.maximumGroundDrop=Mathf.Max(streetGeometry.maximumGroundDrop,sample.pose.groundDrop);streetGeometry.minimumPelvisOffsetY=Mathf.Min(streetGeometry.minimumPelvisOffsetY,sample.pose.pelvisOffsetLocal.y);
                streetGeometry.minimumTorsoLean=Mathf.Min(streetGeometry.minimumTorsoLean,sample.torsoLean);streetGeometry.maximumTorsoLean=Mathf.Max(streetGeometry.maximumTorsoLean,sample.torsoLean);streetTorsoLeanSum+=sample.torsoLean;
                if(sample.skin.maximumStretch>streetGeometry.maximumStretch){streetGeometry.maximumStretch=sample.skin.maximumStretch;streetGeometry.worstSkin=sample;}streetGeometry.minimumRatio=Mathf.Min(streetGeometry.minimumRatio,sample.skin.minimumRatio);
                float thigh=Mathf.Max(sample.rightTwist.x,sample.leftTwist.x),calf=Mathf.Max(sample.rightTwist.y,sample.leftTwist.y);
                if(Mathf.Max(thigh,calf)>Mathf.Max(streetGeometry.maximumThighTwist,streetGeometry.maximumCalfTwist))streetGeometry.worstTwist=sample;
                streetGeometry.maximumThighTwist=Mathf.Max(streetGeometry.maximumThighTwist,thigh);streetGeometry.maximumCalfTwist=Mathf.Max(streetGeometry.maximumCalfTwist,calf);
                for(int leg=0;leg<sample.pose.feet.Length;leg++){var foot=sample.pose.feet[leg];if(foot.kneeFlexDegrees>streetGeometry.maximumKneeFlex){streetGeometry.maximumKneeFlex=foot.kneeFlexDegrees;streetGeometry.worstKnee=sample;}streetGeometry.maximumTargetError=Mathf.Max(streetGeometry.maximumTargetError,foot.targetError);
                    float phase=Mathf.Repeat(sample.pose.gait/(Mathf.PI*2)+(leg==1?.5f:0),1);if(phase<ReturnRecordedWalk.Stance(leg)&&foot.minimumShoeGap<=.02f){streetGeometry.supportSamples++;streetSupportKneeSum+=foot.kneeFlexDegrees;if(foot.kneeFlexDegrees>streetGeometry.maximumSupportKneeFlex){streetGeometry.maximumSupportKneeFlex=foot.kneeFlexDegrees;streetGeometry.worstSupport=sample;}}
                }
            }
            if(traceStreetLandings&&j.SceneElapsed>=9.5f&&j.SceneElapsed<=13.5f)for(int leg=0;leg<2;leg++){
                var foot=j.DescribeStreetStance(leg);j.DescribeStreetLanding(leg,out var raw,out var planned,out var laneShift,out var previousVelocity,out float poseDt,out int episode,out int cycle,out bool swing,out bool reversed);
                streetTargetTrace.Add(new StreetTargetFrame{leg=leg,reviewPhase=streetPhase,episode=episode,cycle=cycle,elapsed=j.SceneElapsed,phase=foot.phase,poseDt=poseDt,swing=swing,contact=foot.contactHeld,reversed=reversed,raw=raw,planned=planned,laneShift=laneShift,plant=foot.plant,actual=foot.actual,root=foot.rootPosition,forward=j.StreetBodyForward,velocity=foot.velocity,previousVelocity=previousVelocity});
            }
            streetSequence&=channels.x>=previousStreetChannels.x-.0001f&&channels.y>=previousStreetChannels.y-.0001f&&channels.z>=previousStreetChannels.z-.0001f&&
                (channels.y<=.001f||channels.x>=.999f)&&(channels.z<=.001f||channels.y>=.999f);previousStreetChannels=channels;
            streetYellowSeen|=channels.x>.1f&&channels.x<.9f&&channels.y==0&&channels.z==0;
            streetRedSeen|=channels.x>=.999f&&channels.y>.1f&&channels.y<.9f&&channels.z==0;
            streetBlueSeen|=channels.x>=.999f&&channels.y>=.999f&&channels.z>.1f&&channels.z<.9f;
            if(j.FruitWhiteAlpha>=.999f){streetWhiteSeconds+=dt;if(!streetWhiteFrame&&j.SceneElapsed>.5f)streetWhiteFrame=game.TryRequestFrame("Reunion-Worlds");}
            if(j.SceneElapsed>=.25f&&!streetTracking){streetTracking=true;streetLastX=j.Position.x;streetStartZ=j.Position.z;streetStartY=j.Position.y;}
            if(streetTracking){
                float change=j.Position.x-streetLastX;
                if(j.SceneElapsed<ReturnJourney3D.StreetReadyAt)streetConnectorLocked&=Mathf.Abs(change)<.001f;
                else {streetRightTravel+=Mathf.Max(0,change);streetLeftTravel+=Mathf.Max(0,-change);}
                if(streetPhase==1)streetPauseError=Mathf.Max(streetPauseError,Mathf.Abs(j.Position.x-streetPauseX));
                streetPlaneError=Mathf.Max(streetPlaneError,Mathf.Max(Mathf.Abs(j.Position.z-streetStartZ),Mathf.Abs(j.Position.y-streetStartY)));streetLastX=j.Position.x;
            }
            if(streetPhase==3&&j.Velocity.x>1.4f)for(int leg=0;leg<2;leg++){
                var foot=j.DescribeStreetStance(leg);float length=Vector3.Distance(foot.hip,foot.knee)+Vector3.Distance(foot.knee,foot.actual);
                streetMaximumLateralRatio=Mathf.Max(streetMaximumLateralRatio,Mathf.Abs(Vector3.Dot(foot.actual-foot.hip,foot.bodyRight))/Mathf.Max(.001f,length));
                if(foot.contactHeld&&!streetFootHeld[leg]){float step=Vector3.Distance(foot.actual,streetPreviousPlant[leg]);if(++streetPlantCounts[leg]>=3&&step>.1f){streetStepSamples++;streetMinimumStep=Mathf.Min(streetMinimumStep,step);streetMaximumStep=Mathf.Max(streetMaximumStep,step);streetStepTrace.Add(new StreetStepSample{leg=leg,elapsed=j.SceneElapsed,phase=foot.phase,actualStride=step,plannedStride=Vector3.Distance(foot.plant,streetPreviousTarget[leg]),landingError=Vector3.Distance(foot.actual,foot.plant)});}streetPreviousPlant[leg]=foot.actual;streetPreviousTarget[leg]=foot.plant;}
                streetFootHeld[leg]=foot.contactHeld;
            }
            if(streetRightTravel>1.5f&&!streetRightFrame)streetRightFrame=game.TryRequestFrame("Street-Walk-Right");
            if(streetLeftTravel>.8f&&!streetLeftFrame)streetLeftFrame=game.TryRequestFrame("Street-Walk-Left");
            if(j.SceneElapsed>1.2f&&j.SceneElapsed<3.2f&&!yellowFrame)yellowFrame=game.TryRequestFrame("Colour-Yellow");
            if(j.SceneElapsed>3.2f&&j.SceneElapsed<5.2f&&!redFrame)redFrame=game.TryRequestFrame("Colour-Red");
            if(j.SceneElapsed>5.2f&&j.SceneElapsed<7.2f&&!blueFrame)blueFrame=game.TryRequestFrame("Colour-Blue");
            if(j.LightDoorOpening){FinishStreet(true,"Fruit colour recovery and playable street reached the light doorway");return;}
            if(j.SceneElapsed>70)Finish(false,"Playable street did not reach the light doorway");
        }
        [Serializable] sealed class StreetResult
        {
            public string scope="street",detail;public bool passed,whiteoutVerified,sequentialColoursVerified,connectorInputLocked,horizontalInputVerified,pauseVerified,plainDoorVerified,storyVerified,cameraVerified,regularStepsVerified;
            public int stepSamples;public float minimumStep,maximumStep,maximumLateralRatio;
            public StreetStepSample[] footsteps;
            public StreetTargetFrame[] landingTrace;
            public float simulatedSeconds,rightTravel,leftTravel,pauseError,planeError,whiteSeconds,cameraError;public int buildings,failures;
        }
        void FinishStreet(bool success,string detail)
        {
            if(Finished)return;var j=game.journey;
            var result=new StreetResult{passed=success,detail=detail,whiteoutVerified=streetWhiteFrame&&streetWhiteSeconds>.5f,
                sequentialColoursVerified=streetSequence&&streetYellowSeen&&streetRedSeen&&streetBlueSeen&&j.Colours>=.999f,connectorInputLocked=streetConnectorLocked,
                horizontalInputVerified=streetRightTravel>35&&streetLeftTravel>=1.9f&&streetPlaneError<.01f&&!j.CityCameraOnly,
                pauseVerified=streetPauseAt>=0&&streetPhase==3&&streetPauseError<.001f,plainDoorVerified=j.LightDoorOpening&&j.PlainLightDoorVisible,
                storyVerified=fruitTreeTouchObserved&&fruitMouthSeconds>.3f&&game.milestones.Contains("Yellow, red and blue reunited")&&game.milestones.Contains("The four routes became city streets")&&game.milestones.Contains("Remembered the voice through the rupture")&&game.milestones.Contains("Remembered the places behind the fragments"),
                cameraVerified=streetCameraObserved&&streetCameraError<=.05f,cameraError=streetCameraError,
                regularStepsVerified=streetStepSamples>=8&&streetMaximumStep<=streetMinimumStep*1.35f&&streetMaximumLateralRatio<=.3f,stepSamples=streetStepSamples,minimumStep=float.IsInfinity(streetMinimumStep)?0:streetMinimumStep,maximumStep=streetMaximumStep,maximumLateralRatio=streetMaximumLateralRatio,footsteps=streetStepTrace.ToArray(),landingTrace=streetTargetTrace.ToArray(),
                simulatedSeconds=simulated,rightTravel=streetRightTravel,leftTravel=streetLeftTravel,pauseError=streetPauseError,planeError=streetPlaneError,whiteSeconds=streetWhiteSeconds,buildings=j.CityBuildingCount,failures=game.Failures};
            if(success){result.passed=result.whiteoutVerified&&result.sequentialColoursVerified&&result.connectorInputLocked&&result.horizontalInputVerified&&result.pauseVerified&&result.plainDoorVerified&&result.storyVerified&&result.cameraVerified&&result.buildings>=16&&result.regularStepsVerified;
                if(!result.passed)result.detail=!result.regularStepsVerified?"Repeated street steps were not regular or remained too wide":"Fruit whiteout, sequential colours, actual horizontal input or street story gates were not verified";}
            string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))output=Path.Combine(Application.persistentDataPath,"ReturnEvidence");
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"street-acceptance.json"),JsonUtility.ToJson(result,true));Debug.Log("[RETURN acceptance] "+(result.passed?"PASS ":"FAIL ")+result.detail);
            if(reviewStreetGeometry){streetGeometry.meanSupportKneeFlex=streetSupportKneeSum/Mathf.Max(1,streetGeometry.supportSamples);streetGeometry.meanTorsoLean=streetTorsoLeanSum/Mathf.Max(1,streetGeometry.samples);File.WriteAllText(Path.Combine(output,"street-leg-geometry.json"),JsonUtility.ToJson(streetGeometry,true));}
            if(hospitalOnly&&result.passed){streetCompleted=true;return;}Finished=true;Application.Quit(result.passed?0:1);
        }
        void ObserveHospitalCamera()
        {
            var j=game.journey;
            if(j.CameraIsOrthographic||!j.HospitalCeilingInView||!j.HospitalLightsInView||!j.HospitalPatientHidden||j.HospitalRenderedPeople!=0)
            {FinishHospital(false,"Hospital perspective lost its ceiling/lights or rendered a bedside character");return;}
            if(!hospitalCameraTracking){hospitalCameraTracking=true;hospitalEyeStart=j.CameraPosition;}
            hospitalEyeDrift=Mathf.Max(hospitalEyeDrift,Vector3.Distance(j.CameraPosition,hospitalEyeStart));
            if(Vector3.Distance(j.CameraPosition,j.HospitalEyePosition)>.005f||hospitalEyeDrift>.005f)
            {FinishHospital(false,"Hospital eye camera moved away from the patient position");return;}
            hospitalCeilingSeen=true;
        }
        void ObserveHospital(float dt)
        {
            if(game.Chapter==Movement.Credits){FinishHospital(true,"Street light door, hospital ceiling, slow eye opening, blinks and offscreen voices completed");return;}
            var j=game.journey;float t=j.SceneElapsed,closure=j.HospitalEyeClosure;
            hospitalClosedSeen|=t<.9f&&closure>=.999f;
            if(t>=.9f&&t<=4.1f){hospitalOpeningMonotone&=closure<=priorHospitalClosure+.0001f;priorHospitalClosure=closure;hospitalOpeningSeen|=closure>.05f&&closure<.95f;}
            if(t>4.15f&&t<5.4f&&closure<.001f){hospitalOpenSeen=true;hospitalOpenSeconds+=dt;}
            hospitalBlinkOneSeen|=t>=5.4f&&t<=5.84f&&closure>.9f;
            hospitalBlinkTwoSeen|=t>=8.8f&&t<=9.24f&&closure>.9f;
            hospitalFinalClosedSeen|=t>=22&&closure>=.999f;
            hospitalNurseOutput|=game.HospitalVoiceOutputObserved(false);hospitalMumOutput|=game.HospitalVoiceOutputObserved(true);
            hospitalStorySeen|=t>11.2f&&t<18&&game.ActiveStoryLine.Contains("coma");
            if(!hospitalOpenFrame&&t>4.2f&&t<5.2f)hospitalOpenFrame=game.TryRequestFrame("Hospital-Opening");
            if(!hospitalFinalClosedFrame&&t>22.2f)hospitalFinalClosedFrame=game.TryRequestFrame("Hospital-Eyes-Closing");
        }
        [Serializable] sealed class HospitalResult
        {
            public string scope="hospital",detail;public bool passed,streetVerified,ceilingVerified,noPeopleVerified,eyeOpeningVerified,blinksVerified,finalEyeClosureVerified,voicesVerified,storyVerified,normalSpeedVerified;
            public bool linkedJourneyVerified;
            public bool landscapeVerified,movementVerified;public int landscapeMeshes,landscapeVertices;public float boardingSeconds,islandWalkDistance;
            public float simulatedSeconds,wallSeconds,eyeDrift,openSeconds;public int ceilingPanels,lightFixtures,renderedPeople,failures;
        }
        void FinishHospital(bool success,string detail)
        {
            if(Finished)return;var j=game.journey;float wall=Time.realtimeSinceStartup-travellerWallStart;
            var result=new HospitalResult{passed=success,detail=detail,streetVerified=streetCompleted,ceilingVerified=hospitalCeilingSeen&&hospitalCameraTracking&&hospitalEyeDrift<=.005f&&j.HospitalCeilingPanels>=30&&j.HospitalLightFixtures==2,
                linkedJourneyVerified=clientJourney&&clientStartVerified&&clientFragmentsComplete&&clientEscapeComplete&&streetCompleted&&game.Chapter==Movement.Credits,
                landscapeVerified=landscapeObserved&&j.QuietLandscape,landscapeMeshes=j.LandscapeMeshes,landscapeVertices=j.LandscapeVertices,
                movementVerified=waterEntered&&rested3D&&boardingSeen&&boardingSeconds>=1.2f&&islandWalkSeconds>.65f&&islandWalkDistance>.5f&&shoreLeafFrame&&shoreArrivalFrame&&shoreMaximumSpeed>.8f&&shoreMaximumSpeed<=1.55f&&shoreMaximumTravel<=.15f&&shoreArrivalShoeGaps.x>=-.002f&&shoreArrivalShoeGaps.y>=-.002f,
                boardingSeconds=boardingSeconds,islandWalkDistance=islandWalkDistance,
                noPeopleVerified=j.HospitalPatientHidden&&j.HospitalRenderedPeople==0,eyeOpeningVerified=hospitalClosedSeen&&hospitalOpeningSeen&&hospitalOpeningMonotone&&hospitalOpenSeen&&hospitalOpenSeconds>1,
                blinksVerified=hospitalBlinkOneSeen&&hospitalBlinkTwoSeen,finalEyeClosureVerified=hospitalFinalClosedSeen&&hospitalFinalClosedFrame,voicesVerified=hospitalNurseOutput&&hospitalMumOutput&&game.HospitalVoicesQuiet,
                storyVerified=hospitalStorySeen&&game.milestones.Contains("Remembered the hospital cause")&&game.milestones.Contains("Credits reached"),normalSpeedVerified=wall>=simulated*.85f,
                simulatedSeconds=simulated,wallSeconds=wall,eyeDrift=hospitalEyeDrift,openSeconds=hospitalOpenSeconds,ceilingPanels=j.HospitalCeilingPanels,lightFixtures=j.HospitalLightFixtures,renderedPeople=j.HospitalRenderedPeople,failures=game.Failures};
            if(success){result.passed=(!clientJourney||result.linkedJourneyVerified)&&result.landscapeVerified&&result.movementVerified&&result.streetVerified&&result.ceilingVerified&&result.noPeopleVerified&&result.eyeOpeningVerified&&result.blinksVerified&&result.finalEyeClosureVerified&&result.voicesVerified&&result.storyVerified&&result.normalSpeedVerified&&hospitalOpenFrame;
                if(!result.passed)result.detail="Landscape volume/matte surfaces, swimming/boarding/shore walking or retained street/hospital gates were not verified";}
            Finished=true;string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))output=Path.Combine(Application.persistentDataPath,"ReturnEvidence");
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"hospital-acceptance.json"),JsonUtility.ToJson(result,true));Debug.Log("[RETURN acceptance] "+(result.passed?"PASS ":"FAIL ")+result.detail);Application.Quit(result.passed?0:1);
        }
        [Serializable] sealed class TravellerResult
        {
            public string scope="traveller",detail;public bool passed,volumeVerified,ordinaryHandsConcealed,keyHandsVisible,movementVerified,fruitVerified,storyVerified,normalSpeedVerified;
            public float simulatedSeconds,wallSeconds,islandWalkSeconds,islandWalkDistance,fruitHeldSeconds,fruitMouthSeconds;public int failures;
        }
        void FinishTraveller(bool success,string detail)
        {
            if(Finished)return;float wall=Time.realtimeSinceStartup-travellerWallStart;
            var result=new TravellerResult{passed=success,detail=detail,volumeVerified=travellerVolumeObserved,ordinaryHandsConcealed=travellerWalkHidden&&travellerSwimHidden,
                keyHandsVisible=travellerBoardHands&&travellerFruitHands,movementVerified=waterEntered&&rested3D&&boardingSeen&&boardingSeconds>=1.2f&&islandWalkSeconds>.65f&&islandWalkDistance>.5f,
                fruitVerified=fruitScalePlausible&&fruitTreeTouchObserved&&fruitTasteDelayed&&fruitHeldSeconds>1&&fruitMouthSeconds>.3f,
                storyVerified=leafRestCaptionSeconds>=5.85f&&fruitMouthSeconds>.3f,normalSpeedVerified=simulated>20&&wall>=simulated*.85f&&game.MotionReviewPausedSeconds==0,
                simulatedSeconds=simulated,wallSeconds=wall,islandWalkSeconds=islandWalkSeconds,islandWalkDistance=islandWalkDistance,fruitHeldSeconds=fruitHeldSeconds,fruitMouthSeconds=fruitMouthSeconds,failures=game.Failures};
            if(success){result.passed=result.volumeVerified&&result.ordinaryHandsConcealed&&result.keyHandsVisible&&result.movementVerified&&result.fruitVerified&&result.storyVerified&&result.normalSpeedVerified;
                if(!result.passed)result.detail="Traveller presentation, real movement, key interaction or story gates were not verified";}
            Finished=true;string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))output=Path.Combine(Application.persistentDataPath,"ReturnEvidence");
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"traveller-acceptance.json"),JsonUtility.ToJson(result,true));
            Debug.Log("[RETURN acceptance] "+(result.passed?"PASS ":"FAIL ")+result.detail);Application.Quit(result.passed?0:1);
        }
        void Finish(bool success,string detail)
        {
            if(clientJourney&&!clientFragmentsComplete){FinishFirstStage(success,detail);return;}
            if(clientJourney&&!clientEscapeComplete){FinishEscape(success,detail);return;}
            if(hospitalOnly){FinishHospital(success,detail);return;}
            if(streetOnly){FinishStreet(success,detail);return;}
            if(travellerOnly){FinishTraveller(success,detail);return;}
            if(escapeOnly){FinishEscape(success,detail);return;}
            if(fragmentsOnly){FinishFirstStage(success,detail);return;}
            if(success&&!PhysicalReturnsVerified()){success=false;detail="Actual grounded radio returns, travel or uninterrupted carrying movement were not verified";}
            if(success&&reviewJournal&&(journalReviewStage!=3||journalReviewVoices!=4||journalReviewPages<2)){success=false;detail="Memory screens were not captured and resumed at all three story points";}
            if(success&&(cityCornerSamples<8||cityCornerMinimumSpeed<.65f)){success=false;detail="Continuous city camera motion was not verified around the crossroads";}
            if(success&&islandSupportPoses.Count!=2){success=false;detail="Island weight transfer was not observed over both supporting feet";}
            if(success&&!hospitalFamilyFramed){success=false;detail="Both family faces were not framed during the hospital awakening";}
            if(success&&!(boardingSeen&&boardingStartFrame&&boardingBraceFrame&&boardingSettledFrame&&boardingSeconds>=1.2f)){success=false;detail="Continuous leaf boarding and supported hands/feet were not observed";}
            if(success&&!(shoreLeafFrame&&shoreArrivalFrame&&shoreLeafSeconds>=.15f&&shoreIslandSeconds>=.15f&&shoreMaximumSpeed>.8f)){success=false;detail="Controlled leaf-to-island walking was not observed";}
            Finished=true;
            Result result=new Result{passed=success,normalSpeedReturn=game.MotionReviewActive,returnSimulatedSeconds=returnSimulated,returnWallSeconds=returnWallStart<0?0:Time.realtimeSinceStartup-returnWallStart,reviewPausedSeconds=game.MotionReviewPausedSeconds,doorColoursJoined=game.journey.DoorColoursJoined,forwardRuptureSupports=game.world.surfaces.Count,replayedRuptureSupports=game.journey.RecallRuptureSupports,replayedFragmentSupports=game.journey.RecallFragmentSupports,reverseSideView=reverseSideView,returnedToVolume=returnedToVolume,reverseDistance=reverseStartX-reverseEndX,detail=detail,simulatedSeconds=simulated,spatialDistanceZ=furthestZ,spatialDistanceX=furthestX,cameraOrbitDegrees=maxOrbit,failures=game.Failures,bursts=bursts,memories=game.journal.Count,milestones=game.milestones.ToArray()};
            result.lowerBridgeReadable=lowerBridgeReadable;result.lowerCircuitReadable=lowerCircuitReadable;
            result.hospitalNurseFramed=hospitalNurseFramed;result.hospitalReachFramed=hospitalReachFramed;result.hospitalReachStartFrame=hospitalReachStartFrame;result.hospitalHandAdvanceObserved=hospitalHandAdvanceObserved;result.hospitalHandAdvance=hospitalHandAdvance;result.hospitalFaceSamples=hospitalFaceSamples;result.hospitalMinimumFaceGap=hospitalMinimumFaceGap;
            result.cityBuildings=game.journey.CityBuildingCount;result.cityPassTravel=cityPassTravel;result.cityPassSeconds=ReturnReverseJourney.DoorAt-ReturnReverseJourney.CityPassAt;result.cityFrame=cityFrame;result.cityRecoveryFrame=cityRecoveryFrame;result.cityRecoveryStarted=cityRecoveryStarted;result.cityRecoveryCompleted=cityRecoveryCompleted;result.cityInputIgnored=cityInputIgnored;result.hospitalClearSamples=hospitalClearSamples;
            if(success){result.passed&=cityPassTravel>30&&result.cityPassSeconds<=8&&cityFrame&&cityRecoveryFrame&&cityRecoveryStarted&&cityRecoveryCompleted&&cityInputIgnored&&result.cityBuildings>=16&&hospitalClearSamples==5&&hospitalFaceSamples==5&&hospitalMinimumFaceGap>=6&&hospitalReachStartFrame&&hospitalHandAdvanceObserved&&hospitalHandAdvance>=.25f&&hospitalNurseFramed&&hospitalReachFramed&&lowerBridgeReadable&&lowerCircuitReadable;if(!result.passed)result.detail="City camera/colour passage or hospital bed clearance was not verified";}
            result.solidHouses=game.journey.RecallSolidHouses;result.solidHouseDepth=solidHouseDepth;result.paperHouseOpacity=paperHouseOpacity;result.architectureFlattened=architectureFlattened;result.architectureRestored=architectureRestored;
            result.normalSpeedEscape=game.EscapeReviewRequested;result.escapeSimulatedSeconds=escapeSimulated;result.escapeWallSeconds=escapeWallSeconds;result.escapePausedSeconds=game.EscapeReviewPausedSeconds;result.escapeThreatVisible=escapeThreatVisible;result.openingInFrame=openingInFrame;
            result.ruptureFractureFrame=ruptureFractureFrame;result.crumblingStayedBroken=crumblingStayedBroken;
            result.whiteJumpGap=whiteJumpGap;result.whiteLeapFrame=whiteLeapFrame;result.finalEscapeVoiceCompleted=game.FinalEscapeVoiceCompleted;
            result.ruptureVoiceOutput=ruptureVoiceOutput;result.ruptureSpeechStoppedOnWater=ruptureSpeechStoppedOnWater;
            result.ruptureRetryChecked=ruptureRetryChecked;result.rupturePersistent=rupturePersistent;result.ruptureStoryChecked=ruptureStoryChecked;
            result.normalSpeedFragments=game.FragmentsReviewRequested;result.fragmentsSimulatedSeconds=fragmentsSimulated;result.fragmentsWallSeconds=fragmentsWallSeconds;result.fragmentsPausedSeconds=game.FragmentsReviewPausedSeconds;result.airborneRejected=airborneRejected;result.transmissionsArrived=transmissionsArrived;result.sentFragments=game.milestones.FindAll(x=>x.StartsWith("Sent fragment ")).Count;result.homeReturns=game.milestones.FindAll(x=>x.StartsWith("Carried fragment ")).Count;
            result.branchTurnDegrees=branchTurnDegrees;result.branchTurnFrames=branchTurnFrames;result.branchTurnComplete=branchTurnComplete;result.branchTurnsVerified=System.Array.TrueForAll(branchTurnFrames,captured=>captured)&&System.Array.TrueForAll(branchTurnComplete,complete=>complete);
            result.fruitCameraContinuous=fruitCameraContinuous;result.returnCameraContinuous=returnCameraContinuous;result.returnHandoffFrame=returnHandoffFrame;result.returnPullbackFrame=returnPullbackFrame;result.fruitCameraAngleDegrees=fruitCameraAngleDegrees;result.fruitCameraLensDegrees=fruitCameraLensDegrees;result.fruitCameraTravel=fruitCameraTravel;result.returnCameraAngleDegrees=returnCameraAngleDegrees;result.returnCameraLensDegrees=returnCameraLensDegrees;result.returnCameraTravel=returnCameraTravel;
            result.fruitScalePlausible=fruitScalePlausible;result.fruitTreeTouchObserved=fruitTreeTouchObserved;result.fruitPickFrame=fruitPickFrame;
            result.fruitTasteDelayed=fruitTasteDelayed;result.fruitBiteFrame=fruitBiteFrame;result.fruitHeldSeconds=fruitHeldSeconds;result.fruitMouthSeconds=fruitMouthSeconds;
            result.swimCycleFrames=swimCycleFrames;result.swimCyclePhases=swimCyclePhases;result.swimCycleShapes=swimCycleShapes;result.swimCameraDistances=swimCameraDistances;result.swimFootSpeeds=swimFootSpeeds;result.swimPlanarSpeeds=swimPlanarSpeeds;result.swimPostures=swimPostures;
            result.waterHandStrokeFrame=waterHandStrokeFrame;result.waterBodyMotionObserved=waterBodyMotionObserved;result.leafWaterMotionObserved=leafWaterMotionObserved;result.leafIdleMotionQuiet=leafIdleMotionQuiet;
            result.islandWalkFrame=islandWalkFrame;result.islandWalkSeconds=islandWalkSeconds;result.islandWalkDistance=islandWalkDistance;result.islandWalkMaxSpeed=islandWalkMaxSpeed;
            result.waterEntered=waterEntered;result.waterSwimFrame=waterSwimFrame;result.leafReadyFrame=leafReadyFrame;result.leafDriftFrame=leafDriftFrame;result.islandApproachFrame=islandApproachFrame;result.waterSwimDistance=waterSwimDistance;result.leafIdleSeconds=leafIdleSeconds;result.leafIdleAdvance=leafIdleAdvance;result.leafDriftDistance=leafDriftDistance;result.leafRestCaptionSeconds=leafRestCaptionSeconds;
            result.hospitalRecollectionFrame=hospitalRecollectionFrame;result.hospitalRecollectionSeconds=hospitalRecollectionSeconds;result.hospitalRecollectionWallSeconds=hospitalRecollectionWallSeconds;
            result.connectionCaptionSeconds=connectionCaptionSeconds;result.connectionCaptionFrames=connectionCaptionFrames;result.connectionReadingVerified=System.Array.TrueForAll(connectionCaptionSeconds,seconds=>seconds>=5.85f)&&System.Array.TrueForAll(connectionCaptionFrames,captured=>captured);
            result.burstPracticeBurstFrame=burstPracticeBurstFrame;result.burstPracticeRenewFrame=burstPracticeRenewFrame;result.burstPracticeTravel=burstPracticeTravel;result.burstLessonComplete=game.BurstLessonStage==3;
            result.memoryVoiceOutput=memoryVoiceOutput;result.memoryVoiceMovement=memoryVoiceMovement;result.memoryVoiceTravel=memoryVoiceTravel;result.memoryCaptionFull=memoryCaptionFull;result.memoryCaptionSeconds=memoryCaptionSeconds;result.branchGestureClear=branchGestureClear;
            result.memoryVoiceAudioSamples=memoryVoiceAudioSamples;result.memoryVoiceTravelFromAudioBlocks=true;
            result.deliberateMemories=deliberateMemories;result.memoryWaitSeconds=memoryWaitSeconds;result.memoryAirborneRejected=memoryAirborneRejected;result.memoryPromptFrame=memoryPromptFrame;
            result.longBurstCrossings=longBurstCrossings;result.burstCrossingFrame=burstCrossingFrame;result.burstChainFrame=burstChainFrame;result.returnBridgeFrame=returnBridgeFrame;
            foreach(AudioListener listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if(listener.isActiveAndEnabled)result.activeAudioListeners++;
            if(game.FragmentsReviewRequested)
            {
                result.passed&=result.branchTurnsVerified;
                if(!result.branchTurnsVerified)result.detail="The three radio quarter-turns were not observed upright around the shared pivot";
                result.passed&=result.connectionReadingVerified;
                if(!result.connectionReadingVerified)result.detail="Memory consequence captions did not receive their reading time";
                bool crossingsVerified=longBurstCrossings[2]>=1&&longBurstCrossings[3]>=2&&burstCrossingFrame&&burstChainFrame&&returnBridgeFrame;
                result.passed&=crossingsVerified;
                if(!crossingsVerified)result.detail="Long rising crossings, renewed collapse-route burst chain or connected lower return were not verified";
                bool memoriesDeliberate=System.Array.TrueForAll(deliberateMemories,remembered=>remembered)&&System.Array.TrueForAll(memoryWaitSeconds,seconds=>seconds>=.55f)&&memoryAirborneRejected&&memoryPromptFrame;
                result.passed&=memoriesDeliberate;
                if(!memoriesDeliberate)result.detail="Deliberate grounded memories, untouched waiting or airborne rejection were not verified";
                bool movementVerified=System.Array.TrueForAll(memoryVoiceMovement,moved=>moved)&&System.Array.TrueForAll(memoryVoiceAudioSamples,count=>count>1);
                result.passed&=result.activeAudioListeners==1&&System.Array.TrueForAll(memoryVoiceOutput,played=>played)&&movementVerified;
                if(!movementVerified)result.detail="Movement during memory voices was not observed on every route";
                bool captionVerified=System.Array.TrueForAll(memoryCaptionFull,full=>full),gesturesVerified=System.Array.TrueForAll(branchGestureClear,clear=>clear);
                result.passed&=captionVerified&&gesturesVerified;
                if(!captionVerified||!gesturesVerified)result.detail="Memory reading time or branch gesture release was not verified";
                bool practiceVerified=burstPracticeBurstFrame&&burstPracticeRenewFrame&&burstPracticeTravel>2&&game.BurstLessonStage==3;
                result.passed&=practiceVerified;
                if(!practiceVerified)result.detail="Air burst and renewal on the practice ground were not verified";
            }
            if(game.EscapeReviewRequested)
            {
                bool fractureVerified=ruptureFractureFrame&&crumblingStayedBroken;result.passed&=fractureVerified;
                if(!fractureVerified)result.detail="Visible ground fracture or persistent timed collapse was not verified";
                bool spokenEscape=System.Array.TrueForAll(ruptureVoiceOutput,played=>played)&&ruptureSpeechStoppedOnWater;
                result.passed&=spokenEscape;
                if(!spokenEscape)result.detail="Spoken escape beats or Still Water speech stop not verified";
                bool leapVerified=whiteJumpGap>0&&whiteLeapFrame&&game.FinalEscapeVoiceCompleted;
                result.passed&=leapVerified;
                if(!leapVerified)result.detail="White-opening leap or complete final voice was not verified";
            }
            result.hospitalGeometryControlsPassed=game.journey.HospitalGeometryControlsPassed;result.hospitalBedMeshCount=game.journey.HospitalBedMeshCount;result.hospitalSkinVerticesChecked=game.journey.HospitalSkinVerticesChecked;
            if(success){bool geometry=result.hospitalGeometryControlsPassed&&result.hospitalBedMeshCount==24&&result.hospitalSkinVerticesChecked>100000;result.passed&=geometry;if(!geometry)result.detail="Actual hospital bed geometry controls or posed vertex checks were not verified";}
            if(success){game.Begin();result.restartPassed=game.Chapter==Movement.Fragments&&game.Fragments==0&&game.Failures==0&&game.BurstLessonStage==0&&!game.ConnectionCaptionActive;result.passed&=result.restartPassed;}
            string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");
            if(string.IsNullOrEmpty(output))output=Path.Combine(Application.persistentDataPath,"ReturnEvidence");
            result.hospitalContact=game.journey.LastHospitalContact;
             Directory.CreateDirectory(output);if(result.hospitalContact!=null)File.WriteAllText(Path.Combine(output,"hospital-contact.json"),JsonUtility.ToJson(result.hospitalContact,true));
             if(game.journey.LastHospitalFaceClearance!=null)File.WriteAllText(Path.Combine(output,"hospital-face-clearance.json"),JsonUtility.ToJson(game.journey.LastHospitalFaceClearance,true));
            File.WriteAllText(Path.Combine(output,"city-camera-pass.json"),JsonUtility.ToJson(new CityCameraPassReview{passed=cityCornerSamples>=8&&cityCornerMinimumSpeed>=.65f,seconds=ReturnReverseJourney.DoorAt-ReturnReverseJourney.CityPassAt,minimumCornerSpeed=cityCornerSamples>0?cityCornerMinimumSpeed:0,cornerSamples=cityCornerSamples},true));
            File.WriteAllText(Path.Combine(output,"standalone-acceptance.json"),JsonUtility.ToJson(result,true));
            File.WriteAllText(Path.Combine(output,"memory-voice-position-windows.json"),JsonUtility.ToJson(new VoicePositionWindows{memories=memoryVoiceWindows},true));
            if(Array.Exists(swimFootPoses,sample=>sample!=null))File.WriteAllText(Path.Combine(output,"swim-feet.json"),JsonUtility.ToJson(new SwimFeetReview{samples=swimFootPoses},true));
            if(game.journey.SkinReview!=null)File.WriteAllText(Path.Combine(output,"ground-skin-review.json"),JsonUtility.ToJson(game.journey.SkinReview,true));
            File.WriteAllText(Path.Combine(output,"island-weight-transfer.json"),JsonUtility.ToJson(new IslandWeightTransferReview{passed=islandSupportPoses.Count==2,samples=islandSupportPoses.ToArray()},true));
            File.WriteAllText(Path.Combine(output,"leaf-boarding.json"),JsonUtility.ToJson(new LeafBoardingReview{passed=boardingSeen&&boardingStartFrame&&boardingBraceFrame&&boardingSettledFrame&&boardingSeconds>=1.2f&&boardingMaximumTravel<=.15f&&boardingMaximumAngle<=8,startFrame=boardingStartFrame,braceFrame=boardingBraceFrame,settledFrame=boardingSettledFrame,seconds=boardingSeconds,maximumRootOrBodyStep=boardingMaximumTravel,maximumRootAngle=boardingMaximumAngle,handClearance=boardingHandGaps,shoeClearance=boardingShoeGaps},true));
            File.WriteAllText(Path.Combine(output,"shore-walk.json"),JsonUtility.ToJson(new ShoreWalkReview{passed=shoreLeafFrame&&shoreArrivalFrame&&shoreLeafSeconds>=.15f&&shoreIslandSeconds>=.15f&&shoreMaximumSpeed>.8f&&shoreMaximumSpeed<=1.55f&&shoreMaximumTravel<=.15f&&shoreArrivalShoeGaps.x>=-.002f&&shoreArrivalShoeGaps.y>=-.002f,leafFrame=shoreLeafFrame,arrivalFrame=shoreArrivalFrame,leafSeconds=shoreLeafSeconds,islandSeconds=shoreIslandSeconds,maximumSpeed=shoreMaximumSpeed,maximumRootOrBodyStep=shoreMaximumTravel,supportGapSeconds=shoreSupportGapSeconds,arrivalShoeClearance=shoreArrivalShoeGaps},true));
            Debug.Log("[RETURN acceptance] "+(result.passed?"PASS":"FAIL")+" "+detail);
            Application.Quit(result.passed?0:1);
        }
    }
}
