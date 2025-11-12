using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EyE.Threading;
namespace Eye.Maps.Templates
{
    public class MazeMeshDrawHex : MazeMeshDrawGeneric<HexIndex2D>
    {

        protected override GenericMazeMap<HexIndex2D> CreateMazeMap()
        {
            MazeMapHex maze = new MazeMapHex(mazeSize);
            maze.GenerateMaze();
            return maze;
        }
        protected override async UniTask<GenericMazeMap<HexIndex2D>> CreateMazeMapAsync(TaskHandler taskContext)
        {
            MazeMapHex maze = new MazeMapHex(mazeSize);
            await maze.GenerateMazeAsync(taskContext);
            return maze;
        }
        protected override HexIndex2D DefaultMazeSize()
        {
            return new HexIndex2D(10, 10);
        }
        protected override Chunker<HexIndex2D> GetChunker()
        {
            return new HexChunker(mazeSize);
        }
        /*protected override WallMeshChunkComputerGeneric<HexIndex2D> GetNewMeshComputer()
        {
            return new WallMeshChunkComputerGeneric<HexIndex2D>();
        }*/


    }

    public class HexChunker : Chunker<HexIndex2D>
    {

        public HexChunker(HexIndex2D size) : base(size) { }
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
}
