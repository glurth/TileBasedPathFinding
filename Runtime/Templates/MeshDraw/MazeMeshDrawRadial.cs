using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EyE.Threading;
using System;
using EyE.Geometry;

namespace EyE.Maps.Templates
{
    public class MazeMeshDrawRadial : MazeMeshDrawGeneric<RadialCoord>,IMazeDrawer<RadialCoord>
    {

        protected override GenericMazeMap<RadialCoord> CreateMazeMap()
        {
            MazeMapRadial  radialMaze = new MazeMapRadial(mazeSize.ring,mazeSize.sector, mazeNormal);
            radialMaze.GenerateMaze();
            return radialMaze;
        }

        protected override async UniTask<GenericMazeMap<RadialCoord>> CreateMazeMapAsync(TaskHandler taskContext)
        {
            MazeMapRadial radialMaze = new MazeMapRadial(mazeSize.ring, mazeSize.sector, mazeNormal);
            await radialMaze.GenerateMazeAsync(taskContext);
            return radialMaze;
        }

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
            MazeMap2D<RadialCoord> mazeMap2D = mazeDrawer.maze as MazeMap2D<RadialCoord>;
            Vector3 right = mazeMap2D.MazeOrientation * Vector3.right;
            Vector3 up =mazeMap2D.MazeOrientation * Vector3.up;

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

                    if (!hasWall)
                    {
                        continue;
                    }
                    if (!isTileVisible(tile) && ((!inBounds&& displayBorderWalls) || !isTileVisible(neighbor))) continue;

                    if (tile.ring == neighbor.ring)
                    {
                        // Radial wall between sectors in the same ring
                        GenerateRadialWall(mesh, tile, neighbor, up, right, mazeDrawer.mazeNormal);
                    }
                    else
                    {
                        // Ring wall between rings (arc)
                       // Debug.Log("Drawing Ring wall at tile: [" + tile + "] neighborIndex: [" + wallIndex + "] at coord:[" + neighbor + "]");
                        GenerateRingWall(mesh, tile, neighbor, up, right, mazeDrawer.mazeNormal);
                    }
                }
                taskContext.IncrementProgress(1f);
                await taskContext.Yield();
            }
            //ReorientAllVerts(mesh, mazeMap2D.MazeOrientation);//
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        // Ring wall: along the circular arc between tile and neighbor
        void GenerateRingWall(MeshData mesh, RadialCoord tile, RadialCoord neighbor, Vector3 planeUp, Vector3 planeRight, Vector3 planeNormal)
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
                angles = GetWallSegmentAnglesInTurns(outerTile, curvy);
            else
                angles = GetWallSegmentAnglesInTurns(innerTile, curvy);

            List<Vector3> bottomVerts = new List<Vector3>();
            float radius = radialMap.RingOuterRadius(innerTile.ring);// (tile.ring + 1) * radialDrawer.radialSpacing;// map.worldScale;
         //   Debug.Log("GenerateRingWall-  from-[" + tile + "]-angleTurns:"+tile.AngleInTurns+"    to Neighbor:  [" + neighbor + "] radius: "+radius+ "-angleTurns:" + tile.AngleInTurns);
            foreach (float a in angles)
            {
                Vector2 pos = RadialCoord.GetRadialDirection(a) * radius;
               // Debug.Log("     vert-[" + pos + "]   mag: " + pos.magnitude );
                bottomVerts.Add(planeUp * pos.y + planeRight * pos.x);// new Vector3(pos.x,  pos.y,0f));
            }
           
            AddWallQuads(mesh, bottomVerts, planeNormal, true);
        }

        // Radial wall: along the straight line from inner to outer radius
        void GenerateRadialWall(MeshData mesh, RadialCoord tile, RadialCoord neighbor, Vector3 planeUp, Vector3 planeRight, Vector3 planeNormal)
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
            Vector2 angleDir = RadialCoord.GetRadialDirection(angleInTurns);
            Vector2 innerPos = angleDir * radiusInner;// PolarToCartesian(angle, radiusInner);
            Vector2 outerPos = angleDir * radiusOuter;// PolarToCartesian(angle, radiusOuter);

            Vector3 bottomStart = planeUp * innerPos.y + planeRight * innerPos.x;
            Vector3 bottomEnd = planeUp * outerPos.y + planeRight * outerPos.x;

            AddWallQuads(mesh,
                    new List<Vector3> { bottomStart, bottomEnd },planeNormal);

        }

        List<float> GetWallSegmentAnglesInTurns(RadialCoord tile, int curvy)
        {
            float segementAngleLengthInTurns = .5f / radialMap.SectorsAtRing(tile.ring);
            segementAngleLengthInTurns *= mazeDrawer.wallWidthFraction;
            float startAngle = (tile.AngleInTurns - segementAngleLengthInTurns);// * 2f * Mathf.PI;
            float endAngle = (tile.AngleInTurns + segementAngleLengthInTurns);// * 2f * Mathf.PI;

            // handle wrap around 0
            if (endAngle < startAngle) endAngle += 1f;// 2f * Mathf.PI;

            List<float> angles = new List<float>();
            float frac = 1f / (float)curvy;
            for (int i = 0; i <= curvy; i++)
                angles.Add(Mathf.Lerp(startAngle, endAngle, i * frac));

            return angles;
        }

        void AddWallQuads(MeshData mesh, List<Vector3> bottomVerts, Vector3 heightDir, bool useNormalizePositionForNormal=false)
        {
            int n = bottomVerts.Count;
            if (n < 2) return;

            List<Vector3> verts = mesh.vertices != null ? new List<Vector3>(mesh.vertices) : new List<Vector3>();
            List<int> tris = mesh.triangles != null ? new List<int>(mesh.triangles) : new List<int>();

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 dir = (bottomVerts[i + 1] - bottomVerts[i]).normalized;
                Vector3 perpendicular2D = -Vector3.Cross(dir, heightDir).normalized;
                Vector3 perpendicular2D0 = bottomVerts[i].normalized;
                Vector3 perpendicular2D1 = bottomVerts[i+1].normalized;
                if (!useNormalizePositionForNormal)
                {
                    perpendicular2D0 = perpendicular2D;
                    perpendicular2D1 = perpendicular2D;
                }

                Vector3 b0i = bottomVerts[i] - perpendicular2D0 * wallThickness * 0.5f;
                Vector3 b1i = bottomVerts[i + 1] - perpendicular2D1 * wallThickness * 0.5f;
                Vector3 b0o = bottomVerts[i] + perpendicular2D0 * wallThickness * 0.5f;
                Vector3 b1o = bottomVerts[i + 1] + perpendicular2D1 * wallThickness * 0.5f;

                Vector3 t0i = b0i + heightDir * wallHeight;
                Vector3 t1i = b1i + heightDir * wallHeight;
                Vector3 t0o = b0o + heightDir * wallHeight;
                Vector3 t1o = b1o + heightDir * wallHeight;

                int start = verts.Count;

                // FRONT (inner)
                verts.AddRange(new[] { b0i, b1i, t1i, t0i });
                //tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                start += 4;

                // BACK (outer)
                verts.AddRange(new[] { b1o, b0o, t0o, t1o });
                //tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                start += 4;
                if (i == 0)
                {
                    // LEFT
                    verts.AddRange(new[] { b0o, b0i, t0i, t0o });
                    //tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                    tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    start += 4;
                }
                if (i == n - 2)
                {
                    // RIGHT
                    verts.AddRange(new[] { b1i, b1o, t1o, t1i });
                    //tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                    tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    start += 4;
                }
                // TOP
                verts.AddRange(new[] { t0i, t1i, t1o, t0o });
                //tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                start += 4;

                // BOTTOM
                verts.AddRange(new[] { b0o, b1o, b1i, b0i });
                //tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris);
        }

        void ReorientAllVerts(MeshData mesh, Quaternion orientataion)
        {
            for (int i = 0; i < mesh.vertexCount; i++)
            {

                Vector3 vert = mesh.vertices[i];
                vert = orientataion * vert;
                mesh.vertices[i] = vert;
            }
        }
    }


}