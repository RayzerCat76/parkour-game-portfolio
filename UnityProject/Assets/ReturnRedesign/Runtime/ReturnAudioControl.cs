using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Return.Redesign
{
    // Speech-only comparison, entered before ReturnGame initializes its world or sound system.
    public sealed class ReturnAudioControl : MonoBehaviour
    {
        [Serializable]
        sealed class Sample
        {
            public float realtime,clipTime;
            public double dspTime;
            public int timeSamples,filterBlocks,audibleFilterBlocks;
            public bool playing,focused,runInBackground;
        }
        [Serializable]
        sealed class FrameTrace
        {
            public string boundary;
            public int updateCount,frameCount;
            public double monotonicSeconds,unityRealtime;
            public bool focused,runInBackground,controlEnabled,objectActive;
        }
        [Serializable]
        sealed class Report
        {
            public bool passed,batchmode,listenerPaused,applicationPaused,sourceMute,gameInitialized,settingsModified;
            public bool loadRequestIssued,loadRequestReturned,playbackRequested;
            public bool focused,runInBackground;
            public int pid,activeListeners,activeSources,outputSampleRate,clipSamples;
            public float listenerVolume,sourceVolume;
            public string failure,loadState,initialLoadState;
            public List<Sample> samples=new List<Sample>();
        }
        readonly Report report=new Report();
        AudioSource speech;
        AudioClip pendingClip;
        int filterBlocks,audibleBlocks;
        float nextSample;
        float loadDeadline;
        bool finished;
        bool updateEntryLogged,sampleBoundaryLogged;
        int updateCount;
        bool lateUpdateLogged;
        int lateUpdateCount;
        double lastUpdateMonotonic,lastUpdateUnityRealtime;
        Timer counterSnapshotTimer;
        ReturnAudioStageProbe audioStageProbe;
        void Start()
        {
            try
            {
                Application.runInBackground=true;
                using(var process=System.Diagnostics.Process.GetCurrentProcess())report.pid=process.Id;
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--return-audio-control-stage")>=0)
                {audioStageProbe=new ReturnAudioStageProbe();audioStageProbe.Install();}
                StartCounterSnapshot();
                report.batchmode=Application.isBatchMode;
                report.outputSampleRate=AudioSettings.outputSampleRate;
                foreach(AudioListener listener in UnityEngine.Object.FindObjectsByType<AudioListener>())
                    if(listener.isActiveAndEnabled)report.activeListeners++;
                if(report.activeListeners==0)
                {
                    var camera=new GameObject("Speech control camera");camera.transform.SetParent(transform,false);
                    camera.AddComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;
                    camera.AddComponent<AudioListener>();report.activeListeners=1;
                }
                if(report.activeListeners!=1)throw new InvalidOperationException("Speech control requires one active listener.");
                foreach(AudioSource source in UnityEngine.Object.FindObjectsByType<AudioSource>())
                    if(source.isActiveAndEnabled)report.activeSources++;
                if(report.activeSources!=0)throw new InvalidOperationException("An existing source prevents an isolated speech control.");
                pendingClip=Resources.Load<AudioClip>("ReturnSpeech/doctor");
                if(pendingClip==null)throw new InvalidOperationException("Doctor speech asset missing.");
                report.initialLoadState=pendingClip.loadState.ToString();report.clipSamples=pendingClip.samples;
                report.loadRequestIssued=true;report.loadRequestReturned=pendingClip.LoadAudioData();
                report.loadState=pendingClip.loadState.ToString();
                if(pendingClip.loadState==AudioDataLoadState.Loaded)BeginPlayback();
                else if(pendingClip.loadState==AudioDataLoadState.Loading)loadDeadline=Time.realtimeSinceStartup+5;
                else throw new InvalidOperationException("Doctor audio-data request left state "+report.loadState+" (returned "+report.loadRequestReturned+").");
            }
            catch(Exception error){Finish(error.GetType().Name+": "+error.Message);}
        }
        void Update()
        {
            if(!updateEntryLogged){updateEntryLogged=true;Debug.Log("[RETURN speech-control-boundary] first-update-entered");}
            int count=Interlocked.Increment(ref updateCount);
            Volatile.Write(ref lastUpdateMonotonic,MonotonicSeconds());
            Volatile.Write(ref lastUpdateUnityRealtime,Time.realtimeSinceStartupAsDouble);
            if(count<=2)TraceFrame("update-"+count);
            if(finished)return;
            if(speech==null)
            {
                try
                {
                    report.loadState=pendingClip.loadState.ToString();
                    if(pendingClip.loadState==AudioDataLoadState.Loaded)BeginPlayback();
                    else if(pendingClip.loadState!=AudioDataLoadState.Loading)Finish("Doctor audio data entered "+report.loadState+".");
                    else if(Time.realtimeSinceStartup>=loadDeadline)Finish("Doctor audio data exceeded the five-second loading wait.");
                }
                catch(Exception error){Finish(error.GetType().Name+": "+error.Message);}
                return;
            }
            if(Time.realtimeSinceStartup<nextSample)return;
            nextSample=Time.realtimeSinceStartup+1;
            bool traceBoundary=!sampleBoundaryLogged;
            if(traceBoundary)Debug.Log("[RETURN speech-control-boundary] due-capture-entered");
            Capture();
            if(traceBoundary){sampleBoundaryLogged=true;Debug.Log("[RETURN speech-control-boundary] due-capture-completed");}
            if(report.samples.Count<3)return;
            Sample first=report.samples[0],last=report.samples[2];
            if(last.dspTime<=first.dspTime){Finish("Unity DSP clock did not advance in the speech-only control.");return;}
            if(last.timeSamples<=first.timeSamples&&last.playing){Finish("Speech sample position did not advance.");return;}
            if(last.filterBlocks==0||last.audibleFilterBlocks==0){Finish("Speech control did not receive nonzero audio-filter output.");return;}
            report.passed=true;Finish(null);
        }
        void LateUpdate()
        {
            Interlocked.Increment(ref lateUpdateCount);
            if(lateUpdateLogged)return;lateUpdateLogged=true;TraceFrame("late-update-1");
        }
        static double MonotonicSeconds()
        {return (double)System.Diagnostics.Stopwatch.GetTimestamp()/System.Diagnostics.Stopwatch.Frequency;}
        void StartCounterSnapshot()
        {
            string[] args=Environment.GetCommandLineArgs();string output=null;
            for(int i=0;i+1<args.Length;i++)if(args[i]=="--return-control-snapshot")output=args[i+1];
            if(output==null)return;
            if(!System.IO.Path.IsPathRooted(output))throw new InvalidOperationException("Control snapshot requires an absolute output path.");
            int pid=report.pid;double started=MonotonicSeconds();string snapshotPath=output;
            counterSnapshotTimer=new Timer(_=>
            {
                // No Unity calls on this worker: only managed counters, clocks and file output.
                try
                {
                    var culture=System.Globalization.CultureInfo.InvariantCulture;
                    string json="{\"pid\":"+pid+",\"elapsedSeconds\":"+(MonotonicSeconds()-started).ToString("R",culture)
                        +",\"updateCount\":"+Volatile.Read(ref updateCount)+",\"lateUpdateCount\":"+Volatile.Read(ref lateUpdateCount)
                        +",\"lastUpdateMonotonic\":"+Volatile.Read(ref lastUpdateMonotonic).ToString("R",culture)
                        +",\"lastUpdateUnityRealtime\":"+Volatile.Read(ref lastUpdateUnityRealtime).ToString("R",culture)
                        +",\"filterBlocks\":"+Volatile.Read(ref filterBlocks)+",\"audibleFilterBlocks\":"+Volatile.Read(ref audibleBlocks)
                        +",\"audioBeforeCount\":"+(audioStageProbe!=null?audioStageProbe.BeforeCount:0)
                        +",\"audioAfterCount\":"+(audioStageProbe!=null?audioStageProbe.AfterCount:0)
                        +",\"preLateCompleteCount\":"+(audioStageProbe!=null?audioStageProbe.PreLateCompleteCount:0)
                        +",\"audioMarkersInstalled\":"+(audioStageProbe!=null&&audioStageProbe.Installed).ToString().ToLowerInvariant()
                        +",\"originalLoopPreserved\":"+(audioStageProbe!=null&&audioStageProbe.OriginalPreserved).ToString().ToLowerInvariant()+"}\n";
                    System.IO.File.WriteAllText(snapshotPath+".tmp",json);
                    System.IO.File.Move(snapshotPath+".tmp",snapshotPath);
                }
                catch(Exception error)
                {try{System.IO.File.WriteAllText(snapshotPath+".error.txt",error.GetType().Name+": "+error.Message);}catch{}}
            },null,5000,Timeout.Infinite);
        }
        void TraceFrame(string boundary)
        {
            Debug.Log("[RETURN speech-control-frame] "+JsonUtility.ToJson(new FrameTrace {
                boundary=boundary,updateCount=updateCount,
                monotonicSeconds=(double)System.Diagnostics.Stopwatch.GetTimestamp()/System.Diagnostics.Stopwatch.Frequency,
                unityRealtime=Time.realtimeSinceStartupAsDouble,frameCount=Time.frameCount,
                focused=Application.isFocused,runInBackground=Application.runInBackground,
                controlEnabled=enabled,objectActive=gameObject.activeInHierarchy}));
        }
        void BeginPlayback()
        {
            speech=gameObject.AddComponent<AudioSource>();speech.playOnAwake=false;speech.spatialBlend=0;
            speech.volume=.64f;speech.loop=false;speech.clip=pendingClip;report.activeSources=1;
            report.listenerPaused=AudioListener.pause;report.listenerVolume=AudioListener.volume;
            report.sourceMute=speech.mute;report.sourceVolume=speech.volume;
            report.focused=Application.isFocused;report.runInBackground=Application.runInBackground;
            Debug.Log("[RETURN speech-control-ready] "+JsonUtility.ToJson(report));
            speech.Play();report.playbackRequested=true;Capture();nextSample=Time.realtimeSinceStartup+1;
        }
        void Capture()
        {
            var sample=new Sample {realtime=Time.realtimeSinceStartup,dspTime=AudioSettings.dspTime,
                clipTime=speech.time,timeSamples=speech.timeSamples,playing=speech.isPlaying,
                focused=Application.isFocused,runInBackground=Application.runInBackground,
                filterBlocks=Volatile.Read(ref filterBlocks),audibleFilterBlocks=Volatile.Read(ref audibleBlocks)};
            report.samples.Add(sample);Debug.Log("[RETURN speech-control-sample] "+JsonUtility.ToJson(sample));
        }
        void OnAudioFilterRead(float[] data,int channels)
        {
            Interlocked.Increment(ref filterBlocks);
            for(int i=0;i<data.Length;i++)if(data[i]>.00001f||data[i]<-.00001f)
            {Interlocked.Increment(ref audibleBlocks);break;}
        }
        void OnApplicationPause(bool value){report.applicationPaused=value;}
        void Finish(string failure)
        {
            if(finished)return;finished=true;report.failure=failure;
            counterSnapshotTimer?.Dispose();
            try{audioStageProbe?.Dispose();}
            catch(Exception error){report.passed=false;report.failure="Audio-stage cleanup: "+error.Message;}
            report.focused=Application.isFocused;report.runInBackground=Application.runInBackground;
            Debug.Log("[RETURN speech-control] "+JsonUtility.ToJson(report));
        }
        void OnDestroy(){counterSnapshotTimer?.Dispose();audioStageProbe?.Dispose();}
    }
}
