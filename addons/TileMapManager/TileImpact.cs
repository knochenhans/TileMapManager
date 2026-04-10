using Godot;

public struct TileImpact
{
    public Vector2 WorldPosition;
    public Node2D Source;
    public Vector2I SourceCell;
    public float Range;
    public int Damage;
    public TileMapManager.TileLayerTag TargetTag;
    public uint CollisionMask;

    // optional
    public string ForcedMaterial;
}