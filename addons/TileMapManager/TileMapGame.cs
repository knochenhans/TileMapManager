using Godot;

public partial class TileMapGame : EntitySystemGame
{
    protected TileMapManager TileMapManager;

    public override void _Ready()
    {
        base._Ready();

        GameInputManager = new TilemapGameInputManager(this, Camera, TileMapManager);
    }

    public override void InitGame(bool loadGame = false)
    {
        var stage = StageManager.Instance.CurrentStageScene;
        if (stage == null)
            return;

        TileMapManager = stage.GetNodeOrNull<TileMapManager>("%TileMapManager");

        base.InitGame(loadGame);
    }

    protected override void InitStageNode(StageNode stageNode)
    {
        if (stageNode is Entity entity)
            entity.IsOnTileMapLayer?.SetTileMapLayer(TileMapManager.TileMapLayerGround);

        base.InitStageNode(stageNode);
    }
}
