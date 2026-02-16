using System;
using System.Collections.Generic;
using UnityEngine;
using EyE.Maps;

namespace EyE.Maps.Templates
{
    /// <summary>
    /// Coordinate type for a radial (concentric ring) maze.
    /// ring : 0..R-1   (0 = center)
    /// sector : 0..S(r)-1  (sector index within that ring)
    ///
    /// Neighbor ordering (6 neighbors to match GenericMazeMap wall arrays):
    /// 0 = same-ring clockwise (s + 1)
    /// 1 = same-ring counter-clockwise (s - 1)
    /// 2 = inner-ring "clockwise" mapped sector
    /// 3 = inner-ring "counter-clockwise" mapped sector
    /// 4 = outer-ring "clockwise" mapped sector
    /// 5 = outer-ring "counter-clockwise" mapped sector
    ///
    /// This implementation fixes wrap / asymmetry issues by mapping sectors using
    /// a nearest-rounding strategy with a deterministic tie-breaker. That makes
    /// mapping consistent across ring boundaries and avoids one-way neighbors
    /// caused by floor/ceil choices around the wrap point.
    /// </summary>
    [Serializable]
    public struct RadialCoord : ITileCoordinate<RadialCoord>
    {
        [SerializeField] int m_ring;
        [SerializeField] int m_sector;
        public static MazeMapRadial mapRef;

        public RadialCoord(int ring, int sector)
        {
            m_ring = ring;
            m_sector = sector;
        }

        public int ring => m_ring;
        public int sector => m_sector;

        public RadialCoord value { get { return this; } }

        public int NumberOfNeighbors()
        {
            if (ring == 0) return mapRef.baseSectors;
            bool outerRingDoubles = (mapRef.SectorsAtRing(ring) < mapRef.SectorsAtRing(ring + 1));
            if (outerRingDoubles) return 5;//two outer ring sectors + inner, left, right
            return 4;//one outer ring sector+ inner, left, right
        }
        
        public RadialCoord[] GetSpatialNeighbors()
        {
            int numNeighbors = NumberOfNeighbors();
            RadialCoord[] n = new RadialCoord[numNeighbors];
            for (int i = 0; i < numNeighbors; i++)
                n[i] = GetSpatialNeighbor(i);
            return n;
        }

        public ITileCoordinateBase GetSpatialNeighborBase(int neighborIndex)
        {
            return GetSpatialNeighbor(neighborIndex);
        }

        public RadialCoord GetSpatialNeighbor(int neighborIndex)
        {
            if (ring == 0)
                return new RadialCoord(1, neighborIndex);

            int ringSectors = mapRef.SectorsAtRing(ring);
            //check if next ring out doubles/has more sectors
            
            

            int circ(int s)
            {
                if (s < 0) s += ringSectors;
                if (s >= ringSectors) s -= ringSectors;
                return s;
            }

            switch (neighborIndex)
            {
                case 0: // same-ring clockwise
                    return new RadialCoord(ring, circ(sector + 1));
                case 1: // same-ring counter-clockwise
                    return new RadialCoord(ring, circ(sector - 1));
                case 2: // inner-ring
                    {
                        if (ring == 1)
                            return new RadialCoord(0, 0);
                        bool innerRingHalves = ring > 1 && (ringSectors > mapRef.SectorsAtRing(ring - 1));
                        int innerSector = sector;
                        if (innerRingHalves) innerSector >>= 1;//= innerSector /2;
                        return new RadialCoord(ring - 1, innerSector);
                    }
                case 3: // outer-ring one of possible one or two
                    {
                        int outerRing = ring + 1;
                        int outerSector = sector;
                        bool outerRingDoubles = (ringSectors < mapRef.SectorsAtRing(ring + 1));
                        if (outerRingDoubles)
                            outerSector <<= 1;//*= 2;
                        return new RadialCoord(outerRing, outerSector);
                    }
                case 4: // outer-ring second of two possible- 
                    {
                        //we know outerRingDoubles must be true for a tile to use this neighbor index
                        int outerRing = ring + 1;
                        int outerSector = (sector<<1) + 1;
                        return new RadialCoord(outerRing, outerSector);
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(neighborIndex));
            }


        }

        // Heuristic: euclidean between tile centers (2D)
        public float HeuristicDistanceTo(ITileCoordinate<RadialCoord> end)
        {
            RadialCoord a = this;
            RadialCoord b = end.value;
            int rDiff = Mathf.Abs(a.ring - b.ring);
            int aDiff = Mathf.Abs(a.sector - b.sector);
            int ringSectors = mapRef.SectorsAtRing(this.ring);
            aDiff = Mathf.Min(aDiff, ringSectors - aDiff);
            return rDiff + aDiff;
        }

        public float AngleInTurns => ((float)sector+0.5f )/ (float)mapRef.SectorsAtRing(ring);
        public Vector2 radialDirection
        {
            get
            {
                return GetRadialDirection(AngleInTurns);
            }
        }
        static public Vector2 GetRadialDirection(float angleInTurns)
        {
                float angleRad = angleInTurns * 2f * Mathf.PI;
                return new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
        }
        public static RadialCoord invalid => new RadialCoord(-1, -1);

        public static bool operator ==(RadialCoord a, RadialCoord b) => (a.ring == b.ring && a.sector == b.sector);
        public static bool operator !=(RadialCoord a, RadialCoord b) => !(a == b);

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType()) return false;
            return this == (RadialCoord)obj;
        }
        public bool Equals(RadialCoord other) => this == other;

        public override int GetHashCode()
        {
            return ring * 1049 + sector;
        }

        public override string ToString()
        {
            return $"(ring:{ring}, sector:{sector})";
        }

        public static int NumTiles(RadialCoord size)
        {
            int count = 0;
            for (int i = 0; i < size.ring; i++)
                count += mapRef.SectorsAtRing(i);
            //count += size.sector;
            return count;
        }
    }
}


