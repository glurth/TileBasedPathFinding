using UnityEngine;
using System.Collections.Generic;

public class MazeMapHex : GenericMazeMap<Eye.Maps.Hex.HexIndex2D>
{
    public MazeMapHex(Eye.Maps.Hex.HexIndex2D size, float worldScale = 1) : base(size, new Eye.Maps.Hex.HexIndex2D(0,0), new Eye.Maps.Hex.HexIndex2D(size.x - 1, size.y - 1), worldScale)
    {
    }

    public override IEnumerable<Eye.Maps.Hex.HexIndex2D> allMapCoords
    {
        get
        {
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                    yield return new Eye.Maps.Hex.HexIndex2D(x, y);

        }
    }

    public override Vector3 GetWorldPosition(Eye.Maps.Hex.HexIndex2D coord)
    {
        return coord.WorldPosXZPosition(this);
        //return new Vector3(coord.x, coord.y, 0);
    }

    // Check if a given coordinate is within the bounds of the maze
    public override bool IsWithinBounds(Eye.Maps.Hex.HexIndex2D coord)
    {
        return coord.x >= 0 && coord.y >= 0 && coord.x < size.x && coord.y < size.y;
    }
    float[] neighborAngles = new float[] { 270, 210, 150, 90, 30, 330 };
    public override Quaternion NeighborBorderOrientation(Eye.Maps.Hex.HexIndex2D coord, int neighborIndex)
    {
        return Quaternion.Euler(0,0,neighborAngles[neighborIndex]);
    }
}