using System.Collections.Generic;
using S1MAPI.Building.Config;
using S1MAPI.Extensions;
using S1MAPI.ProceduralMesh;
using S1MAPI.Utils;
using UnityEngine;

namespace S1MAPI.Building.Structural
{
    /// <summary>
    /// Specifies the axis an interior wall runs along.
    /// </summary>
    public enum InteriorWallAxis
    {
        /// <summary>Wall runs along X axis; <see cref="InteriorWallDefinition.Position"/> is the Z coordinate.</summary>
        X,
        /// <summary>Wall runs along Z axis; <see cref="InteriorWallDefinition.Position"/> is the X coordinate.</summary>
        Z
    }

    /// <summary>
    /// Defines an interior wall placement: axis, position, span, and optional opening.
    /// </summary>
    public sealed class InteriorWallDefinition
    {
        /// <summary>Which axis the wall runs along.</summary>
        public InteriorWallAxis Axis { get; }

        /// <summary>
        /// Position on the perpendicular axis (Z coordinate for <see cref="InteriorWallAxis.X"/>,
        /// X coordinate for <see cref="InteriorWallAxis.Z"/>).
        /// </summary>
        public float Position { get; }

        /// <summary>Start coordinate along the wall's own axis.</summary>
        public float From { get; }

        /// <summary>End coordinate along the wall's own axis.</summary>
        public float To { get; }

        /// <summary>Optional opening (door or window) centered in the wall.</summary>
        public WallOpening? Opening { get; }

        /// <summary>Optional wall color override.</summary>
        public Color? Color { get; }

        /// <summary>Optional wall material override.</summary>
        public Material? Material { get; }

        /// <summary>
        /// Create an interior wall definition.
        /// </summary>
        /// <param name="axis">Axis the wall runs along</param>
        /// <param name="position">Position on the perpendicular axis</param>
        /// <param name="from">Start coordinate along the wall axis</param>
        /// <param name="to">End coordinate along the wall axis</param>
        /// <param name="opening">Optional wall opening</param>
        /// <param name="color">Optional color override</param>
        /// <param name="material">Optional material override</param>
        public InteriorWallDefinition(
            InteriorWallAxis axis, float position, float from, float to,
            WallOpening? opening = null, Color? color = null, Material? material = null)
        {
            Axis = axis;
            Position = position;
            From = from;
            To = to;
            Opening = opening;
            Color = color;
            Material = material;
        }
    }

    /// <summary>
    /// Records the position and dimensions of a doorway in an interior wall.
    /// Used for future NavMesh link generation.
    /// </summary>
    public sealed class DoorwayInfo
    {
        /// <summary>Center of the doorway in local building coordinates.</summary>
        public Vector3 Center { get; }

        /// <summary>Width of the doorway opening in meters.</summary>
        public float Width { get; }

        /// <summary>Height of the doorway opening in meters.</summary>
        public float Height { get; }

        /// <summary>
        /// True if the wall's normal faces along Z (i.e., the wall runs along X).
        /// </summary>
        public bool FacesAlongZ { get; }

        /// <summary>Thickness of the wall containing this doorway.</summary>
        public float WallThickness { get; }

        /// <summary>Wall container with Left/Right/Top segment children (used for frame insetting).</summary>
        internal GameObject? WallContainer { get; set; }

        /// <summary>
        /// Create a doorway info record.
        /// </summary>
        public DoorwayInfo(Vector3 center, float width, float height, bool facesAlongZ, float wallThickness)
        {
            Center = center;
            Width = width;
            Height = height;
            FacesAlongZ = facesAlongZ;
            WallThickness = wallThickness;
        }
    }

    /// <summary>
    /// Builds interior walls with optional door openings.
    /// Tracks doorway positions for future NavMesh link generation.
    /// </summary>
    public sealed class InteriorWallBuilder
    {
        #region Fields

        private readonly Transform _parent;
        private readonly Vector3 _roomSize;
        private readonly float _wallThickness;
        private readonly BuildingPalette _palette;
        private readonly int _layer;
        private GameObject? _container;
        private readonly List<DoorwayInfo> _doorways = new List<DoorwayInfo>();

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new interior wall builder.
        /// </summary>
        /// <param name="parent">Parent transform for interior walls</param>
        /// <param name="roomSize">Room dimensions (width, height, depth)</param>
        /// <param name="wallThickness">Wall thickness in meters</param>
        /// <param name="palette">Material and color palette</param>
        /// <param name="layer">Physics layer for interior wall GameObjects. -1 (default) leaves them on the default layer.</param>
        public InteriorWallBuilder(Transform parent, Vector3 roomSize, float wallThickness, BuildingPalette palette, int layer = -1)
        {
            _parent = parent;
            _roomSize = roomSize;
            _wallThickness = wallThickness;
            _palette = palette;
            _layer = layer;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Recorded doorway positions from all interior walls built so far.
        /// </summary>
        public IReadOnlyList<DoorwayInfo> Doorways =>
            _doorways;

        /// <summary>
        /// Build an interior wall from a definition.
        /// </summary>
        /// <param name="def">Interior wall definition</param>
        /// <returns>The wall GameObject, or null if validation fails</returns>
        public GameObject? BuildInteriorWall(InteriorWallDefinition def)
        {
            if (!Validate(def))
                return null;

            _container ??= BuildingUtilities.CreateFolder("InteriorWalls", _parent);

            float wallLength = def.To - def.From;
            Color wallColor = def.Color ?? _palette.WallColor;
            Material? wallMaterial = def.Material ?? _palette.WallMaterial;

            var (center, size, isVertical) = GetWallTransform(def);

            string wallName = $"InteriorWall_{def.Axis}_{def.Position:F1}";

            GameObject? wall;

            if (def.Opening == null || def.Opening.Type == WallOpeningType.None)
            {
                wall = CreateSolidWall(wallName, center, size, wallColor, wallMaterial);
            }
            else if (def.Opening.Type == WallOpeningType.Door)
            {
                wall = CreateWallWithDoor(wallName, center, size, def.Opening, isVertical, wallColor, wallMaterial);
            }
            else
            {
                // Fallback: solid wall for unsupported opening types
                wall = CreateSolidWall(wallName, center, size, wallColor, wallMaterial);
            }

            if (wall != null && _layer >= 0)
            {
                wall.SetLayerRecursively(_layer);
            }

            return wall;
        }

        #endregion

        #region Private Methods — Validation

        private bool Validate(InteriorWallDefinition def)
        {
            float axisMax = def.Axis == InteriorWallAxis.X ? _roomSize.x : _roomSize.z;
            float perpMax = def.Axis == InteriorWallAxis.X ? _roomSize.z : _roomSize.x;

            if (def.Position < 0f || def.Position > perpMax)
            {
                DebugLog.Warning($"[InteriorWallBuilder] Position {def.Position} out of room bounds (0–{perpMax}). Skipping wall.");
                return false;
            }

            if (def.From >= def.To)
            {
                DebugLog.Warning($"[InteriorWallBuilder] From ({def.From}) >= To ({def.To}). Skipping wall.");
                return false;
            }

            float wallLength = def.To - def.From;
            if (wallLength < Constants.InteriorWall.MinWallLength)
            {
                DebugLog.Warning($"[InteriorWallBuilder] Wall length {wallLength:F2}m < minimum {Constants.InteriorWall.MinWallLength}m. Skipping wall.");
                return false;
            }

            if (def.Opening != null && def.Opening.Type == WallOpeningType.Door && def.Opening.Width >= wallLength)
            {
                DebugLog.Warning($"[InteriorWallBuilder] Door width ({def.Opening.Width}) >= wall length ({wallLength:F2}). Skipping wall.");
                return false;
            }

            return true;
        }

        #endregion

        #region Private Methods — Geometry

        private (Vector3 center, Vector3 size, bool isVertical) GetWallTransform(InteriorWallDefinition def)
        {
            float wallLength = def.To - def.From;
            float midAlong = (def.From + def.To) / 2f;

            // Axis.X: wall runs along X, position is Z, normal faces Z → isVertical = false
            // Axis.Z: wall runs along Z, position is X, normal faces X → isVertical = true
            if (def.Axis == InteriorWallAxis.X)
            {
                Vector3 center = new Vector3(midAlong, _roomSize.y / 2f, def.Position);
                Vector3 size = new Vector3(wallLength, _roomSize.y, _wallThickness);
                return (center, size, false);
            }
            else
            {
                Vector3 center = new Vector3(def.Position, _roomSize.y / 2f, midAlong);
                Vector3 size = new Vector3(_wallThickness, _roomSize.y, wallLength);
                return (center, size, true);
            }
        }

        private GameObject CreateSolidWall(string name, Vector3 center, Vector3 size, Color color, Material? material)
        {
            GameObject wall = PrimitiveBuilder.CreateBox(name, center, size, color, _container!.transform);
            ApplyMaterial(wall, material);
            return wall;
        }

        private GameObject CreateWallWithDoor(
            string name, Vector3 wallCenter, Vector3 wallSize,
            WallOpening opening, bool isVertical,
            Color color, Material? material)
        {
            GameObject container = BuildingUtilities.CreateFolder(name, _container!.transform);

            float wallWidth = isVertical ? wallSize.z : wallSize.x;
            float wallHeight = wallSize.y;
            float doorWidth = opening.Width;
            float doorHeight = opening.Height;
            float offset = opening.Offset;

            // Positive offset shifts door toward positive axis (right/forward)
            // Left (negative direction) gets bigger, right gets smaller
            float leftWidth = (wallWidth - doorWidth) / 2f + offset;
            float rightWidth = (wallWidth - doorWidth) / 2f - offset;

            // Door center shifted by offset along the wall axis
            Vector3 doorShift = isVertical ? Vector3.forward * offset : Vector3.right * offset;
            Vector3 shiftedCenter = wallCenter + doorShift;

            // Left segment
            if (leftWidth > Constants.InteriorWall.SegmentThreshold)
            {
                float leftCenter = doorWidth / 2f + leftWidth / 2f;
                Vector3 leftOffset = isVertical ? Vector3.back * leftCenter : Vector3.left * leftCenter;
                Vector3 leftSize = isVertical
                    ? new Vector3(_wallThickness, wallHeight, leftWidth)
                    : new Vector3(leftWidth, wallHeight, _wallThickness);
                GameObject left = PrimitiveBuilder.CreateBox($"{name}_Left", shiftedCenter + leftOffset, leftSize, color, container.transform);
                ApplyMaterial(left, material);
            }

            // Right segment
            if (rightWidth > Constants.InteriorWall.SegmentThreshold)
            {
                float rightCenter = doorWidth / 2f + rightWidth / 2f;
                Vector3 rightOffset = isVertical ? Vector3.forward * rightCenter : Vector3.right * rightCenter;
                Vector3 rightSize = isVertical
                    ? new Vector3(_wallThickness, wallHeight, rightWidth)
                    : new Vector3(rightWidth, wallHeight, _wallThickness);
                GameObject right = PrimitiveBuilder.CreateBox($"{name}_Right", shiftedCenter + rightOffset, rightSize, color, container.transform);
                ApplyMaterial(right, material);
            }

            // Top segment (wall above door)
            float topHeight = wallHeight - doorHeight;
            if (topHeight > Constants.InteriorWall.SegmentThreshold)
            {
                Vector3 topSize = isVertical
                    ? new Vector3(_wallThickness, topHeight, doorWidth)
                    : new Vector3(doorWidth, topHeight, _wallThickness);
                float topCenterY = wallHeight / 2f - topHeight / 2f;
                Vector3 topOffset = Vector3.up * topCenterY;
                GameObject top = PrimitiveBuilder.CreateBox($"{name}_Top", shiftedCenter + topOffset, topSize, color, container.transform);
                ApplyMaterial(top, material);
            }

            // Record doorway for future NavMesh (use shifted position)
            bool facesAlongZ = !isVertical; // Axis.X wall faces Z
            Vector3 doorCenter = new Vector3(shiftedCenter.x, doorHeight / 2f, shiftedCenter.z);
            var doorwayInfo = new DoorwayInfo(doorCenter, doorWidth, doorHeight, facesAlongZ, _wallThickness);
            doorwayInfo.WallContainer = container;
            _doorways.Add(doorwayInfo);

            return container;
        }

        private void ApplyMaterial(GameObject go, Material? material)
        {
            if (material != null)
            {
                Renderer r = go.GetComponent<Renderer>();
                if (r != null) r.material = material;
            }
        }

        #endregion
    }
}
