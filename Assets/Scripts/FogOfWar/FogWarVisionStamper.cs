using UnityEngine;

/// <summary>CPU-штампы конуса и диска на R8-текстуры тумана.</summary>
public static class FogWarVisionStamper
{
    public static void StampDisk(
        byte[] explored,
        byte[] visible,
        int resolution,
        Vector2 worldMinXZ,
        Vector2 worldMaxXZ,
        Vector3 worldCenter,
        float radiusMeters,
        bool softEdges,
        int featherPixels)
    {
        if (radiusMeters <= 0f) return;
        int cx;
        int cy;
        if (!WorldToCell(worldCenter, worldMinXZ, worldMaxXZ, resolution, out cx, out cy))
            return;

        float cellSizeX = (worldMaxXZ.x - worldMinXZ.x) / resolution;
        float cellSizeZ = (worldMaxXZ.y - worldMinXZ.y) / resolution;
        float cellRadius = Mathf.Max(cellSizeX, cellSizeZ);
        int rCells = Mathf.CeilToInt(radiusMeters / Mathf.Max(0.001f, cellRadius)) + featherPixels + 1;

        int minX = Mathf.Max(0, cx - rCells);
        int maxX = Mathf.Min(resolution - 1, cx + rCells);
        int minY = Mathf.Max(0, cy - rCells);
        int maxY = Mathf.Min(resolution - 1, cy + rCells);

        float rSq = radiusMeters * radiusMeters;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector3 cellWorld = CellToWorld(x, y, worldMinXZ, worldMaxXZ, resolution);
                float dx = cellWorld.x - worldCenter.x;
                float dz = cellWorld.z - worldCenter.z;
                float distSq = dx * dx + dz * dz;
                if (distSq > rSq) continue;

                byte value = 255;
                if (softEdges && featherPixels > 0)
                {
                    float dist = Mathf.Sqrt(distSq);
                    float edge = radiusMeters - dist;
                    float featherWorld = featherPixels * cellRadius;
                    if (edge < featherWorld)
                        value = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * (edge / featherWorld)), 1, 255);
                }

                int idx = x + y * resolution;
                explored[idx] = (byte)Mathf.Max(explored[idx], value);
                visible[idx] = (byte)Mathf.Max(visible[idx], value);
            }
        }
    }

    public static void StampCone(
        byte[] explored,
        byte[] visible,
        int resolution,
        Vector2 worldMinXZ,
        Vector2 worldMaxXZ,
        Vector3 worldOrigin,
        Vector3 worldForward,
        float rangeMeters,
        float fovDegrees,
        bool softEdges,
        int featherPixels)
    {
        if (rangeMeters <= 0f) return;

        worldForward.y = 0f;
        if (worldForward.sqrMagnitude < 0.0001f)
            worldForward = Vector3.forward;
        worldForward.Normalize();

        int ox;
        int oy;
        if (!WorldToCell(worldOrigin, worldMinXZ, worldMaxXZ, resolution, out ox, out oy))
            return;

        float cellSizeX = (worldMaxXZ.x - worldMinXZ.x) / resolution;
        float cellSizeZ = (worldMaxXZ.y - worldMinXZ.y) / resolution;
        float cellRadius = Mathf.Max(cellSizeX, cellSizeZ);
        int rCells = Mathf.CeilToInt(rangeMeters / Mathf.Max(0.001f, cellRadius)) + featherPixels + 1;

        int minX = Mathf.Max(0, ox - rCells);
        int maxX = Mathf.Min(resolution - 1, ox + rCells);
        int minY = Mathf.Max(0, oy - rCells);
        int maxY = Mathf.Min(resolution - 1, oy + rCells);

        float halfFov = Mathf.Max(1f, fovDegrees) * 0.5f;
        float rangeSq = rangeMeters * rangeMeters;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector3 cellWorld = CellToWorld(x, y, worldMinXZ, worldMaxXZ, resolution);
                Vector3 to = cellWorld - worldOrigin;
                to.y = 0f;
                float distSq = to.sqrMagnitude;
                if (distSq < 0.0001f || distSq > rangeSq) continue;

                Vector3 dir = to.normalized;
                float angle = Vector3.Angle(worldForward, dir);
                if (angle > halfFov) continue;

                byte value = 255;
                if (softEdges && featherPixels > 0)
                {
                    float dist = Mathf.Sqrt(distSq);
                    float rangeFade = rangeMeters - dist;
                    float angleFade = halfFov - angle;
                    float featherWorld = featherPixels * cellRadius;
                    float fade = Mathf.Min(
                        rangeFade < featherWorld ? rangeFade / featherWorld : 1f,
                        angleFade < (halfFov * 0.15f) ? angleFade / (halfFov * 0.15f) : 1f);
                    value = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * fade), 1, 255);
                }

                int idx = x + y * resolution;
                explored[idx] = (byte)Mathf.Max(explored[idx], value);
                visible[idx] = (byte)Mathf.Max(visible[idx], value);
            }
        }
    }

    public static bool WorldToCell(
        Vector3 worldPos,
        Vector2 worldMinXZ,
        Vector2 worldMaxXZ,
        int resolution,
        out int cellX,
        out int cellY)
    {
        cellX = 0;
        cellY = 0;
        float nx = Mathf.InverseLerp(worldMinXZ.x, worldMaxXZ.x, worldPos.x);
        float nz = Mathf.InverseLerp(worldMinXZ.y, worldMaxXZ.y, worldPos.z);
        if (nx < 0f || nx > 1f || nz < 0f || nz > 1f)
            return false;

        cellX = Mathf.Clamp(Mathf.FloorToInt(nx * resolution), 0, resolution - 1);
        cellY = Mathf.Clamp(Mathf.FloorToInt(nz * resolution), 0, resolution - 1);
        return true;
    }

    public static Vector3 CellToWorld(
        int cellX,
        int cellY,
        Vector2 worldMinXZ,
        Vector2 worldMaxXZ,
        int resolution)
    {
        float nx = (cellX + 0.5f) / resolution;
        float nz = (cellY + 0.5f) / resolution;
        float x = Mathf.Lerp(worldMinXZ.x, worldMaxXZ.x, nx);
        float z = Mathf.Lerp(worldMinXZ.y, worldMaxXZ.y, nz);
        return new Vector3(x, 0f, z);
    }
}
