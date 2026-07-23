using S1MAPI.Gltf.Data;
using UnityEngine;

namespace S1MAPI.Gltf.Processing
{
    /// <summary>
    /// Processes GLTF nodes and builds the Unity GameObject hierarchy.
    /// Handles coordinate system conversion and mesh attachment.
    /// </summary>
    internal static class GltfNodeProcessor
    {
        /// <summary>
        /// Process GLTF nodes and create Unity GameObjects with proper hierarchy.
        /// </summary>
        /// <param name="gltf">The parsed GLTF root object</param>
        /// <param name="root">The root GameObject to attach nodes to</param>
        /// <param name="meshes">List of processed meshes to attach</param>
        /// <param name="textures">List of processed textures</param>
        /// <param name="shader">Shader to use for materials</param>
        /// <returns>List of created transforms</returns>
        public static List<Transform> ProcessNodes(GltfRoot gltf, GameObject root, List<GltfMeshResult> meshes, List<Texture2D> textures, Shader shader)
        {
            List<Transform> nodes = new List<Transform>();
            Dictionary<int, Transform> nodeMap = new Dictionary<int, Transform>();

            if (gltf.nodes == null)
            {
                return nodes;
            }

            // Create GameObjects
            for (int i = 0; i < gltf.nodes.Count; i++)
            {
                GltfNode node = gltf.nodes[i];
                GameObject go = new GameObject(node.name ?? $"node_{i}");
                nodeMap[i] = go.transform;
                nodes.Add(go.transform);

                // Apply Transforms
                if (node.matrix != null && node.matrix.Length == 16)
                {
                    // Decompose matrix approach
                    Matrix4x4 mat = new Matrix4x4();
                    for (int c = 0; c < 4; c++)
                    {
                        int offset = c * 4;
                        mat.SetColumn(c, new Vector4(
                            node.matrix[offset],
                            node.matrix[offset + 1],
                            node.matrix[offset + 2],
                            node.matrix[offset + 3]));
                    }
                    
                    // Extract TRS
                    Vector3 pos = mat.GetColumn(3);
                    Quaternion rot = mat.rotation;
                    Vector3 scale = mat.lossyScale;

                    // Convert Coordinate System: GLTF (Right-handed, Y-up) -> Unity (Left-handed, Y-up)
                    // Position: Invert X
                    go.transform.localPosition = new Vector3(-pos.x, pos.y, pos.z);
                    
                    // Rotation: Invert Y and Z (or X and W? Standard is -Y, -Z for Unity quaternion from GLTF)
                    // (x, y, z, w) -> (x, -y, -z, w)
                    go.transform.localRotation = new Quaternion(rot.x, -rot.y, -rot.z, rot.w);
                    
                    // Scale: No change usually
                    go.transform.localScale = scale;
                }
                else
                {
                    // TRS
                    if (node.translation != null)
                    {
                        go.transform.localPosition = new Vector3(-node.translation[0], node.translation[1], node.translation[2]);
                    }
                    
                    if (node.rotation != null)
                    {
                        go.transform.localRotation = new Quaternion(node.rotation[0], -node.rotation[1], -node.rotation[2], node.rotation[3]);
                    }
                    
                    if (node.scale != null)
                    {
                        go.transform.localScale = new Vector3(node.scale[0], node.scale[1], node.scale[2]);
                    }
                }

                // Mesh
                if (node.mesh.HasValue && node.mesh.Value < meshes.Count)
                {
                    GltfMeshResult result = meshes[node.mesh.Value];
                    MeshFilter mf = go.AddComponent<MeshFilter>();
                    mf.mesh = result.mesh;
                    MeshRenderer mr = go.AddComponent<MeshRenderer>();
                    
                    // Material assignment
                    Material[] materials = new Material[result.mesh.subMeshCount];
                    Shader matShader = shader ?? Shader.Find("Standard");

                    for (int j = 0; j < result.mesh.subMeshCount; j++)
                    {
                        Material mat = new Material(matShader);
                        int matIndex = (j < result.materialIndices.Length) ? result.materialIndices[j] : -1;

                        if (matIndex >= 0 && gltf.materials != null && matIndex < gltf.materials.Count)
                        {
                            GltfMaterial gltfMat = gltf.materials[matIndex];
                            mat.name = gltfMat.name ?? $"material_{matIndex}";

                            if (gltfMat.pbrMetallicRoughness != null)
                            {
                                var pbr = gltfMat.pbrMetallicRoughness;

                                // Base Color
                                if (pbr.baseColorFactor != null && pbr.baseColorFactor.Length == 4)
                                {
                                    mat.color = new Color(pbr.baseColorFactor[0], pbr.baseColorFactor[1], pbr.baseColorFactor[2], pbr.baseColorFactor[3]);
                                }

                                // Base Texture
                                if (pbr.baseColorTexture != null && textures != null)
                                {
                                    int texIndex = pbr.baseColorTexture.index;
                                    if (texIndex >= 0 && texIndex < textures.Count)
                                    {
                                        mat.mainTexture = textures[texIndex];
                                    }
                                }

                                // Metallic/Roughness (Standard shader specific)
                                if (matShader.name == "Standard")
                                {
                                    if (pbr.metallicFactor.HasValue) mat.SetFloat("_Metallic", pbr.metallicFactor.Value);
                                    if (pbr.roughnessFactor.HasValue) mat.SetFloat("_Glossiness", 1.0f - pbr.roughnessFactor.Value); // Roughness is inverse of smoothness
                                }
                            }
                        }
                        else
                        {
                            mat.name = "DefaultMaterial";
                        }
                        
                        materials[j] = mat;
                    }

                    mr.materials = materials;
                }
            }

            // Hierarchy
            for (int i = 0; i < gltf.nodes.Count; i++)
            {
                GltfNode node = gltf.nodes[i];
                Transform parent = nodeMap[i];

                if (node.children != null)
                {
                    foreach (int childIndex in node.children)
                    {
                        if (nodeMap.TryGetValue(childIndex, out Transform? child) && child != null)
                        {
                            child.SetParent(parent, false);
                        }
                    }
                }
            }

            // Find roots (nodes without parents) and attach to root
            foreach (Transform t in nodes)
            {
                if (t.parent == null)
                {
                    t.SetParent(root.transform, false);
                }
            }

            return nodes;
        }
    }
}
