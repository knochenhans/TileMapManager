using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class FogOfWarManager
{
    public TileMapLayer FogTilemap;    // Tilemap used for fog (opaque tiles)
    public Array<Node2D> Nodes;        // Player node to track
    public int RevealRadius = 3;       // Reveal radius in tiles
    public bool PermanentReveal = true;

    private HashSet<Vector2I> revealedTiles = []; // Memory of revealed tiles
    private Array<Vector2I> lastNodeTiles = [];

    public FogOfWarManager(TileMapLayer fogTilemap, Array<Node2D> nodes)
    {
        FogTilemap = fogTilemap;
        Nodes = nodes;
   
        if (FogTilemap == null)
        {
            GD.PushError("FogOfWarManager: FogTilemap not assigned!");
            return;
        }

        if (Nodes == null)
        {
            GD.PushError("FogOfWarManager: Player not assigned!");
            return;
        }

        for (int i = 0; i < Nodes.Count; i++)
        {
            var node = Nodes[i];
            if (node == null)
            {
                GD.PushError($"FogOfWarManager: Node at index {i} is null!");
                continue;
            }
            var tilePos = FogTilemap.LocalToMap(node.GlobalPosition);
            if (i >= lastNodeTiles.Count)
                lastNodeTiles.Add(tilePos);
            else
                lastNodeTiles[i] = tilePos;
            RevealTilesAroundPlayer(tilePos);
        }
    }

    public void Process(double delta)
    {
        for (int i = 0; i < Nodes.Count; i++)
        {
            var node = Nodes[i];
            if (node == null)
                continue;
            var pos = node.GlobalPosition;
            var currentTile = FogTilemap.LocalToMap(new Vector2I((int)pos.X, (int)pos.Y));
            if (i >= lastNodeTiles.Count || !currentTile.Equals((Vector2I)lastNodeTiles[i]))
            {
                if (i >= lastNodeTiles.Count)
                    lastNodeTiles.Add(currentTile);
                else
                    lastNodeTiles[i] = currentTile;
                RevealTilesAroundPlayer(currentTile);
            }
        }
    }

    private void RevealTilesAroundPlayer(Vector2I centerTile)
    {
        foreach (Vector2I tilePos in GetTilesInRadius(centerTile, RevealRadius))
        {
            if (PermanentReveal)
            {
                if (!revealedTiles.Add(tilePos))
                    continue; // Already revealed earlier
            }

            FogTilemap.SetCellsTerrainConnect([tilePos], 0, -1); // Clear fog tile
        }
    }

    public static IEnumerable<Vector2I> GetTilesInRadius(Vector2I center, int radius)
    {
        int rSquared = radius * radius;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                // circular masking: x² + y² < r²
                if (x * x + y * y <= rSquared)
                    yield return new Vector2I(center.X + x, center.Y + y);
            }
        }
    }

    public void ResetFog()
    {
        revealedTiles.Clear();
        FogTilemap.Clear();
    }
}
