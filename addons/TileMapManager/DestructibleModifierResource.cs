using Godot;

[Tool, GlobalClass]
public partial class DestructibleModifierResource : TileModifierResource
{
    [Export] public bool OverrideDestructible;
    [Export] public bool IsDestructible;
}