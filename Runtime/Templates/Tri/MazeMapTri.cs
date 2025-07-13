using System.Collections.Generic;
using UnityEngine;

namespace Eye.Maps.Templates
{
    public class MazeMapTri : GenericMazeMap<TriangularIndex2D>
    {
        public MazeMapTri(TriangularIndex2D size, float worldScale = 1) :
            base(size,
            start: new TriangularIndex2D(0, 0),
            end: new TriangularIndex2D(size.x - 1, size.y - 1),
            worldScale)
        {

        }

        public override IEnumerable<TriangularIndex2D> allMapCoords
        {
            get
            {
                for (int x = 0; x < size.x; x++)
                    for (int y = 0; y < size.y; y++)
                        yield return new TriangularIndex2D(x, y);

            }
        }

//        private static float tileWidth = 2f / Mathf.Sqrt(3f);
        private static readonly float tileHeight = Mathf.Sqrt(3f) / 2f;
        private static readonly float centroidOffset = tileHeight / 3f;

        public override Vector3 GetModelSpacePosition(TriangularIndex2D coord)
        {
            float worldX = coord.x * 0.5f;
            float worldY = coord.y * tileHeight;

            if (!coord.IsPointingUp())
                worldY += centroidOffset;
            else
                worldY += tileHeight - centroidOffset;
            return new Vector3(worldX, worldY, 0);

        }
        public override Vector3 SingleTileModelSpaceOffset()
        {
            return GetModelSpacePosition(new TriangularIndex2D(1, 1)) - GetModelSpacePosition(new TriangularIndex2D(0, 0));
        }

        // Check if a given coordinate is within the bounds of the maze
        public override bool IsWithinBounds(TriangularIndex2D coord)
        {
            return coord.x >= 0 && coord.y >= 0 && coord.x < size.x && coord.y < size.y;
        }
        float[] neighborAnglesUp = new float[] { 0, 120, 240 };
        float[] neighborAnglesDown = new float[] { 0, 240, 120 };

        public override Quaternion GetModelSpaceOrientation(TriangularIndex2D coord)
        {
            if (!coord.IsPointingUp())
                return Quaternion.identity;
            else
                return Quaternion.Euler(0, 0, 180);
        }

        public override Quaternion NeighborBorderOrientation(TriangularIndex2D coord, int neighborIndex)
        {
            float rot;
            if (coord.IsPointingUp())
                rot = neighborAnglesUp[neighborIndex];
            else
                rot = neighborAnglesDown[neighborIndex];
            return Quaternion.Euler(0, 0, rot);
        }
    }

}