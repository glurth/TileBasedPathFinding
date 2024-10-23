
namespace Eye.Maps.Templates
{
    public class MazeDrawTri : MazeDrawGeneric<TriangularIndex2D>
    {
        protected override GenericMazeMap<TriangularIndex2D> CreateMazeMap()
        {
            return new MazeMapTri(mazeSize);
        }
        protected override TriangularIndex2D DefaultMazeSize()
        {
            return new TriangularIndex2D(10, 10);
        }
    }
}