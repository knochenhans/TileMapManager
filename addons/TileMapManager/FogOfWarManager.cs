using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

public partial class FogOfWarManager : Node
{
    [Export] public TileMapLayer FogTilemap;
    [Export] public int RevealRadius = 4;
    [Export] public bool PermanentReveal = true;
    [Export] public Vector2I FogTileAtlasCoord = new(9, 2);
    [Export] public TileMapManager TileMapManager;

    public Array<Node2D> Nodes;
    public Rect2I UsableArea;

    private HashSet<Vector2I> revealedTiles = [];
    private Array<Vector2I> lastNodeTiles = [];

    public override void _Ready()
    {
        UsableArea = TileMapManager.GetUsedRect();

        if (FogTilemap == null)
        {
            GD.PushError("FogOfWarManager: FogTilemap not assigned!");
            return;
        }

        FillUsableArea();
    }

    public void RegisterPlayers(Array<Node2D> playerNodes)
    {
        Nodes = playerNodes;

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

    public void FillUsableArea()
    {
        if (FogTilemap == null)
            return;

        // Fill the usable area with fog
        for (int x = UsableArea.Position.X; x < UsableArea.End.X; x++)
        {
            for (int y = UsableArea.Position.Y; y < UsableArea.End.Y; y++)
            {
                FogTilemap.SetCell(new Vector2I(x, y), 0, FogTileAtlasCoord); // Set fog tile
            }
        }
    }

    public override void _Process(double delta)
    {
        for (int i = 0; i < Nodes.Count; i++)
        {
            var node = Nodes[i];
            if (node == null)
                continue;
            var pos = node.GlobalPosition;
            var currentTile = FogTilemap.LocalToMap(new Vector2I((int)pos.X, (int)pos.Y));
            if (i >= lastNodeTiles.Count || !currentTile.Equals(lastNodeTiles[i]))
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
        FillUsableArea();
    }

    public IEnumerable<Vector2I> SaveFogState()
    {
        return revealedTiles;
    }

    public void LoadFogState(IEnumerable<Vector2I> savedTiles)
    {
        revealedTiles.Clear();
        FillUsableArea();

        foreach (var tile in savedTiles)
        {
            RevealTile(tile);
        }
    }

    public void RevealRect(Rect2 area, int padding = 0)
    {
        RevealRect(TileMapManager.GetTilesInRect(area), padding);
    }

    public void RevealTileAtPosition(Vector2 position)
    {
        var tilePos = FogTilemap.LocalToMap(position);
        RevealTile(tilePos);
    }

    public void RevealTile(Vector2I tilePos)
    {
        FogTilemap.SetCellsTerrainConnect([tilePos], 0, -1);
        revealedTiles.Add(tilePos);
    }

    public void RevealRect(Rect2I tileRect, int padding = 0)
    {
        Array<Vector2I> tiles = [];

        for (int x = tileRect.Position.X - padding; x < tileRect.End.X + padding; x++)
            for (int y = tileRect.Position.Y - padding; y < tileRect.End.Y + padding; y++)
            {
                tiles.Add(new Vector2I(x, y));
                revealedTiles.Add(new Vector2I(x, y));
            }

        FogTilemap.SetCellsTerrainConnect(tiles, 0, -1);
    }
}
