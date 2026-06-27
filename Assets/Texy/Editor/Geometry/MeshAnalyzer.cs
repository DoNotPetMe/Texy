using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Texy
{
    /// <summary>
    /// Inspects a selected avatar part (SkinnedMeshRenderer or MeshRenderer) and produces a
    /// <see cref="MeshContext"/>. This is what makes "generate without a prompt" meaningful:
    /// the statistics here let the procedural engine choose sensible materials, resolutions
    /// and patterns that fit the geometry.
    /// </summary>
    public static class MeshAnalyzer
    {
        public static bool TryAnalyze(GameObject go, int subMeshIndex, out MeshContext context)
        {
            context = null;
            if (go == null) return false;

            var renderer = go.GetComponent<Renderer>();
            Mesh mesh = ExtractMesh(renderer);
            if (mesh == null)
            {
                // Allow selecting a parent: search children for the first renderer with a mesh.
                renderer = go.GetComponentInChildren<Renderer>();
                mesh = ExtractMesh(renderer);
            }

            if (mesh == null || renderer == null)
                return false;

            context = Build(renderer, mesh, subMeshIndex);
            return true;
        }

        private static Mesh ExtractMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer smr)
                return smr.sharedMesh;
            if (renderer is MeshRenderer)
            {
                var mf = renderer.GetComponent<MeshFilter>();
                return mf != null ? mf.sharedMesh : null;
            }
            return null;
        }

        private static MeshContext Build(Renderer renderer, Mesh mesh, int subMeshIndex)
        {
            var ctx = new MeshContext
            {
                Name = renderer.gameObject.name,
                Renderer = renderer,
                Mesh = mesh,
                SubMeshIndex = subMeshIndex,
                VertexCount = mesh.vertexCount,
                BoundsSize = mesh.bounds.size
            };

            var uvList = new List<Vector2>();
            mesh.GetUVs(0, uvList);
            ctx.UV = uvList.ToArray();

            // Scope triangles to a submesh when requested so per-material slots can differ.
            if (subMeshIndex >= 0 && subMeshIndex < mesh.subMeshCount)
                ctx.Triangles = mesh.GetTriangles(subMeshIndex);
            else
                ctx.Triangles = mesh.triangles;

            ctx.TriangleCount = ctx.Triangles.Length / 3;
            ctx.UVCoverage = EstimateUVCoverage(ctx.UV, ctx.Triangles);
            ctx.IslandCount = Mathf.Max(1, EstimateIslandCount(ctx.UV, ctx.Triangles));
            ctx.ExistingTextureSize = ProbeExistingTextureSize(renderer, subMeshIndex);
            ctx.GeometrySeed = ComputeGeometrySeed(ctx);
            ctx.Guess = Classify(ctx);
            return ctx;
        }

        /// <summary>
        /// Coarse UV coverage via a low-res occupancy grid. Cheap and good enough to tell a
        /// tightly-packed atlas from a sparse decal sheet.
        /// </summary>
        private static float EstimateUVCoverage(Vector2[] uv, int[] tris)
        {
            const int grid = 64;
            if (uv == null || uv.Length == 0 || tris == null || tris.Length < 3)
                return 0f;

            var occupied = new bool[grid * grid];
            int filled = 0;

            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector2 a = SafeUV(uv, tris[i]);
                Vector2 b = SafeUV(uv, tris[i + 1]);
                Vector2 c = SafeUV(uv, tris[i + 2]);

                // Mark the cells under this triangle's bounding box (approximate but fast).
                float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
                float maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                float minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
                float maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));

                int x0 = Mathf.Clamp(Mathf.FloorToInt(Frac01(minX) * grid), 0, grid - 1);
                int x1 = Mathf.Clamp(Mathf.FloorToInt(Frac01(maxX) * grid), 0, grid - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(Frac01(minY) * grid), 0, grid - 1);
                int y1 = Mathf.Clamp(Mathf.FloorToInt(Frac01(maxY) * grid), 0, grid - 1);

                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int idx = y * grid + x;
                    if (!occupied[idx]) { occupied[idx] = true; filled++; }
                }
            }

            return (float)filled / (grid * grid);
        }

        /// <summary>
        /// Union-find over shared vertices to count connected UV islands. Bounded work so it
        /// stays responsive on dense avatar meshes.
        /// </summary>
        private static int EstimateIslandCount(Vector2[] uv, int[] tris)
        {
            if (uv == null || uv.Length == 0 || tris == null || tris.Length < 3)
                return 1;

            int n = uv.Length;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            int Find(int x)
            {
                while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                return x;
            }
            void Union(int a, int b)
            {
                int ra = Find(a), rb = Find(b);
                if (ra != rb) parent[ra] = rb;
            }

            int limit = Mathf.Min(tris.Length, 300000); // guard against pathological meshes
            for (int i = 0; i + 2 < limit; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                if (a >= n || b >= n || c >= n) continue;
                Union(a, b);
                Union(b, c);
            }

            var roots = new HashSet<int>();
            // Only count roots that are actually referenced by triangles.
            for (int i = 0; i < limit; i++)
            {
                int v = tris[i];
                if (v < n) roots.Add(Find(v));
            }
            return roots.Count;
        }

        private static int ProbeExistingTextureSize(Renderer renderer, int subMeshIndex)
        {
            var mats = renderer.sharedMaterials;
            if (mats == null || mats.Length == 0) return 0;

            int slot = subMeshIndex >= 0 && subMeshIndex < mats.Length ? subMeshIndex : 0;
            var mat = mats[slot];
            if (mat == null) return 0;

            var tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            return tex != null ? Mathf.Max(tex.width, tex.height) : 0;
        }

        private static int ComputeGeometrySeed(MeshContext ctx)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + ctx.VertexCount;
                h = h * 31 + ctx.TriangleCount;
                h = h * 31 + Mathf.RoundToInt(ctx.UVCoverage * 1000f);
                h = h * 31 + ctx.IslandCount;
                h = h * 31 + Mathf.RoundToInt(ctx.BoundsSize.sqrMagnitude * 1000f);
                h = h * 31 + (ctx.Name != null ? ctx.Name.GetHashCode() : 0);
                return h & 0x7fffffff;
            }
        }

        /// <summary>
        /// Heuristic surface classification from name hints + geometry. Deliberately conservative:
        /// it only biases the auto prompt, and the user can always override with their own prompt.
        /// </summary>
        private static MeshContext.SurfaceGuess Classify(MeshContext ctx)
        {
            string n = (ctx.Name ?? string.Empty).ToLowerInvariant();

            if (Contains(n, "body", "skin", "torso", "head", "face")) return MeshContext.SurfaceGuess.BodySkin;
            if (Contains(n, "hair", "fur", "tail", "ear")) return MeshContext.SurfaceGuess.HairOrFur;
            if (Contains(n, "cloth", "shirt", "dress", "pant", "skirt", "hood", "jacket", "outfit", "sock", "glove")) return MeshContext.SurfaceGuess.Clothing;
            if (Contains(n, "armor", "metal", "weapon", "horn", "claw", "boot", "shoe")) return MeshContext.SurfaceGuess.HardSurface;
            if (Contains(n, "ring", "chain", "badge", "glass", "acc")) return MeshContext.SurfaceGuess.Accessory;

            // Fall back to geometry: very flat aspect + low island count often = clothing panels;
            // dense, well-packed atlases lean body; tiny sparse meshes lean accessory.
            var s = ctx.BoundsSize;
            float maxDim = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            float minDim = Mathf.Max(0.0001f, Mathf.Min(s.x, Mathf.Min(s.y, s.z)));
            float aspect = maxDim / minDim;

            if (ctx.UVCoverage > 0.55f && ctx.IslandCount <= 12) return MeshContext.SurfaceGuess.BodySkin;
            if (aspect > 6f) return MeshContext.SurfaceGuess.Accessory;
            if (ctx.IslandCount > 20) return MeshContext.SurfaceGuess.Clothing;
            return MeshContext.SurfaceGuess.Unknown;
        }

        private static bool Contains(string haystack, params string[] needles)
        {
            foreach (var s in needles)
                if (haystack.Contains(s)) return true;
            return false;
        }

        private static Vector2 SafeUV(Vector2[] uv, int index)
        {
            return (index >= 0 && index < uv.Length) ? uv[index] : Vector2.zero;
        }

        // Wrap into 0..1 for tileable coverage estimation.
        private static float Frac01(float v)
        {
            v -= Mathf.Floor(v);
            if (v < 0f) v += 1f;
            return v;
        }
    }
}
