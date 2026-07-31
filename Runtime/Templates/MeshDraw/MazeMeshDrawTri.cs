using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EyE.Threading;
namespace EyE.Maps.Templates
{
    public class MazeMeshDrawTri : MazeMeshDrawGeneric<TriangularIndex2D>
    {

        protected override GenericMazeMap<TriangularIndex2D> CreateMazeMap()
        {
            MazeMapTri maze = new MazeMapTri(mazeSize,mazeNormal);
            maze.GenerateMaze();
            return maze;
        }
        protected override GenericMazeMap<TriangularIndex2D> GetUninitializedMap() => new MazeMapTri(mazeSize, mazeNormal);
        /*protected override async UniTask<GenericMazeMap<TriangularIndex2D>> CreateMazeMapAsync(TaskHandler taskContext)
        {
            MazeMapTri maze = new MazeMapTri(mazeSize, mazeNormal);
            await maze.GenerateMazeAsync(taskContext);
            return maze;
        }*/

        protected override Chunker<TriangularIndex2D> GetChunker(int trisPerWall = 12, int idealTrisPerChunk = 1000)
        {
            return new TriChunker(mazeSize, trisPerWall ,idealTrisPerChunk);
        }

        protected override WallMeshChunkComputerGeneric<TriangularIndex2D> GetNewMeshComputer()
        {
            return new WallMeshChunkComputerGeneric<TriangularIndex2D>();//TriWallMeshComputer();// 
        }


    }

    public class TriChunker : Chunker<TriangularIndex2D>
    {

        public TriChunker(TriangularIndex2D size, int trisPerWall = 12, int idealTrisPerChunk = 1000) : base(size, trisPerWall, idealTrisPerChunk ) { }
        protected override int NumberOfTilesInSize(TriangularIndex2D size)
        {
            return size.x * size.y;
        }
        /*protected override List<List<TriangularIndex2D>> GenerateChunks(int numChunks, TriangularIndex2D size)
        {
            return GenerateVector2IntChunks(
                numChunks,
                size,
                h => new Vector2Int(h.x, h.y),
                v => new TriangularIndex2D(v.x, v.y));
        }*/
        protected override async UniTask<List<List<TriangularIndex2D>>> GenerateChunksAsync(int numChunks, TriangularIndex2D size, TaskHandler taskContext)
        {
            return await GenerateVector2IntChunksAsync(
                numChunks,
                size,
                h => new Vector2Int(h.x, h.y),
                v => new TriangularIndex2D(v.x, v.y),
                taskContext);
        }
        /*protected override List<List<TriangularIndex2D>> GenerateChunks(int numChunks, TriangularIndex2D size)
        {
            // determine grid of chunks
            int chunksX = (int)Mathf.Ceil(Mathf.Sqrt(numChunks));
            int chunksY = (int)Mathf.Ceil((float)numChunks / chunksX);

            int chunkWidth = (int)Mathf.Ceil((float)size.x / chunksX);
            int chunkHeight = (int)Mathf.Ceil((float)size.y / chunksY);

            List<List<TriangularIndex2D>> coordinatesPerChunk = new List<List<TriangularIndex2D>>();
            string logstr = "Generating Chunks num("+numChunks+"):["+chunksX+","+chunksY+"]: ";
            for (int cy = 0; cy < chunksY; cy++)
            {
                for (int cx = 0; cx < chunksX; cx++)
                {
                    var cluster = new List<TriangularIndex2D>();
                    int startX = cx * chunkWidth;
                    int startY = cy * chunkHeight;
                    int endX = Mathf.Min(size.x, startX + chunkWidth);
                    int endY = Mathf.Min(size.y, startY + chunkHeight);
                    logstr+="\n chunk coord["+cx+","+cy+ "]-  start coord:[" + startX + "," + startY + "] ending at (exclusive):[" + endX + "," + endY + "]";

                    for (int x = startX; x < endX; x++)
                    {
                        for (int y = startY; y < endY; y++)
                        {
                            cluster.Add(new TriangularIndex2D(x, y));
                        }
                    }
                    coordinatesPerChunk.Add(cluster);
                }
            }
            Debug.Log(logstr);
            return coordinatesPerChunk;
        }
        */
    }
    public class TriWallMeshComputer : WallMeshChunkComputerGeneric<TriangularIndex2D>
    {
        protected override async UniTask BuildUniqueCornersAsync(TaskHandler taskContext)
        {
            Dictionary<Vector2Int, int> cornerIndexByKey = new Dictionary<Vector2Int, int>();
            await taskContext.SetStageMessageAndYield("Tri-Corners generation");
            foreach (TriangularIndex2D coord in map.allMapCoords)
            {
                List<int> tileUniqueCornerIndeces = new List<int>(3);
                int x = coord.x;
                int y = coord.y;
                // Determine corner lattice points for this triangle
                Vector2Int[] corners;
                if (!coord.IsPointingUp())
                {
                    // top, lower right, lower left
                    corners = new Vector2Int[]
                    {
                        new Vector2Int(x, y),       // top
                        new Vector2Int(x+1, y+1),   // right
                        new Vector2Int(x, y+1)    // left
                    };
                }
                else
                {
                    // bottom upper right,upper left, left, right
                    corners = new Vector2Int[]
                    {
                        new Vector2Int(x,y+1),   // bottom
                        new Vector2Int(x+1, y),   // left
                        new Vector2Int(x,y)    // right
                    };
                }


                for (int cornerNumber = 0; cornerNumber < 3; cornerNumber++)
                {
                    int n = (cornerNumber + 2) % 3;
                    if (!cornerIndexByKey.TryGetValue(corners[n], out int cornerIndex))
                    {
                        cornerIndex = uniqueCorners.Count;// uniqueCornerPositions.Count;
                        cornerIndexByKey.Add(corners[n], cornerIndex);

                        // Convert lattice corner to world space
                        Vector3 cornerPos = ComputeCornerPos(coord, n);//  new Vector3(corners[n].x, 0, corners[n].y);
                        uniqueCorners.Add(new Corner { position = cornerPos });
                    }

                    tileUniqueCornerIndeces.Add(cornerIndex);
                }

                cornerIndecesByCoordinate.Add(coord, tileUniqueCornerIndeces);

                await taskContext.Yield();
                taskContext.IncrementProgress(0.1f);
            }

            await taskContext.SetStageMessageAndYield("Finalizing corners.");


        }
    }


}
