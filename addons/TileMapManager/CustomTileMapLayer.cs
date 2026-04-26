using System.Linq;
using Godot;
using Godot.Collections;

public partial class CustomTileMapLayer : TileMapLayer
{
    #region [Fields and Properties]
    [Export] public TileMapManager.TileLayerTag Tags;

    [ExportGroup("Tile Data Layers")]
    [Export] public bool UseTileDataLayers = false;
    [Export] public string DestructibleTileDataLayerName = "destructible";
    [Export] public string MaterialTileDataLayerName = "material";
    [Export] public string HealthTileDataLayerName = "health";
    [Export] public string MainTileDataLayerName = "main_tile";
    [Export] public string TileSizeTileDataLayerName = "tile_size";
    [Export] public string DestructionStrengthTileDataLayerName = "destruction_strength";
    [Export] public string DestructionModeTileDataLayerName = "destruction_mode";
    [Export] public string DamageStagesDataLayerName = "damage_stages";

    [Export] public NodePath AreasNodePath;

    [Signal] public delegate void TileHitEventHandler(Vector2 position, int damage, string material);
    [Signal] public delegate void TileDestroyedEventHandler(Vector2 position, string material, float strength);

    Array<Vector2I> DestroyedTiles = [];

    Dictionary<Vector2I, TileState> TileStates = [];
    public Array<TileModifierZone> TileModifierZones = [];

    public Array<TileMapLayer> TopTileMapLayers;

    public enum TileDestructionMode
    {
        None = 0,        // indestructible
        DamageStages = 1,   // atlas offset
        RemoveTile = 2      // actual deletion (autotile / terrain)
    }

    public enum TileDamageStageMode
    {
        None = 0,
        AtlasStages = 1,   // atlas offset based on health percentage
    }
    #endregion

    #region [Godot]
    public override void _Ready()
    {
        base._Ready();

        if (TileSet == null)
        {
            Logger.LogError($"TileSet is not assigned for TileMapLayer '{Name}'.", "CustomTileMapLayer", Logger.LogTypeEnum.World);
            return;
        }

        //Check if DestructibleTileDataLayerName, MaterialTileDataLayerName and HealthTileDataLayerName exist in TileSet
        if (!TileSet.HasCustomDataLayerByName(DestructibleTileDataLayerName))
            Logger.LogError($"Custom Data Layer '{DestructibleTileDataLayerName}' does not exist in TileSet for TileMapLayer '{Name}'.", "CustomTileMapLayer", Logger.LogTypeEnum.World);

        if (!TileSet.HasCustomDataLayerByName(MaterialTileDataLayerName))
            Logger.LogError($"Custom Data Layer '{MaterialTileDataLayerName}' does not exist in TileSet for TileMapLayer '{Name}'.", "CustomTileMapLayer", Logger.LogTypeEnum.World);

        if (!TileSet.HasCustomDataLayerByName(HealthTileDataLayerName))
            Logger.LogError($"Custom Data Layer '{HealthTileDataLayerName}' does not exist in TileSet for TileMapLayer '{Name}'.", "CustomTileMapLayer", Logger.LogTypeEnum.World);

        if (!TileSet.HasCustomDataLayerByName(MainTileDataLayerName))
            Logger.LogError($"Custom Data Layer '{MainTileDataLayerName}' does not exist in TileSet for TileMapLayer '{Name}'.", "CustomTileMapLayer", Logger.LogTypeEnum.World);

        if (!TileSet.HasCustomDataLayerByName(TileSizeTileDataLayerName))
            Logger.LogError($"Custom Data Layer '{TileSizeTileDataLayerName}' does not exist in TileSet for TileMapLayer '{Name}'.", "CustomTileMapLayer", Logger.LogTypeEnum.World);

        if (UseTileDataLayers)
        {
            CacheZones();
            InitializeTileStates();
        }
    }
    #endregion

    //TODO: Use physics to detect tile hits, get RID to handle big tiles better (atlas)

    #region [Public Methods]
    public TileImpactResult ApplyDamage(Vector2 position, int damage)
    {
        Vector2I tilePosition = LocalToMap(position);
        var result = ApplyDamage(tilePosition, damage);
        if (result.WasHit)
            EmitSignal(SignalName.TileHit, position, damage, result.Material);
        if (result.WasDestroyed)
            EmitSignal(SignalName.TileDestroyed, position, result.Material, result.Strength);
        return result;
    }

    public TileImpactResult ApplyDamage(Vector2I tilePosition, int damage)
    {
        var result = new TileImpactResult();

        if (TileStates.TryGetValue(tilePosition, out var state))
        {
            if (!state.IsDestructible)
                return result;

            var health = state.Health - damage;

            if (health < 0)
            {
                // TileStates.Remove(tilePosition);
                RemoveTile(state.RootPosition, state.Size);

                result.WasDestroyed = true;
                result.Strength = state.Strength;
            }
            else
            {
                if (ApplyVisualStageFromState(tilePosition, state))
                {
                    RemoveTile(state.RootPosition, state.Size);

                    result.WasDestroyed = true;
                    result.Strength = state.Strength;

                    return result;
                }
            }

            state.Health = health;

            result.WasHit = true;
            result.Material = state.Material;
        }

        return result;
    }

    private TileState FindNearbyState(Vector2I pos)
    {
        const int searchRadius = 2;

        foreach (var kv in TileStates)
        {
            var existingPos = kv.Key;

            if (existingPos.DistanceTo(pos) <= searchRadius)
                return kv.Value;
        }

        return null;
    }

    private bool ApplyVisualStageFromState(Vector2I rootPosition, TileState state)
    {
        int health = Mathf.Max(state.Health, 0);
        float t = 1f - ((float)health / state.MaxHealth);

        if (state.DamageStages == null || state.DamageStages.Length == 0)
            return false;

        int totalSlots = state.DamageStages.Length + 1;
        int stageIndex = Mathf.Clamp((int)(t * totalSlots), 0, totalSlots - 1);

        // stage 0 = original
        if (stageIndex == 0)
            return false;

        int targetSourceID = state.DamageStages[stageIndex - 1];

        if (targetSourceID < 0 || targetSourceID == state.CurrentSourceId)
            return true;

        ApplyVisualStage(rootPosition, targetSourceID, state.AtlasCoord, state.Size);

        state.CurrentSourceId = targetSourceID;

        return false;
    }

    private void ApplyVisualStage(Vector2I rootPosition, int sourceID, Vector2I atlasCoord, Vector2I tileSize)
    {
        for (int x = 0; x < tileSize.X; x++)
        {
            for (int y = 0; y < tileSize.Y; y++)
            {
                var offset = new Vector2I(x, y);

                var currentSourceID = GetCellSourceId(rootPosition + offset);

                SetCell(rootPosition + offset, sourceID, atlasCoord + offset);
                GD.Print($"Set cell at {rootPosition + offset} from source {currentSourceID} to source {sourceID} with atlas coord {atlasCoord + offset}");
            }
        }
    }

    private void RemoveTile(Vector2I rootPosition, Vector2I tileSize)
    {
        PrepareTileStatesAround([rootPosition]);

        var affected = new Array<Vector2I>();

        for (int x = 0; x < tileSize.X; x++)
        {
            for (int y = 0; y < tileSize.Y; y++)
            {
                var pos = rootPosition + new Vector2I(x, y);

                affected.Add(pos);

                SetCellsTerrainConnect([pos], 0, -1);
            }
        }

        InvalidateTileStatesAround(affected);
    }

    private void PrepareTileStatesAround(Array<Vector2I> changedTiles)
    {
        foreach (var tile in changedTiles)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    var pos = tile + new Vector2I(x, y);

                    var tileData = GetCellTileData(pos);
                    if (tileData == null)
                        continue;

                    var atlasCoord = GetCellAtlasCoords(pos);

                    var root = ResolveRootPosition(pos);

                    if (TileStates.TryGetValue(root, out var state))
                    {
                        SetCell(pos, state.BaseSourceId, atlasCoord);
                    }
                }
            }
        }
    }

    private Vector2I ResolveRootPosition(Vector2I pos)
    {
        var tileData = GetCellTileData(pos);
        if (tileData == null)
            return pos;

        var atlasCoord = GetCellAtlasCoords(pos);

        if (tileData.HasCustomData(MainTileDataLayerName))
        {
            var main = (Vector2I)tileData.GetCustomData(MainTileDataLayerName);

            if (main != Vector2I.Zero)
                return pos - (atlasCoord - main);
        }

        return pos;
    }

    private void InvalidateTileStatesAround(Array<Vector2I> changedTiles)
    {
        foreach (var tile in changedTiles)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    var pos = tile + new Vector2I(x, y);

                    var tileData = GetCellTileData(pos);
                    if (tileData == null)
                        continue;

                    var atlasCoord = GetCellAtlasCoords(pos);

                    var root = ResolveRootPosition(pos);

                    if (TileStates.TryGetValue(root, out var state))
                    {
                        state.AtlasCoord = atlasCoord;
                        GD.Print($"Updated atlas coord for tile state at {pos} to {atlasCoord}");
                    }
                }
            }
        }
    }

    public System.Collections.Generic.IEnumerable<Vector2I> GetCoveredTiles(Node2D collisionNode, Vector2 worldPosition)
    {
        if (collisionNode is CollisionShape2D collisionShape && collisionShape?.Shape is RectangleShape2D rectShape)
        {
            foreach (var tile in GetCoveredTilesFromRectangle(worldPosition, rectShape))
                yield return tile;
        }
        else if (collisionNode is CollisionPolygon2D collisionPolygon && collisionPolygon?.Polygon.Length >= 3)
        {
            foreach (var tile in GetCoveredTilesFromPolygon(collisionPolygon))
                yield return tile;
        }
    }
    #endregion

    #region [Utility]
    private System.Collections.Generic.IEnumerable<Vector2I> GetCoveredTilesFromPolygon(CollisionPolygon2D collisionPolygon)
    {
        if (collisionPolygon?.Polygon.Length >= 3)
        {
            var globalPoints = new System.Collections.Generic.List<Vector2>();

            foreach (var p in collisionPolygon.Polygon)
                globalPoints.Add(collisionPolygon.ToGlobal(p));

            Rect2 bounds = GetBounds(globalPoints);

            Vector2I start = LocalToMap(ToLocal(bounds.Position));
            Vector2I end = LocalToMap(ToLocal(bounds.End));

            for (int x = start.X; x <= end.X; x++)
            {
                for (int y = start.Y; y <= end.Y; y++)
                {
                    var tile = new Vector2I(x, y);
                    Vector2 worldPos = ToGlobal(MapToLocal(tile));

                    if (PointInPolygon(worldPos, globalPoints))
                        yield return tile;
                }
            }
        }
    }

    private System.Collections.Generic.IEnumerable<Vector2I> GetCoveredTilesFromRectangle(Vector2 worldPosition, RectangleShape2D rectShape)
    {
        var extents = rectShape.Size / 2f;

        Vector2 topLeft = worldPosition - extents;
        Vector2 bottomRight = worldPosition + extents;

        Vector2I start = LocalToMap(ToLocal(topLeft));
        Vector2I end = LocalToMap(ToLocal(bottomRight));

        for (int x = start.X; x <= end.X; x++)
        {
            for (int y = start.Y; y <= end.Y; y++)
            {
                yield return new Vector2I(x, y);
            }
        }
    }

    private Rect2 GetBounds(System.Collections.Generic.List<Vector2> points)
    {
        float minX = points[0].X;
        float maxX = points[0].X;
        float minY = points[0].Y;
        float maxY = points[0].Y;

        foreach (var p in points)
        {
            minX = Mathf.Min(minX, p.X);
            maxX = Mathf.Max(maxX, p.X);
            minY = Mathf.Min(minY, p.Y);
            maxY = Mathf.Max(maxY, p.Y);
        }

        return new Rect2(new Vector2(minX, minY), new Vector2(maxX - minX, maxY - minY));
    }

    private bool PointInPolygon(Vector2 point, System.Collections.Generic.List<Vector2> polygon)
    {
        bool inside = false;

        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            bool intersect =
                ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                (point.X < ((pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y + Mathf.Epsilon)) + pi.X);

            if (intersect)
                inside = !inside;
        }

        return inside;
    }

    private void InitializeTileStates()
    {
        TileStates.Clear();

        foreach (var cell in GetUsedCells())
        {
            var tileData = GetCellTileData(cell);
            if (tileData == null)
                continue;

            var state = CreateTileStateFromTileData(cell, tileData);

            Vector2 worldPos = MapToLocal(cell) + (TileSet.TileSize / 2);

            ApplyZones(state, worldPos);

            TileStates[cell] = state;
        }
    }

    private TileState CreateTileStateFromTileData(Vector2I rootPosition, TileData tileData)
    {
        var state = new TileState
        {
            MaxHealth = tileData.GetCustomData(HealthTileDataLayerName).AsInt32()
        };
        state.Health = state.MaxHealth;

        state.IsDestructible = tileData.GetCustomData(DestructibleTileDataLayerName).AsBool();
        state.Material = tileData.GetCustomData(MaterialTileDataLayerName).AsString();
        state.Strength = (float)tileData.GetCustomData(DestructionStrengthTileDataLayerName).AsDouble();

        state.RootPosition = rootPosition;
        state.BaseSourceId = GetCellSourceId(rootPosition);
        state.AtlasCoord = GetCellAtlasCoords(rootPosition);

        if (tileData.HasCustomData(TileSizeTileDataLayerName))
        {
            state.Size = (Vector2I)tileData.GetCustomData(TileSizeTileDataLayerName);
            if (state.Size == Vector2I.Zero)
                state.Size = Vector2I.One;
        }
        else
        {
            state.Size = Vector2I.One;
        }

        if (tileData.HasCustomData(DamageStagesDataLayerName))
        {
            var arr = (Array<int>)tileData.GetCustomData(DamageStagesDataLayerName);
            state.DamageStages = [.. arr];
        }
        else
        {
            state.DamageStages = null;
        }

        return state;
    }

    private void CacheZones()
    {
        TileModifierZones.Clear();

        foreach (var node in GetNodeOrNull(AreasNodePath)?.GetChildren() ?? [])
        {
            if (node is TileModifierZone zone)
                TileModifierZones.Add(zone);
        }
    }

    private void ApplyZones(TileState state, Vector2 worldPosition)
    {
        foreach (var zone in TileModifierZones)
        {
            if (IsPointAllowed(zone, worldPosition))
                continue;

            if (zone.OverrideDestructible)
                state.IsDestructible = zone.IsDestructible;

            if (zone.OverrideHealth)
            {
                state.Health = zone.Health;
                state.MaxHealth = zone.Health;
            }
        }
    }

    private bool IsPointAllowed(TileModifierZone zone, Vector2 worldPosition)
    {
        foreach (var child in zone.GetChildren())
        {
            if (child is CollisionShape2D shapeNode && shapeNode.Shape != null)
            {
                var shape = shapeNode.Shape;

                // transform world → local space of shape
                Transform2D? globalToLocal = shapeNode.GlobalTransform.AffineInverse();
                Vector2? localPoint = globalToLocal.Value * worldPosition;

                if (shape is RectangleShape2D rect)
                {
                    var extents = rect.Size * 0.5f;
                    if (Mathf.Abs(localPoint.Value.X) <= extents.X &&
                        Mathf.Abs(localPoint.Value.Y) <= extents.Y)
                    {
                        return true;
                    }
                }
                else if (shape is CircleShape2D circle)
                {
                    if (localPoint.Value.Length() <= circle.Radius)
                        return true;
                }
                else if (shape is WorldBoundaryShape2D boundary)
                {
                    // Transform world → shape local space (this handles ALL rotation correctly)
                    localPoint = shapeNode.ToLocal(worldPosition);

                    Vector2 normal = boundary.Normal;
                    float distance = boundary.Distance;

                    float d = normal.Dot(localPoint.Value);

                    if (d <= distance)
                        return true;
                }
            }
        }

        return false;
    }

    public bool HasTag(TileMapManager.TileLayerTag tag)
    {
        return (Tags & tag) != 0;
    }
    #endregion
}
