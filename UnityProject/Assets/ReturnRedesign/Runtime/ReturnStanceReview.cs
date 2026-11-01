using System;
using System.IO;
using UnityEngine;

namespace Return.Redesign
{
    // Opt-in aggregate of posed shoe points; no extra captures or gameplay changes.
    internal sealed class ReturnStanceReview
    {
        [Serializable] internal sealed class Worst
        {
            public int episode,cycle,shoePoint;
            public float elapsed,interval,distance,bodyTurnDegrees,gapBefore,gapAfter;
            public Vector3 from,to;
            public ReturnPerson3D.GroundStanceState poseBefore,poseAfter;
            public Vector3 localPointBefore,localPointAfter;
            public ReturnPerson3D.ShoePointSource source;
        }
        [Serializable] internal sealed class KneePose
        {
            public bool supported;
            public float elapsed,intervalBefore,intervalAfter;
            public ReturnPerson3D.GroundStanceState before,peak,after;
        }
        [Serializable] internal sealed class Foot
        {
            public string side;
            public int observations,contactPairs,pointChanges,unsupportedFrames;
            public float maximumPointSpeed,maximumSegmentTravel,maximumLiftStep,maximumLiftRotation;
            public int postureObservations;
            public int crossedFootFrames;
            public float minimumFootSide=float.MaxValue,maximumFootSide,maximumHipFootLateral;
            public float minimumKneeFlex=180,maximumKneeFlex,kneeFlexTotal,maximumPelvisDrop,pelvisDropTotal,minimumStrideScale=1;
            public ReturnPerson3D.GroundStanceState deepestBodyPose;
            public KneePose maximumKneePose,firstLandingPose,maximumLateralPose,maximumLiftPose,maximumLiftRotationPose;
            public Worst worst;
            public UnsupportedContact firstUnsupportedContact;
        }
        [Serializable] internal sealed class UnsupportedContact
        {
            public int episode,cycle,closestPoint;
            public float minimumGap,maximumGap,closestGap;
            public Vector3 closestWorld;
            public KneePose motion;
        }
        [Serializable] internal sealed class Surface
        {
            public string name;
            public int sampledFrames,walkingFrames,unregisteredSupportFrames;
            public Foot[] feet={new Foot{side="_r"},new Foot{side="_l"}};
        }
        [Serializable] sealed class Report
        {
            public bool covered;
            public string detail;
            public Surface leaf,island;
        }
        sealed class Contact
        {
            public bool valid,leaf;
            public int episode,cycle,point;
            public float elapsed,gap,travel;
            public Vector3 relative;
            public Quaternion bodyHeading;
            public ReturnPerson3D.GroundStanceState pose;
            public Vector3 localPoint;
        }
        readonly Surface leaf=new Surface{name="leaf"},island=new Surface{name="island"};
        readonly Contact[] contacts={new Contact(),new Contact()};
        readonly ReturnPerson3D.GroundStanceState[] previousPosture=new ReturnPerson3D.GroundStanceState[2];
        readonly bool[] previousSupported=new bool[2];
        bool previousPostureLeaf;
        float previousPostureTime;
        Vector3 previousSupportOrigin;
        public bool Saved {get;private set;}
        static bool Touching(float gap)=>gap>=-.002f&&gap<=.015f;
        public void ResetContacts(){foreach(var contact in contacts)contact.valid=false;for(int leg=0;leg<2;leg++)previousPosture[leg]=null;}
        public void Sample(ReturnPerson3D person,bool onLeaf,Vector3 supportOrigin,Func<Vector3,float> surface,float elapsed)
        {
            bool right=person.TryGroundStanceKey(0,out int rightEpisode,out int rightCycle),left=person.TryGroundStanceKey(1,out int leftEpisode,out int leftCycle);
            var summary=onLeaf?leaf:island;
            if(person.GroundWalkReviewActive){summary.walkingFrames++;if(!right&&!left)summary.unregisteredSupportFrames++;}
            if(!right&&!left){ResetContacts();return;}
            var points=person.DescribeShoeVertices();summary.sampledFrames++;
            if(previousPostureLeaf!=onLeaf)for(int leg=0;leg<2;leg++)previousPosture[leg]=null;
            for(int leg=0;leg<2;leg++)
            {
                var track=contacts[leg];var foot=summary.feet[leg];
                var pose=person.DescribeGroundStance(leg);float kneeFlex=180-Vector3.Angle(pose.hip-pose.knee,pose.actual-pose.knee);
                var beforePose=previousPosture[leg];var unsupported=foot.firstUnsupportedContact;
                if(unsupported!=null&&unsupported.motion.after==null&&object.ReferenceEquals(beforePose,unsupported.motion.peak)){unsupported.motion.after=pose;unsupported.motion.intervalAfter=elapsed-unsupported.motion.elapsed;}
                if(foot.maximumKneePose!=null&&foot.maximumKneePose.after==null&&object.ReferenceEquals(previousPosture[leg],foot.maximumKneePose.peak)){foot.maximumKneePose.after=pose;foot.maximumKneePose.intervalAfter=elapsed-foot.maximumKneePose.elapsed;}
                if(foot.maximumKneePose==null||kneeFlex>foot.maximumKneeFlex)foot.maximumKneePose=new KneePose{supported=leg==0?right:left,elapsed=elapsed,intervalBefore=previousPosture[leg]==null?0:elapsed-previousPostureTime,before=previousPosture[leg],peak=pose};
                bool supported=leg==0?right:left;
                if(foot.firstLandingPose!=null&&foot.firstLandingPose.after==null&&object.ReferenceEquals(previousPosture[leg],foot.firstLandingPose.peak)){foot.firstLandingPose.after=pose;foot.firstLandingPose.intervalAfter=elapsed-foot.firstLandingPose.elapsed;}
                if(foot.firstLandingPose==null&&previousPosture[leg]!=null&&supported&&!previousSupported[leg])foot.firstLandingPose=new KneePose{supported=true,elapsed=elapsed,intervalBefore=elapsed-previousPostureTime,before=previousPosture[leg],peak=pose};
                float lateral=Mathf.Abs(Vector3.Dot(pose.actual-pose.hip,pose.bodyRight));
                if(foot.maximumLateralPose!=null&&foot.maximumLateralPose.after==null&&object.ReferenceEquals(previousPosture[leg],foot.maximumLateralPose.peak)){foot.maximumLateralPose.after=pose;foot.maximumLateralPose.intervalAfter=elapsed-foot.maximumLateralPose.elapsed;}
                if(foot.maximumLateralPose==null||lateral>foot.maximumHipFootLateral)foot.maximumLateralPose=new KneePose{supported=supported,elapsed=elapsed,intervalBefore=previousPosture[leg]==null?0:elapsed-previousPostureTime,before=previousPosture[leg],peak=pose};
                if(foot.maximumLiftPose!=null&&foot.maximumLiftPose.after==null&&object.ReferenceEquals(previousPosture[leg],foot.maximumLiftPose.peak)){foot.maximumLiftPose.after=pose;foot.maximumLiftPose.intervalAfter=elapsed-foot.maximumLiftPose.elapsed;}
                if(foot.maximumLiftRotationPose!=null&&foot.maximumLiftRotationPose.after==null&&object.ReferenceEquals(previousPosture[leg],foot.maximumLiftRotationPose.peak)){foot.maximumLiftRotationPose.after=pose;foot.maximumLiftRotationPose.intervalAfter=elapsed-foot.maximumLiftRotationPose.elapsed;}
                float liftInterval=elapsed-previousPostureTime;
                if(person.GroundWalkReviewActive&&previousPosture[leg]!=null&&previousSupported[leg]&&!supported&&liftInterval>0&&liftInterval<.04f){
                    Vector3 liftStep=pose.actual-previousPosture[leg].actual-(supportOrigin-previousSupportOrigin);float liftDistance=new Vector2(liftStep.x,liftStep.z).magnitude;
                    if(foot.maximumLiftPose==null||liftDistance>foot.maximumLiftStep){foot.maximumLiftStep=liftDistance;foot.maximumLiftPose=new KneePose{supported=false,elapsed=elapsed,intervalBefore=liftInterval,before=previousPosture[leg],peak=pose};}
                    float liftRotation=Quaternion.Angle(previousPosture[leg].actualHeading,pose.actualHeading);
                    if(foot.maximumLiftRotationPose==null||liftRotation>foot.maximumLiftRotation){foot.maximumLiftRotation=liftRotation;foot.maximumLiftRotationPose=new KneePose{supported=false,elapsed=elapsed,intervalBefore=liftInterval,before=previousPosture[leg],peak=pose};}
                }
                previousPosture[leg]=pose;previousSupported[leg]=supported;
                foot.postureObservations++;foot.minimumKneeFlex=Mathf.Min(foot.minimumKneeFlex,kneeFlex);foot.maximumKneeFlex=Mathf.Max(foot.maximumKneeFlex,kneeFlex);foot.kneeFlexTotal+=kneeFlex;
                float hipSide=Vector3.Dot(pose.hip-pose.pelvis,pose.bodyRight),footSide=Vector3.Dot(pose.actual-pose.pelvis,pose.bodyRight)*Mathf.Sign(hipSide);
                foot.minimumFootSide=Mathf.Min(foot.minimumFootSide,footSide);foot.maximumFootSide=Mathf.Max(foot.maximumFootSide,footSide);foot.maximumHipFootLateral=Mathf.Max(foot.maximumHipFootLateral,lateral);
                if(footSide<-.001f)foot.crossedFootFrames++;
                foot.pelvisDropTotal+=pose.groundDrop;foot.minimumStrideScale=Mathf.Min(foot.minimumStrideScale,pose.strideScale);
                if(foot.deepestBodyPose==null||pose.groundDrop>foot.maximumPelvisDrop){foot.maximumPelvisDrop=pose.groundDrop;foot.deepestBodyPose=pose;}
                if(leg==0?!right:!left){track.valid=false;continue;}
                int episode=leg==0?rightEpisode:leftEpisode,cycle=leg==0?rightCycle:leftCycle;foot.observations++;
                bool same=track.valid&&track.leaf==onLeaf&&track.episode==episode&&track.cycle==cycle&&track.point<points[leg].Length;
                if(same)
                {
                    Vector3 world=points[leg][track.point],relative=world-supportOrigin;float gap=world.y-surface(world),interval=elapsed-track.elapsed;
                    if(Touching(gap)&&interval>.0001f)
                    {
                        float distance=new Vector2(relative.x-track.relative.x,relative.z-track.relative.z).magnitude,speed=distance/interval;track.travel+=distance;foot.contactPairs++;
                        foot.maximumSegmentTravel=Mathf.Max(foot.maximumSegmentTravel,track.travel);
                        Vector3 localPoint=Quaternion.Inverse(pose.actualHeading)*(world-pose.actual);
                        if(speed>foot.maximumPointSpeed){foot.maximumPointSpeed=speed;foot.worst=new Worst{episode=episode,cycle=cycle,shoePoint=track.point,elapsed=elapsed,interval=interval,distance=distance,bodyTurnDegrees=Quaternion.Angle(track.bodyHeading,person.root.rotation),gapBefore=track.gap,gapAfter=gap,from=track.relative,to=relative,poseBefore=track.pose,poseAfter=pose,localPointBefore=track.localPoint,localPointAfter=localPoint,source=person.DescribeShoePointSource(leg,track.point)};}
                        track.relative=relative;track.elapsed=elapsed;track.gap=gap;track.bodyHeading=person.root.rotation;track.pose=pose;track.localPoint=localPoint;continue;
                    }
                }
                track.valid=false;int selected=-1;float minimum=float.PositiveInfinity;
                for(int i=0;i<points[leg].Length;i++){Vector3 world=points[leg][i];float gap=world.y-surface(world);if(Touching(gap)&&gap<minimum){minimum=gap;selected=i;}}
                if(selected<0){
                    foot.unsupportedFrames++;
                    if(foot.firstUnsupportedContact==null){
                        var missed=new UnsupportedContact{episode=episode,cycle=cycle,closestPoint=-1,minimumGap=float.PositiveInfinity,maximumGap=float.NegativeInfinity,closestGap=float.PositiveInfinity,motion=new KneePose{supported=true,elapsed=elapsed,intervalBefore=beforePose==null?0:elapsed-previousPostureTime,before=beforePose,peak=pose}};
                        for(int i=0;i<points[leg].Length;i++){Vector3 world=points[leg][i];float gap=world.y-surface(world);missed.minimumGap=Mathf.Min(missed.minimumGap,gap);missed.maximumGap=Mathf.Max(missed.maximumGap,gap);if(Mathf.Abs(gap)<Mathf.Abs(missed.closestGap)){missed.closestGap=gap;missed.closestPoint=i;missed.closestWorld=world;}}
                        foot.firstUnsupportedContact=missed;
                    }
                    continue;
                }
                foot.pointChanges++;track.valid=true;track.leaf=onLeaf;track.episode=episode;track.cycle=cycle;track.point=selected;track.elapsed=elapsed;track.gap=minimum;track.travel=0;track.relative=points[leg][selected]-supportOrigin;track.bodyHeading=person.root.rotation;track.pose=pose;
                track.localPoint=Quaternion.Inverse(track.pose.actualHeading)*(points[leg][selected]-track.pose.actual);
            }
            previousPostureLeaf=onLeaf;previousPostureTime=elapsed;previousSupportOrigin=supportOrigin;
        }
        public void Save()
        {
            if(Saved)return;
            string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))output=Path.Combine(Application.persistentDataPath,"ReturnEvidence");Directory.CreateDirectory(output);
            bool covered=leaf.feet[0].contactPairs+leaf.feet[1].contactPairs>0&&island.feet[0].contactPairs+island.feet[1].contactPairs>0;
            var report=new Report{covered=covered,detail="Same posed shoe point tracked only during one supported stance and within -2 to +15 mm of terrain; support translation removed. Aggregate evidence, not human visual acceptance.",leaf=leaf,island=island};
            File.WriteAllText(Path.Combine(output,"ground-stance-review.json"),JsonUtility.ToJson(report,true));Saved=true;
        }
    }
}
