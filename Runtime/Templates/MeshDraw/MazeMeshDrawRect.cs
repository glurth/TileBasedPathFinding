using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EyE.Threading;

namespace Eye.Maps.Templates
{
    public class MazeMeshDrawRect : MazeMeshDrawGeneric<RectangularCoord>
    {
        protected override GenericMazeMap<RectangularCoord> CreateMazeMap()
        {
            MazeMapRect maze = new MazeMapRect(mazeSize);
            maze.GenerateMaze();
            return maze;
        }

        protected override async UniTask<GenericMazeMap<RectangularCoord>> CreateMazeMapAsync(TaskHandler taskContext)
        {
            MazeMapRect maze = new MazeMapRect(mazeSize);
            await maze.GenerateMazeAsync(taskContext);
            return maze;
        }

//        protected override RectangularCoord DefaultMazeSize()
//        {
//            return new RectangularCoord(10, 10);
 //       }
        protected override Chunker<RectangularCoord> GetChunker(int idealTrisPerChunk = 1000)
        {
            return new RectChunker(mazeSize);
        }
        protected override WallMeshChunkComputerGeneric<RectangularCoord> GetNewMeshComputer()
        {
            return new RectWallMeshComputer();//new WallMeshChunkComputerGeneric<RectangularCoord>();//  
        }
    }

    public class RectChunker : Chunker<RectangularCoord>
    {

        public RectChunker(RectangularCoord size, int idealTrisPerChunk = 1000) : base(size,idealTrisPerChunk) { }
        protected override int NumberOfTilesInSize(RectangularCoord size)
        {
            return size.x * size.y;
        }
        /*protected override List<List<RectangularCoord>> GenerateChunks(int numChunks, RectangularCoord size)
        {
            return GenerateVector2IntChunks(
                numChunks,
                size,
                h => new Vector2Int(h.x, h.y),
                v => new RectangularCoord(v.x, v.y));
        }*/
        protected override async UniTask<List<List<RectangularCoord>>> GenerateChunksAsync(int numChunks, RectangularCoord size, TaskHandler taskContext)
        {
            return await GenerateVector2IntChunksAsync(
                numChunks,
                size,
                h => new Vector2Int(h.x, h.y),
                v => new RectangularCoord(v.x, v.y),
                taskContext);
        }
        /*protected override List<List<RectangularCoord>> GenerateChunks(int numChunks, RectangularCoord size)
        {
            // determine grid of chunks
            int chunksX = (int)Mathf.Ceil(Mathf.Sqrt(numChunks));
            int chunksY = (int)Mathf.Ceil((float)numChunks / chunksX);

            int chunkWidth = (int)Mathf.Ceil((float)size.x / chunksX);
            int chunkHeight = (int)Mathf.Ceil((float)size.y / chunksY);

            List<List<RectangularCoord>> coordinatesPerChunk = new List<List<RectangularCoord>>();
            string logstr = "Generating Chunks num(" + numChunks + "):[" + chunksX + "," + chunksY + "]: ";
            for (int cy = 0; cy < chunksY; cy++)
            {
                for (int cx = 0; cx < chunksX; cx++)
                {
                    var cluster = new List<RectangularCoord>();
                    int startX = cx * chunkWidth;
                    int startY = cy * chunkHeight;
                    int endX = Mathf.Min(size.x, startX + chunkWidth);
                    int endY = Mathf.Min(size.y, startY + chunkHeight);
                    logstr += "\n chunk coord[" + cx + "," + cy + "]-  start coord:[" + startX + "," + startY + "] ending at (exclusive):[" + endX + "," + endY + "]";

                    for (int x = startX; x < endX; x++)
                    {
                        for (int y = startY; y < endY; y++)
                        {
                            cluster.Add(new RectangularCoord(x, y));
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

    public class RectWallMeshComputer : WallMeshChunkComputerGeneric<RectangularCoord>
    {
        protected  override async UniTask BuildUniqueCornersAsync(TaskHandler taskContext)
        {
            int cornerCols = map.size.x + 1;
            int cornerRows = map.size.y + 1;

            Vector3 tileStep = map.SingleTileModelSpaceOffset();
            Vector3 basePos = map.GetModelSpacePosition(new RectangularCoord(0, 0)) - new Vector3(0.5f, 0.5f, 0); ;

            uniqueCorners.Clear();
            uniqueCorners.Capacity = cornerCols * cornerRows;
            Dictionary<(int, int), int> cornerIndexByXY = new Dictionary<(int, int), int>();

            for (int y = 0; y < cornerRows; y++)
            {
                for (int x = 0; x < cornerCols; x++)
                {
                    int index = uniqueCorners.Count;
                    cornerIndexByXY[(x, y)] = index;
                    Vector3 pos = basePos + new Vector3(x * tileStep.x, y * tileStep.y, 0);
                    uniqueCorners.Add(new Corner { position = pos });
                }
                await taskContext.Yield();
            }

            cornerIndecesByCoordinate.Clear();
            foreach (RectangularCoord coord in map.allMapCoords)
            {
                int x = coord.x;
                int y = coord.y;
                List<int> tileCorners = new List<int>(4)
                    {
                        cornerIndexByXY[(x + 1, y    )], // n=0 east  ? bottom-right
                        cornerIndexByXY[(x + 1, y + 1)], // n=1 north ? top-right
                        cornerIndexByXY[(x,     y + 1)], // n=2 west  ? top-left
                        cornerIndexByXY[(x,     y    )]  // n=3 south ? bottom-left
                    };
                                    cornerIndecesByCoordinate.Add(coord, tileCorners);
                taskContext.IncrementProgress(0.1f);
                await taskContext.Yield();
            }
        }

    }
}