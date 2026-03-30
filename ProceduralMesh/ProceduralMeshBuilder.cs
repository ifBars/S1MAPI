using UnityEngine;
using S1MAPI.Core;
using S1MAPI.Extensions;
using S1MAPI.Utils;
using S1MAPI.ProceduralMesh.Generators.Primitives;

namespace S1MAPI.ProceduralMesh
{
    /// <summary>
    /// High-level fluent builder for creating procedural meshes.
    /// Provides a chainable API for constructing meshes from basic shapes.
    /// </summary>
    /// <remarks>
    /// Use the builder pattern to add shapes, configure materials, then call Build() to create the final mesh.
    /// Unity automatically handles cleanup on scene unload and application quit.
    /// </remarks>
    public sealed class ProceduralMeshBuilder
    {
        #region Internal Members

        /// <summary>
        /// INTERNAL: The name for the generated mesh.
        /// </summary>
        internal readonly string _name;

        /// <summary>
        /// INTERNAL: Accumulated vertex data.
        /// </summary>
        internal readonly List<Vector3> _vertices = new List<Vector3>();

        /// <summary>
        /// INTERNAL: Accumulated triangle indices.
        /// </summary>
        internal readonly List<int> _triangles = new List<int>();

        /// <summary>
        /// INTERNAL: Accumulated UV coordinates.
        /// </summary>
        internal readonly List<Vector2> _uvs = new List<Vector2>();

        /// <summary>
        /// INTERNAL: Accumulated normal vectors.
        /// </summary>
        internal readonly List<Vector3> _normals = new List<Vector3>();

        /// <summary>
        /// INTERNAL: Material to apply to the generated mesh.
        /// </summary>
        internal Material? _material;

        /// <summary>
        /// INTERNAL: Whether to apply flat shading.
        /// </summary>
        internal bool _applyFlatShading = false;

        #endregion

        #region Public Members

        /// <summary>
        /// Create a new procedural mesh builder.
        /// </summary>
        /// <param name="name">Name for the generated mesh</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty</exception>
        public ProceduralMeshBuilder(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name), "Mesh name cannot be null or empty");
            }

            _name = name;
        }

        /// <summary>
        /// Add a box (cube) to the mesh.
        /// </summary>
        /// <param name="center">Center position of the box</param>
        /// <param name="size">Size of the box</param>
        /// <returns>This builder for method chaining</returns>
        public ProceduralMeshBuilder AddBox(Vector3 center, Vector3 size)
        {
            BoxGenerator.Generate(_vertices, _triangles, center, size);
            DebugLog.Info($"Added box to mesh: {_name}");
            return this;
        }

        /// <summary>
        /// Add a sphere to the mesh.
        /// </summary>
        /// <param name="center">Center position of the sphere</param>
        /// <param name="radius">Radius of the sphere</param>
        /// <param name="subdivisions">Number of subdivisions (higher = smoother)</param>
        /// <returns>This builder for method chaining</returns>
        public ProceduralMeshBuilder AddSphere(Vector3 center, float radius, int subdivisions = Constants.Mesh.DefaultSphereSubdivisions)
        {
            SphereGenerator.Generate(_vertices, _triangles, _uvs, center, radius, subdivisions);
            DebugLog.Info($"Added sphere to mesh: {_name}");
            return this;
        }

        /// <summary>
        /// Add a cylinder to the mesh.
        /// </summary>
        /// <param name="start">Start position (bottom center)</param>
        /// <param name="end">End position (top center)</param>
        /// <param name="radius">Radius of the cylinder</param>
        /// <param name="segments">Number of radial segments</param>
        /// <returns>This builder for method chaining</returns>
        public ProceduralMeshBuilder AddCylinder(Vector3 start, Vector3 end, float radius, int segments = Constants.Mesh.DefaultCylinderSegments)
        {
            CylinderGenerator.Generate(_vertices, _triangles, start, end, radius, segments);
            DebugLog.Info($"Added cylinder to mesh: {_name}");
            return this;
        }

        /// <summary>
        /// Add a capsule to the mesh.
        /// </summary>
        /// <param name="start">Start position (bottom)</param>
        /// <param name="end">End position (top)</param>
        /// <param name="radius">Radius of the capsule</param>
        /// <returns>This builder for method chaining</returns>
        public ProceduralMeshBuilder AddCapsule(Vector3 start, Vector3 end, float radius)
        {
            // Add cylinder for the middle section
            AddCylinder(start + Vector3.up * radius, end - Vector3.up * radius, radius, Constants.Mesh.DefaultCylinderSegments);

            // Add hemisphere for bottom
            AddSphere(start, radius, Constants.Mesh.DefaultCapsuleSubdivisions);

            // Add hemisphere for top
            AddSphere(end, radius, Constants.Mesh.DefaultCapsuleSubdivisions);

            DebugLog.Info($"Added capsule to mesh: {_name}");
            return this;
        }

        /// <summary>
        /// Set the material for the generated mesh.
        /// </summary>
        /// <param name="material">The material to apply</param>
        /// <returns>This builder for method chaining</returns>
        public ProceduralMeshBuilder SetMaterial(Material material)
        {
            _material = material;
            return this;
        }

        /// <summary>
        /// Set a solid color for the mesh (creates a material internally).
        /// </summary>
        /// <param name="color">The color to apply</param>
        /// <returns>This builder for method chaining</returns>
        public ProceduralMeshBuilder SetColor(Color color)
        {
            _material = MaterialPresets.Opaque(color);
            return this;
        }

        /// <summary>
        /// Apply flat shading when building the mesh.
        /// Creates a low-poly aesthetic with hard edges.
        /// </summary>
        /// <returns>This builder for method chaining</returns>
        public ProceduralMeshBuilder ApplyFlatShading()
        {
            _applyFlatShading = true;
            return this;
        }

        /// <summary>
        /// Build the mesh and return it.
        /// </summary>
        /// <returns>The constructed mesh</returns>
        public Mesh BuildMesh()
        {
            if (_vertices.Count == 0)
            {
                DebugLog.Warning($"Building empty mesh: {_name}");
            }

            Mesh mesh = new Mesh
            {
                name = _name,
                vertices = _vertices.ToArray(),
                triangles = _triangles.ToArray()
            };

            if (_uvs.Count > 0)
            {
                mesh.uv = _uvs.ToArray();
            }

            if (_applyFlatShading)
            {
                mesh.ApplyFlatShading();
            }
            else
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();

            DebugLog.Info($"Built mesh: {_name} ({_vertices.Count} vertices, {_triangles.Count / 3} triangles)");
            return mesh;
        }

        /// <summary>
        /// Build a GameObject with the mesh and material applied.
        /// </summary>
        /// <returns>The created GameObject with mesh components</returns>
        public GameObject Build()
        {
            Mesh mesh = BuildMesh();

            GameObject go = new GameObject(_name);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.renderingLayerMask = uint.MaxValue;

            filter.mesh = mesh;

            if (_material != null)
            {
                renderer.material = _material;
            }
            else
            {
                // Create a default material if none specified
                renderer.material = MaterialPresets.Opaque(Color.white);
            }

            DebugLog.Info($"Built GameObject: {_name}");
            return go;
        }

        #endregion
    }
}
