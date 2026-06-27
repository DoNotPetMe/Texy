using UnityEngine;

namespace Texy
{
    /// <summary>
    /// A lightweight, renderer-agnostic snapshot of the geometry Texy is texturing.
    /// Produced by <see cref="MeshAnalyzer"/>. Carries the statistics the procedural
    /// engine uses to seed and shape no-prompt auto generation, plus the UV data the
    /// island mapper rasterizes into masks.
    /// </summary>
    public class MeshContext
    {
        public string Name = "Mesh";
        public Renderer Renderer;
        public Mesh Mesh;

        /// <summary>Material slot this context targets (-1 = whole mesh / first slot).</summary>
        public int SubMeshIndex = -1;

        // ---- Raw UV + topology (submesh-scoped when SubMeshIndex >= 0) ----
        public Vector2[] UV;
        public int[] Triangles;

        // ---- Derived statistics used for heuristics ----
        public int VertexCount;
        public int TriangleCount;

        /// <summary>Fraction of the 0..1 UV space actually covered by triangles (0..1).</summary>
        public float UVCoverage;

        /// <summary>Bounding box size of the mesh in local space; aspect hints at body vs. prop vs. clothing.</summary>
        public Vector3 BoundsSize;

        /// <summary>Number of disconnected UV islands (a proxy for part count / complexity).</summary>
        public int IslandCount;

        /// <summary>Existing main texture resolution, if any, so we can match it. 0 = unknown.</summary>
        public int ExistingTextureSize;

        /// <summary>
        /// A stable seed derived purely from geometry. The same avatar mesh always yields
        /// the same seed, so auto-generated textures are reproducible across sessions.
        /// </summary>
        public int GeometrySeed;

        /// <summary>Heuristic classification of what this mesh likely is, used to bias auto prompts.</summary>
        public SurfaceGuess Guess = SurfaceGuess.Unknown;

        public enum SurfaceGuess
        {
            Unknown,
            BodySkin,
            Clothing,
            HairOrFur,
            HardSurface,
            Accessory
        }
    }
}
