using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using S1MAPI.Utils;
using S1MAPI.Gltf.Data;
using S1MAPI.Gltf.Processing;

namespace S1MAPI.Gltf
{
    /// <summary>
    /// Fluent importer for GLB and GLTF files.
    /// Provides a chainable API for configuring and executing imports.
    /// </summary>
    /// <example>
    /// <code>
    /// GameObject model = new GltfImporter()
    ///     .SetName("MyModel")
    ///     .SetShader(myShader)
    ///     .SetScale(0.01f)
    ///     .ImportAnimations(true)
    ///     .Load(glbBytes);
    /// </code>
    /// </example>
    public sealed class GltfImporter
    {
        #region Constants

        private const uint GLB_MAGIC = 0x46546C67; // "glTF"
        private const uint CHUNK_TYPE_JSON = 0x4E4F534A; // "JSON"
        private const uint CHUNK_TYPE_BIN = 0x004E4942; // "BIN\0"

        #endregion

        #region Fields

        private readonly GltfImportOptions _options;
        private string? _basePath;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new GLTF importer with default options.
        /// </summary>
        public GltfImporter()
        {
            _options = new GltfImportOptions();
        }

        /// <summary>
        /// Creates a new GLTF importer with the specified options.
        /// </summary>
        /// <param name="options">Import options controlling the loading behavior. Create a <see cref="S1MAPI.Gltf.Data.GltfImportOptions"/> instance to configure how the model is imported.</param>
        public GltfImporter(GltfImportOptions options)
        {
            _options = options ?? new GltfImportOptions();
        }

        #endregion

        #region Fluent Configuration

        /// <summary>
        /// Sets the name for the root GameObject.
        /// </summary>
        /// <param name="name">Name for the imported model</param>
        /// <returns>This importer for method chaining</returns>
        public GltfImporter SetName(string name)
        {
            _options.RootName = name;
            return this;
        }

        /// <summary>
        /// Sets the shader to use for materials.
        /// </summary>
        /// <param name="shader">Shader for materials (null for Standard)</param>
        /// <returns>This importer for method chaining</returns>
        public GltfImporter SetShader(Shader shader)
        {
            _options.Shader = shader;
            return this;
        }

        /// <summary>
        /// Sets the scale factor for the model.
        /// </summary>
        /// <param name="scale">Scale multiplier</param>
        /// <returns>This importer for method chaining</returns>
        public GltfImporter SetScale(float scale)
        {
            _options.ScaleFactor = scale;
            return this;
        }

        /// <summary>
        /// Configures whether to import animations.
        /// </summary>
        /// <param name="import">True to import animations</param>
        /// <returns>This importer for method chaining</returns>
        public GltfImporter ImportAnimations(bool import)
        {
            _options.ImportAnimations = import;
            return this;
        }

        /// <summary>
        /// Configures whether to import skins for skeletal animation.
        /// </summary>
        /// <param name="import">True to import skins</param>
        /// <returns>This importer for method chaining</returns>
        public GltfImporter ImportSkins(bool import)
        {
            _options.ImportSkins = import;
            return this;
        }

        /// <summary>
        /// Configures whether to import blend shapes.
        /// </summary>
        /// <param name="import">True to import blend shapes</param>
        /// <returns>This importer for method chaining</returns>
        public GltfImporter ImportBlendShapes(bool import)
        {
            _options.ImportBlendShapes = import;
            return this;
        }

        /// <summary>
        /// Configures whether to import cameras.
        /// </summary>
        /// <param name="import">True to import cameras</param>
        /// <returns>This importer for method chaining</returns>
        public GltfImporter ImportCameras(bool import)
        {
            _options.ImportCameras = import;
            return this;
        }

        /// <summary>
        /// Configures whether meshes should be readable from CPU.
        /// </summary>
        /// <param name="readable">True to keep meshes readable</param>
        /// <returns>This importer for method chaining</returns>
        public GltfImporter ReadableMeshes(bool readable)
        {
            _options.ReadableMeshes = readable;
            return this;
        }

        /// <summary>
        /// Configures whether to generate normals for meshes lacking them.
        /// </summary>
        /// <param name="generate">True to generate missing normals</param>
        /// <returns>This importer for method chaining</returns>
        public GltfImporter GenerateNormals(bool generate)
        {
            _options.GenerateMissingNormals = generate;
            return this;
        }

        /// <summary>
        /// Configures whether to generate tangents for normal mapping.
        /// </summary>
        /// <param name="generate">True to generate missing tangents</param>
        /// <returns>This importer for method chaining</returns>
        public GltfImporter GenerateTangents(bool generate)
        {
            _options.GenerateMissingTangents = generate;
            return this;
        }

        /// <summary>
        /// Sets the emission intensity multiplier for emissive materials.
        /// Values greater than 1.0 create HDR emission for neon/glow effects.
        /// Also reads and applies the KHR_materials_emissive_strength extension if present.
        /// </summary>
        /// <param name="intensity">Emission multiplier (default is 1.0)</param>
        /// <returns>This importer for method chaining</returns>
        public GltfImporter SetEmissionIntensity(float intensity)
        {
            _options.EmissionIntensity = intensity;
            return this;
        }

        #endregion

        #region Load Methods

        /// <summary>
        /// Loads a GLB or GLTF model from a byte array.
        /// Automatically detects format based on magic number.
        /// </summary>
        /// <param name="data">GLB or JSON bytes</param>
        /// <returns>The root GameObject of the imported model, or null on failure</returns>
        public GameObject? Load(byte[] data)
        {
            if (data == null || data.Length < 4)
            {
                DebugLog.Error("Invalid GLTF data: null or too short");
                return null;
            }

            // Check for GLB magic number
            uint magic = BitConverter.ToUInt32(data, 0);
            if (magic == GLB_MAGIC)
            {
                return LoadGlb(data);
            }

            // Assume GLTF JSON
            string json = Encoding.UTF8.GetString(data);
            return LoadGltfJson(json, null);
        }

        /// <summary>
        /// Loads a GLB model from a byte array.
        /// </summary>
        /// <param name="glbBytes">Raw GLB file bytes</param>
        /// <returns>The root GameObject of the imported model, or null on failure</returns>
        public GameObject? LoadGlb(byte[] glbBytes)
        {
            if (glbBytes == null || glbBytes.Length < 12)
            {
                DebugLog.Error("Invalid GLB data: too short");
                return null;
            }

            GltfRoot? root;
            byte[]? binaryChunk;

            if (!ParseGlbChunks(glbBytes, out root, out binaryChunk))
            {
                return null;
            }

            if (root == null)
            {
                DebugLog.Error("Failed to parse GLB root");
                return null;
            }

            GltfBufferResolver resolver = new GltfBufferResolver(null, binaryChunk);
            return ImportModel(root, resolver);
        }

        /// <summary>
        /// Loads a GLTF model from a file path.
        /// Supports both .glb and .gltf files.
        /// </summary>
        /// <param name="filePath">Path to the GLTF/GLB file</param>
        /// <returns>The root GameObject of the imported model, or null on failure</returns>
        public GameObject? LoadFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                DebugLog.Error("Invalid file path: null or empty");
                return null;
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    DebugLog.Error($"File not found: {filePath}");
                    return null;
                }

                byte[] data = File.ReadAllBytes(filePath);
                _basePath = Path.GetDirectoryName(filePath);

                // Set name from filename if not already set
                if (_options.RootName == "GltfModel")
                {
                    _options.RootName = Path.GetFileNameWithoutExtension(filePath);
                }

                return Load(data);
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Failed to load file '{filePath}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads a GLTF model from JSON text.
        /// </summary>
        /// <param name="json">GLTF JSON string</param>
        /// <param name="basePath">Base path for resolving external resources</param>
        /// <returns>The root GameObject of the imported model, or null on failure</returns>
        public GameObject? LoadGltfJson(string json, string? basePath)
        {
            if (string.IsNullOrEmpty(json))
            {
                DebugLog.Error("Invalid GLTF JSON: null or empty");
                return null;
            }

            GltfRoot? root;
            try
            {
                root = JsonConvert.DeserializeObject<GltfRoot>(json);
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Failed to parse GLTF JSON: {ex.Message}");
                return null;
            }

            if (root == null)
            {
                DebugLog.Error("Deserialized GLTF root is null");
                return null;
            }

            string? effectiveBasePath = basePath ?? _basePath;
            GltfBufferResolver resolver = new GltfBufferResolver(effectiveBasePath);
            return ImportModel(root, resolver);
        }

        #endregion

        #region Private Methods

        private bool ParseGlbChunks(byte[] glbBytes, out GltfRoot? root, out byte[]? binaryChunk)
        {
            root = null;
            binaryChunk = null;

            using (MemoryStream ms = new MemoryStream(glbBytes))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                // Header
                uint magic = reader.ReadUInt32();
                if (magic != GLB_MAGIC)
                {
                    DebugLog.Error($"Invalid GLB magic: 0x{magic:X8}");
                    return false;
                }

                uint version = reader.ReadUInt32();
                if (version != 2)
                {
                    DebugLog.Warning($"GLB version is {version}, expected 2. Loading may fail.");
                }

                uint length = reader.ReadUInt32();
                if (length != glbBytes.Length)
                {
                    DebugLog.Warning($"GLB length mismatch: header says {length}, actual {glbBytes.Length}");
                }

                // Chunks
                while (ms.Position < ms.Length)
                {
                    if (ms.Position + 8 > ms.Length)
                    {
                        break;
                    }

                    uint chunkLength = reader.ReadUInt32();
                    uint chunkType = reader.ReadUInt32();

                    if (ms.Position + chunkLength > ms.Length)
                    {
                        DebugLog.Error("GLB chunk extends beyond file");
                        return false;
                    }

                    byte[] chunkData = reader.ReadBytes((int)chunkLength);

                    if (chunkType == CHUNK_TYPE_JSON)
                    {
                        string json = Encoding.UTF8.GetString(chunkData);
                        try
                        {
                            root = JsonConvert.DeserializeObject<GltfRoot>(json);
                        }
                        catch (Exception ex)
                        {
                            DebugLog.Error($"Failed to parse GLB JSON chunk: {ex.Message}");
                            return false;
                        }
                    }
                    else if (chunkType == CHUNK_TYPE_BIN)
                    {
                        binaryChunk = chunkData;
                    }
                    // Ignore unknown chunk types
                }

                if (root == null)
                {
                    DebugLog.Error("GLB file missing JSON chunk");
                    return false;
                }

                return true;
            }
        }

        private GameObject? ImportModel(GltfRoot root, GltfBufferResolver resolver)
        {
            // Resolve all buffers
            if (!resolver.ResolveBuffers(root))
            {
                DebugLog.Error("Failed to resolve GLTF buffers");
                return null;
            }

            // Create load context
            GltfLoadContext context = new GltfLoadContext(root, resolver, _options);

            // Process textures first (materials depend on them)
            List<Texture2D?> textures = ProcessTextures(context);

            // Process materials
            List<Material> materials = GltfMaterialProcessor.ProcessMaterials(context);

            // Process meshes
            List<GltfMeshResult> meshResults = ProcessMeshes(context);

            // Create root GameObject
            GameObject modelRoot = new GameObject(_options.RootName);

            // Apply scale if not 1.0
            if (Math.Abs(_options.ScaleFactor - 1.0f) > 0.0001f)
            {
                modelRoot.transform.localScale = Vector3.one * _options.ScaleFactor;
            }

            // Process nodes and build hierarchy
            ProcessNodes(context, modelRoot, meshResults, materials);

            // Process animations
            if (_options.ImportAnimations)
            {
                List<AnimationClip> animations = GltfAnimationProcessor.ProcessAnimations(context);

                if (animations.Count > 0)
                {
                    // Add Animation component and assign clips
                    Animation anim = modelRoot.AddComponent<Animation>();
                    foreach (AnimationClip clip in animations)
                    {
                        anim.AddClip(clip, clip.name);
                    }

                    if (animations.Count > 0)
                    {
                        anim.clip = animations[0];
                    }
                }
            }

            DebugLog.Info($"Imported GLTF model '{_options.RootName}': " +
                $"{meshResults.Count} meshes, {materials.Count} materials, " +
                $"{textures.Count} textures, {context.Animations.Count} animations");

            return modelRoot;
        }

        private List<Texture2D?> ProcessTextures(GltfLoadContext context)
        {
            GltfRoot gltf = context.Root;
            List<Texture2D?> textures = new List<Texture2D?>();

            if (gltf.textures == null)
            {
                return textures;
            }

            foreach (GltfTexture gltfTex in gltf.textures)
            {
                Texture2D? tex = null;

                if (gltfTex.source.HasValue && gltf.images != null && gltfTex.source.Value < gltf.images.Count)
                {
                    GltfImage image = gltf.images[gltfTex.source.Value];
                    tex = LoadImage(context, image);

                    if (tex != null)
                    {
                        tex.name = gltfTex.name ?? image.name ?? $"texture_{textures.Count}";

                        // Apply sampler settings
                        if (gltfTex.sampler.HasValue && gltf.samplers != null && gltfTex.sampler.Value < gltf.samplers.Count)
                        {
                            ApplySampler(tex, gltf.samplers[gltfTex.sampler.Value]);
                        }
                    }
                }

                textures.Add(tex);
                context.RegisterTexture(tex);
            }

            return textures;
        }

        private Texture2D? LoadImage(GltfLoadContext context, GltfImage image)
        {
            byte[]? imageData = null;

            if (image.bufferView.HasValue)
            {
                imageData = context.BufferResolver.GetBufferViewData(context.Root, image.bufferView.Value);
            }
            else if (!string.IsNullOrEmpty(image.uri))
            {
                // Check for data URI
                if (image.uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    int commaIndex = image.uri.IndexOf(',');
                    if (commaIndex > 0 && image.uri.Substring(0, commaIndex).Contains(";base64"))
                    {
                        try
                        {
                            imageData = Convert.FromBase64String(image.uri.Substring(commaIndex + 1));
                        }
                        catch
                        {
                            DebugLog.Warning($"Failed to decode base64 image data");
                        }
                    }
                }
                // External file handled by buffer resolver would require separate image loading
            }

            if (imageData == null || imageData.Length == 0)
            {
                return null;
            }

            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(imageData))
            {
                return tex;
            }

            UnityEngine.Object.Destroy(tex);
            return null;
        }

        private void ApplySampler(Texture2D tex, GltfSampler sampler)
        {
            if (sampler.wrapS.HasValue)
            {
                tex.wrapModeU = GetWrapMode(sampler.wrapS.Value);
            }

            if (sampler.wrapT.HasValue)
            {
                tex.wrapModeV = GetWrapMode(sampler.wrapT.Value);
            }

            if (sampler.minFilter.HasValue || sampler.magFilter.HasValue)
            {
                tex.filterMode = GetFilterMode(sampler.minFilter ?? 0, sampler.magFilter ?? 0);
            }
        }

        private static TextureWrapMode GetWrapMode(int mode) =>
            mode switch
            {
                GltfTextureWrap.ClampToEdge => TextureWrapMode.Clamp,
                GltfTextureWrap.MirroredRepeat => TextureWrapMode.Mirror,
                GltfTextureWrap.Repeat => TextureWrapMode.Repeat,
                _ => TextureWrapMode.Repeat
            };

        private static FilterMode GetFilterMode(int min, int mag)
        {
            if (min == GltfTextureFilter.Nearest || mag == GltfTextureFilter.Nearest)
            {
                return FilterMode.Point;
            }

            if (min >= GltfTextureFilter.NearestMipmapNearest)
            {
                return FilterMode.Trilinear;
            }

            return FilterMode.Bilinear;
        }

        private List<GltfMeshResult> ProcessMeshes(GltfLoadContext context)
        {
            // Use the existing mesh processor for now
            GltfRoot gltf = context.Root;
            byte[]? binaryBuffer = gltf.buffers?[0]?.Data;

            List<GltfMeshResult> results = GltfMeshProcessor.ProcessMeshes(gltf, binaryBuffer);

            // Register meshes
            foreach (GltfMeshResult result in results)
            {
                if (result?.mesh != null)
                {
                    context.RegisterMesh(result.mesh);
                }
            }

            return results;
        }

        private void ProcessNodes(GltfLoadContext context, GameObject root, List<GltfMeshResult> meshResults, List<Material> materials)
        {
            GltfRoot gltf = context.Root;

            if (gltf.nodes == null)
            {
                return;
            }

            // Create GameObjects for all nodes
            Dictionary<int, Transform> nodeMap = new Dictionary<int, Transform>();

            for (int i = 0; i < gltf.nodes.Count; i++)
            {
                GltfNode node = gltf.nodes[i];
                GameObject go = new GameObject(node.name ?? $"node_{i}");
                nodeMap[i] = go.transform;
                context.RegisterNodeTransform(i, go.transform);

                // Apply transforms
                ApplyNodeTransform(go.transform, node);

                // Attach mesh if present
                if (node.mesh.HasValue && node.mesh.Value < meshResults.Count)
                {
                    AttachMesh(go, meshResults[node.mesh.Value], materials, context);
                }

                // Attach camera if present and enabled
                if (_options.ImportCameras && node.camera.HasValue && gltf.cameras != null && node.camera.Value < gltf.cameras.Count)
                {
                    AttachCamera(go, gltf.cameras[node.camera.Value]);
                }
            }

            // Build hierarchy
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

            // Attach root nodes to model root
            foreach (Transform t in nodeMap.Values)
            {
                if (t.parent == null)
                {
                    t.SetParent(root.transform, false);
                }
            }
        }

        private void ApplyNodeTransform(Transform transform, GltfNode node)
        {
            if (node.matrix != null && node.matrix.Length == 16)
            {
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

                Vector3 pos = mat.GetColumn(3);
                Quaternion rot = mat.rotation;
                Vector3 scale = mat.lossyScale;

                // Convert GLTF to Unity coordinates
                transform.localPosition = new Vector3(-pos.x, pos.y, pos.z);
                transform.localRotation = new Quaternion(rot.x, -rot.y, -rot.z, rot.w);
                transform.localScale = scale;
            }
            else
            {
                if (node.translation != null && node.translation.Length >= 3)
                {
                    transform.localPosition = new Vector3(-node.translation[0], node.translation[1], node.translation[2]);
                }

                if (node.rotation != null && node.rotation.Length >= 4)
                {
                    transform.localRotation = new Quaternion(node.rotation[0], -node.rotation[1], -node.rotation[2], node.rotation[3]);
                }

                if (node.scale != null && node.scale.Length >= 3)
                {
                    transform.localScale = new Vector3(node.scale[0], node.scale[1], node.scale[2]);
                }
            }
        }

        private void AttachMesh(GameObject go, GltfMeshResult meshResult, List<Material> materials, GltfLoadContext context)
        {
            if (meshResult?.mesh == null)
            {
                return;
            }

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.mesh = meshResult.mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            Material[] meshMaterials = new Material[meshResult.mesh.subMeshCount];
            Material? defaultMat = null;

            for (int i = 0; i < meshResult.mesh.subMeshCount; i++)
            {
                int matIndex = i < meshResult.materialIndices.Length ? meshResult.materialIndices[i] : -1;

                if (matIndex >= 0 && matIndex < materials.Count)
                {
                    meshMaterials[i] = materials[matIndex];
                }
                else
                {
                    if (defaultMat == null)
                    {
                        defaultMat = GltfMaterialProcessor.CreateDefaultMaterial(context);
                    }
                    else
                    {
                        meshMaterials[i] = defaultMat;
                    }
                }
            }

            mr.materials = meshMaterials;
        }

        private void AttachCamera(GameObject go, GltfCamera gltfCamera)
        {
            Camera cam = go.AddComponent<Camera>();

            if (gltfCamera.type == "perspective" && gltfCamera.perspective != null)
            {
                cam.orthographic = false;
                cam.fieldOfView = gltfCamera.perspective.yfov * Mathf.Rad2Deg;
                cam.nearClipPlane = gltfCamera.perspective.znear;

                if (gltfCamera.perspective.zfar.HasValue)
                {
                    cam.farClipPlane = gltfCamera.perspective.zfar.Value;
                }

                if (gltfCamera.perspective.aspectRatio.HasValue)
                {
                    cam.aspect = gltfCamera.perspective.aspectRatio.Value;
                }
            }
            else if (gltfCamera.type == "orthographic" && gltfCamera.orthographic != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = gltfCamera.orthographic.ymag;
                cam.nearClipPlane = gltfCamera.orthographic.znear;
                cam.farClipPlane = gltfCamera.orthographic.zfar;
            }
        }

        #endregion
    }
}
