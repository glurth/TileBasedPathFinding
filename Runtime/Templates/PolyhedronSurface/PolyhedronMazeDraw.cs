using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EyE.UnityAssetTypes;

namespace Eye.Maps.Templates
{
    public class PolyhedronMazeDraw : MazeMeshDrawGeneric<FaceCoordinate>
    {

        public FacesAndNeighbors facesAndNeighbors=> ((FaceMazeMap)maze).sourceMap;

        public FacesAndNeighbors createOnEnableSourceFacesAndNeighbors;
        protected override GenericMazeMap<FaceCoordinate> CreateMazeMap()
        {
            FaceMazeMap maze = new FaceMazeMap(createOnEnableSourceFacesAndNeighbors);
            maze.GenerateMaze();
            return maze;
        }
        
        protected override FaceCoordinate DefaultMazeSize()
        {
            return new FaceCoordinate(facesAndNeighbors,facesAndNeighbors.faceDetails.Count);// facesAndNeighbors.faceDetails[facesAndNeighbors.faceDetails.Count - 1].neighborIndices, facesAndNeighbors.faceDetails[facesAndNeighbors.faceDetails.Count - 1].normal);
        }
        /*
        int[] meshIndexToUniqueID;
        Mesh lastMesh = null;
        void PopulateMeshIndexToUniqueID()
        {
            if (lastMesh == facesAndNeighbors.meshRef) return;
            Mesh mesh = facesAndNeighbors.meshRef;
            lastMesh = mesh;
            Dictionary<Vector3, int> uniqueVerts = new Dictionary<Vector3, int>();
            FaceMazeMap faceMaze = maze as FaceMazeMap;
            meshIndexToUniqueID = new int[faceMaze.AsyncUsableVertexList.Length];
            //meshIndexToUniqueID = new int[mesh.vertexCount];
            int uniqueIndexCounter = 0;

            for (int i = 0; i < faceMaze.AsyncUsableVertexList.Length; i++)
            {
                Vector3 v = faceMaze.AsyncUsableVertexList[i];
                if (!uniqueVerts.TryGetValue(v, out int uniqueIdx))
                {
                    uniqueIdx = uniqueIndexCounter++;
                    uniqueVerts[v] = uniqueIdx;
                }
                meshIndexToUniqueID[i] = uniqueIdx;
            }
        }
        protected override Matrix4x4 GetNeighborWallMatrix(FaceCoordinate coord, int neighborIndex, Vector3 tilePosition, int neighborCount)
        {
            //we will use facesAndNeighbors to get ref to mesh, and vertex index of two coners these neighbors touch. (note the faces will NOT share the same vertex index, but they will have the same 3d value [cuz normals per face])
            //then using these as the endpoints, we will generate a maxtrix for a box, scaled to be thin along the line between them, and normal/flush with the face.
            //the cube will be drawn on both sides/faces and should be offset such that they will exactly touch at the apex of where they meet (and overlap beneath)
            // float wallThicknessFraction, wallHeightFraction will define how thick/deep and tall the line should be, as a fraction of it's length
            PopulateMeshIndexToUniqueID();//returns if done already
            Mesh mesh = facesAndNeighbors.meshRef;
            FaceCoordinate neighbor = coord.GetNeighbor(neighborIndex);
            FaceDetails coordFaceDetails = facesAndNeighbors.faceDetails[coord.faceIndex];
            FaceDetails neighborFaceDetails = facesAndNeighbors.faceDetails[neighbor.faceIndex];
            List<Vector3> endpoints = new List<Vector3>(2);
            HashSet<int> neighborUniqueVertIDs = new HashSet<int>();
            foreach (int neighborCornerVertIndex in neighborFaceDetails.cornerVertexMeshIndices)
            {
                neighborUniqueVertIDs.Add(meshIndexToUniqueID[neighborCornerVertIndex]);
            }
            FaceMazeMap faceMaze = maze as FaceMazeMap;
            foreach (int faceCornerVertIndex in coordFaceDetails.cornerVertexMeshIndices)
            {   
                int cornerUniqueID = meshIndexToUniqueID[faceCornerVertIndex];
                if (neighborUniqueVertIDs.Contains(cornerUniqueID))
                    endpoints.Add(faceMaze.AsyncUsableVertexList[faceCornerVertIndex]);// mesh.vertices[faceCornerVertIndex]);

                if (endpoints.Count > 1) break;
            }
            if (endpoints.Count < 2) throw new System.Exception("Unexpected processing- unable to find matching corners for faces: [" + coord + "] ,[" + neighbor + "]");
            Vector3 edge = (endpoints[0] - endpoints[1]);
            Vector3 edgeDir = edge.normalized;
            Vector3 edgeCenterPos = (endpoints[0] + endpoints[1]) / 2f;
            Vector3 edgeNormal = (coordFaceDetails.normal + neighborFaceDetails.normal) * 0.5f;
            if (Mathf.Abs(Vector3.Dot(edgeNormal, edgeDir)) > 0.03f)
                Debug.LogWarning("Error: edge and edge normal are not perpendicular");
            // Debug.Log("Edge Normal:" + edgeNormal + "  Edge dir:" + edgeDir  + "Edge:" + edge);

            Quaternion wallRot = maze.NeighborBorderOrientation(coord, neighborIndex);//  Quaternion.LookRotation(edgeDir, edgeNormal); //rotates forward (z-axis) to look down length of edge, with up pointing directly away from face.
            wallRot = Quaternion.LookRotation(edgeNormal, Vector3.Cross(edgeDir,edgeNormal)); //rotates forward (z-axis) to look down length of edge, with up pointing directly away from face.
            float edgeLen = edge.magnitude;
            Vector3 wallScale = new Vector3(1, wallThicknessFraction, wallHeightFraction) * edgeLen;
            return Matrix4x4.TRS(edgeCenterPos, wallRot, wallScale);

        }*/



        protected override void BuildChucks()
        {
            chunkHandler = new FaceChunker(numberOfChunks, mazeSize,facesAndNeighbors);
            chunkHandler.Build();

        }
        protected override WallMeshChunkComputerGeneric<FaceCoordinate> GetNewMeshComputer()
        {
            return  new FaceWallMeshChunkComputerGeneric();
        }

        /*public int numChunks=1;
        public List<MeshFilter> wallChunkMeshFilters;
        
        WallMeshChunkComputer meshComputer = null;
       
        protected override void GenerateMesh()
        {
            Debug.Log("Setting visiblity for all tiles: " + !startHidden);
            foreach (FaceCoordinate coord in maze.allMapCoords)
            {
                SetTileVisibility(coord, !startHidden);
            }

            //InitializeChunks();
            meshComputer = new WallMeshChunkComputer();
            List<Mesh> chunkMeshes = meshComputer.CreateWallsMeshChunks(facesAndNeighbors, this, tileScale * wallThicknessFraction, tileScale * wallHeightFraction, chunkHandler.ChunkCoordinateLists());
            if (chunkMeshes.Count != numChunks) throw new System.Exception("Failed to generate correct number of chunk meshes- aborting assignment.");
            if (wallChunkMeshFilters.Count != numChunks) throw new System.Exception("Incorrect number of mesh filters to assign chunk meshes to, aborting assignment.");
            for (int i = 0; i < wallChunkMeshFilters.Count; i++)
            {
                wallChunkMeshFilters[i].sharedMesh = chunkMeshes[i];//  should be equal in len, will throw if not
            }
            //WallMeshComputer meshComputer = new  WallMeshComputer();
            //wallsMeshFilter.sharedMesh = meshComputer.CreateWallsMesh(facesAndNeighbors, this,tileScale* wallThicknessFraction, tileScale * wallHeightFraction); 
        }
        protected override void RegenChunkMesh(int chunkIndex)
        {
            wallChunkMeshFilters[chunkIndex].sharedMesh = meshComputer.RebuildSingleChunk(chunkIndex);
        }
        */
    }

    public class FaceChunker : Chunker<FaceCoordinate>
    {
        FacesAndNeighbors map;
        public FaceChunker(int numChunks, FaceCoordinate size, FacesAndNeighbors map) : base(numChunks, size)
        {
            this.map = map;
        }

        protected override List<List<FaceCoordinate>> GenerateChunks(int numChunks, FaceCoordinate size)
        {
            int faceCount = map.faceDetails.Count;
            List<Vector3> normals = new List<Vector3>(faceCount);
            for (int i = 0; i < faceCount; i++)
                normals.Add(map.faceDetails[i].normal);

            // K-means clustering on normals
            List<Vector3> centroids = new List<Vector3>();
            System.Random rand = new System.Random();
            HashSet<int> used = new HashSet<int>();
            // Initialize centroids randomly
            while (centroids.Count < numChunks)
            {
                int pick = rand.Next(faceCount);
                if (!used.Contains(pick))
                {
                    centroids.Add(normals[pick].normalized);
                    used.Add(pick);
                }
            }

            int maxIter = 25;
            List<int>[] clusters = new List<int>[numChunks];
            for (int i = 0; i < numChunks; i++)
                clusters[i] = new List<int>();

            for (int iter = 0; iter < maxIter; iter++)
            {
                // Clear clusters
                for (int i = 0; i < numChunks; i++) clusters[i].Clear();

                // Assign each face to the nearest centroid
                for (int f = 0; f < faceCount; f++)
                {
                    float bestDot = -2f;
                    int bestC = -1;
                    for (int c = 0; c < numChunks; c++)
                    {
                        float dot = Vector3.Dot(normals[f].normalized, centroids[c]);
                        if (dot > bestDot)
                        {
                            bestDot = dot;
                            bestC = c;
                        }
                    }
                    clusters[bestC].Add(f);
                }

                // Update centroids
                for (int c = 0; c < numChunks; c++)
                {
                    if (clusters[c].Count == 0) continue; // Avoid divide by zero
                    Vector3 avg = Vector3.zero;
                    foreach (int f in clusters[c])
                        avg += normals[f].normalized;
                    avg.Normalize();
                    centroids[c] = avg;
                }
            }

            // Return the clusters as chunks
            List<List<FaceCoordinate>> result = new List<List<FaceCoordinate>>(numChunks);
            for (int i = 0; i < numChunks; i++)
            {
                List<int> clusterFaceIndexes = clusters[i];
                List<FaceCoordinate> clusterFaces = new List<FaceCoordinate>();
                foreach (int idx in clusterFaceIndexes)
                    clusterFaces.Add(new FaceCoordinate(map, idx));
                result.Add(clusterFaces);
            }
            return result;
        }
    }



    public class FaceWallMeshChunkComputerGeneric : WallMeshChunkComputerGeneric<FaceCoordinate>
    {
        protected override Vector3 NormalAtCoord(FaceCoordinate coord) { return map.GetModelSpacePosition(coord).normalized; }// coord.modelspace positon, normalized for faces
        protected override Vector3 NormalAtModelSpacePosition(Vector3 pos) { return pos.normalized; }//  vector normalized for faces
        protected override Vector3 ComputeCornerPos(FaceCoordinate coord, int neighborIndex)// use mesh verticies for faces
        {
            FacesAndNeighbors fn = ((FaceMazeMap)map).sourceMap;
            Mesh mesh = fn.meshRef;
            FaceDetails face=fn.faceDetails[coord.faceIndex];
            return mesh.vertices[face.cornerVertexMeshIndices[neighborIndex]];
        }
    }

    public class OLDWallMeshComputer
    {
        // Maze Mesh Construction Plan (with unique corners and edges)
        //
        // Step 1: Build Unique Corner List
        // ----------------------------------
        // - Create a List<Vector3> uniqueCorners
        // - Create a List<int> oldToUniqueCornerMap= init to size of old corners
        // - For each FaceDetails F in FacesAndNeighbors:
        //     - For each index in F.cornerVertexMeshIndices:
        //         - If vertex not in uniqueCorners:
        //             - Add to uniqueCorners
        //         - Map original index to new unique index in oldToUniqueCornerMap
        //
        // Step 2: Build Unique Edges and Wall Visibility
        // ----------------------------------------------
        // - Create List<Edge> uniqueEdges
        // - For each FaceDetails F (index fIndex):
        //     - Convert F.cornerVertexMeshIndices to unique corner indices using oldToUniqueCornerMap
        //     - For each corner in face, record fIndex in the corner’s touchingFaces (if you're tracking per-corner data)
        //     - For i = 0 to corner count - 1:
        //         - cornerA = uniqueCornerIndices[i]
        //         - cornerB = uniqueCornerIndices[(i + 1) % count]
        //         - edge = TryGetOrCreateUniqueEdge(cornerA,cornerB);
        //         - add face to edge.touchingfaces
        //         - Set edge.hasVisibleWall = GetWallVisible(fIndex, neighborFaceIndex)
        //         - add edge to cornerA/B.edges, if not present

        //
        // Step 3: Sort Edges Around Each Corner 
        // -----------------------------------------------------------------------------
        // - For each uniqueCorner:
        //     - sort corners.edgelist by
        //        - a float: compute edge's angle around normal axis
        //
        // Step 4: Generate vertex positions (use new param- wallThickness to compute end-side points (front to back thickness) )
        // ----------------------------------------------------
        // - For each uniqueCorner:
        //      - If only one edge is visible:
        //         - Compute two end-side points and store in edge.wallEndVerts[A or B][left and right]
        //     - If multiple visible edges:
        //          - For each visible pair of edges(A, B) at corner:
        //              - Compute dirA and dirB (unit vectors away from corner)
        //              - Compute bisector = normalize(dirA + dirB)
        //              - Compute angle = angle between dirA and dirB
        //              - Compute offset = wallThickness / sin(angle / 2)
        //              - fanRing[i] = corner + bisector * offset
        //          - Store N(number of visible edges) such points in clockwise order → corner.fanRing[]
        //          - if MORE than 2 visible edges- compute the TIP point location
        //          - duplicate, fanRing but extrude into:  fanRingTopVerts
        //          - duplicate, Tip (if exists) but extrude into:  tipTopVert
        //          - Each edge gets assigned edge.wallEndVerts[A or B][left and right]  (left = fanRing[i], right = fanRing[i+1], and the TIP Vector3 or null),
        //                 -both top and bottom

        // Step 5: Generate Wall Geometry
        // ------------------------------
        // - For each Edge in uniqueEdges:
        //     - If edge.hasVisibleWall == false, skip
        //     - Use edge.wallEndVerts[A and B][left and right] to emit a quad (bottom of wall)
        //     - Use edge.wallEndTopVerts[A and B][left and right] to emit a quad (top of wall)
        //     - use combos of top and bottom to generate front and back of wall
        //     - for each end A and B
        //         - if TIP point exists edge.wallEndVerts[A or B][tip]
        //             - create triangle using edge.wallEndVerts[A or B][left and right and tip] (bottom of miter) // but wound properly
        //             - create triangle using edge.wallEndTopVerts[A or B][left and right and tip] (top of miter)
        //         - if no 3rd point
        //             - create end-cap quad with combos of top bottom

        /*input values*/
        FacesAndNeighbors data;
        PolyhedronMazeDraw mazeDrawer;
        float wallThickness;
        float wallHeight;

        public Mesh CreateWallsMesh(
                FacesAndNeighbors data,
                PolyhedronMazeDraw mazeDrawer,
                float wallThickness,
                float wallHeight)
        {
            this.data = data;
            this.mazeDrawer = mazeDrawer;
            this.wallThickness = wallThickness;
            this.wallHeight = wallHeight;
            Debug.Log("Wall mesh computer running now");
            BuildUniqueCorners();
            BuildUniqueEdges();
            SortCornerEdgesClockwise();
            GenerateVertexPositions();
            return GenerateWallModel();
        }

        //internal storage
        List<Corner> uniqueCorners = new List<Corner>();
        List<int> oldToNewCornerIndex = new List<int>();
        List<Edge> uniqueEdges = new List<Edge>();

        //internal structures
        class Corner
        {
            public Vector3 position;
            public List<int> edgeTo;  //indexes into the uniqueCorners list
            public List<int> edges;  //indexes into the uniqueEdges list
            public List<int> sortedEdges;  //indexes into the edges list
            public Vector3[] fanRing;
            public Vector3? tipVert;
        }
        class Edge
        {
            public int cornerA;
            public int cornerB;
            //   public List<int> touchingFaces;
            public bool hasVisibleWall;
            public Vector3[] wallEndAVerts;
            public Vector3[] wallEndATopVerts;
            public Vector3? wallEndATipVert;
            public Vector3? wallEndATopTipVert;
            public Vector3[] wallEndBVerts;
            public Vector3[] wallEndBTopVerts;
            public Vector3? wallEndBTipVert;
            public Vector3? wallEndBTopTipVert;
        }
        //utility functions
        Vector3 EdgeDir(Edge e)// from A to B
        {
            return (uniqueCorners[e.cornerB].position - uniqueCorners[e.cornerA].position).normalized;
        }
        Vector3 EdgeDirFrom(Edge e, int cornerIndex)
        {
            if (e.cornerA == cornerIndex)
                return (uniqueCorners[e.cornerB].position - uniqueCorners[e.cornerA].position).normalized;
            //assume corner B
            if (e.cornerB == cornerIndex)
                return (uniqueCorners[e.cornerA].position - uniqueCorners[e.cornerB].position).normalized;
            throw new System.Exception("Invalid corner index (" + cornerIndex + ")passed to EdgeDirFrom.  Edge only contains indexes " + e.cornerA + " and " + e.cornerB);
        }
        static float AngleAroundAxis(Vector3 v, Vector3 axis, Vector3 refDir)
        {
            axis = axis.normalized;


            // Project v onto plane orthogonal to axis
            Vector3 p = v - Vector3.Dot(v, axis) * axis;
            if (p == Vector3.zero) return 0f;

            p.Normalize();

            Vector3 cross = Vector3.Cross(refDir, p);
            float sin = Vector3.Dot(cross, axis);
            float cos = Vector3.Dot(refDir, p);

            return Mathf.Atan2(sin, cos); // radians, signed
        }
        void AssignToEdge(Edge e, int whichCornerIndex, Vector3 wallEndVertFront, Vector3 wallEndVertBack, Vector3? tip)
        {
            if (e.cornerA == whichCornerIndex)
            {
                e.wallEndAVerts = new Vector3[] { wallEndVertFront, wallEndVertBack };
                e.wallEndATopVerts = new Vector3[] { Extrude(wallEndVertBack), Extrude(wallEndVertFront) };
                e.wallEndATipVert = tip;
                if (tip == null) e.wallEndATopTipVert = null;
                else e.wallEndATopTipVert = Extrude(tip.Value);
            }
            else//assumes (e.cornerB == whichCornerIndex)
            {
                e.wallEndBVerts = new Vector3[] { wallEndVertBack, wallEndVertFront };
                e.wallEndBTopVerts = new Vector3[] { Extrude(wallEndVertFront), Extrude(wallEndVertBack) };
                e.wallEndBTipVert = tip;
                if (tip == null) e.wallEndBTopTipVert = null;
                else e.wallEndBTopTipVert = Extrude(tip.Value);
            }
        }
        Vector3 Extrude(Vector3 v)
        {
            return v + (v.normalized * wallHeight);
        }
        bool LineIntersection(Ray LineA, Ray LineB, out Vector3 closestPointOnA, out Vector3 closestPointOnB)
        {
            Vector3 p1 = LineA.origin;
            Vector3 d1 = LineA.direction;
            Vector3 p2 = LineB.origin;
            Vector3 d2 = LineB.direction;

            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);

            float denom = a * e - Vector3.Dot(d1, d2) * Vector3.Dot(d1, d2);
            if (Mathf.Approximately(denom, 0f))
            {
                closestPointOnA = closestPointOnB = Vector3.zero;
                return false; // parallel
            }

            float b = Vector3.Dot(d1, d2);
            float c = Vector3.Dot(d1, r);

            float s = (b * f - c * e) / denom;
            float t = (a * f - b * c) / denom;

            closestPointOnA = p1 + d1 * s;
            closestPointOnB = p2 + d2 * t;
            return true;
        }


        /*primary internal functions*/
        void BuildUniqueCorners()
        {
            // Step 1: Build Unique Corner List
            // ----------------------------------
            // - Create a List<Vector3> uniqueCorners
            // - Create a List<int> oldToUniqueCornerMap= init to size of old corners
            // - For each FaceDetails F in FacesAndNeighbors:
            //     - For each index in F.cornerVertexMeshIndices:
            //         - If vertex not in uniqueCorners:
            //             - Add to uniqueCorners
            //         - Map original index to new unique index in oldToUniqueCornerMap
            Vector3[] meshVertices = data.meshRef.vertices;
            int vertexCount = meshVertices.Length;

            oldToNewCornerIndex = new List<int>(new int[vertexCount]);
            Dictionary<Vector3, int> cornerMap = new();

            for (int i = 0; i < data.faceDetails.Count; i++)
            {
                var face = data.faceDetails[i];
                foreach (int cornerIdx in face.cornerVertexMeshIndices)
                {
                    Vector3 pos = meshVertices[cornerIdx];

                    if (!cornerMap.TryGetValue(pos, out int uniqueIndex))
                    {
                        uniqueIndex = uniqueCorners.Count;
                        uniqueCorners.Add(new Corner { position = pos });
                        cornerMap[pos] = uniqueIndex;
                    }

                    oldToNewCornerIndex[cornerIdx] = uniqueIndex;  //cannot be out of bounds-throw exception if so
                }
            }
        }

        void BuildUniqueEdges()
        {
            // Step 2: Build Unique Edges and Wall Visibility
            // ----------------------------------------------
            // - Create List<Edge> uniqueEdges
            // - For each FaceDetails F (index fIndex):
            //     - Convert F.cornerVertexMeshIndices to unique corner indices using oldToUniqueCornerMap
            //     - For each corner in face, record fIndex in the corner’s touchingFaces (if you're tracking per-corner data)
            //     - For i = 0 to corner count - 1:
            //         - cornerA = uniqueCornerIndices[i]
            //         - cornerB = uniqueCornerIndices[(i + 1) % count]
            //         - edge = TryGetOrCreateUniqueEdge(cornerA,cornerB);
            //         - add face to edge.touchingfaces
            //         - Set edge.hasVisibleWall = GetWallVisible(fIndex, neighborFaceIndex)
            //         - add edge to cornerA/B.edges, if not present

            uniqueEdges = new List<Edge>();
            string logstr = "";
            foreach (FaceCoordinate faceCoord in mazeDrawer.maze.allMapCoords)
            {

                FaceDetails face = data.faceDetails[faceCoord.faceIndex];

                // oldToNewCornerIndex  will map old corner mesh indices to unique corner indices: lookup via uniqueCornerIndex to get original Corner Index
                int cornerCount = face.cornerVertexMeshIndices.Count;
                /*List<int> uniqueCornerIndices = new List<int>(cornerCount);
                for (int i = 0; i < cornerCount; i++)
                {
                    int oldIndex = face.cornerVertexMeshIndices[i];
                    int uniqueIndex = oldToNewCornerIndex[oldIndex];
                    uniqueCornerIndices.Add(uniqueIndex);
                }*/
                int uniqueCornerIndex(int i) => oldToNewCornerIndex[face.cornerVertexMeshIndices[i]];

                for (int i = 0; i < cornerCount; i++)
                {

                    int currentCornerA = uniqueCornerIndex(i);// uniqueCornerIndices[i];
                    int currentCornerB = uniqueCornerIndex((i + 1) % cornerCount);//uniqueCornerIndices[(i + 1) % cornerCount];
                    // see if an edge exists that uses these corners
                    int currentEdgeIndex = uniqueEdges.FindIndex(0, (Edge e) => (e.cornerA == currentCornerA && e.cornerB == currentCornerB) || (e.cornerA == currentCornerB && e.cornerB == currentCornerA));
                    Edge edge;
                    if (currentEdgeIndex == -1)// if not, create one
                    {
                        edge = new Edge() { cornerA = currentCornerA, cornerB = currentCornerB };
                        edge.hasVisibleWall = false;
                        uniqueEdges.Add(edge);
                    }
                    else
                        edge = uniqueEdges[currentEdgeIndex];//if so, get it

                    /*
                    int neighborIndex = -1;// face.neighborIndices[i];
                    FaceCoordinate neighborCoord= faceCoord.GetNeighbor(i);
                    FaceCoordinate[] allNeighborCoords = faceCoord.GetNeighbors();
                    for (int x = 0; x < allNeighborCoords.Length; x++)
                    {
                        if (allNeighborCoords[x] == neighborCoord)
                        {
                            neighborIndex = x;
                            break;
                        }
                    }*/
                    bool hasWall = mazeDrawer.maze.Walls[faceCoord][i];
                    edge.hasVisibleWall = hasWall;//|= hasWall && (mazeDrawer.IsTileVisible(faceCoord) || mazeDrawer.IsTileVisible(faceCoord.GetNeighbor(i)));

                    logstr += ("\nedge between faces " + faceCoord + " and " + faceCoord.GetNeighbor(i) + ", has visible wall: " + edge.hasVisibleWall + "  edge dir: " + EdgeDirFrom(edge, currentCornerA));
                    // Add edge index to corners' edge lists if not already present
                    Corner cornerAObj = uniqueCorners[currentCornerA];
                    if (cornerAObj.edges == null)
                        cornerAObj.edges = new List<int>();
                    int edgeIndex = uniqueEdges.IndexOf(edge);
                    if (!cornerAObj.edges.Contains(edgeIndex))
                        cornerAObj.edges.Add(edgeIndex);

                    Corner cornerBObj = uniqueCorners[currentCornerB];
                    if (cornerBObj.edges == null)
                        cornerBObj.edges = new List<int>();
                    if (!cornerBObj.edges.Contains(edgeIndex))
                        cornerBObj.edges.Add(edgeIndex);

                }
            }
            Debug.Log(logstr);
        }

        //below was used to test and confirm order in facesandneighbor data didn't match coordinate neighbor order- fixed that in generator now this is not needed (and slow)... still keeping for future trouybleshooting if needed
        void BuildUniqueEdgesNew()
        {
            // Step 2: Build Unique Edges and Wall Visibility
            // ----------------------------------------------
            // - Create List<Edge> uniqueEdges
            // - For each FaceDetails F (index fIndex):
            //     - Convert F.cornerVertexMeshIndices to unique corner indices using oldToUniqueCornerMap
            //     - For each corner in face, record fIndex in the corner’s touchingFaces (if you're tracking per-corner data)
            //     - For i = 0 to corner count - 1:
            //         - cornerA = uniqueCornerIndices[i]
            //         - cornerB = uniqueCornerIndices[(i + 1) % count]
            //         - edge = TryGetOrCreateUniqueEdge(cornerA,cornerB);
            //         - add face to edge.touchingfaces
            //         - Set edge.hasVisibleWall = GetWallVisible(fIndex, neighborFaceIndex)
            //         - add edge to cornerA/B.edges, if not present

            uniqueEdges = new List<Edge>();
            string logstr = "";
            foreach (FaceCoordinate faceCoord in mazeDrawer.maze.allMapCoords)
            {

                FaceDetails face = data.faceDetails[faceCoord.faceIndex];
                for (int i = 0; i < faceCoord.NumberOfNeighbors(); i++)
                {
                    logstr += "\nChecking neighbor [" + i + "] of face " + faceCoord + " at check uniqueCornerID:" + oldToNewCornerIndex[face.cornerVertexMeshIndices[i]];
                    // oldToNewCornerIndex  will map old corner mesh indices to unique corner indices: lookup via uniqueCornerIndex to get original Corner Index
                    int cornerCount = face.cornerVertexMeshIndices.Count;
                    FaceCoordinate neighbor = faceCoord.GetNeighbor(i);
                    FaceDetails coordFaceDetails = mazeDrawer.facesAndNeighbors.faceDetails[faceCoord.faceIndex];
                    FaceDetails neighborFaceDetails = mazeDrawer.facesAndNeighbors.faceDetails[neighbor.faceIndex];
                    List<int> endpoints = new List<int>(2);
                    HashSet<int> neighborUniqueCornerIDs = new HashSet<int>();
                    foreach (int neighborCornerVertIndex in neighborFaceDetails.cornerVertexMeshIndices)
                    {
                        neighborUniqueCornerIDs.Add(oldToNewCornerIndex[neighborCornerVertIndex]);
                    }
                    int counterVertexCorner = 0;
                    foreach (int faceCornerVertIndex in coordFaceDetails.cornerVertexMeshIndices)
                    {
                        int cornerUniqueID = oldToNewCornerIndex[faceCornerVertIndex];
                        if (neighborUniqueCornerIDs.Contains(cornerUniqueID))
                        {
                            logstr += "\n   Found neighbor [" + i + "] corner of face " + faceCoord + " at faceDetails corner index: " + counterVertexCorner + " uniqueID:" + cornerUniqueID;
                            endpoints.Add(cornerUniqueID);
                        }
                        counterVertexCorner++;
                        if (endpoints.Count > 1) break;
                    }
                    // we have 2 shared corners now
                    int currentCornerA = endpoints[0];// uniqueCornerIndices[i];
                    int currentCornerB = endpoints[1];//uniqueCornerIndices[(i + 1) % cornerCount];
                    // see if an edge exists that uses these corners
                    int currentEdgeIndex = uniqueEdges.FindIndex(0, (Edge e) => (e.cornerA == currentCornerA && e.cornerB == currentCornerB) || (e.cornerA == currentCornerB && e.cornerB == currentCornerA));
                    Edge edge;
                    if (currentEdgeIndex == -1)// if not, create one
                    {
                        edge = new Edge() { cornerA = currentCornerA, cornerB = currentCornerB };
                        edge.hasVisibleWall = false;
                        uniqueEdges.Add(edge);
                    }
                    else
                        edge = uniqueEdges[currentEdgeIndex];//if so, get it

                    bool hasWall = mazeDrawer.maze.Walls[faceCoord][i];
                    edge.hasVisibleWall = hasWall;//|= hasWall && (mazeDrawer.IsTileVisible(faceCoord) || mazeDrawer.IsTileVisible(faceCoord.GetNeighbor(i)));

                    // logstr += ("\nedge between faces " + faceCoord + " and " + faceCoord.GetNeighbor(i) + ", has visible wall: " + edge.hasVisibleWall + "  edge dir: " + EdgeDirFrom(edge, currentCornerA));
                    // Add edge index to corners' edge lists if not already present
                    Corner cornerAObj = uniqueCorners[currentCornerA];
                    if (cornerAObj.edges == null)
                        cornerAObj.edges = new List<int>();
                    int edgeIndex = uniqueEdges.IndexOf(edge);
                    if (!cornerAObj.edges.Contains(edgeIndex))
                        cornerAObj.edges.Add(edgeIndex);

                    Corner cornerBObj = uniqueCorners[currentCornerB];
                    if (cornerBObj.edges == null)
                        cornerBObj.edges = new List<int>();
                    if (!cornerBObj.edges.Contains(edgeIndex))
                        cornerBObj.edges.Add(edgeIndex);

                }
            }
            Debug.Log(logstr);
        }

        void SortCornerEdgesClockwise()
        {
            //return;
            // Step 3: Sort Edges Around Each Corner 
            // -----------------------------------------------------------------------------
            // - For each uniqueCorner:
            //     - sort corners.edgelist by
            //        - a float: compute edge's angle around normal axis
            for (int cIndex = 0; cIndex < uniqueCorners.Count; cIndex++)
            {
                Corner c = uniqueCorners[cIndex];
                Vector3 axis = c.position.normalized;
                // Choose an arbitrary, but consistent reference direction that is not the same as axis
                Vector3 refDir;
                /*if (Mathf.Abs(axis.z) < 0.99f)
                    refDir = Vector3.Cross(axis, Vector3.forward); // not parallel
                else
                    refDir = Vector3.Cross(axis, Vector3.right);
                refDir.Normalize();
                */
                refDir = EdgeDirFrom(uniqueEdges[c.edges[0]], cIndex);
                c.sortedEdges = new List<int>();// indexes into c.edges
                for (int i = 0; i < c.edges.Count; i++) c.sortedEdges.Add(i);

                c.sortedEdges.Sort(Compare);
                int Compare(int x, int y)
                {
                    Edge edgeX = uniqueEdges[c.edges[x]];
                    Edge edgeY = uniqueEdges[c.edges[y]];
                    Vector3 edgeDirX = EdgeDirFrom(edgeX, cIndex);// (uniqueCorners[edgeX.cornerA].position - uniqueCorners[edgeX.cornerB].position).normalized;
                    Vector3 edgeDirY = EdgeDirFrom(edgeY, cIndex);//(uniqueCorners[edgeY.cornerA].position - uniqueCorners[edgeY.cornerB].position).normalized;
                    float angleX = AngleAroundAxis(axis, edgeDirX, refDir);
                    float angleY = AngleAroundAxis(axis, edgeDirY, refDir);
                    return angleX.CompareTo(angleY);
                }
            }
        }

        void GenerateVertexPositions()
        {
            // Step 4: Generate vertex positions (use new param- wallThickness to compute end-side points (front to back thickness) )
            // ----------------------------------------------------
            // - For each uniqueCorner:
            //      - If only one edge is visible:
            //         - Compute two end-side points and store in edge.wallEndVerts[A or B][left and right]
            //     - If multiple visible edges:
            //          - For each visible pair of edges(A, B) at corner:
            //              - Compute dirA and dirB (unit vectors away from corner)
            //              - Compute bisector = normalize(dirA + dirB)
            //              - Compute angle = angle between dirA and dirB
            //              - Compute offset = wallThickness / sin(angle / 2)
            //              - fanRing[i] = corner + bisector * offset
            //          - Store N(number of visible edges) such points in clockwise order → corner.fanRing[]
            //          - if MORE than 2 visible edges- compute the TIP point location
            //          - duplicate, fanRing but extrude into:  fanRingTopVerts
            //          - duplicate, Tip (if exists) but extrude into:  tipTopVert
            //          - Each edge gets assigned edge.wallEndVerts[A or B][left and right]  (left = fanRing[i], right = fanRing[i+1], and the TIP Vector3 or null),
            //                 -both top and bottom
            for (int cIndex = 0; cIndex < uniqueCorners.Count; cIndex++)
            //foreach (Corner c in uniqueCorners)
            {
                Corner c = uniqueCorners[cIndex];
                if (c.edges.Count == 0) continue;
                List<Edge> visibleEdges = new List<Edge>();

                foreach (int edgeIndex in c.sortedEdges)//.edges)
                {
                    //Edge e = uniqueEdges[edgeIndex];
                    Edge e = uniqueEdges[c.edges[edgeIndex]];
                    if (e.hasVisibleWall)
                    {
                        visibleEdges.Add(e);
                    }

                }

                if (visibleEdges.Count == 1)
                {
                    //compute 2 fanRing verts- assign to single edge-end
                    Edge e = visibleEdges[0];
                    Vector3 thicknessOffset = wallThickness * 0.5f * -Vector3.Cross(c.position.normalized, EdgeDirFrom(e, cIndex)); //assumes spheroid..  todo: change later to param
                    AssignToEdge(e, cIndex, c.position + thicknessOffset, c.position - thicknessOffset, null);
                }
                else if (visibleEdges.Count > 1)
                {
                    c.fanRing = new Vector3[visibleEdges.Count];
                    Vector3 axis = c.position.normalized;// assumes spheroid
                    Vector3 refDir;
                    if (Mathf.Abs(axis.z) < 0.99f)
                        refDir = Vector3.Cross(axis, Vector3.forward); // not parallel
                    else
                        refDir = Vector3.Cross(axis, Vector3.right);
                    refDir.Normalize();

                    Vector3 avgRingPos = Vector3.zero;
                    //compute visibleCount fanRing verts
                    for (int eCounter = 0; eCounter < visibleEdges.Count; eCounter++)
                    {
                        Edge e = visibleEdges[eCounter];
                        Edge nextEdge = visibleEdges.RingIndex(eCounter + 1);
                        //              - Compute dirA and dirB (unit vectors away from corner)
                        Vector3 edgeDir = EdgeDirFrom(e, cIndex);
                        Vector3 nextEdgeDir = EdgeDirFrom(nextEdge, cIndex);




                        /*
                        //              - Compute bisector = normalize(dirA + dirB)
                        Vector3 bisector = (edgeDir + nextEdgeDir).normalized;

                        //              - Compute angle = angle between dirA and dirB
                        float angleE = AngleAroundAxis(edgeDir, axis, refDir);  
                        float angleNext = AngleAroundAxis(nextEdgeDir, axis, refDir);
                        float angleDiff = angleNext - angleE;//refDir is fairly arbitrary (but consistent), so only the DIFFERNCE in angles is relevant
                        if (angleDiff < 0)
                        {
                            //angleDiff = -angleDiff;
                            angleDiff += 2f * Mathf.PI; //ensure positive angle
                        }

                        if (angleDiff > Mathf.PI) //bisector (from above) will always be between smaller angle between edges- if this is the larger angle- reverse it.
                        {
                            angleDiff = 2 * Mathf.PI - angleDiff; // we got the more obtuse angle- we want the acute angle for sin computation below
                            bisector = Quaternion.AngleAxis(Mathf.PI, axis) * bisector;
                            //bisector *= -1;
                        }
                        float halfDiffAngle = angleDiff / 2f;
                        //CAH  cos = adj/hyp  adj= thickness, hyp = bisectorwiseoffset
                        float bisectorWiseOffset = wallThickness / Mathf.Sin(halfDiffAngle);
                        c.fanRing[eCounter] = c.position + (bisector * bisectorWiseOffset);
                        */
                        //bisector stuff working, but only sometimes. we'll try different methods
                        // get line in form of a point and a direction, for both edge's side and next edge's side
                        //compute points on each line where they closest (Ideally same point), and compute the avg position of them

                        Vector3 edgeThicknessOffset = -wallThickness * 0.5f * Vector3.Cross(c.position.normalized, edgeDir); //assumes spheroid..  todo: change later to param

                        Vector3 nextEdgethicknessOffset = -wallThickness * 0.5f * Vector3.Cross(c.position.normalized, nextEdgeDir); //assumes spheroid..  todo: change later to param
                        Vector3 posOnEdgeSide = c.position + edgeThicknessOffset;
                        // we want the opposite side of the edge wall
                        Vector3 posOnNextEdgeSide = c.position - nextEdgethicknessOffset;
                        Ray side = new Ray(posOnEdgeSide, edgeDir);
                        Ray nextSide = new Ray(posOnNextEdgeSide, nextEdgeDir);
                        Vector3 closestSidePoint;
                        Vector3 closestNextSidePoint;
                        if (!LineIntersection(side, nextSide, out closestSidePoint, out closestNextSidePoint))// no intersection get midpoint of closest
                        {
                            closestSidePoint += closestNextSidePoint;
                            closestSidePoint *= 0.5f;
                        }
                        c.fanRing[eCounter] = closestSidePoint;

                        //              - Compute offset = wallThickness / sin(angle / 2)
                        //              - fanRing[i] = corner + bisector * offset
                        Vector3 cornerDownLineThicknessPos = c.position + (edgeDir * wallThickness);
                        Vector3 cornerNextDownLineThicknessPos = c.position + (nextEdgeDir * wallThickness);
                        //c.fanRing[eCounter] = cornerDownLineThicknessPos + thicknessOffset; //avg pos
                        //c.fanRing[eCounter] = (cornerDownLineThicknessPos + cornerNextDownLineThicknessPos) / 2f; //avg pos

                        avgRingPos += c.fanRing[eCounter];
                    }
                    avgRingPos /= visibleEdges.Count;

                    if (visibleEdges.Count > 2) //add tip point fan common point
                    {
                        c.tipVert = avgRingPos;// c.position;// can it be this simple? I don't think so....possibly- we'll see how those offsets work.
                    }
                    //loopthough walls again, assign end verts from fan verts-  can be optiized into main loop, if needed
                    for (int eCounter = 0; eCounter < visibleEdges.Count; eCounter++)
                    {
                        Edge e = visibleEdges[eCounter];
                        AssignToEdge(e, cIndex, c.fanRing.RingIndex(eCounter), c.fanRing.RingIndex(eCounter - 1), c.tipVert);
                        //Vector3 thicknessOffset = wallThickness * 0.5f * Vector3.Cross(c.position.normalized, EdgeDir(e)); //assumes spheroid..  todo: change later to param
                        //AssignToEdge(e, cIndex, c.position + thicknessOffset, c.position - thicknessOffset, null);//works as test, just not what we want
                    }
                }// end - more than one edge here
            }


        }

        Mesh GenerateWallModel()
        {
            // Step 5: Generate Wall Geometry
            // ------------------------------
            // - For each Edge in uniqueEdges:
            //     - If edge.hasVisibleWall == false, skip
            //     - Use edge.wallEndVerts[A and B][left and right] to emit a quad (bottom of wall)
            //     - Use edge.wallEndTopVerts[A and B][left and right] to emit a quad (top of wall)
            //     - use combos of top and bottom to generate front and back of wall
            //     - for each end A and B
            //         - if TIP point exists edge.wallEndVerts[A or B][tip]
            //             - create triangle using edge.wallEndVerts[A or B][left and right and tip] (bottom of miter) // but wound properly
            //             - create triangle using edge.wallEndTopVerts[A or B][left and right and tip] (top of miter)
            //         - if no 3rd point
            //             - create end-cap quad with combos of top bottom
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            for (int eCount = 0; eCount < uniqueEdges.Count; eCount++)
            {
                Edge e = uniqueEdges[eCount];
                if (!e.hasVisibleWall) continue;

                // Convenience handles
                var aBot = e.wallEndAVerts;
                var aTop = e.wallEndATopVerts;
                var aTip = e.wallEndATipVert;
                var aTipTop = e.wallEndATopTipVert;

                var bBot = e.wallEndBVerts;
                var bTop = e.wallEndBTopVerts;
                var bTip = e.wallEndBTipVert;
                var bTipTop = e.wallEndBTopTipVert;

                // Bottom quad (A[0]→B[1], A[1]→B[0])
                AddQuad(aBot[0], aBot[1], bBot[1], bBot[0], verts, tris, uvs);

                // Top quad
                AddQuad(aTop[0], aTop[1], bTop[1], bTop[0], verts, tris, uvs);

                // Front face: A[0]→ATop[0]→BTop[1]→B[1]
                AddQuad(aBot[1], aTop[0], bTop[0], bBot[1], verts, tris, uvs);

                // Back face: A[1]→ATop[1]→BTop[0]→B[0]
                AddQuad(aTop[1], aBot[0], bBot[0], bTop[1], verts, tris, uvs);

                // End A
                if (aTip.HasValue && aTipTop.HasValue)
                {
                    AddTri(aBot[1], aBot[0], aTip.Value, verts, tris, uvs);
                    AddTri(aTop[1], aTop[0], aTipTop.Value, verts, tris, uvs);
                }
                else
                {
                    AddQuad(aBot[1], aBot[0], aTop[1], aTop[0], verts, tris, uvs);
                }

                // End B
                if (bTip.HasValue && bTipTop.HasValue)
                {
                    AddTri(bBot[0], bBot[1], bTip.Value, verts, tris, uvs);
                    AddTri(bTop[0], bTop[1], bTipTop.Value, verts, tris, uvs);
                }
                else
                {
                    AddQuad(bBot[0], bBot[1], bTop[0], bTop[1], verts, tris, uvs);
                }
            }

            Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;


            void AddQuad(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br,
                         List<Vector3> v, List<int> t, List<Vector2> uv)
            {
                int start = v.Count;
                v.Add(bl); v.Add(tl); v.Add(tr); v.Add(br);
                t.Add(start + 0); t.Add(start + 1); t.Add(start + 2);
                t.Add(start + 0); t.Add(start + 2); t.Add(start + 3);

                uv.Add(new Vector2(0, 0)); // bl
                uv.Add(new Vector2(0, 1)); // tl
                uv.Add(new Vector2(1, 1)); // tr
                uv.Add(new Vector2(1, 0)); // br
            }

            void AddTri(Vector3 a, Vector3 b, Vector3 tip,
                        List<Vector3> v, List<int> t, List<Vector2> uv)
            {
                int start = v.Count;
                v.Add(a); v.Add(b); v.Add(tip);
                t.Add(start + 0); t.Add(start + 1); t.Add(start + 2);

                // Assign base UVs
                Vector2 uvA = new Vector2(0, 0);
                Vector2 uvB = new Vector2(1, 0);

                // Interpolate UV for tip using distance weighting
                float da = Vector3.Distance(tip, a);
                float db = Vector3.Distance(tip, b);
                float total = da + db;
                Vector2 uvTip = (db / total) * uvA + (da / total) * uvB;

                uv.Add(uvA);
                uv.Add(uvB);
                uv.Add(uvTip);
            }
        }
    }

    public class OLDWallMeshChunkComputer
    {
        // Maze Mesh Construction Plan (with unique corners and edges)
        //
        // Step 1: Build Unique Corner List
        // ----------------------------------
        // - Create a List<Vector3> uniqueCorners
        // - Create a List<int> oldToUniqueCornerMap= init to size of old corners
        // - For each FaceDetails F in FacesAndNeighbors:
        //     - For each index in F.cornerVertexMeshIndices:
        //         - If vertex not in uniqueCorners:
        //             - Add to uniqueCorners
        //         - Map original index to new unique index in oldToUniqueCornerMap
        //
        // Step 2: Build Unique Edges and Wall Visibility
        // ----------------------------------------------
        // - Create List<Edge> uniqueEdges
        // - For each FaceDetails F (index fIndex):
        //     - Convert F.cornerVertexMeshIndices to unique corner indices using oldToUniqueCornerMap
        //     - For each corner in face, record fIndex in the corner’s touchingFaces (if you're tracking per-corner data)
        //     - For i = 0 to corner count - 1:
        //         - cornerA = uniqueCornerIndices[i]
        //         - cornerB = uniqueCornerIndices[(i + 1) % count]
        //         - edge = TryGetOrCreateUniqueEdge(cornerA,cornerB);
        //         - add face to edge.touchingfaces
        //         - Set edge.hasVisibleWall = GetWallVisible(fIndex, neighborFaceIndex)
        //         - add edge to cornerA/B.edges, if not present

        //
        // Step 3: Sort Edges Around Each Corner 
        // -----------------------------------------------------------------------------
        // - For each uniqueCorner:
        //     - sort corners.edgelist by
        //        - a float: compute edge's angle around normal axis
        //
        // Step 4: Generate vertex positions (use new param- wallThickness to compute end-side points (front to back thickness) )
        // ----------------------------------------------------
        // - For each uniqueCorner:
        //      - If only one edge is visible:
        //         - Compute two end-side points and store in edge.wallEndVerts[A or B][left and right]
        //     - If multiple visible edges:
        //          - For each visible pair of edges(A, B) at corner:
        //              - Compute dirA and dirB (unit vectors away from corner)
        //              - Compute bisector = normalize(dirA + dirB)
        //              - Compute angle = angle between dirA and dirB
        //              - Compute offset = wallThickness / sin(angle / 2)
        //              - fanRing[i] = corner + bisector * offset
        //          - Store N(number of visible edges) such points in clockwise order → corner.fanRing[]
        //          - if MORE than 2 visible edges- compute the TIP point location
        //          - duplicate, fanRing but extrude into:  fanRingTopVerts
        //          - duplicate, Tip (if exists) but extrude into:  tipTopVert
        //          - Each edge gets assigned edge.wallEndVerts[A or B][left and right]  (left = fanRing[i], right = fanRing[i+1], and the TIP Vector3 or null),
        //                 -both top and bottom

        // Step 5: Generate Wall Geometry
        // ------------------------------
        // - For each Edge in uniqueEdges:
        //     - If edge.hasVisibleWall == false, skip
        //     - Use edge.wallEndVerts[A and B][left and right] to emit a quad (bottom of wall)
        //     - Use edge.wallEndTopVerts[A and B][left and right] to emit a quad (top of wall)
        //     - use combos of top and bottom to generate front and back of wall
        //     - for each end A and B
        //         - if TIP point exists edge.wallEndVerts[A or B][tip]
        //             - create triangle using edge.wallEndVerts[A or B][left and right and tip] (bottom of miter) // but wound properly
        //             - create triangle using edge.wallEndTopVerts[A or B][left and right and tip] (top of miter)
        //         - if no 3rd point
        //             - create end-cap quad with combos of top bottom

        /*input values*/
        FacesAndNeighbors data;
        PolyhedronMazeDraw mazeDrawer;
        float wallThickness;
        float wallHeight;

        public List<Mesh> CreateWallsMeshChunks(
                FacesAndNeighbors data,
                PolyhedronMazeDraw mazeDrawer,
                float wallThickness,
                float wallHeight,
                IReadOnlyList<IReadOnlyList<FaceCoordinate>> facesPerChuck)
        {
            this.data = data;
            this.mazeDrawer = mazeDrawer;
            this.wallThickness = wallThickness;
            this.wallHeight = wallHeight;
            Debug.Log("Wall mesh computer running now");

            //build chunk by tile coord dic
            chunckIndexByFaceCoord = new Dictionary<FaceCoordinate, int>();
            for (int chunkCounter = 0; chunkCounter < facesPerChuck.Count; chunkCounter++)
            {
                IReadOnlyList<FaceCoordinate> listOfFaces = facesPerChuck[chunkCounter];
                foreach (FaceCoordinate coord in listOfFaces)
                    chunckIndexByFaceCoord[coord] = chunkCounter;
                edgesByChunk.Add(new List<Edge>());
            }

            BuildUniqueCorners();
            BuildUniqueEdges();
            SortCornerEdgesClockwise();
            GenerateVertexPositions();

            List<Mesh> outputMeshes = new List<Mesh>();
            foreach (List<Edge> chuckEdges in edgesByChunk)
                outputMeshes.Add(GenerateWallModel(chuckEdges));

            return outputMeshes;
        }


        public Mesh RebuildSingleChunk(int chunk)
        {
            BuildUniqueEdges();// based on visibility
            SortCornerEdgesClockwise();
            GenerateVertexPositions();

            List<Mesh> outputMeshes = new List<Mesh>();
            List<Edge> chuckEdges = edgesByChunk[chunk];
            return GenerateWallModel(chuckEdges);
        }
        //internal storage
        List<Corner> uniqueCorners = new List<Corner>();
        List<int> oldToNewCornerIndex = new List<int>();
        List<Edge> uniqueEdges = new List<Edge>();
        List<List<Edge>> edgesByChunk = new List<List<Edge>>();
        Dictionary<FaceCoordinate, int> chunckIndexByFaceCoord;
        //internal structures
        class Corner
        {
            public Vector3 position;
            public List<int> edgeTo;  //indexes into the uniqueCorners list
            public List<int> edges;  //indexes into the uniqueEdges list
            public List<int> sortedEdges;  //indexes into the edges list
            public Vector3[] fanRing;
            public Vector3? tipVert;
        }
        class Edge
        {
            public int cornerA;
            public int cornerB;
            //   public List<int> touchingFaces;
            public bool hasVisibleWall;
            public Vector3[] wallEndAVerts;
            public Vector3[] wallEndATopVerts;
            public Vector3? wallEndATipVert;
            public Vector3? wallEndATopTipVert;
            public Vector3[] wallEndBVerts;
            public Vector3[] wallEndBTopVerts;
            public Vector3? wallEndBTipVert;
            public Vector3? wallEndBTopTipVert;
            public Color wallEndADebugColor;
            public Color wallEndBDebugColor;
        }
        //utility functions
        Vector3 EdgeDirFrom(Edge e, int cornerIndex)
        {
            if (e.cornerA == cornerIndex)
                return (uniqueCorners[e.cornerB].position - uniqueCorners[e.cornerA].position).normalized;

            if (e.cornerB == cornerIndex)
                return (uniqueCorners[e.cornerA].position - uniqueCorners[e.cornerB].position).normalized;
            throw new System.Exception("Invalid corner index (" + cornerIndex + ")passed to EdgeDirFrom.  Edge only contains indexes " + e.cornerA + " and " + e.cornerB);
        }
        void AssignToEdge(Edge e, int whichCornerIndex, Vector3 wallEndVertFront, Vector3 wallEndVertBack, Vector3? tip, Color debugColor)
        {
            if (e.cornerA == whichCornerIndex)
            {
                e.wallEndAVerts = new Vector3[] { wallEndVertFront, wallEndVertBack };
                e.wallEndATopVerts = new Vector3[] { Extrude(wallEndVertBack), Extrude(wallEndVertFront) };
                e.wallEndATipVert = tip;
                if (tip == null) e.wallEndATopTipVert = null;
                else e.wallEndATopTipVert = Extrude(tip.Value);
                e.wallEndADebugColor = debugColor;
            }
            else//assumes (e.cornerB == whichCornerIndex)
            {
                e.wallEndBVerts = new Vector3[] { wallEndVertBack, wallEndVertFront };
                e.wallEndBTopVerts = new Vector3[] { Extrude(wallEndVertFront), Extrude(wallEndVertBack) };
                e.wallEndBTipVert = tip;
                if (tip == null) e.wallEndBTopTipVert = null;
                else e.wallEndBTopTipVert = Extrude(tip.Value);
                e.wallEndBDebugColor = debugColor;
            }
        }
        Vector3 Extrude(Vector3 v)
        {
            return v + (v.normalized * wallHeight);
        }
        bool LineIntersection(Ray LineA, Ray LineB, out Vector3 closestPointOnA, out Vector3 closestPointOnB)
        {
            Vector3 p1 = LineA.origin;
            Vector3 d1 = LineA.direction;
            Vector3 p2 = LineB.origin;
            Vector3 d2 = LineB.direction;

            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);

            float denom = a * e - Vector3.Dot(d1, d2) * Vector3.Dot(d1, d2);
            if (Mathf.Approximately(denom, 0f))
            {
                closestPointOnA = closestPointOnB = Vector3.zero;
                return false; // parallel
            }

            float b = Vector3.Dot(d1, d2);
            float c = Vector3.Dot(d1, r);

            float s = (b * f - c * e) / denom;
            float t = (a * f - b * c) / denom;

            closestPointOnA = p1 + d1 * s;
            closestPointOnB = p2 + d2 * t;
            return true;
        }


        /*primary internal functions*/
        void BuildUniqueCorners()
        {
            // Step 1: Build Unique Corner List
            // ----------------------------------
            // - Create a List<Vector3> uniqueCorners
            // - Create a List<int> oldToUniqueCornerMap= init to size of old corners
            // - For each FaceDetails F in FacesAndNeighbors:
            //     - For each index in F.cornerVertexMeshIndices:
            //         - If vertex not in uniqueCorners:
            //             - Add to uniqueCorners
            //         - Map original index to new unique index in oldToUniqueCornerMap
            Vector3[] meshVertices = data.meshRef.vertices;
            int vertexCount = meshVertices.Length;

            oldToNewCornerIndex = new List<int>(new int[vertexCount]);
            Dictionary<Vector3, int> cornerMap = new();

            for (int i = 0; i < data.faceDetails.Count; i++)
            {
                var face = data.faceDetails[i];
                foreach (int cornerIdx in face.cornerVertexMeshIndices)
                {
                    Vector3 pos = meshVertices[cornerIdx];

                    if (!cornerMap.TryGetValue(pos, out int uniqueIndex))
                    {
                        uniqueIndex = uniqueCorners.Count;
                        uniqueCorners.Add(new Corner { position = pos });
                        cornerMap[pos] = uniqueIndex;
                    }

                    oldToNewCornerIndex[cornerIdx] = uniqueIndex;  //cannot be out of bounds-throw exception if so
                }
            }
        }

        void BuildUniqueEdges()
        {
            // Step 2: Build Unique Edges and Wall Visibility
            // ----------------------------------------------
            // - Create List<Edge> uniqueEdges
            // - For each FaceDetails F (index fIndex):
            //     - Convert F.cornerVertexMeshIndices to unique corner indices using oldToUniqueCornerMap
            //     - For each corner in face, record fIndex in the corner’s touchingFaces (if you're tracking per-corner data)
            //     - For i = 0 to corner count - 1:
            //         - cornerA = uniqueCornerIndices[i]
            //         - cornerB = uniqueCornerIndices[(i + 1) % count]
            //         - edge = TryGetOrCreateUniqueEdge(cornerA,cornerB);
            //         - add face to edge.touchingfaces
            //         - Set edge.hasVisibleWall = GetWallVisible(fIndex, neighborFaceIndex)
            //         - add edge to cornerA/B.edges, if not present

            uniqueEdges = new List<Edge>();
            string logstr = "";
            foreach (FaceCoordinate faceCoord in mazeDrawer.maze.allMapCoords)
            {

                FaceDetails face = data.faceDetails[faceCoord.faceIndex];

                // oldToNewCornerIndex  will map old corner mesh indices to unique corner indices: lookup via uniqueCornerIndex to get original Corner Index
                int cornerCount = face.cornerVertexMeshIndices.Count;
                /*List<int> uniqueCornerIndices = new List<int>(cornerCount);
                for (int i = 0; i < cornerCount; i++)
                {
                    int oldIndex = face.cornerVertexMeshIndices[i];
                    int uniqueIndex = oldToNewCornerIndex[oldIndex];
                    uniqueCornerIndices.Add(uniqueIndex);
                }*/
                int uniqueCornerIndex(int i) => oldToNewCornerIndex[face.cornerVertexMeshIndices[i]];

                for (int i = 0; i < cornerCount; i++)
                {

                    int currentCornerA = uniqueCornerIndex(i);// uniqueCornerIndices[i];
                    int currentCornerB = uniqueCornerIndex((i + 1) % cornerCount);//uniqueCornerIndices[(i + 1) % cornerCount];
                    // see if an edge exists that uses these corners
                    int currentEdgeIndex = uniqueEdges.FindIndex(0, (Edge e) => (e.cornerA == currentCornerA && e.cornerB == currentCornerB) || (e.cornerA == currentCornerB && e.cornerB == currentCornerA));
                    Edge edge;
                    if (currentEdgeIndex == -1)// if not, create one
                    {
                        edge = new Edge() { cornerA = currentCornerA, cornerB = currentCornerB };
                        edge.hasVisibleWall = false;
                        uniqueEdges.Add(edge);
                    }
                    else
                        edge = uniqueEdges[currentEdgeIndex];//if so, get it

                    /*
                    int neighborIndex = -1;// face.neighborIndices[i];
                    FaceCoordinate neighborCoord= faceCoord.GetNeighbor(i);
                    FaceCoordinate[] allNeighborCoords = faceCoord.GetNeighbors();
                    for (int x = 0; x < allNeighborCoords.Length; x++)
                    {
                        if (allNeighborCoords[x] == neighborCoord)
                        {
                            neighborIndex = x;
                            break;
                        }
                    }*/
                    bool hasWall = mazeDrawer.maze.Walls[faceCoord][i];
                    edge.hasVisibleWall = hasWall && (mazeDrawer.IsTileVisible(faceCoord) || mazeDrawer.IsTileVisible(faceCoord.GetNeighbor(i)));

                    logstr += ("\nedge between faces " + faceCoord + " and " + faceCoord.GetNeighbor(i) + ", has visible wall: " + edge.hasVisibleWall + "  edge dir: " + EdgeDirFrom(edge, currentCornerA));
                    // Add edge index to corners' edge lists if not already present
                    Corner cornerAObj = uniqueCorners[currentCornerA];
                    if (cornerAObj.edges == null)
                        cornerAObj.edges = new List<int>();
                    int edgeIndex = uniqueEdges.IndexOf(edge);
                    if (!cornerAObj.edges.Contains(edgeIndex))
                        cornerAObj.edges.Add(edgeIndex);

                    Corner cornerBObj = uniqueCorners[currentCornerB];
                    if (cornerBObj.edges == null)
                        cornerBObj.edges = new List<int>();
                    if (!cornerBObj.edges.Contains(edgeIndex))
                        cornerBObj.edges.Add(edgeIndex);

                    int edgeChunk = Mathf.Min(chunckIndexByFaceCoord[faceCoord], chunckIndexByFaceCoord[faceCoord.GetNeighbor(i)]);
                    edgesByChunk[edgeChunk].Add(edge);
                }
            }
            Debug.Log(logstr);
        }

        void SortCornerEdgesClockwise()
        {
            //return;
            // Step 3: Sort Edges Around Each Corner 
            // -----------------------------------------------------------------------------
            // - For each uniqueCorner:
            //     - sort corners.edgelist by
            //        - a float: compute edge's angle around normal axis
            for (int cIndex = 0; cIndex < uniqueCorners.Count; cIndex++)
            {
                int cornerIndex = cIndex;  // de-scope loop integer
                Corner c = uniqueCorners[cornerIndex];
                Vector3 axis = c.position.normalized;
                // Choose an arbitrary, but consistent reference direction that is not the same as axis
                Vector3 refDir;
                refDir = EdgeDirFrom(uniqueEdges[c.edges[0]], cornerIndex);
                c.sortedEdges = new List<int>();/// indexes into c.edges
                for (int i = 0; i < c.edges.Count; i++) c.sortedEdges.Add(i);// fill with 0,1,2,... (unsorted indexes into edges array)
                c.sortedEdges.Sort(Compare);
                int Compare(int x, int y)
                {
                    Edge edgeX = uniqueEdges[c.edges[x]];
                    Edge edgeY = uniqueEdges[c.edges[y]];
                    Vector3 edgeDirX = EdgeDirFrom(edgeX, cornerIndex);
                    Vector3 edgeDirY = EdgeDirFrom(edgeY, cornerIndex);
                    float angleX = Vector3.SignedAngle(refDir, edgeDirX, axis);
                    if (angleX < 0) angleX += 360f;// Mathf.PI * 2f;
                    float angleY = Vector3.SignedAngle(refDir, edgeDirY, axis);
                    if (angleY < 0) angleY += 360f;// Mathf.PI * 2f;
                    return -angleX.CompareTo(angleY);
                }

                //test sort
                /*
                Debug.Log("Edges for corner " + cornerIndex + " axis: (" + uniqueCorners[cornerIndex].position + ") -   ref dir:"+ refDir + "ref edge unsorted index: [0],   globalIndex: [" + c.edges[0] + "]");
                for (int e = 0; e < c.sortedEdges.Count; e++)
                {
                    Edge edge = uniqueEdges[c.edges[c.sortedEdges[e]]];
                    Vector3 edgeDir = EdgeDirFrom(edge, cornerIndex);
                    float angle = Vector3.SignedAngle(refDir, edgeDir, axis);
                    if (angle < 0) angle += 360f;// Mathf.PI * 2f;
                    Debug.Log("   edge sorted index:[" + e + "], unsorted index: ["+ c.sortedEdges[e] + "],   globalIndex: [" + c.edges[c.sortedEdges[e]] + "] dir:("+edgeDir+") degrees from ref: "+angle);
                }*/

            }
        }

        void GenerateVertexPositions()
        {
            // Step 4: Generate vertex positions (use new param- wallThickness to compute end-side points (front to back thickness) )
            // ----------------------------------------------------
            // - For each uniqueCorner:
            //      - If only one edge is visible:
            //         - Compute two end-side points and store in edge.wallEndVerts[A or B][left and right]
            //     - If multiple visible edges:
            //          - For each visible pair of edges(A, B) at corner:
            //              - Compute dirA and dirB (unit vectors away from corner)
            //              - Compute bisector = normalize(dirA + dirB)
            //              - Compute angle = angle between dirA and dirB
            //              - Compute offset = wallThickness / sin(angle / 2)
            //              - fanRing[i] = corner + bisector * offset
            //          - Store N(number of visible edges) such points in clockwise order → corner.fanRing[]
            //          - if MORE than 2 visible edges- compute the TIP point location
            //          - duplicate, fanRing but extrude into:  fanRingTopVerts
            //          - duplicate, Tip (if exists) but extrude into:  tipTopVert
            //          - Each edge gets assigned edge.wallEndVerts[A or B][left and right]  (left = fanRing[i], right = fanRing[i+1], and the TIP Vector3 or null),
            //                 -both top and bottom
            for (int cIndex = 0; cIndex < uniqueCorners.Count; cIndex++)
            //foreach (Corner c in uniqueCorners)
            {
                Corner c = uniqueCorners[cIndex];
                if (c.edges.Count == 0) continue;
                List<Edge> visibleEdges = new List<Edge>();//edges with walls that touch this corner

                foreach (int edgeIndex in c.sortedEdges)// we want the visible edge list in this order
                {
                    //Edge e = uniqueEdges[edgeIndex];
                    Edge e = uniqueEdges[c.edges[edgeIndex]];
                    if (e.hasVisibleWall)
                    {
                        visibleEdges.Add(e);
                    }

                }

                if (visibleEdges.Count == 1)
                {
                    //compute 2 fanRing verts- assign to single edge-end
                    Edge e = visibleEdges[0];
                    Vector3 thicknessOffset = wallThickness * 0.5f * -Vector3.Cross(c.position.normalized, EdgeDirFrom(e, cIndex)); //assumes spheroid..  todo: change later to param
                    AssignToEdge(e, cIndex, c.position + thicknessOffset, c.position - thicknessOffset, null, Color.black);
                }
                else if (visibleEdges.Count > 1)
                {
                    c.fanRing = new Vector3[visibleEdges.Count];
                    Vector3 axis = c.position.normalized;// assumes spheroid
                    Vector3 refDir;
                    if (Mathf.Abs(axis.z) < 0.99f)
                        refDir = Vector3.Cross(axis, Vector3.forward); // not parallel
                    else
                        refDir = Vector3.Cross(axis, Vector3.right);
                    refDir.Normalize();

                    Vector3 avgRingPos = Vector3.zero;
                    //compute visibleCount fanRing verts
                    for (int eCounter = 0; eCounter < visibleEdges.Count; eCounter++)
                    {
                        Edge e = visibleEdges[eCounter];
                        Edge nextEdge = visibleEdges.RingIndex(eCounter + 1);
                        //              - Compute dirA and dirB (unit vectors away from corner)
                        Vector3 edgeDir = EdgeDirFrom(e, cIndex);
                        Vector3 nextEdgeDir = EdgeDirFrom(nextEdge, cIndex);

                        //bisector stuff working, but only sometimes. we'll try different methods
                        // get line in form of a point and a direction, for both edge's side and next edge's side
                        //compute points on each line where they closest (Ideally same point), and compute the avg position of them

                        Vector3 edgeThicknessOffset = -wallThickness * 0.5f * Vector3.Cross(c.position.normalized, edgeDir); //assumes spheroid..  todo: change later to param

                        Vector3 nextEdgethicknessOffset = -wallThickness * 0.5f * Vector3.Cross(c.position.normalized, nextEdgeDir); //assumes spheroid..  todo: change later to param
                        Vector3 posOnEdgeSide = c.position + edgeThicknessOffset;
                        // we want the opposite side of the edge wall
                        Vector3 posOnNextEdgeSide = c.position - nextEdgethicknessOffset;
                        Ray side = new Ray(posOnEdgeSide, edgeDir);
                        Ray nextSide = new Ray(posOnNextEdgeSide, nextEdgeDir);
                        Vector3 closestSidePoint;
                        Vector3 closestNextSidePoint;
                        if (!LineIntersection(side, nextSide, out closestSidePoint, out closestNextSidePoint))// no intersection get midpoint of closest
                        {
                            closestSidePoint += closestNextSidePoint;
                            closestSidePoint *= 0.5f;
                        }
                        c.fanRing[eCounter] = closestSidePoint;

                        avgRingPos += c.fanRing[eCounter];
                    }
                    avgRingPos /= visibleEdges.Count;

                    if (visibleEdges.Count > 2) //add tip point fan common point
                    {
                        c.tipVert = avgRingPos;// c.position;// can it be this simple? I don't think so....possibly- we'll see how those offsets work.
                    }
                    //loopthough walls again, assign end verts from fan verts-  can be optiized into main loop, if needed
                    for (int eCounter = 0; eCounter < visibleEdges.Count; eCounter++)
                    {
                        Edge e = visibleEdges[eCounter];
                        Color color = Color.red * ((float)eCounter / (float)visibleEdges.Count);
                        AssignToEdge(e, cIndex, c.fanRing.RingIndex(eCounter), c.fanRing.RingIndex(eCounter - 1), c.tipVert, color);
                        //Vector3 thicknessOffset = wallThickness * 0.5f * Vector3.Cross(c.position.normalized, EdgeDir(e)); //assumes spheroid..  todo: change later to param
                        //AssignToEdge(e, cIndex, c.position + thicknessOffset, c.position - thicknessOffset, null);//works as test, just not what we want
                    }
                }// end - more than one edge here
            }


        }

        Mesh GenerateWallModel(List<Edge> edgesInChunk)
        {
            // Step 5: Generate Wall Geometry
            // ------------------------------
            // - For each Edge in uniqueEdges:
            //     - If edge.hasVisibleWall == false, skip
            //     - Use edge.wallEndVerts[A and B][left and right] to emit a quad (bottom of wall)
            //     - Use edge.wallEndTopVerts[A and B][left and right] to emit a quad (top of wall)
            //     - use combos of top and bottom to generate front and back of wall
            //     - for each end A and B
            //         - if TIP point exists edge.wallEndVerts[A or B][tip]
            //             - create triangle using edge.wallEndVerts[A or B][left and right and tip] (bottom of miter) // but wound properly
            //             - create triangle using edge.wallEndTopVerts[A or B][left and right and tip] (top of miter)
            //         - if no 3rd point
            //             - create end-cap quad with combos of top bottom
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            for (int eCount = 0; eCount < edgesInChunk.Count; eCount++)
            {
                Edge e = edgesInChunk[eCount];
                if (!e.hasVisibleWall) continue;

                // Convenience handles
                var aBot = e.wallEndAVerts;
                var aTop = e.wallEndATopVerts;
                var aTip = e.wallEndATipVert;
                var aTipTop = e.wallEndATopTipVert;
                var aColor = e.wallEndADebugColor;

                var bBot = e.wallEndBVerts;
                var bTop = e.wallEndBTopVerts;
                var bTip = e.wallEndBTipVert;
                var bTipTop = e.wallEndBTopTipVert;
                var bColor = e.wallEndBDebugColor;
                // Bottom quad (A[0]→B[1], A[1]→B[0])
                //AddQuad(aBot[0], aBot[1], bBot[1], bBot[0], verts, tris, uvs);
                AddQuad(aBot[0], aBot[1], bBot[1], bBot[0], aColor, bColor, verts, tris, uvs, colors);
                // Top quad
                //AddQuad(aTop[0], aTop[1], bTop[1], bTop[0], verts, tris, uvs);
                AddQuad(aTop[0], aTop[1], bTop[1], bTop[0], aColor, bColor, verts, tris, uvs, colors);

                // Front face: A[0]→ATop[0]→BTop[1]→B[1]
                //AddQuad(aBot[1], aTop[0], bTop[0], bBot[1], verts, tris, uvs);
                AddQuad(aBot[1], aTop[0], bTop[0], bBot[1], aColor, bColor, verts, tris, uvs, colors);

                // Back face: A[1]→ATop[1]→BTop[0]→B[0]
                //AddQuad(aTop[1], aBot[0], bBot[0], bTop[1], verts, tris, uvs);
                AddQuad(aTop[1], aBot[0], bBot[0], bTop[1], aColor, bColor, verts, tris, uvs, colors);

                // End A
                if (aTip.HasValue && aTipTop.HasValue)
                {
                    //AddTri(aBot[1], aBot[0], aTip.Value, verts, tris, uvs);
                    //AddTri(aTop[1], aTop[0], aTipTop.Value, verts, tris, uvs);
                    AddTri(aBot[1], aBot[0], aTip.Value, aColor, verts, tris, uvs, colors);
                    AddTri(aTop[1], aTop[0], aTipTop.Value, aColor, verts, tris, uvs, colors);
                }
                else
                {
                    //AddQuad(aBot[1], aBot[0], aTop[1], aTop[0], verts, tris, uvs);
                    AddQuad(aBot[1], aBot[0], aTop[1], aTop[0], aColor, aColor, verts, tris, uvs, colors);
                }

                // End B
                if (bTip.HasValue && bTipTop.HasValue)
                {
                    //AddTri(bBot[0], bBot[1], bTip.Value, verts, tris, uvs);
                    //AddTri(bTop[0], bTop[1], bTipTop.Value, verts, tris, uvs);
                    AddTri(bBot[0], bBot[1], bTip.Value, bColor, verts, tris, uvs, colors);
                    AddTri(bTop[0], bTop[1], bTipTop.Value, bColor, verts, tris, uvs, colors);
                }
                else
                {
                    //AddQuad(bBot[0], bBot[1], bTop[0], bTop[1], verts, tris, uvs);
                    AddQuad(bBot[0], bBot[1], bTop[0], bTop[1], bColor, bColor, verts, tris, uvs, colors);
                }
            }

            Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;


            void OLDAddQuad(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br,
                         List<Vector3> v, List<int> t, List<Vector2> uv)
            {
                int start = v.Count;
                v.Add(bl); v.Add(tl); v.Add(tr); v.Add(br);
                t.Add(start + 0); t.Add(start + 1); t.Add(start + 2);
                t.Add(start + 0); t.Add(start + 2); t.Add(start + 3);

                uv.Add(new Vector2(0, 0)); // bl
                uv.Add(new Vector2(0, 1)); // tl
                uv.Add(new Vector2(1, 1)); // tr
                uv.Add(new Vector2(1, 0)); // br
            }
            void AddQuad(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br,
                Color colorL, Color colorR,
                List<Vector3> v, List<int> t, List<Vector2> uv, List<Color> col)
            {
                int start = v.Count;
                v.Add(bl); v.Add(tl); v.Add(tr); v.Add(br);
                t.Add(start + 0); t.Add(start + 1); t.Add(start + 2);
                t.Add(start + 0); t.Add(start + 2); t.Add(start + 3);

                uv.Add(new Vector2(0, 0)); // bl
                uv.Add(new Vector2(0, 1)); // tl
                uv.Add(new Vector2(1, 1)); // tr
                uv.Add(new Vector2(1, 0)); // br

                col.Add(colorL);
                col.Add(colorL);
                col.Add(colorR);
                col.Add(colorR);


            }
            void OLDAddTri(Vector3 a, Vector3 b, Vector3 tip,
                        List<Vector3> v, List<int> t, List<Vector2> uv)
            {
                int start = v.Count;
                v.Add(a); v.Add(b); v.Add(tip);
                t.Add(start + 0); t.Add(start + 1); t.Add(start + 2);

                // Assign base UVs
                Vector2 uvA = new Vector2(0, 0);
                Vector2 uvB = new Vector2(1, 0);

                // Interpolate UV for tip using distance weighting
                float da = Vector3.Distance(tip, a);
                float db = Vector3.Distance(tip, b);
                float total = da + db;
                Vector2 uvTip = (db / total) * uvA + (da / total) * uvB;

                uv.Add(uvA);
                uv.Add(uvB);
                uv.Add(uvTip);


            }
            void AddTri(Vector3 a, Vector3 b, Vector3 tip, Color c,
                List<Vector3> v, List<int> t, List<Vector2> uv, List<Color> col)
            {
                int start = v.Count;
                v.Add(a); v.Add(b); v.Add(tip);
                t.Add(start + 0); t.Add(start + 1); t.Add(start + 2);

                // Assign base UVs
                Vector2 uvA = new Vector2(0, 0);
                Vector2 uvB = new Vector2(1, 0);

                // Interpolate UV for tip using distance weighting
                float da = Vector3.Distance(tip, a);
                float db = Vector3.Distance(tip, b);
                float total = da + db;
                Vector2 uvTip = (db / total) * uvA + (da / total) * uvB;

                uv.Add(uvA);
                uv.Add(uvB);
                uv.Add(uvTip);

                col.Add(c);
                col.Add(c);
                col.Add(c);

            }
        }
    }
}