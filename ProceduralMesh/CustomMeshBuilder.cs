using UnityEngine;
using S1MAPI.Core;
using S1MAPI.Utils;
using System.Collections.Generic;

namespace S1MAPI.ProceduralMesh
{
    /// <summary>
    /// Simple fluent builder for creating custom meshes from scratch.
    /// Perfect for mods that need custom geometry (furniture, decorations, etc.).
    /// </summary>
    /// <example>
    /// <code>
    /// var mesh = new CustomMeshBuilder("MyChair")
    ///     .AddVertex(new Vector3(0, 0, 0))
    ///     .AddVertex(new Vector3(1, 0, 0))
    ///     .AddVertex(new Vector3(0.5f, 1, 0))
    ///     .AddTriangle(0, 1, 2)
    ///     .Build();
    /// </code>
    /// </example>
    public sealed class CustomMeshBuilder
    {
        private readonly string _name;
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<int> _triangles = new List<int>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private Material? _material;
        private bool _calculateNormals = true;

        /// <summary>
        /// Create a new custom mesh builder.
        /// </summary>
        /// <param name="name">Name for the generated mesh</param>
        public CustomMeshBuilder(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Add a single vertex.
        /// </summary>
        public CustomMeshBuilder AddVertex(Vector3 vertex)
        {
            _vertices.Add(vertex);
            return this;
        }

        /// <summary>
        /// Add multiple vertices at once.
        /// </summary>
        public CustomMeshBuilder AddVertices(params Vector3[] vertices)
        {
            _vertices.AddRange(vertices);
            return this;
        }

        /// <summary>
        /// Add a triangle (counter-clockwise winding).
        /// </summary>
        public CustomMeshBuilder AddTriangle(int v0, int v1, int v2)
        {
            _triangles.Add(v0);
            _triangles.Add(v1);
            _triangles.Add(v2);
            return this;
        }

        /// <summary>
        /// Add a quad (two triangles) from 4 vertices.
        /// </summary>
        public CustomMeshBuilder AddQuad(int v0, int v1, int v2, int v3)
        {
            _triangles.Add(v0);
            _triangles.Add(v1);
            _triangles.Add(v2);
            
            _triangles.Add(v0);
            _triangles.Add(v2);
            _triangles.Add(v3);
            return this;
        }

        /// <summary>
        /// Add UV coordinates for each vertex (must match vertex count).
        /// </summary>
        public CustomMeshBuilder SetUVs(params Vector2[] uvs)
        {
            _uvs.Clear();
            _uvs.AddRange(uvs);
            return this;
        }

        /// <summary>
        /// Set a simple planar UV based on vertex position.
        /// </summary>
        public CustomMeshBuilder ApplyPlanarUVs()
        {
            _uvs.Clear();
            foreach (var v in _vertices)
            {
                _uvs.Add(new Vector2(v.x, v.z));
            }
            return this;
        }

        /// <summary>
        /// Apply UVs by projecting onto a plane.
        /// </summary>
        public CustomMeshBuilder ApplyPlanarUVs(Vector3 up, Vector3 offset)
        {
            _uvs.Clear();
            foreach (var v in _vertices)
            {
                Vector3 projected = Vector3.ProjectOnPlane(v - offset, up.normalized);
                _uvs.Add(new Vector2(projected.x, projected.z));
            }
            return this;
        }

        /// <summary>
        /// Set the material for this mesh.
        /// </summary>
        public CustomMeshBuilder SetMaterial(Material material)
        {
            _material = material;
            return this;
        }

        /// <summary>
        /// Set a solid color (creates material internally).
        /// </summary>
        public CustomMeshBuilder SetColor(Color color)
        {
            _material = MaterialPresets.Opaque(color);
            return this;
        }

        /// <summary>
        /// Disable automatic normal calculation (use when providing custom normals).
        /// </summary>
        public CustomMeshBuilder DontCalculateNormals()
        {
            _calculateNormals = false;
            return this;
        }

        /// <summary>
        /// Build and return the Mesh.
        /// </summary>
        public Mesh BuildMesh()
        {
            if (_vertices.Count == 0)
            {
                DebugLog.Error($"Cannot build mesh '{_name}': no vertices");
                return null!;
            }

            Mesh mesh = new Mesh
            {
                name = _name,
                vertices = _vertices.ToArray(),
                triangles = _triangles.ToArray()
            };

            if (_uvs.Count > 0)
            {
                if (_uvs.Count != _vertices.Count)
                {
                    DebugLog.Warning($"UV count ({_uvs.Count}) != vertex count ({_vertices.Count}), ignoring UVs");
                }
                else
                {
                    mesh.uv = _uvs.ToArray();
                }
            }

            if (_calculateNormals)
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();

            DebugLog.Info($"Built custom mesh: {_name} ({_vertices.Count} vertices, {_triangles.Count / 3} triangles)");
            return mesh;
        }

        /// <summary>
        /// Build a GameObject with the mesh and material applied.
        /// </summary>
        public GameObject Build()
        {
            Mesh mesh = BuildMesh();

            GameObject go = new GameObject(_name);
            go.AddComponent<MeshFilter>().mesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.material = _material ?? MaterialPresets.Opaque(Color.white);
            renderer.renderingLayerMask = uint.MaxValue;

            DebugLog.Info($"Created GameObject: {_name}");
            return go;
        }

        /// <summary>
        /// Build a GameObject with the mesh and attach to a parent.
        /// </summary>
        public GameObject Build(Transform parent)
        {
            GameObject go = Build();
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
