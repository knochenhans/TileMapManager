using Godot;
using Godot.Collections;
using Game.Utils;

using static Logger;
using SaveData = Godot.Collections.Dictionary<string, Godot.Variant>;

public partial class FogOfWarManager : Node
{
    [Export] public TileMapLayer FogTilemap;
    [Export] public int RevealRadius = 4;
    [Export] public bool PermanentReveal = true;
    [Export] public Vector2I FogTileAtlasCoord = new(9, 2);
    [Export] public TileMapManager TileMapManager;

    public Array<StageNode> Nodes;
    public Rect2I UsableArea;

    Array<Vector2I> revealedTiles = [];
    Array<Vector2I> exceptions = [];
    Array<Vector2I> lastNodeTiles = [];

    public override void _Ready()
    {
        UsableArea = TileMapManager.GetUsedRect();

        if (FogTilemap == null)
        {
            LogError("FogOfWarManager: FogTilemap not assigned!", LogTypeEnum.Framework);
            return;
        }

        FillUsableArea();
    }

    public void RegisterActors(Array<StageNode> actorNodes)
    {
        Nodes = actorNodes;

        for (int i = 0; i < Nodes.Count; i++)
        {
            var node = Nodes[i];
            if (node == null)
            {
                LogError($"FogOfWarManager: Node at index {i} is null!", LogTypeEnum.Framework);
                continue;
            }
            var tilePos = FogTilemap.LocalToMap(node.GlobalPosition);
            if (i >= lastNodeTiles.Count)
                lastNodeTiles.Add(tilePos);
            else
                lastNodeTiles[i] = tilePos;
            RevealTilesAroundActor(tilePos);
        }
    }

    public void AddFogOfWarExceptions(Array<Vector2I> exceptionTiles)
    {
        foreach (var tile in exceptionTiles)
            exceptions.Add(tile);
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
        if (FogTilemap == null || Nodes == null)
            return;

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
                RevealTilesAroundActor(currentTile);
            }
        }
    }

    private void RevealTilesAroundActor(Vector2I centerTile)
    {
        foreach (Vector2I tilePos in GetTilesInRadius(centerTile, RevealRadius))
        {
            if (PermanentReveal)
            {
                if (revealedTiles.Contains(tilePos))
                    continue;
                revealedTiles.Add(tilePos);
            }

            if (!exceptions.Contains(tilePos))
                FogTilemap.SetCellsTerrainConnect([tilePos], 0, -1); // Clear fog tile
        }
    }

    public static System.Collections.Generic.IEnumerable<Vector2I> GetTilesInRadius(Vector2I center, int radius)
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

    public System.Collections.Generic.IEnumerable<Vector2I> SaveFogState()
    {
        return revealedTiles;
    }

    public void LoadFogState(System.Collections.Generic.IEnumerable<Vector2I> savedTiles)
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
        if (!revealedTiles.Contains(tilePos))
            revealedTiles.Add(tilePos);
    }

    public void RevealRect(Rect2I tileRect, int padding = 0)
    {
        Array<Vector2I> tiles = [];

        for (int x = tileRect.Position.X - padding; x < tileRect.End.X + padding; x++)
            for (int y = tileRect.Position.Y - padding; y < tileRect.End.Y + padding; y++)
            {
                var v = new Vector2I(x, y);
                tiles.Add(v);
                if (!revealedTiles.Contains(v))
                    revealedTiles.Add(v);
            }

        FogTilemap.SetCellsTerrainConnect(tiles, 0, -1);
    }

    public void Load(SaveData data)
    {
        if (data.TryGetValue("RevealedTiles", out Variant revealedTilesObj))
        {
            var savedTilesStr = (Array<string>)revealedTilesObj;
            var savedTiles = GameUtils.ParseVector2IArrayString(savedTilesStr.ToString());
            LoadFogState(savedTiles);
        }
    }

    public SaveData Save()
    {
        var arr = new Array<Vector2I>();
        foreach (var t in SaveFogState())
            arr.Add(t);

        var data = new SaveData
        {
            ["RevealedTiles"] = (Variant)arr
        };
        return data;
    }
}
