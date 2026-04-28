using Godot;

public partial class FogOfWarTileMap : CustomTileMapLayer
{
    protected void InitializeTileStates()
    {
        foreach (var cell in GetUsedCells())
        {
            Vector2 worldPosition = MapToLocal(cell) + (TileSet.TileSize / 2);

            foreach (var zone in TileModifierZones)
            {
                if (IsPointInZone(zone, worldPosition))
                    continue;

                foreach (var modifier in zone.Modifiers)
                {
                    switch (modifier)
                    {
                        case FogModifierResource f:

                            break;
                    }
                }
            }
        }
    }
}