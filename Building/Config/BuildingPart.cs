namespace S1MAPI.Building.Config
{
    /// <summary>
    /// Categories of building parts for post-build identification.
    /// Aligns with <see cref="BuildingPalette"/> material groups.
    /// </summary>
    public enum BuildingPart
    {
        /// <summary>All exterior wall segments (WallMaterial).</summary>
        ExteriorWalls,
        /// <summary>Floor slab (FloorMaterial).</summary>
        Floor,
        /// <summary>Ceiling slab (CeilingMaterial).</summary>
        Ceiling,
        /// <summary>Roof trim, corner trim, base molding, door frames (TrimMaterial).</summary>
        Trim,
        /// <summary>Corner pillars (PillarMaterial).</summary>
        Pillars,
        /// <summary>Secondary trim, parapet caps (AccentMaterial).</summary>
        Accent,
        /// <summary>Foundation block (FloorMaterial).</summary>
        Foundation,
        /// <summary>Parapet walls, hip roof slopes/slabs (WallMaterial).</summary>
        Roof,
        /// <summary>Stair geometry (FloorMaterial).</summary>
        Stairs,
        /// <summary>Interior wall segments (WallMaterial).</summary>
        InteriorWalls
    }
}
