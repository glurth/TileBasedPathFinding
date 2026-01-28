using UnityEngine;
using System.Collections.Generic;
namespace EyE.Maps.Templates
{
    public abstract class MazeMap2D<T> : GenericMazeMap<T> where T: ITileCoordinate<T>
    {
        public MazeMap2D(T size, T start, T end, float worldScale = 1f, int numSolutions = 1) : base(size, start, end, worldScale, numSolutions)
        {
            InitOrientationMembers(-Vector3.forward);
        }
        public MazeMap2D(T size, T start, T end, Vector3 mazeNormal, float worldScale = 1f, int numSolutions = 1):base(size,start,end,worldScale,numSolutions)
        {
            InitOrientationMembers(mazeNormal);
        }
        protected Quaternion mazeOrientation;
        public Quaternion MazeOrientation { get => mazeOrientation; }
        protected Vector3 mazeNormal;
        protected Vector3 mapPlaneUp;
        protected Vector3 mapPlaneRight;
        void InitOrientationMembers(Vector3 mazeNormal)
        {
            this.mazeNormal = mazeNormal;
            Vector3 worldUp = (Mathf.Abs(mazeNormal.y) > 0.9f) ? Vector3.forward : Vector3.up;
            mazeOrientation = Quaternion.LookRotation(-mazeNormal,worldUp);//negative normal here means the coords we use will act properly (e.g. x+ to the right), when looking AT the maze.
            mapPlaneUp = mazeOrientation * Vector3.up;
            mapPlaneRight = mazeOrientation * Vector3.right;
        }
        override public Quaternion NeighborBorderOrientation(T coord, int neighborIndex)
        {
            T neighborCoord = coord.GetNeighbor(neighborIndex);
            Vector3 pos = GetModelSpacePosition(coord);
            Vector3 neighborPos = GetModelSpacePosition(neighborCoord);
            Vector3 diff = neighborPos - pos;
            Vector3 dir = Vector3.Cross(mazeNormal, diff.normalized);
            return Quaternion.LookRotation(dir, mazeNormal);
        }

        override public Quaternion GetModelSpaceOrientation(T coord)
        {
            return mazeOrientation;
        }
    }


    public class MazeMapRect : MazeMap2D<RectangularCoord>//  GenericMazeMap<RectangularCoord>
    {

        public MazeMapRect(RectangularCoord size, float worldScale = 1, int numSolutions = 1) :
            base(size,
            start: new RectangularCoord(Vector2Int.zero),
            end: new RectangularCoord(new Vector2Int(size.x - 1, size.y - 1)),
            worldScale, numSolutions)
        {

        }
        public MazeMapRect(RectangularCoord size, Vector3 mazeNormal, float worldScale = 1, int numSolutions = 1) :
            base(size,
            start: new RectangularCoord(Vector2Int.zero),
            end: new RectangularCoord(new Vector2Int(size.x - 1, size.y - 1)),
            mazeNormal, worldScale, numSolutions)
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

        public override Vector3 GetModelSpacePosition(RectangularCoord coord)
        {
            return mapPlaneUp * coord.y + mapPlaneRight * coord.x;
           //return new Vector3(coord.x, coord.y, 0);
           //return new Vector3(coord.x, 0, coord.y);
        }
        public override Vector3 SingleTileModelSpaceOffset()
        {
            return GetModelSpacePosition(new RectangularCoord(1, 1)) - GetModelSpacePosition(new RectangularCoord(0, 0));
        }
        // Check if a given coordinate is within the bounds of the maze
        public override bool IsWithinBounds(RectangularCoord coord)
        {
            return coord.x >= 0 && coord.y >= 0 && coord.x < size.x && coord.y < size.y;
        }
        //float[] neighborAngles = new float[] { 90, 270, 180, 0 }; //must match order of RectangularCoord.GetNeighbor
        //float[] neighborAngles = new float[] { 0, 90, 180, 270 }; //must match order of RectangularCoord.GetNeighbor
        float[] neighborAngles = new float[] { 90, 180, 270,0 }; //must match order of RectangularCoord.GetNeighbor
        public override Quaternion NeighborBorderOrientation(RectangularCoord coord, int neighborIndex)
        {
            return base.NeighborBorderOrientation(coord, neighborIndex);
           // return mazeOrientation* Quaternion.Euler(0, 0, neighborAngles[neighborIndex]);
            

        }
    }
}