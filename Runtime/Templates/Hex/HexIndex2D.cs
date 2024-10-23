using System.Collections.Generic;
using UnityEngine;

namespace Eye.Maps.Templates
{
    static class CommonConstants
    {
        static public readonly float sqrtThree = Mathf.Sqrt(3);
        static public readonly float oneOverSqrtThree = 1f / Mathf.Sqrt(3);
    }

    [System.Serializable]
    public struct HexIndex2D : ITileCoordinate<HexIndex2D>
    {

        public HexIndex2D value { get { return this; } }

        /// <summary>
        /// A wrapper around <see cref="Vector2Int"/>.
        /// </summary>
        internal struct AxialCoord
        {
            // A struct's fields should not be exposed
            private Vector2Int value;

            // As we are using implicit conversions we can keep the constructor private
            private AxialCoord(Vector2Int value)
            {
                this.value = value;
            }
            public AxialCoord(int x, int y)
            {
                this.value = new Vector2Int(x, y);
            }
            /// <summary>
            /// Implicitly converts a <see cref="Vector2Int"/> to a Record.
            /// </summary>
            /// <param name="value">The <see cref="Vector2Int"/> to convert.</param>
            /// <returns>A new Record with the specified value.</returns>
            public static implicit operator AxialCoord(Vector2Int value)
            {
                return new AxialCoord(value);
            }
            /// <summary>
            /// Implicitly converts a Record to a <see cref="Vector2Int"/>.
            /// </summary>
            /// <param name="record">The Record to convert.</param>
            /// <returns>
            /// A <see cref="Vector2Int"/> that is the specified Record's value.
            /// </returns>
            public static implicit operator Vector2Int(AxialCoord coord)
            {
                return coord.value;
            }
        }
        internal AxialCoord ToAxialCoord()
        {
            int cx = x - (y - (y & 1)) / 2;
            int cy = y;
            return new AxialCoord(x, y);
        }
        internal static HexIndex2D FromAxialCoord(AxialCoord coord)
        {
            Vector2Int v2 = coord;
            int cx = v2.x + (v2.y - (v2.y & 1)) / 2;
            int cy = v2.y;
            return new HexIndex2D(cx, cy);
        }

        public readonly static HexIndex2D invalid = new HexIndex2D(-1, -1);//{ get { return new Index2D(-1, -1); } }
        public readonly static HexIndex2D one = new HexIndex2D(1, 1);
        public readonly static HexIndex2D zero = new HexIndex2D(0, 0);
        [SerializeField]
        int m_x;
        [SerializeField]
        int m_y;

        public HexIndex2D(int _x, int _y)
        {
            m_x = _x;
            m_y = _y;
        }
        public int x { get { return m_x; } set { m_x = value; } }
        public int y { get { return m_y; } set { m_y = value; } }

        public static HexIndex2D maxElement(IMap<HexIndex2D> map) { return new HexIndex2D(map.size.x - 1, map.size.y - 1); }
        public int Index(IMap<HexIndex2D> map)
        {
            return x + (y * map.size.x);
        }

        public static implicit operator HexIndex2D(Vector2Int i)
        {
            return new HexIndex2D(i.x, i.y);
        }
        public static HexIndex2D operator *(HexIndex2D a, int m)
        {
            return HexIndex2D.FromCubedCoord(a.ToCubedCoords() * m);
            //return new HexIndex2D(a.x * m, a.y * m);
        }
        public static HexIndex2D operator *(HexIndex2D a, HexIndex2D m)
        {
            return new HexIndex2D(a.x * m.x, a.y * m.y);
        }
        public static HexIndex2D operator *(HexIndex2D a, Vector2Int m)
        {
            return new HexIndex2D(a.x * m.x, a.y * m.y);
        }
        public static explicit operator Vector2Int(HexIndex2D i) => new Vector2Int(i.x, i.y);
        public static explicit operator Vector2(HexIndex2D i) => new Vector2(i.x, i.y);

        public static HexIndex2D operator *(HexIndex2D i, float scale)
        {

            Vector3Int cubedCoordSize = i.ToCubedCoords();
            cubedCoordSize = Scale(cubedCoordSize, scale);
            i.SetFromCubedCoord(cubedCoordSize);

            //   i.x = (int)(i.x * scale);
            //   i.y = (int)(i.y * scale);
            return i;
        }
        public static HexIndex2D operator +(HexIndex2D i, HexIndex2D o)
        {
            return HexIndex2D.FromCubedCoord(i.ToCubedCoords() + o.ToCubedCoords());
            //i.x += o.x;
            //i.y += o.y;
            //return i;
        }
        public static HexIndex2D operator -(HexIndex2D i, HexIndex2D o)
        {
            return HexIndex2D.FromCubedCoord(i.ToCubedCoords() - o.ToCubedCoords());
            //i.x -= o.x;
            //i.y -= o.y;
            //return i;
        }
        public static Vector3Int Scale(Vector3Int vec, float scale)
        {
            vec.x = (int)((float)vec.x * scale);
            vec.y = (int)((float)vec.y * scale);
            vec.z = (int)((float)vec.z * scale);
            return vec;
        }

        public static bool operator ==(HexIndex2D a, HexIndex2D b)
        {
            return (a.x == b.x && a.y == b.y);
        }
        public static bool operator !=(HexIndex2D a, HexIndex2D b)
        {
            return (a.x != b.x || a.y != b.y);
        }
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            return this == (HexIndex2D)obj;
        }
        public bool Equals(HexIndex2D obj)
        {
            return this == obj;
        }
        public override int GetHashCode()
        {
            return x + (y * 1000000);// + (x * y);
                                     // return ShiftAndWrap(x.GetHashCode(), 2) ^ y.GetHashCode();
        }

        private int ShiftAndWrap(int value, int positions)
        {
            positions = positions & 0x1F;

            // Save the existing bit pattern, but interpret it as an unsigned integer.
            uint number = System.BitConverter.ToUInt32(System.BitConverter.GetBytes(value), 0);
            // Preserve the bits to be discarded.
            uint wrapped = number >> (32 - positions);
            // Shift and wrap the discarded bits.
            return System.BitConverter.ToInt32(System.BitConverter.GetBytes((number << positions) | wrapped), 0);
        }


        public long Area { get { return x * y; } }
        public override string ToString()
        {
            return "[" + x + " X " + y + "]";
            //return x + "," + y + ": (int)" + ((int)this).ToString();
        }
        public HexIndex2D DisplayIndex()
        {
            int dx = x;
            int dy = y;
            int one = dx % 2;

            dx /= 2;
            dy *= 2;
            dy += one;
            return new HexIndex2D(dx, dy);
        }
        public Vector3Int ToCubedCoords()
        {
            int cx = x - (y - (y & 1)) / 2;
            int cy = y;
            int cz = -cx - cy;
            return new Vector3Int(cx, cy, cz);
        }
        static public HexIndex2D FromCubedCoord(Vector3Int cubedCoord)
        {
            HexIndex2D hexIndex = new HexIndex2D(0, 0);
            hexIndex.SetFromCubedCoord(cubedCoord);
            return hexIndex;
            /*int cx = cubedCoord.x + (cubedCoord.y - (cubedCoord.y & 1)) / 2;
            int cy = cubedCoord.y;
            return new HexIndex2D(cx, cy);
            */
        }
        public void SetFromCubedCoord(Vector3Int cubedCoord)
        {
            x = cubedCoord.x + (cubedCoord.y - (cubedCoord.y & 1)) / 2;
            y = cubedCoord.y;
        }
        static public int DistanceBetweenHexCubedCoords(Vector3Int a, Vector3Int b)
        {
            int steps = (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z)) / 2;
            // Debug.Log("Distance between cubed coords:" + a + " and: " + b + " is:" + steps);
            return steps;
        }
        public int StepDistance(HexIndex2D endHexCoord)
        {
            return DistanceBetweenHexCubedCoords(ToCubedCoords(), endHexCoord.ToCubedCoords());
        }

        static public HexIndex2D[] neighborIndexOffsetsOddY = new HexIndex2D[]
    {
        new HexIndex2D(1, 0),
        new HexIndex2D(1, -1),
        new HexIndex2D(0, -1),
        new HexIndex2D(-1, 0),
        new HexIndex2D(0, +1),
        new HexIndex2D(+1, +1),

    };
        static public HexIndex2D[] neighborIndexOffsetsEvenY = new HexIndex2D[]
        {
        new HexIndex2D(1, 0),
        new HexIndex2D(0, -1),
        new HexIndex2D(-1, -1),
        new HexIndex2D(-1, 0),
        new HexIndex2D(-1, 1),
        new HexIndex2D(0, 1),
        };
        static public Vector3Int[] cubedNeighborCoords = new Vector3Int[]
        {
        new HexIndex2D(1, 0).ToCubedCoords(),
        new HexIndex2D(0, -1).ToCubedCoords(),
        new HexIndex2D(-1, -1).ToCubedCoords(),
        new HexIndex2D(-1, 0).ToCubedCoords(),
        new HexIndex2D(-1, 1).ToCubedCoords(),
        new HexIndex2D(0, 1).ToCubedCoords(),
        };
        static public Dictionary<Vector3Int, int> neighborIndex = new Dictionary<Vector3Int, int>
    {
        { cubedNeighborCoords[0],0 },
        { cubedNeighborCoords[1],1 },
        { cubedNeighborCoords[2],2 },
        { cubedNeighborCoords[3],3 },
        { cubedNeighborCoords[4],4 },
        { cubedNeighborCoords[5],5 },
    };
        public HexIndex2D[] neighborIndexOffsets => (this.y & 0x01) == 0 ? neighborIndexOffsetsEvenY : neighborIndexOffsetsOddY;

        public HexIndex2D[] GetAllNeighbors(HexIndex2D[] allNeighbors = null)
        {
            if (allNeighbors == null)
                allNeighbors = new HexIndex2D[6];// (this.x, this.y);
            for (int i = 0; i < 6; i++)
            {
                HexIndex2D oddEvenOffset = neighborIndexOffsets[i];
                allNeighbors[i] = new HexIndex2D(this.x + oddEvenOffset.x, this.y + oddEvenOffset.y);
            }
            return allNeighbors;

        }
        public IEnumerable<HexIndex2D> AllNeighbors()
        {
            for (int i = 0; i < 6; i++)
            {
                HexIndex2D oddEvenOffset = neighborIndexOffsets[i];
                yield return new HexIndex2D(this.x + oddEvenOffset.x, this.y + oddEvenOffset.y);
                // yield return this + neighborIndexOffsets[i];
            }
        }
        static public int InverseDirectionIndex(int dir)
        {
            return SubtractDirection(dir, 3);
        }
        static private int SubtractDirection(int a, int b)
        {
            a -= b;
            if (a < 0) a += 6;
            return a;
        }
        public HexIndex2D[] GetNeighbors()
        {
            return GetAllNeighbors();
        }
        public HexIndex2D GetNeighbor(int edge)//edge zero based 0-5
        {
            HexIndex2D oddEvenOffet = neighborIndexOffsets[edge];

            return new HexIndex2D(x + oddEvenOffet.x, y + oddEvenOffet.y);
            //return this + zero.neighborIndexOffsets[edge];


            //return HexIndex2D.FromCubedCoord(this.ToCubedCoords() + zero.neighborIndexOffsets[edge].ToCubedCoords());
            //return this + neighborIndexOffsets[edge];
            //used during init:   Vector3 v = new Vector3(Mathf.Sin(i * radiansInc), 0, Mathf.Cos(i * radiansInc));
            //
            //
            //          4  5
            //       3        0
            //          2  1 
            //
            //  return GetAllNeighbors()[edge];
            /*Index2D neighbor = new Index2D(this.x, this.y);
            if (edge == 5) { neighbor.x += 1; if ((0x01 & this.x) != 0) neighbor.y += 1; }
            if (edge == 0) neighbor.x += 2;
            if (edge == 1) { neighbor.x += 1; if ((0x01 & this.x) == 0) neighbor.y -= 1; }
            if (edge == 2) { neighbor.x -= 1; if ((0x01 & this.x) == 0) neighbor.y -= 1; }
            if (edge == 3) neighbor.x -= 2;
            if (edge == 4) { neighbor.x -= 1; if ((0x01 & this.x) != 0) neighbor.y += 1; }
            return neighbor;*/
        }
        public HexIndex2D GetNeighborInverse(int edge)
        {
            //edge = edge - 3;
            //if (edge < 0) edge += 6;
            return GetNeighbor(InverseDirectionIndex(edge));
        }
        public int DirectionTo(HexIndex2D target)
        {
            Vector3Int cubedOffset = target.ToCubedCoords() - this.ToCubedCoords();
            int nIndex;
            if (neighborIndex.TryGetValue(cubedOffset, out nIndex))
                return nIndex;

            Debug.LogError("Unable to Find neighbor index of: " + target + "  relative to " + ToString());
            return -1;
        }


        static Vector2 halfAPixel(IMap<HexIndex2D> map) { return new Vector2((float)0.5f / (float)map.size.x, (float)0.5f / (float)map.size.y); }
        public Vector2 AsLookupUV(IMap<HexIndex2D> map)
        {
            return HexIndex2D.AsUVLookup(x, y, map);
        }

        public void ClipToBounds(IMap<HexIndex2D> map)
        {
            HexIndex2D size = (HexIndex2D)map.size;
            if (x < 0) x = 0;
            if (x >= map.size.x) x = map.size.x - 1;
            if (y < 0) y = 0;
            if (y >= map.size.y) y = map.size.y - 1;
        }
        public bool InBounds(IMap<HexIndex2D> map)
        {
            return (m_x >= 0 && m_x < map.size.x && m_y >= 0 && m_y < map.size.y);
        }



        static Vector3Int RoundCubedFloatToCubed(float fx, float fy, float fz)
        {
            // UnityEngine.Profiling.Profiler.BeginSample("mathf round");
            int x = Mathf.RoundToInt(fx);
            int y = Mathf.RoundToInt(fy);
            int z = Mathf.RoundToInt(fz);
            //  UnityEngine.Profiling.Profiler.EndSample();
            //  UnityEngine.Profiling.Profiler.BeginSample("hex offsets");
            float xDiff = (x - fx);
            if (xDiff < 0) xDiff *= -1;
            float yDiff = (y - fy);
            if (yDiff < 0) yDiff *= -1;
            float zDiff = (z - fz);
            if (zDiff < 0) zDiff *= -1;

            if (xDiff > yDiff && xDiff > zDiff)
                x = -y - z;
            else if (yDiff > zDiff)
                y = -x - z;
            else
                z = -x - y;
            // UnityEngine.Profiling.Profiler.EndSample();
            return new Vector3Int(x, y, z);
        }

        static HexIndex2D RoundCubedFloat(float fx, float fy, float fz)
        {
            // UnityEngine.Profiling.Profiler.BeginSample("mathf round");
            int x = Mathf.RoundToInt(fx);
            int y = Mathf.RoundToInt(fy);
            int z = Mathf.RoundToInt(fz);
            //  UnityEngine.Profiling.Profiler.EndSample();
            //  UnityEngine.Profiling.Profiler.BeginSample("hex offsets");
            float xDiff = (x - fx);
            if (xDiff < 0) xDiff *= -1;
            float yDiff = (y - fy);
            if (yDiff < 0) yDiff *= -1;
            float zDiff = (z - fz);
            if (zDiff < 0) zDiff *= -1;

            if (xDiff > yDiff && xDiff > zDiff)
                x = -y - z;
            else if (yDiff > zDiff)
                y = -x - z;
            else
                z = -x - y;
            // UnityEngine.Profiling.Profiler.EndSample();
            return HexIndex2D.FromCubedCoord(new Vector3Int(x, y, z));
        }

        public static IEnumerable<HexIndex2D> LineFromTo(HexIndex2D from, HexIndex2D to)
        {
            // UnityEngine.Profiling.Profiler.BeginSample("lineFromTo: init");
            Vector3Int fromCubed = from.ToCubedCoords();
            Vector3Int toCubed = to.ToCubedCoords();
            return LineFromToCubedCoords(fromCubed, toCubed);
        }
        public static IEnumerable<HexIndex2D> LineFromToCubedCoords(Vector3Int fromCubed, Vector3Int toCubed)
        {
            // UnityEngine.Profiling.Profiler.BeginSample("lineFromTo: init");

            int steps = DistanceBetweenHexCubedCoords(fromCubed, toCubed);
            float inc = 1f / (float)steps;
            Vector3 cubedInc = ((Vector3)toCubed - (Vector3)fromCubed) * inc;
            Vector3 cubedStepCoord = fromCubed;

            // UnityEngine.Profiling.Profiler.EndSample();

            for (int i = 0; i <= steps; i++)
            {
                // UnityEngine.Profiling.Profiler.BeginSample("lineFromTo: round and inc");
                HexIndex2D returnValue = RoundCubedFloat(cubedStepCoord.x, cubedStepCoord.y, cubedStepCoord.z);
                cubedStepCoord += cubedInc;
                //  UnityEngine.Profiling.Profiler.EndSample();
                yield return returnValue;// RoundCubedFloat(cubedStepCoord.x, cubedStepCoord.y, cubedStepCoord.z);
            }
        }

        //valid x,y values -1 to +1
        static private Vector2 SingleTileWorldOffset(int x, int y, IMap<HexIndex2D> map)
        {
            float xf = x * Mathf.Sqrt(3) * 0.5f * map.worldScale;
            float yf = 0;
            if (y == 0) xf *= 2f;
            else yf = y * 1.5f * map.worldScale;

            return new Vector2(xf, yf);
        }
        public Vector2 SingleTileWorldOffset(IMap<HexIndex2D> map)
        {
            return SingleTileWorldOffset(x, y, map);
        }

        static private Vector2 WorldPosXZPositionAt(int x, int y, IMap<HexIndex2D> map)
        {
            
            float t = CommonConstants.sqrtThree;
            float xf = x * t * 1f * map.worldScale;
            float yf = y * 1.5f * map.worldScale;
            if ((y & 0x01) != 0)
                xf += t * .5f * map.worldScale;
            //  Debug.Log("world pos of: [" + x + "," + y + "] is (using static sqrt3)" + new Vector2(xf, yf) + "   and using computed:" + WorldPosXZPositionAtOriginalVersion(x, y));
            return new Vector2(xf, yf);
        }
        static private Vector2 AsUVLookup(int x, int y, IMap<HexIndex2D> map)
        {
            return new Vector2((float)x / (float)(map.size.x - 1), (float)y / (float)(map.size.y - 1));
        }
        //functions that use mapscale
        public Vector2 WorldPosXZPosition(IMap<HexIndex2D> map)
        {
            return WorldPosXZPositionAt(x, y, map);
        }
        public Vector2 AsFractionOfWorldMap(IMap<HexIndex2D> map)
        {
            // return AsUVLookup(x, y);
            Vector2 worldPos = WorldPosXZPosition(map);
            Vector2 worldMapSize = new HexIndex2D(map.size.x - 1, map.size.y - 1).WorldPosXZPosition(map);
            Vector2 worldMapSizeReciprocal = Vector2.one / worldMapSize;
            return worldPos * worldMapSizeReciprocal + worldMapSizeReciprocal;// / worldMapSize;
        }

        public Vector3 WorldPosPositionNoHeight(IMap<HexIndex2D> map)
        {
            Vector2 xz = WorldPosXZPosition(map);
            return new Vector3(xz.x, 0, xz.y);
        }


        /*
        static void IndexCoordTest(IMap<HexIndex2D> map)
        {
            bool failed = false;
            foreach (HexIndex2D index in map.allMapCoords)//  HexIndex2D.allMapCoords(map))
            {
                Vector2 worldPos = index.WorldPosXZPosition(map);
                HexIndex2D backToIndex = map.GetHexIndexAtWorldPosition(worldPos);//hexmap extension function
                Debug.Log(
                    "index: " + index +
                    " position: " + worldPos +
                    " backToIndex: " + backToIndex +
                    " success: " + (backToIndex == index));
                if (backToIndex != index)
                    failed = true;

            }
            Debug.Log("Index to word and back test failed: " + failed);
        }*/

        public int NumberOfNeighbors()
        {
            return 6;
        }

        public float HeuristicDistanceTo(ITileCoordinate<HexIndex2D> end)
        {
            return StepDistance(end.value);
        }
    }

}