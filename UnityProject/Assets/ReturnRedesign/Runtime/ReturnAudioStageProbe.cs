using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.LowLevel;
using Loop=UnityEngine.LowLevel.PlayerLoop;

namespace Return.Redesign
{
    // Same loop-preserving mechanism as ReturnSound's opt-in execution probe.
    internal sealed class ReturnAudioStageProbe : IDisposable
    {
        struct BeforeAudioStage {}
        struct AfterAudioStage {}
        struct AfterPreLateStage {}
        [Serializable]
        sealed class OrderReport {public List<string> entries=new List<string>();}
        [Serializable]
        sealed class Report
        {
            public string action;
            public bool originalPreserved,restored;
            public int beforeCount,afterCount,preLateCompleteCount;
        }
        PlayerLoopSystem original;
        int beforeCount,afterCount,preLateCompleteCount;
        bool installed,originalPreserved;
        public int BeforeCount=>Volatile.Read(ref beforeCount);
        public int AfterCount=>Volatile.Read(ref afterCount);
        public int PreLateCompleteCount=>Volatile.Read(ref preLateCompleteCount);
        public bool Installed=>Volatile.Read(ref installed);
        public bool OriginalPreserved=>Volatile.Read(ref originalPreserved);
        public void Install()
        {
            original=Loop.GetCurrentPlayerLoop();int inserted=0,preLateInserted=0;
            var order=new OrderReport();bool lateFound=false,audioFound=false;
            CollectBoundaryOrder(original,order,ref lateFound,ref audioFound);
            if(!lateFound||!audioFound)throw new InvalidOperationException("Current loop lacks the expected late-script/audio boundary.");
            Debug.Log("[RETURN control-boundary-order] "+JsonUtility.ToJson(order));
            PlayerLoopSystem candidate=AddMarkers(original,ref inserted,ref preLateInserted);
            if(inserted!=1||preLateInserted!=1||!SameLoop(original,RemoveMarkers(candidate)))
                throw new InvalidOperationException("Stage markers do not preserve one audio step, one PreLateUpdate group and the original loop.");
            Loop.SetPlayerLoop(candidate);installed=true;
            originalPreserved=SameLoop(original,RemoveMarkers(Loop.GetCurrentPlayerLoop()));
            if(!originalPreserved){Dispose();throw new InvalidOperationException("Installed audio-stage markers changed an original loop entry.");}
            Debug.Log("[RETURN control-audio-loop] "+JsonUtility.ToJson(new Report {action="installed",originalPreserved=true}));
        }
        PlayerLoopSystem AddMarkers(PlayerLoopSystem system,ref int inserted,ref int preLateInserted)
        {
            if(system.subSystemList==null)return system;
            var children=new List<PlayerLoopSystem>();
            foreach(PlayerLoopSystem child in system.subSystemList)
            {
                PlayerLoopSystem copied=AddMarkers(child,ref inserted,ref preLateInserted);
                if(child.type==typeof(UnityEngine.PlayerLoop.PostLateUpdate.UpdateAudio))
                {
                    children.Add(new PlayerLoopSystem {type=typeof(BeforeAudioStage),updateDelegate=()=>Interlocked.Increment(ref beforeCount)});
                    children.Add(copied);
                    children.Add(new PlayerLoopSystem {type=typeof(AfterAudioStage),updateDelegate=()=>Interlocked.Increment(ref afterCount)});
                    inserted++;
                }
                else children.Add(copied);
                if(child.type==typeof(UnityEngine.PlayerLoop.PreLateUpdate))
                {
                    children.Add(new PlayerLoopSystem {type=typeof(AfterPreLateStage),updateDelegate=()=>Interlocked.Increment(ref preLateCompleteCount)});
                    preLateInserted++;
                }
            }
            system.subSystemList=children.ToArray();return system;
        }
        static PlayerLoopSystem RemoveMarkers(PlayerLoopSystem system)
        {
            if(system.subSystemList==null)return system;
            var children=new List<PlayerLoopSystem>();
            foreach(PlayerLoopSystem child in system.subSystemList)
                if(child.type!=typeof(BeforeAudioStage)&&child.type!=typeof(AfterAudioStage)&&child.type!=typeof(AfterPreLateStage))children.Add(RemoveMarkers(child));
            system.subSystemList=children.ToArray();return system;
        }
        static bool SameLoop(PlayerLoopSystem left,PlayerLoopSystem right)
        {
            if(left.type!=right.type||left.updateFunction!=right.updateFunction||left.updateDelegate!=right.updateDelegate||
                left.loopConditionFunction!=right.loopConditionFunction)return false;
            int count=left.subSystemList!=null?left.subSystemList.Length:0;
            if(count!=(right.subSystemList!=null?right.subSystemList.Length:0))return false;
            for(int i=0;i<count;i++)if(!SameLoop(left.subSystemList[i],right.subSystemList[i]))return false;
            return true;
        }
        static void CollectBoundaryOrder(PlayerLoopSystem system,OrderReport report,ref bool lateFound,ref bool audioFound)
        {
            if(system.subSystemList==null)return;
            if(system.type==typeof(UnityEngine.PlayerLoop.PreLateUpdate))
            {
                bool afterScript=false;
                foreach(PlayerLoopSystem child in system.subSystemList)
                {
                    if(child.type==typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate)){afterScript=true;lateFound=true;}
                    if(afterScript)report.entries.Add(child.type!=null?child.type.FullName:"unnamed PreLateUpdate entry");
                }
                report.entries.Add("END PreLateUpdate");
            }
            else if(system.type==typeof(UnityEngine.PlayerLoop.PostLateUpdate))
            {
                foreach(PlayerLoopSystem child in system.subSystemList)
                {
                    report.entries.Add(child.type!=null?child.type.FullName:"unnamed PostLateUpdate entry");
                    if(child.type==typeof(UnityEngine.PlayerLoop.PostLateUpdate.UpdateAudio)){audioFound=true;break;}
                }
            }
            else foreach(PlayerLoopSystem child in system.subSystemList)CollectBoundaryOrder(child,report,ref lateFound,ref audioFound);
        }
        public void Dispose()
        {
            if(!installed)return;
            originalPreserved&=SameLoop(original,RemoveMarkers(Loop.GetCurrentPlayerLoop()));
            Loop.SetPlayerLoop(original);installed=false;
            bool restored=SameLoop(original,Loop.GetCurrentPlayerLoop());
            Debug.Log("[RETURN control-audio-loop] "+JsonUtility.ToJson(new Report {action="restored",originalPreserved=originalPreserved,
                restored=restored,beforeCount=BeforeCount,afterCount=AfterCount,preLateCompleteCount=PreLateCompleteCount}));
            if(!originalPreserved||!restored)throw new InvalidOperationException("Audio-stage loop preservation/restoration failed.");
        }
    }
}
