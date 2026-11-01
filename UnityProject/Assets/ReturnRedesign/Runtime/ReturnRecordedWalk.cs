using System;
using UnityEngine;

namespace Return.Redesign
{
    // CMU 02_01: a steady recorded stride, normalized to the character's leg length.
    internal static class ReturnRecordedWalk
    {
        [Serializable] public struct Foot { public float progress,lift,roll; public Vector3 knee; }
        [Serializable] public struct Body
        {
            public Vector3 pelvis,leftHand,rightHand,leftElbow,rightElbow;
            public float pelvisYaw,pelvisPitch,pelvisRoll,chestYaw,chestLean;
        }
        [Serializable] sealed class Leg { public float stance,lead; public Foot[] frames; }
        [Serializable] sealed class Clip { public float stride; public Leg right,left; public Body[] body; }
        static Clip clip;

        public static void EnsureLoaded()
        {
            if(clip!=null)return;
            var source=Resources.Load<TextAsset>("ReturnRealism/RecordedWalk");
            if(source==null)throw new InvalidOperationException("Recorded walking motion is missing.");
            var parsed=JsonUtility.FromJson<Clip>(source.text);
            if(parsed==null||parsed.stride<=0||parsed.body==null||parsed.body.Length<2||parsed.right==null||parsed.left==null||
               parsed.right.frames==null||parsed.left.frames==null||parsed.right.frames.Length<2||parsed.left.frames.Length<2||
               parsed.right.stance<=0||parsed.right.stance>=1||parsed.left.stance<=0||parsed.left.stance>=1)
                throw new InvalidOperationException("Recorded walking motion has invalid stride data.");
            // The source trajectory residual starts at a footfall, rather than at the centre of the sway.
            float lateralCentre=0;foreach(var body in parsed.body)lateralCentre+=body.pelvis.x;lateralCentre/=parsed.body.Length;
            for(int i=0;i<parsed.body.Length;i++){var body=parsed.body[i];body.pelvis.x-=lateralCentre;parsed.body[i]=body;}
            clip=parsed;
        }
        public static float Stride { get { EnsureLoaded();return clip.stride; } }
        public static float Stance(int leg) { EnsureLoaded();return (leg==0?clip.right:clip.left).stance; }
        public static float Lead(int leg) { EnsureLoaded();return (leg==0?clip.right:clip.left).lead; }
        static void Indices(float phase,int count,out int a,out int b,out float blend)
        {float sample=Mathf.Repeat(phase,1)*count;a=Mathf.FloorToInt(sample);b=(a+1)%count;blend=sample-a;}
        public static Foot SampleFoot(float phase,int leg)
        {
            EnsureLoaded();var frames=(leg==0?clip.right:clip.left).frames;Indices(phase,frames.Length,out int a,out int b,out float t);
            // Landing progress completes at one; the next cycle's zero belongs to the new planted stance.
            float endProgress=b==0?1:frames[b].progress;
            return new Foot{progress=Mathf.Lerp(frames[a].progress,endProgress,t),lift=Mathf.Lerp(frames[a].lift,frames[b].lift,t),roll=Mathf.LerpAngle(frames[a].roll,frames[b].roll,t),knee=Vector3.Lerp(frames[a].knee,frames[b].knee,t).normalized};
        }
        public static Body SampleBody(float phase)
        {
            EnsureLoaded();var frames=clip.body;Indices(phase,frames.Length,out int i,out int j,out float t);var a=frames[i];var b=frames[j];
            return new Body{pelvis=Vector3.Lerp(a.pelvis,b.pelvis,t),leftHand=Vector3.Lerp(a.leftHand,b.leftHand,t),rightHand=Vector3.Lerp(a.rightHand,b.rightHand,t),leftElbow=Vector3.Lerp(a.leftElbow,b.leftElbow,t).normalized,rightElbow=Vector3.Lerp(a.rightElbow,b.rightElbow,t).normalized,
                pelvisYaw=Mathf.LerpAngle(a.pelvisYaw,b.pelvisYaw,t),pelvisPitch=Mathf.LerpAngle(a.pelvisPitch,b.pelvisPitch,t),pelvisRoll=Mathf.LerpAngle(a.pelvisRoll,b.pelvisRoll,t),chestYaw=Mathf.LerpAngle(a.chestYaw,b.chestYaw,t),chestLean=Mathf.LerpAngle(a.chestLean,b.chestLean,t)};
        }
    }
}
