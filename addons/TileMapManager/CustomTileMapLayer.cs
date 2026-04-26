using Godot;
using Godot.Collections;

public partial class CustomTileMapLayer : TileMapLayer
{
    [Export] public NodePath AreasNodePath;

    public Array<TileModifierZone> TileModifierZones = [];

    public override void _Ready()
    {
        base._Ready();

        CacheZones();
    }

    protected void CacheZones()
    {
        TileModifierZones.Clear();

        foreach (var node in GetNodeOrNull(AreasNodePath)?.GetChildren() ?? [])
        {
            if (node is TileModifierZone zone)
                TileModifierZones.Add(zone);
        }
    }

    public System.Collections.Generic.IEnumerable<Vector2I> GetCoveredTiles(Node2D collisionNode, Vector2 worldPosition)
    {
        if (collisionNode is CollisionShape2D collisionShape && collisionShape?.Shape is RectangleShape2D rectShape)
        {
            foreach (var tile in GetCoveredTilesFromRectangle(worldPosition, rectShape))
                yield return tile;
        }
        else if (collisionNode is CollisionPolygon2D collisionPolygon && collisionPolygon?.Polygon.Length >= 3)
        {
            foreach (var tile in GetCoveredTilesFromPolygon(collisionPolygon))
                yield return tile;
        }
    }

    protected System.Collections.Generic.IEnumerable<Vector2I> GetCoveredTilesFromPolygon(CollisionPolygon2D collisionPolygon)
    {
        if (collisionPolygon?.Polygon.Length >= 3)
        {
            var globalPoints = new System.Collections.Generic.List<Vector2>();

            foreach (var p in collisionPolygon.Polygon)
                globalPoints.Add(collisionPolygon.ToGlobal(p));

            Rect2 bounds = GetBounds(globalPoints);

            Vector2I start = LocalToMap(ToLocal(bounds.Position));
            Vector2I end = LocalToMap(ToLocal(bounds.End));

            for (int x = start.X; x <= end.X; x++)
            {
                for (int y = start.Y; y <= end.Y; y++)
                {
                    var tile = new Vector2I(x, y);
                    Vector2 worldPos = ToGlobal(MapToLocal(tile));

                    if (PointInPolygon(worldPos, globalPoints))
                        yield return tile;
                }
            }
        }
    }

    protected System.Collections.Generic.IEnumerable<Vector2I> GetCoveredTilesFromRectangle(Vector2 worldPosition, RectangleShape2D rectShape)
    {
        var extents = rectShape.Size / 2f;

        Vector2 topLeft = worldPosition - extents;
        Vector2 bottomRight = worldPosition + extents;

        Vector2I start = LocalToMap(ToLocal(topLeft));
        Vector2I end = LocalToMap(ToLocal(bottomRight));

        for (int x = start.X; x <= end.X; x++)
        {
            for (int y = start.Y; y <= end.Y; y++)
            {
                yield return new Vector2I(x, y);
            }
        }
    }

    protected Rect2 GetBounds(System.Collections.Generic.List<Vector2> points)
    {
        float minX = points[0].X;
        float maxX = points[0].X;
        float minY = points[0].Y;
        float maxY = points[0].Y;

        foreach (var p in points)
        {
            minX = Mathf.Min(minX, p.X);
            maxX = Mathf.Max(maxX, p.X);
            minY = Mathf.Min(minY, p.Y);
            maxY = Mathf.Max(maxY, p.Y);
        }

        return new Rect2(new Vector2(minX, minY), new Vector2(maxX - minX, maxY - minY));
    }

    protected bool PointInPolygon(Vector2 point, System.Collections.Generic.List<Vector2> polygon)
    {
        bool inside = false;

        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            bool intersect =
                ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                (point.X < ((pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y + Mathf.Epsilon)) + pi.X);

            if (intersect)
                inside = !inside;
        }

        return inside;
    }

    protected bool IsPointAllowed(TileModifierZone zone, Vector2 worldPosition)
    {
        foreach (var child in zone.GetChildren())
        {
            if (child is CollisionShape2D shapeNode && shapeNode.Shape != null)
            {
                var shape = shapeNode.Shape;

                // transform world → local space of shape
                Transform2D? globalToLocal = shapeNode.GlobalTransform.AffineInverse();
                Vector2? localPoint = globalToLocal.Value * worldPosition;

                if (shape is RectangleShape2D rect)
                {
                    var extents = rect.Size * 0.5f;
                    if (Mathf.Abs(localPoint.Value.X) <= extents.X &&
                        Mathf.Abs(localPoint.Value.Y) <= extents.Y)
                    {
                        return true;
                    }
                }
                else if (shape is CircleShape2D circle)
                {
                    if (localPoint.Value.Length() <= circle.Radius)
                        return true;
                }
                else if (shape is WorldBoundaryShape2D boundary)
                {
                    // Transform world → shape local space (this handles ALL rotation correctly)
                    localPoint = shapeNode.ToLocal(worldPosition);

                    Vector2 normal = boundary.Normal;
                    float distance = boundary.Distance;

                    float d = normal.Dot(localPoint.Value);

                    if (d <= distance)
                        return true;
                }
            }
        }

        return false;
    }
}