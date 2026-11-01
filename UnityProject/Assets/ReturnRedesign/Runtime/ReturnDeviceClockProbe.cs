using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Return.Redesign
{
    // Opt-in, read-only macOS measurement using the player's existing output device.
    internal sealed class ReturnDeviceClockProbe
    {
        const string CoreAudio="/System/Library/Frameworks/CoreAudio.framework/CoreAudio";
        const uint Global=0x676c6f62,Output=0x6f757470;
        [StructLayout(LayoutKind.Sequential)]
        struct Address {public uint selector,scope,element;}
        [StructLayout(LayoutKind.Sequential)]
        struct SmpteTime
        {
            public short subframes,subframeDivisor;
            public uint counter,type,flags;
            public short hours,minutes,seconds,frames;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct DeviceTime
        {
            public double sampleTime;
            public ulong hostTime;
            public double rateScalar;
            public ulong wordClockTime;
            public SmpteTime smpteTime;
            public uint flags,reserved;
        }
        [DllImport(CoreAudio)]
        static extern int AudioObjectGetPropertyDataSize(uint id,ref Address address,uint qualifierSize,IntPtr qualifier,out uint size);
        [DllImport(CoreAudio)]
        static extern int AudioObjectGetPropertyData(uint id,ref Address address,uint qualifierSize,IntPtr qualifier,ref uint size,IntPtr data);
        [DllImport(CoreAudio)]
        static extern int AudioDeviceGetCurrentTime(uint device,ref DeviceTime time);
        [Serializable]
        sealed class Observation
        {
            public uint flags,processRunningOutput,deviceRunning,deviceRunningSomewhere;
            public double realtime,sampleTime;
            public ulong hostTime;
        }
        [Serializable]
        sealed class Report
        {
            public bool passed,settingsModified;
            public int pid,timestampBytes,smpteBytes,flagsOffset,clockStatus;
            public uint processObject,device,deviceAlive;
            public uint[] processOutputDevices;
            public double nominalSampleRate,sampleDelta,hostDelta;
            public string failure;
            public List<Observation> observations=new List<Observation>();
        }
        readonly Report report=new Report();
        bool finished;
        public void Tick()
        {
            if(finished)return;
            try
            {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                if(report.processObject==0)
                {
                    report.timestampBytes=Marshal.SizeOf<DeviceTime>();report.smpteBytes=Marshal.SizeOf<SmpteTime>();
                    report.flagsOffset=Marshal.OffsetOf<DeviceTime>("flags").ToInt32();
                    if(report.timestampBytes!=64||report.smpteBytes!=24||report.flagsOffset!=56)
                        throw new InvalidOperationException("CoreAudio timestamp layout mismatch.");
                    using(var process=System.Diagnostics.Process.GetCurrentProcess())report.pid=process.Id;
                    report.processObject=UInt(1,"id2p",Global,report.pid);
                    if(report.processObject==0||UInt(report.processObject,"ppid")!=(uint)report.pid)
                        throw new InvalidOperationException("CoreAudio process ownership not established.");
                    byte[] devices=Read(report.processObject,"pdv#",Output);
                    if(devices.Length==0||devices.Length%4!=0)throw new InvalidOperationException("No valid process output-device list.");
                    report.processOutputDevices=new uint[devices.Length/4];
                    for(int i=0;i<report.processOutputDevices.Length;i++)report.processOutputDevices[i]=BitConverter.ToUInt32(devices,i*4);
                    uint preferred=UInt(1,"dOut");report.device=report.processOutputDevices[0];
                    foreach(uint device in report.processOutputDevices)if(device==preferred)report.device=device;
                    report.deviceAlive=UInt(report.device,"livn");
                    byte[] rate=Read(report.device,"nsrt",Global);
                    if(rate.Length!=8)throw new InvalidOperationException("Invalid nominal-rate representation.");
                    report.nominalSampleRate=BitConverter.ToDouble(rate,0);
                }
                var observation=new Observation {realtime=Time.realtimeSinceStartup,
                    processRunningOutput=UInt(report.processObject,"piro"),deviceRunning=UInt(report.device,"goin"),
                    deviceRunningSomewhere=UInt(report.device,"gone")};
                report.observations.Add(observation);
                var timestamp=new DeviceTime {flags=3};
                report.clockStatus=AudioDeviceGetCurrentTime(report.device,ref timestamp);
                Check(report.clockStatus,"AudioDeviceGetCurrentTime");
                observation.flags=timestamp.flags;observation.sampleTime=timestamp.sampleTime;observation.hostTime=timestamp.hostTime;
                if((timestamp.flags&3)!=3)throw new InvalidOperationException("Device did not return both requested valid clock representations.");
                if(report.observations.Count<2)return;
                report.sampleDelta=observation.sampleTime-report.observations[0].sampleTime;
                report.hostDelta=(double)observation.hostTime-report.observations[0].hostTime;
                if(report.sampleDelta<=0||report.hostDelta<=0)throw new InvalidOperationException("Valid device-clock observations did not advance.");
                report.passed=true;
#else
                throw new PlatformNotSupportedException("Device-clock probe requires the macOS player.");
#endif
            }
            catch(Exception error){report.failure=error.GetType().Name+": "+error.Message;}
            finished=true;Debug.Log("[RETURN device-clock] "+JsonUtility.ToJson(report));
        }
        static uint UInt(uint id,string selector,uint scope=Global,int? pid=null)
        {
            byte[] bytes=Read(id,selector,scope,pid);
            if(bytes.Length!=4)throw new InvalidOperationException(selector+" has an unexpected size.");
            return BitConverter.ToUInt32(bytes,0);
        }
        static byte[] Read(uint id,string selector,uint scope,int? pid=null)
        {
            var address=new Address {selector=((uint)selector[0]<<24)|((uint)selector[1]<<16)|((uint)selector[2]<<8)|selector[3],scope=scope,element=0};
            IntPtr qualifier=IntPtr.Zero,data=IntPtr.Zero;
            try
            {
                if(pid.HasValue){qualifier=Marshal.AllocHGlobal(4);Marshal.WriteInt32(qualifier,pid.Value);}
                uint qualifierSize=pid.HasValue?4u:0u,size;
                Check(AudioObjectGetPropertyDataSize(id,ref address,qualifierSize,qualifier,out size),selector+" size");
                if(size==0)return Array.Empty<byte>();
                if(size>4096)throw new InvalidOperationException(selector+" exceeds bounded property storage.");
                data=Marshal.AllocHGlobal((int)size);uint capacity=size;
                Check(AudioObjectGetPropertyData(id,ref address,qualifierSize,qualifier,ref size,data),selector);
                if(size>capacity)throw new InvalidOperationException(selector+" returned excessive data.");
                byte[] bytes=new byte[(int)size];Marshal.Copy(data,bytes,0,bytes.Length);return bytes;
            }
            finally {if(data!=IntPtr.Zero)Marshal.FreeHGlobal(data);if(qualifier!=IntPtr.Zero)Marshal.FreeHGlobal(qualifier);}
        }
        static void Check(int status,string operation)
        {if(status!=0)throw new InvalidOperationException(operation+" failed: "+status);}
    }
}
