using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeDrawCubic : MazeDrawGeneric<CubicCoord>
{

    protected override GenericMazeMap<CubicCoord> CreateMazeMap()
    {
        return new MazeMapCubic(base.mazeSize);
    }
    protected override CubicCoord DefaultMazeSize()
    {
        return new CubicCoord(new Vector3Int(10, 10,10));
    }
}