using System.Collections.Generic;
using Godot;

public partial class TileMapGame : EntitySystemGame
{
    public override void InitGame(bool loadGame = false)
    {
        var stage = StageManager.Instance.CurrentStageScene;
        if (stage == null)
            return;

        GameInputManager = new TilemapGameInputManager(this, Camera, (stage as TileMapStage).TileMapManager);

        base.InitGame(loadGame);
    }

    protected override void InitSaveStateManager()
    {
        SaveStateManager = new TileMapGameSaveStateManager(this);

        StageManager.Instance.StoreStageStates();

        var initialState = StageManager.Instance.GetSaveData();
        SaveStateManager.SaveGameState(initialState, "init");
    }

    protected override void InitStageNode(StageNode stageNode)
    {
        if (stageNode is Entity entity)
            entity.IsOnTileMapLayer?.SetTileMapLayer((StageManager.Instance.CurrentStageScene as TileMapStage).TileMapManager.TileMapLayerGround);

        base.InitStageNode(stageNode);
    }

    protected HashSet<Vector2I> GetTilesFromRectangle(Rect2 rect)
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
}
