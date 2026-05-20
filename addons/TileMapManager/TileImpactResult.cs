using Godot;

public partial class TileImpactResult : ImpactResult
{
    public GeneralTileMapLayer HitLayer;
    public Vector2I Cell;

    public bool WasHit;
    public bool WasDestroyed;

    public float Strength;
    public string Material;

    public VisualEffectType EffectType;
    public bool RequiresNavigationRebuild;

}