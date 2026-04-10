using System.Linq;
using Godot;
using Godot.Collections;

public partial class CustomTileMapLayer : TileMapLayer
{
    #region [Fields and Properties]
    [Export] public TileMapManager.TileLayerTag Tags;

    [Export] public int OriginalSourceID = -1;
    [Export] public int DestroyedSourceID = -1;

    [ExportGroup("Tile Data Layers")]
    [Export] public bool UseTileDataLayers = false;
    [Export] public string DestructibleTileDataLayerName = "destructible";
    [Export] public string MaterialTileDataLayerName = "material";
    [Export] public string HealthTileDataLayerName = "health";
    [Export] public string MainTileDataLayerName = "main_tile";
    [Export] public string TileSizeTileDataLayerName = "tile_size";
    [Export] public string DestructionStrengthTileDataLayerName = "destruction_strength";
    [Export] public string DestructionModeTileDataLayerName = "destruction_mode";
    [Export] public string DamageStageCountTileDataLayerName = "damage_stage_count";

    [Export] public NodePath AreasNodePath;

    [Signal] public delegate void TileHitEventHandler(Vector2 position, int damage, string material);
    [Signal] public delegate void TileDestroyedEventHandler(Vector2 position, string material, float strength);

    Array<Vector2I> DestroyedTiles = [];

    Dictionary<Vector2I, TileState> TileStates = [];
    Array<TileModifierZone> zones = [];

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

    #region [Events]
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

        int tileSourceID = GetCellSourceId(tilePosition);
        if (tileSourceID < 0)
            return result;

        if (TileSet.GetSource(tileSourceID) is not TileSetAtlasSource tileSource)
            return result;

        Vector2I atlasCoord = GetCellAtlasCoords(tilePosition);
        Vector2I tileSize = Vector2I.One;
        Vector2I mainDistance = Vector2I.Zero;

        var tileData = GetCellTileData(tilePosition);
        if (tileData == null)
            return result;

        // Resolve root tile
        if (tileData.HasCustomData(MainTileDataLayerName))
        {
            var mainTileAtlasCoord = (Vector2I)tileData.GetCustomData(MainTileDataLayerName);
            if (mainTileAtlasCoord != Vector2I.Zero)
            {
                mainDistance = atlasCoord - mainTileAtlasCoord;
                atlasCoord = mainTileAtlasCoord;
            }
        }

        Vector2I rootPosition = tilePosition - mainDistance;

        tileData = tileSource.GetTileData(atlasCoord, 0);
        if (tileData == null)
            return result;

        if (tileData.HasCustomData(TileSizeTileDataLayerName))
        {
            tileSize = (Vector2I)tileData.GetCustomData(TileSizeTileDataLayerName);
            if (tileSize == Vector2I.Zero)
                tileSize = Vector2I.One;
        }

        if (!TileStates.TryGetValue(rootPosition, out var state))
        {
            state = CreateTileStateFromTileData(rootPosition, tileData);
            TileStates[rootPosition] = state;
        }

        if (!state.IsDestructible)
            return result;

        result.WasHit = true;
        result.Material = state.Material;

        state.Health -= damage;
        int currentHealth = state.Health;

        ApplyVisualStageFromState(rootPosition, state);

        if (currentHealth <= 0)
        {
            switch (state.DestructionMode)
            {
                case TileDestructionMode.RemoveTile:
                    RemoveTile(rootPosition, tileSize);
                    break;
            }

            TileStates.Remove(rootPosition);

            result.WasDestroyed = true;
            result.Strength = state.Strength;
        }

        return result;
    }

    private void ApplyVisualStageFromState(Vector2I rootPosition, TileState state)
    {
        float t = 1f - ((float)state.Health / state.MaxHealth);

        // ✅ Use real stage data
        if (state.DamageStages?.Length > 0)
        {
            int totalSlots = state.DamageStages.Length + 1;
            int stageIndex = Mathf.Clamp((int)(t * totalSlots), 0, totalSlots - 1);

            // stage 0 = original tile → no change
            if (stageIndex == 0)
                return;

            int targetSourceID = state.DamageStages[stageIndex - 1];

            // 💥 destruction via stage
            if (targetSourceID < 0)
            {
                RemoveTile(rootPosition, state.Size);
                return;
            }

            ApplyStage(rootPosition, targetSourceID, state.AtlasCoord, state.Size);
            return;
        }

        // 🧓 fallback (optional)
        int sourceId = state.BaseSourceId;

        if (t > 0.5f)
            sourceId = state.BaseSourceId + 1;

        ApplyStage(rootPosition, sourceId, state.AtlasCoord, state.Size);
    }

    private void ApplyStage(Vector2I rootPosition, int sourceID, Vector2I atlasCoord, Vector2I tileSize)
    {
        for (int x = 0; x < tileSize.X; x++)
        {
            for (int y = 0; y < tileSize.Y; y++)
            {
                var offset = new Vector2I(x, y);

                SetCell(
                    rootPosition + offset,
                    sourceID,
                    atlasCoord + offset
                );
            }
        }
    }

    private void RemoveTile(Vector2I rootPosition, Vector2I tileSize)
    {
        for (int x = 0; x < tileSize.X; x++)
        {
            for (int y = 0; y < tileSize.Y; y++)
            {
                var offset = new Vector2I(x, y);
                SetCellsTerrainConnect([rootPosition + offset], 0, -1);
            }
        }
    }
    #endregion

    #region [General Logic]
    public void UndoDestroyTile(Vector2I position)
    {
        if (DestroyedTiles.Contains(position))
        {
            SetCell(position, OriginalSourceID);
            DestroyedTiles.Remove(position);
        }
    }
    #endregion

    #region [Utility]
    private void CacheZones()
    {
        zones.Clear();

        foreach (var node in GetNodeOrNull(AreasNodePath)?.GetChildren() ?? [])
        {
            if (node is TileModifierZone zone)
                zones.Add(zone);
        }
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

    private void ApplyZones(TileState state, Vector2 worldPosition)
    {
        foreach (var zone in zones)
        {
            if (!IsPointInsideZone(zone, worldPosition))
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

    private bool IsPointInsideZone(TileModifierZone zone, Vector2 worldPosition)
    {
        foreach (var child in zone.GetChildren())
        {
            if (child is CollisionShape2D shapeNode && shapeNode.Shape != null)
            {
                var shape = shapeNode.Shape;

                // transform world → local space of shape
                Transform2D globalToLocal = shapeNode.GlobalTransform.AffineInverse();
                Vector2 localPoint = globalToLocal * worldPosition;

                if (shape is RectangleShape2D rect)
                {
                    var extents = rect.Size * 0.5f;
                    if (Mathf.Abs(localPoint.X) <= extents.X &&
                        Mathf.Abs(localPoint.Y) <= extents.Y)
                    {
                        return true;
                    }
                }
                else if (shape is CircleShape2D circle)
                {
                    if (localPoint.Length() <= circle.Radius)
                        return true;
                }

                // Extend here if you use more shapes
            }
        }

        return false;
    }

    private TileState CreateTileStateFromTileData(Vector2I pos, TileData tileData)
    {
        int maxHealth = 1;
        if (tileData.HasCustomData(HealthTileDataLayerName))
            maxHealth = tileData.GetCustomData(HealthTileDataLayerName).AsInt32();

        bool isDestructible = false;
        if (tileData.HasCustomData(DestructibleTileDataLayerName))
            isDestructible = tileData.GetCustomData(DestructibleTileDataLayerName).AsBool();

        string material = string.Empty;
        if (tileData.HasCustomData(MaterialTileDataLayerName))
            material = tileData.GetCustomData(MaterialTileDataLayerName).AsString();

        TileDestructionMode destructionMode = TileDestructionMode.None;
        if (tileData.HasCustomData(DestructionModeTileDataLayerName))
            destructionMode = (TileDestructionMode)tileData.GetCustomData(DestructionModeTileDataLayerName).AsInt32();

        float strength = 0f;
        if (tileData.HasCustomData(DestructionStrengthTileDataLayerName))
            strength = tileData.GetCustomData(DestructionStrengthTileDataLayerName).AsSingle();

        int[] damageStages = [];
        if (tileData.HasCustomData("damage_stages"))
            damageStages = [.. (Array<int>)tileData.GetCustomData("damage_stages")];

        return new TileState
        {
            Health = maxHealth,
            MaxHealth = maxHealth,
            IsDestructible = isDestructible,
            Material = material,
            DestructionMode = destructionMode,
            Strength = strength,
            DamageStages = damageStages,

            BaseSourceId = GetCellSourceId(pos),
            AtlasCoord = GetCellAtlasCoords(pos),
            Size = Vector2I.One // extend if using multi-tile
        };
    }

    public bool HasTag(TileMapManager.TileLayerTag tag)
    {
        return (Tags & tag) != 0;
    }
    #endregion
}
