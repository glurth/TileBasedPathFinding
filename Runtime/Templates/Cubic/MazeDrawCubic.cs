using UnityEngine;
namespace Eye.Maps.Templates
{
    public class MazeDrawCubic : MazeDrawGeneric<CubicCoord>
    {

        protected override GenericMazeMap<CubicCoord> CreateMazeMap()
        {
            MazeMapCubic maze = new MazeMapCubic(mazeSize);
            maze.GenerateMaze(null);
            return maze;
        }

        protected override CubicCoord DefaultMazeSize()
        {
            return new CubicCoord(new Vector3Int(10, 10, 10));
        }
    }
}