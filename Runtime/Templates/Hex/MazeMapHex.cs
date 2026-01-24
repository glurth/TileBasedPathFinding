using UnityEngine;
using System.Collections.Generic;
namespace EyE.Maps.Templates
{
    public class MazeMapHex : MazeMap2D<HexIndex2D>// GenericMazeMap<HexIndex2D>
    {
        public MazeMapHex(HexIndex2D size, float worldScale = 1, int numSolutions = 1) : base(size, new HexIndex2D(0, 0), new HexIndex2D(size.x - 1, size.y - 1), worldScale, numSolutions)
        {
        }
        public MazeMapHex(HexIndex2D size, Vector3 mazeNormal, float worldScale = 1, int numSolutions = 1) : base(size, new HexIndex2D(0, 0), new HexIndex2D(size.x - 1, size.y - 1), mazeNormal, worldScale, numSolutions)
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
            float t = CommonConstants.sqrtThree;
            float xf = coord.x * t * 1f;
            float yf = coord.y * 1.5f;
            if ((coord.y & 0x01) != 0)
                xf += t * .5f;
            //  Debug.Log("world pos of: [" + x + "," + y + "] is (using static sqrt3)" + new Vector2(xf, yf) + "   and using computed:" + WorldPosXZPositionAtOriginalVersion(x, y));
            return mapPlaneUp * yf + mapPlaneRight * xf;
            
        }
        public override Vector3 SingleTileModelSpaceOffset()
        {
            return GetModelSpacePosition(new HexIndex2D(1, 1)) - GetModelSpacePosition(new HexIndex2D(0, 0));
        }
        override public Quaternion GetModelSpaceOrientation(HexIndex2D coord)
        {
            return mazeOrientation * Quaternion.Euler(0, 0, 30);
        }
        // Check if a given coordinate is within the bounds of the maze
        public override bool IsWithinBounds(HexIndex2D coord)
        {
            return coord.x >= 0 && coord.y >= 0 && coord.x < size.x && coord.y < size.y;
        }
        float[] neighborAngles = new float[] { 270, 210, 150, 90, 30, 330 };

        public override Quaternion NeighborBorderOrientation(HexIndex2D coord, int neighborIndex)
        {

            return mazeOrientation * Quaternion.Euler(0, 0, 30);// neighborAngles[5-neighborIndex]);
        }
    }
}