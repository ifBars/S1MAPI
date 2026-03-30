using System;
using System.Reflection;
using UnityEngine;
using S1MAPI.Utils;

#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Managing;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Object;
#else
using FishNet;
using FishNet.Managing.Object;
using FishNet.Object;
#endif

namespace S1MAPI.Core
{
    /// <summary>
    /// Reference to a prefab in the game.
    /// These are full GameObjects with components (e.g. NetworkObject, interaction scripts, etc.).
    /// For static mesh-only assets (decoration), use MeshRef instead.
    /// Use S1MAPI.S1.Prefabs for known Schedule 1 prefab assets.
    /// </summary>
    public sealed class PrefabRef
    {
        #region Properties

        /// <summary>The prefab name as registered in FishNet</summary>
        public string Name { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Create a prefab reference.
        /// </summary>
        /// <param name="name">Exact prefab name in FishNet registry</param>
        public PrefabRef(string name)
        {
            Name = name;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Find the prefab GameObject from FishNet's spawnable prefabs.
        /// </summary>
        /// <returns>The prefab GameObject or null if not found</returns>
        public GameObject? Find()
        {
            try
            {
                var networkManager = InstanceFinder.NetworkManager;
                if (networkManager == null) return null;

                var spawnablePrefabs = networkManager.GetPrefabObjects<PrefabObjects>(0, false);
                if (spawnablePrefabs == null) return null;

                int count = spawnablePrefabs.GetObjectCount();
                for (int i = 0; i < count; i++)
                {
                    NetworkObject obj = spawnablePrefabs.GetObject(true, i);
                    if (obj != null && obj.gameObject != null && obj.gameObject.name == Name)
                    {
                        return obj.gameObject;
                    }
                }
            }
            catch (System.Exception e)
            {
                DebugLog.Error($"[PrefabRef] Error finding prefab '{Name}': {e.Message}");
            }
            return null;
        }

        /// <summary>
        /// Instantiate this prefab locally without network spawning.
        /// </summary>
        /// <returns>The instantiated GameObject or null if prefab not found</returns>
        /// <remarks>
        /// <para><strong>WARNING:</strong> Only use this method for prefabs that do NOT have a NetworkObject component.</para>
        /// <para>
        /// For prefabs with NetworkObject components (networked prefabs), you MUST use <see cref="InstantiateNetworked()"/> instead.
        /// Using this method on networked prefabs will cause FishNet to crash and break multiplayer functionality.
        /// </para>
        /// <para>
        /// Use this method only for:
        /// <list type="bullet">
        /// <item><description>Static decorative objects without network synchronization</description></item>
        /// <item><description>Client-side visual effects</description></item>
        /// <item><description>Local UI elements</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        public GameObject? Instantiate()
        {
            var prefab = Find();
            if (prefab == null)
            {
                DebugLog.Warning($"[PrefabRef] Could not find prefab: {Name}");
                return null;
            }
            return UnityEngine.Object.Instantiate(prefab);
        }

        /// <summary>
        /// Instantiate and spawn a networked prefab on the network (server only).
        /// </summary>
        /// <returns>The instantiated and network-spawned GameObject, or null if prefab not found</returns>
        /// <remarks>
        /// <para><strong>CRITICAL:</strong> You MUST use this method for any prefab that has a NetworkObject component.</para>
        /// <para>
        /// Using <see cref="Instantiate"/> on networked prefabs will cause FishNet to crash and completely break 
        /// multiplayer functionality. This is not recoverable without restarting the game.
        /// </para>
        /// <para><strong>What this method does:</strong></para>
        /// <list type="number">
        /// <item><description>Instantiates the prefab as inactive to prevent Awake() from running with uninitialized network state</description></item>
        /// <item><description>Initializes any GUID fields to prevent parse errors (fixes issues with prefabs like ATM)</description></item>
        /// <item><description>Spawns the object on the FishNet network, assigning it a network ID</description></item>
        /// <item><description>Activates the object, allowing Awake() to run with valid network state</description></item>
        /// </list>
        /// <para><strong>Requirements:</strong></para>
        /// <list type="bullet">
        /// <item><description>The prefab must be registered in FishNet's spawnable prefabs list</description></item>
        /// <item><description>The prefab must have a NetworkObject component</description></item>
        /// </list>
        /// <para><strong>Examples of prefabs that require this method:</strong></para>
        /// <list type="bullet">
        /// <item><description>S1.Prefabs.ATM</description></item>
        /// <item><description>S1.Prefabs.Door (any networked doors)</description></item>
        /// <item><description>S1.Prefabs.Storage (any networked storage containers)</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown if called on client when not server</exception>
        public GameObject? InstantiateNetworked()
        {
            return InstantiateNetworkedCore(null, Vector3.zero, Quaternion.identity, activate: true);
        }

        /// <summary>
        /// Instantiate and spawn a networked prefab, positioned before network spawn.
        /// Sets the transform BEFORE calling FishNet Spawn() so that clients receive
        /// the correct world position via replication (critical for prefabs without NetworkTransform).
        /// </summary>
        /// <param name="parent">Parent transform (set before spawn)</param>
        /// <param name="localPosition">Local position relative to parent</param>
        /// <param name="localRotation">Local rotation relative to parent</param>
        /// <returns>The instantiated and network-spawned GameObject, or null if not server or prefab not found</returns>
        public GameObject? InstantiateNetworked(Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            return InstantiateNetworkedCore(parent, localPosition, localRotation, activate: true);
        }

        /// <summary>
        /// Instantiate and spawn a networked prefab without activating it.
        /// The caller is responsible for calling <c>SetActive(true)</c> after configuration.
        /// Used by <see cref="Building.Components.PrefabPlacer"/> to invoke onReady callbacks
        /// before Awake/OnEnable fire (prevents sensors from triggering with default config).
        /// </summary>
        internal GameObject? InstantiateNetworkedInactive(Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            return InstantiateNetworkedCore(parent, localPosition, localRotation, activate: false);
        }

        private GameObject? InstantiateNetworkedCore(Transform? parent, Vector3 localPosition, Quaternion localRotation, bool activate)
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm == null || !nm.IsServer)
            {
                DebugLog.Warning($"[PrefabRef] InstantiateNetworked called but not server — skipping '{Name}'.");
                return null;
            }

            var prefab = Find();
            if (prefab == null)
            {
                DebugLog.Warning($"[PrefabRef] Could not find prefab: {Name}");
                return null;
            }

            bool originalState = prefab.activeSelf;
            prefab.SetActive(false);
            GameObject? instance = UnityEngine.Object.Instantiate(prefab);
            prefab.SetActive(originalState);

            if (instance == null) return null;

            InitializeGuidFields(instance);

            // Set world position before Spawn() so FishNet broadcasts the correct transform.
            // Parent AFTER Spawn to avoid issues with non-networked parent hierarchies.
            if (parent != null)
            {
                instance.transform.position = parent.TransformPoint(localPosition);
                instance.transform.rotation = parent.rotation * localRotation;
            }

            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                nm.ServerManager.Spawn(netObj);
            }

            // Parent after spawn (local operation, not replicated)
            if (parent != null)
            {
                instance.transform.SetParent(parent);
                instance.transform.localPosition = localPosition;
                instance.transform.localRotation = localRotation;
            }

            if (activate)
                instance.SetActive(true);

            return instance;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initialize GUID/guid string fields on all components to prevent parse errors in Awake().
        /// Uses reflection to find and populate empty GUID fields without requiring ScheduleOne references.
        /// </summary>
        private static void InitializeGuidFields(GameObject instance)
        {
            var components = instance.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            foreach (var component in components)
            {
                if (component == null) continue;

                var type = component.GetType();
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    try
                    {
                        // Handle string fields named "guid" or "GUID" that are empty or contain empty GUID
                        if (field.FieldType == typeof(string) && 
                            (field.Name.Equals("guid", StringComparison.OrdinalIgnoreCase) ||
                             field.Name.Contains("guid", StringComparison.OrdinalIgnoreCase)))
                        {
                            var value = field.GetValue(component) as string;
                            if (string.IsNullOrEmpty(value) || value == "00000000-0000-0000-0000-000000000000")
                            {
                                field.SetValue(component, Guid.NewGuid().ToString());
                                DebugLog.Info($"[PrefabRef] Initialized GUID field '{field.Name}' on {type.Name}");
                            }
                        }
                        // Handle System.Guid fields
                        else if (field.FieldType == typeof(Guid))
                        {
                            if (field.GetValue(component) is Guid value)
                            {
                                if (value == Guid.Empty)
                                {
                                    field.SetValue(component, Guid.NewGuid());
                                    DebugLog.Info($"[PrefabRef] Initialized Guid field '{field.Name}' on {type.Name}");
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        DebugLog.Warning($"[PrefabRef] Failed to initialize field '{field.Name}' on {type.Name}: {e.Message}");
                    }
                }
            }
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns the prefab name as a string.
        /// </summary>
        public override string ToString() => Name;

        /// <summary>
        /// Implicitly converts PrefabRef to its underlying prefab name string.
        /// </summary>
        /// <param name="prefab">The prefab reference to convert.</param>
        public static implicit operator string(PrefabRef prefab) => prefab.Name;

        #endregion
    }
}
