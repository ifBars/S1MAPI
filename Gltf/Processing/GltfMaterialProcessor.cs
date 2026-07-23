using S1MAPI.Gltf.Data;
using UnityEngine;
using UnityEngine.Rendering;
using S1MAPI.Utils;

namespace S1MAPI.Gltf.Processing
{
    /// <summary>
    /// Processes GLTF materials and converts them to Unity materials.
    /// Uses URP (Universal Render Pipeline) shaders for Schedule 1 compatibility.
    /// </summary>
    internal static class GltfMaterialProcessor
    {
        #region Shader Property Names

        /// <summary>
        /// URP Lit shader property names.
        /// </summary>
        private static class UrpProperty
        {
            // Textures
            public const string BaseMap = "_BaseMap";
            public const string BumpMap = "_BumpMap";
            public const string MetallicGlossMap = "_MetallicGlossMap";
            public const string OcclusionMap = "_OcclusionMap";
            public const string EmissionMap = "_EmissionMap";

            // Colors and factors
            public const string BaseColor = "_BaseColor";
            public const string EmissionColor = "_EmissionColor";
            public const string Metallic = "_Metallic";
            public const string Smoothness = "_Smoothness";
            public const string BumpScale = "_BumpScale";
            public const string OcclusionStrength = "_OcclusionStrength";

            // Surface options
            public const string Surface = "_Surface";          // 0 = Opaque, 1 = Transparent
            public const string Blend = "_Blend";              // 0 = Alpha, 1 = Premultiply, 2 = Additive, 3 = Multiply
            public const string AlphaClip = "_AlphaClip";      // 0 = Off, 1 = On
            public const string Cutoff = "_Cutoff";
            public const string Cull = "_Cull";                // 0 = Off, 1 = Front, 2 = Back
            public const string SrcBlend = "_SrcBlend";
            public const string DstBlend = "_DstBlend";
            public const string ZWrite = "_ZWrite";
        }

        /// <summary>
        /// URP shader names.
        /// </summary>
        private static class UrpShaderName
        {
            public const string Lit = "Universal Render Pipeline/Lit";
            public const string SimpleLit = "Universal Render Pipeline/Simple Lit";
            public const string Unlit = "Universal Render Pipeline/Unlit";
        }

        #endregion

        #region Public API

        /// <summary>
        /// Process all GLTF materials and convert them to Unity URP materials.
        /// </summary>
        /// <param name="context">The GLTF load context</param>
        /// <returns>List of Unity materials indexed by GLTF material index</returns>
        public static List<Material> ProcessMaterials(GltfLoadContext context)
        {
            List<Material> materials = new List<Material>();
            GltfRoot gltf = context.Root;

            if (gltf.materials == null)
            {
                return materials;
            }

            Shader? shader = GetShader(context);
            if (shader == null)
            {
                DebugLog.Error("Failed to find URP Lit shader for materials");
                return materials;
            }

            for (int i = 0; i < gltf.materials.Count; i++)
            {
                GltfMaterial gltfMat = gltf.materials[i];
                Material unityMat = CreateMaterial(context, gltfMat, shader, i);
                materials.Add(unityMat);
                context.RegisterMaterial(unityMat);
            }

            return materials;
        }

        /// <summary>
        /// Creates a default material for primitives without a material index.
        /// </summary>
        /// <param name="context">The GLTF load context</param>
        /// <returns>A default gray URP material</returns>
        public static Material? CreateDefaultMaterial(GltfLoadContext context)
        {
            Shader? shader = GetShader(context);
            if (shader == null)
            {
                DebugLog.Error("Failed to find URP shader for default material");
                return null;
            }

            Material mat = new Material(shader);
            mat.name = "DefaultGltfMaterial";
            SetBaseColor(mat, new Color(0.8f, 0.8f, 0.8f, 1.0f));
            SetMetallicSmoothness(mat, 0f, 0.5f);
            context.RegisterMaterial(mat);
            return mat;
        }

        #endregion

        #region Private Methods

        private static Shader? GetShader(GltfLoadContext context)
        {
            // Use custom shader if provided
            if (context.Options.Shader != null)
            {
                return context.Options.Shader;
            }

            // Try URP Lit shader first (Schedule 1 uses URP)
            Shader shader = Shader.Find(UrpShaderName.Lit);
            if (shader != null)
            {
                return shader;
            }

            // Fallback to Simple Lit
            shader = Shader.Find(UrpShaderName.SimpleLit);
            if (shader != null)
            {
                DebugLog.Warning("URP Lit shader not found, using Simple Lit");
                return shader;
            }

            // Last resort - Standard (won't render properly in URP but better than nothing)
            shader = Shader.Find("Standard");
            if (shader != null)
            {
                DebugLog.Warning("URP shaders not found, falling back to Standard (may not render correctly)");
            }

            return shader;
        }

        private static Material CreateMaterial(GltfLoadContext context, GltfMaterial gltfMat, Shader shader, int index)
        {
            Material mat = new Material(shader);
            mat.name = gltfMat.name ?? $"material_{index}";

            // PBR Metallic-Roughness
            if (gltfMat.pbrMetallicRoughness != null)
            {
                ApplyPbrMetallicRoughness(context, mat, gltfMat.pbrMetallicRoughness);
            }
            else
            {
                // Default PBR values
                SetBaseColor(mat, Color.white);
                SetMetallicSmoothness(mat, 0f, 0.5f);
            }

            // Normal map
            if (gltfMat.normalTexture != null)
            {
                ApplyNormalTexture(context, mat, gltfMat.normalTexture);
            }

            // Occlusion
            if (gltfMat.occlusionTexture != null)
            {
                ApplyOcclusionTexture(context, mat, gltfMat.occlusionTexture);
            }

            // Emission
            if (gltfMat.emissiveTexture != null || gltfMat.emissiveFactor != null)
            {
                ApplyEmission(context, mat, gltfMat);
            }

            // Alpha mode
            ApplyAlphaMode(mat, gltfMat);

            // Double-sided
            if (gltfMat.doubleSided == true)
            {
                mat.SetFloat(UrpProperty.Cull, 0); // Off
            }

            return mat;
        }

        private static void ApplyPbrMetallicRoughness(GltfLoadContext context, Material mat, GltfPbrMetallicRoughness pbr)
        {
            // Base color factor
            Color baseColor = Color.white;
            if (pbr.baseColorFactor != null && pbr.baseColorFactor.Length >= 4)
            {
                baseColor = new Color(
                    pbr.baseColorFactor[0],
                    pbr.baseColorFactor[1],
                    pbr.baseColorFactor[2],
                    pbr.baseColorFactor[3]
                );
            }
            SetBaseColor(mat, baseColor);

            // Base color texture
            if (pbr.baseColorTexture != null)
            {
                Texture2D? tex = GetTexture(context, pbr.baseColorTexture.index);
                if (tex != null)
                {
                    mat.SetTexture(UrpProperty.BaseMap, tex);
                }
            }

            // Metallic and roughness
            float metallic = pbr.metallicFactor ?? 1.0f;
            float roughness = pbr.roughnessFactor ?? 1.0f;
            float smoothness = 1.0f - roughness; // URP uses smoothness (inverse of roughness)
            
            SetMetallicSmoothness(mat, metallic, smoothness);

            // Metallic-roughness texture
            if (pbr.metallicRoughnessTexture != null)
            {
                Texture2D? sourceTexture = GetTexture(context, pbr.metallicRoughnessTexture.index);
                if (sourceTexture != null)
                {
                    Texture2D metallicSmoothness = GltfMaterialTextureConverter.CreateMetallicSmoothness(
                        sourceTexture,
                        metallic,
                        roughness);
                    context.RegisterTexture(metallicSmoothness);
                    mat.SetTexture(UrpProperty.MetallicGlossMap, metallicSmoothness);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    SetMetallicSmoothness(mat, 1f, 1f);
                }
            }
        }

        private static void SetBaseColor(Material mat, Color color)
        {
            if (mat.HasProperty(UrpProperty.BaseColor))
            {
                mat.SetColor(UrpProperty.BaseColor, color);
            }
            else if (mat.HasProperty("_Color"))
            {
                // Fallback for Standard shader
                mat.SetColor("_Color", color);
            }
        }

        private static void SetMetallicSmoothness(Material mat, float metallic, float smoothness)
        {
            if (mat.HasProperty(UrpProperty.Metallic))
            {
                mat.SetFloat(UrpProperty.Metallic, metallic);
            }
            else if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", metallic);
            }

            if (mat.HasProperty(UrpProperty.Smoothness))
            {
                mat.SetFloat(UrpProperty.Smoothness, smoothness);
            }
            else if (mat.HasProperty("_Glossiness"))
            {
                // Standard shader uses _Glossiness
                mat.SetFloat("_Glossiness", smoothness);
            }
        }

        private static void ApplyNormalTexture(GltfLoadContext context, Material mat, GltfNormalTextureInfo normalInfo)
        {
            Texture2D? sourceTexture = GetTexture(context, normalInfo.index);
            if (sourceTexture != null)
            {
                Texture2D normalTexture = GltfMaterialTextureConverter.CreateLinearCopy(sourceTexture, "Normal");
                context.RegisterTexture(normalTexture);
                mat.SetTexture(UrpProperty.BumpMap, normalTexture);
                mat.EnableKeyword("_NORMALMAP");

                if (normalInfo.scale.HasValue)
                {
                    mat.SetFloat(UrpProperty.BumpScale, normalInfo.scale.Value);
                }
            }
        }

        private static void ApplyOcclusionTexture(GltfLoadContext context, Material mat, GltfOcclusionTextureInfo occlusionInfo)
        {
            Texture2D? sourceTexture = GetTexture(context, occlusionInfo.index);
            if (sourceTexture != null)
            {
                Texture2D occlusionTexture = GltfMaterialTextureConverter.CreateLinearCopy(sourceTexture, "Occlusion");
                context.RegisterTexture(occlusionTexture);
                mat.SetTexture(UrpProperty.OcclusionMap, occlusionTexture);
                mat.EnableKeyword("_OCCLUSIONMAP");

                if (occlusionInfo.strength.HasValue)
                {
                    mat.SetFloat(UrpProperty.OcclusionStrength, occlusionInfo.strength.Value);
                }
            }
        }

        private static void ApplyEmission(GltfLoadContext context, Material mat, GltfMaterial gltfMat)
        {
            Color emissiveColor = Color.black;
            float intensity = context.Options.EmissionIntensity;

            // Check for KHR_materials_emissive_strength extension
            if (gltfMat.extensions != null && gltfMat.extensions.ContainsKey("KHR_materials_emissive_strength"))
            {
                try
                {
                    var extensionData = gltfMat.extensions["KHR_materials_emissive_strength"];
                    if (extensionData is Dictionary<string, object> dict && dict.ContainsKey("emissiveStrength"))
                    {
                        if (dict["emissiveStrength"] is float strength)
                        {
                            intensity *= strength;
                        }
                        else if (dict["emissiveStrength"] is double strengthDouble)
                        {
                            intensity *= (float)strengthDouble;
                        }
                    }
                }
                catch
                {
                    // Ignore extension parsing errors
                }
            }

            if (gltfMat.emissiveFactor != null && gltfMat.emissiveFactor.Length >= 3)
            {
                emissiveColor = new Color(
                    gltfMat.emissiveFactor[0] * intensity,
                    gltfMat.emissiveFactor[1] * intensity,
                    gltfMat.emissiveFactor[2] * intensity,
                    1.0f
                );
            }

            if (gltfMat.emissiveTexture != null)
            {
                Texture2D? tex = GetTexture(context, gltfMat.emissiveTexture.index);
                if (tex != null)
                {
                    mat.SetTexture(UrpProperty.EmissionMap, tex);
                    mat.EnableKeyword("_EMISSION");
                    
                    // If no emissive factor, use white multiplied by intensity to show full texture
                    if (gltfMat.emissiveFactor == null)
                    {
                        emissiveColor = Color.white * intensity;
                    }
                }
            }

            if (emissiveColor != Color.black)
            {
                mat.SetColor(UrpProperty.EmissionColor, emissiveColor);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
        }

        private static void ApplyAlphaMode(Material mat, GltfMaterial gltfMat)
        {
            string alphaMode = gltfMat.alphaMode ?? GltfAlphaMode.Opaque;

            switch (alphaMode)
            {
                case GltfAlphaMode.Opaque:
                    SetUrpOpaqueMode(mat);
                    break;

                case GltfAlphaMode.Mask:
                    SetUrpCutoutMode(mat, gltfMat.alphaCutoff ?? 0.5f);
                    break;

                case GltfAlphaMode.Blend:
                    SetUrpTransparentMode(mat);
                    break;

                default:
                    SetUrpOpaqueMode(mat);
                    break;
            }
        }

        private static void SetUrpOpaqueMode(Material mat)
        {
            mat.SetFloat(UrpProperty.Surface, 0); // Opaque
            mat.SetFloat(UrpProperty.AlphaClip, 0); // No alpha clip
            mat.SetFloat(UrpProperty.SrcBlend, (float)BlendMode.One);
            mat.SetFloat(UrpProperty.DstBlend, (float)BlendMode.Zero);
            mat.SetFloat(UrpProperty.ZWrite, 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Geometry;
        }

        private static void SetUrpCutoutMode(Material mat, float cutoff)
        {
            mat.SetFloat(UrpProperty.Surface, 0); // Opaque (cutout is still opaque surface type)
            mat.SetFloat(UrpProperty.AlphaClip, 1); // Enable alpha clip
            mat.SetFloat(UrpProperty.Cutoff, cutoff);
            mat.SetFloat(UrpProperty.SrcBlend, (float)BlendMode.One);
            mat.SetFloat(UrpProperty.DstBlend, (float)BlendMode.Zero);
            mat.SetFloat(UrpProperty.ZWrite, 1);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
        }

        private static void SetUrpTransparentMode(Material mat)
        {
            mat.SetFloat(UrpProperty.Surface, 1); // Transparent
            mat.SetFloat(UrpProperty.Blend, 0); // Alpha blend
            mat.SetFloat(UrpProperty.AlphaClip, 0);
            mat.SetFloat(UrpProperty.SrcBlend, (float)BlendMode.SrcAlpha);
            mat.SetFloat(UrpProperty.DstBlend, (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat(UrpProperty.ZWrite, 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        private static Texture2D? GetTexture(GltfLoadContext context, int textureIndex)
        {
            IReadOnlyList<Texture2D?>? textures = context.Textures;

            if (textures == null || textureIndex < 0 || textureIndex >= textures.Count)
            {
                return null;
            }

            return textures[textureIndex];
        }

        #endregion
    }
}
