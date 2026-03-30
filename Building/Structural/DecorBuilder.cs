using System.Collections.Generic;
using S1MAPI.Building.Config;
using S1MAPI.ProceduralMesh;
using S1MAPI.Utils;
using UnityEngine;

namespace S1MAPI.Building.Structural
{
    /// <summary>
    /// Creates decorative building elements like trim, pillars, and foundations.
    /// Extracted from SemanticBuildingBuilder for SRP compliance.
    /// </summary>
    public sealed class DecorBuilder
    {
        #region Fields

        private readonly Transform _parent;
        private readonly Vector3 _roomSize;
        private readonly BuildingPalette _palette;

        private float _foundationClearanceX;
        private float _foundationClearanceZ;

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new decor builder.
        /// </summary>
        /// <param name="parent">Parent transform for decor elements</param>
        /// <param name="roomSize">Room dimensions</param>
        /// <param name="palette">Material and color palette</param>
        public DecorBuilder(Transform parent, Vector3 roomSize, BuildingPalette palette)
        {
            _parent = parent;
            _roomSize = roomSize;
            _palette = palette;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Add decorative trim around the top of the building.
        /// </summary>
        /// <param name="height">Height of the trim in meters</param>
        /// <param name="material">Optional material override</param>
        /// <returns>The trim container GameObject</returns>
        public GameObject AddRoofTrim(float height = 0.3f, Material? material = null)
        {
            float wallThickness = 0.2f;
            float trimDepth = wallThickness + 0.1f;
            GameObject container = BuildingUtilities.CreateFolder("RoofTrim", _parent);

            Color color = _palette.TrimColor;
            float yPos = _roomSize.y - height / 2f;

            // North
            GameObject north = PrimitiveBuilder.CreateBox("NorthTrim",
                new Vector3(_roomSize.x / 2f, yPos, _roomSize.z),
                new Vector3(_roomSize.x + trimDepth, height, trimDepth),
                color, container.transform);

            // South
            GameObject south = PrimitiveBuilder.CreateBox("SouthTrim",
                new Vector3(_roomSize.x / 2f, yPos, 0f),
                new Vector3(_roomSize.x + trimDepth, height, trimDepth),
                color, container.transform);

            // East
            GameObject east = PrimitiveBuilder.CreateBox("EastTrim",
                new Vector3(_roomSize.x, yPos, _roomSize.z / 2f),
                new Vector3(trimDepth, height, _roomSize.z - trimDepth),
                color, container.transform);

            // West
            GameObject west = PrimitiveBuilder.CreateBox("WestTrim",
                new Vector3(0f, yPos, _roomSize.z / 2f),
                new Vector3(trimDepth, height, _roomSize.z - trimDepth),
                color, container.transform);

            // Apply material
            Material? mat = material ?? _palette.TrimMaterial;
            if (mat != null)
            {
                ApplyMaterial(north, mat);
                ApplyMaterial(south, mat);
                ApplyMaterial(east, mat);
                ApplyMaterial(west, mat);
            }

            return container;
        }

        /// <summary>
        /// Add a secondary trim above the main roof trim (parapet style).
        /// </summary>
        /// <param name="height">Height of the trim in meters</param>
        /// <param name="material">Optional material override</param>
        /// <returns>The trim container GameObject</returns>
        public GameObject AddSecondaryRoofTrim(float height = 0.15f, Material? material = null)
        {
            float wallThickness = 0.2f;
            float trimDepth = wallThickness + 0.1f;
            GameObject container = BuildingUtilities.CreateFolder("SecondaryRoofTrim", _parent);

            Color color = _palette.AccentColor;
            // Position above the roof line (AddRoofTrim ends at _roomSize.y)
            float yPos = _roomSize.y + height / 2f;

            // North
            GameObject north = PrimitiveBuilder.CreateBox("NorthTrim",
                new Vector3(_roomSize.x / 2f, yPos, _roomSize.z),
                new Vector3(_roomSize.x + trimDepth, height, trimDepth),
                color, container.transform);

            // South
            GameObject south = PrimitiveBuilder.CreateBox("SouthTrim",
                new Vector3(_roomSize.x / 2f, yPos, 0f),
                new Vector3(_roomSize.x + trimDepth, height, trimDepth),
                color, container.transform);

            // East
            GameObject east = PrimitiveBuilder.CreateBox("EastTrim",
                new Vector3(_roomSize.x, yPos, _roomSize.z / 2f),
                new Vector3(trimDepth, height, _roomSize.z - trimDepth),
                color, container.transform);

            // West
            GameObject west = PrimitiveBuilder.CreateBox("WestTrim",
                new Vector3(0f, yPos, _roomSize.z / 2f),
                new Vector3(trimDepth, height, _roomSize.z - trimDepth),
                color, container.transform);

            // Apply material
            Material? mat = material ?? _palette.AccentMaterial ?? _palette.TrimMaterial;
            if (mat != null)
            {
                ApplyMaterial(north, mat);
                ApplyMaterial(south, mat);
                ApplyMaterial(east, mat);
                ApplyMaterial(west, mat);
            }

            return container;
        }

        /// <summary>
        /// Add structural pillars at the four corners of the building.
        /// </summary>
        /// <param name="width">Pillar width in meters</param>
        /// <param name="material">Optional material override</param>
        /// <returns>The pillars container GameObject</returns>
        public GameObject AddCornerPillars(float width = 0.4f, Material? material = null)
        {
            GameObject container = BuildingUtilities.CreateFolder("CornerPillars", _parent);

            Color color = _palette.PillarColor;
            float height = _roomSize.y;
            float offset = width / 2f;

            // Northeast
            GameObject ne = PrimitiveBuilder.CreateBox("Pillar_NE",
                new Vector3(_roomSize.x + offset - 0.1f, height / 2f, _roomSize.z + offset - 0.1f),
                new Vector3(width, height, width),
                color, container.transform);

            // Northwest
            GameObject nw = PrimitiveBuilder.CreateBox("Pillar_NW",
                new Vector3(0f - offset + 0.1f, height / 2f, _roomSize.z + offset - 0.1f),
                new Vector3(width, height, width),
                color, container.transform);

            // Southeast
            GameObject se = PrimitiveBuilder.CreateBox("Pillar_SE",
                new Vector3(_roomSize.x + offset - 0.1f, height / 2f, 0f - offset + 0.1f),
                new Vector3(width, height, width),
                color, container.transform);

            // Southwest
            GameObject sw = PrimitiveBuilder.CreateBox("Pillar_SW",
                new Vector3(0f - offset + 0.1f, height / 2f, 0f - offset + 0.1f),
                new Vector3(width, height, width),
                color, container.transform);

            // Apply material
            Material? mat = material ?? _palette.PillarMaterial;
            if (mat != null)
            {
                ApplyMaterial(ne, mat);
                ApplyMaterial(nw, mat);
                ApplyMaterial(se, mat);
                ApplyMaterial(sw, mat);
            }

            return container;
        }

        /// <summary>
        /// Add thin vertical trim strips at the four corners of the building.
        /// Each corner gets two perpendicular strips forming a right angle, matching
        /// the depth and style of horizontal trims (<see cref="AddRoofTrim"/>, <see cref="AddBaseMolding"/>).
        /// Runs the full wall height so it looks good with or without horizontal trim.
        /// </summary>
        /// <param name="width">Visible width of each trim strip on the wall face in meters</param>
        /// <param name="depth">How far the trim protrudes past the wall surface in meters</param>
        /// <param name="material">Optional material override</param>
        /// <returns>The corner trim container GameObject</returns>
        public GameObject AddCornerTrim(float width = 0.3f, float depth = 0.1f, Material? material = null)
        {
            float wallThickness = 0.2f;
            float trimDepth = wallThickness + depth;
            float height = _roomSize.y;
            GameObject container = BuildingUtilities.CreateFolder("CornerTrim", _parent);

            Color color = _palette.TrimColor;

            // Each corner gets two perpendicular strips:
            // - One on the N/S wall face at the corner end
            // - One on the E/W wall face at the corner end
            // Small overlap at the inner corner is invisible (same color, hidden faces).

            // NE corner — both strips centered at corner so they wrap around the edge
            GameObject neN = PrimitiveBuilder.CreateBox("CornerTrim_NE_N",
                new Vector3(_roomSize.x, height / 2f, _roomSize.z),
                new Vector3(width, height, trimDepth),
                color, container.transform);
            GameObject neE = PrimitiveBuilder.CreateBox("CornerTrim_NE_E",
                new Vector3(_roomSize.x, height / 2f, _roomSize.z),
                new Vector3(trimDepth, height, width),
                color, container.transform);

            // NW corner
            GameObject nwN = PrimitiveBuilder.CreateBox("CornerTrim_NW_N",
                new Vector3(0f, height / 2f, _roomSize.z),
                new Vector3(width, height, trimDepth),
                color, container.transform);
            GameObject nwW = PrimitiveBuilder.CreateBox("CornerTrim_NW_W",
                new Vector3(0f, height / 2f, _roomSize.z),
                new Vector3(trimDepth, height, width),
                color, container.transform);

            // SE corner
            GameObject seS = PrimitiveBuilder.CreateBox("CornerTrim_SE_S",
                new Vector3(_roomSize.x, height / 2f, 0f),
                new Vector3(width, height, trimDepth),
                color, container.transform);
            GameObject seE = PrimitiveBuilder.CreateBox("CornerTrim_SE_E",
                new Vector3(_roomSize.x, height / 2f, 0f),
                new Vector3(trimDepth, height, width),
                color, container.transform);

            // SW corner
            GameObject swS = PrimitiveBuilder.CreateBox("CornerTrim_SW_S",
                new Vector3(0f, height / 2f, 0f),
                new Vector3(width, height, trimDepth),
                color, container.transform);
            GameObject swW = PrimitiveBuilder.CreateBox("CornerTrim_SW_W",
                new Vector3(0f, height / 2f, 0f),
                new Vector3(trimDepth, height, width),
                color, container.transform);

            // Apply material
            Material? mat = material ?? _palette.TrimMaterial;
            if (mat != null)
            {
                ApplyMaterial(neN, mat);
                ApplyMaterial(neE, mat);
                ApplyMaterial(nwN, mat);
                ApplyMaterial(nwW, mat);
                ApplyMaterial(seS, mat);
                ApplyMaterial(seE, mat);
                ApplyMaterial(swS, mat);
                ApplyMaterial(swW, mat);
            }

            return container;
        }

        /// <summary>
        /// Add a solid foundation block beneath the building.
        /// </summary>
        /// <param name="height">Depth of the foundation in meters</param>
        /// <param name="expandX">Extra width expansion beyond room bounds</param>
        /// <param name="expandZ">Extra depth expansion beyond room bounds</param>
        /// <param name="color">Optional color override</param>
        /// <param name="material">Optional material override</param>
        /// <returns>The foundation GameObject</returns>
        public GameObject AddFoundation(float height = 2.0f, float expandX = 0f, float expandZ = 0f, Color? color = null, Material? material = null)
        {
            Color foundationColor = color ?? new Color(0.4f, 0.4f, 0.4f);
            GameObject container = BuildingUtilities.CreateFolder("Foundation", _parent);

            float padding = Constants.Spatial.FoundationPadding;
            float yOffset = -0.001f; // Avoid z-fighting with floor

            // Store clearance so AddStairs can auto-clear the foundation edge
            _foundationClearanceX = padding + expandX;
            _foundationClearanceZ = padding + expandZ;

            float width = _roomSize.x + padding * 2 + expandX * 2;
            float depth = _roomSize.z + padding * 2 + expandZ * 2;

            GameObject foundation = PrimitiveBuilder.CreateBox("FoundationBlock",
                new Vector3(_roomSize.x / 2f, -height / 2f + yOffset, _roomSize.z / 2f),
                new Vector3(width, height, depth),
                foundationColor,
                container.transform);

            if (material != null)
            {
                ApplyMaterial(foundation, material);
            }

            return container;
        }

        /// <summary>
        /// Add stairs from ground level up to the building floor on the specified wall.
        /// Supports multiple visual styles: Solid (concrete box steps), ClosedRiser (two-tone wood with risers),
        /// or OpenStringer (plank treads on diagonal stringer beams).
        /// </summary>
        /// <param name="wall">Which wall the stairs attach to</param>
        /// <param name="foundationHeight">Height of the foundation in meters</param>
        /// <param name="maxStepHeight">Maximum height per step. Lower values create more, shallower steps. (Solid only)</param>
        /// <param name="width">Step width in meters (Solid only)</param>
        /// <param name="stepDepth">Step depth (tread) in meters. Controls how far stairs extend outward. (Solid only)</param>
        /// <param name="color">Optional color override — defaults to floor color (Solid only)</param>
        /// <param name="material">Optional material override — defaults to floor material (Solid only)</param>
        /// <param name="style">Visual style of stairs to generate</param>
        /// <param name="flushWithFloor">If true, topmost step is flush with floor level. If false (default), topmost step is one step below floor. (Solid only)</param>
        /// <param name="gap">Vertical gap between foundation edge and top step. ClosedRiser/OpenStringer default to 0 (flush).</param>
        /// <param name="lateralOffset">Lateral offset from wall center to align stairs with an offset door opening.</param>
        /// <returns>The stairs container GameObject</returns>
        public GameObject AddStairs(
            WallSide wall,
            float foundationHeight,
            float maxStepHeight = Constants.Spatial.DefaultMaxStepHeight,
            float width = 2.5f,
            float stepDepth = Constants.Spatial.DefaultStepDepth,
            Color? color = null,
            Material? material = null,
            StairStyle style = StairStyle.Solid,
            bool flushWithFloor = false,
            float gap = 0f,
            float lateralOffset = 0f)
        {
            return style switch
            {
                StairStyle.ClosedRiser => AddClosedRiserStairs(wall, foundationHeight, gap, lateralOffset),
                StairStyle.OpenStringer => AddOpenStringerStairs(wall, foundationHeight, gap, lateralOffset),
                _ => AddSolidStairs(wall, foundationHeight, maxStepHeight, width, stepDepth, color, material, flushWithFloor, lateralOffset)
            };
        }

        /// <summary>
        /// Add trim-style door frames (left jamb, right jamb, header) around door openings.
        /// Frames use palette trim color/material and protrude slightly past the wall surface.
        /// </summary>
        /// <param name="northOpening">North wall opening</param>
        /// <param name="southOpening">South wall opening</param>
        /// <param name="eastOpening">East wall opening</param>
        /// <param name="westOpening">West wall opening</param>
        /// <param name="material">Optional material override</param>
        /// <returns>The door frames container GameObject</returns>
        public GameObject AddDoorFrames(
            WallOpening? northOpening = null, WallOpening? southOpening = null,
            WallOpening? eastOpening = null, WallOpening? westOpening = null,
            Material? material = null)
        {
            GameObject container = BuildingUtilities.CreateFolder("DoorFrames", _parent);

            float wallThickness = 0.2f;
            float frameWidth = 0.12f;
            float frameProtrusion = 0.1f;
            float trimDepth = wallThickness + frameProtrusion;

            Color color = _palette.TrimColor;
            Material? mat = material ?? _palette.TrimMaterial;

            // Inset wall segments and create frames for each door opening
            if (northOpening?.Type == WallOpeningType.Door)
            {
                InsetDoorWallSegments("NorthWall", frameWidth, false);
                CreateDoorFrame("DoorFrame_North",
                    new Vector3(_roomSize.x / 2f, _roomSize.y / 2f, _roomSize.z),
                    _roomSize.y, northOpening, false, frameWidth, trimDepth, color, mat, container);
            }

            if (southOpening?.Type == WallOpeningType.Door)
            {
                InsetDoorWallSegments("SouthWall", frameWidth, false);
                CreateDoorFrame("DoorFrame_South",
                    new Vector3(_roomSize.x / 2f, _roomSize.y / 2f, 0f),
                    _roomSize.y, southOpening, false, frameWidth, trimDepth, color, mat, container);
            }

            if (eastOpening?.Type == WallOpeningType.Door)
            {
                InsetDoorWallSegments("EastWall", frameWidth, true);
                CreateDoorFrame("DoorFrame_East",
                    new Vector3(_roomSize.x, _roomSize.y / 2f, _roomSize.z / 2f),
                    _roomSize.y, eastOpening, true, frameWidth, trimDepth, color, mat, container);
            }

            if (westOpening?.Type == WallOpeningType.Door)
            {
                InsetDoorWallSegments("WestWall", frameWidth, true);
                CreateDoorFrame("DoorFrame_West",
                    new Vector3(0f, _roomSize.y / 2f, _roomSize.z / 2f),
                    _roomSize.y, westOpening, true, frameWidth, trimDepth, color, mat, container);
            }

            return container;
        }

        /// <summary>
        /// Add trim-style door frames around interior doorway openings.
        /// Uses <see cref="DoorwayInfo"/> recorded by <see cref="InteriorWallBuilder"/>.
        /// </summary>
        /// <param name="doorways">Interior doorway positions and dimensions</param>
        /// <param name="frameWidth">Width of the frame casing in meters</param>
        /// <param name="frameProtrusion">How far the frame extends past the wall surface</param>
        /// <param name="material">Optional material override (defaults to palette trim material)</param>
        /// <returns>The interior door frames container GameObject</returns>
        public GameObject AddInteriorDoorFrames(
            IReadOnlyList<DoorwayInfo> doorways,
            float frameWidth = 0.12f, float frameProtrusion = 0.04f,
            Material? material = null)
        {
            GameObject container = BuildingUtilities.CreateFolder("InteriorDoorFrames", _parent);

            Color color = _palette.TrimColor;
            Material? mat = material ?? _palette.TrimMaterial;

            for (int i = 0; i < doorways.Count; i++)
            {
                DoorwayInfo doorway = doorways[i];
                float trimDepth = doorway.WallThickness + frameProtrusion;
                bool isVertical = !doorway.FacesAlongZ;

                // Inset wall segments to make room for the frame (same as exterior)
                if (doorway.WallContainer != null)
                {
                    InsetDoorWallSegments(doorway.WallContainer.transform,
                        doorway.WallContainer.name, frameWidth, isVertical);
                }

                // DoorwayInfo.Center is at door mid-height; CreateDoorFrame expects wall center (mid-wall-height)
                Vector3 wallCenter = new Vector3(doorway.Center.x, _roomSize.y / 2f, doorway.Center.z);

                CreateDoorFrame($"InteriorDoorFrame_{i}", wallCenter, _roomSize.y,
                    doorway.Width, doorway.Height, 0f,
                    isVertical, frameWidth, trimDepth, color, mat, container);
            }

            return container;
        }

        /// <summary>
        /// Add base molding around the bottom of the building.
        /// Automatically gaps around door openings so the molding does not clip through door frames.
        /// </summary>
        /// <param name="height">Molding height in meters</param>
        /// <param name="depth">Molding depth in meters</param>
        /// <param name="material">Optional material override</param>
        /// <param name="northOpening">North wall opening (doors create a gap)</param>
        /// <param name="southOpening">South wall opening (doors create a gap)</param>
        /// <param name="eastOpening">East wall opening (doors create a gap)</param>
        /// <param name="westOpening">West wall opening (doors create a gap)</param>
        /// <param name="skipWalls">Wall sides to omit molding from (null = include all walls)</param>
        /// <returns>The molding container GameObject</returns>
        public GameObject AddBaseMolding(
            float height = 0.3f, float depth = 0.1f, Material? material = null,
            WallOpening? northOpening = null, WallOpening? southOpening = null,
            WallOpening? eastOpening = null, WallOpening? westOpening = null,
            IEnumerable<WallSide>? skipWalls = null)
        {
            float halfWidth = _roomSize.x / 2f;
            float halfDepth = _roomSize.z / 2f;
            float wallThickness = 0.2f;
            float trimDepth = wallThickness + depth;
            GameObject container = BuildingUtilities.CreateFolder("BaseMolding", _parent);

            Color color = _palette.TrimColor;
            Material? mat = material ?? _palette.TrimMaterial;

            HashSet<WallSide>? skip = skipWalls != null ? new HashSet<WallSide>(skipWalls) : null;

            // North (extends along X, centered on wall surface)
            if (skip == null || !skip.Contains(WallSide.North))
            {
                CreateMoldingSegments("BaseMolding_North",
                    new Vector3(halfWidth, height / 2f, _roomSize.z),
                    new Vector3(_roomSize.x + trimDepth, height, trimDepth),
                    _roomSize.x + trimDepth, false, northOpening, height, color, mat, container);
            }

            // South (extends along X, centered on wall surface)
            if (skip == null || !skip.Contains(WallSide.South))
            {
                CreateMoldingSegments("BaseMolding_South",
                    new Vector3(halfWidth, height / 2f, 0f),
                    new Vector3(_roomSize.x + trimDepth, height, trimDepth),
                    _roomSize.x + trimDepth, false, southOpening, height, color, mat, container);
            }

            // East (extends along Z, centered on wall surface)
            if (skip == null || !skip.Contains(WallSide.East))
            {
                CreateMoldingSegments("BaseMolding_East",
                    new Vector3(_roomSize.x, height / 2f, halfDepth),
                    new Vector3(trimDepth, height, _roomSize.z - trimDepth),
                    _roomSize.z - trimDepth, true, eastOpening, height, color, mat, container);
            }

            // West (extends along Z, centered on wall surface)
            if (skip == null || !skip.Contains(WallSide.West))
            {
                CreateMoldingSegments("BaseMolding_West",
                    new Vector3(0f, height / 2f, halfDepth),
                    new Vector3(trimDepth, height, _roomSize.z - trimDepth),
                    _roomSize.z - trimDepth, true, westOpening, height, color, mat, container);
            }

            return container;
        }

        /// <summary>
        /// Add floor to the room.
        /// </summary>
        /// <param name="thickness">Floor thickness in meters</param>
        /// <returns>The floor GameObject</returns>
        public GameObject AddFloor(float thickness = 0.1f)
        {
            GameObject floor = PrimitiveBuilder.CreateBox("Floor",
                new Vector3(_roomSize.x / 2f, -thickness / 2f, _roomSize.z / 2f),
                new Vector3(_roomSize.x, thickness, _roomSize.z),
                _palette.FloorColor,
                _parent);

            if (_palette.FloorMaterial != null)
            {
                ApplyMaterial(floor, _palette.FloorMaterial);
            }

            return floor;
        }

        /// <summary>
        /// Add ceiling to the room.
        /// The ceiling's top surface is flush with the top of the walls (_roomSize.y),
        /// so roof slabs placed at _roomSize.y sit directly on top with no overlap.
        /// </summary>
        /// <param name="thickness">Ceiling thickness in meters</param>
        /// <returns>The ceiling GameObject</returns>
        public GameObject AddCeiling(float thickness = 0.1f)
        {
            GameObject ceiling = PrimitiveBuilder.CreateBox("Ceiling",
                new Vector3(_roomSize.x / 2f, _roomSize.y - thickness / 2f, _roomSize.z / 2f),
                new Vector3(_roomSize.x, thickness, _roomSize.z),
                _palette.CeilingColor,
                _parent);

            if (_palette.CeilingMaterial != null)
            {
                ApplyMaterial(ceiling, _palette.CeilingMaterial);
            }

            return ceiling;
        }

        #endregion

        #region Private Methods

        private GameObject AddSolidStairs(
            WallSide wall,
            float foundationHeight,
            float maxStepHeight,
            float width,
            float stepDepth,
            Color? color,
            Material? material,
            bool flushWithFloor = false,
            float lateralOffset = 0f)
        {
            GameObject container = BuildingUtilities.CreateFolder(Constants.Spatial.StairsFolderName, _parent);
            Color stepColor = color ?? _palette.FloorColor;
            int count = Mathf.Max(2, Mathf.CeilToInt(foundationHeight / maxStepHeight));
            float stepRise = foundationHeight / count;

            // Default: floor acts as the final step, so we generate count-1 visible steps.
            // Topmost step surface is at Y = -stepRise (one step below floor).
            // flushWithFloor: topmost step surface is at Y = 0 (flush with floor level).
            int visibleSteps = flushWithFloor ? count : count - 1;

            // Push steps outward past the foundation edge
            // Uses clearance set by AddFoundation (padding + expand), or 0.1m default
            float clearanceX = _foundationClearanceX > 0f ? _foundationClearanceX : 0.1f;
            float clearanceZ = _foundationClearanceZ > 0f ? _foundationClearanceZ : 0.1f;

            for (int i = 0; i < visibleSteps; i++)
            {
                float height = (i + 1) * stepRise;
                float yCenter = -foundationHeight + height / 2f;
                bool isNorthSouth = wall == WallSide.North || wall == WallSide.South;
                float clearance = isNorthSouth ? clearanceZ : clearanceX;
                float perpOffset = (visibleSteps - 1 - i) * stepDepth + stepDepth / 2f + clearance;

                Vector3 position;
                Vector3 size;

                switch (wall)
                {
                    case WallSide.North:
                        position = new Vector3(_roomSize.x / 2f + lateralOffset, yCenter, _roomSize.z + perpOffset);
                        size = new Vector3(width, height, stepDepth);
                        break;
                    case WallSide.South:
                        position = new Vector3(_roomSize.x / 2f + lateralOffset, yCenter, -perpOffset);
                        size = new Vector3(width, height, stepDepth);
                        break;
                    case WallSide.East:
                        position = new Vector3(_roomSize.x + perpOffset, yCenter, _roomSize.z / 2f + lateralOffset);
                        size = new Vector3(stepDepth, height, width);
                        break;
                    case WallSide.West:
                        position = new Vector3(-perpOffset, yCenter, _roomSize.z / 2f + lateralOffset);
                        size = new Vector3(stepDepth, height, width);
                        break;
                    default:
                        continue;
                }

                GameObject step = PrimitiveBuilder.CreateBox(
                    $"Step{i + 1}", position, size, stepColor, container.transform);

                Material? mat = material ?? _palette.FloorMaterial;
                if (mat != null)
                {
                    ApplyMaterial(step, mat);
                }
            }

            return container;
        }

        private GameObject AddClosedRiserStairs(WallSide wall, float foundationHeight, float gap, float lateralOffset = 0f)
        {
            GameObject container = BuildingUtilities.CreateFolder("Stairs_ClosedRiser", _parent);

            // Tread planks and riser faces use separate materials for two-tone look
            Material? treadMaterial = MaterialPresets.FindExistingMaterial(Constants.Materials.TreadWoodName);
            Material? riserMaterial = MaterialPresets.FindExistingMaterial(Constants.Materials.RiserWoodName);

            Color riserColor = new Color(0.85f, 0.80f, 0.72f);
            Color treadColor = new Color(0.45f, 0.35f, 0.25f);

            float width = 2.5f;
            float stepDepth = Constants.Spatial.DefaultStepDepth;
            float treadThickness = 0.05f;
            int count = Mathf.Max(2, Mathf.CeilToInt(foundationHeight / Constants.Spatial.DefaultMaxStepHeight));
            float stepRise = foundationHeight / count;
            // ClosedRiser: top step is flush with floor
            int visibleSteps = count;

            float clearanceX = _foundationClearanceX > 0f ? _foundationClearanceX : 0.1f;
            float clearanceZ = _foundationClearanceZ > 0f ? _foundationClearanceZ : 0.1f;

            for (int i = 0; i < visibleSteps; i++)
            {
                float height = (i + 1) * stepRise;
                float yCenter = -foundationHeight + height / 2f - gap;
                bool isNorthSouth = wall == WallSide.North || wall == WallSide.South;
                float clearance = isNorthSouth ? clearanceZ : clearanceX;
                float perpOffset = (visibleSteps - 1 - i) * stepDepth + stepDepth / 2f + clearance;

                // Tread sits ON TOP of riser (not embedded) to avoid z-fighting
                float treadY = -foundationHeight + height + treadThickness / 2f - gap;

                Vector3 riserPos, riserSize, treadPos, treadSize;

                // Tread overhang: slight lip past riser face
                float treadOverhangDepth = 0.04f;  // 4cm total depth overhang (2cm front + back)
                float treadOverhangWidth = 0.06f;  // 6cm total width overhang (3cm per side)

                switch (wall)
                {
                    case WallSide.North:
                        riserPos = new Vector3(_roomSize.x / 2f + lateralOffset, yCenter, _roomSize.z + perpOffset);
                        riserSize = new Vector3(width, height, stepDepth);
                        treadPos = new Vector3(_roomSize.x / 2f + lateralOffset, treadY, _roomSize.z + perpOffset);
                        treadSize = new Vector3(width + treadOverhangWidth, treadThickness, stepDepth + treadOverhangDepth);
                        break;
                    case WallSide.South:
                        riserPos = new Vector3(_roomSize.x / 2f + lateralOffset, yCenter, -perpOffset);
                        riserSize = new Vector3(width, height, stepDepth);
                        treadPos = new Vector3(_roomSize.x / 2f + lateralOffset, treadY, -perpOffset);
                        treadSize = new Vector3(width + treadOverhangWidth, treadThickness, stepDepth + treadOverhangDepth);
                        break;
                    case WallSide.East:
                        riserPos = new Vector3(_roomSize.x + perpOffset, yCenter, _roomSize.z / 2f + lateralOffset);
                        riserSize = new Vector3(stepDepth, height, width);
                        treadPos = new Vector3(_roomSize.x + perpOffset, treadY, _roomSize.z / 2f + lateralOffset);
                        treadSize = new Vector3(stepDepth + treadOverhangDepth, treadThickness, width + treadOverhangWidth);
                        break;
                    case WallSide.West:
                        riserPos = new Vector3(-perpOffset, yCenter, _roomSize.z / 2f + lateralOffset);
                        riserSize = new Vector3(stepDepth, height, width);
                        treadPos = new Vector3(-perpOffset, treadY, _roomSize.z / 2f + lateralOffset);
                        treadSize = new Vector3(stepDepth + treadOverhangDepth, treadThickness, width + treadOverhangWidth);
                        break;
                    default:
                        continue;
                }

                // Riser body (light brown wood)
                GameObject riser = PrimitiveBuilder.CreateBox(
                    $"Riser{i + 1}", riserPos, riserSize, riserColor, container.transform);
                if (riserMaterial != null) ApplyMaterial(riser, riserMaterial);

                // Tread plank on top (dark wood, slight overhang)
                GameObject tread = PrimitiveBuilder.CreateBox(
                    $"Tread{i + 1}", treadPos, treadSize, treadColor, container.transform);
                if (treadMaterial != null) ApplyMaterial(tread, treadMaterial);
            }

            DebugLog.Info($"[DecorBuilder] ClosedRiser stairs: {visibleSteps} steps, gap={gap:F2}, treadMat={treadMaterial?.name ?? "fallback"}, riserMat={riserMaterial?.name ?? "fallback"}");
            return container;
        }

        private GameObject AddOpenStringerStairs(WallSide wall, float foundationHeight, float gap, float lateralOffset = 0f)
        {
            GameObject container = BuildingUtilities.CreateFolder("Stairs_OpenStringer", _parent);

            // Separate materials for treads and stringer beams
            Material? strinTreadMaterial = MaterialPresets.FindExistingMaterial(Constants.Materials.StringerTreadWoodName);
            Color woodColor = new Color(0.55f, 0.45f, 0.35f);
            Material? strinBeamMaterial = MaterialPresets.FindExistingMaterial(Constants.Materials.StringerBeamWoodName);

            float width = 3.0f;
            float stepDepth = Constants.Spatial.DefaultStepDepth;
            float treadThickness = 0.07f;
            float beamWidth = 0.25f;   // Stringer beam cross-section width (4x4 post style)
            float beamHeight = 0.25f;  // Stringer beam cross-section height (4x4 post style)
            int count = Mathf.Max(2, Mathf.CeilToInt(foundationHeight / Constants.Spatial.DefaultMaxStepHeight));
            float stepRise = foundationHeight / count;
            // Top step sits one rise below floor level (the foundation/floor is the final surface)
            int visibleSteps = count - 1;

            float clearanceX = _foundationClearanceX > 0f ? _foundationClearanceX : 0.1f;
            float clearanceZ = _foundationClearanceZ > 0f ? _foundationClearanceZ : 0.1f;
            bool isNorthSouth = wall == WallSide.North || wall == WallSide.South;
            float clearance = isNorthSouth ? clearanceZ : clearanceX;

            // --- Treads (flat planks, no solid risers) ---
            float treadOverhang = 0.20f; // Treads extend past the stringer beams on each side
            for (int i = 0; i < visibleSteps; i++)
            {
                float stepTop = -foundationHeight + (i + 1) * stepRise - gap;
                float treadY = stepTop + treadThickness / 2f;
                float perpOffset = (visibleSteps - 1 - i) * stepDepth + stepDepth / 2f + clearance;

                Vector3 treadPos, treadSize;
                switch (wall)
                {
                    case WallSide.North:
                        treadPos = new Vector3(_roomSize.x / 2f + lateralOffset, treadY, _roomSize.z + perpOffset);
                        treadSize = new Vector3(width + treadOverhang, treadThickness, stepDepth + 0.04f);
                        break;
                    case WallSide.South:
                        treadPos = new Vector3(_roomSize.x / 2f + lateralOffset, treadY, -perpOffset);
                        treadSize = new Vector3(width + treadOverhang, treadThickness, stepDepth + 0.04f);
                        break;
                    case WallSide.East:
                        treadPos = new Vector3(_roomSize.x + perpOffset, treadY, _roomSize.z / 2f + lateralOffset);
                        treadSize = new Vector3(stepDepth + 0.04f, treadThickness, width + treadOverhang);
                        break;
                    case WallSide.West:
                        treadPos = new Vector3(-perpOffset, treadY, _roomSize.z / 2f + lateralOffset);
                        treadSize = new Vector3(stepDepth + 0.04f, treadThickness, width + treadOverhang);
                        break;
                    default:
                        continue;
                }

                GameObject tread = PrimitiveBuilder.CreateBox(
                    $"Tread{i + 1}", treadPos, treadSize, woodColor, container.transform);
                if (strinTreadMaterial != null) ApplyMaterial(tread, strinTreadMaterial);
            }

            // --- Stringer beams (diagonal supports on left and right sides) ---
            // Top end embeds into the foundation (at floor level), bottom end sinks into the ground.
            float bottomExtend = 0.30f;

            // Stringer runs from inside the foundation down to the ground
            float stringerBottomY = -foundationHeight - gap;
            // Push the top anchor well below floor level and behind the wall so the beam is fully hidden
            float stringerTopY = -gap + stepRise;               // One step above floor (hidden inside foundation)
            float topPerp = -beamHeight;                      // Behind the wall surface (into foundation)
            float bottomPerp = (visibleSteps - 1) * stepDepth + stepDepth / 2f + clearance;
            float stringerRun = bottomPerp - topPerp;
            float stringerRise = stringerTopY - stringerBottomY;

            // Stringer angle
            float angleRad = Mathf.Atan2(stringerRise, stringerRun);
            float angleDeg = angleRad * Mathf.Rad2Deg;

            float baseLength = Mathf.Sqrt(stringerRise * stringerRise + stringerRun * stringerRun);

            // The beam's actual endpoints along the slope:
            // - Top end: pull back 2 step rises from the anchor to hide inside foundation
            float topTrim = stepRise * 3.0f;
            // - Bottom end: extend into the ground
            float stringerLength = baseLength + bottomExtend - topTrim;

            // Compute the actual top and bottom endpoints of the trimmed/extended beam
            // Direction along slope (from top toward bottom): perp increases, Y decreases
            float dirPerp = Mathf.Cos(angleRad);
            float dirY = Mathf.Sin(angleRad);

            // Actual top end (trimmed): move topTrim along slope from the original top anchor
            float actualTopPerp = topPerp + topTrim * dirPerp;
            float actualTopY = stringerTopY - topTrim * dirY;
            // Actual bottom end (extended): move bottomExtend along slope past original bottom
            float actualBottomPerp = bottomPerp + bottomExtend * dirPerp;
            float actualBottomY = stringerBottomY - bottomExtend * dirY;

            // Midpoint is simply the average of the actual endpoints
            float midPerp = (actualTopPerp + actualBottomPerp) / 2f;
            float midY = (actualTopY + actualBottomY) / 2f;

            // Left/right offset: beams sit inside the tread width, treads overhang past them
            float sideOffset = width / 2f - beamWidth;

            for (int side = 0; side < 2; side++)
            {
                float signedOffset = (side == 0) ? -sideOffset : sideOffset;

                Vector3 beamPos;
                // Use Y as the long axis so the wood grain texture runs along the beam length
                Vector3 beamSize = new Vector3(beamWidth, stringerLength, beamHeight);
                Quaternion beamRot;

                switch (wall)
                {
                    case WallSide.North:
                        beamPos = new Vector3(_roomSize.x / 2f + lateralOffset + signedOffset, midY, _roomSize.z + midPerp);
                        beamRot = Quaternion.Euler(angleDeg + 90f, 0f, 0f);
                        break;
                    case WallSide.South:
                        beamPos = new Vector3(_roomSize.x / 2f + lateralOffset + signedOffset, midY, -midPerp);
                        beamRot = Quaternion.Euler(-angleDeg - 90f, 0f, 0f);
                        break;
                    case WallSide.East:
                        beamPos = new Vector3(_roomSize.x + midPerp, midY, _roomSize.z / 2f + lateralOffset + signedOffset);
                        beamSize = new Vector3(beamHeight, stringerLength, beamWidth);
                        beamRot = Quaternion.Euler(0f, 0f, -angleDeg - 90f);
                        break;
                    case WallSide.West:
                        beamPos = new Vector3(-midPerp, midY, _roomSize.z / 2f + lateralOffset + signedOffset);
                        beamSize = new Vector3(beamHeight, stringerLength, beamWidth);
                        beamRot = Quaternion.Euler(0f, 0f, angleDeg + 90f);
                        break;
                    default:
                        continue;
                }

                GameObject beam = PrimitiveBuilder.CreateBox(
                    $"Stringer{side + 1}", beamPos, beamSize, woodColor, container.transform);
                beam.transform.rotation = _parent.rotation * beamRot;
                if (strinBeamMaterial != null) ApplyMaterial(beam, strinBeamMaterial);
                else if (strinTreadMaterial != null) ApplyMaterial(beam, strinTreadMaterial);
            }

            DebugLog.Info($"[DecorBuilder] OpenStringer stairs: {visibleSteps} treads + 2 stringers, gap={gap:F2}, treadMat={strinTreadMaterial?.name ?? "fallback"}, beamMat={strinBeamMaterial?.name ?? "fallback"}");
            return container;
        }

        /// <summary>
        /// Creates a molding strip, splitting it into two segments if a door opening is present.
        /// </summary>
        private void CreateMoldingSegments(
            string name, Vector3 center, Vector3 size, float wallLength,
            bool isZAxis, WallOpening? opening, float moldingHeight,
            Color color, Material? material, GameObject container)
        {
            bool hasDoorGap = opening != null
                && opening.Type == WallOpeningType.Door
                && opening.BottomOffset < moldingHeight;

            if (!hasDoorGap)
            {
                GameObject strip = PrimitiveBuilder.CreateBox(name, center, size, color, container.transform);
                if (material != null) ApplyMaterial(strip, material);
                return;
            }

            float doorOffset = opening!.Offset;

            // Asymmetric segment lengths around the offset door
            float leftLength = (wallLength - opening.Width) / 2f + doorOffset;
            float rightLength = (wallLength - opening.Width) / 2f - doorOffset;

            if (isZAxis)
            {
                // Strip runs along Z (East/West walls)
                if (leftLength > 0f)
                {
                    Vector3 segSize = new Vector3(size.x, size.y, leftLength);
                    Vector3 lowZ = center + Vector3.back * (wallLength / 2f - leftLength / 2f);
                    GameObject left = PrimitiveBuilder.CreateBox($"{name}_L", lowZ, segSize, color, container.transform);
                    if (material != null) ApplyMaterial(left, material);
                }
                if (rightLength > 0f)
                {
                    Vector3 segSize = new Vector3(size.x, size.y, rightLength);
                    Vector3 highZ = center + Vector3.forward * (wallLength / 2f - rightLength / 2f);
                    GameObject right = PrimitiveBuilder.CreateBox($"{name}_R", highZ, segSize, color, container.transform);
                    if (material != null) ApplyMaterial(right, material);
                }
            }
            else
            {
                // Strip runs along X (North/South walls)
                if (leftLength > 0f)
                {
                    Vector3 segSize = new Vector3(leftLength, size.y, size.z);
                    Vector3 lowX = center + Vector3.left * (wallLength / 2f - leftLength / 2f);
                    GameObject left = PrimitiveBuilder.CreateBox($"{name}_L", lowX, segSize, color, container.transform);
                    if (material != null) ApplyMaterial(left, material);
                }
                if (rightLength > 0f)
                {
                    Vector3 segSize = new Vector3(rightLength, size.y, size.z);
                    Vector3 highX = center + Vector3.right * (wallLength / 2f - rightLength / 2f);
                    GameObject right = PrimitiveBuilder.CreateBox($"{name}_R", highX, segSize, color, container.transform);
                    if (material != null) ApplyMaterial(right, material);
                }
            }
        }

        /// <summary>
        /// Shrinks wall segments around a door opening to make room for the door frame.
        /// The side segments pull back laterally and the top segment pulls up,
        /// leaving gaps that the door frame casings fill exactly.
        /// </summary>
        private void InsetDoorWallSegments(string wallName, float inset, bool isVertical)
        {
            Transform? wallContainer = _parent.Find($"Walls/{wallName}");
            if (wallContainer == null) return;

            InsetDoorWallSegments(wallContainer, wallName, inset, isVertical);
        }

        private static void InsetDoorWallSegments(Transform wallContainer, string childPrefix, float inset, bool isVertical)
        {
            Transform? left = wallContainer.Find($"{childPrefix}_Left");
            Transform? right = wallContainer.Find($"{childPrefix}_Right");
            Transform? top = wallContainer.Find($"{childPrefix}_Top");

            // Shrink side segments away from the door opening
            if (left != null)
            {
                Vector3 scale = left.localScale;
                Vector3 pos = left.localPosition;
                if (isVertical) { scale.z -= inset; pos.z -= inset / 2f; }
                else { scale.x -= inset; pos.x -= inset / 2f; }
                left.localScale = scale;
                left.localPosition = pos;
            }

            if (right != null)
            {
                Vector3 scale = right.localScale;
                Vector3 pos = right.localPosition;
                if (isVertical) { scale.z -= inset; pos.z += inset / 2f; }
                else { scale.x -= inset; pos.x += inset / 2f; }
                right.localScale = scale;
                right.localPosition = pos;
            }

            // Shrink top segment upward and widen to cover gaps left by side insets
            if (top != null)
            {
                Vector3 scale = top.localScale;
                Vector3 pos = top.localPosition;
                scale.y -= inset;
                pos.y += inset / 2f;
                if (isVertical) scale.z += 2f * inset;
                else scale.x += 2f * inset;
                top.localScale = scale;
                top.localPosition = pos;
            }
        }

        private void CreateDoorFrame(
            string name, Vector3 wallCenter, float wallHeight,
            WallOpening opening, bool isVertical, float frameWidth,
            float trimDepth, Color color, Material? material, GameObject container)
        {
            CreateDoorFrame(name, wallCenter, wallHeight,
                opening.Width, opening.Height, opening.Offset,
                isVertical, frameWidth, trimDepth, color, material, container);
        }

        private void CreateDoorFrame(
            string name, Vector3 wallCenter, float wallHeight,
            float doorWidth, float doorHeight, float doorOffset,
            bool isVertical, float frameWidth, float trimDepth,
            Color color, Material? material, GameObject container)
        {
            // Shift frame to match door offset
            Vector3 doorShift = isVertical
                ? new Vector3(0f, 0f, doorOffset)
                : new Vector3(doorOffset, 0f, 0f);
            Vector3 doorCenter = wallCenter + doorShift;

            float jambYOffset = -(wallHeight - doorHeight) / 2f;
            float sideOffset = doorWidth / 2f + frameWidth / 2f;

            // Jamb sizes
            Vector3 jambSize = isVertical
                ? new Vector3(trimDepth, doorHeight, frameWidth)
                : new Vector3(frameWidth, doorHeight, trimDepth);

            // Left jamb
            Vector3 leftJambPos = doorCenter + (isVertical
                ? new Vector3(0f, jambYOffset, -sideOffset)
                : new Vector3(-sideOffset, jambYOffset, 0f));
            GameObject leftJamb = PrimitiveBuilder.CreateBox($"{name}_Left", leftJambPos, jambSize, color, container.transform);

            // Right jamb
            Vector3 rightJambPos = doorCenter + (isVertical
                ? new Vector3(0f, jambYOffset, sideOffset)
                : new Vector3(sideOffset, jambYOffset, 0f));
            GameObject rightJamb = PrimitiveBuilder.CreateBox($"{name}_Right", rightJambPos, jambSize, color, container.transform);

            // Header (spans across both jambs)
            float headerWidth = doorWidth + 2f * frameWidth;
            float headerYOffset = -(wallHeight / 2f) + doorHeight + frameWidth / 2f;
            Vector3 headerSize = isVertical
                ? new Vector3(trimDepth, frameWidth, headerWidth)
                : new Vector3(headerWidth, frameWidth, trimDepth);
            GameObject header = PrimitiveBuilder.CreateBox($"{name}_Top",
                doorCenter + new Vector3(0f, headerYOffset, 0f), headerSize, color, container.transform);

            if (material != null)
            {
                ApplyMaterial(leftJamb, material);
                ApplyMaterial(rightJamb, material);
                ApplyMaterial(header, material);
            }
        }

        private static void ApplyMaterial(GameObject obj, Material material)
        {
            Renderer r = obj.GetComponent<Renderer>();
            if (r != null) r.material = material;
        }

        #endregion
    }
}
