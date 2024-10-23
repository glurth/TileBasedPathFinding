using UnityEngine;
using Eye.Maps;

// CubicCoord struct that wraps a Vector3Int
[System.Serializable]
public struct CubicCoord : ITileCoordinate<CubicCoord>
{
    public CubicCoord value { get { return this; } }
    [SerializeField]
    Vector3Int data;

    public CubicCoord(Vector3Int value)
    {
        data = value;
    }

    public int x => data.x;
    public int y => data.y;
    public int z => data.z;

    // Returns a list of neighboring coordinates (6 possible neighbors in a cubic grid).
    public int NumberOfNeighbors() => 6;

    /// <summary>
    /// Returns the neighboring coordinates in the order: +X, -X, +Y, -Y, +Z, -Z
    /// </summary>
    public CubicCoord[] GetNeighbors()
    {
        return new CubicCoord[]
        {
            new CubicCoord(new Vector3Int(x + 1, y, z)), // +X
            new CubicCoord(new Vector3Int(x - 1, y, z)), // -X
            new CubicCoord(new Vector3Int(x, y + 1, z)), // +Y
            new CubicCoord(new Vector3Int(x, y - 1, z)), // -Y
            new CubicCoord(new Vector3Int(x, y, z + 1)), // +Z
            new CubicCoord(new Vector3Int(x, y, z - 1))  // -Z
        };
    }

    public CubicCoord GetNeighbor(int neighborIndex)
    {
        var neighbors = GetNeighbors();
        if (neighborIndex >= 0 && neighborIndex < neighbors.Length)
            return neighbors[neighborIndex];
        throw new System.ArgumentOutOfRangeException();
    }

    public float HeuristicDistanceTo(ITileCoordinate<CubicCoord> end)
    {
        return Vector3Int.Distance(this.data, end.value.data);
    }

    public static bool operator ==(CubicCoord a, CubicCoord b)
    {
        return (a.x == b.x && a.y == b.y && a.z == b.z);
    }

    public static bool operator !=(CubicCoord a, CubicCoord b)
    {
        return (a.x != b.x || a.y != b.y || a.z != b.z);
    }

    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }
        return this == (CubicCoord)obj;
    }

    public bool Equals(CubicCoord obj)
    {
        return this == obj;
    }

    public override int GetHashCode()
    {
        return x + (y * 1000000) + (z * 1000000000);
    }
}
