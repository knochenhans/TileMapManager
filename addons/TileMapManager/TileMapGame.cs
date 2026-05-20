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
        CombatSystem.TileImpactRequested += tileMapManager.ApplyTileImpact;
        tileMapManager.TileImpacted += GameContext.VisualEffectSystem.OnImpact;

        NavigationRegion = stage.GetNodeOrNull<NavigationRegion2D>("NavigationRegion2D");
        // GameInputManager = new TilemapGameInputManager(this, Camera, (stage as TileMapStage).TileMapManager);
    }

    public override void UninitGame()
    {
        var stage = StageManager.CurrentStageScene;
        if (stage == null)
            return;

        var tileMapManager = (stage as TileMapStage).TileMapManager;
        tileMapManager.TileImpacted -= GameContext.VisualEffectSystem.OnImpact;

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

    #region [Utility]
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
