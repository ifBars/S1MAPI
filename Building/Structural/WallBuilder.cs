using System.Collections.Generic;
using S1MAPI.Building.Config;
using S1MAPI.ProceduralMesh;
using S1MAPI.ProceduralMesh.Generators.Primitives;
using S1MAPI.Utils;
using UnityEngine;
using S1MAPI.S1;

namespace S1MAPI.Building.Structural
{
    /// <summary>
    /// Specifies which side of the room a wall is on.
    /// </summary>
    public enum WallSide
    {
        /// <summary>The north wall (positive Z direction).</summary>
        North,
        /// <summary>The south wall (zero or negative Z direction).</summary>
        South,
        /// <summary>The east wall (positive X direction).</summary>
        East,
        /// <summary>The west wall (zero or negative X direction).</summary>
        West
    }

    /// <summary>
    /// Visual style for generated stairs.
    /// </summary>
    public enum StairStyle
    {
        /// <summary>Solid concrete box steps (default).</summary>
        Solid,
        /// <summary>Two-tone wood stairs with risers and tread planks (closed riser style).</summary>
        ClosedRiser,
        /// <summary>Open plank treads on diagonal stringer beams (open stringer style).</summary>
        OpenStringer
    }

    /// <summary>
    /// Type of opening in a wall.
    /// </summary>
    public enum WallOpeningType
    {
        /// <summary>No opening (solid wall).</summary>
        None,
        /// <summary>A door opening.</summary>
        Door,
        /// <summary>A window opening.</summary>
        Window
    }

    /// <summary>
    /// Configuration for a wall opening (door or window).
    /// </summary>
    public sealed class WallOpening
    {
        /// <summary>The type of opening (door, window, or none).</summary>
        public WallOpeningType Type { get; set; } = WallOpeningType.None;
        /// <summary>The width of the opening in meters.</summary>
        public float Width { get; set; } = 2.0f;
        /// <summary>The height of the opening in meters.</summary>
        public float Height { get; set; } = 2.2f;
        /// <summary>The bottom offset (sill height) in meters. Used for windows.</summary>
        public float BottomOffset { get; set; } = 0f;
        /// <summary>Optional window for the left side segment of a door wall.</summary>
        public WallOpening? LeftWindow { get; set; }
        /// <summary>Optional window for the right side segment of a door wall.</summary>
        public WallOpening? RightWindow { get; set; }
        /// <summary>Number of window panes across the opening (default 1).</summary>
        public int Count { get; set; } = 1;
        /// <summary>Width of the wall divider between adjacent panes in meters.</summary>
        public float DividerWidth { get; set; } = Constants.Window.DefaultDividerWidth;
        /// <summary>Optional glass material override. When null, uses Materials.WindowGlass.</summary>
        public Material? GlassMaterial { get; set; }
        /// <summary>
        /// Lateral offset from center along the wall in meters.
        /// Positive shifts toward the right/forward end, negative toward left/back.
        /// </summary>
        public float Offset { get; set; } = 0f;
        /// <summary>Optional frame color override for window openings. Defaults to dark gray when null.</summary>
        public Color? FrameColor { get; set; }
        /// <summary>Optional frame material override for window openings.</summary>
        public Material? FrameMaterial { get; set; }

        /// <summary>
        /// Creates a door opening configuration.
        /// </summary>
        /// <param name="width">The door width in meters (default 2.0).</param>
        /// <param name="height">The door height in meters (default 2.2).</param>
        /// <param name="offset">Lateral offset from center along the wall in meters (default 0, centered).</param>
        /// <returns>A new WallOpening configured as a door.</returns>
        public static WallOpening Door(float width = 2.0f, float height = 2.2f, float offset = 0f) => new()
        {
            Type = WallOpeningType.Door,
            Width = width,
            Height = height,
            BottomOffset = 0f,
            Offset = offset
        };

        /// <summary>
        /// Creates a window opening configuration.
        /// </summary>
        /// <param name="width">The window width in meters (default 2.5).</param>
        /// <param name="height">The window height in meters (default 2.0).</param>
        /// <param name="sillHeight">The sill height from the floor in meters (default 0.8).</param>
        /// <param name="count">Number of window panes to distribute across the opening width (default 1).</param>
        /// <param name="dividerWidth">Width of wall dividers between adjacent panes in meters (default 0.15).</param>
        /// <param name="glassMaterial">Optional glass material override. Defaults to Materials.WindowGlass when null.</param>
        /// <param name="offset">Lateral offset from center along the wall in meters (default 0, centered).</param>
        /// <param name="frameMaterial">Optional frame material override.</param>
        /// <param name="frameColor">Optional frame color override. Defaults to dark gray when null.</param>
        /// <returns>A new WallOpening configured as a window.</returns>
        public static WallOpening Window(
            float width = 2.5f, float height = 2.0f, float sillHeight = 0.8f,
            int count = 1, float dividerWidth = Constants.Window.DefaultDividerWidth,
            Material? glassMaterial = null, float offset = 0f,
            Material? frameMaterial = null, Color? frameColor = null) => new()
        {
            Type = WallOpeningType.Window,
            Width = width,
            Height = height,
            BottomOffset = sillHeight,
            Count = count,
            DividerWidth = dividerWidth,
            GlassMaterial = glassMaterial,
            Offset = offset,
            FrameMaterial = frameMaterial,
            FrameColor = frameColor
        };

        /// <summary>
        /// Creates a door opening with windows on the left and/or right side segments.
        /// </summary>
        /// <param name="doorWidth">The door width in meters (default 2.0).</param>
        /// <param name="doorHeight">The door height in meters (default 2.2).</param>
        /// <param name="leftWindow">Window config for left side, or null for no window.</param>
        /// <param name="rightWindow">Window config for right side, or null for no window.</param>
        /// <returns>A new WallOpening configured as a door with side windows.</returns>
        public static WallOpening DoorWithWindows(
            float doorWidth = 2.0f, float doorHeight = 2.2f,
            WallOpening? leftWindow = null, WallOpening? rightWindow = null)
        {
            // When neither window is specified, use defaults on both sides
            if (leftWindow == null && rightWindow == null)
            {
                WallOpening defaultWindow = Window(width: 1.5f, height: 1.5f, sillHeight: 0.8f);
                leftWindow = defaultWindow;
                rightWindow = defaultWindow;
            }

            return new WallOpening
            {
                Type = WallOpeningType.Door,
                Width = doorWidth,
                Height = doorHeight,
                BottomOffset = 0f,
                LeftWindow = leftWindow,
                RightWindow = rightWindow
            };
        }
    }

    /// <summary>
    /// Builds walls with optional openings (doors, windows).
    /// Extracted from SemanticBuildingBuilder for SRP compliance.
    /// </summary>
    public sealed class WallBuilder
    {
        #region Fields

        private static readonly Color FrameColor = new Color(0.1f, 0.1f, 0.1f);
        private static readonly Color GlassTint = new Color(0.7f, 0.9f, 1f);

        private readonly Transform _parent;
        private readonly Vector3 _roomSize;
        private readonly float _wallThickness;
        private readonly BuildingPalette _palette;
        private IReadOnlyDictionary<WallSide, WallAppearance>? _wallOverrides;
        private GameObject? _wallsContainer;

        // Per-wall interior material state, set in BuildWall() and read by CreateWallSegment()
        private WallSide _currentSide;
        private Material? _currentInteriorMaterial;
        private Color _currentInteriorColor;
        private bool _hasInteriorOverride;

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new wall builder.
        /// </summary>
        /// <param name="parent">Parent transform for walls</param>
        /// <param name="roomSize">Room dimensions (width, height, depth)</param>
        /// <param name="wallThickness">Wall thickness in meters</param>
        /// <param name="palette">Material and color palette</param>
        public WallBuilder(Transform parent, Vector3 roomSize, float wallThickness, BuildingPalette palette)
        {
            _parent = parent;
            _roomSize = roomSize;
            _wallThickness = wallThickness;
            _palette = palette;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Build all four walls with specified openings.
        /// </summary>
        /// <param name="northOpening">Opening for north wall (or null for solid)</param>
        /// <param name="southOpening">Opening for south wall (or null for solid)</param>
        /// <param name="eastOpening">Opening for east wall (or null for solid)</param>
        /// <param name="westOpening">Opening for west wall (or null for solid)</param>
        /// <returns>The walls container GameObject</returns>
        public GameObject BuildWalls(
            WallOpening? northOpening = null,
            WallOpening? southOpening = null,
            WallOpening? eastOpening = null,
            WallOpening? westOpening = null)
        {
            _wallsContainer = BuildingUtilities.CreateFolder("Walls", _parent);

            // North wall (at Z = depth)
            BuildWall(WallSide.North, northOpening);

            // South wall (at Z = 0)
            BuildWall(WallSide.South, southOpening);

            // East wall (at X = width)
            BuildWall(WallSide.East, eastOpening);

            // West wall (at X = 0)
            BuildWall(WallSide.West, westOpening);

            return _wallsContainer;
        }

        /// <summary>
        /// Build all four walls with specified openings and per-wall appearance overrides.
        /// </summary>
        /// <param name="northOpening">Opening for north wall (or null for solid)</param>
        /// <param name="southOpening">Opening for south wall (or null for solid)</param>
        /// <param name="eastOpening">Opening for east wall (or null for solid)</param>
        /// <param name="westOpening">Opening for west wall (or null for solid)</param>
        /// <param name="wallOverrides">Per-wall material/color overrides (null entries use palette defaults)</param>
        /// <returns>The walls container GameObject</returns>
        public GameObject BuildWalls(
            WallOpening? northOpening,
            WallOpening? southOpening,
            WallOpening? eastOpening,
            WallOpening? westOpening,
            IReadOnlyDictionary<WallSide, WallAppearance> wallOverrides)
        {
            _wallOverrides = wallOverrides;
            try
            {
                return BuildWalls(northOpening, southOpening, eastOpening, westOpening);
            }
            finally
            {
                _wallOverrides = null;
            }
        }

        /// <summary>
        /// Build a single wall with optional opening.
        /// </summary>
        /// <param name="side">Which side of the room</param>
        /// <param name="opening">Opening configuration (or null for solid wall)</param>
        /// <returns>The wall GameObject(s)</returns>
        public GameObject BuildWall(WallSide side, WallOpening? opening = null)
        {
            _wallsContainer ??= BuildingUtilities.CreateFolder("Walls", _parent);

            Color wallColor = GetWallColor(side);
            Material? wallMaterial = GetWallMaterial(side);

            _currentSide = side;
            _currentInteriorMaterial = GetWallInteriorMaterial(side);
            _currentInteriorColor = GetWallInteriorColor(side);
            _hasInteriorOverride = _currentInteriorMaterial != null || HasExplicitInteriorColor(side);

            var (position, size, isVertical) = GetWallTransform(side);
            string wallName = $"{side}Wall";

            if (opening == null || opening.Type == WallOpeningType.None)
            {
                return CreateSolidWall(wallName, position, size, wallColor, wallMaterial);
            }

            return opening.Type switch
            {
                WallOpeningType.Door when opening.LeftWindow != null || opening.RightWindow != null
                    => CreateWallWithDoorAndWindows(wallName, position, size, opening, isVertical, wallColor, wallMaterial),
                WallOpeningType.Door => CreateWallWithDoor(wallName, position, size, opening, isVertical, wallColor, wallMaterial),
                WallOpeningType.Window => CreateWallWithWindow(wallName, position, size, opening, isVertical, wallColor, wallMaterial),
                _ => CreateSolidWall(wallName, position, size, wallColor, wallMaterial)
            };
        }

        #endregion

        #region Private Methods

        private (Vector3 position, Vector3 size, bool isVertical) GetWallTransform(WallSide side)
        {
            // N/S walls extend by wallThickness on each end to cover corner gaps with E/W walls
            float extendedWidth = _roomSize.x + _wallThickness;

            return side switch
            {
                WallSide.North => (
                    new Vector3(_roomSize.x / 2f, _roomSize.y / 2f, _roomSize.z),
                    new Vector3(extendedWidth, _roomSize.y, _wallThickness),
                    false
                ),
                WallSide.South => (
                    new Vector3(_roomSize.x / 2f, _roomSize.y / 2f, 0f),
                    new Vector3(extendedWidth, _roomSize.y, _wallThickness),
                    false
                ),
                WallSide.East => (
                    new Vector3(_roomSize.x, _roomSize.y / 2f, _roomSize.z / 2f),
                    new Vector3(_wallThickness, _roomSize.y, _roomSize.z),
                    true
                ),
                WallSide.West => (
                    new Vector3(0f, _roomSize.y / 2f, _roomSize.z / 2f),
                    new Vector3(_wallThickness, _roomSize.y, _roomSize.z),
                    true
                ),
                _ => throw new System.ArgumentException($"Unknown wall side: {side}")
            };
        }

        private GameObject CreateSolidWall(string name, Vector3 position, Vector3 size, Color wallColor, Material? wallMaterial)
        {
            return CreateWallSegment(name, position, size, wallColor, wallMaterial, _wallsContainer!.transform);
        }

        private GameObject CreateWallWithDoor(string name, Vector3 wallCenter, Vector3 wallSize, WallOpening opening, bool isVertical, Color wallColor, Material? wallMaterial)
        {
            GameObject container = BuildingUtilities.CreateFolder(name, _wallsContainer!.transform);

            float wallWidth = isVertical ? wallSize.z : wallSize.x;
            float wallHeight = wallSize.y;
            float doorWidth = opening.Width;
            float doorHeight = opening.Height;

            float halfMargin = (wallWidth - doorWidth) / 2f;
            float offset = Mathf.Clamp(opening.Offset, -halfMargin, halfMargin);
            if (!Mathf.Approximately(offset, opening.Offset))
                DebugLog.Warning(
                    $"[WallBuilder] {_currentSide} wall: door offset {opening.Offset:F2}m " +
                    $"exceeds wall margin (±{halfMargin:F2}m) and was clamped to {offset:F2}m.");

            // Positive offset shifts door toward positive axis (right/forward)
            // Left (negative direction) gets bigger, right gets smaller
            float leftWidth = halfMargin + offset;
            float rightWidth = halfMargin - offset;

            // Door center shifted by offset along the wall axis
            Vector3 doorShift = isVertical ? Vector3.forward * offset : Vector3.right * offset;

            // Left segment
            if (leftWidth > 0f)
            {
                float leftCenter = doorWidth / 2f + leftWidth / 2f;
                Vector3 leftOffset = isVertical ? Vector3.back * leftCenter : Vector3.left * leftCenter;
                Vector3 leftSize = isVertical
                    ? new Vector3(_wallThickness, wallHeight, leftWidth)
                    : new Vector3(leftWidth, wallHeight, _wallThickness);
                CreateWallSegment($"{name}_Left", wallCenter + doorShift + leftOffset, leftSize, wallColor, wallMaterial, container.transform);
            }

            // Right segment
            if (rightWidth > 0f)
            {
                float rightCenter = doorWidth / 2f + rightWidth / 2f;
                Vector3 rightOffset = isVertical ? Vector3.forward * rightCenter : Vector3.right * rightCenter;
                Vector3 rightSize = isVertical
                    ? new Vector3(_wallThickness, wallHeight, rightWidth)
                    : new Vector3(rightWidth, wallHeight, _wallThickness);
                CreateWallSegment($"{name}_Right", wallCenter + doorShift + rightOffset, rightSize, wallColor, wallMaterial, container.transform);
            }

            // Top segment (wall above door)
            float topHeight = wallHeight - doorHeight;
            if (topHeight > 0f)
            {
                Vector3 topSize = isVertical
                    ? new Vector3(_wallThickness, topHeight, doorWidth)
                    : new Vector3(doorWidth, topHeight, _wallThickness);
                float topCenterY = wallHeight / 2f - topHeight / 2f;
                Vector3 topOffset = Vector3.up * topCenterY;
                CreateWallSegment($"{name}_Top", wallCenter + doorShift + topOffset, topSize, wallColor, wallMaterial, container.transform);
            }

            return container;
        }

        private GameObject CreateWallWithDoorAndWindows(string name, Vector3 wallCenter, Vector3 wallSize, WallOpening opening, bool isVertical, Color wallColor, Material? wallMaterial)
        {
            GameObject container = BuildingUtilities.CreateFolder(name, _wallsContainer!.transform);

            float wallWidth = isVertical ? wallSize.z : wallSize.x;
            float wallHeight = wallSize.y;
            float doorWidth = opening.Width;
            float doorHeight = opening.Height;
            float offset = opening.Offset;

            float leftSideWidth = (wallWidth - doorWidth) / 2f + offset;
            float rightSideWidth = (wallWidth - doorWidth) / 2f - offset;

            Vector3 doorShift = isVertical ? Vector3.forward * offset : Vector3.right * offset;
            Vector3 shiftedCenter = wallCenter + doorShift;

            // Left side (may contain a window)
            if (leftSideWidth > 0f)
            {
                BuildDoorSideSegment(name, "_Left", shiftedCenter, doorWidth, wallHeight,
                    leftSideWidth, opening.LeftWindow, isVertical, true, container.transform, wallColor, wallMaterial);
            }

            // Right side (may contain a window)
            if (rightSideWidth > 0f)
            {
                BuildDoorSideSegment(name, "_Right", shiftedCenter, doorWidth, wallHeight,
                    rightSideWidth, opening.RightWindow, isVertical, false, container.transform, wallColor, wallMaterial);
            }

            // Top segment (wall above door) — same as CreateWallWithDoor
            float topHeight = wallHeight - doorHeight;
            if (topHeight > 0f)
            {
                Vector3 topSize = isVertical
                    ? new Vector3(_wallThickness, topHeight, doorWidth)
                    : new Vector3(doorWidth, topHeight, _wallThickness);
                float topCenterY = wallHeight / 2f - topHeight / 2f;
                Vector3 topOffset = Vector3.up * topCenterY;
                CreateWallSegment($"{name}_Top", shiftedCenter + topOffset, topSize, wallColor, wallMaterial, container.transform);
            }

            return container;
        }

        private void BuildDoorSideSegment(
            string wallName, string suffix, Vector3 wallCenter,
            float doorWidth, float wallHeight, float fullSideWidth,
            WallOpening? sideWindow, bool isVertical, bool isLeftSide, Transform parent,
            Color wallColor, Material? wallMaterial)
        {
            float dirSign = isLeftSide ? -1f : 1f;

            if (sideWindow == null || fullSideWidth < Constants.Window.MinDoorSideWidth)
            {
                // No window — create single solid segment (same as CreateWallWithDoor)
                Vector3 offset = isVertical
                    ? new Vector3(0f, 0f, dirSign * (doorWidth / 2f + fullSideWidth / 2f))
                    : new Vector3(dirSign * (doorWidth / 2f + fullSideWidth / 2f), 0f, 0f);
                Vector3 size = isVertical
                    ? new Vector3(_wallThickness, wallHeight, fullSideWidth)
                    : new Vector3(fullSideWidth, wallHeight, _wallThickness);
                CreateWallSegment($"{wallName}{suffix}", wallCenter + offset, size, wallColor, wallMaterial, parent);
                return;
            }

            // With window: small strip adjacent to the door (for InsetDoorWallSegments), window section gets the rest
            float stripWidth = Mathf.Min(Constants.Window.MaxDoorStripWidth, fullSideWidth - sideWindow.Width - Constants.Window.DoorStripMargin);
            stripWidth = Mathf.Max(Constants.Window.MinDoorStripWidth, stripWidth);
            float windowSectionWidth = fullSideWidth - stripWidth;

            // Solid strip next to the door (keeps {wallName}_Left / _Right name for InsetDoorWallSegments)
            float stripCenterOffset = doorWidth / 2f + stripWidth / 2f;
            Vector3 stripOffset = isVertical
                ? new Vector3(0f, 0f, dirSign * stripCenterOffset)
                : new Vector3(dirSign * stripCenterOffset, 0f, 0f);
            Vector3 stripSize = isVertical
                ? new Vector3(_wallThickness, wallHeight, stripWidth)
                : new Vector3(stripWidth, wallHeight, _wallThickness);
            CreateWallSegment($"{wallName}{suffix}", wallCenter + stripOffset, stripSize, wallColor, wallMaterial, parent);

            // Window section in the remaining area (no overlap with strip so InsetDoorWallSegments works)
            float winSectionCenterOffset = doorWidth / 2f + stripWidth + windowSectionWidth / 2f;
            Vector3 winSectionCenter = wallCenter + (isVertical
                ? new Vector3(0f, 0f, dirSign * winSectionCenterOffset)
                : new Vector3(dirSign * winSectionCenterOffset, 0f, 0f));

            // Shift window toward door by stripWidth/2 so it centers in the full side width
            float windowOffset = -dirSign * stripWidth / 2f;
            CreateWindowInSection($"{wallName}{suffix}Win", winSectionCenter, windowSectionWidth, wallHeight,
                sideWindow, isVertical, parent, windowOffset, wallColor, wallMaterial);
        }

        private GameObject CreateWallWithWindow(string name, Vector3 wallCenter, Vector3 wallSize, WallOpening opening, bool isVertical, Color wallColor, Material? wallMaterial)
        {
            GameObject container = BuildingUtilities.CreateFolder(name, _wallsContainer!.transform);
            float wallWidth = isVertical ? wallSize.z : wallSize.x;
            CreateWindowInSection(name, wallCenter, wallWidth, wallSize.y, opening, isVertical, container.transform, opening.Offset, wallColor, wallMaterial);
            return container;
        }

        private void CreateWindowInSection(
            string namePrefix, Vector3 sectionCenter,
            float sectionWidth, float sectionHeight,
            WallOpening window, bool isVertical, Transform parent,
            float windowOffset, Color wallColor, Material? wallMaterial)
        {
            int paneCount = Mathf.Max(1, window.Count);
            float windowWidth;
            if (paneCount > 1)
            {
                // Multi-pane: Width is per-pane width (capped), equal gaps for sides and dividers
                float perPane = Mathf.Min(window.Width, Mathf.Min(sectionWidth - Constants.Window.SideMargin, Constants.Window.MaxPaneWidth));
                float equalGap = (sectionWidth - paneCount * perPane) / (paneCount + 1);

                // Enforce minimum gap; shrink panes if needed
                if (equalGap < Constants.Window.MinGap)
                {
                    equalGap = Constants.Window.MinGap;
                    perPane = (sectionWidth - (paneCount + 1) * equalGap) / paneCount;
                }

                // Auto-reduce count if panes would be too narrow
                while (perPane < Constants.Window.MinPaneWidth && paneCount > 1)
                {
                    paneCount--;
                    equalGap = (sectionWidth - paneCount * perPane) / (paneCount + 1);
                    if (equalGap < Constants.Window.MinGap)
                    {
                        equalGap = Constants.Window.MinGap;
                        perPane = (sectionWidth - (paneCount + 1) * equalGap) / paneCount;
                    }
                }

                // Band spans all panes + inner gaps; outer gaps become sideWidth
                windowWidth = paneCount * perPane + (paneCount - 1) * equalGap;
            }
            else
            {
                windowWidth = Mathf.Min(window.Width, sectionWidth - Constants.Window.SideMargin);
            }

            float windowHeight = Mathf.Min(window.Height, sectionHeight - Constants.Window.VerticalMargin);
            float windowBottom = window.BottomOffset;

            float topHeight = sectionHeight - (windowBottom + windowHeight);
            float sideWidth = (sectionWidth - windowWidth) / 2f;
            float halfHeight = sectionHeight / 2f;
            float windowCenterY = (windowBottom + windowHeight / 2f) - halfHeight;

            // Clamp window offset so the window cannot extend past the section bounds
            float clampedOffset = Mathf.Clamp(windowOffset, -sideWidth, sideWidth);
            if (!Mathf.Approximately(clampedOffset, windowOffset))
                DebugLog.Warning(
                    $"[WallBuilder] {_currentSide} wall: window offset {windowOffset:F2}m " +
                    $"exceeds section margin (±{sideWidth:F2}m) and was clamped to {clampedOffset:F2}m.");

            // Window center shifted along wall axis (positive = +Z for vertical, +X for horizontal)
            Vector3 winShift = isVertical
                ? new Vector3(0f, 0f, clampedOffset)
                : new Vector3(clampedOffset, 0f, 0f);
            Vector3 windowCenter = sectionCenter + winShift;

            // Bottom segment (sill) — full section width, no shift
            if (windowBottom > Constants.Window.SegmentThreshold)
            {
                Vector3 bottomSize = isVertical
                    ? new Vector3(_wallThickness, windowBottom, sectionWidth)
                    : new Vector3(sectionWidth, windowBottom, _wallThickness);
                Vector3 bottomOffset = Vector3.down * (halfHeight - windowBottom / 2f);
                CreateWallSegment($"{namePrefix}_Bottom", sectionCenter + bottomOffset, bottomSize, wallColor, wallMaterial, parent);
            }

            // Top segment (header) — full section width, no shift
            if (topHeight > Constants.Window.SegmentThreshold)
            {
                Vector3 topSize = isVertical
                    ? new Vector3(_wallThickness, topHeight, sectionWidth)
                    : new Vector3(sectionWidth, topHeight, _wallThickness);
                Vector3 topOffset = Vector3.up * (halfHeight - topHeight / 2f);
                CreateWallSegment($"{namePrefix}_Top", sectionCenter + topOffset, topSize, wallColor, wallMaterial, parent);
            }

            // Side segments — asymmetric widths when window is offset
            // For isVertical: +Z side = sideWidth - offset, -Z side = sideWidth + offset
            // For non-vertical: -X side = sideWidth + offset, +X side = sideWidth - offset
            float posSideWidth = sideWidth - clampedOffset;
            float negSideWidth = sideWidth + clampedOffset;
            float leftSideWidth = isVertical ? posSideWidth : negSideWidth;
            float rightSideWidth = isVertical ? negSideWidth : posSideWidth;

            if (leftSideWidth > Constants.Window.SegmentThreshold)
            {
                Vector3 leftSize = isVertical
                    ? new Vector3(_wallThickness, windowHeight, leftSideWidth)
                    : new Vector3(leftSideWidth, windowHeight, _wallThickness);
                Vector3 leftOffset = isVertical
                    ? new Vector3(0f, windowCenterY, windowWidth / 2f + leftSideWidth / 2f)
                    : new Vector3(-(windowWidth / 2f + leftSideWidth / 2f), windowCenterY, 0f);
                CreateWallSegment($"{namePrefix}_Left", windowCenter + leftOffset, leftSize, wallColor, wallMaterial, parent);
            }

            if (rightSideWidth > Constants.Window.SegmentThreshold)
            {
                Vector3 rightSize = isVertical
                    ? new Vector3(_wallThickness, windowHeight, rightSideWidth)
                    : new Vector3(rightSideWidth, windowHeight, _wallThickness);
                Vector3 rightOffset = isVertical
                    ? new Vector3(0f, windowCenterY, -(windowWidth / 2f + rightSideWidth / 2f))
                    : new Vector3(windowWidth / 2f + rightSideWidth / 2f, windowCenterY, 0f);
                CreateWallSegment($"{namePrefix}_Right", windowCenter + rightOffset, rightSize, wallColor, wallMaterial, parent);
            }

            // Multi-pane window rendering
            // For count > 1: divider width == sideWidth (equal gaps by construction)
            float dividerW = paneCount > 1 ? sideWidth : window.DividerWidth;
            float paneWidth = paneCount > 1
                ? (windowWidth - (paneCount - 1) * dividerW) / paneCount
                : windowWidth;

            float bandStart = -windowWidth / 2f + paneWidth / 2f;
            Color frameColor = window.FrameColor ?? FrameColor;

            for (int i = 0; i < paneCount; i++)
            {
                float paneOffset = bandStart + i * (paneWidth + dividerW);

                Vector3 paneShift = isVertical
                    ? new Vector3(0f, 0f, paneOffset)
                    : new Vector3(paneOffset, 0f, 0f);
                Vector3 paneCenter = windowCenter + paneShift;

                // Frame for this pane
                string framePrefix = paneCount > 1 ? $"Frame{i}_" : "Frame";
                CreateWindowFrame(parent, paneCenter, paneWidth, windowHeight, windowCenterY, isVertical, frameColor, window.FrameMaterial, framePrefix);

                // Glass pane
                Vector3 glassSize = isVertical
                    ? new Vector3(_wallThickness * 0.2f, windowHeight - 0.1f, paneWidth - 0.1f)
                    : new Vector3(paneWidth - 0.1f, windowHeight - 0.1f, _wallThickness * 0.2f);

                string paneSuffix = paneCount > 1 ? $"_{i}" : "";
                GameObject glass = PrimitiveBuilder.CreateBox(
                    $"{namePrefix}_WindowGlass{paneSuffix}",
                    paneCenter + new Vector3(0f, windowCenterY, 0f),
                    glassSize, GlassTint, parent);

                Material glassMat = window.GlassMaterial ?? Materials.WindowGlass;
                if (glassMat != null)
                {
                    Renderer r = glass.GetComponent<Renderer>();
                    if (r != null) r.material = glassMat;
                }

                // Divider wall between this pane and the next
                if (i < paneCount - 1)
                {
                    float dividerOffset = paneOffset + paneWidth / 2f + dividerW / 2f;
                    Vector3 dividerShift = isVertical
                        ? new Vector3(0f, 0f, dividerOffset)
                        : new Vector3(dividerOffset, 0f, 0f);
                    Vector3 dividerSize = isVertical
                        ? new Vector3(_wallThickness, windowHeight, dividerW)
                        : new Vector3(dividerW, windowHeight, _wallThickness);

                    CreateWallSegment(
                        $"{namePrefix}_Divider_{i}",
                        windowCenter + dividerShift + new Vector3(0f, windowCenterY, 0f),
                        dividerSize, wallColor, wallMaterial, parent);
                }
            }
        }

        private void CreateWindowFrame(Transform parent, Vector3 wallCenter, float windowWidth, float windowHeight, float windowCenterY, bool isVertical, Color color, Material? material = null, string namePrefix = "Frame")
        {
            float frameDepth = Constants.Window.FrameDepth;
            float frameWidth = Constants.Window.FrameWidth;

            // Top frame
            Vector3 topFrameSize = isVertical
                ? new Vector3(_wallThickness + frameDepth, frameWidth, windowWidth)
                : new Vector3(windowWidth, frameWidth, _wallThickness + frameDepth);
            GameObject top = PrimitiveBuilder.CreateBox($"{namePrefix}Top", wallCenter + new Vector3(0f, windowCenterY + windowHeight / 2f - frameWidth / 2f, 0f), topFrameSize, color, parent);

            // Bottom frame
            GameObject bottom = PrimitiveBuilder.CreateBox($"{namePrefix}Bottom", wallCenter + new Vector3(0f, windowCenterY - windowHeight / 2f + frameWidth / 2f, 0f), topFrameSize, color, parent);

            // Side frames
            Vector3 sideFrameSize = isVertical
                ? new Vector3(_wallThickness + frameDepth, windowHeight - 2 * frameWidth, frameWidth)
                : new Vector3(frameWidth, windowHeight - 2 * frameWidth, _wallThickness + frameDepth);

            float sideOffset = windowWidth / 2f - frameWidth / 2f;
            Vector3 leftFrameOffset = isVertical ? new Vector3(0f, windowCenterY, sideOffset) : new Vector3(-sideOffset, windowCenterY, 0f);
            Vector3 rightFrameOffset = isVertical ? new Vector3(0f, windowCenterY, -sideOffset) : new Vector3(sideOffset, windowCenterY, 0f);

            GameObject left = PrimitiveBuilder.CreateBox($"{namePrefix}Left", wallCenter + leftFrameOffset, sideFrameSize, color, parent);
            GameObject right = PrimitiveBuilder.CreateBox($"{namePrefix}Right", wallCenter + rightFrameOffset, sideFrameSize, color, parent);

            if (material != null)
            {
                ApplyFrameMaterial(top, material);
                ApplyFrameMaterial(bottom, material);
                ApplyFrameMaterial(left, material);
                ApplyFrameMaterial(right, material);
            }
        }

        private Color GetWallColor(WallSide side)
        {
            if (_wallOverrides != null &&
                _wallOverrides.TryGetValue(side, out WallAppearance? appearance) &&
                appearance.Color.HasValue)
            {
                return appearance.Color.Value;
            }
            return _palette.WallColor;
        }

        private Material? GetWallMaterial(WallSide side)
        {
            if (_wallOverrides != null &&
                _wallOverrides.TryGetValue(side, out WallAppearance? appearance) &&
                appearance.Material != null)
            {
                return appearance.Material;
            }
            return _palette.WallMaterial;
        }

        private void ApplyWallMaterial(GameObject wall, Material? material)
        {
            if (material != null)
            {
                Renderer r = wall.GetComponent<Renderer>();
                if (r != null) r.material = material;
            }
        }

        private void ApplyFrameMaterial(GameObject frame, Material material)
        {
            Renderer r = frame.GetComponent<Renderer>();
            if (r != null) r.material = material;
        }

        private Material? GetWallInteriorMaterial(WallSide side)
        {
            if (_wallOverrides != null &&
                _wallOverrides.TryGetValue(side, out WallAppearance? appearance) &&
                appearance.InteriorMaterial != null)
            {
                return appearance.InteriorMaterial;
            }
            return _palette.InteriorWallMaterial;
        }

        private Color GetWallInteriorColor(WallSide side)
        {
            if (_wallOverrides != null &&
                _wallOverrides.TryGetValue(side, out WallAppearance? appearance) &&
                appearance.InteriorColor.HasValue)
            {
                return appearance.InteriorColor.Value;
            }
            return _palette.InteriorWallColor ?? _palette.WallColor;
        }

        private bool HasExplicitInteriorColor(WallSide side)
        {
            if (_wallOverrides != null &&
                _wallOverrides.TryGetValue(side, out WallAppearance? appearance) &&
                appearance.InteriorColor.HasValue)
                return true;
            return _palette.InteriorWallColor.HasValue;
        }

        /// <summary>
        /// Unified wall segment creation. When an interior override is active,
        /// creates a dual-material mesh; otherwise delegates to the standard
        /// <see cref="PrimitiveBuilder.CreateBox"/> path.
        /// </summary>
        private GameObject CreateWallSegment(string name, Vector3 position, Vector3 size,
            Color wallColor, Material? wallMaterial, Transform parent)
        {
            if (_hasInteriorOverride)
                return CreateDualMaterialBox(name, position, size, wallColor, wallMaterial, parent);

            GameObject wall = PrimitiveBuilder.CreateBox(name, position, size, wallColor, parent);
            ApplyWallMaterial(wall, wallMaterial);
            return wall;
        }

        /// <summary>
        /// Creates a wall segment with a dual-submesh mesh so the interior (room-facing) face
        /// can use a different material from the exterior and edge faces.
        /// </summary>
        private GameObject CreateDualMaterialBox(string name, Vector3 position, Vector3 size,
            Color exteriorColor, Material? exteriorMaterial, Transform parent)
        {
            var interiorFace = _currentSide switch
            {
                WallSide.North => DualMaterialBoxGenerator.InteriorFace.NegZ,
                WallSide.South => DualMaterialBoxGenerator.InteriorFace.PosZ,
                WallSide.East => DualMaterialBoxGenerator.InteriorFace.NegX,
                WallSide.West => DualMaterialBoxGenerator.InteriorFace.PosX,
                _ => DualMaterialBoxGenerator.InteriorFace.NegZ
            };

            Mesh mesh = DualMaterialBoxGenerator.Generate(interiorFace);

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = position;
            go.transform.localScale = size;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.allowOcclusionWhenDynamic = false;

            Material extMat = exteriorMaterial ?? MaterialPresets.Opaque(exteriorColor);
            Material intMat = _currentInteriorMaterial ?? MaterialPresets.Opaque(_currentInteriorColor);

            mr.materials = new Material[] { extMat, intMat };

            go.AddComponent<BoxCollider>();

            return go;
        }

        #endregion
    }
}
