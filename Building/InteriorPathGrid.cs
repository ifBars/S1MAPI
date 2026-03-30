using System;
using System.Collections.Generic;
using S1MAPI.Utils;
using UnityEngine;

namespace S1MAPI.Building
{
    /// <summary>
    /// 2D walkability grid with A* pathfinding for building interiors.
    /// Generated from building layout data (room size, walls, doorways).
    /// All positions are in local building coordinates (Y=0 is floor level).
    /// </summary>
    internal sealed class InteriorPathGrid
    {
        private readonly float _cellSize;
        private readonly int _gridWidth;
        private readonly int _gridDepth;
        private readonly bool[] _walkable;
        private readonly Transform _buildingRoot;
        private readonly Vector3 _roomSize;
        private readonly IReadOnlyList<NavDoorwayInfo> _doorways;

        /// <summary>
        /// Create a walkability grid for the given building layout.
        /// Marks cells as unwalkable for walls, doorway openings, and physics colliders.
        /// </summary>
        /// <param name="buildingRoot">Root transform of the building.</param>
        /// <param name="roomSize">Room dimensions in local coordinates.</param>
        /// <param name="doorways">Doorway positions and dimensions.</param>
        public InteriorPathGrid(
            Transform buildingRoot,
            Vector3 roomSize,
            IReadOnlyList<NavDoorwayInfo> doorways)
        {
            _buildingRoot = buildingRoot;
            _roomSize = roomSize;
            _doorways = doorways;
            _cellSize = BuildingUtilities.ComputeGridCellSize(roomSize.x, roomSize.z);

            // Grid covers exactly the room — no extension through walls.
            // NPCs lerp through the wall zone via InteriorNavigator; the A* path
            // only needs to reach doorway-edge cells inside the room boundary.
            _gridWidth = Mathf.CeilToInt(roomSize.x / _cellSize);
            _gridDepth = Mathf.CeilToInt(roomSize.z / _cellSize);
            _walkable = new bool[_gridWidth * _gridDepth];

            GenerateGrid();
        }

        /// <summary>Computed grid cell size in meters.</summary>
        public float CellSize =>
            _cellSize;

        /// <summary>
        /// Regenerate the walkability grid (e.g., after furniture placement changes).
        /// </summary>
        public void Regenerate()
        {
            GenerateGrid();
        }

        /// <summary>
        /// Find a path from startLocal to endLocal in local building coordinates.
        /// Returns world-space waypoints, or null if no path exists.
        /// </summary>
        public List<Vector3>? FindPath(Vector3 startLocal, Vector3 endLocal)
        {
            int sx = LocalToGridX(startLocal.x);
            int sz = LocalToGridZ(startLocal.z);
            int ex = LocalToGridX(endLocal.x);
            int ez = LocalToGridZ(endLocal.z);

            // Clamp to grid bounds
            sx = Mathf.Clamp(sx, 0, _gridWidth - 1);
            sz = Mathf.Clamp(sz, 0, _gridDepth - 1);
            ex = Mathf.Clamp(ex, 0, _gridWidth - 1);
            ez = Mathf.Clamp(ez, 0, _gridDepth - 1);

            // Snap to nearest walkable cell if start/end are unwalkable
            if (!IsWalkableCell(sx, sz))
                FindNearestWalkableCell(ref sx, ref sz);
            if (!IsWalkableCell(ex, ez))
                FindNearestWalkableCell(ref ex, ref ez);

            if (!IsWalkableCell(sx, sz) || !IsWalkableCell(ex, ez))
                return null;

            // A* search
            List<Vector2Int>? path = AStar(sx, sz, ex, ez);
            if (path == null) return null;

            // Convert grid path to world-space waypoints
            var worldPath = new List<Vector3>(path.Count);
            foreach (var cell in path)
            {
                Vector3 local = GridToLocal(cell.x, cell.y);
                worldPath.Add(_buildingRoot.TransformPoint(local));
            }

            // Smooth: skip intermediate waypoints when line-of-sight exists
            SmoothPath(worldPath);

            return worldPath;
        }

        /// <summary>
        /// Check if a local position is on a walkable cell.
        /// </summary>
        public bool IsWalkable(Vector3 localPos)
        {
            int gx = Mathf.FloorToInt(localPos.x / _cellSize);
            int gz = Mathf.FloorToInt(localPos.z / _cellSize);
            return IsWalkableCell(gx, gz);
        }

        /// <summary>
        /// Get the nearest walkable cell center in local coordinates.
        /// </summary>
        public Vector3 NearestWalkableCell(Vector3 localPos)
        {
            int gx = LocalToGridX(localPos.x);
            int gz = LocalToGridZ(localPos.z);
            FindNearestWalkableCell(ref gx, ref gz);
            return GridToLocal(gx, gz);
        }

        #region Grid Generation

        private void GenerateGrid()
        {
            float wallMargin = Constants.InteriorNav.WallMargin;

            // Start all cells walkable
            for (int i = 0; i < _walkable.Length; i++)
                _walkable[i] = true;

            // Mark wall margin zone as unwalkable.
            // Cells near the room edge are blocked; MarkDoorwayOpenings()
            // carves paths through at doorway locations.
            for (int gx = 0; gx < _gridWidth; gx++)
            {
                for (int gz = 0; gz < _gridDepth; gz++)
                {
                    Vector3 local = GridToLocal(gx, gz);

                    if (local.x < wallMargin || local.x > _roomSize.x - wallMargin ||
                        local.z < wallMargin || local.z > _roomSize.z - wallMargin)
                    {
                        _walkable[gz * _gridWidth + gx] = false;
                    }
                }
            }

            // Mark interior wall cells as unwalkable (with doorway openings)
            MarkInteriorWalls(wallMargin);

            // Mark cells blocked by physics colliders (furniture, etc.)
            MarkPhysicsObstacles();

            // Re-open doorway cells LAST — door panel colliders must not block
            // the walkable path through doorways.
            MarkDoorwayOpenings();

            int walkableCount = 0;
            for (int i = 0; i < _walkable.Length; i++)
                if (_walkable[i]) walkableCount++;

            DebugLog.Info($"[InteriorPathGrid] Generated {_gridWidth}x{_gridDepth} grid " +
                          $"({walkableCount}/{_walkable.Length} walkable), cellSize={_cellSize:F2}m");
        }

        private void MarkInteriorWalls(float margin)
        {
            foreach (var door in _doorways)
            {
                if (!door.IsInterior) continue;

                // Determine wall line from doorway's InwardNormal and Center.
                // Interior walls run perpendicular to the InwardNormal.
                Vector3 normal = door.InwardNormal;
                bool isXWall = Mathf.Abs(normal.z) > Mathf.Abs(normal.x); // wall runs along X
                float wallPos = isXWall ? door.Center.z : door.Center.x;
                float doorCenter = isXWall ? door.Center.x : door.Center.z;
                float halfWidth = door.Width / 2f;

                for (int gx = 0; gx < _gridWidth; gx++)
                {
                    for (int gz = 0; gz < _gridDepth; gz++)
                    {
                        Vector3 local = GridToLocal(gx, gz);

                        float perpDist = isXWall
                            ? Mathf.Abs(local.z - wallPos)
                            : Mathf.Abs(local.x - wallPos);

                        if (perpDist > margin) continue;

                        // Check if cell is within the doorway opening
                        float alongWall = isXWall ? local.x : local.z;
                        if (Mathf.Abs(alongWall - doorCenter) < halfWidth + 0.1f) continue;

                        _walkable[gz * _gridWidth + gx] = false;
                    }
                }
            }
        }

        private void MarkDoorwayOpenings()
        {
            foreach (var door in _doorways)
            {
                Vector3 center = door.Center;
                float halfWidth = door.Width / 2f;
                Vector3 normal = door.InwardNormal;

                // Open cells in a rectangle covering the doorway width × wall thickness.
                // For exterior doorways this extends through the wall zone into the
                // extended grid area so NPCs can path all the way through.
                bool isXDoor = Mathf.Abs(normal.z) > Mathf.Abs(normal.x);

                for (int gx = 0; gx < _gridWidth; gx++)
                {
                    for (int gz = 0; gz < _gridDepth; gz++)
                    {
                        Vector3 local = GridToLocal(gx, gz);

                        float perpDist, alongDist;
                        if (isXDoor)
                        {
                            perpDist = Mathf.Abs(local.z - center.z);
                            alongDist = Mathf.Abs(local.x - center.x);
                        }
                        else
                        {
                            perpDist = Mathf.Abs(local.x - center.x);
                            alongDist = Mathf.Abs(local.z - center.z);
                        }

                        // Full-cell buffer on width ensures NPCs can path through
                        // without clipping the door frame edges. NPC capsule radius
                        // (~0.35m) needs clearance beyond the nominal doorway width.
                        if (perpDist < door.WallThickness / 2f + _cellSize &&
                            alongDist < halfWidth + _cellSize)
                        {
                            _walkable[gz * _gridWidth + gx] = true;
                        }
                    }
                }
            }
        }

        private void MarkPhysicsObstacles()
        {
            // Short box at walking height — avoids detecting ceiling/roof colliders
            // which are building children and would mark every cell unwalkable.
            float probeHeight = 0.5f;
            Vector3 halfExtents = new Vector3(
                _cellSize / 2f * 0.8f,
                probeHeight,
                _cellSize / 2f * 0.8f);

            Quaternion rotation = _buildingRoot.rotation;

            // Collect structural colliders to exclude (floor, ceiling, walls, foundation, stairs, roof)
            var excludedColliders = new HashSet<Collider>();
            CollectStructuralColliders(_buildingRoot, excludedColliders);

            // Cache living entity roots (players/NPCs with Animators) to skip
            var livingRoots = new HashSet<int>();
            var staticRoots = new HashSet<int>();

            for (int gx = 0; gx < _gridWidth; gx++)
            {
                for (int gz = 0; gz < _gridDepth; gz++)
                {
                    if (!_walkable[gz * _gridWidth + gx]) continue;

                    Vector3 cellLocal = GridToLocal(gx, gz);
                    // Center probe at walking height (0.5m above floor)
                    Vector3 localCenter = new Vector3(cellLocal.x, probeHeight, cellLocal.z);
                    Vector3 worldCenter = _buildingRoot.TransformPoint(localCenter);

                    Collider[] hits = Physics.OverlapBox(worldCenter, halfExtents, rotation);
                    foreach (Collider hit in hits)
                    {
                        if (hit.isTrigger) continue;
                        if (excludedColliders.Contains(hit)) continue;

                        // Skip living entities (players/NPCs)
                        if (BuildingUtilities.IsLivingEntity(hit.transform, livingRoots, staticRoots))
                            continue;

                        // Only block for colliders whose center is inside the building
                        // footprint. This allows external furniture (e.g. MeshVault) placed
                        // inside to block, while filtering world objects (trees, lights)
                        // whose colliders bleed in from outside.
                        Vector3 colliderLocal = _buildingRoot.InverseTransformPoint(hit.bounds.center);
                        if (colliderLocal.x < -0.5f || colliderLocal.x > _roomSize.x + 0.5f ||
                            colliderLocal.z < -0.5f || colliderLocal.z > _roomSize.z + 0.5f)
                            continue;

                        _walkable[gz * _gridWidth + gx] = false;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Collect colliders from known structural folders (Floor, Walls, Ceiling,
        /// Foundation, Stairs, Roof) so they are excluded from physics obstacle detection.
        /// </summary>
        private static void CollectStructuralColliders(Transform root, HashSet<Collider> excluded)
        {
            string[] structuralNames = {
                "Floor", "Ceiling", "Walls", "ExteriorWalls", "InteriorWalls",
                Constants.Spatial.FoundationFolderName,
                Constants.Spatial.StairsFolderName,
                "Roof", "Parapet", "Molding", "WindowFrames", "DoorFrames"
            };

            foreach (string name in structuralNames)
            {
                Transform? folder = root.Find(name);
                if (folder == null) continue;
                foreach (Collider c in folder.GetComponentsInChildren<Collider>())
                    excluded.Add(c);
            }

            // Also exclude colliders directly on the root itself
            Collider? rootCollider = root.GetComponent<Collider>();
            if (rootCollider != null) excluded.Add(rootCollider);
        }

        #endregion

        #region A* Pathfinding

        private static readonly int[] DirectionX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] DirectionZ = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly float[] MoveCost = { 1f, 1.414f, 1f, 1.414f, 1f, 1.414f, 1f, 1.414f };

        private List<Vector2Int>? AStar(int sx, int sz, int ex, int ez)
        {
            if (sx == ex && sz == ez)
                return new List<Vector2Int> { new Vector2Int(sx, sz) };

            int totalCells = _gridWidth * _gridDepth;
            float[] gScore = new float[totalCells];
            float[] fScore = new float[totalCells];
            int[] cameFrom = new int[totalCells];
            bool[] closed = new bool[totalCells];

            for (int i = 0; i < totalCells; i++)
            {
                gScore[i] = float.MaxValue;
                fScore[i] = float.MaxValue;
                cameFrom[i] = -1;
            }

            int startIdx = sz * _gridWidth + sx;
            int endIdx = ez * _gridWidth + ex;
            gScore[startIdx] = 0f;
            fScore[startIdx] = Heuristic(sx, sz, ex, ez);

            // Simple priority queue using sorted list (fine for few hundred cells)
            var open = new SortedList<float, int>(new DuplicateKeyComparer());
            open.Add(fScore[startIdx], startIdx);

            while (open.Count > 0)
            {
                int currentIdx = open.Values[0];
                open.RemoveAt(0);

                if (currentIdx == endIdx)
                    return ReconstructPath(cameFrom, currentIdx);

                if (closed[currentIdx]) continue;
                closed[currentIdx] = true;

                int cx = currentIdx % _gridWidth;
                int cz = currentIdx / _gridWidth;

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + DirectionX[d];
                    int nz = cz + DirectionZ[d];

                    if (nx < 0 || nx >= _gridWidth || nz < 0 || nz >= _gridDepth) continue;

                    int neighborIdx = nz * _gridWidth + nx;
                    if (closed[neighborIdx] || !_walkable[neighborIdx]) continue;

                    // Prevent diagonal corner-cutting through walls
                    if (d % 2 == 1) // diagonal
                    {
                        int adj1 = cz * _gridWidth + nx; // same z, neighbor x
                        int adj2 = nz * _gridWidth + cx; // neighbor z, same x
                        if (!_walkable[adj1] || !_walkable[adj2]) continue;
                    }

                    float tentG = gScore[currentIdx] + MoveCost[d];
                    if (tentG < gScore[neighborIdx])
                    {
                        cameFrom[neighborIdx] = currentIdx;
                        gScore[neighborIdx] = tentG;
                        fScore[neighborIdx] = tentG + Heuristic(nx, nz, ex, ez);
                        open.Add(fScore[neighborIdx], neighborIdx);
                    }
                }
            }

            return null; // no path
        }

        private float Heuristic(int ax, int az, int bx, int bz)
        {
            float dx = ax - bx;
            float dz = az - bz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private List<Vector2Int> ReconstructPath(int[] cameFrom, int current)
        {
            var path = new List<Vector2Int>();
            while (current != -1)
            {
                int x = current % _gridWidth;
                int z = current / _gridWidth;
                path.Add(new Vector2Int(x, z));
                current = cameFrom[current];
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Comparer that allows duplicate keys in SortedList.
        /// </summary>
        private sealed class DuplicateKeyComparer : IComparer<float>
        {
            public int Compare(float x, float y)
            {
                int result = x.CompareTo(y);
                return result == 0 ? 1 : result; // never return 0 — treat equal as greater
            }
        }

        #endregion

        #region Path Smoothing

        private void SmoothPath(List<Vector3> worldPath)
        {
            if (worldPath.Count <= 2) return;

            int i = 0;
            while (i < worldPath.Count - 2)
            {
                // Try to skip intermediate waypoints via line-of-sight
                int farthest = i + 1;
                for (int j = worldPath.Count - 1; j > i + 1; j--)
                {
                    if (HasLineOfSight(worldPath[i], worldPath[j]))
                    {
                        farthest = j;
                        break;
                    }
                }

                // Remove all waypoints between i and farthest
                if (farthest > i + 1)
                {
                    worldPath.RemoveRange(i + 1, farthest - i - 1);
                }

                i++;
            }
        }

        private bool HasLineOfSight(Vector3 worldA, Vector3 worldB)
        {
            // Check walkability on the grid along the line from A to B
            Vector3 localA = _buildingRoot.InverseTransformPoint(worldA);
            Vector3 localB = _buildingRoot.InverseTransformPoint(worldB);

            int ax = LocalToGridX(localA.x);
            int az = LocalToGridZ(localA.z);
            int bx = LocalToGridX(localB.x);
            int bz = LocalToGridZ(localB.z);

            // Bresenham-like line walk
            int dx = Mathf.Abs(bx - ax);
            int dz = Mathf.Abs(bz - az);
            int sx = ax < bx ? 1 : -1;
            int sz = az < bz ? 1 : -1;
            int err = dx - dz;

            int x = ax, z = az;
            while (true)
            {
                if (!IsWalkableCell(x, z)) return false;
                if (x == bx && z == bz) break;

                int e2 = 2 * err;
                if (e2 > -dz) { err -= dz; x += sx; }
                if (e2 < dx) { err += dx; z += sz; }
            }

            return true;
        }

        #endregion

        #region Coordinate Conversion

        // Grid origin is at (0, 0) in local building coords — aligned with room corner.
        private int LocalToGridX(float localX) =>
            Mathf.Clamp(Mathf.FloorToInt(localX / _cellSize), 0, _gridWidth - 1);

        private int LocalToGridZ(float localZ) =>
            Mathf.Clamp(Mathf.FloorToInt(localZ / _cellSize), 0, _gridDepth - 1);

        private Vector3 GridToLocal(int gx, int gz) =>
            new Vector3(
                (gx + 0.5f) * _cellSize,
                0f,
                (gz + 0.5f) * _cellSize);

        private bool IsWalkableCell(int gx, int gz)
        {
            if (gx < 0 || gx >= _gridWidth || gz < 0 || gz >= _gridDepth)
                return false;
            return _walkable[gz * _gridWidth + gx];
        }

        private void FindNearestWalkableCell(ref int gx, ref int gz)
        {
            // BFS outward from (gx, gz) to find nearest walkable cell
            int bestX = gx, bestZ = gz;
            float bestDist = float.MaxValue;
            int searchRadius = Mathf.Max(_gridWidth, _gridDepth);

            for (int r = 1; r <= searchRadius; r++)
            {
                bool found = false;
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue; // only ring
                        int nx = gx + dx;
                        int nz = gz + dz;
                        if (IsWalkableCell(nx, nz))
                        {
                            float dist = dx * dx + dz * dz;
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestX = nx;
                                bestZ = nz;
                                found = true;
                            }
                        }
                    }
                }
                if (found) break;
            }

            gx = bestX;
            gz = bestZ;
        }

        #endregion

        #region Visualization

        private GameObject? _vizRoot;

        /// <summary>
        /// Create or refresh in-game visualization of the walkability grid.
        /// Green = walkable, red = blocked. Call again to refresh after Regenerate().
        /// </summary>
        public void Visualize()
        {
            DestroyVisualization();

            _vizRoot = new GameObject("[S1MAPI] PathGrid Visualizer");
            _vizRoot.transform.SetParent(_buildingRoot, worldPositionStays: false);
            _vizRoot.transform.localPosition = Vector3.zero;
            _vizRoot.transform.localRotation = Quaternion.identity;

            // Create shared materials
            Material walkableMat = CreateFlatMaterial(new Color(0f, 1f, 0f, 0.35f));
            Material blockedMat = CreateFlatMaterial(new Color(1f, 0f, 0f, 0.35f));

            float pad = 0.05f; // small gap between cells
            float quadSize = _cellSize - pad * 2f;

            for (int gx = 0; gx < _gridWidth; gx++)
            {
                for (int gz = 0; gz < _gridDepth; gz++)
                {
                    bool walkable = _walkable[gz * _gridWidth + gx];
                    Vector3 localCenter = GridToLocal(gx, gz);
                    localCenter.y = 0.02f; // slightly above floor

                    GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = $"Cell_{gx}_{gz}";
                    quad.transform.SetParent(_vizRoot.transform, worldPositionStays: false);
                    quad.transform.localPosition = localCenter;
                    quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // face up
                    quad.transform.localScale = new Vector3(quadSize, quadSize, 1f);

                    // Remove collider so it doesn't interfere with gameplay
                    var col = quad.GetComponent<Collider>();
                    if (col != null) UnityEngine.Object.Destroy(col);

                    var renderer = quad.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        renderer.sharedMaterial = walkable ? walkableMat : blockedMat;
                }
            }

            DebugLog.Info($"[InteriorPathGrid] Visualization created: {_gridWidth}x{_gridDepth} cells");
        }

        /// <summary>
        /// Remove the visualization markers.
        /// </summary>
        public void DestroyVisualization()
        {
            if (_vizRoot != null)
            {
                UnityEngine.Object.Destroy(_vizRoot);
                _vizRoot = null;
            }
        }

        /// <summary>
        /// Diagnose why a specific cell is blocked. Logs the reason (wall margin,
        /// interior wall, physics collider name/path) to the console.
        /// </summary>
        /// <param name="gx">Grid X coordinate (shown in Cell_X_Z name)</param>
        /// <param name="gz">Grid Z coordinate (shown in Cell_X_Z name)</param>
        public void DiagnoseCell(int gx, int gz)
        {
            if (gx < 0 || gx >= _gridWidth || gz < 0 || gz >= _gridDepth)
            {
                DebugLog.Warning($"[PathGrid] Cell ({gx},{gz}) is out of bounds (grid is {_gridWidth}x{_gridDepth})");
                return;
            }

            bool walkable = _walkable[gz * _gridWidth + gx];
            Vector3 localPos = GridToLocal(gx, gz);
            Vector3 worldPos = _buildingRoot.TransformPoint(localPos);

            DebugLog.Info($"[PathGrid] Cell ({gx},{gz}): local=({localPos.x:F2},{localPos.z:F2}), world=({worldPos.x:F2},{worldPos.y:F2},{worldPos.z:F2}), walkable={walkable}");

            if (walkable) return;

            // Check: exterior wall margin
            float wallMargin = Constants.InteriorNav.WallMargin;
            if (localPos.x < wallMargin || localPos.x > _roomSize.x - wallMargin ||
                localPos.z < wallMargin || localPos.z > _roomSize.z - wallMargin)
            {
                DebugLog.Info($"  -> Blocked by EXTERIOR WALL margin (margin={wallMargin:F2}, roomSize=({_roomSize.x:F2},{_roomSize.z:F2}))");
            }

            // Check: interior wall
            foreach (var door in _doorways)
            {
                if (!door.IsInterior) continue;
                Vector3 normal = door.InwardNormal;
                bool isXWall = Mathf.Abs(normal.z) > Mathf.Abs(normal.x);
                float wallPos = isXWall ? door.Center.z : door.Center.x;
                float perpDist = isXWall ? Mathf.Abs(localPos.z - wallPos) : Mathf.Abs(localPos.x - wallPos);
                if (perpDist <= wallMargin)
                {
                    float alongWall = isXWall ? localPos.x : localPos.z;
                    float doorCenter = isXWall ? door.Center.x : door.Center.z;
                    if (Mathf.Abs(alongWall - doorCenter) >= door.Width / 2f + 0.1f)
                    {
                        DebugLog.Info($"  -> Blocked by INTERIOR WALL at {(isXWall ? "Z" : "X")}={wallPos:F2} (perpDist={perpDist:F2})");
                    }
                }
            }

            // Check: physics collider
            float probeHeight = 0.5f;
            Vector3 halfExtents = new Vector3(_cellSize / 2f * 0.8f, probeHeight, _cellSize / 2f * 0.8f);
            Vector3 probeCenter = _buildingRoot.TransformPoint(new Vector3(localPos.x, probeHeight, localPos.z));
            Collider[] hits = Physics.OverlapBox(probeCenter, halfExtents, _buildingRoot.rotation);

            var excludedColliders = new HashSet<Collider>();
            CollectStructuralColliders(_buildingRoot, excludedColliders);

            foreach (Collider hit in hits)
            {
                if (hit.isTrigger) continue;
                bool excluded = excludedColliders.Contains(hit);
                Vector3 colliderLocal = _buildingRoot.InverseTransformPoint(hit.bounds.center);
                bool insideFootprint = colliderLocal.x >= -0.5f && colliderLocal.x <= _roomSize.x + 0.5f &&
                                       colliderLocal.z >= -0.5f && colliderLocal.z <= _roomSize.z + 0.5f;
                string path = GetTransformPath(hit.transform);
                DebugLog.Info($"  -> Collider: \"{path}\" (excluded={excluded}, insideFootprint={insideFootprint}, localPos=({colliderLocal.x:F2},{colliderLocal.y:F2},{colliderLocal.z:F2}), type={hit.GetType().Name})");
            }
        }

        private static string GetTransformPath(Transform t)
        {
            string path = t.name;
            Transform? parent = t.parent;
            int depth = 0;
            while (parent != null && depth < 5)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
                depth++;
            }
            return path;
        }

        private static Material CreateFlatMaterial(Color color)
        {
            // Use a transparent unlit shader
            Shader? shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            var mat = new Material(shader!);
            mat.color = color;
            return mat;
        }

        #endregion
    }
}
