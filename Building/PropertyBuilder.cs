using S1MAPI.Building.Config;
using S1MAPI.Building.Structural;
using S1MAPI.Building.Interior;
using S1MAPI.Building.Components;
using UnityEngine;
using S1MAPI.Core;
using S1MAPI.S1;

namespace S1MAPI.Building
{
    /// <summary>
    /// Fluent builder for constructing property structures with a clean, chainable API.
    /// Delegates to specialized builders (WallBuilder, FurnitureBuilder, etc.).
    /// </summary>
    /// <example>
    /// var property = new PropertyBuilder("MyWarehouse")
    ///     .DefineRoom(15f, 6f, 20f)
    ///     .AddFloor()
    ///     .AddCeiling()
    ///     .AddWalls(southDoor: true)
    ///     .AddGrid(new Vector3(15f, 0f, 20f))
    ///     .Build();
    /// </example>
    public sealed class PropertyBuilder
    {
        #region Fields

        private readonly string _name;
        private readonly GameObject _root;
        private BuildingConfig _config;
        private Vector3 _roomSize;

        private WallBuilder? _wallBuilder;
        private FurnitureBuilder? _furnitureBuilder;
        private LightingBuilder? _lightingBuilder;
        private DecorBuilder? _decorBuilder;
        private PrefabPlacer? _prefabPlacer;

        #endregion

        #region Constructor

        public PropertyBuilder(string name)
        {
            _name = name;
            _root = new GameObject(name);
            _config = BuildingConfig.Default;
            _roomSize = _config.Size;
        }

        #endregion

        #region Configuration

        public PropertyBuilder WithConfig(BuildingConfig config)
        {
            _config = config;
            _roomSize = config.Size;
            InvalidateBuilders();
            return this;
        }

        public PropertyBuilder WithPalette(BuildingPalette palette)
        {
            _config.Palette = palette;
            InvalidateBuilders();
            return this;
        }

        public PropertyBuilder DefineRoom(float width, float height, float depth)
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

        public PropertyBuilder AddFloor(Color? color = null, Material? material = null)
        {
            var palette = color.HasValue || material != null
                ? _config.Palette.Clone().WithFloor(material!)
                : _config.Palette;

            if (color.HasValue)
            {
                palette.FloorColor = color.Value;
            }
            if (material != null)
            {
                palette.FloorMaterial = material;
            }

            GetDecorBuilder(palette).AddFloor(_config.FloorThickness);
            return this;
        }

        public PropertyBuilder AddCeiling(Color? color = null, Material? material = null)
        {
            var palette = _config.Palette;
            if (color.HasValue || material != null)
            {
                palette = palette.Clone();
                if (color.HasValue) palette.CeilingColor = color.Value;
                if (material != null) palette.CeilingMaterial = material;
            }

            GetDecorBuilder(palette).AddCeiling(_config.CeilingThickness);
            return this;
        }

        public PropertyBuilder AddWalls(
            bool northDoor = false,
            bool southDoor = false,
            bool eastWindow = false,
            bool westWindow = false,
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

            var builder = GetWallBuilder(palette);
            builder.BuildWalls(
                northOpening: northDoor ? WallOpening.Door() : null,
                southOpening: southDoor ? WallOpening.Door() : null,
                eastOpening: eastWindow ? WallOpening.Window() : null,
                westOpening: westWindow ? WallOpening.Window() : null);

            return this;
        }

        public PropertyBuilder AddWalls(
            WallOpening? north = null,
            WallOpening? south = null,
            WallOpening? east = null,
            WallOpening? west = null)
        {
            GetWallBuilder().BuildWalls(north, south, east, west);
            return this;
        }

        #endregion

        #region Grid System

        /// <summary>
        /// Add a placeholder GameObject for a grid. The S1API layer will configure the actual Grid component.
        /// This creates a child object named "Grid" that can be configured by PropertyPrefabBuilder.
        /// </summary>
        /// <param name="size">Grid size (width, depth).</param>
        /// <param name="cellSize">Cell size in meters.</param>
        /// <returns>This builder for chaining.</returns>
        public PropertyBuilder AddGridPlaceholder(Vector2? size = null, float cellSize = 1f)
        {
            var gridSize = size ?? new Vector2(_roomSize.x, _roomSize.z);

            var gridGO = new GameObject("Grid");
            gridGO.transform.SetParent(_root.transform, false);

            return this;
        }

        #endregion

        #region Decoration

        public PropertyBuilder AddRoofTrim(float height = 0.3f, Material? material = null)
        {
            GetDecorBuilder().AddRoofTrim(height, material);
            return this;
        }

        public PropertyBuilder AddSecondaryRoofTrim(float height = 0.15f, Material? material = null)
        {
            GetDecorBuilder().AddSecondaryRoofTrim(height, material);
            return this;
        }

        public PropertyBuilder AddCornerPillars(float width = 0.4f, Material? material = null)
        {
            GetDecorBuilder().AddCornerPillars(width, material);
            return this;
        }

        public PropertyBuilder AddFoundation(float height = 2.0f, float expandX = 0f, float expandZ = 0f)
        {
            GetDecorBuilder().AddFoundation(height, expandX, expandZ);
            return this;
        }

        public PropertyBuilder AddBaseMolding(float height = 0.3f, float depth = 0.1f, Material? material = null)
        {
            GetDecorBuilder().AddBaseMolding(height, depth, material);
            return this;
        }

        #endregion

        #region Lighting

        public PropertyBuilder AddLights(float? intensity = null, Color? color = null)
        {
            GetLightingBuilder().AddCeilingLights(intensity, color);
            return this;
        }

        public PropertyBuilder AddAmbientLighting(float intensity = 0.3f)
        {
            GetLightingBuilder().AddAmbientLighting(intensity);
            return this;
        }

        #endregion

        #region Furniture

        public PropertyBuilder AddFurniture(FurnitureType type, string position, Color? color = null)
        {
            (Vector3 pos, Quaternion rot) = ParseSemanticPosition(position, GetOptimalMargin(type));
            GetFurnitureBuilder().Create(type, pos, rot, color);
            return this;
        }

        public PropertyBuilder AddFurniture(FurnitureType type, Vector3 position, Quaternion rotation, Color? color = null)
        {
            GetFurnitureBuilder().Create(type, position, rotation, color);
            return this;
        }

        #endregion

        #region Prefabs

        public PropertyBuilder AddPrefab(PrefabRef prefab, Vector3 position, Quaternion rotation)
        {
            GetPrefabPlacer().Place(prefab, position, rotation);
            return this;
        }

        public PropertyBuilder AddSlidingDoors(Vector3 position, Quaternion rotation, string openingHours = "6AM-6PM")
        {
            GetPrefabPlacer().PlaceSlidingDoors(position, rotation, openingHours, Materials.MetalDarkGrey);
            return this;
        }

        #endregion

        #region Build

        public GameObject Build()
        {
            return _root;
        }

        public GameObject Build(Action<GameObject> postBuild)
        {
            var property = Build();
            postBuild?.Invoke(property);
            return property;
        }

        public GameObject Root => _root;

        public Vector3 RoomSize => _roomSize;

        public BuildingConfig Config => _config;

        #endregion

        #region Private Methods

        private void InvalidateBuilders()
        {
            _wallBuilder = null;
            _furnitureBuilder = null;
            _lightingBuilder = null;
            _decorBuilder = null;
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

        private DecorBuilder GetDecorBuilder(BuildingPalette? palette = null)
        {
            return _decorBuilder ??= new DecorBuilder(_root.transform, _roomSize, palette ?? _config.Palette);
        }

        private PrefabPlacer GetPrefabPlacer()
        {
            return _prefabPlacer ??= new PrefabPlacer(_root.transform);
        }

        private float GetOptimalMargin(FurnitureType type)
        {
            float baseOffset = 0.15f;
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
