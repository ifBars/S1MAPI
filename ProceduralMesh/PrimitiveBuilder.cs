using UnityEngine;
using S1MAPI.Core;
using S1MAPI.Utils;
using Object = UnityEngine.Object;

namespace S1MAPI.ProceduralMesh
{
    /// <summary>
    /// Helper for creating and configuring Unity primitive GameObjects
    /// Based on patterns from PetShopSpawner
    /// </summary>
    public static class PrimitiveBuilder
    {
        #region Public API - Primitive Creation
        
        /// <summary>
        /// Create a Unity primitive GameObject with material and optional parent
        /// </summary>
        /// <param name="type">The type of primitive to create</param>
        /// <param name="name">Name for the GameObject</param>
        /// <param name="localPosition">Local position relative to parent</param>
        /// <param name="localScale">Local scale</param>
        /// <param name="color">Base color for the material</param>
        /// <param name="parent">Optional parent transform</param>
        /// <returns>The created GameObject</returns>
        public static GameObject CreatePrimitive(
            PrimitiveType type,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            Transform? parent = null)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;

            if (parent != null)
            {
                primitive.transform.SetParent(parent);
            }

            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;

            // Setup material
            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
            {
                SetupMaterial(renderer, name, color);
            }

            return primitive;
        }

        /// <summary>
        /// Create a primitive with scaled dimensions
        /// </summary>
        /// <param name="type">The type of primitive to create</param>
        /// <param name="name">Name for the GameObject</param>
        /// <param name="localPosition">Local position relative to parent</param>
        /// <param name="localScale">Local scale</param>
        /// <param name="color">Base color for the material</param>
        /// <param name="scale">Global scale multiplier</param>
        /// <param name="parent">Optional parent transform</param>
        /// <returns>The created GameObject</returns>
        public static GameObject CreatePrimitive(
            PrimitiveType type,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            float scale,
            Transform? parent = null)
        {
            return CreatePrimitive(
                type,
                name,
                localPosition * scale,
                localScale * scale,
                color,
                parent
            );
        }
        
        /// <summary>
        /// Create a prefab instance from a loaded resource or existing object
        /// </summary>
        /// <param name="prefabName">Name of the prefab to find/load</param>
        /// <param name="name">Name for the instance</param>
        /// <param name="localPosition">Local position relative to parent</param>
        /// <param name="localRotation">Local rotation</param>
        /// <param name="parent">Optional parent transform</param>
        /// <returns>The created GameObject instance or null if not found</returns>
        public static GameObject CreatePrefab(
            string prefabName,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Transform? parent = null)
        {
            // 1. Try Resources.Load
            GameObject prefab = Resources.Load<GameObject>(prefabName);

            // 2. Try finding existing object in memory (e.g. from loaded asset bundles)
            if (prefab == null)
            {
                // Note: This finds ACTIVE instances, not the prefab asset itself unless it's loaded in memory
                // but for "Bong_Trash", if it's used in the game, it might be available.
                // We'll search for any GameObject with that name.
                GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (GameObject obj in allObjects)
                {
                    // Check for exact name match (ignoring (Clone) or path)
                    if (obj.name.Equals(prefabName, System.StringComparison.OrdinalIgnoreCase) ||
                        obj.name.Equals(prefabName + ".prefab", System.StringComparison.OrdinalIgnoreCase))
                    {
                        prefab = obj;
                        break;
                    }
                }
            }

            if (prefab == null)
            {
                DebugLog.Warning($"Could not find prefab: {prefabName}");
                return null!;
            }

            GameObject instance = Object.Instantiate(prefab);
            instance.name = name;

            if (parent != null)
            {
                instance.transform.SetParent(parent);
            }

            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            
            return instance;
        }

        #endregion

        #region Public API - Material Setup
        
        /// <summary>
        /// Setup material for a renderer based on the object name and color
        /// Automatically determines if the material should be transparent (glass/window)
        /// </summary>
        /// <param name="renderer">The renderer to configure</param>
        /// <param name="name">Name of the object (used to determine material type)</param>
        /// <param name="color">Base color</param>
        public static void SetupMaterial(Renderer renderer, string name, Color color)
        {
            if (renderer == null)
            {
                DebugLog.Warning("Cannot setup material on null renderer");
                return;
            }

            string lowerName = name.ToLower();
            Material material;

            // Determine if this should be transparent
            if (lowerName.Contains("glass") || lowerName.Contains("window"))
            {
                // Use the new high-quality clear glass for windows
                if (lowerName.Contains("clear") || lowerName.Contains("window"))
                {
                    material = MaterialPresets.ClearGlass();
                }
                else
                {
                    // Fallback for colored glass
                    material = MaterialPresets.Glass(color);
                }
            }
            else
            {
                material = MaterialPresets.Opaque(color);
            }

            renderer.material = material;
            renderer.allowOcclusionWhenDynamic = false;
        }
        
        #endregion

        #region Public API - Box Helper
        
        /// <summary>
        /// Create a box primitive (convenience method)
        /// </summary>
        public static GameObject CreateBox(
            string name,
            Vector3 localPosition,
            Vector3 size,
            Color color,
            Transform? parent = null)
        {
            return CreatePrimitive(
                PrimitiveType.Cube,
                name,
                localPosition,
                size,
                color,
                parent
            );
        }

        /// <summary>
        /// Create a sphere primitive (convenience method)
        /// </summary>
        public static GameObject CreateSphere(
            string name,
            Vector3 localPosition,
            float radius,
            Color color,
            Transform? parent = null)
        {
            return CreatePrimitive(
                PrimitiveType.Sphere,
                name,
                localPosition,
                Vector3.one * radius * 2f,
                color,
                parent
            );
        }

        /// <summary>
        /// Create a cylinder primitive (convenience method)
        /// </summary>
        public static GameObject CreateCylinder(
            string name,
            Vector3 localPosition,
            Vector3 size,
            Color color,
            Transform? parent = null)
        {
            return CreatePrimitive(
                PrimitiveType.Cylinder,
                name,
                localPosition,
                size,
                color,
                parent
            );
        }

        #endregion

        #region Public API - Lighting

        /// <summary>
        /// Create a point light with URP-correct defaults matching Schedule I's pipeline.
        /// </summary>
        /// <param name="name">GameObject name</param>
        /// <param name="localPosition">Local position relative to parent</param>
        /// <param name="color">Light color</param>
        /// <param name="range">Light range in meters</param>
        /// <param name="intensity">Light intensity</param>
        /// <param name="parent">Optional parent transform</param>
        /// <param name="shadows">Shadow mode (default: None to match game's URP settings)</param>
        /// <param name="renderMode">Render mode (default: ForcePixel for URP per-pixel lighting)</param>
        public static GameObject CreatePointLight(
            string name,
            Vector3 localPosition,
            Color color,
            float range,
            float intensity,
            Transform? parent = null,
            LightShadows shadows = LightShadows.None,
            LightRenderMode renderMode = LightRenderMode.ForcePixel)
        {
            GameObject lightObj = new GameObject(name);
            if (parent != null)
            {
                lightObj.transform.SetParent(parent);
            }
            lightObj.transform.localPosition = localPosition;

            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = shadows;
            light.renderMode = renderMode;

            return lightObj;
        }

        #endregion
    }
}
