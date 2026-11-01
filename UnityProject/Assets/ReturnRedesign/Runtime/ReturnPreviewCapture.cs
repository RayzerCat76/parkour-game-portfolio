using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Return.Redesign
{
    public sealed class ReturnPreviewCapture : IDisposable
    {
        const int MaximumPending = 4;
        const int PriorityPending = 2;
        int pending, disposed;
        long nextSequence, publishedSequence;
        readonly object publication = new object();
        readonly ConcurrentQueue<string> errors = new ConcurrentQueue<string>();
        readonly System.Collections.Generic.Stack<byte[]> pixelBuffers = new System.Collections.Generic.Stack<byte[]>();
        int pixelBufferLength,pixelBufferAllocations,pixelBufferReuses;
        public int PixelBufferAllocations => Volatile.Read(ref pixelBufferAllocations);
        public int PixelBufferReuses => Volatile.Read(ref pixelBufferReuses);
        readonly bool measureStages=Array.IndexOf(Environment.GetCommandLineArgs(),"--return-measure-capture")>=0;
        readonly object stageLock=new object();
        readonly long[] stageTicks=new long[9],stageMaximum=new long[9];
        readonly int[] stageCalls=new int[9];
        static readonly string[] stageNames={"acquire-target","submit-image","submit-readback","readback-latency","copy-pixels","release-target","worker-queue","encode-pixels","publish-files"};
        [Serializable] struct Stage {public string name;public int calls;public double meanMs,maximumMs;}
        [Serializable] sealed class StageReport {public Stage[] stages;}
        public bool CaptureTimingEnabled=>measureStages;
        public bool Busy => Volatile.Read(ref pending) != 0;
        public bool Full => !CanReserve();
        public bool CanReserve(bool priority=false) => Volatile.Read(ref disposed) == 0 && Volatile.Read(ref pending) < MaximumPending+(priority?PriorityPending:0);
        public bool TryGetError(out string error) => errors.TryDequeue(out error);
        public bool TryReserve(bool priority=false)
        {
            int limit=MaximumPending+(priority?PriorityPending:0);
            while (CanReserve(priority))
            {
                int count = Volatile.Read(ref pending);
                if (count >= limit) return false;
                if (Interlocked.CompareExchange(ref pending, count + 1, count) == count) return true;
            }
            return false;
        }
        // Called on the game thread after WaitForEndOfFrame; each slot owns its image.
        public void CancelReservation()=>Release();
        public void Capture(string root, string name, Func<string> snapshot, RenderTexture source=null)
        {
            try
            {
                if (Volatile.Read(ref disposed) != 0) { Release(); return; }
                int width = source!=null?source.width:Screen.width, height = source!=null?source.height:Screen.height;
                long sequence = Interlocked.Increment(ref nextSequence);
                string state = snapshot();
                if (SystemInfo.supportsAsyncGPUReadback)
                {
                    // Explicit target blits have ReadPixels row order; screen captures do not.
                    bool reverseRows = source==null&&SystemInfo.graphicsUVStartsAtTop;
                    long acquireAt=StageNow();var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);RecordStage(0,acquireAt);
                    try
                    {
                        long imageAt=StageNow();
                        RenderTexture previous=RenderTexture.active;
                        try{if(source!=null)Graphics.Blit(source,target);else ScreenCapture.CaptureScreenshotIntoRenderTexture(target);}
                        finally{RenderTexture.active=previous;}
                        RecordStage(1,imageAt);long readbackAt=StageNow();
                        AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, request =>
                        {
                            RecordStage(3,readbackAt);
                            byte[] pixels=null;
                            try
                            {
                                if (request.hasError) throw new InvalidOperationException("Asynchronous preview readback failed");
                                long copyAt=StageNow();var data=request.GetData<byte>();pixels=TakePixels(data.Length);data.CopyTo(pixels);RecordStage(4,copyAt);
                                EncodeAndPublish(root, name, state, sequence, width, height, pixels, reverseRows);pixels=null;
                            }
                            catch (Exception error) { Fail(error); }
                            finally { if(pixels!=null)ReturnPixels(pixels);long releaseAt=StageNow();RenderTexture.ReleaseTemporary(target);RecordStage(5,releaseAt); }
                        });
                        RecordStage(2,readbackAt);
                    }
                    catch { RenderTexture.ReleaseTemporary(target); throw; }
                }
                else
                {
                    byte[] pixels=null;
                    try
                    {
                        var frame = new Texture2D(width, height, TextureFormat.RGBA32, false);
                        RenderTexture previous=RenderTexture.active;
                        try { if(source!=null)RenderTexture.active=source;frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);var data=frame.GetRawTextureData<byte>();pixels=TakePixels(data.Length);data.CopyTo(pixels); }
                        finally { RenderTexture.active=previous;UnityEngine.Object.Destroy(frame); }
                        EncodeAndPublish(root, name, state, sequence, width, height, pixels, false);pixels=null;
                    }
                    finally { if(pixels!=null)ReturnPixels(pixels); }
                }
            }
            catch (Exception error) { Fail(error); }
        }
        void EncodeAndPublish(string root, string name, string state, long sequence, int width, int height, byte[] pixels, bool reverseRows)
        {
            if (Volatile.Read(ref disposed) != 0) { ReturnPixels(pixels);Release();return; }
            long queuedAt=StageNow();
            Task.Run(() =>
            {
                RecordStage(6,queuedAt);long encodeAt=StageNow();
                try
                {
                    // Top-origin render targets differ from the encoder's ReadPixels row order.
                    if (reverseRows)
                    {
                        int stride = width * 4;
                        var row = new byte[stride];
                        for (int y = 0; y < height / 2; y++)
                        {
                            int first = y * stride, last = (height - 1 - y) * stride;
                            Buffer.BlockCopy(pixels, first, row, 0, stride);
                            Buffer.BlockCopy(pixels, last, pixels, first, stride);
                            Buffer.BlockCopy(row, 0, pixels, last, stride);
                        }
                    }
                    // The former RGB screenshot was opaque regardless of backbuffer alpha.
                    for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
                    bool live = name.EndsWith(".jpg", StringComparison.Ordinal);
                    byte[] encoded = live
                        ? ImageConversion.EncodeArrayToJPG(pixels, GraphicsFormat.R8G8B8A8_UNorm, (uint)width, (uint)height, 0, 82)
                        : ImageConversion.EncodeArrayToPNG(pixels, GraphicsFormat.R8G8B8A8_UNorm, (uint)width, (uint)height, 0);
                    RecordStage(7,encodeAt);long publishAt=StageNow();
                    try
                    {
                    lock (publication)
                    {
                        if (Volatile.Read(ref disposed) != 0) return;
                        if (live && sequence <= publishedSequence) return;
                        string directory = Path.Combine(root, "Evidence/Frames");
                        Directory.CreateDirectory(directory);
                        string frame = Path.Combine(directory, name);
                        File.WriteAllBytes(frame + ".tmp", encoded);
                        Publish(frame + ".tmp", frame);
                        if (sequence > publishedSequence)
                        {
                            string stateFile = Path.Combine(root, "Preview/state.json");
                            File.WriteAllText(stateFile + ".tmp", state);
                            Publish(stateFile + ".tmp", stateFile);
                            publishedSequence = sequence;
                        }
                    }
                    }
                    finally { RecordStage(8,publishAt); }
                }
                catch (Exception error) { errors.Enqueue(error.ToString()); }
                finally { ReturnPixels(pixels);Release(); }
            });
        }
        static void Publish(string temporary, string destination)
        {
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }
        void Release() { Interlocked.Decrement(ref pending); }
        void Fail(Exception error) { errors.Enqueue(error.ToString()); Release(); }
        byte[] TakePixels(int length)
        {
            lock(pixelBuffers)
            {
                if(pixelBufferLength!=length){pixelBuffers.Clear();pixelBufferLength=length;}
                if(pixelBuffers.Count>0){Interlocked.Increment(ref pixelBufferReuses);return pixelBuffers.Pop();}
            }
            var pixels=new byte[length];Interlocked.Increment(ref pixelBufferAllocations);return pixels;
        }
        void ReturnPixels(byte[] pixels)
        {
            lock(pixelBuffers)
                if(Volatile.Read(ref disposed)==0&&pixels.Length==pixelBufferLength&&pixelBuffers.Count<MaximumPending+PriorityPending)pixelBuffers.Push(pixels);
        }
        long StageNow()=>measureStages?System.Diagnostics.Stopwatch.GetTimestamp():0;
        void RecordStage(int index,long started)
        {
            if(started==0)return;long ticks=System.Diagnostics.Stopwatch.GetTimestamp()-started;
            lock(stageLock){stageTicks[index]+=ticks;stageMaximum[index]=Math.Max(stageMaximum[index],ticks);stageCalls[index]++;}
        }
        public string DescribeStages()
        {
            var report=new StageReport{stages=new Stage[stageNames.Length]};double scale=1000.0/System.Diagnostics.Stopwatch.Frequency;
            lock(stageLock)for(int i=0;i<stageNames.Length;i++)
            {report.stages[i]=new Stage{name=stageNames[i],calls=stageCalls[i],meanMs=stageCalls[i]>0?stageTicks[i]*scale/stageCalls[i]:0,maximumMs=stageMaximum[i]*scale};stageTicks[i]=stageMaximum[i]=0;stageCalls[i]=0;}
            return JsonUtility.ToJson(report);
        }
        public void Dispose() { lock (publication) { Interlocked.Exchange(ref disposed, 1); }lock(pixelBuffers){pixelBuffers.Clear();pixelBufferLength=0;} }
    }
}
