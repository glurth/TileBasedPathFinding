using UnityEngine;
using Eye.Maps;
// RectangularCoord struct that wraps a Vector2Int
[System.Serializable]
public struct RectangularCoord : ITileCoordinate<RectangularCoord>
{
    public RectangularCoord value { get { return this; } }
    [SerializeField]
    Vector2Int data;
    public RectangularCoord(Vector2Int value)
    {
        data = value;
    }

   // public RectangularCoord value => this;

    public int x => data.x;
    public int y => data.y;

    // Returns a list of neighboring coordinates (up, down, left, right).
    public int NumberOfNeighbors() => 4;

    /// <summary>
    /// 
    /// </summary>
    /// <returns>up,down,right,left in order</returns>
    public RectangularCoord[] GetNeighbors()
    {
        return new RectangularCoord[]
        {
            new RectangularCoord(new Vector2Int(x + 1, y)),
            new RectangularCoord(new Vector2Int(x - 1, y)),
            new RectangularCoord(new Vector2Int(x, y + 1)),
            new RectangularCoord(new Vector2Int(x, y - 1))
        };
    }

    public RectangularCoord GetNeighbor(int neighborIndex)
    {
        var neighbors = GetNeighbors();
        if (neighborIndex >= 0 && neighborIndex < neighbors.Length)
            return neighbors[neighborIndex];
        throw new System.ArgumentOutOfRangeException();
    }

    public float HeuristicDistanceTo(ITileCoordinate<RectangularCoord> end)
    {
        return Vector2Int.Distance(this.data, end.value.data);
    }

    public static bool operator ==(RectangularCoord a, RectangularCoord b)
    {
        return (a.x == b.x && a.y == b.y);
    }
    public static bool operator !=(RectangularCoord a, RectangularCoord b)
    {
        return (a.x != b.x || a.y != b.y);
    }
    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }
        return this == (RectangularCoord)obj;
    }
    public bool Equals(RectangularCoord obj)
    {
        return this == obj;
    }
    public override int GetHashCode()
    {
        return x + (y * 1000000);// + (x * y);
                                 // return ShiftAndWrap(x.GetHashCode(), 2) ^ y.GetHashCode();
    }

    public override string ToString()
    {
        return data.ToString();
    }
}
