using System;
using UnityEngine;
namespace Eye.Maps.Templates
{
    /// <summary>
    /// Represents a triangular tile coordinate implementing ITileCoordinate interface.
    /// </summary>
    [System.Serializable]
    public struct TriangularIndex2D : ITileCoordinate<TriangularIndex2D>
    {
        [SerializeField]
        private int m_x;
        [SerializeField]
        private int m_y;

        public TriangularIndex2D(int x, int y)
        {
            m_x = x;
            m_y = y;
        }

        public int x
        {
            get { return m_x; }
            set { m_x = value; }
        }

        public int y
        {
            get { return m_y; }
            set { m_y = value; }
        }

        public TriangularIndex2D value => this;

        public static readonly TriangularIndex2D invalid = new TriangularIndex2D(-1, -1);
        public static readonly TriangularIndex2D zero = new TriangularIndex2D(0, 0);

        /// <summary>
        /// Neighbor offsets for a triangular grid.
        /// </summary>
        private static readonly TriangularIndex2D[] upNeighborOffsets = new TriangularIndex2D[]
        {
        new TriangularIndex2D(0, 1),
        new TriangularIndex2D(-1,0),
        new TriangularIndex2D(1,0 )
        };
        private static readonly TriangularIndex2D[] downNeighborOffsets = new TriangularIndex2D[]
        {
        new TriangularIndex2D(0, -1),
        new TriangularIndex2D(-1, 0),
        new TriangularIndex2D(1, 0)
        };
        public bool IsPointingUp()
        {
            // Alternate pointing up and down based on row and column
            return (x + y) % 2 == 0;
        }
        public int NumberOfNeighbors()
        {
            return 3;
        }

        public TriangularIndex2D[] GetNeighbors()
        {
            TriangularIndex2D[] neighbors = new TriangularIndex2D[NumberOfNeighbors()];
            TriangularIndex2D[] neighborOffsets= downNeighborOffsets;
            if (IsPointingUp())
                neighborOffsets = upNeighborOffsets;

            for (int i = 0; i < NumberOfNeighbors(); i++)
            {
                neighbors[i] = new TriangularIndex2D(x + neighborOffsets[i].x, y + neighborOffsets[i].y);
            }
            return neighbors;
        }

        public TriangularIndex2D GetNeighbor(int neighborIndex)
        {
            if (neighborIndex < 0 || neighborIndex >= NumberOfNeighbors())
            {
                throw new ArgumentOutOfRangeException(nameof(neighborIndex), $"Index must be between 0 and {NumberOfNeighbors() - 1}.");
            }
            TriangularIndex2D[] neighborOffsets = downNeighborOffsets;
            if (IsPointingUp())
                neighborOffsets = upNeighborOffsets;
            return new TriangularIndex2D(x + neighborOffsets[neighborIndex].x, y + neighborOffsets[neighborIndex].y);
        }



        public float HeuristicDistanceTo(ITileCoordinate<TriangularIndex2D> end)
        {
            int dx = Math.Abs(x - end.value.x);
            int dy = Math.Abs(y - end.value.y);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            return Equals((TriangularIndex2D)obj);
        }

        public bool Equals(TriangularIndex2D other)
        {
            return m_x == other.m_x && m_y == other.m_y;
        }

        public override int GetHashCode()
        {
            return m_x + (m_y * 1000000);
        }

        public static bool operator ==(TriangularIndex2D a, TriangularIndex2D b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(TriangularIndex2D a, TriangularIndex2D b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            return $"[{x}, {y}]";
        }
    }
}