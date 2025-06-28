using UnityEngine;
using System.Collections.Generic;
namespace Eye.Maps.Templates
{
    public class MazeMapHex : GenericMazeMap<HexIndex2D>
    {
        public MazeMapHex(HexIndex2D size, float worldScale = 1) : base(size, new HexIndex2D(0, 0), new HexIndex2D(size.x - 1, size.y - 1), worldScale)
        {
        }

        public override IEnumerable<HexIndex2D> allMapCoords
        {
            get
            {
                for (int x = 0; x < size.x; x++)
                    for (int y = 0; y < size.y; y++)
                        yield return new HexIndex2D(x, y);

            }
        }

        public override Vector3 GetModelSpacePosition(HexIndex2D coord)
        {
            return coord.WorldPosXZPosition(this);
            //return new Vector3(coord.x, coord.y, 0);
        }
        override public Quaternion GetModelSpaceOrientation(HexIndex2D coord)
        {
            return Quaternion.Euler(0, 0, 30);
        }
        // Check if a given coordinate is within the bounds of the maze
        public override bool IsWithinBounds(HexIndex2D coord)
        {
            return coord.x >= 0 && coord.y >= 0 && coord.x < size.x && coord.y < size.y;
        }
        float[] neighborAngles = new float[] { 270, 210, 150, 90, 30, 330 };
        public override Quaternion NeighborBorderOrientation(HexIndex2D coord, int neighborIndex)
        {
            return Quaternion.Euler(0, 0, neighborAngles[neighborIndex]);
        }
    }
}