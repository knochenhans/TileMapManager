using Godot;
using Godot.Collections;

public partial class CustomTileMapLayer : TileMapLayer
{
	[Export] public bool GroundLayer = false;
	[Export] public int OriginalSourceID = -1;
	[Export] public int DestroyedSourceID = -1;

	[Signal] public delegate void TileHitEventHandler(Vector2 position, int damage, string material);
	[Signal] public delegate void TileDestroyedEventHandler(Vector2 position, string material);

	private Array<Vector2I> DestroyedTiles = [];

	private Dictionary<Vector2I, int> tileHealth = [];

	public Array<TileMapLayer> TopTileMapLayers;

	public void OnHit(Vector2 Position)
	{
		// Get the tile at the given position
		var tile = LocalToMap(Position);
		var atlasCoord = GetCellAtlasCoords(tile);
		var tileID = GetCellSourceId(tile);
		var material = string.Empty;

		if (tileID > -1)
		{
			var tileData = GetCellTileData(tile);
			var isDestructible = tileData.GetCustomData("destructible");
			material = tileData.GetCustomData("material").AsString();

			if (isDestructible.AsBool())
			{
				// If the tile is not in the dictionary, add its max health from the tile data
				if (!tileHealth.ContainsKey(tile))
					tileHealth[tile] = tileData.GetCustomData("health").AsInt16();

				var newHealth = tileHealth[tile] - 1;

				// Change tile into the next tile in the atlas
				if (newHealth <= 0)
				{
					SetCell(tile, tileID, new Vector2I(atlasCoord.X + 1, atlasCoord.Y));
					EmitSignal(SignalName.TileDestroyed, Position, material);
				}

				// Decrease the health of the tile in the dictionary
				tileHealth[tile] = newHealth;
			}
		}

		EmitSignal(SignalName.TileHit, Position, 1, material);
	}

	public void DestroyTile(Vector2I position)
	{
		// If OriginalSourceID is not set, use the current cell's source ID for simplicity
		if (OriginalSourceID == -1)
			OriginalSourceID = GetCellSourceId(position);

		var atlasCoordinates = GetCellAtlasCoords(position);
		SetCell(position, DestroyedSourceID, atlasCoordinates);
		DestroyedTiles.Add(position);
	}

	public void UndoDestroyTile(Vector2I position)
	{
		if (DestroyedTiles.Contains(position))
		{
			SetCell(position, OriginalSourceID);
			DestroyedTiles.Remove(position);
		}
	}
}
