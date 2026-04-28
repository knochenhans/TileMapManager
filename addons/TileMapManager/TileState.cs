using Godot;

public partial class TileState : GodotObject
{
    public int Health;
    public int MaxHealth;

    public bool IsDestructible;
    public string Material;
    public float Strength;

    public int Energy;

    public int BaseSourceId;
    public int CurrentSourceId = -1;
    public Vector2I RootPosition;
    public Vector2I AtlasCoord;
    public Vector2I Size;

    public int[] DamageStages; // ordered stages, last can be -1 for removal
}