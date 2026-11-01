using System.Threading;
using UnityEngine;

namespace Return.Redesign
{
    public sealed class ReturnVoiceOutputMeter : MonoBehaviour
    {
        int playback, observedPlayback, audible;
        int filterBlocks, audibleFilterBlocks;
        int positionEnabled, measuredPlayback, positionSamples, observedPositionSamples;
        float position, firstPosition, maximumTravel, observedTravel;
        int positionPublication,windowSamples,firstPublication,lastPublication;
        int framePublication,mainFrame,mainPaused,windowFrameSamples,windowFirstFrame,windowLastFrame,windowFirstPaused,windowLastPaused;
        long positionTicks,mainFrameTicks;
        float windowMaximumPositionAge,windowMaximumFrameAge;
        float positionY,positionMove,windowFirstX,windowFirstY,windowLastX,windowLastY,windowFirstMove,windowLastMove,windowMinimumY,windowMaximumY;
        bool positionStarted;
        [System.Serializable] public sealed class PositionWindow
        {
            public int coherentAudioSamples,firstPublication,lastPublication;
            public Vector2 firstPosition,lastPosition;
            public float firstMove,lastMove,minimumY,maximumY;
            public int coherentFrameSamples,firstMainFrame,lastMainFrame;
            public bool firstFramePaused,lastFramePaused;
            public float maximumPositionAgeSeconds,maximumFrameAgeSeconds;
        }
        // Match VoiceLevel's .003 threshold after its 7.5 RMS scale.
        const double MinimumEnergy = (.003 / 7.5) * (.003 / 7.5);
        public bool OutputObserved
        {
            get { int current = Volatile.Read(ref playback); return current != 0 && Volatile.Read(ref observedPlayback) == current; }
        }
        public float AudibleTravel => OutputObserved ? Volatile.Read(ref observedTravel) : 0;
        public int AudiblePositionSamples => OutputObserved ? Volatile.Read(ref observedPositionSamples) : 0;
        public int FilterBlocks => Volatile.Read(ref filterBlocks);
        public int AudibleFilterBlocks => Volatile.Read(ref audibleFilterBlocks);
        public PositionWindow DescribePositionWindow()
        {
            if(!OutputObserved)return null;
            return new PositionWindow{coherentAudioSamples=Volatile.Read(ref windowSamples),firstPublication=Volatile.Read(ref firstPublication),lastPublication=Volatile.Read(ref lastPublication),firstPosition=new Vector2(Volatile.Read(ref windowFirstX),Volatile.Read(ref windowFirstY)),lastPosition=new Vector2(Volatile.Read(ref windowLastX),Volatile.Read(ref windowLastY)),firstMove=Volatile.Read(ref windowFirstMove),lastMove=Volatile.Read(ref windowLastMove),minimumY=Volatile.Read(ref windowMinimumY),maximumY=Volatile.Read(ref windowMaximumY),coherentFrameSamples=Volatile.Read(ref windowFrameSamples),firstMainFrame=Volatile.Read(ref windowFirstFrame),lastMainFrame=Volatile.Read(ref windowLastFrame),firstFramePaused=Volatile.Read(ref windowFirstPaused)!=0,lastFramePaused=Volatile.Read(ref windowLastPaused)!=0,maximumPositionAgeSeconds=Volatile.Read(ref windowMaximumPositionAge),maximumFrameAgeSeconds=Volatile.Read(ref windowMaximumFrameAge)};
        }
        public void MarkMainFrame(int frame,bool paused)
        {
            Interlocked.Increment(ref framePublication);
            Volatile.Write(ref mainFrame,frame);Volatile.Write(ref mainPaused,paused?1:0);
            Interlocked.Exchange(ref mainFrameTicks,System.Diagnostics.Stopwatch.GetTimestamp());
            Interlocked.Increment(ref framePublication);
        }
        public void SetPosition(float x,bool enabled)=>SetPosition(x,0,0,enabled);
        public void SetPosition(float x,float y,float move,bool enabled)
        {
            if (!enabled) { Volatile.Write(ref positionEnabled, 0); return; }
            Interlocked.Increment(ref positionPublication);
            Volatile.Write(ref position, x);
            Volatile.Write(ref positionY,y);Volatile.Write(ref positionMove,move);
            Interlocked.Exchange(ref positionTicks,System.Diagnostics.Stopwatch.GetTimestamp());
            Interlocked.Increment(ref positionPublication);
            Volatile.Write(ref positionEnabled, 1);
        }
        public void BeginPlayback(bool enabled)
        {
            Volatile.Write(ref audible, 0);
            Interlocked.Increment(ref playback);
            SetAudible(enabled);
        }
        public void SetAudible(bool enabled) { Volatile.Write(ref audible, enabled ? 1 : 0); }
        void OnAudioFilterRead(float[] data, int channels)
        {
            Interlocked.Increment(ref filterBlocks);
            int current = Volatile.Read(ref playback);
            if (current == 0 || Volatile.Read(ref audible) == 0 || data == null || data.Length == 0) return;
            Interlocked.Increment(ref audibleFilterBlocks);
            int step = channels > 0 ? channels : 1, count = 0;
            double energy = 0;
            for (int i = 0; i < data.Length; i += step) { double sample = data[i]; energy += sample * sample; count++; }
            if (energy / count > MinimumEnergy && Volatile.Read(ref audible) != 0 && Volatile.Read(ref playback) == current)
            {
                if (measuredPlayback != current)
                { measuredPlayback = current; positionStarted = false; maximumTravel = 0; positionSamples = 0; windowSamples=0; }
                if (Volatile.Read(ref positionEnabled) != 0)
                {
                    float x = Volatile.Read(ref position);
                    if (!positionStarted) { firstPosition = x; positionStarted = true; }
                    maximumTravel = System.Math.Max(maximumTravel, System.Math.Abs(x - firstPosition));
                    positionSamples++;
                    // Diagnostic packets require one complete main-thread publication; x-travel above is unchanged.
                    int publication=Volatile.Read(ref positionPublication);
                    float sampleX=Volatile.Read(ref position),sampleY=Volatile.Read(ref positionY),sampleMove=Volatile.Read(ref positionMove);
                    long publishedAt=Interlocked.Read(ref positionTicks);
                    if((publication&1)==0&&publication==Volatile.Read(ref positionPublication)){
                        if(windowSamples==0){firstPublication=publication/2;windowFirstX=sampleX;windowFirstY=sampleY;windowFirstMove=sampleMove;windowMinimumY=windowMaximumY=sampleY;windowFrameSamples=0;windowFirstFrame=windowLastFrame=-1;windowFirstPaused=windowLastPaused=0;windowMaximumPositionAge=windowMaximumFrameAge=0;}
                        long now=System.Diagnostics.Stopwatch.GetTimestamp();
                        if(publishedAt>0)windowMaximumPositionAge=System.Math.Max(windowMaximumPositionAge,(float)System.Math.Max(0,(now-publishedAt)/(double)System.Diagnostics.Stopwatch.Frequency));
                        int frameRevision=Volatile.Read(ref framePublication),frame=Volatile.Read(ref mainFrame),paused=Volatile.Read(ref mainPaused);long frameAt=Interlocked.Read(ref mainFrameTicks);
                        if(frameAt>0&&(frameRevision&1)==0&&frameRevision==Volatile.Read(ref framePublication)){
                            if(windowFrameSamples==0){windowFirstFrame=frame;windowFirstPaused=paused;}
                            windowLastFrame=frame;windowLastPaused=paused;windowFrameSamples++;
                            windowMaximumFrameAge=System.Math.Max(windowMaximumFrameAge,(float)System.Math.Max(0,(now-frameAt)/(double)System.Diagnostics.Stopwatch.Frequency));
                        }
                        lastPublication=publication/2;windowLastX=sampleX;windowLastY=sampleY;windowLastMove=sampleMove;
                        windowMinimumY=System.Math.Min(windowMinimumY,sampleY);windowMaximumY=System.Math.Max(windowMaximumY,sampleY);windowSamples++;
                    }
                }
                Volatile.Write(ref observedTravel, maximumTravel);
                Volatile.Write(ref observedPositionSamples, positionSamples);
                Interlocked.Exchange(ref observedPlayback, current);
            }
        }
    }
}
