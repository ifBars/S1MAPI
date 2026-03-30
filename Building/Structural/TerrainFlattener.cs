using S1MAPI.Utils;
using UnityEngine;

#if IL2CPP
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#endif

namespace S1MAPI.Building.Structural
{
    /// <summary>
    /// Flattens terrain height and clears detail/grass under a building footprint.
    /// Only lowers terrain — never raises it — to avoid creating dirt walls.
    /// Works on both Mono and IL2CPP runtimes.
    /// </summary>
    public static class TerrainFlattener
    {
        #region IL2CPP ICalls

#if IL2CPP
        // Height ICalls — GetHeights managed wrapper crashes in Il2CppObjectPool, so we bypass it too
        private delegate System.IntPtr Internal_GetHeightsDelegate(
            System.IntPtr @this, int xBase, int yBase, int width, int height);

        private delegate void Internal_SetHeightsDelegate(
            System.IntPtr @this, int xBase, int yBase, int totalWidth, int totalHeight, System.IntPtr heights);

        // Detail ICalls — both GetDetailLayer and SetDetailLayer are stripped
        private delegate System.IntPtr GetDetailLayerDelegate(
            System.IntPtr @this, int xBase, int yBase, int width, int height, int layer);

        private delegate void Internal_SetDetailLayerDelegate(
            System.IntPtr @this, int xBase, int yBase, int totalWidth, int totalHeight,
            int detailIndex, System.IntPtr data);

        private static Internal_GetHeightsDelegate? _getHeightsICall;
        private static Internal_SetHeightsDelegate? _setHeightsICall;
        private static bool _heightICallsResolved;

        private static GetDetailLayerDelegate? _getDetailLayerICall;
        private static Internal_SetDetailLayerDelegate? _setDetailLayerICall;
        private static bool _detailICallsResolved;

        /// <summary>
        /// Resolve both Internal_GetHeights and Internal_SetHeights ICalls.
        /// Returns true only if both resolved successfully.
        /// </summary>
        private static bool ResolveHeightICalls()
        {
            if (_heightICallsResolved) return _getHeightsICall != null && _setHeightsICall != null;
            _heightICallsResolved = true;

            try
            {
                _getHeightsICall = IL2CPP.ResolveICall<Internal_GetHeightsDelegate>(
                    "UnityEngine.TerrainData::Internal_GetHeights");
                _setHeightsICall = IL2CPP.ResolveICall<Internal_SetHeightsDelegate>(
                    "UnityEngine.TerrainData::Internal_SetHeights");

                if (_getHeightsICall != null && _setHeightsICall != null)
                    DebugLog.Info("[TerrainFlattener] Resolved height ICalls.");
                else
                    DebugLog.Error("[TerrainFlattener] Height ICalls resolved to null.");
            }
            catch (System.Exception ex)
            {
                DebugLog.Error($"[TerrainFlattener] Failed to resolve height ICalls: {ex.Message}");
                _getHeightsICall = null;
                _setHeightsICall = null;
            }

            return _getHeightsICall != null && _setHeightsICall != null;
        }

        /// <summary>
        /// Resolve both GetDetailLayer and Internal_SetDetailLayer ICalls.
        /// Returns true only if both resolved successfully.
        /// </summary>
        private static bool ResolveDetailICalls()
        {
            if (_detailICallsResolved) return _getDetailLayerICall != null && _setDetailLayerICall != null;
            _detailICallsResolved = true;

            try
            {
                _getDetailLayerICall = IL2CPP.ResolveICall<GetDetailLayerDelegate>(
                    "UnityEngine.TerrainData::GetDetailLayer");
                _setDetailLayerICall = IL2CPP.ResolveICall<Internal_SetDetailLayerDelegate>(
                    "UnityEngine.TerrainData::Internal_SetDetailLayer");

                if (_getDetailLayerICall != null && _setDetailLayerICall != null)
                    DebugLog.Info("[TerrainFlattener] Resolved detail layer ICalls.");
                else
                    DebugLog.Warning("[TerrainFlattener] Detail layer ICalls resolved to null — grass clearing unavailable.");
            }
            catch (System.Exception ex)
            {
                DebugLog.Warning($"[TerrainFlattener] Failed to resolve detail layer ICalls: {ex.Message}");
                _getDetailLayerICall = null;
                _setDetailLayerICall = null;
            }

            return _getDetailLayerICall != null && _setDetailLayerICall != null;
        }
#endif

        #endregion

        #region Public API

        /// <summary>
        /// Flatten terrain under a building so the ground is level at the target height.
        /// Only lowers terrain that is above the target — never raises terrain.
        /// Optionally clears grass and detail layers in the flattened region.
        /// </summary>
        /// <param name="buildingRoot">The positioned building root GameObject.</param>
        /// <param name="roomSize">The building's room dimensions.</param>
        /// <param name="targetWorldY">World Y to flatten to (typically foundation base).</param>
        /// <param name="padding">Extra padding around the footprint in meters.</param>
        /// <param name="clearDetails">Clear grass and detail layers in the flattened region.</param>
        /// <param name="blendDistance">Distance in meters over which terrain smoothly transitions
        /// from the flattened height back to natural terrain. 0 = hard edge.</param>
        /// <returns>True if terrain was found and flattened immediately; false if terrain wasn't available
        /// (a background retry has been queued automatically).</returns>
        public static bool FlattenUnder(
            GameObject buildingRoot, Vector3 roomSize,
            float targetWorldY, float padding = Constants.Terrain.DefaultFlattenPadding,
            bool clearDetails = true, float blendDistance = 0f)
        {
            try
            {
                if (FlattenUnderCore(buildingRoot, roomSize, targetWorldY, padding, clearDetails, blendDistance))
                    return true;
            }
            catch (System.Exception ex)
            {
                // IL2CPP clients can throw TypeInitializationException when terrain
                // runtime generics aren't initialized yet. Catch and fall through to retry.
                DebugLog.Warning($"[TerrainFlattener] FlattenUnder threw (will retry): {ex.GetType().Name}: {ex.Message}");
            }

            // Terrain not loaded or threw — queue automatic retry on the building root
            DebugLog.Info("[TerrainFlattener] Terrain not available — queuing retry.");
            TerrainRetryQueue.Enqueue(buildingRoot,
                () => FlattenUnderCore(buildingRoot, roomSize, targetWorldY, padding, clearDetails, blendDistance));
            return false;
        }

        private static bool FlattenUnderCore(
            GameObject buildingRoot, Vector3 roomSize,
            float targetWorldY, float padding, bool clearDetails, float blendDistance)
        {
            Bounds innerBounds = ComputeXZBounds(buildingRoot.transform, roomSize, padding);

            // Expand sample region to include blend zone
            Bounds sampleBounds = innerBounds;
            if (blendDistance > 0f)
                sampleBounds.Expand(new Vector3(blendDistance * 2f, 0f, blendDistance * 2f));

            Terrain? terrain = FindCoveringTerrain(innerBounds);
            if (terrain == null)
                return false;

            TerrainData tData = terrain.terrainData;
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = tData.size;
            int hmRes = tData.heightmapResolution;

            // Convert expanded bounds to heightmap sample indices
            int xStart = Mathf.Clamp(Mathf.FloorToInt((sampleBounds.min.x - terrainPos.x) / terrainSize.x * hmRes), 0, hmRes - 1);
            int xEnd = Mathf.Clamp(Mathf.CeilToInt((sampleBounds.max.x - terrainPos.x) / terrainSize.x * hmRes), 0, hmRes - 1);
            int zStart = Mathf.Clamp(Mathf.FloorToInt((sampleBounds.min.z - terrainPos.z) / terrainSize.z * hmRes), 0, hmRes - 1);
            int zEnd = Mathf.Clamp(Mathf.CeilToInt((sampleBounds.max.z - terrainPos.z) / terrainSize.z * hmRes), 0, hmRes - 1);

            int sampleWidth = xEnd - xStart + 1;
            int sampleHeight = zEnd - zStart + 1;

            if (sampleWidth <= 0 || sampleHeight <= 0)
            {
                DebugLog.Warning("[TerrainFlattener] Computed zero-size sample region.");
                return false;
            }

            // Compute blend zone size in samples for each axis
            int blendSamplesX = blendDistance > 0f
                ? Mathf.CeilToInt(blendDistance / terrainSize.x * hmRes)
                : 0;
            int blendSamplesZ = blendDistance > 0f
                ? Mathf.CeilToInt(blendDistance / terrainSize.z * hmRes)
                : 0;

            float normalizedTarget = Mathf.Clamp01((targetWorldY - terrainPos.y) / terrainSize.y);

#if MONO
            FlattenMono(tData, xStart, zStart, sampleWidth, sampleHeight, normalizedTarget,
                blendSamplesX, blendSamplesZ);
#elif IL2CPP
            FlattenIl2Cpp(tData, xStart, zStart, sampleWidth, sampleHeight, normalizedTarget,
                blendSamplesX, blendSamplesZ);
#endif

            if (clearDetails)
            {
                // Clear details only in the inner footprint, not the blend zone
                ClearDetails(tData, terrainPos, terrainSize, innerBounds);
            }

            FlushTerrain(terrain);

            DebugLog.Info($"[TerrainFlattener] Flattened {sampleWidth}x{sampleHeight} samples " +
                          $"to Y={targetWorldY:F2} on terrain '{terrain.name}'.");
            return true;
        }

        #endregion

        #region Private Methods — Terrain Lookup

        /// <summary>
        /// Find the terrain whose XZ bounds cover the given world-space bounds.
        /// Skips terrains with null terrainData.
        /// </summary>
        private static Terrain? FindCoveringTerrain(Bounds worldBounds)
        {
            foreach (Terrain t in Terrain.activeTerrains)
            {
                TerrainData? data;
                try
                {
                    data = t.terrainData;
                    if (data == null) continue;
                }
                catch (System.Exception)
                {
                    continue;
                }

                Vector3 tPos = t.transform.position;
                Vector3 tSize = data.size;

                if (worldBounds.min.x >= tPos.x && worldBounds.max.x <= tPos.x + tSize.x &&
                    worldBounds.min.z >= tPos.z && worldBounds.max.z <= tPos.z + tSize.z)
                {
                    return t;
                }
            }

            return null;
        }

        #endregion

        #region Private Methods — Bounds

        /// <summary>
        /// Compute a padded world-space AABB from the building's rotated footprint.
        /// Only uses XZ corners (Y is irrelevant for heightmap sampling).
        /// </summary>
        private static Bounds ComputeXZBounds(Transform building, Vector3 roomSize, float padding)
        {
            Vector3 c0 = building.TransformPoint(new Vector3(0f, 0f, 0f));
            Vector3 c1 = building.TransformPoint(new Vector3(roomSize.x, 0f, 0f));
            Vector3 c2 = building.TransformPoint(new Vector3(0f, 0f, roomSize.z));
            Vector3 c3 = building.TransformPoint(new Vector3(roomSize.x, 0f, roomSize.z));

            var bounds = new Bounds(c0, Vector3.zero);
            bounds.Encapsulate(c1);
            bounds.Encapsulate(c2);
            bounds.Encapsulate(c3);
            bounds.Expand(new Vector3(padding * 2f, 0f, padding * 2f));

            return bounds;
        }

        /// <summary>
        /// Compute a smoothstep blend factor for a sample position.
        /// Returns 0 inside the inner flat zone, smoothly ramps to 1 at the outer blend edge.
        /// </summary>
        private static float ComputeBlendFactor(
            int x, int z, int sampleWidth, int sampleHeight,
            int blendSamplesX, int blendSamplesZ)
        {
            if (blendSamplesX <= 0 && blendSamplesZ <= 0) return 0f;

            // Normalized distance from inner bounds edge (0 = at edge, 1 = at outer limit)
            float dx = 0f;
            if (blendSamplesX > 0)
            {
                if (x < blendSamplesX)
                    dx = (float)(blendSamplesX - x) / blendSamplesX;
                else if (x >= sampleWidth - blendSamplesX)
                    dx = (float)(x - (sampleWidth - 1 - blendSamplesX)) / blendSamplesX;
            }

            float dz = 0f;
            if (blendSamplesZ > 0)
            {
                if (z < blendSamplesZ)
                    dz = (float)(blendSamplesZ - z) / blendSamplesZ;
                else if (z >= sampleHeight - blendSamplesZ)
                    dz = (float)(z - (sampleHeight - 1 - blendSamplesZ)) / blendSamplesZ;
            }

            // Rectangular falloff — use max of axis distances
            float t = Mathf.Clamp01(Mathf.Max(dx, dz));

            // Smoothstep for natural-looking transition
            return t * t * (3f - 2f * t);
        }

        #endregion

        #region Private Methods — Detail Clearing

        /// <summary>
        /// Clear grass and detail layers in the given world-space bounds.
        /// Converts to detail map indices and delegates to the platform-specific implementation.
        /// </summary>
        private static void ClearDetails(
            TerrainData tData, Vector3 terrainPos, Vector3 terrainSize, Bounds worldBounds)
        {
            int detailWidth = tData.detailWidth;
            int detailHeight = tData.detailHeight;

            if (detailWidth <= 0 || detailHeight <= 0) return;

            // Convert world XZ to detail map indices
            int dxStart = Mathf.Clamp(Mathf.FloorToInt((worldBounds.min.x - terrainPos.x) / terrainSize.x * detailWidth), 0, detailWidth - 1);
            int dxEnd = Mathf.Clamp(Mathf.CeilToInt((worldBounds.max.x - terrainPos.x) / terrainSize.x * detailWidth), 0, detailWidth - 1);
            int dzStart = Mathf.Clamp(Mathf.FloorToInt((worldBounds.min.z - terrainPos.z) / terrainSize.z * detailHeight), 0, detailHeight - 1);
            int dzEnd = Mathf.Clamp(Mathf.CeilToInt((worldBounds.max.z - terrainPos.z) / terrainSize.z * detailHeight), 0, detailHeight - 1);

            int regionWidth = dxEnd - dxStart + 1;
            int regionHeight = dzEnd - dzStart + 1;

            if (regionWidth <= 0 || regionHeight <= 0) return;

            // GetSupportedLayers returns which detail layers have data in this region
#if MONO
            int[] layers = tData.GetSupportedLayers(dxStart, dzStart, regionWidth, regionHeight);
#elif IL2CPP
            Il2CppStructArray<int> layersArray = tData.GetSupportedLayers(dxStart, dzStart, regionWidth, regionHeight);
            int[] layers = new int[layersArray.Length];
            for (int i = 0; i < layersArray.Length; i++)
                layers[i] = layersArray[i];
#endif

            if (layers.Length == 0) return;

            int cleared = 0;
            foreach (int layer in layers)
            {
#if MONO
                if (ClearDetailLayerMono(tData, dxStart, dzStart, regionWidth, regionHeight, layer))
                    cleared++;
#elif IL2CPP
                if (ClearDetailLayerIl2Cpp(tData, dxStart, dzStart, regionWidth, regionHeight, layer))
                    cleared++;
#endif
            }

            if (cleared > 0)
            {
                DebugLog.Info($"[TerrainFlattener] Cleared {cleared} detail layer(s) " +
                              $"in {regionWidth}x{regionHeight} region.");
            }
        }

        #endregion

        #region Private Methods — Mono

#if MONO
        private static void FlattenMono(
            TerrainData tData, int xStart, int zStart,
            int sampleWidth, int sampleHeight, float normalizedTarget,
            int blendSamplesX, int blendSamplesZ)
        {
            // GetHeights parameter order: (xBase, yBase, width, height)
            // where yBase = z index, height = z count
            // Returns float[zCount, xCount] indexed as [z, x]
            float[,] heights = tData.GetHeights(xStart, zStart, sampleWidth, sampleHeight);

            bool modified = false;
            for (int z = 0; z < sampleHeight; z++)
            {
                for (int x = 0; x < sampleWidth; x++)
                {
                    float blend = ComputeBlendFactor(x, z, sampleWidth, sampleHeight,
                        blendSamplesX, blendSamplesZ);
                    float blendedTarget = normalizedTarget + blend * (heights[z, x] - normalizedTarget);

                    if (heights[z, x] > blendedTarget)
                    {
                        heights[z, x] = blendedTarget;
                        modified = true;
                    }
                }
            }

            if (modified)
                tData.SetHeights(xStart, zStart, heights);
        }

        private static bool ClearDetailLayerMono(
            TerrainData tData, int xStart, int zStart,
            int regionWidth, int regionHeight, int layer)
        {
            int[,] details = tData.GetDetailLayer(xStart, zStart, regionWidth, regionHeight, layer);
            bool modified = false;

            for (int z = 0; z < regionHeight; z++)
            {
                for (int x = 0; x < regionWidth; x++)
                {
                    if (details[z, x] != 0)
                    {
                        details[z, x] = 0;
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                tData.SetDetailLayer(xStart, zStart, layer, details);
            }

            return modified;
        }
#endif

        #endregion

        #region Private Methods — IL2CPP

#if IL2CPP
        private static void FlattenIl2Cpp(
            TerrainData tData, int xStart, int zStart,
            int sampleWidth, int sampleHeight, float normalizedTarget,
            int blendSamplesX, int blendSamplesZ)
        {
            if (!ResolveHeightICalls())
            {
                DebugLog.Error("[TerrainFlattener] Cannot flatten terrain — height ICalls unavailable.");
                return;
            }

            System.IntPtr tDataPtr = IL2CPP.Il2CppObjectBaseToPtrNotNull(tData);

            // Call Internal_GetHeights directly — the managed GetHeights wrapper
            // crashes in Il2CppObjectPool when wrapping the return value.
            System.IntPtr heightsPtr = _getHeightsICall!(tDataPtr, xStart, zStart, sampleWidth, sampleHeight);
            if (heightsPtr == System.IntPtr.Zero)
            {
                DebugLog.Error("[TerrainFlattener] Internal_GetHeights returned null.");
                return;
            }

            // Wrap the native 2D float array as a flat Il2CppStructArray for direct data access.
            // The underlying memory is contiguous row-major floats [z0x0, z0x1, ..., z1x0, ...].
            var heightsArray = new Il2CppStructArray<float>(heightsPtr);
            System.Span<float> data = heightsArray.AsSpan();
            int totalElements = sampleHeight * sampleWidth;

            bool modified = false;
            for (int i = 0; i < totalElements; i++)
            {
                int z = i / sampleWidth;
                int x = i % sampleWidth;
                float blend = ComputeBlendFactor(x, z, sampleWidth, sampleHeight,
                    blendSamplesX, blendSamplesZ);
                float blendedTarget = normalizedTarget + blend * (data[i] - normalizedTarget);

                if (data[i] > blendedTarget)
                {
                    data[i] = blendedTarget;
                    modified = true;
                }
            }

            if (modified)
                _setHeightsICall!(tDataPtr, xStart, zStart, sampleWidth, sampleHeight, heightsPtr);
        }

        private static bool ClearDetailLayerIl2Cpp(
            TerrainData tData, int xStart, int zStart,
            int regionWidth, int regionHeight, int layer)
        {
            if (!ResolveDetailICalls()) return false;

            System.IntPtr tDataPtr = IL2CPP.Il2CppObjectBaseToPtrNotNull(tData);

            // Call GetDetailLayer ICall to get a native int[,] array
            System.IntPtr detailPtr = _getDetailLayerICall!(tDataPtr, xStart, zStart, regionWidth, regionHeight, layer);
            if (detailPtr == System.IntPtr.Zero)
            {
                DebugLog.Warning($"[TerrainFlattener] GetDetailLayer returned null for layer {layer}.");
                return false;
            }

            // Wrap the native 2D int array and zero it out
            var detailArray = new Il2CppStructArray<int>(detailPtr);
            System.Span<int> data = detailArray.AsSpan();

            bool modified = false;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != 0)
                {
                    data[i] = 0;
                    modified = true;
                }
            }

            if (modified)
            {
                // Internal_SetDetailLayer(this, xBase, yBase, width, height, layerIndex, data)
                _setDetailLayerICall!(tDataPtr, xStart, zStart, regionWidth, regionHeight, layer, detailPtr);
            }

            return modified;
        }
#endif

        #endregion

        #region Private Methods — Flush

        /// <summary>
        /// Force terrain to update visuals and collision data.
        /// </summary>
        private static void FlushTerrain(Terrain terrain)
        {
            TerrainCollider? collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                collider.terrainData = null;
                collider.terrainData = terrain.terrainData;
            }
            terrain.Flush();
        }

        #endregion
    }
}
