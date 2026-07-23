using UnityEngine;

namespace S1MAPI.Gltf.Processing
{
    internal static class GltfMaterialTextureConverter
    {
        public static Texture2D CreateLinearCopy(Texture2D source, string suffix)
        {
            Color32[] pixels = source.GetPixels32();
            Texture2D converted = CreateLinearTexture(source, suffix);
            converted.SetPixels32(pixels);
            converted.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return converted;
        }

        public static Texture2D CreateMetallicSmoothness(
            Texture2D source,
            float metallicFactor,
            float roughnessFactor)
        {
            Color32[] pixels = source.GetPixels32();
            float clampedMetallic = Mathf.Clamp01(metallicFactor);
            float clampedRoughness = Mathf.Clamp01(roughnessFactor);

            for (int index = 0; index < pixels.Length; index++)
            {
                Color32 sourcePixel = pixels[index];
                byte metallic = ToByte((sourcePixel.b / 255f) * clampedMetallic);
                byte smoothness = ToByte(1f - ((sourcePixel.g / 255f) * clampedRoughness));
                pixels[index] = new Color32(metallic, 0, 0, smoothness);
            }

            Texture2D converted = CreateLinearTexture(source, "MetallicSmoothness");
            converted.SetPixels32(pixels);
            converted.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return converted;
        }

        private static Texture2D CreateLinearTexture(Texture2D source, string suffix)
        {
            Texture2D texture = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                mipChain: true,
                linear: true)
            {
                name = $"{source.name}_{suffix}",
                filterMode = source.filterMode,
                anisoLevel = source.anisoLevel,
                wrapModeU = source.wrapModeU,
                wrapModeV = source.wrapModeV
            };

            return texture;
        }

        private static byte ToByte(float value) =>
            (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
    }
}
