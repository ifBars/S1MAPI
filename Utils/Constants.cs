namespace S1MAPI.Utils
{
    /// <summary>
    /// Core constants for S1MAPI library
    /// </summary>
    internal static class Constants
    {
        public const string LIBRARY_NAME = "S1MAPI";
        public const string LIBRARY_VERSION = "1.0.0";
        public const string LIBRARY_AUTHOR = "Bars";

        /// <summary>
        /// Minimum supported Unity version
        /// </summary>
        public const string MIN_UNITY_VERSION = "2019.4";

        /// <summary>
        /// Recommended Unity version
        /// </summary>
        public const string RECOMMENDED_UNITY_VERSION = "2022.3.62f2";

        /// <summary>
        /// Layer names used by S1MAPI components
        /// </summary>
        public static class Layers
        {
            public const string DEFAULT = "Default";
            public const string IGNORE_RAYCAST = "Ignore Raycast";
        }

        /// <summary>
        /// Tag names used by S1MAPI components
        /// </summary>
        public static class Tags
        {
            public const string UNTAGGED = "Untagged";
        }

        /// <summary>
        /// Mesh generation and processing constants
        /// </summary>
        public static class Mesh
        {
            /// <summary>
            /// Maximum vertices per mesh allowed by Unity
            /// </summary>
            public const int MaxVerticesPerMesh = 65535;

            /// <summary>
            /// Threshold distance for welding vertices
            /// </summary>
            public const float VertexWeldThreshold = 0.001f;

            /// <summary>
            /// Default number of radial segments for cylinders
            /// </summary>
            public const int DefaultCylinderSegments = 12;

            /// <summary>
            /// Default subdivision count for sphere generation
            /// </summary>
            public const int DefaultSphereSubdivisions = 8;

            /// <summary>
            /// Default subdivision count for capsule hemispheres
            /// </summary>
            public const int DefaultCapsuleSubdivisions = 6;
        }

        /// <summary>
        /// Material and rendering constants
        /// </summary>
        public static class Materials
        {
            /// <summary>
            /// Default alpha value for transparent materials
            /// </summary>
            public const float DefaultTransparencyAlpha = 0.5f;

            /// <summary>
            /// Alpha value for window/glass materials
            /// </summary>
            public const float WindowGlassAlpha = 0.35f;

            /// <summary>
            /// Alpha value for general glass materials
            /// </summary>
            public const float GlassAlpha = 0.3f;
        }

        /// <summary>
        /// Resource loading constants
        /// </summary>
        public static class Resources
        {
            /// <summary>
            /// Default pixels per unit for sprite creation
            /// </summary>
            public const float DefaultPixelsPerUnit = 100f;
        }

        /// <summary>
        /// Building and spatial constants
        /// </summary>
        public static class Spatial
        {
            /// <summary>
            /// Default grid cell size for snapping operations
            /// </summary>
            public const float DefaultGridSize = 0.5f;
        }

        /// <summary>
        /// Terrain and area clearing constants.
        /// </summary>
        public static class Terrain
        {
            /// <summary>
            /// Default padding around clearing bounds in meters.
            /// </summary>
            public const float DefaultClearingPadding = 2f;

            /// <summary>
            /// Default name patterns for vegetation and natural clutter.
            /// Used by the vegetation clearing pass to remove nature objects near buildings.
            /// </summary>
            public static readonly string[] DefaultVegetationKeywords =
            {
                "Rock", "Boulder", "Shrub", "Bush"
            };

            /// <summary>
            /// Name patterns for objects protected from footprint destruction.
            /// These typically extend far beyond the building and create visual gaps.
            /// </summary>
            public static readonly string[] DefaultProtectedKeywords =
            {
                "Road", "Sidewalk"
            };
        }

        /// <summary>
        /// GLTF file format constants
        /// </summary>
        public static class Gltf
        {
            /// <summary>
            /// GLB magic number ("glTF" in little-endian)
            /// </summary>
            public const uint GlbMagic = 0x46546C67;

            /// <summary>
            /// JSON chunk type identifier
            /// </summary>
            public const uint ChunkTypeJson = 0x4E4F534A;

            /// <summary>
            /// Binary chunk type identifier
            /// </summary>
            public const uint ChunkTypeBin = 0x004E4942;

            /// <summary>
            /// Current supported GLTF version
            /// </summary>
            public const int SupportedVersion = 2;

            /// <summary>
            /// Default name for imported models
            /// </summary>
            public const string DefaultModelName = "GltfModel";
        }
    }
}
