using UnityEngine;

namespace S1MAPI.ProceduralMesh.Generators.Primitives
{
    /// <summary>
    /// Generates a unit-cube mesh (-0.5 to 0.5) with two submeshes: one for the
    /// interior face and one for all other faces (exterior + edges).
    /// Used by WallBuilder when an interior material is specified so that
    /// interior and exterior wall faces can have different materials.
    /// </summary>
    internal static class DualMaterialBoxGenerator
    {
        /// <summary>Which face of the unit cube is the interior (room-facing) side.</summary>
        internal enum InteriorFace
        {
            /// <summary>-Z face (north wall interior).</summary>
            NegZ,
            /// <summary>+Z face (south wall interior).</summary>
            PosZ,
            /// <summary>-X face (east wall interior).</summary>
            NegX,
            /// <summary>+X face (west wall interior).</summary>
            PosX
        }

        private static readonly Mesh?[] Cache = new Mesh?[4];

        /// <summary>
        /// Get a cached unit-cube mesh with 2 submeshes for the given interior face.
        /// Submesh 0 = exterior (5 faces), submesh 1 = interior (1 face).
        /// </summary>
        internal static Mesh Generate(InteriorFace interiorFace)
        {
            int index = (int)interiorFace;
            Mesh? cached = Cache[index];
            if (cached != null)
                return cached;

            Mesh mesh = BuildMesh(interiorFace);
            Cache[index] = mesh;
            return mesh;
        }

        private static Mesh BuildMesh(InteriorFace interiorFace)
        {
            // 6 faces × 4 vertices = 24 vertices with per-face normals and UVs
            Vector3[] vertices = new Vector3[24];
            Vector3[] normals = new Vector3[24];
            Vector2[] uvs = new Vector2[24];

            // Face definitions: (normal, tangent axis 1, tangent axis 2, center offset)
            // Each face has 4 verts at corners: center ± t1*0.5 ± t2*0.5
            var faces = new[]
            {
                (normal: Vector3.right,   t1: Vector3.forward, t2: Vector3.up,      offset: Vector3.right * 0.5f),   // +X
                (normal: Vector3.left,    t1: Vector3.back,    t2: Vector3.up,      offset: Vector3.left * 0.5f),    // -X
                (normal: Vector3.up,      t1: Vector3.right,   t2: Vector3.forward, offset: Vector3.up * 0.5f),      // +Y
                (normal: Vector3.down,    t1: Vector3.right,   t2: Vector3.back,    offset: Vector3.down * 0.5f),    // -Y
                (normal: Vector3.forward, t1: Vector3.left,    t2: Vector3.up,      offset: Vector3.forward * 0.5f), // +Z
                (normal: Vector3.back,    t1: Vector3.right,   t2: Vector3.up,      offset: Vector3.back * 0.5f)     // -Z
            };

            // Map InteriorFace enum to face index
            int interiorFaceIndex = interiorFace switch
            {
                InteriorFace.PosX => 0, // +X
                InteriorFace.NegX => 1, // -X
                InteriorFace.PosZ => 4, // +Z
                InteriorFace.NegZ => 5, // -Z
                _ => 5
            };

            int[] exteriorTris = new int[30]; // 5 faces × 6 indices
            int[] interiorTris = new int[6];  // 1 face × 6 indices
            int ei = 0, ii = 0;

            for (int f = 0; f < 6; f++)
            {
                int vi = f * 4;
                var (normal, t1, t2, offset) = faces[f];

                vertices[vi + 0] = offset - t1 * 0.5f - t2 * 0.5f;
                vertices[vi + 1] = offset + t1 * 0.5f - t2 * 0.5f;
                vertices[vi + 2] = offset + t1 * 0.5f + t2 * 0.5f;
                vertices[vi + 3] = offset - t1 * 0.5f + t2 * 0.5f;

                normals[vi + 0] = normal;
                normals[vi + 1] = normal;
                normals[vi + 2] = normal;
                normals[vi + 3] = normal;

                uvs[vi + 0] = new Vector2(0f, 0f);
                uvs[vi + 1] = new Vector2(1f, 0f);
                uvs[vi + 2] = new Vector2(1f, 1f);
                uvs[vi + 3] = new Vector2(0f, 1f);

                if (f == interiorFaceIndex)
                {
                    interiorTris[ii++] = vi + 0;
                    interiorTris[ii++] = vi + 2;
                    interiorTris[ii++] = vi + 1;
                    interiorTris[ii++] = vi + 0;
                    interiorTris[ii++] = vi + 3;
                    interiorTris[ii++] = vi + 2;
                }
                else
                {
                    exteriorTris[ei++] = vi + 0;
                    exteriorTris[ei++] = vi + 2;
                    exteriorTris[ei++] = vi + 1;
                    exteriorTris[ei++] = vi + 0;
                    exteriorTris[ei++] = vi + 3;
                    exteriorTris[ei++] = vi + 2;
                }
            }

            Mesh mesh = new Mesh
            {
                name = $"DualMaterialBox_{interiorFace}",
                vertices = vertices,
                normals = normals,
                uv = uvs,
                subMeshCount = 2
            };

            mesh.SetTriangles(exteriorTris, 0);
            mesh.SetTriangles(interiorTris, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }
    }
}
