using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EyE.Threading;
using System;
using EyE.Geometry;

namespace EyE.Maps.Templates
{
    public static class Vector2Extensions
    {
        /// <summary>
        /// Gets normal vector for angle, in radians, about origin.
        /// </summary>
        /// <param name="angle">angle in Radius</param>
        /// <returns></returns>
        static public Vector2 NormalFromAngle(float angle)
        {
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }

    public class MazeMeshDrawRadial : MazeMeshDrawGeneric<RadialCoord>,IMazeDrawer<RadialCoord>
    {
       // public float radialSpacing=1;
       // public int numOfRingsToDoubleSectors=3;
      //  MazeMapRadial radialMaze;
       // public override GenericMazeMap<RadialCoord> maze => radialMaze;

        public void SetTileVisibility(RadialCoord coord, bool isVisible)
        {
           //throw new NotImplementedException();
        }

        protected override GenericMazeMap<RadialCoord> CreateMazeMap()
        {
            MazeMapRadial  radialMaze = new MazeMapRadial(mazeSize.ring,mazeSize.sector);
            radialMaze.GenerateMaze();
            return radialMaze;
        }

        protected override async UniTask<GenericMazeMap<RadialCoord>> CreateMazeMapAsync(TaskHandler taskContext)
        {
            MazeMapRadial radialMaze = new MazeMapRadial(mazeSize.ring, mazeSize.sector);
            await radialMaze.GenerateMazeAsync(taskContext);
            return radialMaze;
        }

        /*protected override RadialCoord DefaultMazeSize()
        {
            return new RadialCoord(5, 10);
        }*/
        protected override Chunker<RadialCoord> GetChunker(int idealTrisPerChunk = 1000)
        {
            return new RadialChunker(mazeSize, maze as  MazeMapRadial,idealTrisPerChunk);
        }
        protected override WallMeshChunkComputerGeneric<RadialCoord> GetNewMeshComputer()
        {
            return new RadialMazeMeshGenerator(30);//  
        }
    }
    public class RadialChunker : Chunker<RadialCoord>
    {
        MazeMapRadial mapRef;
        public RadialChunker(RadialCoord size, MazeMapRadial mapRef,int idealTrisPerChunk = 1000) : base(size, idealTrisPerChunk)
        {
            this.mapRef = mapRef;
        }
        protected override int NumberOfTilesInSize(RadialCoord size)
        {
            return RadialCoord.NumTiles(size);

        }
        protected override async UniTask<List<List<RadialCoord>>> GenerateChunksAsync(int numChunks, RadialCoord size, TaskHandler taskContext)
        {
           // numChunks = 1;
            int dimChunksR = Mathf.CeilToInt(Mathf.Sqrt(numChunks));
            dimChunksR = Mathf.Min(dimChunksR, size.ring);
            int dimChunksAng = Mathf.CeilToInt(numChunks / (float)dimChunksR);
            List<RadialCoord>[,] coordinatesPerChunk = new List<RadialCoord>[dimChunksR, dimChunksAng];
            for (int ring = 0; ring < dimChunksR; ring++)
            {
                for (int sector = 0; sector < dimChunksAng; sector++)
                {
                    coordinatesPerChunk[ring, sector] = new List<RadialCoord>();
                }
            }

            for (int ring = 0; ring < size.ring; ring++)
            {
                for (int sector = 0; sector < mapRef.SectorsAtRing(ring); sector++)
                {
                    RadialCoord coord = new RadialCoord(ring, sector);
                    int angChunkIndex = Mathf.Min(dimChunksAng - 1, Mathf.FloorToInt(coord.AngleInTurns * dimChunksAng));
                    int radChunkIndex = Mathf.Min(dimChunksR - 1, (ring * dimChunksR) / size.ring);
                    //int angChunkIndex = (int) (dimChunksAng/(coord.AngleInTurns+1));
                    //int radChunkIndex = dimChunksR/(ring+1);
                    coordinatesPerChunk[radChunkIndex, angChunkIndex].Add(coord);
                }
                taskContext.IncrementProgress(.1f);
                await taskContext.Yield();
            }

            List<List<RadialCoord>> coordsPerChunk = new List<List<RadialCoord>>();
            for (int r = 0; r < dimChunksR; r++)
                for (int sector = 0; sector < dimChunksAng; sector++)
                    coordsPerChunk.Add(coordinatesPerChunk[r, sector]);

            return coordsPerChunk;
        }

    }

    public class RadialMazeMeshGenerator: WallMeshChunkComputerGeneric<RadialCoord>
    {
        Func<RadialCoord, bool> isTileVisible;
        MazeMapRadial radialMap;
        MazeMeshDrawRadial radialDrawer;
        public int curvy = 4;

        List<MeshData> chunkMeshes = new List<MeshData>();
        IReadOnlyList<IReadOnlyList<RadialCoord>> coordsPerChuck;

        public RadialMazeMeshGenerator(int curvy)
        {
            this.curvy = curvy;
        }
        /// <summary>
        /// Returns a list of generated meshes, one for each chunk.
        /// </summary>
        /// <param name="data">the maze map data stored as a GenericMazeMap<T></param>
        /// <param name="mazeDrawer">instance of the class that draws the meshes, provides IsVisible information per tile</param>
        /// <param name="wallThickness"></param>
        /// <param name="wallHeight"></param>
        /// <param name="coordsPerChuck">Number of coordinate each chunk/mesh will represent</param>
        /// <param name="displayBorderWalls"></param>
        /// <returns></returns>
        async public override UniTask<List<MeshData>> CreateWallsMeshChunksAsync(
                GenericMazeMap<RadialCoord> data,
                MazeMeshDrawGeneric<RadialCoord> mazeDrawer,
                float wallThickness,
                float wallHeight,
                IReadOnlyList<IReadOnlyList<RadialCoord>> coordsPerChuck,
                TaskHandler taskContext,
                bool displayBorderWalls = true)
        {

            this.map = data;
            radialMap = map as MazeMapRadial;
            this.mazeDrawer = mazeDrawer;
            radialDrawer = mazeDrawer as MazeMeshDrawRadial;
            this.wallThickness = wallThickness;
            this.wallHeight = wallHeight;
            this.displayBorderWalls = displayBorderWalls;
            this.coordsPerChuck = coordsPerChuck;
            isTileVisible = (RadialCoord coord) => { return mazeDrawer.IsTileVisible(coord); };
            int chunkCounter = 0;
            foreach (IReadOnlyList<RadialCoord> coordsInChunk in coordsPerChuck)
            {
                await taskContext.SetStageMessageAndYield("RadialMazeMeshGenerator: creating wall mesh for chunk " + chunkCounter);
                MeshData chunkMesh = await GenerateChunkMesh(coordsInChunk,taskContext);
                chunkMeshes.Add(chunkMesh);

                taskContext.IncrementProgress(1f);
                chunkCounter++;
            }
            return chunkMeshes;
        }

        public override Mesh RebuildSingleChunk(int chunk)
        {
            chunkMeshes[chunk] = GenerateChunkMesh(coordsPerChuck[chunk], new TaskHandler(false)).AsTask().GetAwaiter().GetResult();
            return chunkMeshes[chunk].ToMesh();
        }

        async public UniTask<MeshData> GenerateChunkMesh(IReadOnlyList<RadialCoord> chunkTiles,TaskHandler taskContext)
        {
            MeshData mesh = new MeshData();

            foreach (var tile in chunkTiles)
            {
                int neighborsCount = tile.NumberOfNeighbors();

                for (int ni = 0; ni < neighborsCount; ni++)
                {
                    RadialCoord neighbor = tile.GetNeighbor(ni);
                    bool inBounds = map.IsWithinBounds(neighbor);

                    // skip entirely if out-of-bounds and not drawing borders
                    if (!inBounds && !displayBorderWalls) continue;

                    int wallIndex = map.GetNeighborIndexOf(tile, neighbor);
                    bool hasWall = (!inBounds) || map.Walls[tile][wallIndex];
                    /*if (tile.sector == 0 || neighbor.sector == 0)
                    {
                        int testThisTileneighborIndex = map.GetNeighborIndexOf(neighbor, tile);
                        if(inBounds)
                            Debug.Log("has wall: ("+hasWall+") at tile: [" + tile + "] neighborIndex: [" + wallIndex + "] at coord:[" + neighbor + "] +  double check reverse: wall exists-" + map.Walls[neighbor][testThisTileneighborIndex]);
                        else
                            Debug.Log("has wall: (" + hasWall + ") at tile: [" + tile + "] neighborIndex: [" + wallIndex + "] at coord:[" + neighbor + "] +  neighbor is out of bounds");
                    }*/
                    if (!hasWall)
                    {

                        continue;
                    }

                    // skip invisible tiles unless drawing borders
                    if (!displayBorderWalls)
                        if (!isTileVisible(tile) && (inBounds && !isTileVisible(neighbor))) continue;

                    if (tile.ring == neighbor.ring)
                    {
                        // Radial wall between sectors in the same ring
                        GenerateRadialWall(mesh, tile, neighbor);
                    }
                    else
                    {
                        // Ring wall between rings (arc)
                       // Debug.Log("Drawing Ring wall at tile: [" + tile + "] neighborIndex: [" + wallIndex + "] at coord:[" + neighbor + "]");
                        GenerateRingWall(mesh, tile, neighbor);
                    }
                }
                taskContext.IncrementProgress(1f);
                await taskContext.Yield();
            }
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        // Ring wall: along the circular arc between tile and neighbor
        void GenerateRingWall(MeshData mesh, RadialCoord tile, RadialCoord neighbor)
        {
            List<float> angles;
            RadialCoord innerTile = tile;
            RadialCoord outerTile = neighbor;
            if (neighbor.ring < tile.ring)
            {
                innerTile = neighbor;
                outerTile = tile;
            }
            bool outerRingDoubles = (radialMap.SectorsAtRing(innerTile.ring) < radialMap.SectorsAtRing(outerTile.ring));

            if (innerTile.ring==0|| outerRingDoubles)
                angles = GetWallSegmentAngles(outerTile, curvy);
            else
                angles = GetWallSegmentAngles(innerTile, curvy);

            List<Vector3> bottomVerts = new List<Vector3>();
            List<Vector3> topVerts = new List<Vector3>();

            float radius = radialMap.RingOuterRadius(innerTile.ring);// (tile.ring + 1) * radialDrawer.radialSpacing;// map.worldScale;
         //   Debug.Log("GenerateRingWall-  from-[" + tile + "]-angleTurns:"+tile.AngleInTurns+"    to Neighbor:  [" + neighbor + "] radius: "+radius+ "-angleTurns:" + tile.AngleInTurns);
            foreach (float a in angles)
            {
                Vector2 pos = PolarToCartesian(a, radius);
               // Debug.Log("     vert-[" + pos + "]   mag: " + pos.magnitude );
                bottomVerts.Add(new Vector3(pos.x,  pos.y,0f));
            }

            foreach (var v in bottomVerts)
                topVerts.Add(new Vector3(v.x, v.y, wallHeight));
            
            AddWallQuads(mesh, bottomVerts, topVerts,true);
        }

        // Radial wall: along the straight line from inner to outer radius
        void GenerateRadialWall(MeshData mesh, RadialCoord tile, RadialCoord neighbor)
        {
            //handle wrapping around ring 
            float tileAngle = tile.AngleInTurns;
            float neighborAngle = neighbor.AngleInTurns;
            if (Mathf.Abs(tileAngle - neighborAngle) > 0.5f)
            {
                if (tileAngle < neighborAngle) tileAngle += 1f;
                else neighborAngle += 1f;
            }
            float angleInTurns = (tileAngle + neighborAngle) * 0.5f;
            if (angleInTurns > 1f) angleInTurns -= 1f;
           // Debug.Log("GenerateRadialWall-  angleInTurns:" + angleInTurns + "  from-["+ tile + "] tile.AngleInTurns:" + tile.AngleInTurns + "  ["+ neighbor + "]neighbor.AngleInTurns:" + neighbor.AngleInTurns);
            float radiusInner = radialMap.RingInnerRadius(tile.ring);//(tile.ring) * radialDrawer.radialSpacing;
            float radiusOuter = radialMap.RingOuterRadius(tile.ring);// (tile.ring+1) * radialDrawer.radialSpacing;
            radiusInner -= wallThickness * 0.5f;
            radiusOuter += wallThickness * 0.5f;


            // Radial line from inner to outer radius at tile's angle
            float angle = angleInTurns * 2f * Mathf.PI;
            Vector2 innerPos = PolarToCartesian(angle, radiusInner);
            Vector2 outerPos = PolarToCartesian(angle, radiusOuter);

            Vector3 bottomStart = new Vector3(innerPos.x, innerPos.y,0f);
            Vector3 bottomEnd = new Vector3(outerPos.x, outerPos.y,0f);
            Vector3 topStart = new Vector3(innerPos.x, innerPos.y, wallHeight);
            Vector3 topEnd = new Vector3(outerPos.x, outerPos.y, wallHeight);

            AddWallQuads(mesh,
                new List<Vector3> { bottomStart, bottomEnd },
                new List<Vector3> { topStart, topEnd });
        }

        List<float> GetWallSegmentAngles(RadialCoord tile, int curvy)
        {
            float segementAngleLengthInTurns = .5f / radialMap.SectorsAtRing(tile.ring);
            segementAngleLengthInTurns *= mazeDrawer.wallWidthFraction;
            float startAngle = (tile.AngleInTurns - segementAngleLengthInTurns) * 2f * Mathf.PI;
            float endAngle =   (tile.AngleInTurns + segementAngleLengthInTurns) * 2f * Mathf.PI;

            // handle wrap around 0
            if (endAngle < startAngle) endAngle += 2f * Mathf.PI;

            List<float> angles = new List<float>();
            for (int i = 0; i <= curvy; i++)
                angles.Add(Mathf.Lerp(startAngle, endAngle, i / (float)curvy));

            return angles;
        }



        Vector2 PolarToCartesian(float angle, float radius)
        {
            return Vector2Extensions.NormalFromAngle(angle) * radius;
            //return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        void AddWallQuads(MeshData mesh, List<Vector3> bottomVerts, List<Vector3> topVerts, bool useNormalizePositionForNormal=false)
        {
            int n = bottomVerts.Count;
            if (n < 2) return;

            List<Vector3> verts = mesh.vertices != null ? new List<Vector3>(mesh.vertices) : new List<Vector3>();
            List<int> tris = mesh.triangles != null ? new List<int>(mesh.triangles) : new List<int>();

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 dir = (bottomVerts[i + 1] - bottomVerts[i]).normalized;
                Vector3 normal2D = new Vector3(-dir.y, dir.x, 0f);
                Vector3 normal2D0 = -bottomVerts[i].normalized;
                Vector3 normal2D1 = -bottomVerts[i+1].normalized;
                if (!useNormalizePositionForNormal)
                {
                    normal2D0 = normal2D;
                    normal2D1 = normal2D;
                }

                Vector3 b0i = bottomVerts[i] + normal2D0 * wallThickness * 0.5f;
                Vector3 b1i = bottomVerts[i + 1] + normal2D1 * wallThickness * 0.5f;
                Vector3 b0o = bottomVerts[i] - normal2D0 * wallThickness * 0.5f;
                Vector3 b1o = bottomVerts[i + 1] - normal2D1 * wallThickness * 0.5f;

                Vector3 t0i = b0i + Vector3.forward * wallHeight;
                Vector3 t1i = b1i + Vector3.forward * wallHeight;
                Vector3 t0o = b0o + Vector3.forward * wallHeight;
                Vector3 t1o = b1o + Vector3.forward * wallHeight;

                int start = verts.Count;

                // FRONT (inner)
                verts.AddRange(new[] { b0i, b1i, t1i, t0i });
                tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                start += 4;

                // BACK (outer)
                verts.AddRange(new[] { b1o, b0o, t0o, t1o });
                tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                start += 4;
                if (i == 0)
                {
                    // LEFT
                    verts.AddRange(new[] { b0o, b0i, t0i, t0o });
                    tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                    start += 4;
                }
                if (i == n - 2)
                {
                    // RIGHT
                    verts.AddRange(new[] { b1i, b1o, t1o, t1i });
                    tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                    start += 4;
                }
                // TOP
                verts.AddRange(new[] { t0i, t1i, t1o, t0o });
                tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                start += 4;

                // BOTTOM
                verts.AddRange(new[] { b0o, b1o, b1i, b0i });
                tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris);
        }
    }


}