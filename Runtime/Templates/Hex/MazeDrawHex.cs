
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Eye.Maps.Hex;
public class MazeDrawHex : MazeDrawGeneric<HexIndex2D>
{

    protected override GenericMazeMap<HexIndex2D> CreateMazeMap()
    {
        return new MazeMapHex(mazeSize);
    }
    protected override HexIndex2D DefaultMazeSize()
    {
        return new HexIndex2D(10, 10);
    }
}
