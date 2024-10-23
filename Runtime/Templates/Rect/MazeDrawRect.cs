using UnityEngine;
namespace Eye.Maps.Templates
{
    public class MazeDrawRect : MazeDrawGeneric<RectangularCoord>
    {

        protected override GenericMazeMap<RectangularCoord> CreateMazeMap()
        {
            return new MazeMapRect(base.mazeSize);
        }
        protected override RectangularCoord DefaultMazeSize()
        {
            return new RectangularCoord(new Vector2Int(10, 10));
        }
    }
}