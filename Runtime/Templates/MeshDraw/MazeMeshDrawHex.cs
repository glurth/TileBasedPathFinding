using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EyE.Threading;
namespace EyE.Maps.Templates
{
    public class MazeMeshDrawHex : MazeMeshDrawGeneric<HexIndex2D>
    {

        protected override GenericMazeMap<HexIndex2D> CreateMazeMap()
        {
            MazeMapHex maze = new MazeMapHex(mazeSize, mazeNormal);
            maze.GenerateMaze();
            return maze;
        }
        protected override async UniTask<GenericMazeMap<HexIndex2D>> CreateMazeMapAsync(TaskHandler taskContext)
        {
            MazeMapHex maze = new MazeMapHex(mazeSize,mazeNormal);
            await maze.GenerateMazeAsync(taskContext);
            return maze;
        }
  //      protected override HexIndex2D DefaultMazeSize()
  //      {
  //          return new HexIndex2D(10, 10);
  //      }
        protected override Chunker<HexIndex2D> GetChunker(int idealTrisPerChunk = 1000)
        {
            return new HexChunker(mazeSize);
        }
        protected override WallMeshChunkComputerGeneric<HexIndex2D> GetNewMeshComputer()
        {
            HexWallMeshComputer c= new HexWallMeshComputer();// WallMeshChunkComputerGeneric<HexIndex2D>();
            c.testOffset = testOffset;
            return c;
        }
        public int testOffset;
    }

    public class HexChunker : Chunker<HexIndex2D>
    {

        public HexChunker(HexIndex2D size, int idealTrisPerChunk = 1000) : base(size,  idealTrisPerChunk ) { }
        protected override int NumberOfTilesInSize(HexIndex2D size)
        {
            return size.x * size.y;
        }
        /*protected override List<List<HexIndex2D>> GenerateChunks(int numChunks, HexIndex2D size)
        {
            return GenerateVector2IntChunks(
                numChunks,
                size,
                h => new Vector2Int(h.x, h.y),
                v => new HexIndex2D(v.x, v.y));
        }*/
        protected override async UniTask<List<List<HexIndex2D>>> GenerateChunksAsync(int numChunks, HexIndex2D size,TaskHandler taskContext)
        {
            return await GenerateVector2IntChunksAsync(
                numChunks,
                size,
                h => new Vector2Int(h.x, h.y),
                v => new HexIndex2D(v.x, v.y),
                taskContext);
        }
        /*protected override List<List<HexIndex2D>> GenerateChunks(int numChunks, HexIndex2D size)
        {

            // determine grid of chunks
            int chunksX = (int)Mathf.Ceil(Mathf.Sqrt(numChunks));
            int chunksY = (int)Mathf.Ceil((float)numChunks / chunksX);

            int chunkWidth = (int)Mathf.Ceil((float)size.x / chunksX);
            int chunkHeight = (int)Mathf.Ceil((float)size.y / chunksY);

            List<List<HexIndex2D>> coordinatesPerChunk = new List<List<HexIndex2D>>();

            for (int cy = 0; cy < chunksY; cy++)
            {
                for (int cx = 0; cx < chunksX; cx++)
                {
                    var cluster = new List<HexIndex2D>();
                    int startX = cx * chunkWidth;
                    int startY = cy * chunkHeight;
                    int endX = Mathf.Min(size.x, startX + chunkWidth);
                    int endY = Mathf.Min(size.y, startY + chunkHeight);

                    for (int x = startX; x < endX; x++)
                    {
                        for (int y = startY; y < endY; y++)
                        {
                            cluster.Add(new HexIndex2D(x, y));
                        }
                    }
                    coordinatesPerChunk.Add(cluster);
                }
            }
            return coordinatesPerChunk;
        }*/




    }
    public class HexWallMeshComputer : WallMeshChunkComputerGeneric<HexIndex2D>
    {
        public int testOffset = 0;
        static Vector3Int[] cubedCornerOffsets = new Vector3Int[]
        {
            new Vector3Int(+1, +1, -1),
            new Vector3Int(-1, +1, -1),
            new Vector3Int(-1, +1, +1),
            new Vector3Int(-1, -1, +1),
            new Vector3Int(+1, -1, +1),
            new Vector3Int(+1, -1, -1),
        };


        Vector3Int CornerCubedCoord(HexIndex2D coord,int c)
        {
            return 2*coord.ToCubedCoords() +  cubedCornerOffsets[(c+ testOffset) %6];
        }


        // Corner offsets in doubled-axial.
        // These 6 offsets match your corner order exactly.
        static readonly Vector2Int[] cornerOffsets = new Vector2Int[]
        {
            new Vector2Int(+1,  1),   // corner 0
            new Vector2Int( 0, -1),   // corner 1
            new Vector2Int(0, 2),   // corner 2
            new Vector2Int(-1,  -1),   // corner 3
            new Vector2Int( -1, +1),   // corner 4
            new Vector2Int(+0, +2),   // corner 5
        };


        Vector2Int CornerKey(HexIndex2D h, int c)
        {
            int q = h.x;
            int r = h.y;

            // Tile center in world*2 units
            int baseX = 3 * q + (r & 1);
            int baseY = 2 * r;

            return new Vector2Int(
                baseX + cornerOffsets[c].x,
                baseY + cornerOffsets[c].y
            );
        }

        protected override async UniTask BuildUniqueCornersAsync(TaskHandler taskContext)
        {
           // Dictionary<HexIndex2D, int> cornerLookup = new Dictionary<HexIndex2D, int>();
            Dictionary<Vector3Int, int> cornerLookup = new Dictionary<Vector3Int, int>();

           // string logStr = "";
            for (int y = 0; y < map.size.y; y++)
            {
                for (int x = 0; x < map.size.x; x++)
                {
                    HexIndex2D tile = new HexIndex2D(x, y);
                    List<int> list = new List<int>(6);//full list passed to cornerIndecesByCoordinate dic- need a new one for each

                    Vector3Int tileCubedCoord = tile.ToCubedCoords();
                    int c = 0;
                    while (c < 6)
                    {
                        HexIndex2D neighbor = tile.GetNeighbor(c);
                        int secondNeighborCornerIndex = ((c + 5) % 6);
                        HexIndex2D neighbor2 = tile.GetNeighbor(secondNeighborCornerIndex);


                        Vector3Int cc = tileCubedCoord + neighbor.ToCubedCoords() + neighbor2.ToCubedCoords();
 //                       logStr += $"\nhex:{tile} , corner index:{c}, cornerKey [{cc}]:{ComputeCornerPos(tile, c)}";
                        int idx;
                        if (cornerLookup.TryGetValue(cc, out idx))
                        {
//                            logStr += " (Exists)";
                            list.Add(idx);
                        }
                        else
                        {
//                            logStr += " (Created)";
                            Vector3 pos = ComputeCornerPos(tile, c);
                            idx = uniqueCorners.Count;
                            uniqueCorners.Add(new Corner { position = pos });
                            cornerLookup.Add(cc, idx);
                            list.Add(idx);
                        }

                        c = c + 1;
                    }

                    cornerIndecesByCoordinate.Add(tile, list);
                }
                taskContext.IncrementProgress(0.1f);
                await taskContext.Yield();
            }
          //  await UniTask.SwitchToMainThread();
          //  Debug.Log(logStr);
        }

    }
    /*{
    uniqueCorners.Clear();
    cornerIndecesByCoordinate.Clear();
    //temp storage:
    //each tile will store corner 12 23 34   (where the two numbers are the two neighbor edge indexes)
    //for the other corners, 01 50 45 , we will get them from neighboring tiles.
    // use temp storage to populate uniqueCorners list, and cornerIndecesByCoordinate dictionary

    int[] storedCornerIndices = { 2, 3, 4 };
    int[] storedInNeighborCornerIndices = { 5, 0, 1 };

    // Precompute bottom-left 3 corner offsets once
    if (hexCornerOffsets == null)
    {
        hexCornerOffsets = new Vector3[3];

        // Pick an arbitrary tile (0,0)
        HexIndex2D coord = new HexIndex2D(0, 0);
        Vector3 coordCenterPos = map.GetModelSpacePosition(coord);


        // find corner for neighbor edge 
        // Bottom-left half: directions 1,2,3
        for (int n = 0; n < 3; n++)
        {
            Vector3 c = ComputeCornerPos(coord, storedCornerIndices[n]);
            hexCornerOffsets[n] = c;
        }
    }

    // Storage for the 3-corner lists per tile
    Dictionary<HexIndex2D, int[]> storedCorners = new Dictionary<HexIndex2D, int[]>();

    // First pass: create the 3 stored corners for every tile
    //store in unique corners- and record index of the 3 in each tile

    HexIndex2D size = map.size;
    for (int x = -1; x < size.x + 1; x++)
        for (int y = -1; y < size.y + 1; y++)
        {
            HexIndex2D tile = new HexIndex2D(x, y);
            Vector3 center = map.GetModelSpacePosition(tile);

            int[] cornerIdx = new int[3];
            for (int n = 0; n < 3; n++)
            {
                Vector3 pos = center + hexCornerOffsets[n];
                int idx = uniqueCorners.Count;
                cornerIdx[n] = idx;
            }

            storedCorners[tile] = cornerIdx;

            await taskContext.Yield();
        }

    // Second pass: assemble full 6-corner list for each tile
    foreach (HexIndex2D tile in map.allMapCoords)
    {
        int[] full = new int[6];

        // Corners 3,4,5 come from our stored bottom-left half
        full[storedCornerIndices[0]] = storedCorners[tile][0];

        uniqueCorners.Add(new Corner { position = pos });

        full[storedCornerIndices[1]] = storedCorners[tile][1];
        full[storedCornerIndices[2]] = storedCorners[tile][2];

        for (int n = 0; n < 3; n++)
        {
            int neighborIndex = storedInNeighborCornerIndices[n];
            HexIndex2D neighbor = tile.GetNeighbor(neighborIndex);
            // if (!map.IsWithinBounds(neighbor))
            //   throw new System.Exception($"Neighbor[{neighborIndex}]:{neighbor} of tile: {tile} is out of bounds.");

            full[neighborIndex] = storedCorners[neighbor][n];
        }

        cornerIndecesByCoordinate[tile] = new List<int>(full);

        taskContext.IncrementProgress(0.1f);
        await taskContext.Yield();
    }
}*/



}
