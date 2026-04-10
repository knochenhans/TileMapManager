using Godot;

public partial class TileState : GodotObject
{
    public int Health;
    public int MaxHealth;
    public bool IsDestructible;
    public string Material;

    public int BaseSourceId;
    public Vector2I AtlasCoord;
    public Vector2I Size;

    public CustomTileMapLayer.TileDestructionMode DestructionMode;

    public float Strength;

    public int[] DamageStages;
}