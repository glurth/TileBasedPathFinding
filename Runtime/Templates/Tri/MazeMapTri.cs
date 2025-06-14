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

        private static float tileWidth = 2f / Mathf.Sqrt(3f);
        public override Vector3 GetModelSpacePosition(TriangularIndex2D coord)
        {

            // Calculate the width and height of an equilateral triangle
            float halfWidth = tileWidth / 2f;
            float centroidHeight = 1f / 3f;

            // Compute the horizontal coordinate (x offset)
            float worldX = coord.x * halfWidth;

            // Compute the vertical coordinate (y offset)
            float worldY;

            // Adjust for the upward or downward orientation of the triangle
            if (!coord.IsPointingUp())
            {
                // For downward-pointing triangles, shift x to the right by half width
                worldY = (coord.y) + centroidHeight;
            }
            else
                worldY = ((coord.y + 1)) - centroidHeight;
            // Return the computed position
            return new Vector3(worldX, worldY, 0);
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
            float rot;
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