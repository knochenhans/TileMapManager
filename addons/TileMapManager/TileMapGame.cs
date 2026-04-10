using System.Collections.Generic;
using System.Threading.Tasks;

using Godot;

public partial class TileMapGame : EntitySystemGame
{
    NavigationRegion2D NavigationRegion;

    #region [Lifecycle]
    public override void InitGame(bool loadGame = false)
    {
        var stage = StageManager.CurrentStageScene;
        if (stage == null)
            return;

        base.InitGame(loadGame);

        var tileMapManager = (stage as TileMapStage).TileMapManager;
        tileMapManager.TileHit += StageManager.CombatSystem.OnAddExplosion;
        tileMapManager.TileDestroyed += OnTileDestroyed;
        tileMapManager.TileDestroyed += StageManager.CombatSystem.OnAddExplosion;

        NavigationRegion = stage.GetNodeOrNull<NavigationRegion2D>("NavigationRegion2D");
        // GameInputManager = new TilemapGameInputManager(this, Camera, (stage as TileMapStage).TileMapManager);
    }

    public override void UninitGame()
    {
        var stage = StageManager.CurrentStageScene;
        if (stage == null)
            return;

        var tileMapManager = (stage as TileMapStage).TileMapManager;
        tileMapManager.TileHit -= StageManager.CombatSystem.OnAddExplosion;
        tileMapManager.TileDestroyed -= OnTileDestroyed;
        tileMapManager.TileDestroyed -= StageManager.CombatSystem.OnAddExplosion;

        base.UninitGame();
    }

    protected override void InitStageNode(StageNode stageNode)
    {
        if (stageNode is Entity entity)
        {
            var tileMapManager = (StageManager.CurrentStageScene as TileMapStage).TileMapManager;
            entity.InitForTileMapLayer(tileMapManager);
        }

        base.InitStageNode(stageNode);
    }
    #endregion

    #region [Events]
    protected virtual void OnTileDestroyed(ExplosionResource explosionResource, Node2D source, Vector2 position, float strength)
    {
        (Camera as Camera).AddTrauma(strength);
    }
    #endregion

    #region [Utility]
    protected static HashSet<Vector2I> GetTilesFromRectangle(Rect2 rect)
    {
        HashSet<Vector2I> roomTiles = [];

        var tileMapManager = (StageManager.Instance.CurrentStageScene as TileMapStage).TileMapManager;

        var topLeft = tileMapManager.WorldToMap(rect.Position) + new Vector2I(1, -1);
        var bottomRight = tileMapManager.WorldToMap(rect.Position + rect.Size) - new Vector2I(0, 0);

        for (int x = topLeft.X; x <= bottomRight.X; x++)
        {
            for (int y = topLeft.Y; y <= bottomRight.Y; y++)
            {
                roomTiles.Add(new Vector2I(x, y));
            }
        }
        return roomTiles;
    }

    protected async Task BakeNavigationPolygonAsync()
    {
        if (NavigationRegion.IsBaking())
        {
            Logger.Log("Waiting for navigation polygon to finish baking...", Logger.LogTypeEnum.World);

            //TODO: Doesn't seem to work as expected
            await ToSignal(NavigationRegion, NavigationRegion2D.SignalName.BakeFinished);
        }

        Logger.Log("Baking navigation polygon...", Logger.LogTypeEnum.World);
        CallDeferred(MethodName.BakeNavigationPolygon);
    }

    protected void BakeNavigationPolygon()
    {
        NavigationRegion.BakeNavigationPolygon();
    }
    #endregion
}
