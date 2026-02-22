using S1MAPI.Utils;
using UnityEngine;

namespace S1MAPI.Building.Structural
{
    /// <summary>
    /// Configuration for area clearing operations.
    /// </summary>
    public sealed class ClearingOptions
    {
        /// <summary>Extra padding around the clearing bounds in meters.</summary>
        public float Padding { get; set; } = Constants.Terrain.DefaultClearingPadding;

        /// <summary>Whether to remove terrain tree instances.</summary>
        public bool ClearTerrainTrees { get; set; } = true;

        /// <summary>Whether to remove all scene objects within the building footprint.</summary>
        public bool ClearSceneObjects { get; set; } = true;

        /// <summary>Whether to remove vegetation/clutter within the padded area around the building.</summary>
        public bool ClearVegetation { get; set; } = true;

        /// <summary>
        /// Name patterns for vegetation and natural clutter (case-insensitive).
        /// Only used by the vegetation pass. Objects inside the building footprint
        /// are destroyed regardless of keywords (unless protected).
        /// </summary>
        public string[]? VegetationKeywords { get; set; } = Constants.Terrain.DefaultVegetationKeywords;

        /// <summary>
        /// Name patterns for objects protected from footprint destruction (case-insensitive).
        /// Objects matching these keywords are skipped by the footprint pass because they
        /// typically extend far beyond the building and leave visible gaps when destroyed.
        /// Set to null to destroy everything in the footprint.
        /// </summary>
        public string[]? ProtectedKeywords { get; set; } = Constants.Terrain.DefaultProtectedKeywords;

        /// <summary>
        /// Transforms to preserve during clearing (e.g. the building itself).
        /// All children of preserved transforms are also preserved.
        /// Auto-populated from the building hierarchy when using
        /// <see cref="TerrainClearer.ClearAroundBuilding"/>.
        /// </summary>
        public Transform[]? Preserved { get; set; }

        /// <summary>
        /// Optional filter applied to both passes.
        /// Return true to keep, false to remove.
        /// </summary>
        /// <example>
        /// // Keep rocks inside the building footprint:
        /// options.Filter = go => go.name.Contains("Rock");
        /// </example>
        public Func<GameObject, bool>? Filter { get; set; }

        /// <summary>Default clearing options.</summary>
        public static ClearingOptions Default =>
            new();
    }

    /// <summary>
    /// Clears terrain trees and scene objects from a specified world-space area.
    /// Typically used to prepare a building site after positioning.
    /// </summary>
    public static class TerrainClearer
    {
        #region API

        /// <summary>
        /// Clear terrain trees and scene objects within the specified world-space bounds.
        /// </summary>
        internal static void ClearArea(Bounds bounds, ClearingOptions? options = null)
        {
            options ??= ClearingOptions.Default;
            float pad = options.Padding;

            int treesRemoved = 0;
            int objectsRemoved = 0;

            // Terrain trees need extra padding (canopies extend beyond trunk).
            if (options.ClearTerrainTrees)
            {
                Bounds treeBounds = bounds;
                treeBounds.Expand(new Vector3(pad * 2f, pad * 2f, pad * 2f));
                treesRemoved = ClearTerrainTrees(treeBounds);
            }

            // Scene objects: two passes in a single scan.
            // Pass 1 (ClearSceneObjects): destroy everything in building footprint.
            // Pass 2 (ClearVegetation): destroy vegetation in padded area around building.
            if (options.ClearSceneObjects || options.ClearVegetation)
            {
                Bounds vegBounds = bounds;
                vegBounds.Expand(new Vector3(pad, pad, pad));
                objectsRemoved = ClearSceneObjects(bounds, vegBounds, options);
            }

            if (treesRemoved + objectsRemoved > 0)
                DebugLog.Info($"[TerrainClearer] Cleared {treesRemoved} trees, {objectsRemoved} objects");
        }

        /// <summary>
        /// Clear the area around a building using its transform and room size.
        /// Correctly handles rotated buildings by computing world-space bounds
        /// from the building's actual corners.
        /// </summary>
        /// <param name="buildingRoot">The building root GameObject (must be positioned).</param>
        /// <param name="roomSize">The building's room dimensions.</param>
        /// <param name="options">Clearing configuration. Uses defaults if null.</param>
        public static void ClearAroundBuilding(
            GameObject buildingRoot, Vector3 roomSize, ClearingOptions? options = null)
        {
            ClearingOptions opts = options ?? ClearingOptions.Default;

            // Auto-populate Preserved with the building hierarchy if not set,
            // without mutating the caller's options instance.
            if (opts.Preserved == null)
            {
                opts = new ClearingOptions
                {
                    Padding = opts.Padding,
                    ClearTerrainTrees = opts.ClearTerrainTrees,
                    ClearSceneObjects = opts.ClearSceneObjects,
                    ClearVegetation = opts.ClearVegetation,
                    VegetationKeywords = opts.VegetationKeywords,
                    ProtectedKeywords = opts.ProtectedKeywords,
                    Preserved = new[] { buildingRoot.transform },
                    Filter = opts.Filter
                };
            }

            Bounds bounds = ComputeWorldBounds(buildingRoot.transform, roomSize);
            ClearArea(bounds, opts);
        }

        #endregion

        #region Bounds Computation

        /// <summary>
        /// Compute an axis-aligned bounding box from a building's rotated corners.
        /// </summary>
        private static Bounds ComputeWorldBounds(Transform building, Vector3 roomSize)
        {
            Vector3 min = Vector3.zero;
            Vector3 max = roomSize;

            Vector3 c0 = building.TransformPoint(new Vector3(min.x, min.y, min.z));
            Vector3 c1 = building.TransformPoint(new Vector3(max.x, min.y, min.z));
            Vector3 c2 = building.TransformPoint(new Vector3(min.x, min.y, max.z));
            Vector3 c3 = building.TransformPoint(new Vector3(max.x, min.y, max.z));
            Vector3 c4 = building.TransformPoint(new Vector3(min.x, max.y, min.z));
            Vector3 c5 = building.TransformPoint(new Vector3(max.x, max.y, min.z));
            Vector3 c6 = building.TransformPoint(new Vector3(min.x, max.y, max.z));
            Vector3 c7 = building.TransformPoint(new Vector3(max.x, max.y, max.z));

            Bounds b = new Bounds(c0, Vector3.zero);
            b.Encapsulate(c1);
            b.Encapsulate(c2);
            b.Encapsulate(c3);
            b.Encapsulate(c4);
            b.Encapsulate(c5);
            b.Encapsulate(c6);
            b.Encapsulate(c7);

            return b;
        }

        #endregion

        #region Terrain Trees

        /// <summary>Remove tree instances from all active terrains that fall within bounds.</summary>
        private static int ClearTerrainTrees(Bounds bounds)
        {
            int totalRemoved = 0;

            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                TerrainData data;
                try
                {
                    data = terrain.terrainData;
                    if (data is null) continue;
                }
                catch (Exception ex)
                {
                    DebugLog.Warning($"[TerrainClearer] Could not access terrain data for '{terrain.name}': {ex.Message}");
                    continue;
                }

                Vector3 terrainPos = terrain.transform.position;
                Vector3 terrainSize = data.size;
                TreeInstance[] originalTrees = data.treeInstances;
                List<TreeInstance> surviving = new List<TreeInstance>(originalTrees.Length);
                int removed = 0;

                for (int i = 0; i < originalTrees.Length; i++)
                {
                    TreeInstance tree = originalTrees[i];
                    Vector3 worldPos = new Vector3(
                        terrainPos.x + tree.position.x * terrainSize.x,
                        terrainPos.y + tree.position.y * terrainSize.y,
                        terrainPos.z + tree.position.z * terrainSize.z
                    );

                    if (bounds.Contains(worldPos))
                    {
                        removed++;
                    }
                    else
                    {
                        surviving.Add(tree);
                    }
                }

                if (removed > 0)
                {
                    data.treeInstances = surviving.ToArray();

                    TerrainCollider? collider = terrain.GetComponent<TerrainCollider>();
                    if (collider != null)
                    {
                        collider.enabled = false;
                        collider.enabled = true;
                    }
                    terrain.Flush();

                    DebugLog.Info($"[TerrainClearer] Removed {removed} trees from terrain '{terrain.name}'");
                }

                totalRemoved += removed;
            }

            return totalRemoved;
        }

        #endregion

        #region Scene Objects

        /// <summary>
        /// Destroy scene objects using a two-pass approach in a single renderer scan.
        /// Pass 1 (footprint): destroys all objects inside buildingBounds.
        /// Pass 2 (vegetation): destroys only keyword-matched objects in the padded vegetationBounds.
        /// Both passes respect Preserved and Filter.
        /// </summary>
        private static int ClearSceneObjects(
            Bounds buildingBounds, Bounds vegetationBounds, ClearingOptions options)
        {
            // Use the building's XZ for the footprint but the vegetation bounds' Y range,
            // so ground-level objects (y=0) under an elevated building (y=1) are caught
            // while deep underground objects (sewers) are excluded.
            Bounds footprintBounds = buildingBounds;
            Vector3 fpMin = footprintBounds.min;
            Vector3 fpMax = footprintBounds.max;
            fpMin.y = vegetationBounds.min.y;
            fpMax.y = vegetationBounds.max.y;
            footprintBounds.SetMinMax(fpMin, fpMax);

            Renderer[] allRenderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
            HashSet<Transform> preserved = BuildPreservedSet(options);
            HashSet<GameObject> toDestroy = new HashSet<GameObject>();

            foreach (Renderer r in allRenderers)
            {
                if (r == null) continue;
                Transform t = r.transform;
                if (t.GetComponent<Terrain>() != null) continue;
                if (preserved.Contains(t)) continue;

                bool inFootprint = footprintBounds.Contains(t.position);
                bool inVegetationZone = !inFootprint && vegetationBounds.Contains(t.position);

                if (!inFootprint && !inVegetationZone) continue;

                GameObject target = ResolveLodRoot(t.gameObject);

                // Pass 1: everything inside the building footprint, except protected objects.
                if (inFootprint && options.ClearSceneObjects)
                {
                    if (options.ProtectedKeywords != null
                        && MatchesKeyword(target.name, options.ProtectedKeywords))
                        continue;
                    if (options.Filter != null && options.Filter(target)) continue;
                    toDestroy.Add(target);
                    continue;
                }

                // Pass 2: only vegetation in the padded zone around the building.
                if (inVegetationZone && options.ClearVegetation
                    && options.VegetationKeywords != null
                    && MatchesKeyword(target.name, options.VegetationKeywords))
                {
                    if (options.Filter != null && options.Filter(target)) continue;
                    toDestroy.Add(target);
                }
            }

            foreach (GameObject go in toDestroy)
                UnityEngine.Object.Destroy(go);

            return toDestroy.Count;
        }

        /// <summary>Walk up from a renderer to its LODGroup parent, if one exists.</summary>
        private static GameObject ResolveLodRoot(GameObject obj)
        {
            Transform current = obj.transform;
            while (current.parent != null)
            {
                if (current.parent.GetComponent<LODGroup>() != null)
                    return current.parent.gameObject;
                current = current.parent;
            }
            return obj;
        }

        /// <summary>Check if a GameObject name contains any of the given keywords (case-insensitive).</summary>
        private static bool MatchesKeyword(string name, string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>Flatten all preserved transforms and their children into a set for fast lookup.</summary>
        private static HashSet<Transform> BuildPreservedSet(ClearingOptions options)
        {
            HashSet<Transform> preserved = new HashSet<Transform>();

            if (options.Preserved == null) return preserved;

            foreach (Transform root in options.Preserved)
            {
                if (root == null) continue;
                foreach (Transform child in root.GetComponentsInChildren<Transform>())
                {
                    preserved.Add(child);
                }
            }

            return preserved;
        }

        #endregion
    }
}
