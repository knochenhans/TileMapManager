using Godot;

public struct TileImpact
{
    public Vector2 WorldPosition;
    public Node2D Source;
    public int Damage;
    public TileMapManager.TileLayerTag TargetTag;

    // optional
    public string ForcedMaterial;
}