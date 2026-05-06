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

        protected override Chunker<RadialCoord> GetChunker(int trisPerWall = 12, int idealTrisPerChunk = 1000)
        {
            return new RadialChunker(mazeSize, maze as  MazeMapRadial,trisPerWall, idealTrisPerChunk);
        }
        protected override WallMeshChunkComputerGeneric<RadialCoord> GetNewMeshComputer()
        {
            return new RadialMazeMeshGenerator(30);//  
        }
    }
    public class RadialChunker : Chunker<RadialCoord>
    {
        MazeMapRadial mapRef;
        public RadialChunker(RadialCoord size, MazeMapRadial mapRef, int trisPerWall = 12, int idealTrisPerChunk = 1000) : base(size, trisPerWall, idealTrisPerChunk)
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
            radialMap = map as MazeMapRadial;
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
        async public /*override*/ UniTask<List<MeshData>> NOTCreateWallsMeshChunksAsync(
                GenericMazeMap<RadialCoord> data,
                MazeMeshDrawGeneric<RadialCoord> mazeDrawer,
                MazeMeshDrawBase.WallDrawConfig drawConfig,
                IReadOnlyList<IReadOnlyList<RadialCoord>> coordsPerChuck,
                TaskHandler taskContext)
        {

            this.map = data;
            radialMap = map as MazeMapRadial;
            this.mazeDrawer = mazeDrawer;
            radialDrawer = mazeDrawer as MazeMeshDrawRadial;
            this.drawConfig = drawConfig;
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

        protected override int GetMinimalUVxSegements()
        {
            return curvy;
        }
        static float MidAngleTurns(float angle1, float angle2)
        {
            float angleDiff = angle2 - angle1;

            if (angleDiff > 0.5f) angleDiff -= 1f;
            else if (angleDiff < -0.5f) angleDiff += 1f;

            float mid = angle1 + angleDiff * 0.5f;

            if (mid < 0f) mid += 1f;
            else if (mid >= 1f) mid -= 1f;

            return mid;
        }
        static bool IsCounterClockwiseOf(float contextAngle, float angleToCheck)
        {
            float d = angleToCheck - contextAngle;

            if (d > 0.5f) d -= 1f;
            else if (d < -0.5f) d += 1f;

            return d > 0f;
        }
        Vector3 Vector2ToMazePlanePosition(Vector2 point)
        {
            Vector3 right = radialMap.MazeOrientation * Vector3.right;
            Vector3 up = radialMap.MazeOrientation * Vector3.up;
            return right * point.x + up * point.y;
        }
        protected override Vector3 ComputeCornerPos(RadialCoord coord, int neighborIndex)
        {
            //return base.ComputeCornerPos(coord, neighborIndex);
            radialMap = map as MazeMapRadial;
            RadialCoord neighborCoord = coord.GetSpatialNeighbor(neighborIndex);
            float radiusToUse;
            Vector2 angleDir;
            float coordRingTileWidthInTurns = 1f / radialMap.SectorsAtRing(coord.ring);
            float neighborRingTileWidthInTurns = 1f / radialMap.SectorsAtRing(neighborCoord.ring);
            float coordStartAngleInTurns = coord.AngleInTurns - coordRingTileWidthInTurns * 0.5f;
            float coordEndAngleInTurns = coord.AngleInTurns + coordRingTileWidthInTurns * 0.5f;
            if (neighborCoord.ring == coord.ring)
            {
              //  float edgeAngleInTurns = MidAngleTurns(coord.AngleInTurns, neighborCoord.AngleInTurns);
                float radiusInner = radialMap.RingInnerRadius(coord.ring);//(tile.ring) * radialDrawer.radialSpacing;
                float radiusOuter = radialMap.RingOuterRadius(coord.ring);// (tile.ring+1) * radialDrawer.radialSpacing;
                radiusToUse = radiusOuter;
                float angleToUse2 = coordEndAngleInTurns;
                if (!IsCounterClockwiseOf(coord.AngleInTurns, neighborCoord.AngleInTurns))
                {
                    radiusToUse = radiusInner;
                    angleToUse2 = coordStartAngleInTurns;
                }
                angleDir = RadialCoord.GetRadialDirection(angleToUse2) * radiusToUse;
                Vector3 result = Vector2ToMazePlanePosition(angleDir);
                Debug.Log("Generated Corner same ring radial edge (angle: " + angleToUse2 + ")   for coord:" + coord + "(angle: "+coord.AngleInTurns+") with neighborIndex: " + neighborIndex + " (" + neighborCoord + "- angle: "+ neighborCoord.AngleInTurns+") : pos " + result);
                return result;
            }


            float neighborStartAngleInTurns = neighborCoord.AngleInTurns - neighborRingTileWidthInTurns * 0.5f;
            float neighborEndAngleInTurns = neighborCoord.AngleInTurns + neighborRingTileWidthInTurns * 0.5f;
            /*
            float coordStart = IsCounterClockwiseOf(coordStartAngleInTurns, coordEndAngleInTurns) ? coordStartAngleInTurns : coordEndAngleInTurns;
            float neighborStart = IsCounterClockwiseOf(coordStartAngleInTurns, neighborEndAngleInTurns) ? coordStartAngleInTurns : neighborEndAngleInTurns;
            //lesser of the two most counter-clockwise angles
            float overlapStart = !IsCounterClockwiseOf(neighborStartAngleInTurns, neighborStartAngleInTurns)? neighborStartAngleInTurns : neighborStartAngleInTurns;
            //lesser of the two most clockwise angles
            float overlapEnd = IsCounterClockwiseOf(coordEndAngleInTurns, neighborEndAngleInTurns) ? coordEndAngleInTurns : neighborEndAngleInTurns;
            */
            float angleToUse;
            if (coord.ring > neighborCoord.ring)
            {
                radiusToUse = radialMap.RingInnerRadius(coord.ring);
                angleToUse = coordEndAngleInTurns;
            }
            else
            {
                radiusToUse = radialMap.RingOuterRadius(coord.ring);
                angleToUse = coordStartAngleInTurns;
            }

            if (coord.ring != 0)
                angleDir = RadialCoord.GetRadialDirection(angleToUse) * radiusToUse;
            else// ring 0 only has one tile which start and end at rotation 0 and 1, but we want corners with ring 1 for edges
                angleDir = RadialCoord.GetRadialDirection(neighborStartAngleInTurns) * radiusToUse;
            Vector3 result2 = Vector2ToMazePlanePosition(angleDir);
            Debug.Log("Generated Corner for coord:" + coord + " with neighborIndex: " + neighborIndex + " (" + coord.GetSpatialNeighbor(neighborIndex) + ") : pos " + result2);
            return result2;

        }
        protected override Vector3 EdgeDirFrom(Edge e, int cornerIndex)//override for radial
        {
            RadialCoord sideA = e.sideACoord;
            RadialCoord sideB = e.sideBCoord;
            if(sideA.ring == sideB.ring)// straight line
            {
                return base.EdgeDirFrom(e, cornerIndex);
            }
            if (e.cornerA == cornerIndex)
            {
                Vector3 perp = Vector3.Cross(uniqueCorners[e.cornerA].position, mazeDrawer.drawConfig.mazeNormal);
                if (Vector3.Dot(perp, (uniqueCorners[e.cornerB].position - uniqueCorners[e.cornerA].position)) < 0f)
                    perp = -perp;
                return perp.normalized;
            }
            if (e.cornerB == cornerIndex)
            {
                Vector3 perp = Vector3.Cross(uniqueCorners[e.cornerB].position, mazeDrawer.drawConfig.mazeNormal);
                if (Vector3.Dot(perp, (uniqueCorners[e.cornerA].position - uniqueCorners[e.cornerB].position)) < 0f)
                    perp = -perp;
                return perp.normalized;
            }
            throw new System.Exception("Invalid corner index (" + cornerIndex + ")passed to EdgeDirFrom.  Edge only contains indexes " + e.cornerA + " and " + e.cornerB);

        }

        protected override Vector3 GetPointOnWallFace(Vector3 bottomLeft, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight, Vector2 uv, RadialCoord sideACoord, RadialCoord sideBCoord)
        {
            if (sideACoord.ring == sideBCoord.ring) 
                return base.GetPointOnWallFace(bottomLeft, topLeft, topRight, bottomRight, uv, sideACoord, sideBCoord);
            return GetPointOnCurvedRectange(bottomLeft, topLeft, topRight, bottomRight, uv);
            //return GetPointOnCurvedRectange(p00, p10, p01, p11, uv);
        }
        protected override Vector3 GetPointInWall(Vector3 normalizedPoint,
            Vector3 startBottomLeft, Vector3 startTopLeft, Vector3 startTopRight, Vector3 startBottomRight,
            Vector3 endBottomLeft, Vector3 endTopLeft, Vector3 endTopRight, Vector3 endBottomRight, 
            RadialCoord sideACoord, RadialCoord sideBCoord)
        {
            if (sideACoord.ring == sideBCoord.ring) 
                return base.GetPointInWall(normalizedPoint,
                                         startBottomLeft, startTopLeft, startTopRight, startBottomRight,
                                         endBottomLeft, endTopLeft, endTopRight, endBottomRight, sideACoord, sideBCoord);
            return InterpolateCurvedWallPoint(normalizedPoint,
                                             startBottomLeft, startTopLeft, startTopRight, startBottomRight,
                                             endBottomLeft, endTopLeft, endTopRight, endBottomRight);
        }

        protected override (int, int) MapFuncGetCornerIndexesForEdge(RadialCoord sideA, RadialCoord sideB)
        {
            //for radial maps,
            //we can't use the below basic method because we assume corners match up, specifically, radial map tiles MAY have more neighbors than corners.
            // (except for the center tile)
            //   - all tiles in a radial map will have four corners.
            //   - if the next outward ring doubles the number of tiles in a ring (happens according to formula) then the tile will have FIVE neighbors rather than the usual four- (even though it still only has four corners).
            //so, for a radial map, we need to find which of the corners to use..
            //   - if the next ring does NOT double- we can use the existsing formula
            //   - if it DOES double new ring- we'll need to figure which of the neighboring corners to use, and which of this tiles corners to use (we'll need one from each, or, upon reflection, both from neighbor)
            //      - note that even in this case ONE of the corners WILL still be shared.

            int sideBNeighborIndex = map.GetSpatialNeighborIndexOf(sideA, sideB);
            MazeMapRadial radial = map as MazeMapRadial;

            int sectorsA = radial.SectorsAtRing(sideA.ring);
            int sectorsB = radial.SectorsAtRing(sideB.ring);

            bool sameRing = sideA.ring == sideB.ring;

            // --- SAME RING: unchanged ---
            if (sameRing)
            {
                int cornerA = cornerIndecesByCoordinate[sideA][sideBNeighborIndex];
                int cornerB = cornerIndecesByCoordinate[sideA][
                    (sideBNeighborIndex + 1).RingIndex(sideA.NumberOfNeighbors())
                ];
                return (cornerA, cornerB);
            }

            // --- RING TRANSITION ---
            bool doublingOutward = (sectorsB == sectorsA * 2) || (sideA.ring == 0);
            int cornerIndexA;
            int cornerIndexB;
            // --- NORMAL (no doubling): unchanged ---
            if (!doublingOutward)
            {
                cornerIndexA = cornerIndecesByCoordinate[sideA][sideBNeighborIndex];
                cornerIndexB = cornerIndecesByCoordinate[sideA][
                    (sideBNeighborIndex + 1).RingIndex(sideA.NumberOfNeighbors())
                ];
            }
            else
            {
                // --- DOUBLING CASE ---
                // Map neighbor index ? one of 4 corners
                // Assumption: neighbor indices for radial connections are contiguous pairs
                // sideA has fewer sectors, neighbor splits into two? use sideB corners
                int sideANeighborIndex = map.GetSpatialNeighborIndexOf(sideB, sideA);
                cornerIndexA = cornerIndecesByCoordinate[sideB][sideANeighborIndex];
                cornerIndexB = cornerIndecesByCoordinate[sideB][
                    (sideANeighborIndex + 1).RingIndex(sideB.NumberOfNeighbors())
                ];
            }
            return (cornerIndexA, cornerIndexB);
        }
        public /*override*/ Mesh NOTRebuildSingleChunk(int chunk)
        {
            chunkMeshes[chunk] = GenerateChunkMesh(coordsPerChuck[chunk], new TaskHandler(false)).AsTask().GetAwaiter().GetResult();
            return chunkMeshes[chunk].ToMesh();
        }

        async  UniTask<MeshData> GenerateChunkMesh(IReadOnlyList<RadialCoord> chunkTiles,TaskHandler taskContext)
        {
            MeshData mesh = new MeshData();
            MazeMap2D<RadialCoord> mazeMap2D = mazeDrawer.maze as MazeMap2D<RadialCoord>;
            Vector3 right = mazeMap2D.MazeOrientation * Vector3.right;
            Vector3 up =mazeMap2D.MazeOrientation * Vector3.up;

            foreach (RadialCoord tile in chunkTiles)
            {
                int neighborsCount = tile.NumberOfNeighbors();

                for (int ni = 0; ni < neighborsCount; ni++)
                {
                    RadialCoord neighbor = tile.GetSpatialNeighbor(ni);
                    bool inBounds = map.IsWithinBounds(neighbor);

                    // skip entirely if out-of-bounds and not drawing borders
                    if (!inBounds && !drawConfig.drawBorderWalls) continue;

                    bool hasWall = (!inBounds) || map.Walls[tile][ni];
                    if (!hasWall)
                    {
                        continue;
                    }
                    if (!isTileVisible(tile) && ((!inBounds&& drawConfig.drawBorderWalls) || !isTileVisible(neighbor))) continue;

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
            radiusInner -= drawConfig.wallThickness * 0.5f;
            radiusOuter += drawConfig.wallThickness * 0.5f;


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
            //segementAngleLengthInTurns *=  mazeDrawer.wallWidthFraction;
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
            List<int> tris = mesh.triangles != null && mesh.triangles[0] != null ? new List<int>(mesh.triangles[0]) : new List<int>();
            List<Vector2> uvs = mesh.meshUV0s != null  ? new List<Vector2>(mesh.meshUV0s) : new List<Vector2>();
            float oneOverN = 1f / (float)(n-1);
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

                Vector3 b0i = bottomVerts[i] - perpendicular2D0 * drawConfig.wallThickness * 0.5f;
                Vector3 b1i = bottomVerts[i + 1] - perpendicular2D1 * drawConfig.wallThickness * 0.5f;
                Vector3 b0o = bottomVerts[i] + perpendicular2D0 * drawConfig.wallThickness * 0.5f;
                Vector3 b1o = bottomVerts[i + 1] + perpendicular2D1 * drawConfig.wallThickness * 0.5f;

                Vector3 t0i = b0i + heightDir * drawConfig.wallHeight;
                Vector3 t1i = b1i + heightDir * drawConfig.wallHeight;
                Vector3 t0o = b0o + heightDir * drawConfig.wallHeight;
                Vector3 t1o = b1o + heightDir * drawConfig.wallHeight;
                float u0 = i * oneOverN;
                float u1 = (i + 1) * oneOverN;
                Vector2 uv00 = new Vector2(u0,0);
                Vector2 uv01 = new Vector2(u0, 1);
                Vector2 uv11 = new Vector2(u1, 0); 
                Vector2 uv10 = new Vector2(u1, 1);

                int start = verts.Count;

                // FRONT (inner)
                verts.AddRange(new[] { b0i, b1i, t1i, t0i });
                tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                uvs.AddRange(new[] { uv00, uv10, uv11, uv01 });
                start += 4;

                // BACK (outer)
                verts.AddRange(new[] { b1o, b0o, t0o, t1o });
                tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                uvs.AddRange(new[] { uv00, uv10, uv11, uv01 });
                start += 4;
                if (i == 0)
                {
                    // LEFT
                    verts.AddRange(new[] { b0o, b0i, t0i, t0o });
                    tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    uvs.AddRange(new[] { uv00, uv10, uv11, uv01 });
                    start += 4;
                }
                if (i == n - 2)
                {
                    // RIGHT
                    verts.AddRange(new[] { b1i, b1o, t1o, t1i });
                    tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    uvs.AddRange(new[] { uv00, uv10, uv11, uv01 });
                    start += 4;
                }
                // TOP
                verts.AddRange(new[] { t0i, t1i, t1o, t0o });
                tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                uvs.AddRange(new[] { uv00, uv10, uv11, uv01 });
                start += 4;

                // BOTTOM
                verts.AddRange(new[] { b0o, b1o, b1i, b0i });
                tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                uvs.AddRange(new[] { uv00, uv10, uv11, uv01 });
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris);
            mesh.SetUVs(0, uvs);
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