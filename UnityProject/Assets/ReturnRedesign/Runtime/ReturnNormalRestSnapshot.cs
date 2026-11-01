using System;
using UnityEngine;

namespace Return.Redesign
{
    public sealed class ReturnNormalRestSnapshot : ScriptableObject
    {
        public const string ResourceName="ReturnRealism/Normal-Rest-Snapshot-v95";
        public bool captured,finite,borrowedViewsCleared;
        public int weightStreams,clearedViewCount,ownedNativeAllocations,triangleCount,vertexSamples,expressionFrames,expressionDeltaSamples;
        public SourceMesh[] meshes;
        [Serializable] public sealed class SourceMesh {public string person;public Mesh mesh;public int vertexCount,indexCount;public bool importedReadable;public string[] boneNames;public Triangle[] triangles;public ExpressionFrame[] frames;}
        [Serializable] public sealed class Triangle {public int triangle;public int[] indices;public Vector3 restSurface;public Vertex[] vertices;}
        [Serializable] public sealed class Vertex {public int vertex;public Vector3 position,normal;public Influence[] influences;public Adjacent[] adjacent;}
        [Serializable] public sealed class Influence {public int bone;public string boneName;public float weight,bindParity;public Matrix4x4 bindPose;}
        [Serializable] public sealed class Adjacent {public int triangle;public int[] indices;public bool degenerate;public Vector3 restSurface;}
        [Serializable] public sealed class ExpressionFrame {public string name;public int shape,frame;public float weight;public ExpressionDelta[] deltas;}
        [Serializable] public sealed class ExpressionDelta {public int vertex;public Vector3 position,normal;}
    }
}
