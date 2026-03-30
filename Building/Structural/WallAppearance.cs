using UnityEngine;

namespace S1MAPI.Building.Structural
{
    /// <summary>
    /// Optional material and color overrides for a single exterior wall side.
    /// When a value is null, the palette default is used.
    /// </summary>
    public sealed class WallAppearance
    {
        /// <summary>Optional wall color override. Null uses the palette default.</summary>
        public Color? Color { get; }

        /// <summary>Optional wall material override. Null uses the palette default.</summary>
        public Material? Material { get; }

        /// <summary>Optional interior wall material override (room-facing side).
        /// Null uses the palette's <see cref="Config.BuildingPalette.InteriorWallMaterial"/> default.</summary>
        public Material? InteriorMaterial { get; }

        /// <summary>Optional interior wall color override.
        /// Null uses the palette's <see cref="Config.BuildingPalette.InteriorWallColor"/> (or WallColor) default.</summary>
        public Color? InteriorColor { get; }

        /// <summary>
        /// Create a wall appearance override.
        /// </summary>
        /// <param name="color">Optional color override (null = use palette)</param>
        /// <param name="material">Optional material override (null = use palette)</param>
        /// <param name="interiorMaterial">Optional interior material override (null = use palette)</param>
        /// <param name="interiorColor">Optional interior color override (null = use palette)</param>
        public WallAppearance(Color? color = null, Material? material = null,
            Material? interiorMaterial = null, Color? interiorColor = null)
        {
            Color = color;
            Material = material;
            InteriorMaterial = interiorMaterial;
            InteriorColor = interiorColor;
        }
    }
}
