using System;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace Phase01.MovementSandbox
{
    public sealed class Phase01SandboxPerformanceMonitor : MonoBehaviour
    {
        [Serializable]
        public sealed class Result
        {
            public int sampleCount;
            public float durationSeconds;
            public float averageFrameMs;
            public float medianFrameMs;
            public float p95FrameMs;
            public float p99FrameMs;
            public float worstFrameMs;
            public float averageFps;
            public long averageGcBytesPerFrame;
            public int gcCollectionCount;
            public int errorCount;
            public int exceptionCount;
            public int unsafeMeshErrorCount;
        }

        private readonly float[] samples = new float[18000];
        private ProfilerRecorder gcRecorder;
        private int count;
        private long gcBytes;
        private int collectionBaseline;
        private float started;
        private bool measuring;
        private int errors;
        private int exceptions;
        private int unsafeMeshErrors;

        public float CurrentFrameMs { get; private set; }
        public float CurrentFps => CurrentFrameMs > 0.001f ? 1000f / CurrentFrameMs : 0f;
        public long CurrentGcBytes { get; private set; }

        private void OnEnable()
        {
            gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            Application.logMessageReceived += OnLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
            gcRecorder.Dispose();
        }

        private void Update()
        {
            CurrentFrameMs = Time.unscaledDeltaTime * 1000f;
            CurrentGcBytes = gcRecorder.Valid ? gcRecorder.LastValue : 0L;
            if (!measuring || count >= samples.Length)
                return;
            samples[count++] = CurrentFrameMs;
            gcBytes += CurrentGcBytes;
        }

        public void BeginMeasurement()
        {
            count = 0;
            gcBytes = 0;
            errors = 0;
            exceptions = 0;
            unsafeMeshErrors = 0;
            collectionBaseline = Collections();
            started = Time.realtimeSinceStartup;
            measuring = true;
        }

        public Result EndMeasurement()
        {
            measuring = false;
            float[] sorted = new float[count];
            Array.Copy(samples, sorted, count);
            Array.Sort(sorted);
            double sum = 0d;
            for (int i = 0; i < count; i++)
                sum += sorted[i];
            float average = count > 0 ? (float)(sum / count) : 0f;
            return new Result
            {
                sampleCount = count,
                durationSeconds = Time.realtimeSinceStartup - started,
                averageFrameMs = average,
                medianFrameMs = Percentile(sorted, 0.5f),
                p95FrameMs = Percentile(sorted, 0.95f),
                p99FrameMs = Percentile(sorted, 0.99f),
                worstFrameMs = count > 0 ? sorted[count - 1] : 0f,
                averageFps = average > 0f ? 1000f / average : 0f,
                averageGcBytesPerFrame = count > 0 ? gcBytes / count : 0L,
                gcCollectionCount = Collections() - collectionBaseline,
                errorCount = errors,
                exceptionCount = exceptions,
                unsafeMeshErrorCount = unsafeMeshErrors
            };
        }

        public static void Write(string path, Result result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!measuring)
                return;
            if (type == LogType.Error || type == LogType.Assert)
                errors++;
            if (type == LogType.Exception)
                exceptions++;
            if (condition.IndexOf("Destroying Mesh assets immediately is not permitted", StringComparison.OrdinalIgnoreCase) >= 0)
                unsafeMeshErrors++;
        }

        private static int Collections() => GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);

        private static float Percentile(float[] values, float percentile)
        {
            if (values.Length == 0)
                return 0f;
            return values[Mathf.Clamp(Mathf.CeilToInt(values.Length * percentile) - 1, 0, values.Length - 1)];
        }
    }
}
