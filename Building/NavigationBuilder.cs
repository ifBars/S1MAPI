using System;
using System.Collections.Generic;
using S1MAPI.Utils;
using UnityEngine;
using UnityEngine.AI;
#if IL2CPP
using Il2CppInterop.Runtime.Injection;
#endif

namespace S1MAPI.Building
{
    /// <summary>
    /// Records the position and dimensions of a doorway for NavMesh source filtering
    /// and ramp/ground plane generation. Used for both exterior and interior doorways.
    /// </summary>
    public sealed class NavDoorwayInfo
    {
        /// <summary>Center of the doorway in local building coordinates (Y=0, floor level).</summary>
        public Vector3 Center { get; }

        /// <summary>Width of the doorway opening in meters.</summary>
        public float Width { get; }

        /// <summary>Height of the doorway opening in meters.</summary>
        public float Height { get; }

        /// <summary>Unit vector perpendicular to the wall face in the XZ plane.</summary>
        public Vector3 InwardNormal { get; }

        /// <summary>Thickness of the wall containing this doorway.</summary>
        public float WallThickness { get; }

        /// <summary>
        /// Position at the base of the stairs in local building coordinates (ground level).
        /// Null for interior doorways or exterior doorways without stairs.
        /// </summary>
        public Vector3? StairBasePosition { get; }

        /// <summary>
        /// Whether this doorway is an interior wall doorway (between rooms)
        /// rather than an exterior wall doorway.
        /// </summary>
        public bool IsInterior { get; }

        /// <summary>
        /// Create a doorway info record.
        /// </summary>
        /// <param name="center">Door center in local building coordinates (Y=0)</param>
        /// <param name="width">Doorway width in meters</param>
        /// <param name="height">Doorway height in meters</param>
        /// <param name="inwardNormal">Unit vector perpendicular to the wall face</param>
        /// <param name="wallThickness">Wall thickness in meters</param>
        /// <param name="stairBasePosition">Position at stair base (ground level), or null if no stairs</param>
        /// <param name="isInterior">True for interior wall doorways between rooms</param>
        public NavDoorwayInfo(
            Vector3 center, float width, float height,
            Vector3 inwardNormal, float wallThickness,
            Vector3? stairBasePosition = null,
            bool isInterior = false)
        {
            Center = center;
            Width = width;
            Height = height;
            InwardNormal = inwardNormal;
            WallThickness = wallThickness;
            StairBasePosition = stairBasePosition;
            IsInterior = isInterior;
        }
    }

    /// <summary>
    /// Manages NPC navigation for building interiors.
    /// <para>
    /// Carves the building footprint from baked NavMesh using <c>NavMeshObstacle</c>,
    /// then provides custom A* pathfinding for interior navigation.
    /// A Harmony patch on <c>NPCMovement.SetDestination</c> automatically intercepts NPCs
    /// whose destinations fall inside the building — no manual <c>SendNPCTo</c> calls needed.
    /// NPCs seamlessly transition between exterior NavMesh and interior A* pathfinding.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Call <see cref="Build"/> after the building is positioned in the scene.
    /// Call <see cref="Rebuild"/> when interior objects change (e.g. furniture moved).
    /// Call <see cref="Remove"/> when the building is destroyed.
    /// </remarks>
    public sealed class NavigationBuilder
    {
        #region Fields

        private readonly Transform _buildingRoot;
        private readonly Vector3 _roomSize;
        private readonly IReadOnlyList<NavDoorwayInfo> _doorways;
        private readonly float _foundationHeight;
        private readonly float _wallThickness;

        // Physical objects
        private readonly List<GameObject> _rampObjects = new List<GameObject>();

        // Carving obstacles placed at wall positions to block terrain NavMesh
        private readonly List<GameObject> _carvingObstacles = new List<GameObject>();

        // Custom interior pathfinding
        private InteriorNavigatorCore? _navCore;
        private InteriorNavigator? _navShell;

        private bool _isBuilt;

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new navigation builder for a building.
        /// </summary>
        /// <param name="buildingRoot">Root transform of the building (must be positioned before calling Build)</param>
        /// <param name="roomSize">Interior room dimensions (width, height, depth)</param>
        /// <param name="doorways">Doorway positions for source filtering and ramp generation</param>
        /// <param name="wallThickness">Exterior wall thickness in meters.</param>
        /// <param name="foundationHeight">Foundation height in meters (0 = no foundation).</param>
        public NavigationBuilder(
            Transform buildingRoot,
            Vector3 roomSize,
            IReadOnlyList<NavDoorwayInfo> doorways,
            float wallThickness,
            float foundationHeight = 0f)
        {
            _buildingRoot = buildingRoot;
            _roomSize = roomSize;
            _doorways = doorways;
            _wallThickness = wallThickness;
            _foundationHeight = foundationHeight;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Whether the NavMesh is currently active.
        /// </summary>
        public bool IsBuilt =>
            _isBuilt;

        #endregion

        #region Public API

        /// <summary>
        /// Set up interior NPC navigation for this building.
        /// <para>
        /// Carves baked NavMesh under the footprint and installs a custom A*
        /// pathfinding grid. NPCs whose <c>SetDestination</c> targets fall inside the
        /// building are automatically intercepted and navigated through the interior.
        /// </para>
        /// Must be called after the building is positioned in the scene.
        /// </summary>
        public void Build()
        {
            if (_isBuilt)
            {
                DebugLog.Warning("[NavigationBuilder] NavMesh already built. Call Rebuild() to refresh.");
                return;
            }

            CreateStairRamps();

            // Carve the full building footprint — removes all baked NavMesh inside.
            PlaceFootprintCarvingObstacle();

            // Build A* pathfinding grid and navigation core.
            var pathGrid = new InteriorPathGrid(
                _buildingRoot, _roomSize, _doorways);
            _navCore = new InteriorNavigatorCore(
                pathGrid, _doorways, _buildingRoot, _roomSize, _foundationHeight);

            // Attach thin MonoBehaviour shell for Unity lifecycle forwarding.
#if IL2CPP
            if (!ClassInjector.IsTypeRegisteredInIl2Cpp<InteriorNavigator>())
                ClassInjector.RegisterTypeInIl2Cpp<InteriorNavigator>();
#endif
            _navShell = _buildingRoot.gameObject.AddComponent<InteriorNavigator>();
            _navShell._core = _navCore;

            _isBuilt = true;
        }

        /// <summary>
        /// Tear down and rebuild the NavMesh.
        /// Use when interior objects change (e.g. furniture placed or moved).
        /// </summary>
        public void Rebuild()
        {
            Remove();
            Build();
        }

        /// <summary>
        /// Check if an NPC is currently being managed by this building's interior navigator.
        /// </summary>
        /// <param name="npc">The NPC component to check.</param>
        public bool IsNPCInside(Component npc)
        {
            return _navCore != null && _navCore.IsTracking(npc);
        }

        /// <summary>
        /// Send an NPC to a position inside the building. The NPC walks to the nearest
        /// doorway on exterior NavMesh, enters via lerp, then follows A* path to target.
        /// </summary>
        /// <param name="npc">The NPC's NPCMovement component.</param>
        /// <param name="localTarget">Target position in building-local coordinates (0,0 = building corner).</param>
        /// <param name="onArrival">Optional callback invoked when the NPC reaches the destination.</param>
        public void SendNPCToPosition(Component npc, Vector3 localTarget, Action? onArrival = null)
            => _navCore?.SendNPCToPosition(npc, localTarget, onArrival);

        /// <summary>
        /// Recall an NPC from the building. If inside, begins exit via A* path to doorway.
        /// If still approaching, releases immediately. The NPC's NavMeshAgent is re-enabled on exit.
        /// </summary>
        /// <param name="npc">The NPC's NPCMovement component.</param>
        public void RecallNPC(Component npc)
            => _navCore?.RecallNPC(npc);

        /// <summary>
        /// Convert a world-space position to building-local coordinates.
        /// Use this to convert world positions (e.g. furniture transforms) to the local
        /// coordinates expected by <see cref="SendNPCToPosition"/>.
        /// </summary>
        /// <param name="worldPosition">The world-space position to convert.</param>
        public Vector3 WorldToLocal(Vector3 worldPosition)
            => _buildingRoot.InverseTransformPoint(worldPosition);

        /// <summary>
        /// Convert a building-local position to world-space coordinates.
        /// </summary>
        /// <param name="localPosition">The building-local position to convert.</param>
        public Vector3 LocalToWorld(Vector3 localPosition)
            => _buildingRoot.TransformPoint(localPosition);

        /// <summary>
        /// Show or hide the interior pathfinding grid visualization.
        /// Green cells are walkable, red cells are blocked.
        /// </summary>
        /// <param name="show">True to show the grid, false to hide it.</param>
        public void VisualizePathGrid(bool show = true)
        {
            _navCore?.VisualizeGrid(show);
        }

        /// <summary>
        /// Diagnose why a specific grid cell is blocked. Logs the blocking reason
        /// (wall margin, interior wall, physics collider name) to the console.
        /// Cell coordinates are visible in the visualization quad names (Cell_X_Z).
        /// </summary>
        /// <param name="gridX">The X coordinate on the interior grid.</param>
        /// <param name="gridZ">The Z coordinate on the interior grid.</param>
        public void DiagnoseCell(int gridX, int gridZ)
        {
            _navCore?.DiagnoseCell(gridX, gridZ);
        }

        /// <summary>
        /// Remove all NavMesh data and cleanup.
        /// Call when the building is destroyed.
        /// </summary>
        public void Remove()
        {
            if (!_isBuilt) return;

            // Release all NPCs and clean up navigation core
            if (_navCore != null)
            {
                _navCore.ReleaseAllNPCs();
                _navCore.Cleanup();
                _navCore = null;
            }
            if (_navShell != null)
            {
                _navShell._core = null; // prevent double cleanup from OnDestroy
                UnityEngine.Object.Destroy(_navShell);
                _navShell = null;
            }

            foreach (GameObject obs in _carvingObstacles)
                UnityEngine.Object.Destroy(obs);
            _carvingObstacles.Clear();

            foreach (GameObject ramp in _rampObjects)
                UnityEngine.Object.Destroy(ramp);
            _rampObjects.Clear();

            _isBuilt = false;
            DebugLog.Info("[NavigationBuilder] Removed NavMesh data.");
        }

        #endregion

        #region Public API — Walkability

        /// <summary>
        /// Check if a local-space position is on a walkable grid cell.
        /// Returns false when the navigation system has not been built yet.
        /// </summary>
        /// <param name="localPos">Position in building-local coordinates.</param>
        /// <returns>True if the cell at <paramref name="localPos"/> is walkable.</returns>
        public bool IsWalkable(Vector3 localPos)
            => _navCore?.PathGrid?.IsWalkable(localPos) ?? false;

        /// <summary>
        /// Get the center of the nearest walkable grid cell in local coordinates.
        /// Returns <paramref name="localPos"/> unchanged when the navigation system has not been built yet.
        /// </summary>
        /// <param name="localPos">Position in building-local coordinates.</param>
        /// <returns>The center of the nearest walkable cell, or <paramref name="localPos"/> if unavailable.</returns>
        public Vector3 NearestWalkableCell(Vector3 localPos)
            => _navCore?.PathGrid?.NearestWalkableCell(localPos) ?? localPos;

        /// <summary>
        /// Grid cell size in meters. Matches the resolution used by interior pathfinding
        /// so callers can iterate cells at the same spacing.
        /// Returns 0.5 when the navigation system has not been built yet.
        /// </summary>
        public float CellSize
            => _navCore?.PathGrid?.CellSize ?? 0.5f;

        #endregion

        #region Private — NavMesh Carving

        /// <summary>
        /// Place a single carving obstacle covering the entire building footprint.
        /// This carves out ALL baked NavMesh inside the building (terrain island).
        /// Test: if carving only affects baked data (not runtime-added instances),
        /// the separately-added interior NavMeshData survives untouched.
        /// </summary>
        private void PlaceFootprintCarvingObstacle()
        {
            // Obstacle covers full footprint, tall enough to intersect terrain NavMesh
            // at any height. Positioned at building center.
            float height = _roomSize.y + 4f; // generous vertical coverage

            Vector3 localCenter = new Vector3(
                _roomSize.x / 2f,
                height / 2f,
                _roomSize.z / 2f);

            var go = new GameObject("NavMeshFootprintCarve");
            go.transform.SetParent(_buildingRoot);
            go.transform.localPosition = localCenter;
            go.transform.localRotation = Quaternion.identity;

            NavMeshObstacle obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.size = new Vector3(_roomSize.x, height, _roomSize.z);
            obstacle.center = Vector3.zero;

            _carvingObstacles.Add(go);

            DebugLog.Info($"[NavigationBuilder] Footprint carve: size=({_roomSize.x:F1}, {height:F1}, {_roomSize.z:F1}), " +
                          $"worldCenter={go.transform.position}");
        }

        #endregion

        #region Private — Stair Ramps

        /// <summary>
        /// Create invisible ramp colliders at each exterior doorway with stairs.
        /// </summary>
        private void CreateStairRamps()
        {
            foreach (NavDoorwayInfo doorway in _doorways)
            {
                if (!doorway.StairBasePosition.HasValue) continue;

                Vector3 top = doorway.Center;
                Vector3 bottom = doorway.StairBasePosition.Value;

                Vector3 flatDelta = new Vector3(bottom.x - top.x, 0f, bottom.z - top.z);
                float horizontalDist = flatDelta.magnitude;
                float verticalDist = Mathf.Abs(bottom.y);
                float rampLength = Mathf.Sqrt(horizontalDist * horizontalDist + verticalDist * verticalDist);
                float slopeAngle = Mathf.Atan2(verticalDist, horizontalDist) * Mathf.Rad2Deg;

                Vector3 mid = (top + bottom) / 2f;
                Vector3 outward = flatDelta.normalized;

                GameObject rampGO = new GameObject("NavMeshRamp");
                rampGO.transform.SetParent(_buildingRoot);
                rampGO.transform.localPosition = mid;

                Quaternion facing = Quaternion.LookRotation(outward, Vector3.up);
                rampGO.transform.localRotation = facing * Quaternion.AngleAxis(slopeAngle, Vector3.right);

                float rampWidth = Mathf.Max(doorway.Width, Constants.NavMesh.MinRampWidth)
                                  + Constants.NavMesh.RampErosionBuffer;

                BoxCollider col = rampGO.AddComponent<BoxCollider>();
                col.center = Vector3.zero;
                col.size = new Vector3(rampWidth, Constants.NavMesh.RampColliderThickness, rampLength);

                _rampObjects.Add(rampGO);

                Vector3 worldTop = _buildingRoot.TransformPoint(top);
                Vector3 worldBottom = _buildingRoot.TransformPoint(bottom);
                DebugLog.Info($"[NavigationBuilder] Ramp: worldPos={rampGO.transform.position}, " +
                              $"top={worldTop}, bottom={worldBottom}, " +
                              $"width={rampWidth:F2}, length={rampLength:F2}, angle={slopeAngle:F1}°");
            }
        }

        #endregion
    }
}
