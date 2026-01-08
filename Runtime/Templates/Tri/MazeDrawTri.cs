using UnityEngine;
using System.Collections.Generic;
namespace EyE.Maps.Templates
{
    public class MazeDrawTri : MazeDrawGeneric<TriangularIndex2D>
    {
        protected override GenericMazeMap<TriangularIndex2D> CreateMazeMap()
        {
            MazeMapTri maze = new MazeMapTri(mazeSize);
            maze.GenerateMaze();
            return maze;
        }
        protected override TriangularIndex2D DefaultMazeSize()
        {
            return new TriangularIndex2D(10, 10);
        }
        protected /*override*/ int XXNumTouchingWallsAtWallEnd(TriangularIndex2D coord, int wallIndex, bool isRightEnd, out bool isAcute)
        {
            









        //    isAcute = false;
        //    return 0;

            isAcute = false;

            Vector3 GetWallDirection(TriangularIndex2D c, int wIdx)
            {
                Quaternion rot = maze.NeighborBorderOrientation(c, wIdx);
                return rot * Vector3.forward; // adjust if needed
            }

            bool HasWallAtCorner(TriangularIndex2D tri, int corner)
            {
                // This needs to map corner to wall index on tri
                // Because walls are between neighbors, you need to convert corner to wall index
                // For triangle: walls indexed 0,1,2, corners 0,1,2
                // Usually, corner i is between walls (i) and (i-1 mod 3)
                // But you must confirm your maze implementation details.
                // Here, let's assume wall at corner c means wall adjacent to corner c:
                int wallA = corner; // or some mapping
                return maze.Walls[tri][wallA];
            }

            // Get the 3 triangles sharing the corner
            int cornerIndex = isRightEnd ? (wallIndex + 1) % 3 : (wallIndex + 2) % 3;

            TriangularIndex2D tri0 = coord;
            TriangularIndex2D tri1 = coord.GetNeighbor(cornerIndex);
            TriangularIndex2D tri2 = tri1.GetNeighbor((cornerIndex + 1) % 3);

            // Collect walls at this corner
            var walls = new List<(TriangularIndex2D tri, int wallIdx)>();

            if (HasWallAtCorner(tri0, cornerIndex))
                walls.Add((tri0, cornerIndex));

            if (maze.IsWithinBounds(tri1) && HasWallAtCorner(tri1, (cornerIndex + 2) % 3))
                walls.Add((tri1, (cornerIndex + 2) % 3));

            if (maze.IsWithinBounds(tri2) && HasWallAtCorner(tri2, cornerIndex))
                walls.Add((tri2, cornerIndex));

            int count = walls.Count;

            // Check all pairs for acute angles
            for (int i = 0; i < walls.Count; i++)
            {
                for (int j = i + 1; j < walls.Count; j++)
                {
                    Vector3 dirA = GetWallDirection(walls[i].tri, walls[i].wallIdx);
                    Vector3 dirB = GetWallDirection(walls[j].tri, walls[j].wallIdx);
                    float angle = Vector3.Angle(dirA, dirB);
                    if (angle < 90f)
                        isAcute = true;
                }
            }

            return count;
        }

        // Utility: does this triangle have a wall touching the given corner?
        private bool HasWallAtCorner(TriangularIndex2D coord, int cornerIndex)
        {
            if (!maze.Walls.TryGetValue(coord, out bool[] walls)) return false;
            return walls[cornerIndex] || walls[(cornerIndex + 2) % 3];
        }
    }
}