
namespace Eye.Maps.Templates
{
    public class MazeDrawTri : MazeDrawGeneric<TriangularIndex2D>
    {
        protected override GenericMazeMap<TriangularIndex2D> CreateMazeMap()
        {
            MazeMapTri maze = new MazeMapTri(mazeSize);
            maze.GenerateMaze(null);
            return maze;
        }
        protected override TriangularIndex2D DefaultMazeSize()
        {
            return new TriangularIndex2D(10, 10);
        }
    }
}