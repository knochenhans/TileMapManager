using Godot;
using Godot.Collections;

public partial class FogHiddenArea : Area2D
{
    [Export] public string ID;

    public CollisionShape2D CollisionShape => GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
    public CollisionPolygon2D CollisionPolygon => GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");

    public Node2D GetCollisionNode()
    {
        return CollisionShape ?? (Node2D)CollisionPolygon;
    }
}