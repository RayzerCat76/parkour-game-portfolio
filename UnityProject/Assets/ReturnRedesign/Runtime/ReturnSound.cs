using System.Collections.Generic;
using UnityEngine;

namespace Return.Redesign
{
    // Procedural effects and provisional spoken dialogue. Music remains deferred.
    public sealed class ReturnSound
    {
        readonly AudioSource source;
        readonly AudioSource speech;
        readonly ReturnVoiceOutputMeter speechMeter;
        readonly Dictionary<string,bool> observedSpeech=new Dictionary<string,bool>();
        readonly Dictionary<string,float> observedSpeechTravel=new Dictionary<string,float>();
        readonly Dictionary<string,int> observedSpeechSamples=new Dictionary<string,int>();
        readonly Dictionary<string,ReturnVoiceOutputMeter.PositionWindow> observedSpeechWindows=new Dictionary<string,ReturnVoiceOutputMeter.PositionWindow>();
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        readonly Dictionary<string,AudioClip> voices=new Dictionary<string,AudioClip>();
        float pulse,air;
        string voiceKey="";
        readonly float[] speechWindow=new float[512];
        bool muted,paused;
        readonly bool traceSpeech,audioProbe,audioRendererExitProbe,audioLoopProbe,audioLoopExecutionProbe;
        readonly ReturnDeviceClockProbe deviceClockProbe;
        float nextSpeechTrace;
        readonly float audioStartupTime;
        bool audioStartupChecked,audioRecoveryAttempted,audioResetAccepted,applicationPaused,audioProbeVoiceStarted,rendererExitAttempted,rendererWasRecording;
        bool audioLoopMarkersInstalled,audioLoopOriginalPreserved;
        int audioLoopBefore,audioLoopAfter,audioLoopProbeSamples;
        UnityEngine.LowLevel.PlayerLoopSystem originalAudioProbeLoop;
        struct BeforeAudioProbeStep {}
        struct AfterAudioProbeStep {}
        [System.Serializable]
        sealed class AudioLoopExecutionReport
        {
            public bool passed,originalPreserved,restored,stageReached;
            public int beforeCount,afterCount;
            public string failure;
        }
        [System.Serializable]
        sealed class SpeechTrace
        {
            public string action,key,clip,loadState,speakerMode,audioFoundation,engineAudioDisabled;
            public float realtime,clipTime,clipLength,sourceVolume,listenerVolume;
            public double dspTime;
            public int clipSamples,timeSamples,sampleRate,outputSampleRate,filterBlocks,audibleFilterBlocks;
            public int configuredSampleRate,dspBufferSize,realVoices,activeListeners;
            public bool playing,paused,muted,sourceMute,sourceEnabled,sourceActive,loop,listenerPaused,outputObserved;
            public bool focused,batchmode,applicationPaused,audioRecoveryAttempted,audioResetAccepted,rendererExitAttempted,rendererWasRecording;
        }
        [System.Serializable]
        sealed class AudioLoopStep
        {
            public string path;
            public bool nativeCallback,managedCallback,conditionCallback,matchesDefaultCallbacks;
        }
        [System.Serializable]
        sealed class AudioLoopReport
        {
            public bool passed,sameAudioRegistration;
            public string failure;
            public List<AudioLoopStep> defaultSteps=new List<AudioLoopStep>(),currentSteps=new List<AudioLoopStep>();
            public List<string> missingSteps=new List<string>(),unexpectedSteps=new List<string>();
        }
        public bool Muted {get=>muted;set{muted=value;source.mute=value;speech.mute=value;speechMeter.SetAudible(!muted&&!paused&&speech.isPlaying);}}
        public ReturnSound(GameObject owner)
        {
            audioProbe=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-audio-probe")>=0;
            if(audioProbe&&System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-audio-device-clock")>=0)
                deviceClockProbe=new ReturnDeviceClockProbe();
            audioRendererExitProbe=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-audio-renderer-exit")>=0;
            audioLoopProbe=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-audio-loop-inspect")>=0;
            audioLoopExecutionProbe=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-audio-loop-execute")>=0;
            traceSpeech=audioProbe||System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-self-test")>=0;
            audioStartupTime=Time.realtimeSinceStartup;
            source=owner.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.volume=.22f;
            var dialogue=new GameObject("Spoken dialogue");dialogue.transform.SetParent(owner.transform,false);
            speech=dialogue.AddComponent<AudioSource>();speech.playOnAwake=false;speech.spatialBlend=0;speech.volume=.64f;
            speechMeter=dialogue.AddComponent<ReturnVoiceOutputMeter>();
            foreach(string key in new[]{"doctor","nurse","mum","hospital-nurse","hospital-mum","memory-0","memory-1","memory-2","memory-3","rupture-0","rupture-1","rupture-2","rupture-4"})
            {
                AudioClip voice=Resources.Load<AudioClip>("ReturnSpeech/"+key);
                if(voice==null)throw new System.InvalidOperationException("Missing RETURN dialogue: "+key);
                voices[key]=voice;
            }
            MakeEffects();
            AudioSettings.OnAudioConfigurationChanged+=AudioConfigurationChanged;
            TraceSpeech("initialised");
        }
        void MakeEffects()
        {
            foreach(AudioClip clip in clips.Values)Object.Destroy(clip);
            clips.Clear();
            Make("jump",.13f,160,440,.18f);Make("land",.1f,65,40,.28f);
            Make("burst",.22f,280,90,.7f);Make("block",.6f,420,840,.1f);
            Make("radio",1.3f,90,480,.75f);Make("reform",.35f,110,360,.5f);
            Make("monitor",.14f,880,880,0);Make("fruit",.55f,520,780,.12f);
            Make("air",2.4f,90,60,.94f);
        }
        public void ApplicationPause(bool value)
        {applicationPaused=value;TraceSpeech(value?"application-pause":"application-resume");}
        void AudioConfigurationChanged(bool deviceWasChanged)
        {TraceSpeech("configuration-changed device="+deviceWasChanged);}
        public void TickAudioStartup()
        {
            if(audioProbe)return;
            if(audioStartupChecked)return;
            if(AudioSettings.dspTime>0){audioStartupChecked=true;return;}
            if(paused||applicationPaused||Time.realtimeSinceStartup-audioStartupTime<1)return;
            audioStartupChecked=true;
            AudioConfiguration config=AudioSettings.GetConfiguration();
            TraceSpeech("startup-clock-stalled");
            if((int)config.speakerMode==0||config.numRealVoices<=0||config.sampleRate<0||config.dspBufferSize<0)
            {TraceSpeech("startup-configuration-invalid");return;}
            audioRecoveryAttempted=true;audioResetAccepted=AudioSettings.Reset(config);
            TraceSpeech("startup-reset accepted="+audioResetAccepted);
            if(!audioResetAccepted)return;
            source.Stop();speech.Stop();RememberVoiceOutput();MakeEffects();
            foreach(AudioClip voice in voices.Values)voice.LoadAudioData();
            speechMeter.BeginPlayback(!muted&&!paused);
            if(voiceKey.Length>0){speech.clip=voices[voiceKey];speech.Play();}
        }
        void Make(string key,float duration,float from,float to,float noise)
        {
            const int rate=22050;float[] data=new float[Mathf.CeilToInt(duration*rate)];uint random=8917;float phase=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/data.Length;phase+=Mathf.Lerp(from,to,t)*Mathf.PI*2/rate;
                random=random*1664525u+1013904223u;float hiss=(random>>9)/8388608f-1;
                float envelope=Mathf.Min(1,t*40)*Mathf.Pow(1-t,1.8f);
                data[i]=(Mathf.Sin(phase)*(1-noise)+hiss*noise)*envelope*.4f;
            }
            AudioClip clip=AudioClip.Create("RETURN "+key,data.Length,1,rate,false);clip.SetData(data,0);clips[key]=clip;
        }
        public void Cue(string key){if(!Muted&&clips.TryGetValue(key,out AudioClip clip))source.PlayOneShot(clip);}
        public int PrologueStage(float time)
        {
            if(time<3)return 0;time-=3;
            int stage=1;foreach(string key in new[]{"doctor","nurse","mum"})
            {float duration=Mathf.Max(3.5f,voices[key].length+.6f);if(time<duration)return stage;time-=duration;stage++;}
            return 4;
        }
        public void PrologueVoice(int stage)
        {if(stage==0)StopSpeech();else if(stage<4)Speak(new[]{"doctor","nurse","mum"}[stage-1]);}
        public void Speak(string key)
        {if(voiceKey==key)return;TraceSpeech("replaced");speech.Stop();RememberVoiceOutput();voiceKey=key;speechMeter.BeginPlayback(!muted&&!paused);speech.clip=voices[key];speech.Play();nextSpeechTrace=Time.realtimeSinceStartup+1;TraceSpeech("play");}
        public void TickSpeechTrace()
        {
            if(audioProbe&&!audioProbeVoiceStarted)
            {
                if(audioLoopExecutionProbe)InstallAudioLoopMarkers();
                if(audioLoopProbe)TraceAudioLoop();
                if(audioRendererExitProbe)
                {rendererExitAttempted=true;rendererWasRecording=AudioRenderer.Stop();TraceSpeech("recording-mode-exit");}
                audioProbeVoiceStarted=true;Speak("doctor");return;
            }
            if(!traceSpeech||(!audioProbe&&voiceKey.Length==0)||Time.realtimeSinceStartup<nextSpeechTrace)return;
            if(audioProbe&&Time.realtimeSinceStartup-audioStartupTime>5)return;
            nextSpeechTrace=Time.realtimeSinceStartup+1;TraceSpeech(audioProbe?"startup-probe":"sample");
            if(deviceClockProbe!=null&&speech.isPlaying&&speech.clip!=null&&speech.clip.loadState==AudioDataLoadState.Loaded)
                deviceClockProbe.Tick();
            if(audioLoopExecutionProbe&&++audioLoopProbeSamples==3)FinishAudioLoopMarkers();
        }
        void InstallAudioLoopMarkers()
        {
            try
            {
                originalAudioProbeLoop=UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
                int inserted=0;
                var candidate=AddAudioLoopMarkers(originalAudioProbeLoop,ref inserted);
                if(inserted!=1||!SameLoop(originalAudioProbeLoop,RemoveAudioLoopMarkers(candidate)))
                    throw new System.InvalidOperationException("Marker insertion does not preserve exactly one native audio step and the original loop");
                UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(candidate);
                audioLoopMarkersInstalled=true;
                audioLoopOriginalPreserved=SameLoop(originalAudioProbeLoop,RemoveAudioLoopMarkers(UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop()));
                if(!audioLoopOriginalPreserved)throw new System.InvalidOperationException("Installed markers altered an original loop entry");
            }
            catch(System.Exception error)
            {
                if(audioLoopMarkersInstalled)UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(originalAudioProbeLoop);
                audioLoopMarkersInstalled=false;
                Debug.Log("[RETURN audio-execution] "+JsonUtility.ToJson(new AudioLoopExecutionReport{failure=error.GetType().Name+": "+error.Message}));
            }
        }
        UnityEngine.LowLevel.PlayerLoopSystem AddAudioLoopMarkers(UnityEngine.LowLevel.PlayerLoopSystem system,ref int inserted)
        {
            if(system.subSystemList==null)return system;
            var children=new List<UnityEngine.LowLevel.PlayerLoopSystem>();
            foreach(var child in system.subSystemList)
            {
                var copied=AddAudioLoopMarkers(child,ref inserted);
                if(child.type==typeof(UnityEngine.PlayerLoop.PostLateUpdate.UpdateAudio))
                {
                    children.Add(new UnityEngine.LowLevel.PlayerLoopSystem{type=typeof(BeforeAudioProbeStep),updateDelegate=()=>audioLoopBefore++});
                    children.Add(copied);
                    children.Add(new UnityEngine.LowLevel.PlayerLoopSystem{type=typeof(AfterAudioProbeStep),updateDelegate=()=>audioLoopAfter++});
                    inserted++;
                }
                else children.Add(copied);
            }
            system.subSystemList=children.ToArray();return system;
        }
        static UnityEngine.LowLevel.PlayerLoopSystem RemoveAudioLoopMarkers(UnityEngine.LowLevel.PlayerLoopSystem system)
        {
            if(system.subSystemList==null)return system;
            var children=new List<UnityEngine.LowLevel.PlayerLoopSystem>();
            foreach(var child in system.subSystemList)
                if(child.type!=typeof(BeforeAudioProbeStep)&&child.type!=typeof(AfterAudioProbeStep))children.Add(RemoveAudioLoopMarkers(child));
            system.subSystemList=children.ToArray();return system;
        }
        static bool SameLoop(UnityEngine.LowLevel.PlayerLoopSystem left,UnityEngine.LowLevel.PlayerLoopSystem right)
        {
            if(left.type!=right.type||left.updateFunction!=right.updateFunction||left.updateDelegate!=right.updateDelegate||
                left.loopConditionFunction!=right.loopConditionFunction)return false;
            int count=left.subSystemList!=null?left.subSystemList.Length:0;
            if(count!=(right.subSystemList!=null?right.subSystemList.Length:0))return false;
            for(int i=0;i<count;i++)if(!SameLoop(left.subSystemList[i],right.subSystemList[i]))return false;
            return true;
        }
        void FinishAudioLoopMarkers()
        {
            if(!audioLoopMarkersInstalled)return;
            var report=new AudioLoopExecutionReport{beforeCount=audioLoopBefore,afterCount=audioLoopAfter,
                originalPreserved=audioLoopOriginalPreserved,stageReached=audioLoopBefore>0&&audioLoopAfter>0};
            try
            {
                report.originalPreserved&=SameLoop(originalAudioProbeLoop,RemoveAudioLoopMarkers(UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop()));
                UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(originalAudioProbeLoop);
                report.restored=SameLoop(originalAudioProbeLoop,UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop());
                report.passed=report.originalPreserved&&report.restored&&report.stageReached;
                if(!report.passed)report.failure="Audio stage was not reached or loop preservation/restoration failed";
            }
            catch(System.Exception error){report.failure=error.GetType().Name+": "+error.Message;}
            audioLoopMarkersInstalled=false;
            Debug.Log("[RETURN audio-execution] "+JsonUtility.ToJson(report));
        }
        static void TraceAudioLoop()
        {
            var report=new AudioLoopReport();
            try
            {
                var expected=new Dictionary<string,UnityEngine.LowLevel.PlayerLoopSystem>();
                var current=new Dictionary<string,UnityEngine.LowLevel.PlayerLoopSystem>();
                CollectAudioLoop(UnityEngine.LowLevel.PlayerLoop.GetDefaultPlayerLoop(),"",expected,report.defaultSteps);
                CollectAudioLoop(UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop(),"",current,report.currentSteps);
                foreach(var step in report.currentSteps)
                {
                    if(!expected.TryGetValue(step.path,out var original))
                    {report.unexpectedSteps.Add(step.path);continue;}
                    var active=current[step.path];
                    step.matchesDefaultCallbacks=active.updateFunction==original.updateFunction&&
                        active.updateDelegate==original.updateDelegate&&active.loopConditionFunction==original.loopConditionFunction;
                }
                foreach(string path in expected.Keys)if(!current.ContainsKey(path))report.missingSteps.Add(path);
                bool callbacksMatch=true;foreach(var step in report.currentSteps)if(!step.matchesDefaultCallbacks)callbacksMatch=false;
                report.sameAudioRegistration=report.missingSteps.Count==0&&report.unexpectedSteps.Count==0&&
                    report.defaultSteps.Count==report.currentSteps.Count&&callbacksMatch;
                report.passed=report.defaultSteps.Count>0;
                if(!report.passed)report.failure="Default loop exposes no audio subsystem entries";
            }
            catch(System.Exception error){report.failure=error.GetType().Name+": "+error.Message;}
            Debug.Log("[RETURN audio-loop] "+JsonUtility.ToJson(report));
        }
        static void CollectAudioLoop(UnityEngine.LowLevel.PlayerLoopSystem system,string parent,
            Dictionary<string,UnityEngine.LowLevel.PlayerLoopSystem> entries,List<AudioLoopStep> steps)
        {
            string name=system.type!=null?system.type.FullName:"Root";
            string path=parent.Length==0?name:parent+"/"+name;
            if(system.type!=null&&system.type.Name.Contains("Audio"))
            {
                entries[path]=system;
                steps.Add(new AudioLoopStep{path=path,nativeCallback=system.updateFunction!=System.IntPtr.Zero,
                    managedCallback=system.updateDelegate!=null,conditionCallback=system.loopConditionFunction!=System.IntPtr.Zero});
            }
            if(system.subSystemList!=null)foreach(var child in system.subSystemList)CollectAudioLoop(child,path,entries,steps);
        }
        public void TraceSpeech(string action)
        {
            if(!traceSpeech)return;
            AudioClip clip=speech.clip;
            AudioConfiguration config=AudioSettings.GetConfiguration();
            Debug.Log("[RETURN speech] "+JsonUtility.ToJson(new SpeechTrace {
                action=action,key=voiceKey,clip=clip!=null?clip.name:"",loadState=clip!=null?clip.loadState.ToString():"none",
                realtime=Time.realtimeSinceStartup,dspTime=AudioSettings.dspTime,clipTime=clip!=null?speech.time:0,clipLength=clip!=null?clip.length:0,
                clipSamples=clip!=null?clip.samples:0,timeSamples=clip!=null?speech.timeSamples:0,sampleRate=clip!=null?clip.frequency:0,outputSampleRate=AudioSettings.outputSampleRate,
                playing=speech.isPlaying,paused=paused,muted=muted,sourceMute=speech.mute,sourceEnabled=speech.enabled,sourceActive=speech.gameObject.activeInHierarchy,
                loop=speech.loop,sourceVolume=speech.volume,listenerPaused=AudioListener.pause,listenerVolume=AudioListener.volume,
                speakerMode=config.speakerMode.ToString(),configuredSampleRate=config.sampleRate,dspBufferSize=config.dspBufferSize,realVoices=config.numRealVoices,activeListeners=ActiveListenerCount(),audioFoundation=AudioFoundationTrace(),engineAudioDisabled=AudioDisabledTrace(),
                focused=Application.isFocused,batchmode=Application.isBatchMode,applicationPaused=applicationPaused,audioRecoveryAttempted=audioRecoveryAttempted,audioResetAccepted=audioResetAccepted,rendererExitAttempted=rendererExitAttempted,rendererWasRecording=rendererWasRecording,
                filterBlocks=speechMeter.FilterBlocks,audibleFilterBlocks=speechMeter.AudibleFilterBlocks,outputObserved=speechMeter.OutputObserved }));
        }
        static int ActiveListenerCount()
        {
            int count=0;
            foreach(AudioListener listener in Object.FindObjectsByType<AudioListener>())
                if(listener.isActiveAndEnabled)count++;
            return count;
        }
        static string AudioDisabledTrace()
        {
            try
            {
                var property=typeof(AudioSettings).GetProperty("unityAudioDisabled",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                return property!=null&&property.PropertyType==typeof(bool)?((bool)property.GetValue(null)?"true":"false"):"unavailable";
            }
            catch(System.Exception error){return "error:"+error.GetType().Name;}
        }
        static string AudioFoundationTrace()
        {
            try
            {
                var method=typeof(AudioSettings).GetMethod("GetEnhancedConfiguration",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                if(method==null)return "unavailable";
                object config=method.Invoke(null,null);
                return config.GetType().GetField("audioFoundation")?.GetValue(config)?.ToString()??"unavailable";
            }
            catch(System.Exception){return "unavailable";}
        }
        public bool IsSpeaking(string key)=>voiceKey==key&&speech.isPlaying;
        public bool HasCompletedSpeech(string key)=>voiceKey==key&&!speech.isPlaying&&!paused;
        void RememberVoiceOutput()
        {
            if(voiceKey.Length==0||!speechMeter.OutputObserved)return;
            observedSpeech[voiceKey]=true;
            observedSpeechTravel.TryGetValue(voiceKey,out float travel);observedSpeechSamples.TryGetValue(voiceKey,out int samples);
            observedSpeechTravel[voiceKey]=Mathf.Max(travel,speechMeter.AudibleTravel);
            observedSpeechSamples[voiceKey]=System.Math.Max(samples,speechMeter.AudiblePositionSamples);
            observedSpeechWindows[voiceKey]=speechMeter.DescribePositionWindow();
        }
        public bool VoiceOutputObserved(string key)=>(voiceKey==key&&speechMeter.OutputObserved)||(observedSpeech.TryGetValue(key,out bool heard)&&heard);
        public void MarkSpeechFrame(int frame,bool paused)=>speechMeter.MarkMainFrame(frame,paused);
        public void SetSpeechPosition(float x,bool enabled)=>speechMeter.SetPosition(x,enabled);
        public void SetSpeechPosition(Vector2 position,float move,bool enabled)=>speechMeter.SetPosition(position.x,position.y,move,enabled);
        public ReturnVoiceOutputMeter.PositionWindow VoicePositionWindow(string key)
        {if(voiceKey==key&&speechMeter.OutputObserved)return speechMeter.DescribePositionWindow();observedSpeechWindows.TryGetValue(key,out var window);return window;}
        public float VoiceAudibleTravel(string key)
        {observedSpeechTravel.TryGetValue(key,out float travel);return Mathf.Max(travel,voiceKey==key?speechMeter.AudibleTravel:0);}
        public int VoiceAudiblePositionSamples(string key)
        {observedSpeechSamples.TryGetValue(key,out int samples);return System.Math.Max(samples,voiceKey==key?speechMeter.AudiblePositionSamples:0);}
        public void ResetVoiceOutputHistory(){observedSpeech.Clear();observedSpeechTravel.Clear();observedSpeechSamples.Clear();observedSpeechWindows.Clear();speechMeter.BeginPlayback(false);}
        public void StopSpeech(){speech.Stop();RememberVoiceOutput();speechMeter.SetAudible(false);TraceSpeech("stop");voiceKey="";}
        public float VoiceLevel(string key)
        {
            if(voiceKey!=key||!speech.isPlaying||paused)return 0;
            speech.GetOutputData(speechWindow,0);float energy=0;
            for(int i=0;i<speechWindow.Length;i++)energy+=speechWindow[i]*speechWindow[i];
            return Mathf.Clamp01(Mathf.Sqrt(energy/speechWindow.Length)*7.5f);
        }
        public void Pause(bool value)
        {if(paused==value)return;paused=value;if(value){source.Pause();speech.Pause();}else{source.UnPause();speech.UnPause();}speechMeter.SetAudible(!muted&&!paused&&speech.isPlaying);TraceSpeech(value?"pause":"unpause");}
        public void Hospital(float dt,bool active)
        {if(!active){pulse=air=0;return;}pulse-=dt;air-=dt;if(pulse<=0){Cue("monitor");pulse=1.3f;}if(air<=0){Cue("air");air=3.8f;}}
        public void Dispose(){FinishAudioLoopMarkers();AudioSettings.OnAudioConfigurationChanged-=AudioConfigurationChanged;if(speechMeter!=null)speechMeter.SetAudible(false);if(speech!=null)Object.Destroy(speech.gameObject);foreach(AudioClip clip in clips.Values)Object.Destroy(clip);}
    }
}
