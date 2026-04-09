using System.Threading.Tasks;
using Godot;
using Godot.Collections;

public record LandscapeDefinition(
    string ID,
    TileMapLayer TileMapLayer,
    Vector2I AtlasStart,
    int TilesCount,
    float LowerThreshold = 0.0f,
    float UpperThreshold = 1.0f,
    bool Repeat = false,
    bool Random = false
);

public partial class MapGenerator : GodotObject
{
    #region [Fields and Properties]
    public NoiseTexture2D TileSetNoiseTexture;
    #endregion

    #region [Saving and Loading]
    public async Task Load()
    {
        if (TileSetNoiseTexture != null)
            await ToSignal(TileSetNoiseTexture, "changed");
    }
    #endregion

    #region [Lifecycle]
    // public void SetRandomArea(TileMapLayer tileMapLayer, Vector2I start, Vector2I size, Vector2I atlasStart, int tilesCount, float lowerThreshold = 0.0f, float upperThreshold = 1.0f, bool repeat = false)
    // {
    //     var image = TileSetNoiseTexture.GetImage();
    //     int imageWidth = image.GetWidth();
    //     int imageHeight = image.GetHeight();

    //     for (int x = 0; x < size.X; x++)
    //     {
    //         for (int y = 0; y < size.Y; y++)
    //         {
    //             var pos = new Vector2I(start.X + x, start.Y + y);
    //             // var cell = TileMapLayer.GetCellSourceId(pos);
    //             // if (cell != -1)
    //             //     continue;

    //             int px, py;
    //             if (repeat)
    //             {
    //                 px = x % imageWidth;
    //                 py = y % imageHeight;
    //             }
    //             else
    //             {
    //                 px = (int)Mathf.Floor((float)x / size.X * imageWidth);
    //                 py = (int)Mathf.Floor((float)y / size.Y * imageHeight);
    //                 px = Mathf.Clamp(px, 0, imageWidth - 1);
    //                 py = Mathf.Clamp(py, 0, imageHeight - 1);
    //             }

    //             Color noiseColor = image.GetPixel(px, py);
    //             float noiseValue = noiseColor.R;

    //             if (noiseValue < lowerThreshold || noiseValue > upperThreshold)
    //                 continue;

    //             // Map noise value to tile index within the atlas range, distributing tiles evenly
    //             float mappedValue = (noiseValue - lowerThreshold) / (upperThreshold - lowerThreshold);
    //             mappedValue = Mathf.Clamp(mappedValue, 0.0f, 1.0f);

    //             // Distribute tiles evenly: each tile covers an equal range of mappedValue
    //             int tileOffset = Mathf.Min((int)(mappedValue * tilesCount), tilesCount - 1);
    //             Vector2I tileId = atlasStart + new Vector2I(tileOffset, 0);

    //             tileMapLayer.SetCell(pos, 1, tileId);
    //         }
    //     }
    // }

    // public void SetRandomNoiseAreaAroundTile(Vector2I center, Vector2I size, Vector2I atlasStart, int tilesCount, int innerRadius = 0)
    // {
    //     int radiusX = size.X / 2;
    //     int radiusY = size.Y / 2;

    //     var tileSetSource = TileMapLayer.TileSet.GetSource(1);

    //     var image = TileSetNoiseTexture.GetImage();
    //     int imageWidth = image.GetWidth();
    //     int imageHeight = image.GetHeight();

    //     // Choose a random starting point in the image, ensuring the area fits
    //     int startX = GD.RandRange(0, imageWidth - size.X);
    //     int startY = GD.RandRange(0, imageHeight - size.Y);

    //     for (int x = -radiusX; x <= radiusX; x++)
    //     {
    //         for (int y = -radiusY; y <= radiusY; y++)
    //         {
    //             // Check if the point is inside the ellipse
    //             if (x * x * radiusY * radiusY + y * y * radiusX * radiusX > radiusX * radiusX * radiusY * radiusY)
    //                 continue;

    //             // Ignore tiles within innerRadius (circle)
    //             if (innerRadius > 0 && (x * x + y * y) < innerRadius * innerRadius)
    //                 continue;

    //             var pos = new Vector2I(center.X + x, center.Y + y);
    //             var cell = TileMapLayer.GetCellSourceId(pos);
    //             if (cell != -1)
    //                 continue;

    //             int px = startX + (x + radiusX);
    //             int py = startY + (y + radiusY);

    //             // Clamp to image bounds just in case
    //             px = Mathf.Clamp(px, 0, imageWidth - 1);
    //             py = Mathf.Clamp(py, 0, imageHeight - 1);

    //             Color noiseColor = image.GetPixel(px, py);
    //             float noiseValue = noiseColor.R;

    //             // Map noise value to tile index within the atlas range
    //             int tileOffset = Mathf.FloorToInt(noiseValue * tilesCount);
    //             Vector2I tileId = atlasStart + new Vector2I(tileOffset, 0);

    //             TileMapLayer.SetCell(pos, 1, tileId);
    //         }
    //     }
    // }

    // public void SetRandomArea(Vector2I center, Vector2I size, Vector2I atlasStart, int tilesCount, int innerRadius = 0)
    // {
    //     int radiusX = size.X / 2;
    //     int radiusY = size.Y / 2;

    //     var tileSetSource = TileMapLayer.TileSet.GetSource(1);

    //     for (int x = -radiusX; x <= radiusX; x++)
    //     {
    //         for (int y = -radiusY; y <= radiusY; y++)
    //         {
    //             // Check if the point is inside the ellipse
    //             if (x * x * radiusY * radiusY + y * y * radiusX * radiusX > radiusX * radiusX * radiusY * radiusY)
    //                 continue;

    //             // Ignore tiles within innerRadius (circle)
    //             if (innerRadius > 0 && (x * x + y * y) < innerRadius * innerRadius)
    //                 continue;

    //             var pos = new Vector2I(center.X + x, center.Y + y);

    //             if (GD.Randf() > 0.01f)
    //                 continue;

    //             int tileOffset = GD.RandRange(0, tilesCount - 1);
    //             Vector2I tileId = atlasStart + new Vector2I(tileOffset, 0);

    //             TileMapLayer.SetCell(pos, 1, tileId);
    //         }
    //     }
    // }

    // public void SetRandomAreaAroundPosition(Vector2 position, Vector2I size, Vector2I atlasStart, int tileSourceRangeEnd, int innerRadius = 0)
    // {
    //     var tileMapPosition = GetPositionInTileMap(position);
    //     SetRandomAreaAroundTile(tileMapPosition, size, atlasStart, tileSourceRangeEnd, innerRadius);
    // }

    // public void SetRandomNoiseAreaAroundPosition(Vector2 position, Vector2I size, Vector2I atlasStart, int tilesCount, int innerRadius = 0)
    // {
    //     var tileMapPosition = GetPositionInTileMap(position);
    //     SetRandomNoiseAreaAroundTile(tileMapPosition, size, atlasStart, tilesCount, innerRadius);
    // }

    public void SetRandomArea(LandscapeDefinition[] landscapes, Vector2I start, Vector2I size)
    {
        var image = TileSetNoiseTexture.GetImage();
        int imageWidth = image.GetWidth();
        int imageHeight = image.GetHeight();

        var positions = new Dictionary<int, Array<Vector2I>>();

        foreach (var landscape in landscapes)
        {
            positions[landscape.GetHashCode()] = [];
        }

        for (int x = 0; x < size.X; x++)
        {
            for (int y = 0; y < size.Y; y++)
            {
                var pos = new Vector2I(start.X + x, start.Y + y);

                int px = (int)Mathf.Floor((float)x / size.X * imageWidth);
                int py = (int)Mathf.Floor((float)y / size.Y * imageHeight);
                px = Mathf.Clamp(px, 0, imageWidth - 1);
                py = Mathf.Clamp(py, 0, imageHeight - 1);

                Color noiseColor = image.GetPixel(px, py);
                float noiseValue = noiseColor.R;

                foreach (var landscape in landscapes)
                {
                    if (landscape.Random)
                    {
                        if (GD.Randf() > 0.01f)
                            continue;

                        Vector2I tileId = landscape.AtlasStart + new Vector2I(GD.RandRange(0, landscape.TilesCount - 1), 0);
                        if (landscape.TileMapLayer.GetCellSourceId(pos) != -1)
                            continue;
                        if (noiseValue < landscape.LowerThreshold || noiseValue > landscape.UpperThreshold)
                            continue;
                        landscape.TileMapLayer.SetCell(pos, 0, tileId);
                    }
                    else
                    {
                        var cell = landscape.TileMapLayer.GetCellSourceId(pos);
                        if (cell != -1)
                            continue;

                        if (noiseValue < landscape.LowerThreshold || noiseValue > landscape.UpperThreshold)
                            continue;

                        // Map noise value to tile index within the atlas range, distributing tiles evenly
                        float mappedValue = (noiseValue - landscape.LowerThreshold) / (landscape.UpperThreshold - landscape.LowerThreshold);
                        mappedValue = Mathf.Clamp(mappedValue, 0.0f, 1.0f);

                        int tileOffset = Mathf.Min((int)(mappedValue * landscape.TilesCount), landscape.TilesCount - 1);
                        Vector2I tileId = landscape.AtlasStart + new Vector2I(tileOffset, 0);
                        positions[landscape.GetHashCode()].Add(pos);
                    }
                }
            }
        }

        for (int i = 0; i < landscapes.Length; i++)
        {
            if (positions.ContainsKey(landscapes[i].GetHashCode()))
            {
                landscapes[i].TileMapLayer.SetCellsTerrainConnect(positions[landscapes[i].GetHashCode()], 0, i);
            }
        }
    }
    #endregion

    #region [Utility]
    public static Vector2I GetPositionInTileMap(Vector2 position, Vector2I TileSize)
    {
        var tileX = (int)(position.X / TileSize.X);
        var tileY = (int)(position.Y / TileSize.Y);
        return new Vector2I(tileX, tileY);
    }

    public bool IsNoiseCircleInRange(Vector2I center, int radius, float minValue, float maxValue)
    {
        var image = TileSetNoiseTexture.GetImage();
        int imageWidth = image.GetWidth();
        int imageHeight = image.GetHeight();

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                // Check if inside circle
                if ((x * x) + (y * y) > radius * radius)
                    continue;

                int px = center.X + x;
                int py = center.Y + y;

                // Clamp to image bounds
                px = Mathf.Clamp(px, 0, imageWidth - 1);
                py = Mathf.Clamp(py, 0, imageHeight - 1);

                float noiseValue = image.GetPixel(px, py).R;
                if (noiseValue < minValue || noiseValue > maxValue)
                    return false;
            }
        }
        return true;
    }

    public Array<Vector2I> FindNoiseSpotsInRange(int radius, float minValue, float maxValue, int count, int maxTries = 10000)
    {
        var image = TileSetNoiseTexture.GetImage();
        int imageWidth = image.GetWidth();
        int imageHeight = image.GetHeight();

        var foundCenters = new Array<Vector2I>();
        var rand = new RandomNumberGenerator();

        int tries = 0;
        while (foundCenters.Count < count && tries < maxTries)
        {
            tries++;

            // Pick a random center within bounds so the circle fits
            int cx = rand.RandiRange(radius, imageWidth - radius - 1);
            int cy = rand.RandiRange(radius, imageHeight - radius - 1);
            var center = new Vector2I(cx, cy);

            // Optionally, skip if too close to already found centers
            bool tooClose = false;
            foreach (var c in foundCenters)
            {
                if (center.DistanceTo(c) < radius * 2)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose)
                continue;

            if (IsNoiseCircleInRange(center, radius, minValue, maxValue))
            {
                foundCenters.Add(center);
            }
        }

        if (tries >= maxTries)
        {
            Logger.LogError($"Warning: Could not find {count} noise spots in range after {maxTries} tries.", Logger.LogTypeEnum.Framework);
        }

        return foundCenters;
    }
    #endregion
}
