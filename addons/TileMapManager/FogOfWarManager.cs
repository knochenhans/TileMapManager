using Godot;
using Godot.Collections;
using CoreSystems;

using static Logger;
using SaveData = Godot.Collections.Dictionary<string, Godot.Variant>;

public partial class FogOfWarManager : Node
{
    #region [Fields and Properties]
    [Export] public FogOfWarTileMap FogTileMap;
    [Export] public TileMapManager TileMapManager;
    [Export] public Vector2I FogTileAtlasCoord = new(9, 2);

    [ExportCategory("Fill Settings")]
    [Export] public int RevealRadius = 4;
    [Export] public bool PermanentReveal = true;
    [Export] public bool InitiallyFilled = true;

    public Array<StageNode> ActorNodes = [];
    public Rect2I UsableArea;

    Array<Vector2I> revealedTiles = [];
    Dictionary<string, Array<Vector2I>> hiddenAreas = [];
    Array<Vector2I> lastNodeTiles = [];
    int initialPadding = 16;
    #endregion

    #region [Godot]
    public override void _Ready()
    {
        var fogTileSize = FogTileMap.TileSet.TileSize;
        var defaultTileSize = TileMapManager.DefaultTileSize;
        var factor = new Vector2I(defaultTileSize.X / fogTileSize.X, defaultTileSize.Y / fogTileSize.Y);

        var usedRect = TileMapManager.GetUsedRect();
        UsableArea = new Rect2I(usedRect.Position * factor, usedRect.Size * factor);

        if (FogTileMap == null)
        {
            LogError("FogOfWarManager: FogTilemap not assigned!", LogTypeEnum.Framework);
            return;
        }

        if (InitiallyFilled)
            FillUsableArea(initialPadding);

        // foreach (var node in FogTilemap.TileModifierZones)
        // {
        //     if (node is TileModifierZone hiddenZone && hiddenZone.FOWHide)
        //     {
        //         var coveredTiles = FogTilemap.GetCoveredTiles(hiddenZone.GetCollisionNode(), hiddenZone.GlobalPosition);
        //         AddHiddenArea(coveredTiles, hiddenZone.ID);
        //     }
        // }
    }

    public override void _Process(double delta)
    {
        if (FogTileMap == null || ActorNodes == null)
            return;

        for (int i = 0; i < ActorNodes.Count; i++)
        {
            var node = ActorNodes[i];
            if (node == null)
                continue;
            var pos = node.GlobalPosition;
            var currentTile = FogTileMap.LocalToMap(new Vector2I((int)pos.X, (int)pos.Y));
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
    #endregion

    #region [Main Logic]
    public void RegisterActor(StageNode actorNode)
    {
        ActorNodes.Add(actorNode);
        var tilePos = FogTileMap.LocalToMap(actorNode.GlobalPosition);
        lastNodeTiles.Add(tilePos);
        RevealTilesAroundActor(tilePos);
    }

    public void AddHiddenArea(System.Collections.Generic.IEnumerable<Vector2I> tiles, string id)
    {
        foreach (var tile in tiles)
        {
            if (!hiddenAreas.ContainsKey(id))
                hiddenAreas[id] = [];
            hiddenAreas[id].Add(tile);
        }
    }

    public void AddHiddenArea(Rect2I tileRect, string id)
    {
        if (!hiddenAreas.ContainsKey(id))
            hiddenAreas[id] = [];
        for (int x = tileRect.Position.X; x < tileRect.End.X; x++)
        {
            for (int y = tileRect.Position.Y; y < tileRect.End.Y; y++)
            {
                hiddenAreas[id].Add(new Vector2I(x, y));
            }
        }
    }

    public void AddHiddenArea(Node2D collisionNode, Vector2 globalPosition, string id)
    {
        var coveredTiles = FogTileMap.GetCoveredTiles(collisionNode, globalPosition);
        AddHiddenArea(coveredTiles, id);
    }

    public void FillUsableArea(int padding = 0)
    {
        if (FogTileMap == null)
            return;

        var tilesToFill = new Array<Vector2I>();

        for (int x = UsableArea.Position.X - padding; x < UsableArea.End.X + padding; x++)
        {
            for (int y = UsableArea.Position.Y - padding; y < UsableArea.End.Y + padding; y++)
            {
                var tilePos = new Vector2I(x, y);
                tilesToFill.Add(tilePos);
                FogTileMap.SetCell(new Vector2I(x, y), 0, FogTileAtlasCoord);
            }
        }

        //TODO: Using SetCell is faster here for now
        // SetRectRevealedState(UsableArea, padding, false);
    }

    public static System.Collections.Generic.IEnumerable<Vector2I> GetTilesInRadius(Vector2I center, int radius)
    {
        int rSquared = radius * radius;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                // circular masking: x² + y² < r²
                if ((x * x) + (y * y) <= rSquared)
                    yield return new Vector2I(center.X + x, center.Y + y);
            }
        }
    }

    public void ResetFog()
    {
        revealedTiles.Clear();
        FillUsableArea();
    }

    public void RevealHiddenArea(string id)
    {
        if (hiddenAreas.TryGetValue(id, out var tiles))
        {
            var tilesToReveal = new Array<Vector2I>();

            tilesToReveal.AddRange(tiles);
            SetTilesAsRevealed(tilesToReveal);
        }
    }

    public void UnlockHiddenArea(string id) => hiddenAreas.Remove(id);

    public void SetRectAsRevealed(Rect2 area, int padding = 0, bool revealed = true) => SetRectAsRevealed(TileMapManager.GetTilesRectInRect(area, TileMapManager.TileLayerTag.Fog), padding, revealed);

    public void SetRectAsRevealed(Rect2I tileRect, int padding = 0, bool revealed = true)
    {
        if (FogTileMap == null)
            return;

        const int margin = 3;

        GetInnerOuterTiles(tileRect, padding, margin, out Array<Vector2I> innerTiles, out Array<Vector2I> outerTiles);

        foreach (var v in innerTiles)
        {
            FogTileMap.SetCell(v, 0, FogTileAtlasCoord);
            SetTileRevealedState(v, revealed);
        }

        if (outerTiles.Count > 0)
            SetTilesAsRevealed(outerTiles, revealed);
    }

    public void RevealAll() => SetRectAsRevealed(UsableArea);

    public void SetTilesAsRevealed(Array<Vector2I> tiles, bool revealed = true)
    {
        FogTileMap.SetCellsTerrainConnect(tiles, 0, revealed ? -1 : 0);

        foreach (var tilePos in tiles)
            SetTileRevealedState(tilePos, revealed);
    }

    public void SetTileAsRevealed(Vector2I tile, bool revealed = true)
    {
        FogTileMap.SetCellsTerrainConnect([tile], 0, revealed ? -1 : 0);

        SetTileRevealedState(tile, revealed);
    }

    #endregion

    #region [Utility]
    private bool IsTileHidden(Vector2I tile)
    {
        foreach (var tiles in hiddenAreas.Values)
        {
            if (tiles.Contains(tile))
                return true;
        }
        return false;
    }

    private void SetTileRevealedState(Vector2I tile, bool revealed = true)
    {
        if (revealed && !revealedTiles.Contains(tile))
            revealedTiles.Add(tile);
        else if (!revealed)
            revealedTiles.Remove(tile);
    }

    private Vector2I FogToWallTile(Vector2I fogTile)
    {
        Vector2 worldPos = FogTileMap.ToGlobal(FogTileMap.MapToLocal(fogTile));
        return TileMapManager.TileMapLayerWalls.LocalToMap(worldPos);
    }

    private void RevealTilesAroundActor(Vector2I centerTile)
    {
        var visibleTiles = ComputeFOV(centerTile, RevealRadius);

        var tilesToReveal = new Array<Vector2I>();

        foreach (var tilePos in visibleTiles)
        {
            if (IsTileHidden(tilePos))
                continue;

            if (PermanentReveal && revealedTiles.Contains(tilePos))
                continue;

            tilesToReveal.Add(tilePos);

            if (PermanentReveal)
                revealedTiles.Add(tilePos);
        }

        SetTilesAsRevealed(tilesToReveal);
    }

    private System.Collections.Generic.HashSet<Vector2I> ComputeFOV(Vector2I origin, int radius)
    {
        var visible = new System.Collections.Generic.HashSet<Vector2I>
    {
        origin
    };

        for (int octant = 0; octant < 8; octant++)
        {
            CastLight(origin, 1, 1.0f, 0.0f, radius, GetTransformForOctant(octant), visible);
        }

        return visible;
    }

    private void CastLight(Vector2I origin, int row, float startSlope, float endSlope, int radius, (int xx, int xy, int yx, int yy) transform, System.Collections.Generic.HashSet<Vector2I> visible)
    {
        if (startSlope < endSlope)
            return;

        float radiusSquared = radius * radius;

        for (int i = row; i <= radius; i++)
        {
            bool blocked = false;
            float newStartSlope = startSlope;

            for (int dx = -i; dx <= 0; dx++)
            {
                int dy = -i;

                float lSlope = (dx - 0.5f) / (dy + 0.5f);
                float rSlope = (dx + 0.5f) / (dy - 0.5f);

                if (rSlope > startSlope)
                    continue;

                if (lSlope < endSlope)
                    break;

                int mapX = origin.X + (dx * transform.xx) + (dy * transform.xy);
                int mapY = origin.Y + (dx * transform.yx) + (dy * transform.yy);

                var pos = new Vector2I(mapX, mapY);

                float distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared <= radiusSquared)
                    visible.Add(pos);

                if (blocked)
                {
                    if (IsBlockingVision(pos))
                    {
                        newStartSlope = rSlope;
                        continue;
                    }
                    else
                    {
                        blocked = false;
                        startSlope = newStartSlope;
                    }
                }
                else
                {
                    if (IsBlockingVision(pos) && i < radius)
                    {
                        blocked = true;

                        CastLight(origin, i + 1, startSlope, lSlope, radius, transform, visible);

                        newStartSlope = rSlope;
                    }
                }
            }

            if (blocked)
                break;
        }
    }

    private (int xx, int xy, int yx, int yy) GetTransformForOctant(int octant)
    {
        return octant switch
        {
            0 => (1, 0, 0, 1),
            1 => (0, 1, 1, 0),
            2 => (0, -1, 1, 0),
            3 => (-1, 0, 0, 1),
            4 => (-1, 0, 0, -1),
            5 => (0, -1, -1, 0),
            6 => (0, 1, -1, 0),
            7 => (1, 0, 0, -1),
            _ => (1, 0, 0, 1)
        };
    }

    private bool IsBlockingVision(Vector2I fogTile)
    {
        if (!UsableArea.HasPoint(fogTile))
            return true;

        var wallTile = FogToWallTile(fogTile);

        int sourceId = TileMapManager.TileMapLayerWalls.GetCellSourceId(wallTile);
        if (sourceId < 0)
            return false;

        var tileData = TileMapManager.TileMapLayerWalls.GetCellTileData(wallTile);
        if (tileData == null)
            return false;

        // if (tileData.HasCustomData("blocks_vision"))
        //     return tileData.GetCustomData("blocks_vision").AsBool();

        return true;
    }

    private void GetInnerOuterTiles(Rect2I tileRect, int padding, int margin, out Array<Vector2I> innerTiles, out Array<Vector2I> outerTiles)
    {
        innerTiles = [];
        outerTiles = [];

        var expandedPos = tileRect.Position - new Vector2I(padding, padding);
        var expandedSize = tileRect.Size + new Vector2I(padding * 2, padding * 2);
        var expandedRect = new Rect2I(expandedPos, expandedSize);

        var innerPos = expandedRect.Position + new Vector2I(margin, margin);
        var innerSize = expandedRect.Size - new Vector2I(margin * 2, margin * 2);
        bool hasInner = innerSize.X > 0 && innerSize.Y > 0;
        var innerRect = hasInner ? new Rect2I(innerPos, innerSize) : new Rect2I();

        for (int x = expandedRect.Position.X; x < expandedRect.End.X; x++)
        {
            for (int y = expandedRect.Position.Y; y < expandedRect.End.Y; y++)
            {
                var v = new Vector2I(x, y);

                if (hasInner &&
                    x >= innerRect.Position.X && x < innerRect.End.X &&
                    y >= innerRect.Position.Y && y < innerRect.End.Y)
                {
                    innerTiles.Add(v);
                }
                else
                {
                    outerTiles.Add(v);
                }
            }
        }
    }
    #endregion

    #region [Saving and Loading]
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
        arr.AddRange(SaveFogState());

        return new SaveData
        {
            ["RevealedTiles"] = (Variant)arr
        };
    }

    public System.Collections.Generic.IEnumerable<Vector2I> SaveFogState() => revealedTiles;

    public void LoadFogState(System.Collections.Generic.IEnumerable<Vector2I> savedTiles)
    {
        ResetFog();

        foreach (var tile in savedTiles)
            SetTileAsRevealed(tile);
    }
    #endregion
}
