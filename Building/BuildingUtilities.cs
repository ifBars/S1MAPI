using S1MAPI.Extensions;
using UnityEngine;
using UnityEngine.AI;
using S1MAPI.Utils;

namespace S1MAPI.Building
{
    /// <summary>
    /// Utility functions for building construction and configuration
    /// </summary>
    public static class BuildingUtilities
    {
        #region Public API - NavMesh
        
        /// <summary>
        /// Add a NavMesh obstacle to a GameObject
        /// </summary>
        /// <param name="gameObject">The GameObject to add the obstacle to</param>
        /// <param name="carving">Whether the obstacle should carve the NavMesh</param>
        /// <param name="carveOnlyStationary">Whether to only carve when stationary</param>
        public static void AddNavMeshObstacle(
            GameObject gameObject,
            bool carving = true,
            bool carveOnlyStationary = false)
        {
            if (gameObject == null)
            {
                DebugLog.Warning("Cannot add NavMeshObstacle to null GameObject");
                return;
            }

            NavMeshObstacle obstacle = gameObject.GetOrAddComponent<NavMeshObstacle>();
            obstacle.carving = carving;
            obstacle.carveOnlyStationary = carveOnlyStationary;

            DebugLog.Info($"Added NavMeshObstacle to: {gameObject.name}");
        }

        /// <summary>
        /// Add NavMesh obstacles to multiple GameObjects
        /// </summary>
        public static void AddNavMeshObstacles(GameObject[] gameObjects, bool carving = true)
        {
            if (gameObjects == null)
            {
                return;
            }

            foreach (GameObject go in gameObjects)
            {
                AddNavMeshObstacle(go, carving);
            }
        }
        
        #endregion

        #region Public API - Colliders
        
        /// <summary>
        /// Remove all colliders from a GameObject
        /// </summary>
        public static void RemoveColliders(GameObject gameObject, bool includeChildren = false)
        {
            if (gameObject == null)
            {
                return;
            }

            if (includeChildren)
            {
                Collider[] colliders = gameObject.GetComponentsInChildren<Collider>();
                foreach (Collider collider in colliders)
                {
                    UnityEngine.Object.Destroy(collider);
                }
            }
            else
            {
                Collider collider = gameObject.GetComponent<Collider>();
                if (collider != null)
                {
                    UnityEngine.Object.Destroy(collider);
                }
            }
        }

        /// <summary>
        /// Setup collider for a primitive based on naming conventions
        /// Removes colliders from decorative or fallback objects
        /// </summary>
        public static void SetupCollider(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            string lowerName = gameObject.name.ToLower();

            // Remove colliders from decorative objects
            if (lowerName.Contains("fallback") || 
                lowerName.Contains("decorative") ||
                lowerName.Contains("rug") ||
                lowerName.Contains("mat"))
            {
                RemoveColliders(gameObject);
            }
        }
        
        #endregion

        #region Public API - Occlusion
        
        /// <summary>
        /// Apply occlusion settings to all renderers in a GameObject
        /// </summary>
        public static void ApplyOcclusionSettings(GameObject gameObject, bool allowOcclusion)
        {
            if (gameObject == null)
            {
                return;
            }

            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                renderer.allowOcclusionWhenDynamic = allowOcclusion;
            }

            DebugLog.Info($"Set occlusion to {allowOcclusion} for {renderers.Length} renderers in {gameObject.name}");
        }
        
        #endregion

        #region Public API - Grid

        /// <summary>
        /// Compute the grid cell size for a room with the given dimensions.
        /// Returns the largest square cell size ≤ <see cref="Constants.Spatial.DefaultGridSize"/>
        /// that evenly divides the room's X axis. The pathfinding grid uses this
        /// same value so furniture placement and NPC navigation are always aligned.
        /// </summary>
        public static float ComputeGridCellSize(float roomX, float roomZ)
        {
            float target = Constants.Spatial.DefaultGridSize;
            int nx = Mathf.Max(1, Mathf.CeilToInt(roomX / target));
            int nz = Mathf.Max(1, Mathf.CeilToInt(roomZ / target));
            return Mathf.Min(roomX / nx, roomZ / nz);
        }


        /// <summary>
        /// Check whether a transform belongs to a living entity (player/NPC) by
        /// looking for a <see cref="NavMeshAgent"/> or <see cref="CharacterController"/>
        /// anywhere in its root hierarchy.
        /// Results are cached in the provided sets for efficiency.
        /// </summary>
        /// <returns>true if the transform's root contains a NavMeshAgent or CharacterController.</returns>
        internal static bool IsLivingEntity(Transform t, HashSet<int> livingRoots, HashSet<int> staticRoots)
        {
            int rootId = t.root.GetInstanceID();
            if (livingRoots.Contains(rootId)) return true;
            if (staticRoots.Contains(rootId)) return false;

            Transform root = t.root;
            bool isLiving = root.GetComponentInChildren<NavMeshAgent>() != null
                         || root.GetComponentInChildren<CharacterController>() != null;

            (isLiving ? livingRoots : staticRoots).Add(rootId);
            return isLiving;
        }

        #endregion

        #region Public API - Hierarchy Organization
        
        /// <summary>
        /// Create an empty GameObject to serve as a folder/container
        /// </summary>
        public static GameObject CreateFolder(string name, Transform? parent = null)
        {
            GameObject folder = new GameObject(name);

            if (parent != null)
            {
                folder.transform.SetParent(parent);
                folder.transform.Reset();
            }

            return folder;
        }
        
        #endregion
    }
}
