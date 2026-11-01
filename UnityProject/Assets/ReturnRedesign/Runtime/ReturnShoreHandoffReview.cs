using System;
using System.IO;
using UnityEngine;

namespace Return.Redesign
{
    // Opt-in native observation; ordinary controls and gameplay do not use this route.
    sealed class ReturnShoreHandoffReview
    {
        [Serializable] sealed class Result
        {
            public bool passed,swimmingSeen,shoreSeen;public int samples;
            public float seconds,settledSeconds,maximumRootStep,maximumBodyStep,maximumRootAngle,minimumShoeGap,minimumTorsoClearance=float.MaxValue;
            public string error;public ReturnPerson3D.GroundPoseReport previousPose,pose;
        }
        readonly ReturnGame game;readonly Vector3 target=new Vector3(-2.3f,0,26);readonly Result result=new Result{minimumShoeGap=float.MaxValue};
        ReturnPerson3D.GroundPoseReport previous;Vector3 previousBody;bool arrived,finished;
        public ReturnShoreHandoffReview(ReturnGame game){this.game=game;}
        public ReturnInput Input()
        {
            if(finished||arrived)return new ReturnInput();var j=game.journey;
            if(j.OnIsland&&new Vector2(j.Position.x-target.x,j.Position.z-target.z).magnitude<.2f){arrived=true;return new ReturnInput();}
            Vector2 direction=j.DirectionTo(target);return new ReturnInput{move=direction.x,vertical=direction.y};
        }
        public void Observe(float dt)
        {
            if(finished)return;result.seconds+=dt;var j=game.journey;var pose=j.DescribeFruitGroundPose("Direct swim-to-shore handoff");
            result.swimmingSeen|=!pose.groundSurfaceProvided&&j.Position.y<-.25f&&new Vector2(j.Velocity.x,j.Velocity.z).magnitude>1;
            if(result.swimmingSeen&&pose.groundSurfaceProvided&&!pose.airborne)
            {
                result.shoreSeen=true;result.samples++;
                Vector3 torso=j.CharacterBodyFocus;
                if(ReturnVolumeArt.IslandContains(torso.x,torso.z-26)){
                    float clearance=torso.y-ReturnVolumeArt.IslandHeight(torso.x,torso.z-26);result.minimumTorsoClearance=Mathf.Min(result.minimumTorsoClearance,clearance);
                    if(clearance<.08f){Finish(false,"Shore handoff torso enters the bank",pose);return;}
                }
                if(previous!=null)
                {
                    float rootStep=Vector3.Distance(previous.rootPosition,pose.rootPosition),bodyStep=Vector3.Distance(previousBody,j.CharacterBodyFocus),angle=Quaternion.Angle(previous.rootRotation,pose.rootRotation);
                    result.maximumRootStep=Mathf.Max(result.maximumRootStep,rootStep);result.maximumBodyStep=Mathf.Max(result.maximumBodyStep,bodyStep);result.maximumRootAngle=Mathf.Max(result.maximumRootAngle,angle);
                    if(rootStep>Mathf.Max(.15f,dt*5.5f)||bodyStep>Mathf.Max(.15f,dt*5.5f)||angle>Mathf.Max(8,dt*220)){Finish(false,"Swimming-to-shore body transform jumped",pose);return;}
                }
                foreach(var foot in pose.feet)
                {
                    float gap=foot.minimumShoeGap;result.minimumShoeGap=Mathf.Min(result.minimumShoeGap,gap);
                    if(float.IsNaN(gap)||float.IsInfinity(gap)||gap<-.004f){Finish(false,"Shore handoff shoe penetrates the bank",pose);return;}
                }
                if(arrived&&new Vector2(j.Velocity.x,j.Velocity.z).magnitude<.05f)result.settledSeconds+=dt;
                if(result.settledSeconds>=.6f)
                {
                    foreach(var foot in pose.feet)if(foot.minimumShoeGap>.03f){Finish(false,"Shore handoff shoe remained above the bank",pose);return;}
                    Finish(true,null,pose);return;
                }
            }
            if(result.seconds>10){Finish(false,"Direct swimming-to-shore route did not settle",pose);return;}
            previous=pose;previousBody=j.CharacterBodyFocus;
        }
        void Finish(bool passed,string error,ReturnPerson3D.GroundPoseReport pose)
        {
            finished=true;result.passed=passed&&result.swimmingSeen&&result.shoreSeen&&result.samples>20;result.error=error;result.previousPose=previous;result.pose=pose;
            if(!result.passed&&string.IsNullOrEmpty(result.error))result.error="Incomplete direct shore observation";
            string directory=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(directory))directory=Path.Combine(Application.persistentDataPath,"ReturnEvidence");Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"swim-shore-handoff.json"),JsonUtility.ToJson(result,true));
            Debug.Log("[RETURN shoreline] "+(result.passed?"PASS":"FAIL")+" "+result.error);Application.Quit(result.passed?0:1);
        }
    }
}
