using System;
using System.Collections.Generic;
using S1MAPI.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

#if IL2CPP
using Il2CppInterop.Runtime.Injection;
#endif

namespace S1MAPI.Building.Components
{
    /// <summary>
    /// Static operations queue for deferred client-side linking of FishNet-replicated prefabs.
    /// When a networked prefab is spawned on the server, FishNet replicates it to clients
    /// but does NOT replicate the parent hierarchy. This queue finds the replicated object
    /// on the client and parents it to the correct building transform.
    /// <para>
    /// Separated from <see cref="NetworkedPrefabLinkerBehaviour"/> so that generic delegates
    /// never appear on the ClassInjector-registered MonoBehaviour.
    /// </para>
    /// </summary>
    internal static class NetworkedPrefabLinkerQueue
    {
        private const float PollInterval = 0.5f;
        private const int MaxAttempts = 60; // 30 seconds total
        private const float PositionTolerance = 1.0f;

        private sealed class LinkRequest
        {
            public string PrefabName;
            public Vector3 ExpectedWorldPos;
            public Transform Parent;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Action<GameObject>? OnLinked;

            public LinkRequest(string prefabName, Vector3 expectedWorldPos, Transform parent,
                Vector3 localPosition, Quaternion localRotation, Action<GameObject>? onLinked)
            {
                PrefabName = prefabName;
                ExpectedWorldPos = expectedWorldPos;
                Parent = parent;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                OnLinked = onLinked;
            }
        }

        private sealed class LinkerState
        {
            public readonly List<LinkRequest> Requests = new List<LinkRequest>();
            public int Attempts;
            public float NextPollTime;
        }

        private static readonly Dictionary<int, LinkerState> States = new Dictionary<int, LinkerState>();

        /// <summary>
        /// Queue a link request. Immediately scans scene root objects for an already-replicated
        /// match (handles the case where FishNet delivered the object before this call).
        /// If not found, a <see cref="NetworkedPrefabLinkerBehaviour"/> is attached to the
        /// target to drive polling until the replicated object arrives.
        /// </summary>
        internal static void Enqueue(GameObject target, string prefabName, Vector3 expectedWorldPos,
            Transform parent, Vector3 localPosition, Quaternion localRotation,
            Action<GameObject>? onLinked = null)
        {
            var request = new LinkRequest(prefabName, expectedWorldPos, parent,
                localPosition, localRotation, onLinked);

            // Immediate scan — the object may already exist if FishNet replicated before this call
            if (TryMatchAndLink(request, 0))
            {
                DebugLog.Info($"[PrefabLinker] Linked '{prefabName}' to '{parent.name}' " +
                              "immediately (already replicated).");
                return;
            }

            // Not found yet — attach behaviour and poll
            var behaviour = NetworkedPrefabLinkerBehaviour.EnsureOn(target);
            int id = behaviour.GetInstanceID();

            if (!States.TryGetValue(id, out var state))
            {
                state = new LinkerState { NextPollTime = Time.unscaledTime + PollInterval };
                States[id] = state;
            }

            state.Requests.Add(request);

            DebugLog.Info($"[PrefabLinker] Queued link for '{prefabName}' " +
                          $"({state.Requests.Count} pending). building='{target.name}'");
        }

        /// <summary>
        /// Called by <see cref="NetworkedPrefabLinkerBehaviour.Update"/> each frame.
        /// Returns true to keep polling, false to self-destruct.
        /// </summary>
        internal static bool Tick(int id, string buildingName)
        {
            if (!States.TryGetValue(id, out var state) || state.Requests.Count == 0)
            {
                States.Remove(id);
                return false;
            }

            if (Time.unscaledTime < state.NextPollTime)
                return true;

            state.Attempts++;
            state.NextPollTime = Time.unscaledTime + PollInterval;

            if (state.Attempts > MaxAttempts)
            {
                DebugLog.Warning($"[PrefabLinker] Gave up after {MaxAttempts} attempts " +
                                 $"({state.Requests.Count} unresolved). building='{buildingName}'");
                States.Remove(id);
                return false;
            }

            for (int i = state.Requests.Count - 1; i >= 0; i--)
            {
                var req = state.Requests[i];

                if (req.Parent == null)
                {
                    state.Requests.RemoveAt(i);
                    continue;
                }

                if (TryMatchAndLink(req, state.Attempts))
                {
                    state.Requests.RemoveAt(i);
                }
                else if (state.Attempts % 10 == 0)
                {
                    DebugLog.Info($"[PrefabLinker] Still waiting for '{req.PrefabName}'... " +
                                 $"attempt {state.Attempts}/{MaxAttempts}");
                }
            }

            if (state.Requests.Count == 0)
            {
                DebugLog.Info("[PrefabLinker] All pending links resolved.");
                States.Remove(id);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Scan scene root objects for an unparented object matching the request's
        /// prefab name and expected world position. If found, parent it and invoke the callback.
        /// </summary>
        private static bool TryMatchAndLink(LinkRequest req, int attempts)
        {
            GameObject[] rootObjects;
            try
            {
                rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            }
            catch (Exception)
            {
                return false;
            }

            // Two-pass matching: first try name + position (precise), then name-only (fallback).
            // FishNet may replicate objects at incorrect world positions (e.g., origin) when the
            // server sets transform on an inactive object before Spawn(). The name-only pass
            // catches these stranded objects and parents them correctly using stored local coords.
            GameObject? bestMatch = null;
            float bestDist = float.MaxValue;

            foreach (var obj in rootObjects)
            {
                if (obj == null) continue;
                if (!obj.name.Contains(req.PrefabName)) continue;

                float dist = Vector3.Distance(obj.transform.position, req.ExpectedWorldPos);

                // Precise match — take immediately
                if (dist <= PositionTolerance)
                {
                    bestMatch = obj;
                    bestDist = dist;
                    break;
                }

                // Name-only fallback — pick closest among stranded objects
                if (dist < bestDist)
                {
                    bestMatch = obj;
                    bestDist = dist;
                }
            }

            if (bestMatch != null)
            {
                if (bestDist > PositionTolerance)
                {
                    DebugLog.Info($"[PrefabLinker] Name-only match for '{bestMatch.name}' " +
                                  $"(dist={bestDist:F1}m, expected within {PositionTolerance:F1}m). " +
                                  $"FishNet likely spawned at wrong position.");
                }

                bestMatch.transform.SetParent(req.Parent);
                bestMatch.transform.localPosition = req.LocalPosition;
                bestMatch.transform.localRotation = req.LocalRotation;

                if (attempts > 0)
                {
                    DebugLog.Info($"[PrefabLinker] Linked '{bestMatch.name}' to '{req.Parent.name}' " +
                                 $"after {attempts} poll(s).");
                }

                try
                {
                    req.OnLinked?.Invoke(bestMatch);
                }
                catch (Exception ex)
                {
                    DebugLog.Error($"[PrefabLinker] OnLinked callback threw: {ex.Message}");
                }

                return true;
            }

            return false;
        }

        /// <summary>Cleanup state for a destroyed behaviour.</summary>
        internal static void Remove(int id)
        {
            States.Remove(id);
        }
    }

    /// <summary>
    /// Attached to a building root on clients to poll for FishNet-replicated prefabs
    /// and parent them into the building hierarchy once they arrive.
    /// <para>
    /// This MonoBehaviour has NO generic delegate parameters — all delegate storage
    /// lives in <see cref="NetworkedPrefabLinkerQueue"/>.
    /// </para>
    /// </summary>
    internal sealed class NetworkedPrefabLinkerBehaviour : MonoBehaviour
    {
#if IL2CPP
        private static bool _registered;
#endif

        internal static NetworkedPrefabLinkerBehaviour EnsureOn(GameObject target)
        {
#if IL2CPP
            if (!_registered)
            {
                ClassInjector.RegisterTypeInIl2Cpp<NetworkedPrefabLinkerBehaviour>();
                _registered = true;
            }
#endif
            var existing = target.GetComponent<NetworkedPrefabLinkerBehaviour>();
            return existing != null ? existing : target.AddComponent<NetworkedPrefabLinkerBehaviour>();
        }

        private void Update()
        {
            if (!NetworkedPrefabLinkerQueue.Tick(GetInstanceID(), gameObject.name))
            {
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            NetworkedPrefabLinkerQueue.Remove(GetInstanceID());
        }
    }
}
