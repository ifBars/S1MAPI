using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using S1MAPI.Utils;
using UnityEngine;
using UnityEngine.AI;

#if IL2CPP
using Il2CppInterop.Runtime;
#endif

namespace S1MAPI.Building
{
    /// <summary>
    /// Core logic for NPC interior navigation using custom A* pathfinding.
    /// <para>
    /// This is a plain C# class (not a MonoBehaviour) to avoid IL2CPP ClassInjector
    /// warnings for methods with custom parameter types. The thin
    /// <see cref="InteriorNavigator"/> MonoBehaviour shell forwards Unity lifecycle
    /// calls (<c>Update</c>, <c>OnDestroy</c>) to this class.
    /// </para>
    /// </summary>
    internal sealed class InteriorNavigatorCore
    {
        #region Nested Types

        private enum NPCNavState
        {
            Approaching,     // NavMeshAgent walking to doorway exterior
            Entering,        // Agent disabled, lerping through doorway inward
            Inside,          // Following A* path via Transform movement
            Exiting,         // A* path to doorway interior point
            LeavingDoorway   // Lerping through doorway outward
        }

        private sealed class TrackedNPC
        {
            public Component NpcComponent;
            public NPCNavState State;
            public NavDoorwayInfo TargetDoorway;
            public Vector3 DoorwayExteriorWorld;   // stair base (ground level) — NavMeshAgent target
            public Vector3 DoorwayThresholdWorld;  // just outside doorway at floor level — lerp phase 1 target
            public Vector3 DoorwayInteriorWorld;
            public List<Vector3>? Path;
            public int PathIndex;
            public Vector3 TargetLocal;
            public Transform? ChaseTarget;
            public float ChaseRepathTimer;
            public float Speed;
            public Action? OnArrival;
            public float LerpProgress;
            public float LerpDuration;
            public Vector3 LerpStart;
            public Vector3 LerpEnd;
            public bool Arrived;           // directed mode: true once OnArrival has fired
            public Vector3? PendingExteriorDestination; // set when NPC exits to resume navigation
            public Vector3? SavedChasePosition;       // chase target position saved before exit
            public float RepathTimer;     // fallback re-pathfind timer for all NPCs
            public float StuckTimer;      // time NPC hasn't moved
            public Vector3 StuckStartPos; // position when stuck timer began
            public Vector3 LastChaseTargetLocal; // last chase target used for repath (avoids redundant recompute)
            public float ApproachStartTime; // Time.time when Approaching state began
            public bool IsStairLerp;       // true during stair climb/descent lerp (Y follows ramp slope)
            public Vector3 LastValidPos;   // last position set by our code (restored if agent warps NPC)

            // Cached reflection results
            public object? MovementRef;    // NPCMovement instance
            public NavMeshAgent? Agent;
            public Collider? NpcCollider;  // disabled while inside to prevent pushing player
        }

        /// <summary>
        /// Cross-platform member accessor for game types.
        /// On Mono, game members are fields; on IL2CPP (Il2CppInterop), they become properties.
        /// Tries property first, then falls back to field — works on both runtimes.
        /// </summary>
        private readonly struct MemberAccessor
        {
            private readonly FieldInfo? _field;
            private readonly PropertyInfo? _prop;

            public bool IsValid =>
                _field != null || _prop != null;

            public MemberAccessor(Type type, string name, BindingFlags flags)
            {
                _prop = type.GetProperty(name, flags);
                _field = _prop == null ? type.GetField(name, flags) : null;
            }

            public object? GetValue(object target) =>
                _field != null ? _field.GetValue(target) : _prop?.GetValue(target);

            public void SetValue(object target, object? value)
            {
                if (_field != null) _field.SetValue(target, value);
                else _prop?.SetValue(target, value);
            }
        }

        #endregion

        #region Fields

        private readonly InteriorPathGrid _grid;
        /// <summary>Exposes the walkability grid to NavigationBuilder pass-through queries.</summary>
        internal InteriorPathGrid PathGrid => _grid;
        private readonly IReadOnlyList<NavDoorwayInfo> _doorways;
        private readonly Transform _buildingRoot;
        private readonly Vector3 _roomSize;
        private readonly float _foundationHeight;

        private readonly Dictionary<Component, TrackedNPC> _tracked =
            new Dictionary<Component, TrackedNPC>();
        private readonly List<Component> _removeQueue = new List<Component>();
        private float _doorwayScanTimer;
        private float _approachLogTimer;
        private static readonly Collider[] _scanBuffer = new Collider[32];

        // Prevent NPC from being managed by two buildings simultaneously
        private static readonly HashSet<Component> _globallyManaged = new HashSet<Component>();

        // Static registry of all active buildings (for auto-detection patch)
        private static readonly List<InteriorNavigatorCore> _activeBuildings = new List<InteriorNavigatorCore>();
        private static bool _patchApplied;

        // Reflection cache (resolved once)
        private static bool _reflectionResolved;
        private static Type? _npcMovementType;
        private static MemberAccessor _agentAccessor;
        private static MethodInfo? _setAgentEnabled;
        private static MemberAccessor _walkSpeedAccessor;
        private static MemberAccessor _runSpeedAccessor;
        private static MemberAccessor _speedScaleAccessor;
        private static MemberAccessor _moveSpeedMultAccessor;
        private static MemberAccessor _hasDestinationAccessor;
        private static MethodInfo? _originalSetDestination;
        private static Harmony? _harmony;

        // Chase detection: NPCMovement.npc → NPC.Behaviour → activeBehaviour, check if CombatBehaviour
        private static MemberAccessor _movementNpcAccessor;        // NPCMovement.npc (protected field)
        private static MemberAccessor _npcBehaviourAccessor;       // NPC.Behaviour
        private static MemberAccessor _activeBehaviourAccessor;    // NPCBehaviour.activeBehaviour
        private static Type? _combatBehaviourType;

        #endregion

        #region Constructor

        /// <summary>
        /// Create and initialize the interior navigation core.
        /// </summary>
        public InteriorNavigatorCore(
            InteriorPathGrid grid,
            IReadOnlyList<NavDoorwayInfo> doorways,
            Transform buildingRoot,
            Vector3 roomSize,
            float foundationHeight)
        {
            _grid = grid;
            _doorways = doorways;
            _buildingRoot = buildingRoot;
            _roomSize = roomSize;
            _foundationHeight = foundationHeight;

            ResolveReflection();

            _activeBuildings.Add(this);
            ApplyPatchIfNeeded();
        }

        #endregion

        #region Reflection & Patching

        private static void ResolveReflection()
        {
            if (_reflectionResolved) return;
            _reflectionResolved = true;

            // On IL2CPP, Il2CppInterop prefixes game namespaces with "Il2Cpp"
#if IL2CPP
            const string typeName = "Il2CppScheduleOne.NPCs.NPCMovement";
#else
            const string typeName = "ScheduleOne.NPCs.NPCMovement";
#endif
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                _npcMovementType = asm.GetType(typeName);
                if (_npcMovementType != null) break;
            }
            if (_npcMovementType == null)
            {
                DebugLog.Warning("[InteriorNavigator] NPCMovement type not found.");
                return;
            }

            // MemberAccessor tries GetProperty first (IL2CPP), falls back to GetField (Mono)
            const BindingFlags pub = BindingFlags.Public | BindingFlags.Instance;
            _agentAccessor = new MemberAccessor(_npcMovementType, "Agent", pub);
            _setAgentEnabled = _npcMovementType.GetMethod("SetAgentEnabled", pub);
            _walkSpeedAccessor = new MemberAccessor(_npcMovementType, "WalkSpeed", pub);
            _runSpeedAccessor = new MemberAccessor(_npcMovementType, "RunSpeed", pub);
            _speedScaleAccessor = new MemberAccessor(_npcMovementType, "MovementSpeedScale", pub);
            _moveSpeedMultAccessor = new MemberAccessor(_npcMovementType, "MoveSpeedMultiplier", pub);
            _hasDestinationAccessor = new MemberAccessor(_npcMovementType, "HasDestination", pub);

            // Chase detection: NPCMovement.npc → NPC.Behaviour → activeBehaviour → CombatBehaviour
            const BindingFlags nonPub = BindingFlags.NonPublic | BindingFlags.Instance;
            _movementNpcAccessor = new MemberAccessor(_npcMovementType, "npc", nonPub | BindingFlags.Public);
#if IL2CPP
            const string npcTypeName = "Il2CppScheduleOne.NPCs.NPC";
            const string behaviourTypeName = "Il2CppScheduleOne.NPCs.Behaviour.NPCBehaviour";
            const string combatTypeName = "Il2CppScheduleOne.Combat.CombatBehaviour";
#else
            const string npcTypeName = "ScheduleOne.NPCs.NPC";
            const string behaviourTypeName = "ScheduleOne.NPCs.Behaviour.NPCBehaviour";
            const string combatTypeName = "ScheduleOne.Combat.CombatBehaviour";
#endif
            Type? npcType = null;
            Type? npcBehaviourType = null;
            foreach (Assembly asm2 in AppDomain.CurrentDomain.GetAssemblies())
            {
                npcType ??= asm2.GetType(npcTypeName);
                npcBehaviourType ??= asm2.GetType(behaviourTypeName);
                _combatBehaviourType ??= asm2.GetType(combatTypeName);
                if (npcType != null && npcBehaviourType != null && _combatBehaviourType != null)
                    break;
            }
            if (npcType != null)
                _npcBehaviourAccessor = new MemberAccessor(npcType, "Behaviour", pub);
            if (npcBehaviourType != null)
                _activeBehaviourAccessor = new MemberAccessor(npcBehaviourType, "activeBehaviour", pub);
        }

        private static void ApplyPatchIfNeeded()
        {
            if (_patchApplied || _npcMovementType == null) return;
            _patchApplied = true;

            // Find the 4-param public SetDestination(Vector3, Action<WalkResult>, float, float)
            MethodInfo? target = null;
            foreach (MethodInfo m in _npcMovementType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.Name != "SetDestination") continue;
                ParameterInfo[] pars = m.GetParameters();
                if (pars.Length == 4 && pars[0].ParameterType == typeof(Vector3))
                {
                    target = m;
                    break;
                }
            }

            if (target == null)
            {
                DebugLog.Warning("[InteriorNavigator] Could not find SetDestination to patch.");
                return;
            }

            _originalSetDestination = target;

            _harmony = new Harmony("com.s1mapi.interiornavigator");

            // Patch SetDestination — intercept all NPC destination calls
            MethodInfo setDestPrefix = typeof(InteriorNavigatorCore).GetMethod(
                nameof(SetDestinationPrefix),
                BindingFlags.NonPublic | BindingFlags.Static)!;
            _harmony.Patch(target, prefix: new HarmonyMethod(setDestPrefix));

            // Patch UpdateDestination — prevent private 5-param SetDestination from
            // overwriting our agent destination for managed NPCs.
            // UpdateDestination is called from FixedUpdate and bypasses our SetDestination patch.
            MethodInfo? updateDest = _npcMovementType.GetMethod("UpdateDestination",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (updateDest != null)
            {
                MethodInfo updateDestPrefix = typeof(InteriorNavigatorCore).GetMethod(
                    nameof(UpdateDestinationPrefix),
                    BindingFlags.NonPublic | BindingFlags.Static)!;
                _harmony.Patch(updateDest, prefix: new HarmonyMethod(updateDestPrefix));
                DebugLog.Info("[InteriorNavigator] Patched UpdateDestination.");
            }
            else
            {
                DebugLog.Warning("[InteriorNavigator] Could not find UpdateDestination to patch.");
            }

            DebugLog.Info("[InteriorNavigator] Patched NPCMovement.SetDestination for auto-interception.");
        }

        #endregion

        #region Harmony Patch

        /// <summary>
        /// Harmony prefix on <c>NPCMovement.SetDestination</c>. Automatically intercepts
        /// NPCs whose destination falls inside a registered building and redirects them
        /// through the custom A* interior pathfinding system.
        /// <para>
        /// Always blocks the original for managed NPCs — we send the agent to the
        /// doorway ourselves. The UpdateDestination patch prevents the game's FixedUpdate
        /// from overwriting our agent destination.
        /// </para>
        /// </summary>
        private static bool SetDestinationPrefix(object __instance, Vector3 pos)
        {
            Component? movement = __instance as Component;
            if (movement == null) return true;

            // Check if destination is inside any registered building (0.5m margin prevents
            // oscillation when player is near building wall — slight position offsets won't
            // cause immediate exit/re-entry cycles)
            for (int i = 0; i < _activeBuildings.Count; i++)
            {
                InteriorNavigatorCore nav = _activeBuildings[i];
                if (nav == null || nav._buildingRoot == null) continue;

                Vector3 localPos = nav._buildingRoot.InverseTransformPoint(pos);
                if (!nav.IsInsideBuilding(localPos))
                {
                    // Extended: combat AI resolves targets to carving boundary points
                    // (~0.8m outside). Only intercept if the NPC is in combat — prevents
                    // false interception of consumer NPCs walking to exterior positions
                    // near the building (e.g. stair approach points).
                    if (!IsNPCInCombat(movement) ||
                        !nav.IsInsideBuilding(localPos, margin: 3f) ||
                        !nav.IsPlayerInside())
                        continue;
                }

                // Clamp near-boundary positions to interior
                localPos.x = Mathf.Clamp(localPos.x, 0f, nav._roomSize.x);
                localPos.z = Mathf.Clamp(localPos.z, 0f, nav._roomSize.z);

                // Chase detection: only enable continuous player tracking when the
                // NPC's active behaviour is combat (PursuitBehaviour, CombatBehaviour, etc.).
                // This avoids false positives from consumer mods sending NPCs to positions
                // near the player (e.g. consumer NPCs walking to interior positions).
                Transform? chaseTarget = IsNPCInCombat(movement) ? DetectChaseTarget(pos) : null;

                // Already tracked by this building — update target, always block original
                if (nav._tracked.TryGetValue(movement, out TrackedNPC? existing))
                {
                    existing.TargetLocal = localPos;
                    existing.OnArrival = null;
                    if (chaseTarget != null)
                    {
                        existing.ChaseTarget = chaseTarget;
                        existing.LastChaseTargetLocal = localPos;
                    }
                    else
                    {
                        existing.ChaseTarget = null;
                    }

                    if (existing.State == NPCNavState.Inside)
                        nav.ComputePathToTarget(existing);

                    // Re-send agent to doorway if still approaching — the agent's path
                    // may have been consumed or cleared since the initial send.
                    if (existing.State == NPCNavState.Approaching &&
                        existing.Agent != null &&
                        existing.Agent.enabled &&
                        existing.Agent.isOnNavMesh)
                    {
                        existing.Agent.SetDestination(existing.DoorwayExteriorWorld);
                    }

                    return false;
                }

                // New NPC — register and send agent to doorway ourselves
                DebugLog.Info($"[InteriorNavigator] Tracking new NPC {movement.name} " +
                             $"targeting local ({localPos.x:F1}, {localPos.z:F1})");

                TrackedNPC tracked = nav.CreateTrackedNPC(movement);
                tracked.TargetLocal = localPos;
                tracked.ChaseTarget = chaseTarget;
                tracked.ChaseRepathTimer = 0f;
                tracked.OnArrival = null;

                NavDoorwayInfo door = nav.FindNearestExteriorDoorway(
                    nav._buildingRoot.InverseTransformPoint(movement.transform.position));
                tracked.TargetDoorway = door;
                nav.ComputeDoorwayPoints(tracked, door);
                tracked.State = NPCNavState.Approaching;
                tracked.ApproachStartTime = Time.time;

                // Send agent to doorway exterior directly — don't let the game send it
                // to the interior position (which the agent can't reach due to carving)
                if (tracked.Agent != null && tracked.Agent.enabled && tracked.Agent.isOnNavMesh)
                    tracked.Agent.SetDestination(tracked.DoorwayExteriorWorld);

                nav._tracked[movement] = tracked;
                _globallyManaged.Add(movement);

                return false; // block original — we've set the agent destination ourselves
            }

            // Destination not inside any building — check if this NPC is tracked and should exit
            if (_globallyManaged.Contains(movement))
            {
                for (int i = 0; i < _activeBuildings.Count; i++)
                {
                    InteriorNavigatorCore nav = _activeBuildings[i];
                    if (nav == null) continue;

                    if (nav._tracked.TryGetValue(movement, out TrackedNPC? data))
                    {
                        // During Approaching: only release if destination is clearly
                        // far from the building. Near-building destinations (carving
                        // boundary points from GetRandomReachablePointNear) should
                        // NOT release — the NPC is still pursuing a target inside.
                        if (data.State == NPCNavState.Approaching)
                        {
                            Vector3 destLocal = nav._buildingRoot.InverseTransformPoint(pos);
                            if (!nav.IsInsideBuilding(destLocal, margin: 5f))
                            {
                                _globallyManaged.Remove(movement);
                                nav._tracked.Remove(movement);
                                return true; // genuinely far — let original run
                            }
                            return false; // near building — keep tracking, block game
                        }

                        // Destination is outside building — recall NPC to exit
                        data.PendingExteriorDestination = pos;
                        nav.RecallNPC(movement);
                        return false; // suppress original while NPC exits the building
                    }
                }
            }

            return true; // not our concern, run original
        }

        /// <summary>
        /// Harmony prefix on <c>NPCMovement.UpdateDestination</c>. Skips the method
        /// entirely for managed NPCs — prevents the private 5-param SetDestination
        /// (called from FixedUpdate) from overwriting our agent destination.
        /// </summary>
        private static bool UpdateDestinationPrefix(object __instance)
        {
            Component? movement = __instance as Component;
            if (movement != null && _globallyManaged.Contains(movement))
                return false; // skip — we're managing this NPC's destination
            return true;
        }

        /// <summary>
        /// Check if the NPC's active behaviour is a CombatBehaviour (or subclass like PursuitBehaviour).
        /// Uses reflection to read NPCMovement.npc → NPC.Behaviour.activeBehaviour without
        /// compile-time ScheduleOne dependencies. Returns false if reflection fails (graceful fallback: no chase).
        /// </summary>
        private static bool IsNPCInCombat(Component movement)
        {
            if (_combatBehaviourType == null || !_movementNpcAccessor.IsValid ||
                !_npcBehaviourAccessor.IsValid || !_activeBehaviourAccessor.IsValid)
                return false;

            try
            {
                object? npc = _movementNpcAccessor.GetValue(movement);
                if (npc == null) return false;

                object? behaviourComp = _npcBehaviourAccessor.GetValue(npc);
                if (behaviourComp == null) return false;

                object? activeBehaviour = _activeBehaviourAccessor.GetValue(behaviourComp);
                if (activeBehaviour == null) return false;

                return _combatBehaviourType.IsInstanceOfType(activeBehaviour);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if the destination is near any player (indicating a chase scenario).
        /// Returns the nearest player's Transform if so, null otherwise.
        /// Uses <see cref="Camera.allCameras"/> to support multiplayer.
        /// </summary>
        private static Transform? DetectChaseTarget(Vector3 destination)
        {
            foreach (Camera cam in Camera.allCameras)
            {
                Vector3 camPos = cam.transform.position;
                float dx = destination.x - camPos.x;
                float dz = destination.z - camPos.z;
                float distSq = dx * dx + dz * dz;

                // If destination is within 3m of a player, this is a chase
                if (distSq < 9f)
                    return cam.transform;
            }

            return null;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Send an NPC to a position inside the building. The NPC walks to the nearest
        /// doorway on exterior NavMesh, enters via lerp, then follows A* path to target.
        /// </summary>
        public void SendNPCToPosition(Component npc, Vector3 localTarget, Action? onArrival)
        {
            // Already tracked by this building — update target in place
            if (_tracked.TryGetValue(npc, out TrackedNPC? existing))
            {
                existing.TargetLocal = localTarget;
                existing.OnArrival = onArrival;
                existing.Arrived = false;
                existing.ChaseTarget = null;
                if (existing.State == NPCNavState.Inside)
                    ComputePathToTarget(existing);
                return;
            }

            if (_globallyManaged.Contains(npc))
            {
                DebugLog.Warning("[InteriorNavigator] NPC is already managed by another building.");
                return;
            }

            TrackedNPC tracked = CreateTrackedNPC(npc);
            tracked.TargetLocal = localTarget;
            tracked.OnArrival = onArrival;
            tracked.ChaseTarget = null;

            // Check if NPC is already near a doorway — skip approach if close enough.
            // For stair doorways, check proximity to the stair base (not door center)
            // so the NPC doesn't skip the stair climb from the sidewalk.
            Vector3 npcPos = npc.transform.position;
            NavDoorwayInfo? nearDoor = null;
            for (int i = 0; i < _doorways.Count; i++)
            {
                NavDoorwayInfo door = _doorways[i];
                if (door.IsInterior) continue;

                Vector3 checkPoint;
                float checkThreshold;
                if (door.StairBasePosition.HasValue)
                {
                    checkPoint = _buildingRoot.TransformPoint(door.StairBasePosition.Value);
                    checkThreshold = Constants.InteriorNav.StairApproachThreshold;
                }
                else
                {
                    checkPoint = _buildingRoot.TransformPoint(door.Center);
                    checkThreshold = Constants.InteriorNav.DoorwayApproachThreshold;
                }

                if (HorizontalDistance(npcPos, checkPoint) < checkThreshold)
                {
                    nearDoor = door;
                    break;
                }
            }

            _tracked[npc] = tracked;
            _globallyManaged.Add(npc);

            if (nearDoor != null)
            {
                tracked.TargetDoorway = nearDoor;
                ComputeDoorwayPoints(tracked, nearDoor);
                BeginDoorwayEntry(npc, tracked);
            }
            else
            {
                BeginApproach(tracked);
                SendAgentToDoorway(tracked);
            }

            DebugLog.Info($"[InteriorNavigator] Sending NPC to local ({localTarget.x:F1}, {localTarget.z:F1})");
        }

        /// <summary>
        /// Send an NPC to chase a moving target inside the building.
        /// Re-pathfinds at <see cref="Constants.InteriorNav.ChaseRepathInterval"/>.
        /// </summary>
        public void SendNPCToChase(Component npc, Transform target)
        {
            if (_globallyManaged.Contains(npc))
            {
                DebugLog.Warning("[InteriorNavigator] NPC is already managed by a building.");
                return;
            }

            SendNPCToChaseInternal(npc, target);
        }

        private void SendNPCToChaseInternal(Component npc, Transform target)
        {
            Vector3 targetLocal = _buildingRoot.InverseTransformPoint(target.position);

            TrackedNPC tracked = CreateTrackedNPC(npc);
            tracked.TargetLocal = targetLocal;
            tracked.ChaseTarget = target;
            tracked.ChaseRepathTimer = 0f;
            tracked.OnArrival = null;

            BeginApproach(tracked);
            SendAgentToDoorway(tracked);
            _tracked[npc] = tracked;
            _globallyManaged.Add(npc);

            DebugLog.Info("[InteriorNavigator] NPC chasing player — continuous tracking enabled.");
        }

        /// <summary>
        /// Directly set the NavMeshAgent destination to the doorway exterior.
        /// Used by the consumer API (SendNPCToPosition/SendNPCToChase) where the
        /// game's behavior system isn't involved. The Harmony prefix path doesn't
        /// need this — the game's own SetDestination handles agent routing.
        /// </summary>
        private static void SendAgentToDoorway(TrackedNPC data)
        {
            if (data.Agent != null && data.Agent.enabled && data.Agent.isOnNavMesh)
                data.Agent.SetDestination(data.DoorwayExteriorWorld);
        }

        /// <summary>
        /// Recall an NPC from the building. If inside, begins exit via A* path to doorway.
        /// If still approaching, releases immediately.
        /// </summary>
        public void RecallNPC(Component npc)
        {
            if (!_tracked.TryGetValue(npc, out TrackedNPC? data)) return;

            if (data.State == NPCNavState.Approaching)
            {
                // Still on exterior NavMesh — just release
                ReleaseNPC(npc, data, warpToExterior: false);
            }
            else if (data.State != NPCNavState.Exiting &&
                     data.State != NPCNavState.LeavingDoorway)
            {
                BeginExit(npc, data);
            }
        }

        /// <summary>Whether this navigator is currently managing the given NPC.</summary>
        public bool IsTracking(Component npc) =>
            _tracked.ContainsKey(npc);

        /// <summary>
        /// Show/hide the walkability grid visualization (green = walkable, red = blocked).
        /// </summary>
        public void VisualizeGrid(bool show)
        {
            if (show)
                _grid.Visualize();
            else
                _grid.DestroyVisualization();
        }

        /// <summary>
        /// Diagnose why a specific grid cell is blocked. Logs details to console.
        /// Cell coordinates are shown in the visualization quad names (Cell_X_Z).
        /// </summary>
        public void DiagnoseCell(int gx, int gz) =>
            _grid.DiagnoseCell(gx, gz);

        /// <summary>
        /// Release all tracked NPCs. Warps those inside to the exterior and re-enables agents.
        /// Called automatically on building destruction.
        /// </summary>
        public void ReleaseAllNPCs()
        {
            foreach (KeyValuePair<Component, TrackedNPC> kvp in _tracked)
            {
                if (kvp.Key == null) continue;
                TrackedNPC data = kvp.Value;

                if (data.State != NPCNavState.Approaching)
                {
                    // NPC is inside — warp to exterior
                    if (data.DoorwayExteriorWorld != Vector3.zero)
                        kvp.Key.transform.position = data.DoorwayExteriorWorld;
                    if (data.NpcCollider != null)
                        data.NpcCollider.enabled = true;
                    EnableAgent(data);
                }

                // Restore HasDestination so the game's pursuit AI resumes generating
                // SetDestination calls (mirrors ReleaseNPC logic)
                if (data.MovementRef != null && _hasDestinationAccessor.IsValid)
                {
                    try { _hasDestinationAccessor.SetValue(data.MovementRef, true); }
                    catch (Exception ex) { DebugLog.Warning($"[InteriorNavigator] RestoreHasDestination failed: {ex.Message}"); }
                }

                _globallyManaged.Remove(kvp.Key);
            }
            _tracked.Clear();
        }

        #endregion

        #region Update Loop

        public void Update()
        {
            // Periodically scan for untracked NPCs near doorways.
            // Catches NPCs whose SetDestination resolved to a NavMesh point outside
            // the building (carving boundary) — the prefix can't intercept those.
            _doorwayScanTimer -= Time.deltaTime;
            if (_doorwayScanTimer <= 0f)
            {
                _doorwayScanTimer = 0.5f;
                ScanForNearbyNPCs();
            }

            if (_tracked.Count == 0) return;

            _removeQueue.Clear();

            foreach (KeyValuePair<Component, TrackedNPC> kvp in _tracked)
            {
                Component npc = kvp.Key;
                TrackedNPC data = kvp.Value;

                if (npc == null)
                {
                    _removeQueue.Add(npc!);
                    continue;
                }

                // The game may re-enable the NavMeshAgent at any time (e.g. NPC.SetVisible
                // toggles Agent.enabled). A re-enabled agent on carved NavMesh snaps the NPC
                // to ground level, overriding our position control. Disable it and restore
                // the last known good position to undo the warp.
                if (data.State != NPCNavState.Approaching && data.Agent != null && data.Agent.enabled)
                {
                    data.Agent.enabled = false;
                    npc.transform.position = data.LastValidPos;
                }

                switch (data.State)
                {
                    case NPCNavState.Approaching:
                        UpdateApproaching(npc, data);
                        break;
                    case NPCNavState.Entering:
                        UpdateLerp(npc, data, onComplete: () =>
                        {
                            // Check if we still need phase 2 (through doorway)
                            float distToInterior = Vector3.Distance(
                                npc.transform.position, data.DoorwayInteriorWorld);
                            if (distToInterior > Constants.InteriorNav.PhaseTransitionThreshold)
                            {
                                // Phase 1 complete — now lerp through the doorway
                                data.IsStairLerp = false;
                                data.Speed = GetNPCSpeed(data);
                                data.LerpStart = npc.transform.position;
                                data.LerpEnd = data.DoorwayInteriorWorld;
                                float dist = Vector3.Distance(data.LerpStart, data.LerpEnd);
                                data.LerpDuration = Mathf.Max(
                                    dist / Mathf.Max(data.Speed, 1f), 0.1f);
                                data.LerpProgress = 0f;
                            }
                            else
                            {
                                data.State = NPCNavState.Inside;
                                ComputePathToTarget(data);
                            }
                        });
                        break;
                    case NPCNavState.Inside:
                        UpdateInside(npc, data);
                        break;
                    case NPCNavState.Exiting:
                    {
                        // Stuck detection for exit — force doorway leave if NPC
                        // hasn't made progress in 3 seconds
                        Vector3 exitPos = npc.transform.position;
                        if (data.StuckTimer == 0f)
                            data.StuckStartPos = exitPos;
                        data.StuckTimer += Time.deltaTime;
                        float exitDisplSq = (exitPos.x - data.StuckStartPos.x) * (exitPos.x - data.StuckStartPos.x) +
                                            (exitPos.z - data.StuckStartPos.z) * (exitPos.z - data.StuckStartPos.z);
                        if (exitDisplSq > Constants.InteriorNav.ExitStuckDisplacementSq)
                            data.StuckTimer = 0f;
                        else if (data.StuckTimer > Constants.InteriorNav.ExitStuckTimeout)
                        {
                            DebugLog.Warning("[InteriorNavigator] NPC stuck during exit, forcing doorway leave.");
                            BeginDoorwayLeave(npc, data);
                            break;
                        }

                        UpdatePathFollow(npc, data, onComplete: () =>
                            BeginDoorwayLeave(npc, data));
                        break;
                    }
                    case NPCNavState.LeavingDoorway:
                        UpdateLerp(npc, data, onComplete: () =>
                        {
                            // If not yet at stair base, descend stairs
                            float distToExt = Vector3.Distance(
                                npc.transform.position, data.DoorwayExteriorWorld);
                            if (distToExt > Constants.InteriorNav.PhaseTransitionThreshold)
                            {
                                data.IsStairLerp = data.TargetDoorway.StairBasePosition.HasValue;
                                data.Speed = GetNPCSpeed(data);
                                data.LerpStart = npc.transform.position;
                                data.LerpEnd = data.DoorwayExteriorWorld;
                                float dist = Vector3.Distance(data.LerpStart, data.LerpEnd);
                                data.LerpDuration = Mathf.Max(
                                    dist / Mathf.Max(data.Speed, 1f), 0.1f);
                                data.LerpProgress = 0f;
                            }
                            else
                            {
                                ReleaseNPC(npc, data, warpToExterior: false);
                            }
                        });
                        break;
                }

                // Save position after our code sets it, so we can restore if the
                // game's NavMeshAgent warps the NPC next frame.
                if (data.State != NPCNavState.Approaching)
                    data.LastValidPos = npc.transform.position;
            }

            foreach (Component npc in _removeQueue)
            {
                _globallyManaged.Remove(npc);
                _tracked.Remove(npc);
            }
        }

        #endregion

        #region Doorway Proximity Scan

        /// <summary>
        /// Detect untracked NPCs standing near doorways while the player is inside.
        /// The game's combat AI resolves destinations to NavMesh carving boundary points
        /// (outside the building), so the SetDestination prefix may not intercept them.
        /// This scan catches those NPCs by proximity instead.
        /// </summary>
        private void ScanForNearbyNPCs()
        {
            if (_npcMovementType == null) return;

            // Only scan if any player is inside this building (multiplayer-safe)
            Transform? insidePlayer = FindPlayerInside();
            if (insidePlayer == null) return;

            Vector3 playerLocal = _buildingRoot.InverseTransformPoint(insidePlayer.position);
            float threshold = Constants.InteriorNav.DoorwayApproachThreshold;

            for (int d = 0; d < _doorways.Count; d++)
            {
                NavDoorwayInfo door = _doorways[d];
                if (door.IsInterior) continue;

                // Scan at ground level — cop stands at terrain height, not elevated floor
                Vector3 scanCenter = door.Center;
                scanCenter.y = door.StairBasePosition.HasValue
                    ? door.StairBasePosition.Value.y
                    : -_foundationHeight;
                Vector3 doorWorld = _buildingRoot.TransformPoint(scanCenter);
                int count = Physics.OverlapSphereNonAlloc(
                    doorWorld, threshold, _scanBuffer, Physics.AllLayers,
                    QueryTriggerInteraction.Collide);

                for (int c = 0; c < count; c++)
                {
                    if (_scanBuffer[c] == null) continue;

                    Component? movement = FindComponentInParent(_scanBuffer[c], _npcMovementType);
                    if (movement == null) continue;
                    if (_globallyManaged.Contains(movement)) continue;
                    if (_tracked.ContainsKey(movement)) continue;

                    DebugLog.Info($"[InteriorNavigator] Auto-detected NPC {movement.name} near doorway, registering...");

                    TrackedNPC tracked = CreateTrackedNPC(movement);
                    tracked.TargetLocal = playerLocal;
                    tracked.ChaseTarget = insidePlayer;
                    tracked.ChaseRepathTimer = 0f;
                    tracked.OnArrival = null;
                    tracked.TargetDoorway = door;
                    ComputeDoorwayPoints(tracked, door);

                    _tracked[movement] = tracked;
                    _globallyManaged.Add(movement);

                    // NPC is already at the doorway — skip Approaching, enter immediately
                    BeginDoorwayEntry(movement, tracked);
                }
            }
        }

        #endregion

        #region State: Approaching

        private void BeginApproach(TrackedNPC data)
        {
            // Find nearest exterior doorway to the NPC's current position
            Vector3 npcLocal = _buildingRoot.InverseTransformPoint(data.NpcComponent.transform.position);
            NavDoorwayInfo door = FindNearestExteriorDoorway(npcLocal);
            data.TargetDoorway = door;
            ComputeDoorwayPoints(data, door);

            data.State = NPCNavState.Approaching;
            data.ApproachStartTime = Time.time;

            DebugLog.Info($"[InteriorNavigator] NPC approaching doorway at {data.DoorwayExteriorWorld}");
        }

        private void UpdateApproaching(Component npc, TrackedNPC data)
        {
            Vector3 npcPos = npc.transform.position;
            // Stair doorways: use a tight threshold so the NPC walks all the way
            // to the stair base before we take over and lerp up the stairs.
            float threshold = data.TargetDoorway.StairBasePosition.HasValue
                ? Constants.InteriorNav.StairApproachThreshold
                : Constants.InteriorNav.DoorwayApproachThreshold;

            // Check distance to target doorway
            float distXZ = HorizontalDistance(npcPos, data.DoorwayExteriorWorld);

            if (distXZ < threshold)
            {
                DebugLog.Info($"[InteriorNavigator] NPC reached doorway (distXZ={distXZ:F2}), entering...");
                BeginDoorwayEntry(npc, data);
                return;
            }

            // Check all exterior doorways — NPC may be closer to a different one
            // (e.g., routed around the building by NavMesh)
            for (int i = 0; i < _doorways.Count; i++)
            {
                NavDoorwayInfo door = _doorways[i];
                if (door.IsInterior || door == data.TargetDoorway) continue;

                Vector3 doorWorld = _buildingRoot.TransformPoint(door.Center);
                float altDist = HorizontalDistance(npcPos, doorWorld);
                if (altDist < threshold)
                {
                    DebugLog.Info($"[InteriorNavigator] NPC near alternate doorway (dist={altDist:F2}), switching...");
                    data.TargetDoorway = door;
                    ComputeDoorwayPoints(data, door);
                    BeginDoorwayEntry(npc, data);
                    return;
                }
            }

            // Approach timeout — if the agent has been trying for too long, force entry
            // at the nearest doorway. Uses 2-phase lerp so NPC walks smoothly to the
            // doorway exterior before entering (not a warp).
            float elapsed = Time.time - data.ApproachStartTime;
            if (elapsed > 12f)
            {
                Vector3 npcLocal = _buildingRoot.InverseTransformPoint(npcPos);
                NavDoorwayInfo nearest = FindNearestExteriorDoorway(npcLocal);
                data.TargetDoorway = nearest;
                ComputeDoorwayPoints(data, nearest);

                float nearestDist = HorizontalDistance(npcPos, data.DoorwayExteriorWorld);
                if (nearestDist < 12f) // Only force if within reasonable distance
                {
                    DebugLog.Warning($"[InteriorNavigator] Approach timeout ({elapsed:F0}s), " +
                                     $"forcing entry (dist={nearestDist:F1}m)...");
                    BeginDoorwayEntry(npc, data);
                    return;
                }
            }

            if (data.Agent == null) return;

            bool pathPending = data.Agent.pathPending;
            bool hasPath = data.Agent.hasPath;
            float remaining = data.Agent.remainingDistance;
            bool remainingValid = !float.IsInfinity(remaining) && !float.IsNaN(remaining);

            // Agent finished its path near-ish to doorway — enter
            float maxEntryDist = data.TargetDoorway.StairBasePosition.HasValue
                ? Constants.InteriorNav.StairMaxEntryDistance : 8f;
            if (!pathPending && remainingValid && remaining < 0.5f && distXZ < maxEntryDist)
            {
                DebugLog.Info($"[InteriorNavigator] Agent path done near doorway (distXZ={distXZ:F2}), entering...");
                BeginDoorwayEntry(npc, data);
                return;
            }

            // Re-send agent to doorway when it has no active path
            if (!pathPending && !hasPath && data.Agent.enabled && data.Agent.isOnNavMesh)
            {
                data.Agent.SetDestination(data.DoorwayExteriorWorld);
            }

            // Periodic diagnostic logging
            _approachLogTimer -= Time.deltaTime;
            if (_approachLogTimer <= 0f)
            {
                _approachLogTimer = 3f;
                DebugLog.Info($"[InteriorNavigator] Approach: distXZ={distXZ:F2}, " +
                             $"elapsed={elapsed:F0}s, hasPath={hasPath}, remaining={remaining:F2}");
            }
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        #endregion

        #region State: Entering / Leaving (Doorway Lerp)

        private void BeginDoorwayEntry(Component npc, TrackedNPC data)
        {
            // Snapshot position before disabling agent — this is the last known good
            // position for warp-back if the game re-enables the agent later.
            data.LastValidPos = npc.transform.position;

            // Stop the agent directly (avoid NPCMovement.Stop() which fires stale callbacks)
            if (data.Agent != null)
            {
                data.Agent.isStopped = true;
                data.Agent.ResetPath();
            }
            DisableAgent(data);

            // Disable NPC collider to prevent pushing the player while inside
            if (data.NpcCollider != null)
                data.NpcCollider.enabled = false;

            // Clear HasDestination so game's UpdateDestination() doesn't run
            // and warp the NPC back to the NavMesh surface
            ClearHasDestination(data);

            data.Speed = GetNPCSpeed(data);
            data.LerpStart = npc.transform.position;

            // Entry phasing depends on whether doorway has stairs:
            //   Stair doorways: Phase 1 climbs to DoorwayThresholdWorld (ramp top),
            //                   Phase 2 enters through doorway to DoorwayInteriorWorld.
            //   Non-stair doorways: Phase 1 walks to DoorwayExteriorWorld (corrects approach angle),
            //                       Phase 2 enters through doorway to DoorwayInteriorWorld.
            bool hasStairs = data.TargetDoorway.StairBasePosition.HasValue;
            if (hasStairs)
            {
                float distToThreshold = Vector3.Distance(npc.transform.position, data.DoorwayThresholdWorld);
                bool climbPhase = distToThreshold > Constants.InteriorNav.PhaseTransitionThreshold;
                data.LerpEnd = climbPhase
                    ? data.DoorwayThresholdWorld
                    : data.DoorwayInteriorWorld;
                data.IsStairLerp = climbPhase;
            }
            else
            {
                float distToExterior = Vector3.Distance(npc.transform.position, data.DoorwayExteriorWorld);
                data.LerpEnd = distToExterior > Constants.InteriorNav.ExteriorAngleCorrectionThreshold
                    ? data.DoorwayExteriorWorld
                    : data.DoorwayInteriorWorld;
                data.IsStairLerp = false;
            }

            float distance = Vector3.Distance(data.LerpStart, data.LerpEnd);
            data.LerpDuration = Mathf.Max(distance / Mathf.Max(data.Speed, 1f), 0.1f);
            data.LerpProgress = 0f;
            data.State = NPCNavState.Entering;
        }

        private void BeginDoorwayLeave(Component npc, TrackedNPC data)
        {
            data.Speed = GetNPCSpeed(data);
            data.LerpStart = npc.transform.position;
            // Stair doorways: lerp to threshold first, then LeavingDoorway completion
            // descends to stair base. Non-stair: go directly to exterior (single phase).
            data.LerpEnd = data.TargetDoorway.StairBasePosition.HasValue
                ? data.DoorwayThresholdWorld
                : data.DoorwayExteriorWorld;
            float distance = Vector3.Distance(data.LerpStart, data.LerpEnd);
            data.LerpDuration = Mathf.Max(distance / Mathf.Max(data.Speed, 1f), 0.1f);
            data.LerpProgress = 0f;
            data.IsStairLerp = false; // interior→threshold/exterior is through doorway, not on stairs
            data.State = NPCNavState.LeavingDoorway;
        }

        private void UpdateLerp(Component npc, TrackedNPC data, Action onComplete)
        {
            data.LerpProgress += Time.deltaTime / data.LerpDuration;
            float t = Mathf.Clamp01(data.LerpProgress);
            float smooth = t * t * (3f - 2f * t); // smoothstep
            Vector3 pos = Vector3.Lerp(data.LerpStart, data.LerpEnd, smooth);

            // During stair climb/descent, project Y linearly based on horizontal
            // progress from LerpStart to LerpEnd. This avoids the smoothstep arc
            // that causes floating, and handles street-to-stair elevation differences
            // since LerpStart.y is the NPC's actual Y (may be at street level).
            if (data.IsStairLerp)
            {
                float dx = data.LerpEnd.x - data.LerpStart.x;
                float dz = data.LerpEnd.z - data.LerpStart.z;
                float lenSq = dx * dx + dz * dz;
                if (lenSq > 0.001f)
                {
                    float dot = (pos.x - data.LerpStart.x) * dx + (pos.z - data.LerpStart.z) * dz;
                    float rampT = Mathf.Clamp01(dot / lenSq);
                    pos.y = Mathf.Lerp(data.LerpStart.y, data.LerpEnd.y, rampT);
                }
            }

            npc.transform.position = pos;

            // Face movement direction
            RotateToward(npc.transform, data.LerpEnd - data.LerpStart);

            if (t >= 1f)
            {
                npc.transform.position = data.LerpEnd;
                onComplete();
            }
        }

        #endregion

        #region State: Inside

        private void UpdateInside(Component npc, TrackedNPC data)
        {
            Vector3 pos = npc.transform.position;

            // Chase mode: periodically re-pathfind toward moving target.
            // ReferenceEquals bypasses Unity's overloaded == to test "was a target assigned?"
            // Then Unity's == detects destroyed objects (native pointer gone, proxy alive).
            if (!ReferenceEquals(data.ChaseTarget, null))
            {
                if (data.ChaseTarget == null)
                {
                    // Target was assigned but Unity object was destroyed — exit building
                    BeginExit(npc, data);
                    return;
                }

                Vector3 targetLocal = _buildingRoot.InverseTransformPoint(data.ChaseTarget.position);
                if (!IsInsideBuilding(targetLocal))
                {
                    BeginExit(npc, data);
                    return;
                }

                // Stopping distance: don't move when already close enough to target.
                // Prevents oscillation from overshooting + re-pathing.
                float distToTarget = Vector3.Distance(pos, data.ChaseTarget.position);
                if (distToTarget < Constants.InteriorNav.DestinationArrivalThreshold)
                {
                    // Close enough — just face the target and idle
                    RotateToward(npc.transform, data.ChaseTarget.position - pos);
                    data.Path = null;
                    data.ChaseRepathTimer = Constants.InteriorNav.ChaseRepathInterval;
                    return;
                }

                data.ChaseRepathTimer -= Time.deltaTime;
                if (data.ChaseRepathTimer <= 0f)
                {
                    data.ChaseRepathTimer = Constants.InteriorNav.ChaseRepathInterval;

                    // Only recompute if the target has moved more than one grid cell.
                    // This avoids resetting PathIndex every 0.2s when the target barely moved,
                    // which was causing NPCs to repeatedly re-traverse waypoint 0.
                    float targetMovedSq = (targetLocal.x - data.LastChaseTargetLocal.x) * (targetLocal.x - data.LastChaseTargetLocal.x) +
                                          (targetLocal.z - data.LastChaseTargetLocal.z) * (targetLocal.z - data.LastChaseTargetLocal.z);
                    float cellThreshold = _grid.CellSize * 2f;
                    if (targetMovedSq > cellThreshold * cellThreshold || data.Path == null)
                    {
                        data.TargetLocal = targetLocal;
                        data.LastChaseTargetLocal = targetLocal;
                        ComputePathToTarget(data);
                    }
                }
            }

            // Fallback: re-pathfind periodically even without chase target.
            // This handles the case where the game stops calling SetDestination
            // after we cleared HasDestination (NPC would otherwise stand forever).
            // Skip when Arrived — the NPC reached its destination and should idle
            // until the consumer sets a new target or recalls.
            if (!data.Arrived)
            {
                data.RepathTimer -= Time.deltaTime;
                if (data.RepathTimer <= 0f)
                {
                    data.RepathTimer = 0.5f;
                    if (data.Path == null || data.PathIndex >= data.Path.Count)
                    {
                        ComputePathToTarget(data);
                        if (data.Path != null)
                            DebugLog.Info($"[InteriorNavigator] Fallback re-path found {data.Path.Count} waypoints, speed={data.Speed:F1}");
                    }
                }
            }

            // Stuck detection — if NPC hasn't moved meaningfully over 2 seconds, recompute path.
            // Uses cumulative displacement (not per-frame delta) to avoid false positives
            // at high framerates where per-frame movement is tiny but NPC IS progressing.
            if (data.Path != null && data.PathIndex < data.Path.Count)
            {
                if (data.StuckTimer == 0f)
                    data.StuckStartPos = pos;

                data.StuckTimer += Time.deltaTime;

                float displacementSq = (pos.x - data.StuckStartPos.x) * (pos.x - data.StuckStartPos.x) +
                                       (pos.z - data.StuckStartPos.z) * (pos.z - data.StuckStartPos.z);

                if (displacementSq > 0.25f) // moved > 0.5m from start → making progress
                {
                    data.StuckTimer = 0f;
                }
                else if (data.StuckTimer > 2.0f)
                {
                    data.StuckTimer = 0f;
                    Vector3 waypoint = data.Path[data.PathIndex];
                    DebugLog.Warning($"[InteriorNavigator] NPC stuck (displaced {Mathf.Sqrt(displacementSq):F2}m in 2s). " +
                                      $"speed={data.Speed:F1}, pathIdx={data.PathIndex}/{data.Path.Count}, " +
                                      $"npcWorld=({pos.x:F1},{pos.y:F1},{pos.z:F1}), " +
                                      $"waypoint=({waypoint.x:F1},{waypoint.y:F1},{waypoint.z:F1})");
                    ComputePathToTarget(data);
                }
            }

            UpdatePathFollow(npc, data, onComplete: () =>
            {
                if (data.ChaseTarget == null)
                {
                    // Directed mode: arrived at destination
                    data.Arrived = true;
                    data.OnArrival?.Invoke();
                    data.OnArrival = null;
                    // NPC stays until RecallNPC
                }
                // Chase mode: will re-pathfind next interval
            });
        }

        #endregion

        #region State: Exiting

        private void BeginExit(Component npc, TrackedNPC data)
        {
            Vector3 currentLocal = _buildingRoot.InverseTransformPoint(npc.transform.position);
            NavDoorwayInfo door = FindNearestExteriorDoorway(currentLocal);
            data.TargetDoorway = door;
            ComputeDoorwayPoints(data, door);

            // Save chase target position before clearing — used by ReleaseNPC to
            // resume pursuit on exterior NavMesh after the NPC exits the building.
            if (data.ChaseTarget != null)
            {
                try { data.SavedChasePosition = data.ChaseTarget.position; }
                catch { /* destroyed */ }
            }

            data.ChaseTarget = null;

            // Pathfind to doorway interior point
            data.Path = _grid.FindPath(currentLocal, _buildingRoot.InverseTransformPoint(data.DoorwayInteriorWorld));
            data.PathIndex = 0;
            data.StuckTimer = 0f;

            if (data.Path == null)
            {
                // No path to doorway — skip directly to leave lerp rather than
                // getting stuck forever. NPC will lerp through walls if needed.
                DebugLog.Warning("[InteriorNavigator] Exit pathfind failed, forcing doorway leave.");
                BeginDoorwayLeave(npc, data);
                return;
            }

            data.State = NPCNavState.Exiting;
        }

        #endregion

        #region Path Following

        private void UpdatePathFollow(Component npc, TrackedNPC data, Action onComplete)
        {
            if (data.Path == null || data.PathIndex >= data.Path.Count)
            {
                onComplete();
                return;
            }

            // Refresh speed each frame (catches walk→run transitions in chase mode)
            data.Speed = GetNPCSpeed(data);
            if (data.Speed < 0.1f)
            {
                DebugLog.Warning($"[InteriorNavigator] NPC speed near zero ({data.Speed:F3}), forcing minimum.");
                data.Speed = 1.8f; // fallback to walk speed
            }

            Vector3 target = data.Path[data.PathIndex];
            Vector3 pos = npc.transform.position;

            // Move at floor height
            float floorY = _buildingRoot.TransformPoint(Vector3.zero).y;
            target.y = floorY;
            Vector3 newPos = Vector3.MoveTowards(pos, target, data.Speed * Time.deltaTime);
            newPos.y = floorY;
            npc.transform.position = newPos;

            // Face movement direction
            RotateToward(npc.transform, target - pos);

            // Check waypoint arrival — advance through multiple waypoints per frame
            // if speed is high enough (prevents slow cell-by-cell crawl)
            float distSq = (newPos.x - target.x) * (newPos.x - target.x) +
                           (newPos.z - target.z) * (newPos.z - target.z);

            float threshold = (data.PathIndex == data.Path.Count - 1)
                ? Constants.InteriorNav.DestinationArrivalThreshold
                : Constants.InteriorNav.WaypointArrivalThreshold;

            if (distSq < threshold * threshold)
            {
                data.PathIndex++;
                // Skip ahead through close waypoints in the same frame
                while (data.PathIndex < data.Path.Count - 1)
                {
                    Vector3 next = data.Path[data.PathIndex];
                    next.y = floorY;
                    float nextDistSq = (newPos.x - next.x) * (newPos.x - next.x) +
                                       (newPos.z - next.z) * (newPos.z - next.z);
                    if (nextDistSq < threshold * threshold)
                        data.PathIndex++;
                    else
                        break;
                }
            }
        }

        private void ComputePathToTarget(TrackedNPC data)
        {
            Vector3 currentLocal = _buildingRoot.InverseTransformPoint(
                data.NpcComponent.transform.position);
            data.Path = _grid.FindPath(currentLocal, data.TargetLocal);
            data.PathIndex = 0;

            if (data.Path == null)
            {
                DebugLog.Warning("[InteriorNavigator] No path found to target " +
                                  $"({data.TargetLocal.x:F1}, {data.TargetLocal.z:F1})");
                return;
            }

            // Skip the first waypoint if it's at the NPC's current position.
            // FindPath always starts from the NPC's current grid cell, so waypoint 0
            // is nearly always right where we stand — advancing past it avoids wasting
            // a frame on a zero-distance move after every repath.
            if (data.Path.Count > 1)
            {
                Vector3 wp0 = data.Path[0];
                Vector3 pos = data.NpcComponent.transform.position;
                float distSq = (pos.x - wp0.x) * (pos.x - wp0.x) + (pos.z - wp0.z) * (pos.z - wp0.z);
                if (distSq < _grid.CellSize * _grid.CellSize)
                    data.PathIndex = 1;
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Cross-platform GetComponent by <see cref="System.Type"/>.
        /// IL2CPP requires <c>Il2CppSystem.Type</c>; this converts automatically.
        /// </summary>
        private static Component? FindComponent(Component target, Type type)
        {
#if IL2CPP
            return target.GetComponent(Il2CppType.From(type));
#else
            return target.GetComponent(type);
#endif
        }

        /// <summary>
        /// Cross-platform GetComponentInChildren by <see cref="System.Type"/>.
        /// </summary>
        private static Component? FindComponentInChildren(Component target, Type type)
        {
#if IL2CPP
            return target.GetComponentInChildren(Il2CppType.From(type));
#else
            return target.GetComponentInChildren(type);
#endif
        }

        /// <summary>
        /// Cross-platform GetComponentInParent by <see cref="System.Type"/>.
        /// </summary>
        private static Component? FindComponentInParent(Component target, Type type)
        {
#if IL2CPP
            return target.GetComponentInParent(Il2CppType.From(type));
#else
            return target.GetComponentInParent(type);
#endif
        }

        private void RotateToward(Transform t, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;

            Quaternion targetRot = Quaternion.LookRotation(direction.normalized, Vector3.up);
            t.rotation = Quaternion.RotateTowards(
                t.rotation, targetRot,
                Constants.InteriorNav.RotationSpeed * Time.deltaTime);
        }

        private NavDoorwayInfo FindNearestExteriorDoorway(Vector3 localPos)
        {
            NavDoorwayInfo? best = null;
            float bestDist = float.MaxValue;
            foreach (NavDoorwayInfo d in _doorways)
            {
                if (d.IsInterior) continue;
                float dist = Vector3.Distance(localPos, d.Center);
                if (dist < bestDist) { bestDist = dist; best = d; }
            }
            return best!;
        }

        private void ComputeDoorwayPoints(TrackedNPC data, NavDoorwayInfo door)
        {
            // Exterior point: where the NavMesh agent walks to before we take control.
            Vector3 extLocal;
            if (door.StairBasePosition.HasValue)
            {
                // Stair doorway: use actual stair base so the NPC walks to the
                // bottom of the stairs and the lerp climbs the stair surface.
                extLocal = door.StairBasePosition.Value;
            }
            else
            {
                // Non-stair: well outside the carving zone on surviving NavMesh.
                float extOffset = door.WallThickness / 2f + 2.5f;
                extLocal = door.Center - door.InwardNormal * extOffset;
                extLocal.y = -_foundationHeight;
            }

            Vector3 extWorld = _buildingRoot.TransformPoint(extLocal);

            // Don't snap to NavMesh — SamplePosition can move the point around building
            // corners, causing the agent to route along the carving boundary and get stuck.
            // The agent's SetDestination internally snaps to the nearest reachable NavMesh.

            // Threshold point: just outside the doorway at floor level.
            // For stair doorways this is the ramp top — Phase 1 climbs from the
            // stair base to here, Phase 2 passes through the doorway.
            float threshOffset = door.WallThickness / 2f + 0.3f;
            Vector3 threshLocal = door.Center - door.InwardNormal * threshOffset;
            threshLocal.y = door.StairBasePosition.HasValue ? 0f : extLocal.y;

            // Interior point: inside the wall on the floor
            float intOffset = door.WallThickness / 2f + 0.3f;
            Vector3 intLocal = door.Center + door.InwardNormal * intOffset;
            intLocal.y = 0f;

            data.DoorwayExteriorWorld = extWorld;
            data.DoorwayThresholdWorld = _buildingRoot.TransformPoint(threshLocal);
            data.DoorwayInteriorWorld = _buildingRoot.TransformPoint(intLocal);
        }

        private bool IsInsideBuilding(Vector3 localPos, float margin = 0f)
        {
            return localPos.x >= -margin && localPos.x <= _roomSize.x + margin &&
                   localPos.z >= -margin && localPos.z <= _roomSize.z + margin;
        }

        /// <summary>
        /// Check if any player is inside this building.
        /// Uses <see cref="Camera.allCameras"/> to support multiplayer.
        /// </summary>
        private bool IsPlayerInside() =>
            FindPlayerInside() != null;

        /// <summary>
        /// Find the first player transform inside this building, or null if none.
        /// Uses <see cref="Camera.allCameras"/> to support multiplayer — each player has a camera.
        /// </summary>
        private Transform? FindPlayerInside()
        {
            foreach (Camera cam in Camera.allCameras)
            {
                Vector3 playerLocal = _buildingRoot.InverseTransformPoint(cam.transform.position);
                if (IsInsideBuilding(playerLocal, margin: 1f))
                    return cam.transform;
            }
            return null;
        }

        private TrackedNPC CreateTrackedNPC(Component npc)
        {
            var tracked = new TrackedNPC { NpcComponent = npc };

            // Resolve NPCMovement and NavMeshAgent via reflection
            if (_npcMovementType != null)
            {
                Component? movement = FindComponent(npc, _npcMovementType);
                if (movement == null)
                    movement = FindComponentInChildren(npc, _npcMovementType);
                if (movement == null)
                    movement = FindComponentInParent(npc, _npcMovementType);

                if (movement != null)
                {
                    tracked.MovementRef = movement;
                    if (_agentAccessor.IsValid)
                        tracked.Agent = _agentAccessor.GetValue(movement) as NavMeshAgent;
                }
            }

            // Find the NPC's physics collider (CapsuleCollider on a child GameObject).
            // Disabled while inside to prevent NPCs from pushing the player.
            tracked.NpcCollider = npc.GetComponentInChildren<CapsuleCollider>();

            tracked.Speed = GetNPCSpeed(tracked);
            return tracked;
        }

        private float GetNPCSpeed(TrackedNPC data)
        {
            if (data.MovementRef == null) return 3.5f;

            float walkSpeed = 1.8f;
            float runSpeed = 7f;
            float scale = 0f;
            float multiplier = 1f;

            try
            {
                if (_walkSpeedAccessor.IsValid)
                {
                    object? val = _walkSpeedAccessor.GetValue(data.MovementRef);
                    if (val is float f) walkSpeed = f;
                }
                if (_runSpeedAccessor.IsValid)
                {
                    object? val = _runSpeedAccessor.GetValue(data.MovementRef);
                    if (val is float f) runSpeed = f;
                }
                if (_speedScaleAccessor.IsValid)
                {
                    object? val = _speedScaleAccessor.GetValue(data.MovementRef);
                    if (val is float f) scale = f;
                }
                if (_moveSpeedMultAccessor.IsValid)
                {
                    object? val = _moveSpeedMultAccessor.GetValue(data.MovementRef);
                    if (val is float f) multiplier = f;
                }
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"[InteriorNavigator] Failed to read NPC speed via reflection: {ex.Message}");
            }

            return Mathf.Lerp(walkSpeed, runSpeed, scale) * multiplier;
        }

        private void ReleaseNPC(Component npc, TrackedNPC data, bool warpToExterior)
        {
            if (warpToExterior && data.DoorwayExteriorWorld != Vector3.zero)
                npc.transform.position = data.DoorwayExteriorWorld;

            // Re-enable collider before releasing back to game control
            if (data.NpcCollider != null)
                data.NpcCollider.enabled = true;

            EnableAgent(data);

            // Restore HasDestination — we cleared it in BeginDoorwayEntry to prevent
            // UpdateDestination from overwriting our agent destination. If not restored,
            // the game's pursuit AI stops generating SetDestination calls and the NPC
            // stands idle forever (won't re-enter building if player goes back inside).
            if (data.MovementRef != null && _hasDestinationAccessor.IsValid)
            {
                try { _hasDestinationAccessor.SetValue(data.MovementRef, true); }
                catch (Exception ex) { DebugLog.Warning($"[InteriorNavigator] RestoreHasDestination failed: {ex.Message}"); }
            }

            // Must remove from global set BEFORE invoking SetDestination
            // so the Harmony prefix lets the call through to the original method.
            _globallyManaged.Remove(npc);
            _removeQueue.Add(npc);

            // Resume navigation: prefer pending exterior destination (game called
            // SetDestination(outside) while NPC was inside), then saved chase position
            // (NPC exited because chase target left building).
            Vector3? resumeDestination = data.PendingExteriorDestination ?? data.SavedChasePosition;
            if (resumeDestination.HasValue &&
                data.MovementRef != null &&
                _originalSetDestination != null)
            {
                try
                {
                    // Parameters: (Vector3 destination, Action<WalkResult> callback, float walkSpeedMult, float runSpeedMult)
                    _originalSetDestination.Invoke(
                        data.MovementRef,
                        new object?[] { resumeDestination.Value, null, 1f, 1f });
                    DebugLog.Info($"[InteriorNavigator] NPC released, resuming navigation to " +
                                  $"({resumeDestination.Value.x:F1}, {resumeDestination.Value.z:F1})");
                }
                catch (Exception ex)
                {
                    DebugLog.Warning($"[InteriorNavigator] Failed to set resume destination: {ex.Message}");
                }
            }
            else
            {
                DebugLog.Info("[InteriorNavigator] NPC released from building (no resume destination).");
            }
        }

        private void DisableAgent(TrackedNPC data)
        {
            if (data.MovementRef != null && _setAgentEnabled != null)
            {
                try { _setAgentEnabled.Invoke(data.MovementRef, new object[] { false }); }
                catch (Exception ex) { DebugLog.Warning($"[InteriorNavigator] DisableAgent failed: {ex.Message}"); }
            }
        }

        private void ClearHasDestination(TrackedNPC data)
        {
            if (data.MovementRef != null && _hasDestinationAccessor.IsValid)
            {
                try { _hasDestinationAccessor.SetValue(data.MovementRef, false); }
                catch (Exception ex) { DebugLog.Warning($"[InteriorNavigator] ClearHasDestination failed: {ex.Message}"); }
            }
        }

        private void EnableAgent(TrackedNPC data)
        {
            if (data.MovementRef != null && _setAgentEnabled != null)
            {
                try { _setAgentEnabled.Invoke(data.MovementRef, new object[] { true }); }
                catch (Exception ex) { DebugLog.Warning($"[InteriorNavigator] EnableAgent failed: {ex.Message}"); }
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Clean up this navigator instance. Unregisters from the active buildings list,
        /// releases all NPCs, and unpatches Harmony when no buildings remain.
        /// Safe to call multiple times.
        /// </summary>
        public void Cleanup()
        {
            _activeBuildings.Remove(this);
            ReleaseAllNPCs();

            // Unpatch when no buildings remain
            if (_activeBuildings.Count == 0 && _harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
                _patchApplied = false;
                DebugLog.Info("[InteriorNavigator] Unpatched NPCMovement.SetDestination (no active buildings).");
            }
        }

        #endregion
    }
}
