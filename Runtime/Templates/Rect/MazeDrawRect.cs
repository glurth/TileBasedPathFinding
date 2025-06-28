using UnityEngine;
namespace Eye.Maps.Templates
{
    public class MazeDrawRect : MazeDrawGeneric<RectangularCoord>
    {

        protected override GenericMazeMap<RectangularCoord> CreateMazeMap()
        {
            MazeMapRect maze= new MazeMapRect(base.mazeSize);
            maze.GenerateMaze(null);
            return maze;
        }
        protected override RectangularCoord DefaultMazeSize()
        {
            return new RectangularCoord(new Vector2Int(10, 10));
        }
    }
}