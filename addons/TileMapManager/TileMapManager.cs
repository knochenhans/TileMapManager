using Godot;
using Godot.Collections;
using System;
using System.Linq;

public partial class TileMapManager : Node2D
{
    [Export] public TileMapLayer TileMapLayerGround;
    [Export] public TileMapLayer TileMapLayerRooms;
    [Export] public TileMapLayer TileMapLayerUsableArea;
    [Export] public TileMapLayer TileMapLayerFogOfWar;

    [Export] public TimedAudioStreamPlayer2D HitSoundPlayer;
    [Export] public TimedAudioStreamPlayer2D ExplosionSoundPlayer;
    
    [Export] public string TileMapDataLayerName = "material";
    [Export] public NavigationRegion2D NavigationRegion;

    [Signal] public delegate void TileHitEventHandler(ExplosionResource explosionResource, Vector2 position);

    Array<TileMapLayer> TileMapLayers = [];
    Vector2I tileSize;

    public ExplosionDatabase ExplosionDatabase;

    private Array<Rect2I> Rooms = [];

    public override void _Ready()
    {
        if (TileMapLayerGround == null)
        {
            Logger.LogError($"TileMapLayerGround is not set for stage {GetOwner<Stage>().ID}. Please assign a TileMapLayerGround in the inspector.", "TileMapManager", Logger.LogTypeEnum.World);
            return;
        }

        var layers = GetChildren().Cast<CustomTileMapLayer>().ToArray();

        Array<TileMapLayer> topTileMapLayers = [];

        foreach (var layer in layers)
            if (!layer.GroundLayer)
            {
                topTileMapLayers.Add(layer);
                if (layer is CustomTileMapLayer customLayer)
                {
                    customLayer.TileHit += OnTileHit;
                    customLayer.TileDestroyed += OnTileDestroyed;
                }
            }

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
                tileSize = tileMapLayer.TileSet.TileSize;

                if (!tileMapLayer.TileSet.HasCustomDataLayerByName(TileMapDataLayerName))
                {
                    Logger.Log($"TileMapLayer {tileMapLayer.Name} does not have a custom data layer named '{TileMapDataLayerName}'. Please add it to the TileMapLayer.", "TileMapManager", Logger.LogTypeEnum.World, Logger.LogLevelEnum.Error);
                    return;
                }
            }
        }
    }

    public Rect2I GetTilesInRect(Rect2 rect)
    {
        var min = rect.Position / tileSize;
        var max = (rect.Position + rect.Size) / tileSize;

        Vector2I topLeft = new((int)Math.Floor(min.X), (int)Math.Floor(min.Y));
        Vector2I bottomRight = new((int)Math.Ceiling(max.X), (int)Math.Ceiling(max.Y));

        return new Rect2I(topLeft, bottomRight - topLeft);
    }

    public Vector2I GetTileAtPosition(Vector2 position)
    {
        return new Vector2I((int)Math.Floor(position.X / tileSize.X), (int)Math.Floor(position.Y / tileSize.Y));
    }

    public TileData GetTileDataAtPosition(Vector2 position, TileMapLayer layer)
    {
        var tilePosition = GetTileAtPosition(position);
        return layer.GetCellTileData(tilePosition);
    }

    public void OnTileHit(Vector2 position, int damage, string material)
    {
        var tileHitExplosionResource = ExplosionDatabase.GetExplosionResource(material);
        if (tileHitExplosionResource != null)
            EmitSignal(SignalName.TileHit, tileHitExplosionResource, position);
    }

    public void OnTileDestroyed(Vector2 position, string material)
    {
        var tileDestroyedExplosionResource = ExplosionDatabase.GetExplosionResource(material);
        if (tileDestroyedExplosionResource != null)
            EmitSignal(SignalName.TileHit, tileDestroyedExplosionResource, position);

        if (NavigationRegion != null)
            NavigationRegion.BakeNavigationPolygon();
        else
            Logger.LogWarning("NavigationRegion is not set. Navigation mesh will not be updated on tile destruction.", "TileMapManager", Logger.LogTypeEnum.World);
    }

    public void FindRooms(Array<Vector2I> doorCells)
    {
        if (TileMapLayerRooms == null)
        {
            Logger.LogWarning("TileMapLayerRooms is not set. Cannot find rooms.", "TileMapManager", Logger.LogTypeEnum.World);
            return;
        }

        RoomFinder roomFinder = new(TileMapLayerRooms, doorCells);
        Rooms = roomFinder.FindRooms();
    }

    public Vector2I WorldToMap(Vector2 globalPosition)
    {
        return TileMapLayerGround.LocalToMap(globalPosition);
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

        if (tileData == null)
            return false;
    
        return true;
    }
}
