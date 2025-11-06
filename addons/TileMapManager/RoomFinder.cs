using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class RoomFinder(TileMapLayer tilemap, Array<Vector2I> doorCells)
{
    public TileMapLayer Tilemap = tilemap;

    public readonly HashSet<Vector2I> DoorCells = [.. doorCells];

    public Array<Rect2I> FindRooms()
    {
        var rooms = new Array<Rect2I>();
        var visited = new HashSet<Vector2I>();

        Rect2I used = Tilemap.GetUsedRect();

        for (int x = used.Position.X; x < used.Position.X + used.Size.X; x++)
        {
            for (int y = used.Position.Y; y < used.Position.Y + used.Size.Y; y++)
            {
                var pos = new Vector2I(x, y);

                // Skip tiles with content or already visited
                if (visited.Contains(pos) || !IsEmpty(pos))
                    continue;

                // Flood fill to discover entire room and get its bounding rect
                var roomRect = FloodFillRoom(pos, visited);
                rooms.Add(roomRect);
            }
        }

        return rooms;
    }

    private Rect2I FloodFillRoom(Vector2I start, HashSet<Vector2I> visited)
    {
        var queue = new Queue<Vector2I>();
        queue.Enqueue(start);
        visited.Add(start);

        int minX = start.X, maxX = start.X;
        int minY = start.Y, maxY = start.Y;

        var dirs = new Vector2I[]
        {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var offset in dirs)
            {
                var next = current + offset;

                if (!visited.Contains(next) && IsEmpty(next))
                {
                    visited.Add(next);
                    queue.Enqueue(next);

                    // update bounds here
                    if (next.X < minX) minX = next.X;
                    if (next.X > maxX) maxX = next.X;
                    if (next.Y < minY) minY = next.Y;
                    if (next.Y > maxY) maxY = next.Y;
                }
            }
        }

        return new Rect2I(
            new Vector2I(minX, minY),
            new Vector2I(maxX - minX + 1, maxY - minY + 1)
        );
    }


    private bool IsEmpty(Vector2I pos)
    {
        // treat door cells and their immediate neighbors as non-empty (block flood)
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                var neighbor = pos + new Vector2I(dx, dy);
                if (DoorCells.Contains(neighbor))
                    return false;
            }
        }

        var used = Tilemap.GetUsedRect();

        // If outside tilemap bounds -> NOT empty (block flood)
        if (pos.X < used.Position.X || pos.X >= used.Position.X + used.Size.X ||
            pos.Y < used.Position.Y || pos.Y >= used.Position.Y + used.Size.Y)
            return false;

        return Tilemap.GetCellSourceId(pos) == -1;
    }
}
