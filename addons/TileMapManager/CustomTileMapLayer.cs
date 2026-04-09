using Godot;
using Godot.Collections;

public partial class CustomTileMapLayer : TileMapLayer
{
    #region [Fields and Properties]
    [Export] public TileMapManager.TileLayerTag Tags;

    [Export] public int OriginalSourceID = -1;
    [Export] public int DestroyedSourceID = -1;

    [ExportGroup("Tile Data Layers")]
    [Export] public string DestructibleTileDataLayerName = "destructible";
    [Export] public string MaterialTileDataLayerName = "material";
    [Export] public string HealthTileDataLayerName = "health";
    [Export] public string MainTileDataLayerName = "main_tile";
    [Export] public string TileSizeLayerName = "tile_size";
    [Export] public string DestructionStrengthTileDataLayerName = "destruction_strength";
    [Export] public string DestructionModeLayerName = "destruction_mode";
    [Export] public string DamageStageCountLayerName = "damage_stage_count";

    [Signal] public delegate void TileHitEventHandler(Vector2 position, int damage, string material);
    [Signal] public delegate void TileDestroyedEventHandler(Vector2 position, string material, float strength);

    Array<Vector2I> DestroyedTiles = [];

    Dictionary<Vector2I, int> tileHealth = [];

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

    private partial class TileState : GodotObject
    {
        public int Health;
        public int MaxHealth;
        public int BaseSourceId;
        public Vector2I AtlasCoord;
        public Vector2I Size;
    }
    Dictionary<Vector2I, TileState> tileStates = [];
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

        if (!TileSet.HasCustomDataLayerByName(TileSizeLayerName))
            Logger.LogError($"Custom Data Layer '{TileSizeLayerName}' does not exist in TileSet for TileMapLayer '{Name}'.", "CustomTileMapLayer", Logger.LogTypeEnum.World);
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

        // 🔁 Resolve root tile
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

        bool isDestructible = tileData.GetCustomData(DestructibleTileDataLayerName).AsBool();
        if (!isDestructible)
            return result;

        string material = tileData.GetCustomData(MaterialTileDataLayerName).AsString();
        float strength = (float)tileData.GetCustomData(DestructionStrengthTileDataLayerName).AsDouble();

        result.WasHit = true;
        result.Material = material;

        if (tileData.HasCustomData(TileSizeLayerName))
        {
            tileSize = (Vector2I)tileData.GetCustomData(TileSizeLayerName);
            if (tileSize == Vector2I.Zero)
                tileSize = Vector2I.One;
        }

        int maxHealth = tileData.GetCustomData(HealthTileDataLayerName).AsInt32();

        if (!tileHealth.ContainsKey(rootPosition))
            tileHealth[rootPosition] = maxHealth;

        tileHealth[rootPosition] -= damage;
        int currentHealth = tileHealth[rootPosition];

        ApplyVisualStage(rootPosition, atlasCoord, tileSize, mainDistance, currentHealth, maxHealth, tileData);

        if (currentHealth <= 0)
        {
            var destructionMode = TileDestructionMode.DamageStages;

            if (tileData.HasCustomData(DestructionModeLayerName))
                destructionMode = (TileDestructionMode)(int)tileData.GetCustomData(DestructionModeLayerName);

            switch (destructionMode)
            {
                case TileDestructionMode.RemoveTile:
                    RemoveTile(rootPosition, tileSize);
                    // RestoreNeighbors(rootPosition);
                    break;
            }

            tileHealth.Remove(rootPosition);

            result.WasDestroyed = true;
            result.Strength = strength;
        }

        return result;
    }

    // private void RestoreNeighbors(Vector2I center)
    // {
    //     for (int x = -1; x <= 1; x++)
    //     {
    //         for (int y = -1; y <= 1; y++)
    //         {
    //             var pos = center + new Vector2I(x, y);

    //             if (!tileStates.TryGetValue(pos, out var state))
    //                 continue;

    //             ApplyVisualStageFromState(pos, state);
    //         }
    //     }
    // }

    private void ApplyVisualStageFromState(Vector2I rootPosition, TileState state)
    {
        float t = 1f - ((float)state.Health / state.MaxHealth);

        int sourceId = state.BaseSourceId;

        // your damage stage logic
        if (t > 0.5f)
            sourceId = state.BaseSourceId + 1; // example

        ApplyStage(rootPosition, sourceId, state.AtlasCoord, state.Size);
    }

    private void ApplyVisualStage(Vector2I rootPosition, Vector2I atlasCoord, Vector2I tileSize, Vector2I mainDistance, int currentHealth, int maxHealth, TileData tileData)
    {
        if (!tileHealth.ContainsKey(rootPosition))
            return;

        // 🧠 Preferred: explicit stage mapping
        if (tileData.HasCustomData("damage_stages"))
        {
            var stages = (Array<int>)tileData.GetCustomData("damage_stages");

            float t = 1f - ((float)currentHealth / maxHealth);
            // stages contains only the NEXT stages, so include the current stage as an extra slot
            int totalSlots = stages.Count + 1;
            int stageIndex = Mathf.Clamp((int)(t * totalSlots), 0, totalSlots - 1);

            // stageIndex == 0 -> still the current stage, no change
            if (stageIndex == 0)
                return;

            int targetSourceID = stages[stageIndex - 1];

            if (targetSourceID < 0)
            {
                RemoveTile(rootPosition, tileSize);
                // RestoreNeighbors(rootPosition);
                return;
            }

            ApplyStage(rootPosition, targetSourceID, atlasCoord, tileSize);
            return;
        }

        // fallback (legacy)
        if (tileData.HasCustomData(DamageStageCountLayerName))
        {
            int stageCount = tileData.GetCustomData(DamageStageCountLayerName).AsInt32();

            float t = 1f - ((float)currentHealth / maxHealth);
            int stageIndex = Mathf.Clamp((int)(t * stageCount), 0, stageCount - 1);

            ApplyStage(rootPosition, GetCellSourceId(rootPosition) + stageIndex, atlasCoord, tileSize);
        }
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
    public bool HasTag(TileMapManager.TileLayerTag tag)
    {
        return (Tags & tag) != 0;
    }
    #endregion
}
