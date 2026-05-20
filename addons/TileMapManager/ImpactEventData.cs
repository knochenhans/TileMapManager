using Godot;

public partial class ImpactEventData : RefCounted
{
    public Vector2 Position;
    public Vector2 Direction;
    public MaterialType MaterialType;
    public float Force;

    public bool Destroyed;

    public VisualEffectType VisualEffectType;

    public Color LightColorOverride;
    public float LightEnergyOverride;
}