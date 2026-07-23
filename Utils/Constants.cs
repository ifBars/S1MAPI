namespace S1MAPI.Utils
{
    /// <summary>
    /// Core constants for S1MAPI library
    /// </summary>
    internal static class Constants
    {
        public const string LIBRARY_NAME = "S1MAPI";
        public const string LIBRARY_VERSION = "2.0.1";
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
            /// <summary>Default Unity layer.</summary>
            public const string DEFAULT = "Default";
            /// <summary>Ignore Raycast Unity layer.</summary>
            public const string IGNORE_RAYCAST = "Ignore Raycast";
        }

        /// <summary>
        /// Tag names used by S1MAPI components
        /// </summary>
        public static class Tags
        {
            /// <summary>Default Unity tag for untagged objects.</summary>
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

            /// <summary>
            /// Scene material name for closed riser tread planks.
            /// </summary>
            public const string TreadWoodName = "wood brown";

            /// <summary>
            /// Scene material name for closed riser faces.
            /// </summary>
            public const string RiserWoodName = "wood_beige";

            /// <summary>
            /// Scene material name for open stringer tread planks.
            /// </summary>
            public const string StringerTreadWoodName = "mansion_brownwood_mat";

            /// <summary>
            /// Scene material name for open stringer diagonal beams.
            /// </summary>
            public const string StringerBeamWoodName = "wood brown";
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

            /// <summary>
            /// Default maximum step height for generated stairs.
            /// Kept below typical CharacterController stepOffset (~0.3m) for reliable climbing.
            /// </summary>
            public const float DefaultMaxStepHeight = 0.20f;

            /// <summary>
            /// Default step depth (tread) for generated stairs in meters.
            /// </summary>
            public const float DefaultStepDepth = 0.3f;

            /// <summary>
            /// Base padding around the foundation block in meters.
            /// Used by DecorBuilder to offset the foundation beyond room bounds
            /// and by BuildingBuilder to compute stair clearance for navigation.
            /// </summary>
            public const float FoundationPadding = 0.1f;

            /// <summary>
            /// GameObject folder name for stair geometry under the building root.
            /// Used by DecorBuilder to parent step colliders and by NavigationBuilder
            /// to exclude them from NavMesh source collection.
            /// </summary>
            public const string StairsFolderName = "Stairs";

            /// <summary>
            /// GameObject folder name for foundation geometry under the building root.
            /// Excluded from NavMesh source collection because the foundation box's bottom
            /// face creates a phantom walkable surface at ground level inside the building.
            /// The floor collider provides the correct walkable surface instead.
            /// </summary>
            public const string FoundationFolderName = "Foundation";

        }

        /// <summary>
        /// Window geometry and rendering constants.
        /// </summary>
        public static class Window
        {
            /// <summary>
            /// Default divider width between adjacent window panes in meters.
            /// </summary>
            public const float DefaultDividerWidth = 0.15f;

            /// <summary>
            /// Maximum individual pane width for multi-pane windows in meters.
            /// </summary>
            public const float MaxPaneWidth = 2.0f;

            /// <summary>
            /// Minimum individual pane width in meters. Pane count is auto-reduced if panes would be narrower.
            /// </summary>
            public const float MinPaneWidth = 0.3f;

            /// <summary>
            /// Minimum gap between window panes and wall edges in meters.
            /// </summary>
            public const float MinGap = 0.15f;

            /// <summary>
            /// Minimum horizontal margin reserved for side walls in a window section.
            /// </summary>
            public const float SideMargin = 0.5f;

            /// <summary>
            /// Minimum vertical margin reserved for header and sill in a window section.
            /// </summary>
            public const float VerticalMargin = 1.2f;

            /// <summary>
            /// Minimum side width required to place a window in a door side segment.
            /// </summary>
            public const float MinDoorSideWidth = 1.0f;

            /// <summary>
            /// Window frame depth in meters.
            /// </summary>
            public const float FrameDepth = 0.05f;

            /// <summary>
            /// Window frame member width in meters.
            /// </summary>
            public const float FrameWidth = 0.1f;

            /// <summary>
            /// Geometry threshold below which wall segments are not created.
            /// </summary>
            public const float SegmentThreshold = 0.01f;

            /// <summary>
            /// Maximum width of the solid strip next to a door in door-with-windows walls.
            /// </summary>
            public const float MaxDoorStripWidth = 0.5f;

            /// <summary>
            /// Minimum width of the solid strip next to a door in door-with-windows walls.
            /// </summary>
            public const float MinDoorStripWidth = 0.3f;

            /// <summary>
            /// Margin subtracted from the available side width when sizing the door strip.
            /// </summary>
            public const float DoorStripMargin = 0.4f;

            /// <summary>
            /// Name prefix used for window frame GameObjects.
            /// Used by registration to exclude frames from wall material swaps.
            /// </summary>
            public const string FrameNamePrefix = "Frame";

            /// <summary>
            /// Substring present in window glass GameObject names.
            /// Used by registration to exclude glass from wall material swaps.
            /// </summary>
            public const string GlassNameSubstring = "Glass";
        }

        /// <summary>
        /// Roof geometry and style constants.
        /// </summary>
        public static class Roof
        {
            /// <summary>
            /// Parapet wall height for the Deep preset in meters.
            /// </summary>
            public const float DeepParapetHeight = 0.6f;

            /// <summary>
            /// Cap height for the Deep preset in meters.
            /// </summary>
            public const float DeepCapHeight = 0.25f;

            /// <summary>
            /// Cap overhang past the parapet wall for the Deep preset in meters.
            /// </summary>
            public const float DeepCapOverhang = 0.15f;

            /// <summary>
            /// Parapet wall height for the Shallow preset in meters.
            /// </summary>
            public const float ShallowParapetHeight = 0.3f;

            /// <summary>
            /// Cap height for the Shallow preset in meters.
            /// </summary>
            public const float ShallowCapHeight = 0.15f;

            /// <summary>
            /// Cap overhang past the parapet wall for the Shallow preset in meters.
            /// </summary>
            public const float ShallowCapOverhang = 0.05f;

            /// <summary>
            /// Extra depth added to wall thickness for parapet trim depth.
            /// </summary>
            public const float ParapetDepthPadding = 0.1f;

            /// <summary>
            /// Default ridge height above the ceiling for gable roofs in meters.
            /// </summary>
            public const float DefaultRidgeHeight = 2.0f;

            /// <summary>
            /// Default eave overhang past walls for gable roofs in meters.
            /// </summary>
            public const float DefaultOverhang = 0.3f;

            /// <summary>
            /// Height of the 3D base slab beneath gable and hip roofs in meters.
            /// </summary>
            public const float DefaultBaseSlabHeight = 0.15f;

            /// <summary>
            /// Scene material name for hip roof slopes.
            /// </summary>
            public const string RoofSlopeMaterialName = "mansion_roof_mat";

            /// <summary>Fallback roof color red component.</summary>
            public const float DefaultRoofColorR = 0.45f;
            /// <summary>Fallback roof color green component.</summary>
            public const float DefaultRoofColorG = 0.35f;
            /// <summary>Fallback roof color blue component.</summary>
            public const float DefaultRoofColorB = 0.3f;
        }

        /// <summary>
        /// NavigationBuilder constants.
        /// </summary>
        public static class NavMesh
        {
            /// <summary>
            /// Minimum width for stair ramps in meters.
            /// Wider ramps allow more NPCs to traverse simultaneously.
            /// </summary>
            public const float MinRampWidth = 3.0f;

            /// <summary>
            /// Extra width added to stair ramps beyond the door width to compensate
            /// for NavMesh agent-radius erosion on both edges.
            /// </summary>
            public const float RampErosionBuffer = 1.0f;

            /// <summary>
            /// Thickness of invisible ramp colliders in meters.
            /// Thin enough to not interfere with gameplay, thick enough for NavMesh voxelization.
            /// </summary>
            public const float RampColliderThickness = 0.1f;
        }

        /// <summary>
        /// Interior A* pathfinding constants.
        /// </summary>
        public static class InteriorNav
        {
            /// <summary>Maximum target cell size for the interior pathfinding grid.
            /// Actual cell size is computed by <see cref="Building.BuildingUtilities.ComputeGridCellSize"/>
            /// to evenly divide the room dimensions.</summary>
            public const float MaxGridCellSize = Spatial.DefaultGridSize;

            /// <summary>Wall margin — cells within this distance of walls are unwalkable.
            /// Must be less than CellSize/2 (0.25m) so NPCs can walk on 1-cell-wide paths.</summary>
            public const float WallMargin = 0.15f;

            /// <summary>Re-pathfind interval for chase mode in seconds.</summary>
            public const float ChaseRepathInterval = 0.2f;

            /// <summary>Distance threshold for considering NPC arrived at an intermediate waypoint.</summary>
            public const float WaypointArrivalThreshold = 0.3f;

            /// <summary>Distance threshold for considering NPC arrived at final destination.</summary>
            public const float DestinationArrivalThreshold = 0.5f;

            /// <summary>Distance for detecting NPC arrival at doorway exterior point.
            /// Generous threshold ensures the NPC triggers entry even if the NavMesh agent
            /// stops short of the exact exterior point due to carving boundary erosion.</summary>
            public const float DoorwayApproachThreshold = 4.0f;

            /// <summary>Approach threshold for stair doorways. Tighter than
            /// <see cref="DoorwayApproachThreshold"/> so the NPC walks all the way
            /// to the stair base before the lerp takes over.</summary>
            public const float StairApproachThreshold = 1.5f;

            /// <summary>Distance threshold for phase transitions during doorway entry/exit.
            /// If the NPC is closer than this to the next phase target, skip to the following phase.</summary>
            public const float PhaseTransitionThreshold = 0.15f;

            /// <summary>Max horizontal distance from doorway for agent-done entry on stair doorways.
            /// Tighter than non-stair (8m) to avoid triggering entry from the sidewalk.</summary>
            public const float StairMaxEntryDistance = 3f;

            /// <summary>Distance threshold for non-stair Phase 1 (angle correction).
            /// If the NPC is farther than this from the exterior point, Phase 1 walks
            /// to the exterior first to correct approach angle before entering.</summary>
            public const float ExteriorAngleCorrectionThreshold = 1.0f;

            /// <summary>Time in seconds before an NPC in the Exiting state is considered stuck.</summary>
            public const float ExitStuckTimeout = 3.0f;

            /// <summary>Squared displacement below which the stuck timer accumulates during exit.</summary>
            public const float ExitStuckDisplacementSq = 0.25f;

            /// <summary>Rotation speed in degrees per second for NPC facing direction.</summary>
            public const float RotationSpeed = 360f;
        }

        /// <summary>
        /// Interior wall geometry constants.
        /// </summary>
        public static class InteriorWall
        {
            /// <summary>
            /// Minimum wall length in meters.
            /// </summary>
            public const float MinWallLength = 0.5f;

            /// <summary>
            /// Geometry threshold below which wall segments are not created.
            /// </summary>
            public const float SegmentThreshold = 0.01f;
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
                "Rock", "Boulder", "Shrub", "Bush", "Tree rustle", "Foliage"
            };

            /// <summary>
            /// Name patterns for objects protected from footprint destruction.
            /// These typically extend far beyond the building and create visual gaps.
            /// </summary>
            public static readonly string[] DefaultProtectedKeywords =
            {
                "Road", "Sidewalk", "Wedge"
            };

            /// <summary>
            /// Default padding around terrain flattening bounds in meters.
            /// </summary>
            public const float DefaultFlattenPadding = 0.5f;

            /// <summary>
            /// Default blend distance for terrain edge smoothing in meters.
            /// Terrain smoothly transitions from the flattened height back to
            /// natural terrain over this distance.
            /// </summary>
            public const float DefaultBlendDistance = 3f;
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
