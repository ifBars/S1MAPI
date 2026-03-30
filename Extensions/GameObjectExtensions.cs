using S1MAPI.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace S1MAPI.Extensions
{
    /// <summary>
    /// Extension methods for GameObject operations.
    /// </summary>
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Get existing component or add one if missing. Returns the component.
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            return gameObject.TryGetComponent<T>(out T? component) ? component : gameObject.AddComponent<T>();
        }

        /// <summary>
        /// Check if GameObject has a component of type T.
        /// </summary>
        public static bool HasComponent<T>(this GameObject gameObject) where T : Component
        {
            return gameObject.TryGetComponent<T>(out _);
        }

        /// <summary>
        /// Safe destroy that handles null checks.
        /// </summary>
        public static void Destroy(this GameObject gameObject)
        {
            if (gameObject != null)
            {
                Object.Destroy(gameObject);
            }
        }

        /// <summary>
        /// Hide the GameObject by setting active to false.
        /// </summary>
        public static GameObject Hide(this GameObject gameObject)
        {
            gameObject.SetActive(false);
            return gameObject;
        }

        /// <summary>
        /// Show the GameObject by setting active to true.
        /// </summary>
        public static GameObject Show(this GameObject gameObject)
        {
            gameObject.SetActive(true);
            return gameObject;
        }

        /// <summary>
        /// Set the layer for this GameObject and all its children.
        /// </summary>
        public static GameObject SetLayerRecursively(this GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                SetLayerRecursively(gameObject.transform.GetChild(i).gameObject, layer);
            }
            return gameObject;
        }

        /// <summary>
        /// Set the layer by name for this GameObject and all its children.
        /// </summary>
        public static GameObject SetLayerRecursively(this GameObject gameObject, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer == -1)
            {
                DebugLog.Warning($"Layer not found: {layerName}");
                return gameObject;
            }
            return gameObject.SetLayerRecursively(layer);
        }

        #region Component Enabling

        /// <summary>
        /// Enable all MonoBehaviour components on this GameObject and its children.
        /// </summary>
        /// <param name="gameObject">The GameObject to enable components on</param>
        /// <param name="recursive">Whether to enable components on children (default: true)</param>
        public static void EnableAllComponents(this GameObject gameObject, bool recursive = true)
        {
            if (gameObject == null)
            {
                DebugLog.Warning("[S1MAPI] Cannot enable components on null GameObject");
                return;
            }

            var components = recursive
                ? gameObject.GetComponentsInChildren<MonoBehaviour>(includeInactive: true)
                : gameObject.GetComponents<MonoBehaviour>();

            foreach (var component in components)
            {
                component?.Enable();
            }
        }

        /// <summary>
        /// Enable specific component types by name on this GameObject and its children.
        /// </summary>
        /// <param name="gameObject">The GameObject to enable components on</param>
        /// <param name="componentNames">Array of component type names to enable</param>
        /// <param name="recursive">Whether to search children (default: true)</param>
        public static void EnableComponentsByName(this GameObject gameObject, string[] componentNames, bool recursive = true)
        {
            if (gameObject == null)
            {
                DebugLog.Warning("[S1MAPI] Cannot enable components on null GameObject");
                return;
            }

            if (componentNames == null || componentNames.Length == 0)
            {
                DebugLog.Warning("[S1MAPI] No component names provided");
                return;
            }

            var components = recursive
                ? gameObject.GetComponentsInChildren<MonoBehaviour>(includeInactive: true)
                : gameObject.GetComponents<MonoBehaviour>();

            foreach (var component in components)
            {
                if (component == null) continue;

                string componentTypeName = component.GetType().Name;
                foreach (var name in componentNames)
                {
                    if (componentTypeName == name)
                    {
                        component.Enable();
                        DebugLog.Info($"[S1MAPI] Enabled component: {componentTypeName}");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Get a component by name without requiring a type reference.
        /// </summary>
        /// <param name="gameObject">The GameObject to search</param>
        /// <param name="componentName">Name of the component type</param>
        /// <param name="recursive">Whether to search children (default: true)</param>
        /// <returns>The component if found, null otherwise</returns>
        public static MonoBehaviour? GetComponentByName(this GameObject gameObject, string componentName, bool recursive = true)
        {
            if (gameObject == null) return null;

            var components = recursive
                ? gameObject.GetComponentsInChildren<MonoBehaviour>(includeInactive: true)
                : gameObject.GetComponents<MonoBehaviour>();

            foreach (var component in components)
            {
                if (component != null && component.GetType().Name == componentName)
                {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Enable a component by name and optionally invoke a setup method on it.
        /// </summary>
        /// <param name="gameObject">The GameObject containing the component</param>
        /// <param name="componentName">Name of the component type</param>
        /// <param name="setupMethodName">Optional method name to invoke after enabling</param>
        /// <param name="recursive">Whether to search children (default: true)</param>
        /// <returns>True if component was found and enabled</returns>
        public static bool EnableAndSetup(this GameObject gameObject, string componentName, string? setupMethodName = null, bool recursive = true)
        {
            var component = gameObject.GetComponentByName(componentName, recursive);
            if (component == null)
            {
                DebugLog.Warning($"[S1MAPI] Component '{componentName}' not found on {gameObject.name}");
                return false;
            }

            component.Enable();

            if (!string.IsNullOrEmpty(setupMethodName))
            {
                var method = component.GetType().GetMethod(setupMethodName,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (method != null)
                {
                    try
                    {
                        method.Invoke(component, null);
                        DebugLog.Info($"[S1MAPI] Invoked {setupMethodName} on {componentName}");
                    }
                    catch (System.Exception e)
                    {
                        DebugLog.Error($"[S1MAPI] Failed to invoke {setupMethodName}: {e.Message}");
                    }
                }
                else
                {
                    DebugLog.Warning($"[S1MAPI] Method '{setupMethodName}' not found on {componentName}");
                }
            }

            return true;
        }

        #endregion
    }
}
