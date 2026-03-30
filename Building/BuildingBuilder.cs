using System.Collections.Generic;
using S1MAPI.Building.Config;
using S1MAPI.Building.Structural;
using S1MAPI.Building.Interior;
using S1MAPI.Building.Components;
using UnityEngine;
using S1MAPI.Core;
using S1MAPI.S1;
using S1MAPI.Utils;

namespace S1MAPI.Building
{
    /// <summary>
    /// Fluent builder for constructing buildings with a clean, chainable API.
    /// Delegates to specialized builders (WallBuilder, FurnitureBuilder, etc.).
    /// </summary>
    /// <example>
    /// var building = new BuildingBuilder("MyShop")
    ///     .WithConfig(BuildingConfig.Dispensary)
    ///     .AddFloor()
    ///     .AddCeiling()
    ///     .AddWalls(southDoor: true, eastDoor: true, westWindow: true)
    ///     .AddLights()
    ///     .AddFurniture(FurnitureType.Counter, "north")
    ///     .Build();
    /// </example>
    public sealed class BuildingBuilder
    {
        #region Fields

        private readonly string _name;
        private readonly GameObject _root;
        private BuildingConfig _config;
        private Vector3 _roomSize;

        // Part registry for post-build targeting
        private readonly BuildingPartRegistry _registry;

        // Lazy-initialized builders
        private WallBuilder? _wallBuilder;
        private FurnitureBuilder? _furnitureBuilder;
        private LightingBuilder? _lightingBuilder;
        private DecorBuilder? _decorBuilder;
        private RoofBuilder? _roofBuilder;
        private PrefabPlacer? _prefabPlacer;
        private InteriorWallBuilder? _interiorWallBuilder;

        // Interior wall physics layer (-1 = default layer, no change)
        private int _interiorWallLayer = -1;

        // Cached interior doorway metadata survives InvalidateBuilders()
        private IReadOnlyList<DoorwayInfo>? _interiorDoorwaysCache;

        // Foundation and stair tracking for NavMesh link computation
        private float _foundationHeight;
        private float _foundationExpandX;
        private float _foundationExpandZ;
        private readonly List<StairSpec> _stairs = new List<StairSpec>();

        /// <summary>
        /// Internal record capturing the full stair specification so
        /// <see cref="ComputeStairBasePosition"/> can reproduce the exact run distance.
        /// </summary>
        private readonly struct StairSpec
        {
            public readonly WallSide Wall;
            public readonly float FoundationHeight;
            public readonly float Width;
            public readonly float LateralOffset;
            public readonly float MaxStepHeight;
            public readonly float StepDepth;
            public readonly StairStyle Style;
            public readonly bool FlushWithFloor;

            public StairSpec(WallSide wall, float foundationHeight, float width,
                float lateralOffset, float maxStepHeight, float stepDepth,
                StairStyle style, bool flushWithFloor)
            {
                Wall = wall;
                FoundationHeight = foundationHeight;
                Width = width;
                LateralOffset = lateralOffset;
                MaxStepHeight = maxStepHeight;
                StepDepth = stepDepth;
                Style = style;
                FlushWithFloor = flushWithFloor;
            }
        }

        // Stored wall openings for cross-builder communication (e.g., base molding gap)
        private WallOpening? _northOpening;
        private WallOpening? _southOpening;
        private WallOpening? _eastOpening;
        private WallOpening? _westOpening;

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new building builder.
        /// </summary>
        /// <param name="name">Name for the building GameObject</param>
        public BuildingBuilder(string name)
        {
            _name = name;
            _root = new GameObject(name);
            _registry = new BuildingPartRegistry();
            _config = BuildingConfig.Default;
            _roomSize = _config.Size;
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Apply a building configuration preset.
        /// </summary>
        /// <param name="config">Building configuration</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder WithConfig(BuildingConfig config)
        {
            _config = config;
            _roomSize = config.Size;
            InvalidateBuilders();
            return this;
        }

        /// <summary>
        /// Apply a palette to the current config.
        /// </summary>
        /// <param name="palette">Building palette</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder WithPalette(BuildingPalette palette)
        {
            _config.Palette = palette;
            InvalidateBuilders();
            return this;
        }

        /// <summary>
        /// Define room dimensions directly.
        /// </summary>
        /// <param name="width">Room width (X axis) in meters</param>
        /// <param name="height">Room height (Y axis) in meters</param>
        /// <param name="depth">Room depth (Z axis) in meters</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder DefineRoom(float width, float height, float depth)
        {
            _config.Width = width;
            _config.Height = height;
            _config.Depth = depth;
            _roomSize = new Vector3(width, height, depth);
            InvalidateBuilders();
            return this;
        }

        #endregion

        #region Structure

        /// <summary>
        /// Add floor to the room.
        /// </summary>
        /// <param name="color">Optional color override</param>
        /// <param name="material">Optional material override</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddFloor(Color? color = null, Material? material = null)
        {
            if (color.HasValue) _config.Palette.FloorColor = color.Value;
            if (material != null) _config.Palette.FloorMaterial = material;

            var floor = GetDecorBuilder().AddFloor(_config.FloorThickness);
            _registry.Register(BuildingPart.Floor, floor);
            return this;
        }

        /// <summary>
        /// Add ceiling to the room.
        /// </summary>
        /// <param name="color">Optional color override</param>
        /// <param name="material">Optional material override</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddCeiling(Color? color = null, Material? material = null)
        {
            if (color.HasValue) _config.Palette.CeilingColor = color.Value;
            if (material != null) _config.Palette.CeilingMaterial = material;

            var ceiling = GetDecorBuilder().AddCeiling(_config.CeilingThickness);
            _registry.Register(BuildingPart.Ceiling, ceiling);
            return this;
        }



        /// <summary>
        /// Add walls with specified openings.
        /// </summary>
        /// <param name="northDoor">Add door on north wall</param>
        /// <param name="southDoor">Add door on south wall</param>
        /// <param name="eastDoor">Add door on east wall</param>
        /// <param name="westDoor">Add door on west wall</param>
        /// <param name="northWindow">Add window on north wall</param>
        /// <param name="southWindow">Add window on south wall</param>
        /// <param name="eastWindow">Add window on east wall</param>
        /// <param name="westWindow">Add window on west wall</param>
        /// <param name="northDoorWindows">Add windows alongside north door (requires northDoor)</param>
        /// <param name="southDoorWindows">Add windows alongside south door (requires southDoor)</param>
        /// <param name="eastDoorWindows">Add windows alongside east door (requires eastDoor)</param>
        /// <param name="westDoorWindows">Add windows alongside west door (requires westDoor)</param>
        /// <param name="color">Optional wall color override</param>
        /// <param name="material">Optional wall material override</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddWalls(
            bool northDoor = false, bool southDoor = false,
            bool eastDoor = false, bool westDoor = false,
            bool northWindow = false, bool southWindow = false,
            bool eastWindow = false, bool westWindow = false,
            bool northDoorWindows = false, bool southDoorWindows = false,
            bool eastDoorWindows = false, bool westDoorWindows = false,
            Color? color = null,
            Material? material = null)
        {
            var palette = _config.Palette;
            if (color.HasValue || material != null)
            {
                palette = palette.Clone();
                if (color.HasValue) palette.WallColor = color.Value;
                if (material != null) palette.WallMaterial = material;
            }

            _northOpening = northDoor
                ? (northDoorWindows ? WallOpening.DoorWithWindows() : WallOpening.Door())
                : (northWindow ? WallOpening.Window() : null);
            _southOpening = southDoor
                ? (southDoorWindows ? WallOpening.DoorWithWindows() : WallOpening.Door())
                : (southWindow ? WallOpening.Window() : null);
            _eastOpening = eastDoor
                ? (eastDoorWindows ? WallOpening.DoorWithWindows() : WallOpening.Door())
                : (eastWindow ? WallOpening.Window() : null);
            _westOpening = westDoor
                ? (westDoorWindows ? WallOpening.DoorWithWindows() : WallOpening.Door())
                : (westWindow ? WallOpening.Window() : null);

            var builder = GetWallBuilder(palette);
            var wallsContainer = builder.BuildWalls(
                northOpening: _northOpening,
                southOpening: _southOpening,
                eastOpening: _eastOpening,
                westOpening: _westOpening);
            RegisterWallChildren(wallsContainer);

            return this;
        }

        /// <summary>
        /// Add walls with fine-grained control over openings.
        /// </summary>
        /// <param name="north">North wall opening configuration</param>
        /// <param name="south">South wall opening configuration</param>
        /// <param name="east">East wall opening configuration</param>
        /// <param name="west">West wall opening configuration</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddWalls(
            WallOpening? north = null,
            WallOpening? south = null,
            WallOpening? east = null,
            WallOpening? west = null)
        {
            _northOpening = north;
            _southOpening = south;
            _eastOpening = east;
            _westOpening = west;

            var wallsContainer = GetWallBuilder().BuildWalls(north, south, east, west);
            RegisterWallChildren(wallsContainer);
            return this;
        }

        /// <summary>
        /// Add walls with fine-grained control over openings and per-wall appearance.
        /// Walls without an appearance override use the palette defaults.
        /// </summary>
        /// <param name="north">North wall opening configuration</param>
        /// <param name="south">South wall opening configuration</param>
        /// <param name="east">East wall opening configuration</param>
        /// <param name="west">West wall opening configuration</param>
        /// <param name="wallAppearances">Per-wall material/color overrides keyed by wall side</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddWalls(
            WallOpening? north,
            WallOpening? south,
            WallOpening? east,
            WallOpening? west,
            IReadOnlyDictionary<WallSide, WallAppearance> wallAppearances)
        {
            _northOpening = north;
            _southOpening = south;
            _eastOpening = east;
            _westOpening = west;

            var wallsContainer = GetWallBuilder().BuildWalls(north, south, east, west, wallAppearances);
            RegisterWallChildren(wallsContainer);
            return this;
        }

        #endregion

        #region Interior Walls

        /// <summary>
        /// Set the physics layer for interior wall GameObjects.
        /// Use this to place interior walls on a layer outside the placement raycast mask
        /// so the ghost model passes through them while players still physically collide.
        /// </summary>
        /// <param name="layer">Unity layer index (0–31). -1 leaves walls on the default layer.</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder WithInteriorWallLayer(int layer)
        {
            if (_interiorWallBuilder != null)
                throw new System.InvalidOperationException(
                    "[BuildingBuilder.WithInteriorWallLayer] Must be called before AddInteriorWall.");
            _interiorWallLayer = layer;
            return this;
        }

        /// <summary>
        /// Add an interior wall spanning a sub-region of the room.
        /// </summary>
        /// <param name="axis">Axis the wall runs along (X or Z)</param>
        /// <param name="position">Position on the perpendicular axis (Z for X-axis walls, X for Z-axis walls)</param>
        /// <param name="from">Start coordinate along the wall's axis</param>
        /// <param name="to">End coordinate along the wall's axis</param>
        /// <param name="opening">Optional opening (door or window) centered in the wall</param>
        /// <param name="color">Optional wall color override (defaults to palette wall color)</param>
        /// <param name="material">Optional wall material override (defaults to palette wall material)</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddInteriorWall(
            InteriorWallAxis axis, float position, float from, float to,
            WallOpening? opening = null,
            Color? color = null, Material? material = null)
        {
            var def = new InteriorWallDefinition(axis, position, from, to, opening, color, material);
            var wall = GetInteriorWallBuilder().BuildInteriorWall(def);
            if (wall != null)
                _registry.Register(BuildingPart.InteriorWalls, wall);
            return this;
        }

        /// <summary>
        /// Add an interior wall spanning the full room width along the specified axis.
        /// </summary>
        /// <param name="axis">Axis the wall runs along (X or Z)</param>
        /// <param name="position">Position on the perpendicular axis (Z for X-axis walls, X for Z-axis walls)</param>
        /// <param name="opening">Optional opening (door or window) centered in the wall</param>
        /// <param name="color">Optional wall color override (defaults to palette wall color)</param>
        /// <param name="material">Optional wall material override (defaults to palette wall material)</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddInteriorWall(
            InteriorWallAxis axis, float position,
            WallOpening? opening = null,
            Color? color = null, Material? material = null)
        {
            float axisMax = axis == InteriorWallAxis.X ? _roomSize.x : _roomSize.z;
            return AddInteriorWall(axis, position, 0f, axisMax, opening, color, material);
        }

        /// <summary>
        /// Doorway positions recorded from all interior walls.
        /// Each entry provides center, dimensions, and orientation for future NavMesh link generation.
        /// </summary>
        public IReadOnlyList<DoorwayInfo> InteriorDoorways =>
            _interiorWallBuilder?.Doorways ?? _interiorDoorwaysCache ?? (IReadOnlyList<DoorwayInfo>)System.Array.Empty<DoorwayInfo>();

        /// <summary>
        /// Create a <see cref="NavigationBuilder"/> configured for this building.
        /// Collects exterior and interior doorway positions, stair geometry, and building dimensions.
        /// Call <see cref="NavigationBuilder.Build"/> on the returned instance after positioning the building.
        /// </summary>
        /// <returns>A configured builder ready to build</returns>
        public NavigationBuilder CreateNavigationBuilder()
        {
            var doorways = new List<NavDoorwayInfo>();

            // Exterior doorways (with optional stair base positions)
            TryAddExteriorDoor(WallSide.North, _northOpening, doorways);
            TryAddExteriorDoor(WallSide.South, _southOpening, doorways);
            TryAddExteriorDoor(WallSide.East, _eastOpening, doorways);
            TryAddExteriorDoor(WallSide.West, _westOpening, doorways);

            // Interior doorways (threshold generation + door panel collider filtering)
            foreach (DoorwayInfo interior in InteriorDoorways)
            {
                Vector3 normal = interior.FacesAlongZ ? Vector3.forward : Vector3.right;
                // DoorwayInfo.Center.y is at mid-door height; NavDoorwayInfo expects Y=0 (floor level)
                Vector3 center = new Vector3(interior.Center.x, 0f, interior.Center.z);
                doorways.Add(new NavDoorwayInfo(
                    center, interior.Width, interior.Height,
                    normal, interior.WallThickness, isInterior: true));
            }

            bool hasExterior = false;
            for (int i = 0; i < doorways.Count; i++)
            {
                if (!doorways[i].IsInterior) { hasExterior = true; break; }
            }
            if (!hasExterior)
                throw new System.InvalidOperationException(
                    "[BuildingBuilder.CreateNavigationBuilder] No exterior doorways found. " +
                    "NavigationBuilder requires at least one exterior door (AddWalls with a door opening).");

            return new NavigationBuilder(
                _root.transform, _roomSize,
                doorways, _config.WallThickness, _foundationHeight);
        }

        /// <summary>
        /// Flatten terrain under the building footprint.
        /// Must be called after the building is positioned in the scene.
        /// </summary>
        /// <param name="padding">Extra padding around the footprint in meters.</param>
        /// <param name="clearDetails">Clear grass and detail layers in the flattened region.</param>
        /// <param name="blendDistance">Distance for smooth transition back to natural terrain.</param>
        public BuildingBuilder FlattenTerrain(
            float padding = Constants.Terrain.DefaultFlattenPadding,
            bool clearDetails = true, float blendDistance = Constants.Terrain.DefaultBlendDistance)
        {
            float targetWorldY = _root.transform.position.y - _foundationHeight;
            Vector3 footprint = new Vector3(
                _roomSize.x + _foundationExpandX * 2f,
                _roomSize.y,
                _roomSize.z + _foundationExpandZ * 2f);
            TerrainFlattener.FlattenUnder(
                _root, footprint, targetWorldY, padding, clearDetails, blendDistance);
            return this;
        }

        #endregion

        #region Decoration

        /// <summary>
        /// Add decorative trim around the roofline.
        /// Not needed when using <see cref="AddParapetRoof"/> or <see cref="AddHipRoof"/>,
        /// which include their own roof structure. Useful for custom roof designs with <see cref="AddCeiling"/>.
        /// </summary>
        /// <param name="height">Trim height in meters</param>
        /// <param name="material">Optional material override</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddRoofTrim(float height = 0.3f, Material? material = null)
        {
            var trim = GetDecorBuilder().AddRoofTrim(height, material);
            _registry.Register(BuildingPart.Trim, trim);
            return this;
        }

        /// <summary>
        /// Add a secondary decorative trim above the roofline.
        /// Not needed when using <see cref="AddParapetRoof"/> or <see cref="AddHipRoof"/>,
        /// which include their own roof structure. Useful for custom roof designs with <see cref="AddCeiling"/>.
        /// </summary>
        /// <param name="height">Trim height in meters</param>
        /// <param name="material">Optional material override</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddSecondaryRoofTrim(float height = 0.15f, Material? material = null)
        {
            var trim = GetDecorBuilder().AddSecondaryRoofTrim(height, material);
            _registry.Register(BuildingPart.Accent, trim);
            return this;
        }

        /// <summary>
        /// Add a parapet roof (raised wall and cap above the roofline).
        /// The cap extends past the parapet wall by the overhang amount, creating a ledge.
        /// Includes a thin roof slab at ceiling height. For interior ceilings, use
        /// <see cref="AddCeiling"/> separately — the ceiling sits just below the roof slab with no overlap.
        /// Use <see cref="ParapetPreset.Deep"/> for a prominent commercial look or
        /// <see cref="ParapetPreset.Shallow"/> for a subtler profile.
        /// </summary>
        /// <param name="preset">Sizing preset (Deep or Shallow). Overridden by explicit dimensions.</param>
        /// <param name="parapetHeight">Height of the parapet wall in meters. Null uses preset default.</param>
        /// <param name="parapetDepth">Depth of the parapet wall. Null uses wall thickness + padding.</param>
        /// <param name="capHeight">Height of the cap. Null uses preset default.</param>
        /// <param name="capOverhang">How far the cap extends past the parapet wall on each side. Null uses preset default.</param>
        /// <param name="parapetColor">Color override for the parapet wall.</param>
        /// <param name="parapetMaterial">Material override for the parapet wall.</param>
        /// <param name="capColor">Color override for the cap.</param>
        /// <param name="capMaterial">Material override for the cap.</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddParapetRoof(
            ParapetPreset preset = ParapetPreset.Deep,
            float? parapetHeight = null,
            float? parapetDepth = null,
            float? capHeight = null,
            float? capOverhang = null,
            Color? parapetColor = null,
            Material? parapetMaterial = null,
            Color? capColor = null,
            Material? capMaterial = null)
        {
            var roof = GetRoofBuilder().AddParapetRoof(preset, parapetHeight, parapetDepth,
                capHeight, capOverhang, parapetColor, parapetMaterial, capColor, capMaterial);
            _registry.Register(BuildingPart.Roof, roof);
            return this;
        }

        /// <summary>
        /// Add a hip (four-slope) roof using custom mesh geometry.
        /// All four sides slope inward to a central ridge that is shorter than the building length.
        /// For square buildings, the ridge collapses to a point (pyramid roof).
        /// A base slab sits at ceiling height giving the roof visible thickness from below.
        /// For interior ceilings, use <see cref="AddCeiling"/> separately — the ceiling sits
        /// just below the roof slab with no overlap.
        /// </summary>
        /// <param name="ridgeHeight">Height of the ridge peak above the ceiling in meters.</param>
        /// <param name="overhang">How far the roof eaves extend past the walls in meters.</param>
        /// <param name="ridgeAlongX">If true, ridge runs along X axis. If false, along Z. Null auto-selects the longer axis.</param>
        /// <param name="roofColor">Color for the sloped roof planes.</param>
        /// <param name="roofMaterial">Material for the sloped roof planes. Null uses fallback color.</param>
        /// <param name="baseSlabHeight">Height of the 3D base slab beneath the slopes. 0 disables the slab.</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddHipRoof(
            float ridgeHeight = Constants.Roof.DefaultRidgeHeight,
            float overhang = Constants.Roof.DefaultOverhang,
            bool? ridgeAlongX = null,
            Color? roofColor = null,
            Material? roofMaterial = null,
            float baseSlabHeight = Constants.Roof.DefaultBaseSlabHeight)
        {
            var roof = GetRoofBuilder().AddHipRoof(ridgeHeight, overhang, ridgeAlongX,
                roofColor, roofMaterial, baseSlabHeight);
            _registry.Register(BuildingPart.Roof, roof);
            return this;
        }

        /// <summary>
        /// Add structural pillars at corners.
        /// </summary>
        /// <param name="width">Pillar width in meters</param>
        /// <param name="material">Optional material override</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddCornerPillars(float width = 0.4f, Material? material = null)
        {
            var pillars = GetDecorBuilder().AddCornerPillars(width, material);
            _registry.Register(BuildingPart.Pillars, pillars);
            return this;
        }

        /// <summary>
        /// Add thin vertical trim strips at the four corners of the building.
        /// Each corner gets two perpendicular strips forming a right angle that seamlessly
        /// connects with horizontal trims (<see cref="AddRoofTrim"/>, <see cref="AddBaseMolding"/>).
        /// </summary>
        /// <param name="width">Visible width of each trim strip on the wall face in meters</param>
        /// <param name="depth">How far the trim protrudes past the wall surface in meters</param>
        /// <param name="material">Optional material override</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddCornerTrim(float width = 0.3f, float depth = 0.1f, Material? material = null)
        {
            var cornerTrim = GetDecorBuilder().AddCornerTrim(width, depth, material);
            _registry.Register(BuildingPart.Trim, cornerTrim);
            return this;
        }

        /// <summary>
        /// Add foundation beneath the building.
        /// </summary>
        /// <param name="height">Foundation depth in meters</param>
        /// <param name="expandX">Extra expansion on X axis</param>
        /// <param name="expandZ">Extra expansion on Z axis</param>
        /// <param name="color">Optional color override (defaults to grey)</param>
        /// <param name="material">Optional material override</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddFoundation(float height = 2.0f, float expandX = 0f, float expandZ = 0f, Color? color = null, Material? material = null)
        {
            _foundationHeight = height;
            _foundationExpandX = expandX;
            _foundationExpandZ = expandZ;
            var foundation = GetDecorBuilder().AddFoundation(height, expandX, expandZ, color, material);
            _registry.Register(BuildingPart.Foundation, foundation);
            return this;
        }

        /// <summary>
        /// Add stairs from ground level up to the building floor on the specified wall.
        /// Automatically aligns with the door opening offset on the specified wall.
        /// Supports multiple visual styles: Solid (default concrete box steps), ClosedRiser (two-tone wood with risers),
        /// or OpenStringer (plank treads on diagonal stringer beams).
        /// </summary>
        /// <param name="wall">Which wall the stairs attach to</param>
        /// <param name="foundationHeight">Foundation height in meters (must match AddFoundation height)</param>
        /// <param name="maxStepHeight">Maximum height per step. Lower values create more, shallower steps. (Solid only)</param>
        /// <param name="width">Step width in meters (Solid only)</param>
        /// <param name="stepDepth">Step depth (tread) in meters. Controls how far stairs extend outward. (Solid only)</param>
        /// <param name="color">Optional color override — defaults to palette floor color (Solid only)</param>
        /// <param name="material">Optional material override — defaults to palette floor material (Solid only)</param>
        /// <param name="style">Visual style of stairs to generate</param>
        /// <param name="flushWithFloor">If true, topmost step is flush with floor level. If false (default), topmost step is one step below floor. (Solid only)</param>
        /// <param name="gap">Vertical gap between foundation edge and top step. ClosedRiser/OpenStringer default to 0 (flush).</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddStairs(
            WallSide wall,
            float foundationHeight = 2.0f,
            float maxStepHeight = Constants.Spatial.DefaultMaxStepHeight,
            float width = 2.5f,
            float stepDepth = Constants.Spatial.DefaultStepDepth,
            Color? color = null,
            Material? material = null,
            StairStyle style = StairStyle.Solid,
            bool flushWithFloor = false,
            float gap = 0f)
        {
            float lateralOffset = GetDoorOffset(wall);
            _stairs.Add(new StairSpec(wall, foundationHeight, width, lateralOffset,
                maxStepHeight, stepDepth, style, flushWithFloor));
            var stairs = GetDecorBuilder().AddStairs(wall, foundationHeight, maxStepHeight, width, stepDepth, color, material, style, flushWithFloor, gap, lateralOffset);
            _registry.Register(BuildingPart.Stairs, stairs);
            return this;
        }

        /// <summary>
        /// Add trim-style door frames around door openings.
        /// Must be called after AddWalls.
        /// </summary>
        /// <param name="material">Optional material override</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddDoorFrames(Material? material = null)
        {
            var frames = GetDecorBuilder().AddDoorFrames(
                _northOpening, _southOpening, _eastOpening, _westOpening, material);
            _registry.Register(BuildingPart.Trim, frames);
            return this;
        }

        /// <summary>
        /// Add trim-style door frames around interior doorway openings.
        /// Must be called after AddInteriorWall.
        /// </summary>
        /// <param name="material">Optional material override</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddInteriorDoorFrames(Material? material = null)
        {
            if (_interiorWallBuilder != null && _interiorWallBuilder.Doorways.Count > 0)
            {
                var frames = GetDecorBuilder().AddInteriorDoorFrames(_interiorWallBuilder.Doorways, material: material);
                _registry.Register(BuildingPart.Trim, frames);
            }
            return this;
        }

        /// <summary>
        /// Add base molding around the bottom of the building.
        /// </summary>
        /// <param name="height">Molding height in meters</param>
        /// <param name="depth">Molding depth in meters</param>
        /// <param name="material">Optional material override</param>
        /// <param name="skipWalls">Wall sides to omit molding from (null = include all walls)</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddBaseMolding(float height = 0.3f, float depth = 0.1f, Material? material = null,
            IEnumerable<WallSide>? skipWalls = null)
        {
            var molding = GetDecorBuilder().AddBaseMolding(height, depth, material,
                _northOpening, _southOpening, _eastOpening, _westOpening, skipWalls);
            _registry.Register(BuildingPart.Trim, molding);
            return this;
        }

        #endregion

        #region Lighting

        /// <summary>
        /// Add ceiling lights distributed across the room.
        /// </summary>
        /// <param name="intensity">Light intensity</param>
        /// <param name="color">Light color</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddLights(float? intensity = null, Color? color = null)
        {
            GetLightingBuilder().AddCeilingLights(intensity, color);
            return this;
        }

        /// <summary>
        /// Add ambient fill lighting.
        /// </summary>
        /// <param name="intensity">Ambient light intensity</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddAmbientLighting(float intensity = 0.3f)
        {
            GetLightingBuilder().AddAmbientLighting(intensity);
            return this;
        }

        #endregion

        #region Furniture

        /// <summary>
        /// Add furniture at a semantic position.
        /// </summary>
        /// <param name="type">Type of furniture</param>
        /// <param name="position">Semantic position (center, north, south, east, west, northeast, etc.)</param>
        /// <param name="color">Optional color override</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddFurniture(FurnitureType type, string position, Color? color = null)
        {
            (Vector3 pos, Quaternion rot) = ParseSemanticPosition(position, GetOptimalMargin(type));
            GetFurnitureBuilder().Create(type, pos, rot, color);
            return this;
        }

        /// <summary>
        /// Add furniture at exact coordinates.
        /// </summary>
        /// <param name="type">Type of furniture</param>
        /// <param name="position">Local position</param>
        /// <param name="rotation">Local rotation</param>
        /// <param name="color">Optional color override</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddFurniture(FurnitureType type, Vector3 position, Quaternion rotation, Color? color = null)
        {
            GetFurnitureBuilder().Create(type, position, rotation, color);
            return this;
        }

        #endregion

        #region Prefabs

        /// <summary>
        /// Place a game prefab at the specified position.
        /// </summary>
        /// <param name="prefab">Prefab reference from GamePrefabs</param>
        /// <param name="position">Local position</param>
        /// <param name="rotation">Local rotation</param>
        /// <param name="onCreated">Optional callback invoked with the instantiated GameObject</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddPrefab(PrefabRef prefab, Vector3 position, Quaternion rotation, Action<GameObject>? onCreated = null)
        {
            GameObject? instance = GetPrefabPlacer().Place(prefab, position, rotation);
            if (instance != null)
            {
                onCreated?.Invoke(instance);
            }
            return this;
        }

        /// <summary>
        /// Add sliding double doors at a door opening. Server only — returns the builder
        /// unchanged on clients (the door is replicated via FishNet).
        /// </summary>
        /// <remarks>
        /// <para><strong>Multiplayer behavior:</strong></para>
        /// <list type="bullet">
        /// <item><description><strong>Server/host:</strong> Instantiates and network-spawns the door.
        /// FishNet replicates it to all clients. The <paramref name="onCreated"/> callback fires.</description></item>
        /// <item><description><strong>Client:</strong> Returns immediately — the door is not created locally.
        /// The server-spawned door arrives via FishNet replication and is automatically parented
        /// to the building hierarchy with material/text customization applied.</description></item>
        /// </list>
        /// <para><strong>DoorController server-gating:</strong> The game's DoorController gates
        /// ALL proximity sensor callbacks (PlayerVicinityEnter/Exit, NPCVicinityEnter/Exit) and
        /// auto-close logic behind <c>InstanceFinder.IsServer</c>. This means on clients:</para>
        /// <list type="bullet">
        /// <item><description>Doors will NOT auto-open when players/NPCs approach</description></item>
        /// <item><description>Doors will NOT auto-close when players/NPCs leave</description></item>
        /// <item><description>Manual interaction (clicking the door handle) DOES work via ServerRpc</description></item>
        /// </list>
        /// <para>S1MAPI cannot implement client-side auto-open/close because it requires game assembly
        /// types (DoorController, EDoorSide, Player) that S1MAPI intentionally does not reference.
        /// Consumer mods that need this behavior must implement their own proximity polling
        /// using DoorController.SetIsOpen_Server (public, RequireOwnership=false, RunLocally=true).</para>
        /// </remarks>
        /// <param name="position">Local position for doors</param>
        /// <param name="rotation">Local rotation</param>
        /// <param name="openingHours">Text for opening hours sign</param>
        /// <param name="onCreated">Optional callback invoked with the instantiated door GameObject
        /// before activation. Runs after internal customization (material, opening hours text) but
        /// before Awake/OnEnable fire, so sensors see configured values.
        /// Only fires on the server — will NOT fire on clients.</param>
        /// <returns>This builder for chaining</returns>
        public BuildingBuilder AddSlidingDoors(Vector3 position, Quaternion rotation, string openingHours = "6AM-6PM", Action<GameObject>? onCreated = null)
        {
            GetPrefabPlacer().PlaceSlidingDoors(position, rotation, openingHours, Materials.MetalDarkGrey, onCreated);
            return this;
        }

        #endregion

        #region Build

        /// <summary>
        /// Finalize and return the building GameObject.
        /// </summary>
        /// <returns>The completed building</returns>
        public GameObject Build()
        {
            DebugLog.Info($"[BuildingBuilder] Built '{_name}' ({_roomSize.x}x{_roomSize.y}x{_roomSize.z}m)");
            return _root;
        }

        /// <summary>
        /// Finalize and return the building, then execute a post-build action.
        /// </summary>
        /// <param name="postBuild">Action to execute with the completed building</param>
        /// <returns>The completed building</returns>
        public GameObject Build(Action<GameObject> postBuild)
        {
            var building = Build();
            postBuild?.Invoke(building);
            return building;
        }

        /// <summary>
        /// Get the root GameObject before Build() is called.
        /// </summary>
        public GameObject Root => _root;

        /// <summary>
        /// Get the current room size.
        /// </summary>
        public Vector3 RoomSize => _roomSize;

        /// <summary>
        /// Get the current configuration.
        /// </summary>
        public BuildingConfig Config => _config;

        /// <summary>
        /// Post-build registry for targeting specific building parts (walls, floor, trim, etc.).
        /// </summary>
        public BuildingPartRegistry Registry => _registry;

        /// <summary>
        /// Grid cell size computed from room dimensions.
        /// Furniture placement and interior pathfinding both use this value
        /// so the two grids are always aligned.
        /// </summary>
        public float GridCellSize =>
            BuildingUtilities.ComputeGridCellSize(_roomSize.x, _roomSize.z);

        #endregion

        #region Private Methods - Builder Access

        private void InvalidateBuilders()
        {
            // Preserve interior doorway metadata before clearing the builder
            if (_interiorWallBuilder != null && _interiorWallBuilder.Doorways.Count > 0)
                _interiorDoorwaysCache = _interiorWallBuilder.Doorways;

            _wallBuilder = null;
            _furnitureBuilder = null;
            _lightingBuilder = null;
            _decorBuilder = null;
            _roofBuilder = null;
            _interiorWallBuilder = null;
            // PrefabPlacer doesn't depend on room size
        }

        private WallBuilder GetWallBuilder(BuildingPalette? palette = null)
        {
            return _wallBuilder ??= new WallBuilder(_root.transform, _roomSize, _config.WallThickness, palette ?? _config.Palette);
        }

        private FurnitureBuilder GetFurnitureBuilder()
        {
            if (_furnitureBuilder == null)
            {
                var container = BuildingUtilities.CreateFolder("Furniture", _root.transform);
                _furnitureBuilder = new FurnitureBuilder(container.transform, _config.Palette);
            }
            return _furnitureBuilder;
        }

        private LightingBuilder GetLightingBuilder()
        {
            return _lightingBuilder ??= new LightingBuilder(_root.transform, _roomSize, _config.Palette);
        }

        private DecorBuilder GetDecorBuilder()
        {
            return _decorBuilder ??= new DecorBuilder(_root.transform, _roomSize, _config.Palette);
        }

        private RoofBuilder GetRoofBuilder()
        {
            return _roofBuilder ??= new RoofBuilder(_root.transform, _roomSize, _config.WallThickness, _config.Palette);
        }

        private InteriorWallBuilder GetInteriorWallBuilder()
        {
            return _interiorWallBuilder ??= new InteriorWallBuilder(
                _root.transform, _roomSize, _config.WallThickness, _config.Palette, _interiorWallLayer);
        }

        private PrefabPlacer GetPrefabPlacer()
        {
            return _prefabPlacer ??= new PrefabPlacer(_root.transform);
        }

        private float GetDoorOffset(WallSide wall)
        {
            WallOpening? opening = wall switch
            {
                WallSide.North => _northOpening,
                WallSide.South => _southOpening,
                WallSide.East => _eastOpening,
                WallSide.West => _westOpening,
                _ => null
            };
            return opening?.Offset ?? 0f;
        }

        private void RegisterWallChildren(GameObject wallsContainer)
        {
            for (int i = 0; i < wallsContainer.transform.childCount; i++)
            {
                Transform child = wallsContainer.transform.GetChild(i);
                WallSide? side = ParseWallSide(child.name);

                // Solid wall — the child itself is the wall segment (has a Renderer)
                if (child.GetComponent<Renderer>() != null)
                {
                    if (side.HasValue)
                        _registry.Register(side.Value, child.gameObject);
                    else
                        _registry.Register(BuildingPart.ExteriorWalls, child.gameObject);
                    continue;
                }

                // Container (wall with door/window) — register individual wall segments,
                // skipping window frames and glass so they don't get material-swapped
                for (int j = 0; j < child.childCount; j++)
                {
                    Transform segment = child.GetChild(j);
                    string segName = segment.name;
                    if (segName.StartsWith(Constants.Window.FrameNamePrefix) || segName.Contains(Constants.Window.GlassNameSubstring))
                        continue;

                    if (side.HasValue)
                        _registry.Register(side.Value, segment.gameObject);
                    else
                        _registry.Register(BuildingPart.ExteriorWalls, segment.gameObject);
                }
            }
        }

        private static WallSide? ParseWallSide(string name)
        {
            if (name.StartsWith("North")) return WallSide.North;
            if (name.StartsWith("South")) return WallSide.South;
            if (name.StartsWith("East")) return WallSide.East;
            if (name.StartsWith("West")) return WallSide.West;
            return null;
        }

        #endregion

        #region Private Methods - NavMesh

        /// <summary>
        /// If <paramref name="opening"/> is a door, compute its center, inward normal,
        /// and optional stair base position, then append a <see cref="NavDoorwayInfo"/> to <paramref name="list"/>.
        /// </summary>
        private void TryAddExteriorDoor(
            WallSide wall, WallOpening? opening, List<NavDoorwayInfo> list)
        {
            if (opening == null || opening.Type != WallOpeningType.Door) return;

            Vector3 center = wall switch
            {
                WallSide.North => new Vector3(_roomSize.x / 2f + opening.Offset, 0f, _roomSize.z),
                WallSide.South => new Vector3(_roomSize.x / 2f + opening.Offset, 0f, 0f),
                WallSide.East => new Vector3(_roomSize.x, 0f, _roomSize.z / 2f + opening.Offset),
                WallSide.West => new Vector3(0f, 0f, _roomSize.z / 2f + opening.Offset),
                _ => Vector3.zero
            };

            Vector3 inward = wall switch
            {
                WallSide.North => Vector3.back,
                WallSide.South => Vector3.forward,
                WallSide.East => Vector3.left,
                WallSide.West => Vector3.right,
                _ => Vector3.zero
            };

            Vector3? stairBase = ComputeStairBasePosition(wall, center, inward);

            list.Add(new NavDoorwayInfo(
                center, opening.Width, opening.Height,
                inward, _config.WallThickness, stairBase));
        }

        /// <summary>
        /// Compute the ground-level position at the base of the stairs for a given wall.
        /// Returns null when no stairs exist on the wall or no foundation is present.
        /// </summary>
        private Vector3? ComputeStairBasePosition(WallSide wall, Vector3 doorCenter, Vector3 inwardNormal)
        {
            if (_foundationHeight <= 0f) return null;

            foreach (StairSpec spec in _stairs)
            {
                if (spec.Wall != wall) continue;

                // Mirror the step count and visible steps logic from DecorBuilder stair builders
                int stepCount = Mathf.Max(2, Mathf.CeilToInt(spec.FoundationHeight / spec.MaxStepHeight));
                int visibleSteps = spec.Style == StairStyle.ClosedRiser || spec.FlushWithFloor
                    ? stepCount
                    : stepCount - 1;

                // Match the clearance used by DecorBuilder: padding + foundation expand
                bool isNorthSouth = wall == WallSide.North || wall == WallSide.South;
                float foundationClearance = Constants.Spatial.FoundationPadding
                    + (isNorthSouth ? _foundationExpandZ : _foundationExpandX);

                // Bottom step far edge distance from wall = visibleSteps * stepDepth + clearance
                // Add half a step depth as standing buffer beyond the stair edge
                float stairRun = visibleSteps * spec.StepDepth + spec.StepDepth / 2f + foundationClearance;

                Vector3 outward = -inwardNormal;
                Vector3 stairBaseXZ = doorCenter + outward * stairRun;

                return new Vector3(stairBaseXZ.x, -spec.FoundationHeight, stairBaseXZ.z);
            }

            return null;
        }

        #endregion

        #region Private Methods - Positioning

        private float GetOptimalMargin(FurnitureType type)
        {
            float baseOffset = 0.15f; // Wall half-thickness + gap
            var footprint = FurnitureBuilder.GetFootprint(type);
            return Mathf.Max(footprint.x, footprint.z) / 2f + baseOffset;
        }

        private (Vector3 position, Quaternion rotation) ParseSemanticPosition(string position, float margin)
        {
            float x = _roomSize.x / 2f;
            float z = _roomSize.z / 2f;
            float cornerMargin = margin * 2.2f;

            return position.ToLowerInvariant() switch
            {
                "center" => (new Vector3(x, 0f, z), Quaternion.identity),
                "north" => (new Vector3(x, 0f, _roomSize.z - margin), Quaternion.Euler(0f, 180f, 0f)),
                "south" => (new Vector3(x, 0f, margin), Quaternion.identity),
                "east" => (new Vector3(_roomSize.x - margin, 0f, z), Quaternion.Euler(0f, -90f, 0f)),
                "west" => (new Vector3(margin, 0f, z), Quaternion.Euler(0f, 90f, 0f)),
                "northeast" => (new Vector3(_roomSize.x - cornerMargin, 0f, _roomSize.z - cornerMargin), Quaternion.Euler(0f, -135f, 0f)),
                "northwest" => (new Vector3(cornerMargin, 0f, _roomSize.z - cornerMargin), Quaternion.Euler(0f, 135f, 0f)),
                "southeast" => (new Vector3(_roomSize.x - cornerMargin, 0f, cornerMargin), Quaternion.Euler(0f, -45f, 0f)),
                "southwest" => (new Vector3(cornerMargin, 0f, cornerMargin), Quaternion.Euler(0f, 45f, 0f)),
                _ => (new Vector3(x, 0f, z), Quaternion.identity)
            };
        }

        #endregion
    }
}
