using S1MAPI.Building.Config;
using S1MAPI.ProceduralMesh;
using UnityEngine;

namespace S1MAPI.Building.Components
{
    /// <summary>
    /// Creates lighting for buildings.
    /// Extracted from SemanticBuildingBuilder for SRP compliance.
    /// </summary>
    public sealed class LightingBuilder
    {
        #region Fields

        private readonly Transform _parent;
        private readonly Vector3 _roomSize;
        private readonly BuildingPalette _palette;

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new lighting builder.
        /// </summary>
        /// <param name="parent">Parent transform for lights</param>
        /// <param name="roomSize">Room dimensions for light placement</param>
        /// <param name="palette">Material and color palette</param>
        public LightingBuilder(Transform parent, Vector3 roomSize, BuildingPalette palette)
        {
            _parent = parent;
            _roomSize = roomSize;
            _palette = palette;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Add ceiling lights distributed across the room.
        /// Places lights in a grid pattern based on room size.
        /// </summary>
        /// <param name="intensity">Light intensity override (uses palette if null)</param>
        /// <param name="color">Light color override (uses palette if null)</param>
        /// <param name="spacing">Approximate spacing between lights in meters</param>
        /// <returns>The lights container GameObject</returns>
        public GameObject AddCeilingLights(float? intensity = null, Color? color = null, float spacing = 4f)
        {
            Color lightColor = color ?? _palette.LightColor;
            float lightIntensity = intensity ?? _palette.LightIntensity;

            GameObject lightsContainer = BuildingUtilities.CreateFolder("Lights", _parent);

            // Calculate grid based on room size
            int xCount = Mathf.Max(1, Mathf.RoundToInt(_roomSize.x / spacing));
            int zCount = Mathf.Max(1, Mathf.RoundToInt(_roomSize.z / spacing));

            float xStep = _roomSize.x / (xCount + 1);
            float zStep = _roomSize.z / (zCount + 1);
            float yPos = _roomSize.y - 0.3f; // Below ceiling (ceiling sits inside room at _roomSize.y - thickness)

            for (int x = 1; x <= xCount; x++)
            {
                for (int z = 1; z <= zCount; z++)
                {
                    Vector3 pos = new Vector3(x * xStep, yPos, z * zStep);

                    // Create point light
                    PrimitiveBuilder.CreatePointLight(
                        $"CeilingLight_{x}_{z}",
                        pos,
                        lightColor,
                        range: 8f,
                        intensity: lightIntensity,
                        parent: lightsContainer.transform);

                    // Create visual fixture (small emissive cylinder)
                    PrimitiveBuilder.CreateCylinder(
                        $"LightFixture_{x}_{z}",
                        pos + Vector3.up * 0.1f,
                        new Vector3(0.3f, 0.1f, 0.3f),
                        lightColor,
                        lightsContainer.transform);
                }
            }

            return lightsContainer;
        }

        /// <summary>
        /// Add a single point light at a specific position.
        /// </summary>
        /// <param name="position">Local position for the light</param>
        /// <param name="intensity">Light intensity</param>
        /// <param name="color">Light color</param>
        /// <param name="range">Light range in meters</param>
        /// <param name="includeFixture">Whether to add a visual fixture</param>
        /// <returns>The light GameObject</returns>
        public GameObject AddPointLight(Vector3 position, float intensity = 1f, Color? color = null, float range = 8f, bool includeFixture = true)
        {
            Color lightColor = color ?? _palette.LightColor;

            GameObject container = BuildingUtilities.CreateFolder("PointLight", _parent);

            PrimitiveBuilder.CreatePointLight(
                "Light",
                position,
                lightColor,
                range,
                intensity,
                container.transform);

            if (includeFixture)
            {
                PrimitiveBuilder.CreateCylinder(
                    "Fixture",
                    position + Vector3.up * 0.1f,
                    new Vector3(0.3f, 0.1f, 0.3f),
                    lightColor,
                    container.transform);
            }

            return container;
        }

        /// <summary>
        /// Add ambient lighting to the room (lower intensity fill lights).
        /// </summary>
        /// <param name="intensity">Base intensity for ambient lights</param>
        /// <returns>The ambient lights container</returns>
        public GameObject AddAmbientLighting(float intensity = 0.3f)
        {
            GameObject container = BuildingUtilities.CreateFolder("AmbientLights", _parent);

            // Four corner fill lights
            Vector3[] corners = {
                new(0.5f, _roomSize.y - 0.5f, 0.5f),
                new(_roomSize.x - 0.5f, _roomSize.y - 0.5f, 0.5f),
                new(0.5f, _roomSize.y - 0.5f, _roomSize.z - 0.5f),
                new(_roomSize.x - 0.5f, _roomSize.y - 0.5f, _roomSize.z - 0.5f)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                PrimitiveBuilder.CreatePointLight(
                    $"AmbientLight_{i}",
                    corners[i],
                    _palette.LightColor,
                    range: _roomSize.magnitude / 2f,
                    intensity: intensity,
                    parent: container.transform);
            }

            return container;
        }

        #endregion
    }
}
