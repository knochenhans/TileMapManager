using Godot;
using Godot.Collections;

public partial class FogHiddenArea : Area2D
{
    [Export] public string ID;

    CollisionShape2D CollisionShape => GetNode<CollisionShape2D>("CollisionShape2D");
    CollisionPolygon2D CollisionPolygon => GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");

    // public Rect2 GetRect()
    // {
    //     if (CollisionShape == null || CollisionShape.Shape == null)
    //         return new Rect2();

    //     var shape = CollisionShape.Shape;
    //     if (shape is RectangleShape2D rectShape)
    //     {
    //         var extents = rectShape.Size / 2f;
    //         var position = CollisionShape.Position - extents;
    //         return new Rect2(position, extents * 2);
    //     }

    //     return new Rect2();
    // }

    public System.Collections.Generic.IEnumerable<Vector2I> GetCoveredTiles(TileMapLayer tileMap)
    {
        var coveredTiles = new Array<Vector2I>();

        if (tileMap == null)
        {
            foreach (var item in coveredTiles)
            {
                yield return item;
            }
        }

        // --- Rectangle ---
        if (CollisionShape?.Shape is RectangleShape2D rectShape)
        {
            var extents = rectShape.Size / 2f;

            Vector2 topLeft = ToGlobal(CollisionShape.Position - extents);
            Vector2 bottomRight = ToGlobal(CollisionShape.Position + extents);

            Vector2I start = tileMap.LocalToMap(tileMap.ToLocal(topLeft));
            Vector2I end = tileMap.LocalToMap(tileMap.ToLocal(bottomRight));

            for (int x = start.X; x <= end.X; x++)
            {
                for (int y = start.Y; y <= end.Y; y++)
                {
                    coveredTiles.Add(new Vector2I(x, y));
                }
            }

            foreach (var item in coveredTiles)
            {
                yield return item;
            }
        }

        // --- Polygon ---
        if (CollisionPolygon?.Polygon.Length >= 3)
        {
            var globalPoints = new Array<Vector2>();

            foreach (var p in CollisionPolygon.Polygon)
                globalPoints.Add(CollisionPolygon.ToGlobal(p));

            // Bounding box (optimization)
            Rect2 bounds = GetBounds(globalPoints);

            Vector2I start = tileMap.LocalToMap(tileMap.ToLocal(bounds.Position));
            Vector2I end = tileMap.LocalToMap(tileMap.ToLocal(bounds.End));

            for (int x = start.X; x <= end.X; x++)
            {
                for (int y = start.Y; y <= end.Y; y++)
                {
                    Vector2 worldPos = tileMap.MapToLocal(new Vector2I(x, y));

                    if (PointInPolygon(worldPos, globalPoints))
                        yield return new Vector2I(x, y);
                }
            }
        }

        foreach (var item in coveredTiles)
        {
            yield return item;
        }
    }

    private Rect2 GetBounds(Array<Vector2> points)
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

    private bool PointInPolygon(Vector2 point, Array<Vector2> polygon)
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
}