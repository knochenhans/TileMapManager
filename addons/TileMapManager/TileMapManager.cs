using Godot;
using Godot.Collections;

using System;
using System.Linq;
using System.Threading.Tasks;

public partial class TileMapManager : Node2D
{
    #region [Fields and Properties]
    [Flags]
    public enum TileLayerTag
    {
        None = 0,
        Ground = 1 << 0,
        Wall = 1 << 1,
        Blocking = 1 << 2,
        Object = 1 << 3,
        Fog = 1 << 4,
    }

    Dictionary<TileLayerTag, Array<CustomTileMapLayer>> LayersByTag = [];

    [Export] public Array<CustomTileMapLayer> TileMapLayerGround = [];
    [Export] public CustomTileMapLayer TileMapLayerUsableArea;
    [Export] public CustomTileMapLayer TileMapLayerFogOfWar;
    [Export] public CustomTileMapLayer TileMapLayerObjects;
    [Export] public CustomTileMapLayer TileMapLayerPaths;
    [Export] public CustomTileMapLayer TileMapLayerWalls;
    [Export] public string MaterialTileDataLayerName = "material";
    [Export] public NavigationRegion2D NavigationRegion;

    [Signal] public delegate void TileHitEventHandler(ExplosionResource explosionResource, Node2D source, Vector2 position);
    [Signal] public delegate void TileDestroyedEventHandler(ExplosionResource explosionResource, Node2D source, Vector2 position, float strength);

    Array<TileMapLayer> TileMapLayers = [];

    public ExplosionDatabase HitExplosionDatabase;
    public ExplosionDatabase GroundHitExplosionDatabase;
    public ExplosionDatabase DestructionExplosionDatabase;

    MapGenerator MapGenerator = new();
    Array<Rect2I> Rooms = [];

    Vector2I DefaultTileSize = new(32, 32);
    #endregion

    #region [Godot]
    public override void _Ready()
    {
        if (TileMapLayerGround == null || TileMapLayerGround.Count == 0)
        {
            Logger.LogError($"TileMapLayerGround is not set for stage {GetOwner<Stage>().ID}. Please assign at least one TileMapLayerGround in the inspector.", "TileMapManager", Logger.LogTypeEnum.World);
            return;
        }

        CustomTileMapLayer[] layers;
        try
        {
            layers = [.. GetChildren().Cast<CustomTileMapLayer>()];
        }
        catch (InvalidCastException)
        {
            Logger.LogWarning("One or more children of TileMapManager could not be cast to CustomTileMapLayer; filtering valid children instead.", "TileMapManager", Logger.LogTypeEnum.World);
            layers = [.. GetChildren().OfType<CustomTileMapLayer>()];
        }

        Array<TileMapLayer> topTileMapLayers = [];

        foreach (var layer in layers)
            layer.TopTileMapLayers = topTileMapLayers;

        foreach (var layer in GetChildren())
        {
            if (layer is TileMapLayer tileMapLayer)
            {
                TileMapLayers.Add(tileMapLayer);

                if (tileMapLayer.TileSet == null)
                {
                    Logger.LogError($"TileMapLayer {tileMapLayer.Name} has no TileSet assigned.", "TileMapManager", Logger.LogTypeEnum.World);
                    continue;
                }

                if (!tileMapLayer.TileSet.HasCustomDataLayerByName(MaterialTileDataLayerName))
                {
                    Logger.Log($"TileMapLayer {tileMapLayer.Name} does not have a custom data layer named '{MaterialTileDataLayerName}'. Please add it to the TileMapLayer.", "TileMapManager", Logger.LogTypeEnum.World, Logger.LogLevelEnum.Error);
                }
            }
        }

        foreach (var layer in layers)
        {
            foreach (TileLayerTag tag in Enum.GetValues(typeof(TileLayerTag)))
            {
                if (tag == TileLayerTag.None)
                    continue;

                if (layer.Tags.HasFlag(tag))
                {
                    if (!LayersByTag.ContainsKey(tag))
                        LayersByTag[tag] = [];

                    LayersByTag[tag].Add(layer);
                }
            }
        }
    }
    #endregion

    #region [Lifecycle]
    public async Task InitMapGenerator(NoiseTexture2D noiseTexture)
    {
        MapGenerator = new MapGenerator
        {
            TileSetNoiseTexture = noiseTexture
        };
        await MapGenerator.Load();
    }

    public void GenerateGround(int innerRadius = 0)
    {
        Logger.Log("Generating ground...", Logger.LogTypeEnum.Framework);

        //     MapGenerator.SetRandomNoiseAreaAroundPosition(PlayerEntity.Position, generationAreaSize, new Vector2I(0, 0), 4, innerRadius);
        //     MapGenerator.SetRandomAreaAroundPosition(PlayerEntity.Position, generationAreaSize, new Vector2I(0, 1), 3, innerRadius);

        var noiseTextureWidth = MapGenerator.TileSetNoiseTexture.GetImage().GetWidth();
        var noiseTextureHeight = MapGenerator.TileSetNoiseTexture.GetImage().GetHeight();

        var start = new Vector2I(-(noiseTextureWidth / 2), -(noiseTextureHeight / 2));
        var size = new Vector2I(noiseTextureWidth, noiseTextureHeight);

        start = new Vector2I(0, 0);
        size = new Vector2I(200, 200);

        GD.Print($"Start: {start}, Size: {size}");

        var landscapes = new[]
        {
            new LandscapeDefinition("Water1", TileMapLayerGround[0], new Vector2I(0, 2), 9, 0.0f, 0.3f),
            new LandscapeDefinition("Water2", TileMapLayerGround[1], new Vector2I(0, 0), 5, 0.3f),
            new LandscapeDefinition("Water3", TileMapLayerGround[2], new Vector2I(0, 1), 6, 0.6f, 1.0f),
            // // new LandscapeDefinition("Rock", new Vector2I(0, 3), 6, 0.8f, 1.0f)
            // new LandscapeDefinition("Random", TileMapAboveGround, new Vector2I(0, 4), 6, 0.4f, 0.8f, Random: true)
        };

        MapGenerator.SetRandomArea(
            landscapes,
            start,
            size
        );

        // TownSpots = MapGenerator.FindNoiseSpotsInRange(10, 0.3f, 0.7f, 3);
        // GenerateTowns();

        GD.Print("Map generation complete.");
    }
    #endregion

    #region [Public]
    private void HandleImpactResult(TileImpactResult result, Node2D source, Vector2 position, string material)
    {
        if (result.WasHit)
        {
            var explosionResource = HitExplosionDatabase.GetExplosionResource(material);
            if (explosionResource != null)
                EmitSignal(SignalName.TileHit, explosionResource, source, position);
        }

        if (result.WasDestroyed)
        {
            var explosionResource = DestructionExplosionDatabase.GetExplosionResource(material);
            if (explosionResource != null)
            {
                EmitSignal(SignalName.TileHit, explosionResource, source, position);
                EmitSignal(SignalName.TileDestroyed, explosionResource, source, position, result.Strength);
            }

            NavigationRegion?.BakeNavigationPolygon();
        }
    }

    public void ApplyTileImpact(TileImpact impact)
    {
        var cell = WorldToMap(impact.WorldPosition);

        var layer = GetTopLayerByTag(cell, impact.TargetTag);
        if (layer == null)
            return;

        var tileData = layer.GetCellTileData(cell);
        if (tileData == null)
            return;

        string material = impact.ForcedMaterial;

        if (string.IsNullOrEmpty(material))
            material = tileData.GetCustomData(MaterialTileDataLayerName).AsString();

        var result = (layer as CustomTileMapLayer).ApplyDamage(cell, impact.Damage);

        HandleImpactResult(result, impact.Source, impact.WorldPosition, material);
    }

    public Vector2I WorldToMap(Vector2 globalPosition)
    {
        if (TileMapLayerGround == null || TileMapLayerGround.Count == 0)
        {
            Logger.LogWarning("TileMapLayerGround is not set. WorldToMap will return Vector2I.Zero.", "TileMapManager", Logger.LogTypeEnum.World);
            return Vector2I.Zero;
        }

        // Use the first ground layer as the reference for mapping world -> map coordinates
        return TileMapLayerGround[0].LocalToMap(globalPosition);
    }

    public Vector2 MapToWorld(Vector2I tilePosition)
    {
        if (TileMapLayerGround == null || TileMapLayerGround.Count == 0)
        {
            Logger.LogWarning("TileMapLayerGround is not set. MapToWorld will return Vector2.Zero.", "TileMapManager", Logger.LogTypeEnum.World);
            return Vector2.Zero;
        }

        // Use the first ground layer as the reference for mapping map -> world coordinates
        return TileMapLayerGround[0].MapToLocal(tilePosition) + (GetTileSize(TileMapLayerGround[0]) / 2);
    }

    public Rect2I GetUsedRect()
    {
        if (TileMapLayerUsableArea == null)
        {
            Logger.LogWarning("TileMapLayerUsableArea is not set. Cannot get used rect.", "TileMapManager", Logger.LogTypeEnum.World);
            return new Rect2I();
        }
        return TileMapLayerUsableArea.GetUsedRect();
    }

    public bool IsTileUnderFogOfWar(Vector2I tilePosition)
    {
        if (TileMapLayerFogOfWar == null)
            return true;

        var tileData = TileMapLayerFogOfWar.GetCellTileData(tilePosition);

        return tileData != null;
    }

    public Rect2I GetTilesInRect(Rect2 rect, TileLayerTag tag)
    {
        if (!LayersByTag.TryGetValue(tag, out var layers))
        {
            Logger.LogWarning($"No layers found with tag {tag}. Cannot get tiles in rect.", "TileMapManager", Logger.LogTypeEnum.World);
            return new Rect2I();
        }
        var layer = layers[0];
        var tileSize = GetTileSize(layer);
        var min = rect.Position / tileSize;
        var max = (rect.Position + rect.Size) / tileSize;

        Vector2I topLeft = new((int)Math.Floor(min.X), (int)Math.Floor(min.Y));
        Vector2I bottomRight = new((int)Math.Ceiling(max.X), (int)Math.Ceiling(max.Y));

        return new Rect2I(topLeft, bottomRight - topLeft);
    }

    public Vector2I GetTileAtPosition(Vector2 position, TileMapLayer layer = null)
    {
        layer ??= TileMapLayerGround[0];

        var tileSize = GetTileSize(layer);
        return new Vector2I((int)Math.Floor(position.X / tileSize.X), (int)Math.Floor(position.Y / tileSize.Y));
    }

    public TileData GetTileDataAtPosition(Vector2 position, TileMapLayer layer)
    {
        var tilePosition = GetTileAtPosition(position, layer);
        return layer.GetCellTileData(tilePosition);
    }

    public Array<Vector2I> GetTilesInRadius(Vector2I center, int radius, TileMapLayer layer)
    {
        Array<Vector2I> tiles = [];
        for (int x = center.X - radius; x <= center.X + radius; x++)
        {
            for (int y = center.Y - radius; y <= center.Y + radius; y++)
            {
                Vector2I tilePos = new(x, y);
                if (tilePos.DistanceTo(center) <= radius && layer.GetCellTileData(tilePos) != null)
                {
                    tiles.Add(tilePos);
                }
            }
        }
        return tiles;
    }

    public bool IsTilePositionEmpty(Vector2I tilePosition, TileMapLayer layer)
    {
        return layer.GetCellTileData(tilePosition) == null;
    }

    public Vector2I GetRandomEmptyTilePosition(Vector2I? center = null, int radius = -1)
    {
        if (TileMapLayerGround == null)
        {
            Logger.LogWarning("TileMapLayerGround is not set. Cannot get random empty tile position.", "TileMapManager", Logger.LogTypeEnum.World);
            return Vector2I.Zero;
        }

        if (TileMapLayerWalls == null)
        {
            Logger.LogWarning("TileMapLayerWalls is not set. Cannot check for empty tile positions properly.", "TileMapManager", Logger.LogTypeEnum.World);
            return Vector2I.Zero;
        }

        var usedRect = TileMapLayerUsableArea.GetUsedRect();

        Vector2I randomPosition;
        int attempts = 0;

        while (true)
        {
            int x = GD.RandRange(usedRect.Position.X, usedRect.Position.X + usedRect.Size.X - 1);
            int y = GD.RandRange(usedRect.Position.Y, usedRect.Position.Y + usedRect.Size.Y - 1);
            randomPosition = new Vector2I(x, y);

            if (center != null && radius > 0)
            {
                if (randomPosition.DistanceTo(center.Value) > radius)
                    continue;
            }

            if (IsTilePositionEmpty(randomPosition, TileMapLayerWalls))
                break;

            attempts++;
            if (attempts > 100)
            {
                Logger.LogWarning("GetRandomEmptyTilePosition: Too many attempts to find an empty tile. Returning Vector2I.Zero.", "TileMapManager", Logger.LogTypeEnum.World);
                return Vector2I.Zero;
            }
        }

        return randomPosition;
    }

    public TileData GetTopmostTileData(Vector2 position, TileLayerTag tag)
    {
        return GetTopmostTileData(GetTileAtPosition(position), tag);
    }

    public TileData GetTopmostTileData(Vector2I cell, TileLayerTag tag = TileLayerTag.None)
    {
        if (tag == TileLayerTag.None)
            return GetTopmostTileData(cell);

        if (!LayersByTag.TryGetValue(tag, out var layers))
            return null;

        // assume order = top to bottom
        foreach (var layer in layers)
        {
            var tile = layer.GetCellTileData(cell);
            if (tile != null)
                return tile;
        }

        return null;
    }

    public TileData GetTopmostTileData(Vector2I cell)
    {
        // Return the first non-null tile data from all layers, starting from the topmost layer
        foreach (var tileMapLayer in TileMapLayers)
        {
            TileData tileData = tileMapLayer.GetCellTileData(cell);
            if (tileData != null)
                return tileData;
        }
        return null;
    }

    public TileData GetTopmostCellGroundData(Vector2I cell)
    {
        // Return the first non-null tile data from the tile map ground layers
        var reversedLayers = TileMapLayerGround.Duplicate();
        reversedLayers.Reverse();
        foreach (var tileMapLayer in reversedLayers)
        {
            TileData tileData = tileMapLayer.GetCellTileData(cell);
            if (tileData != null)
                return tileData;
        }
        return null;
    }

    public TileMapLayer GetTopLayerByTag(Vector2I cell, TileLayerTag tag)
    {
        if (!LayersByTag.TryGetValue(tag, out var layers))
            return null;

        // assume order = top to bottom
        foreach (var layer in layers)
        {
            var tile = layer.GetCellTileData(cell);
            if (tile != null)
                return layer;
        }

        return null;
    }

    public Vector2I FindRandomNonEmptyTile(Vector2 position, TileMapLayer layer, int radius = 5)
    {
        var tiles = GetTilesInRadius(GetTileAtPosition(position), radius, layer);
        return tiles.OrderBy(_ => GD.Randf()).FirstOrDefault();
    }

    public Vector2I GetTileSize(TileMapLayer layer)
    {
        if (layer.TileSet == null)
        {
            Logger.LogWarning($"TileMapLayer {layer.Name} has no TileSet assigned. Cannot get tile size.", "TileMapManager", Logger.LogTypeEnum.World);
            return Vector2I.Zero;
        }
        return layer.TileSet.TileSize;
    }
    #endregion
}
