using UnityEngine;
using System.Collections.Generic;
namespace Eye.Maps.Templates
{
    public class MazeMapCubic : GenericMazeMap<CubicCoord>
    {
        public MazeMapCubic(CubicCoord size, float worldScale = 1) :
            base(size,
            start: new CubicCoord(Vector3Int.zero),
            end: new CubicCoord(new Vector3Int(size.x - 1, size.y - 1, size.z - 1)),
            worldScale)
        {
        }

        public override IEnumerable<CubicCoord> allMapCoords
        {
            get
            {
                for (int x = 0; x < size.x; x++)
                    for (int y = 0; y < size.y; y++)
                        for (int z = 0; z < size.z; z++)
                            yield return new CubicCoord(new Vector3Int(x, y, z));
            }
        }

        public override Vector3 GetModelSpacePosition(CubicCoord coord)
        {
            return new Vector3(coord.x, coord.y, coord.z);
        }

        // Check if a given coordinate is within the bounds of the maze
        public override bool IsWithinBounds(CubicCoord coord)
        {
            return coord.x >= 0 && coord.y >= 0 && coord.z >= 0 &&
                   coord.x < size.x && coord.y < size.y && coord.z < size.z;
        }

        // Angles corresponding to neighboring coordinates in cubic space
        Quaternion[] neighborBorderOrintation = new Quaternion[]
        {
        Quaternion.Euler(0,0,90),
        Quaternion.Euler(0,0,270),
        Quaternion.Euler(0,0,0),
        Quaternion.Euler(0,0,180),
        Quaternion.Euler(90,0,0),
        Quaternion.Euler(270,0,0)};

        public override Quaternion NeighborBorderOrientation(CubicCoord coord, int neighborIndex)
        {
            return neighborBorderOrintation[neighborIndex];
        }
    }
}