using System;
using UnityEngine;
using S1MAPI.Core;
using S1MAPI.Extensions;
using S1MAPI.S1;
using S1MAPI.Utils;

#if IL2CPP
using Il2CppFishNet;
using Il2CppTMPro;
#else
using FishNet;
using TMPro;
#endif

namespace S1MAPI.Building.Components
{
    /// <summary>
    /// Places network-spawnable prefabs from the game.
    /// Handles FishNet network spawning for multiplayer compatibility.
    /// Extracted from SemanticBuildingBuilder for SRP compliance.
    /// </summary>
    public sealed class PrefabPlacer
    {
        #region Fields

        private readonly Transform _parent;

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new prefab placer.
        /// </summary>
        /// <param name="parent">Parent transform for placed prefabs</param>
        public PrefabPlacer(Transform parent)
        {
            _parent = parent;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Place a prefab at the specified position.
        /// When <paramref name="networked"/> is true, the prefab is spawned via FishNet
        /// on the server and automatically replicated to clients. On clients, returns null
        /// but queues a deferred link so the replicated object is parented to the building
        /// once it arrives via FishNet.
        /// </summary>
        /// <param name="prefab">Prefab reference from GamePrefabs</param>
        /// <param name="localPosition">Local position relative to parent</param>
        /// <param name="localRotation">Local rotation</param>
        /// <param name="networked">Whether to spawn on network (server only). When true,
        /// returns null on clients — the server-spawned object is replicated via FishNet
        /// and automatically parented to the building hierarchy.</param>
        /// <param name="enableComponents">Whether to enable MonoBehaviour components (default: false)</param>
        /// <param name="onReady">Optional callback invoked on the GameObject after instantiation (server)
        /// or after the FishNet-replicated object is linked (client). Use this to configure
        /// components before sensors/triggers activate (e.g., set DoorController.AutoOpenForPlayer).</param>
        /// <returns>The instantiated prefab, or null if not found or if networked and not server</returns>
        public GameObject? Place(PrefabRef prefab, Vector3 localPosition, Quaternion localRotation, bool networked = true, bool enableComponents = false, Action<GameObject>? onReady = null)
        {
            Action<GameObject>? combined = null;
            if (enableComponents && onReady != null)
            {
                combined = (go) => { go.EnableAllComponents(recursive: true); onReady(go); };
            }
            else if (enableComponents)
            {
                combined = (go) => go.EnableAllComponents(recursive: true);
            }
            else
            {
                combined = onReady;
            }
            return PlaceInternal(prefab, localPosition, localRotation, networked, combined);
        }

        /// <summary>
        /// Place a prefab with specific components enabled by name.
        /// Useful for enabling only certain game logic (e.g., ATM, VendingMachine).
        /// When <paramref name="networked"/> is true, returns null on non-server callers
        /// but the replicated object is automatically parented on clients.
        /// </summary>
        /// <param name="prefab">Prefab reference from GamePrefabs</param>
        /// <param name="localPosition">Local position relative to parent</param>
        /// <param name="localRotation">Local rotation</param>
        /// <param name="componentNames">Array of component type names to enable</param>
        /// <param name="networked">Whether to spawn on network (server only). When true,
        /// returns null on clients — the server-spawned object is replicated via FishNet
        /// and automatically parented to the building hierarchy.</param>
        /// <returns>The instantiated prefab, or null if not found or if networked and not server</returns>
        public GameObject? PlaceWithComponents(PrefabRef prefab, Vector3 localPosition, Quaternion localRotation, string[] componentNames, bool networked = true)
        {
            string[] names = componentNames;
            return PlaceInternal(prefab, localPosition, localRotation, networked,
                (go) => go.EnableComponentsByName(names, recursive: true));
        }

        /// <summary>
        /// Place sliding double doors at a wall opening.
        /// On server, spawns the door and applies customization (material, opening hours text).
        /// On clients, returns null but queues a deferred link so the FishNet-replicated door
        /// is parented to the building and receives the same customization once it arrives.
        /// See <see cref="BuildingBuilder.AddSlidingDoors"/> remarks for DoorController
        /// server-gating details.
        /// </summary>
        /// <param name="localPosition">Local position for the doors</param>
        /// <param name="localRotation">Local rotation</param>
        /// <param name="openingHoursText">Text to display for opening hours</param>
        /// <param name="doorMaterial">Optional material for door panels</param>
        /// <param name="onServerReady">Optional callback invoked on the door GameObject before activation,
        /// server-only. Runs after internal customization (material, opening hours text) but before
        /// Awake/OnEnable fire, so sensors see configured values. Does not fire on clients.</param>
        /// <returns>The door instance on server, or null on clients / if prefab not found</returns>
        public GameObject? PlaceSlidingDoors(Vector3 localPosition, Quaternion localRotation, string openingHoursText = "6AM-6PM", Material? doorMaterial = null, Action<GameObject>? onServerReady = null)
        {
            Material? mat = doorMaterial;
            string text = openingHoursText;

            // Internal customization fires on both server and client (via deferred linker).
            // Consumer callback fires server-only, still pre-activation.
            bool deferActivation = onServerReady != null;
            GameObject? instance = PlaceInternal(Prefabs.SlidingDoors, localPosition, localRotation, networked: true,
                (go) => CustomizeSlidingDoors(go, mat, text), activate: !deferActivation);

            if (instance != null && deferActivation)
            {
                onServerReady!(instance);
                if (!instance.activeSelf)
                    instance.SetActive(true);
            }

            return instance;
        }

        /// <summary>
        /// Place an item prefab (like bongs on shelves).
        /// </summary>
        /// <param name="prefab">Prefab reference</param>
        /// <param name="localPosition">Local position</param>
        /// <param name="localRotation">Local rotation</param>
        /// <returns>The item instance or null if prefab not found</returns>
        public GameObject? PlaceItem(PrefabRef prefab, Vector3 localPosition, Quaternion localRotation)
        {
            return Place(prefab, localPosition, localRotation, networked: false);
        }

        /// <summary>
        /// Place multiple items in a row (e.g., items on a shelf).
        /// </summary>
        /// <param name="prefab">Prefab reference for items</param>
        /// <param name="startPosition">Starting position</param>
        /// <param name="spacing">Spacing between items</param>
        /// <param name="count">Number of items to place</param>
        /// <param name="baseRotation">Base rotation for items</param>
        /// <param name="rotationVariance">Random rotation variance in degrees</param>
        /// <returns>Array of placed items</returns>
        public GameObject?[] PlaceItemRow(PrefabRef prefab, Vector3 startPosition, float spacing, int count, Quaternion baseRotation, float rotationVariance = 15f)
        {
            GameObject?[] items = new GameObject?[count];
            
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(spacing * (i - (count - 1) / 2f), 0f, 0f);
                float yRotation = UnityEngine.Random.Range(-rotationVariance, rotationVariance);
                Quaternion rotation = baseRotation * Quaternion.Euler(0f, yRotation, 0f);
                
                items[i] = PlaceItem(prefab, startPosition + offset, rotation);
            }

            return items;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Core placement logic shared by all public Place methods.
        /// On server: instantiates, parents, invokes onReady, returns instance.
        /// On client (networked): queues a deferred link via NetworkedPrefabLinkerQueue
        /// so the FishNet-replicated object is parented and customized when it arrives.
        /// </summary>
        private GameObject? PlaceInternal(PrefabRef prefab, Vector3 localPosition, Quaternion localRotation,
            bool networked, Action<GameObject>? onReady, bool activate = true)
        {
            // For networked prefabs, instantiate WITHOUT activating so onReady can
            // configure components before Awake/OnEnable fire. This prevents sensors
            // (e.g., DoorSensor) from triggering with prefab-default values.
            GameObject? instance = networked
                ? prefab.InstantiateNetworkedInactive(_parent, localPosition, localRotation)
                : prefab.Instantiate();

            if (instance == null)
            {
                if (!networked)
                {
                    DebugLog.Warning($"[PrefabPlacer] '{prefab.Name}' local instantiate returned null.");
                    return null;
                }

                if (_parent == null)
                {
                    DebugLog.Warning($"[PrefabPlacer] '{prefab.Name}' returned null and parent is destroyed — cannot queue link.");
                    return null;
                }

                // On a connected client, queue a link so the FishNet-replicated
                // object gets parented to the building once it arrives.
                var nm = InstanceFinder.NetworkManager;
                if (nm != null && nm.IsClient)
                {
                    Vector3 expectedWorldPos = _parent.TransformPoint(localPosition);
                    NetworkedPrefabLinkerQueue.Enqueue(
                        _parent.gameObject, prefab.Name, expectedWorldPos,
                        _parent, localPosition, localRotation, onReady);
                }
                else
                {
                    DebugLog.Warning($"[PrefabPlacer] '{prefab.Name}' returned null — " +
                                      $"nm={nm != null}, isClient={nm?.IsClient}, isServer={nm?.IsServer}");
                }
                return null;
            }

            if (!networked)
            {
                instance.transform.SetParent(_parent);
                instance.transform.localPosition = localPosition;
                instance.transform.localRotation = localRotation;
            }

            onReady?.Invoke(instance);

            // Activate AFTER onReady so sensors/triggers see configured values, not prefab defaults.
            if (activate && networked && !instance.activeSelf)
                instance.SetActive(true);

            return instance;
        }

        private static void CustomizeSlidingDoors(GameObject doors, Material? doorMaterial, string? openingHoursText)
        {
            doors.name = "SlidingDoors";

            if (doorMaterial != null)
            {
                ApplyMaterialToPath(doors, "Door/Door", doorMaterial);
                ApplyMaterialToPath(doors, "Door/Door (1)", doorMaterial);
            }

            if (!string.IsNullOrEmpty(openingHoursText))
            {
                SetOpeningHoursText(doors, openingHoursText);
            }
        }

        private static void ApplyMaterialToPath(GameObject root, string path, Material material)
        {
            Transform t = root.transform.Find(path);
            if (t != null)
            {
                Renderer r = t.GetComponent<Renderer>();
                if (r != null) r.material = material;
            }
        }

        private static void SetOpeningHoursText(GameObject doorInstance, string text)
        {
            Transform signTransform = doorInstance.transform.Find("Door/Door/OpeningHoursSign");
            if (signTransform != null)
            {
                TextMeshPro? tmp = signTransform.GetComponent<TextMeshPro>();
                if (tmp != null)
                {
                    tmp.text = text;
                    return;
                }
            }

            // Fallback: search recursively
            TextMeshPro[] tmps = doorInstance.GetComponentsInChildren<TextMeshPro>(true);
            foreach (var t in tmps)
            {
                if (t.name == "OpeningHoursSign" || t.transform.parent?.name == "OpeningHoursSign")
                {
                    t.text = text;
                    return;
                }
            }
        }

        #endregion
    }
}
