using Godot;

public partial class TileModifierZone : Area2D
{
    [Export] public bool OverrideDestructible = true;
    [Export] public bool IsDestructible = false;

    [Export] public bool OverrideHealth = false;
    [Export] public int Health = 100;
}