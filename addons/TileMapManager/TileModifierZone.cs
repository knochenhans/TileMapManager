using Godot;

public partial class TileModifierZone : Area2D
{
    [Export] public string ID;

    [Export] public Godot.Collections.Array<TileModifierResource> Modifiers = [];

    public CollisionShape2D CollisionShape => GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
    public CollisionPolygon2D CollisionPolygon => GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");

    public Node2D GetCollisionNode() => CollisionShape ?? (Node2D)CollisionPolygon;
}