using System;
using System.Collections.Generic;
using S1MAPI.Utils;
using UnityEngine;

#if IL2CPP
using Il2CppInterop.Runtime.Injection;
#endif

namespace S1MAPI.Building.Structural
{
    /// <summary>
    /// Static operations queue for deferred terrain operations.
    /// Separated from <see cref="TerrainRetryBehaviour"/> so that generic delegates
    /// never appear on the ClassInjector-registered MonoBehaviour — IL2CPP cannot
    /// marshal generic delegate parameters on injected types.
    /// </summary>
    internal static class TerrainRetryQueue
    {
        private const float RetryInterval = 1f;
        private const int MaxRetries = 30;

        private sealed class RetryState
        {
            public readonly List<Func<bool>> Ops = new List<Func<bool>>();
            public int Attempts;
            public float NextRetryTime;
        }

        private static readonly Dictionary<int, RetryState> States = new Dictionary<int, RetryState>();

        /// <summary>
        /// Ensure a <see cref="TerrainRetryBehaviour"/> exists on the target
        /// and queue an operation to retry when terrain becomes available.
        /// </summary>
        internal static void Enqueue(GameObject target, Func<bool> operation)
        {
            var behaviour = TerrainRetryBehaviour.EnsureOn(target);
            int id = behaviour.GetInstanceID();

            if (!States.TryGetValue(id, out var state))
            {
                state = new RetryState { NextRetryTime = Time.unscaledTime + RetryInterval };
                States[id] = state;
            }
            state.Ops.Add(operation);

            DebugLog.Info($"[TerrainRetry] Queued operation ({state.Ops.Count} pending). " +
                          $"Terrain.activeTerrains={Terrain.activeTerrains.Length}, " +
                          $"building='{target.name}'");
        }

        /// <summary>
        /// Called by <see cref="TerrainRetryBehaviour.Update"/> each frame.
        /// Returns true if the behaviour should keep running, false if it should self-destruct.
        /// </summary>
        internal static bool Tick(int id, string buildingName)
        {
            if (!States.TryGetValue(id, out var state) || state.Ops.Count == 0)
            {
                States.Remove(id);
                return false;
            }

            // Throttle to RetryInterval using unscaledTime (independent of timeScale)
            if (Time.unscaledTime < state.NextRetryTime)
                return true;

            state.Attempts++;
            state.NextRetryTime = Time.unscaledTime + RetryInterval;

            int terrainCount = Terrain.activeTerrains.Length;

            // Check retry limit
            if (state.Attempts > MaxRetries)
            {
                DebugLog.Error($"[TerrainRetry] Gave up after {MaxRetries} attempts " +
                               $"({state.Ops.Count} ops remaining). " +
                               $"Terrain.activeTerrains={terrainCount}, " +
                               $"building='{buildingName}'");
                States.Remove(id);
                return false;
            }

            // Wait for terrain to be available
            if (terrainCount == 0)
            {
                if (state.Attempts % 5 == 0)
                {
                    DebugLog.Info($"[TerrainRetry] Waiting for terrain... " +
                                  $"attempt {state.Attempts}/{MaxRetries}, " +
                                  $"building='{buildingName}'");
                }
                return true;
            }

            // Terrain is available — execute all pending operations
            DebugLog.Info($"[TerrainRetry] Terrain available ({terrainCount} active). " +
                          $"Executing {state.Ops.Count} deferred ops after {state.Attempts} attempt(s).");

            for (int i = state.Ops.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (state.Ops[i]())
                    {
                        state.Ops.RemoveAt(i);
                    }
                    else
                    {
                        DebugLog.Warning("[TerrainRetry] Operation returned false " +
                                         "despite terrain being available — removing.");
                        state.Ops.RemoveAt(i);
                    }
                }
                catch (Exception ex)
                {
                    DebugLog.Error($"[TerrainRetry] Operation threw: {ex.Message}\n{ex.StackTrace}");
                    state.Ops.RemoveAt(i);
                }
            }

            if (state.Ops.Count == 0)
            {
                DebugLog.Info("[TerrainRetry] All deferred terrain operations completed.");
                States.Remove(id);
                return false;
            }

            return true;
        }

        /// <summary>Cleanup state for a destroyed behaviour.</summary>
        internal static void Remove(int id)
        {
            States.Remove(id);
        }
    }

    /// <summary>
    /// Attached to a building root to retry terrain operations that failed
    /// because terrain wasn't loaded yet (common on multiplayer clients).
    /// Polls until <see cref="Terrain.activeTerrains"/> is non-empty,
    /// then executes queued operations and removes itself.
    /// <para>
    /// This MonoBehaviour has NO <c>Func</c> or generic delegate parameters
    /// on any method, because IL2CPP ClassInjector cannot marshal them.
    /// All delegate storage lives in <see cref="TerrainRetryQueue"/>.
    /// </para>
    /// </summary>
    internal sealed class TerrainRetryBehaviour : MonoBehaviour
    {
#if IL2CPP
        private static bool _registered;
#endif

        /// <summary>
        /// Ensure IL2CPP type registration, then add or get the component on the target.
        /// </summary>
        internal static TerrainRetryBehaviour EnsureOn(GameObject target)
        {
#if IL2CPP
            if (!_registered)
            {
                ClassInjector.RegisterTypeInIl2Cpp<TerrainRetryBehaviour>();
                _registered = true;
            }
#endif
            var existing = target.GetComponent<TerrainRetryBehaviour>();
            return existing != null ? existing : target.AddComponent<TerrainRetryBehaviour>();
        }

        private void Update()
        {
            if (!TerrainRetryQueue.Tick(GetInstanceID(), gameObject.name))
            {
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            TerrainRetryQueue.Remove(GetInstanceID());
        }
    }
}
