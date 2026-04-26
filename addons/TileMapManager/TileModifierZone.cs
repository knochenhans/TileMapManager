using Godot;

public partial class TileModifierZone : Area2D
{
    [Export] public string ID;

    [Export] public bool OverrideDestructible = false;
    [Export] public bool IsDestructible = true;

    [Export] public bool OverrideHealth = false;
    [Export] public int Health = 100;

    [ExportCategory("Fog of War")]
    [Export] public bool FOWHide = false;

    public CollisionShape2D CollisionShape => GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
    public CollisionPolygon2D CollisionPolygon => GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");

    public Node2D GetCollisionNode() => CollisionShape ?? (Node2D)CollisionPolygon;
}