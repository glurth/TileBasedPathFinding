using UnityEngine;

namespace EyE.Maps.Templates
{
    /// <summary>
    /// MazeDraw implementation to visualize a radial maze.
    /// Default configuration: 6 base sectors, linear increase of 1 sector per ring, 6 rings.
    /// </summary>
    public class MazeDrawRadial : MazeDrawGeneric<RadialCoord>
    {
        protected override GenericMazeMap<RadialCoord> CreateMazeMap()
        {
            // mazeSize for RadialCoord encodes (rings, baseSectors) in its fields:
            // mazeSize.ring = desired number of rings
            // mazeSize.sector = desired baseSectors
            int rings = Mathf.Max(1, mazeSize.ring);
            int baseSectors = 6;
            MazeMapRadial maze = new MazeMapRadial(rings, baseSectors, worldScale: 1f);
            maze.GenerateMaze();
            return maze;
        }

        protected override RadialCoord DefaultMazeSize()
        {
            // default: 6 rings, base 6 sectors
            return new RadialCoord(6, 6);
        }
    }
}