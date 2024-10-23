using UnityEngine;
using System.Collections.Generic;
namespace Eye.Maps.Templates
{
    public class MazeMapRect : GenericMazeMap<RectangularCoord>
    {
        public MazeMapRect(RectangularCoord size, float worldScale = 1) :
            base(size,
            start: new RectangularCoord(Vector2Int.zero),
            end: new RectangularCoord(new Vector2Int(size.x - 1, size.y - 1)),
            worldScale)
        {
        }

        public override IEnumerable<RectangularCoord> allMapCoords
        {
            get
            {
                for (int x = 0; x < size.x; x++)
                    for (int y = 0; y < size.y; y++)
                        yield return new RectangularCoord(new Vector2Int(x, y));

            }
        }

        public override Vector3 GetWorldPosition(RectangularCoord coord)
        {
            return new Vector3(coord.x, coord.y, 0);
        }

        // Check if a given coordinate is within the bounds of the maze
        public override bool IsWithinBounds(RectangularCoord coord)
        {
            return coord.x >= 0 && coord.y >= 0 && coord.x < size.x && coord.y < size.y;
        }
        float[] neighborAngles = new float[] { 90, 270, 180, 0 }; //must match order of RectangularCoord.GetNeighbor
        public override Quaternion NeighborBorderOrientation(RectangularCoord coord, int neighborIndex)
        {
            return Quaternion.Euler(0, 0, neighborAngles[neighborIndex]);
        }
    }
}