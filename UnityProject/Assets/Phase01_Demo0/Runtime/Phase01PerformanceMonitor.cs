using System;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01PerformanceMonitor : MonoBehaviour
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
            public int unsafeMeshDestroyErrorCount;
            public int runtimeMeshCreationCount;
            public int runtimeMeshDestructionCount;
        }

        private const int Capacity = 18000;
        private readonly float[] frameTimes = new float[Capacity];
        private ProfilerRecorder gcRecorder;
        private int sampleCount;
        private long gcBytes;
        private int collectionBaseline;
        private float measurementStart;
        private bool measuring;
        private int errorCount;
        private int exceptionCount;
        private int unsafeDestroyCount;

        public float CurrentFrameMs { get; private set; }
        public float CurrentFps => CurrentFrameMs > 0.001f ? 1000f / CurrentFrameMs : 0f;
        public long CurrentGcBytes { get; private set; }
        public bool IsMeasuring => measuring;

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
            if (!measuring || sampleCount >= frameTimes.Length)
                return;

            frameTimes[sampleCount++] = CurrentFrameMs;
            gcBytes += CurrentGcBytes;
        }

        public void BeginMeasurement()
        {
            sampleCount = 0;
            gcBytes = 0L;
            errorCount = 0;
            exceptionCount = 0;
            unsafeDestroyCount = 0;
            collectionBaseline = TotalCollections();
            measurementStart = Time.realtimeSinceStartup;
            measuring = true;
        }

        public Result EndMeasurement()
        {
            measuring = false;
            float[] sorted = new float[sampleCount];
            Array.Copy(frameTimes, sorted, sampleCount);
            Array.Sort(sorted);
            double sum = 0d;
            for (int i = 0; i < sampleCount; i++)
                sum += sorted[i];

            float average = sampleCount > 0 ? (float)(sum / sampleCount) : 0f;
            return new Result
            {
                sampleCount = sampleCount,
                durationSeconds = Time.realtimeSinceStartup - measurementStart,
                averageFrameMs = average,
                medianFrameMs = Percentile(sorted, 0.5f),
                p95FrameMs = Percentile(sorted, 0.95f),
                p99FrameMs = Percentile(sorted, 0.99f),
                worstFrameMs = sampleCount > 0 ? sorted[sampleCount - 1] : 0f,
                averageFps = average > 0f ? 1000f / average : 0f,
                averageGcBytesPerFrame = sampleCount > 0 ? gcBytes / sampleCount : 0L,
                gcCollectionCount = TotalCollections() - collectionBaseline,
                errorCount = errorCount,
                exceptionCount = exceptionCount,
                unsafeMeshDestroyErrorCount = unsafeDestroyCount,
                runtimeMeshCreationCount = Phase01BranchTerrain.RuntimeMeshCreationCount,
                runtimeMeshDestructionCount = Phase01BranchTerrain.RuntimeMeshDestructionCount
            };
        }

        public static void WriteResult(string path, Result result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!measuring)
                return;
            if (type == LogType.Error || type == LogType.Assert)
                errorCount++;
            if (type == LogType.Exception)
                exceptionCount++;
            if (condition.IndexOf("Destroying Mesh assets immediately is not permitted", StringComparison.OrdinalIgnoreCase) >= 0)
                unsafeDestroyCount++;
        }

        private static int TotalCollections()
        {
            return GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        }

        private static float Percentile(float[] values, float percentile)
        {
            if (values.Length == 0)
                return 0f;
            int index = Mathf.Clamp(Mathf.CeilToInt(values.Length * percentile) - 1, 0, values.Length - 1);
            return values[index];
        }
    }
}
