using UnityEngine;
using System.Collections.Generic;
using EyE.Threading;
using Cysharp.Threading.Tasks;

namespace Eye.Maps.Templates
{
    public abstract class MazeMeshDrawGeneric<T> : MonoBehaviour, IMazeDrawer<T> where T : ITileCoordinate<T>
    {
        public GenericMazeMap<T> _maze=null;
        public GenericMazeMap<T> maze
        {
            get => _maze;
        }
        public void SetMaze(GenericMazeMap<T> toValue)
        {
            _maze = toValue;
            BuildChucks();
            GenerateMazeVisuals();
        }
        public void SetMazeVisibilityRef(Dictionary<T, bool> tileVisibility)
        {
            this.tileVisibility = tileVisibility;
        }
        
        protected bool skipUpdateMazeGenerationRunning = false;// locks Update function
        public async UniTask SetMazeAsync(GenericMazeMap<T> toValue, CancelBoolRef cancelRef=null, ProgressFloatRef progressRef=null)
        {
            skipUpdateMazeGenerationRunning = true;
            _maze = toValue;
            BuildChucks();
            await GenerateMazeVisualsAsync(cancelRef,progressRef);
            skipUpdateMazeGenerationRunning = false;
        }
        public T mazeSize;

        public float tileScale = 1f;               // Size of the tiles
        public float wallThicknessFraction = 0.2f; // Thickness of the walls (fraction of tile size)
        public float wallHeightFraction = 0.2f; // Thickness of the walls (fraction of tile size)
        public float wallWidthFraction = 1f; // Thickness of the walls (fraction of tile size)
        
        public GameObject startPositionMarkerPrefab;             // Prefab for floor tiles
        public GameObject endPositionMarkerPrefab;             // Prefab for floor tiles
        public bool drawBorderWalls = true;        // Determines if border walls should be drawn
        public bool startHidden = false;
        public List<MeshFilter> wallChunkMeshFilters;
        //visibility stuff
        private Dictionary<T, bool> tileVisibility = new Dictionary<T, bool>();


        //chunking
        public int numberOfChunks;
        protected Chunker<T> chunkHandler;
        HashSet<int> chunkRegenByIndex = new HashSet<int>();
        protected virtual void RegenChunkMesh(int chunkIndex)
        {
            wallChunkMeshFilters[chunkIndex].sharedMesh = meshComputer.RebuildSingleChunk(chunkIndex);
        }
        //refs
        private GameObject instantiatedStartPositionMarker;
        private GameObject instantiatedEndPositionMarker;
        /*private Mesh floorMesh;
        private Material floorMaterial;
        private Mesh wallMesh;
        private Material wallMaterial;*/

        private void Reset()
        {
            mazeSize = DefaultMazeSize();
        }

        public bool createMazeOnEnable = true;
        protected virtual void OnEnable()
        {
            if (createMazeOnEnable)
            {
                GenericMazeMap<T> newMaze = CreateMazeMap();
                SetMaze(newMaze);

            }
            
        }
        protected abstract void BuildChucks();

        protected virtual void Update()
        {
            foreach (int chunkIndex in chunkRegenByIndex)
            {
                RegenChunkMesh(chunkIndex);//to do- optimize to use redrawchunk
            }
            chunkRegenByIndex.Clear();
        }

        WallMeshChunkComputerGeneric<T> meshComputer = null;
        protected virtual WallMeshChunkComputerGeneric<T> GetNewMeshComputer()
        {
            return new WallMeshChunkComputerGeneric<T>();
        }

        void GenerateMesh()
        {
            Debug.Log("Setting visiblity for all tiles: " + !startHidden);
            foreach (T coord in maze.allMapCoords)
            {
                SetTileVisibility(coord, !startHidden);
            }

            //InitializeChunks();
            meshComputer = GetNewMeshComputer();
            List<Mesh> chunkMeshes = meshComputer.CreateWallsMeshChunks(maze, this, tileScale * wallThicknessFraction, tileScale * wallHeightFraction, chunkHandler.ChunkCoordinateLists());
            if (chunkMeshes.Count != numberOfChunks) throw new System.Exception("Failed to generate correct number of chunk meshes- aborting assignment.");
            if (wallChunkMeshFilters.Count != numberOfChunks) throw new System.Exception("Incorrect number of mesh filters to assign chunk meshes to, aborting assignment.");
            for (int i = 0; i < wallChunkMeshFilters.Count; i++)
            {
                wallChunkMeshFilters[i].sharedMesh = chunkMeshes[i];//  should be equal in len, will throw if not
            }
            //WallMeshComputer meshComputer = new  WallMeshComputer();
            //wallsMeshFilter.sharedMesh = meshComputer.CreateWallsMesh(facesAndNeighbors, this,tileScale* wallThicknessFraction, tileScale * wallHeightFraction); 
        }

        // Abstract method for creating the specific maze map
        protected abstract GenericMazeMap<T> CreateMazeMap();
        protected abstract T DefaultMazeSize();
        struct CoordPair
        {
            public T a, b;

            public CoordPair(T a, T b)
            {
                this.a = a;
                this.b = b;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is CoordPair other)) return false;
                return (a.Equals(other.a) && b.Equals(other.b))
                    || (a.Equals(other.b) && b.Equals(other.a));
            }

            public override int GetHashCode()
            {
                return (EqualityComparer<T>.Default.GetHashCode(a) + EqualityComparer<T>.Default.GetHashCode(b)) * 31;
            }
        }
        protected virtual int NumTouchingWallsAtWallEnd(T coord, int wallIndex, bool isRightEnd, out bool isAcute)
        {
           
            isAcute = false;
          //  return 0;
            Quaternion wallDir = maze.NeighborBorderOrientation(coord, wallIndex);
            T neighbor1 = coord.GetNeighbor(wallIndex);
           // Debug.Log("*Starting touching wall count [" + coord + "] wall [" + neighbor1 + " ] rightSide:" + isRightEnd);
            Vector3 tilePos = maze.GetModelSpacePosition(coord);
            Vector3 neighbor1Pos = maze.GetModelSpacePosition(neighbor1);
            Vector3 wallPos =  (tilePos  + neighbor1Pos) / 2;
            float neighborDist = (tilePos - neighbor1Pos).magnitude;
            float computedEdgeLength = neighborDist * Mathf.Tan(Mathf.PI / coord.NumberOfNeighbors() );

            /*Vector3 tileNormal = Vector3.forward;
            if (isRightEnd) tileNormal *= -1;
            Vector3 wallDirToConer = Vector3.Cross((tilePos - neighbor1Pos).normalized, tileNormal).normalized;*/
            Vector3 wallRightVector = (wallDir * Vector3.right);
            Vector3 altwallDirToConer = wallRightVector;
            if (!isRightEnd)
                altwallDirToConer *= -1;
           // Vector3 altcornerPos = (altwallDirToConer * (computedEdgeLength / 2f)) + wallPos;

            Vector3 cornerPos = (altwallDirToConer * computedEdgeLength / 2f) + wallPos;

            HashSet<T> visited = new HashSet<T>();
            Queue<(T,int)> toCheck = new Queue<(T, int)>();
            float maxDist = computedEdgeLength * 1.1f;

           // Debug.Log("-*Computed corner pos: " + cornerPos);// + "  alt:"+altcornerPos);
            
            int sanityCounter = 0;
            int wallCounter = 0;
           
            HashSet<CoordPair> wallsChecked= new HashSet<CoordPair>();
            wallsChecked.Add(new CoordPair(coord, neighbor1));
            bool doInit = true;
            string wallsFoundLog = "\n  *  Computed corner pos: " + cornerPos + "   wall dir to corner:"+ altwallDirToConer;
            wallsFoundLog += "\n tile dir to neighbor: "+ (neighbor1Pos - tilePos) + " dist: "+ (neighbor1Pos - tilePos).magnitude + "  maxDist: "+ computedEdgeLength;
            while (toCheck.Count > 0 || doInit)
            {
                T dest;
                if (doInit)
                {
                    dest = coord;
                    doInit = false;
                }
                else
                {
                    (T, int) currentFromTo = toCheck.Dequeue();
                    T start = currentFromTo.Item1;
                    dest = start.GetNeighbor(currentFromTo.Item2);

                    //do check
                    if (sanityCounter++ > 104) throw new System.Exception("Unexpededly many tiles checked in MazeDrawGeneric<" + typeof(T) + ">:NumTouchingWallsAtWallEnd function");
                   // Debug.Log("--*Checking wall between " + start + " and " + dest);
                    CoordPair pair = new CoordPair(start, dest);
                    if (!wallsChecked.Contains(pair))
                    {
                        wallsChecked.Add(pair);

                        if (maze.Walls[start][currentFromTo.Item2])
                        {
                            wallCounter++;
                            Vector3 wallNeighborPos = (maze.GetModelSpacePosition(start) + maze.GetModelSpacePosition(dest))/2f;
                            Vector3 WallDirToCorner = cornerPos - wallNeighborPos;
                            float angle = Vector3.Angle(altwallDirToConer, WallDirToCorner);
                            //Debug.Log("---*Found: Angle to start wall: " + angle + "  (startwall dir: [" + altwallDirToConer + "]  test wall dir: [" + WallDirToCorner + "]  )");
                            if (angle < 89)
                            {
                                isAcute = true;
                            }
                            wallsFoundLog+="\n found touching wall:  between ["+start+"] and ["+dest+"]-  isAcute:"+isAcute;
                        }
                        else
                        {
                           // Debug.Log("---*skipping: no wall");
                        }
                    }
                    else
                    {
                     //   Debug.Log("---*skipping: checked already");
                    }
                }
                
                if (!maze.IsWithinBounds(dest)) continue;
                //we will now try to branch out from dest
                visited.Add(dest);
               // Debug.Log("--*Enqueing neighbors of " + dest);
                for (int i = 0; i < dest.NumberOfNeighbors(); i++)
                {
                    T nextNeighborToCheck = dest.GetNeighbor(i);
                    if (visited.Contains(nextNeighborToCheck)) continue;

                    Vector3 currentPos = maze.GetModelSpacePosition(nextNeighborToCheck);
                    float distToCorner = (cornerPos - currentPos).magnitude;
                   // Debug.Log("---*neighbor " + nextNeighborToCheck + " distSQrd from corner: " + distToCorner + "   is less than maxDist("+ maxDist + "): "+ (distToCorner <= maxDist));
                    if (distToCorner <= maxDist)
                        toCheck.Enqueue(new(dest, i));
                    else
                        wallsFoundLog +="\n        Not Enqueing neighbor (too far)[" + nextNeighborToCheck+"] dist: "+ distToCorner + " ";
                }
            }
         //   Debug.Log("-*Counted walls touching corner coord [ between " + coord + "  and " + neighbor1 + "  righside:" + isRightEnd+"]   TotalwallCount:"+ wallCounter + " has acute:" + isAcute + wallsFoundLog);
            return wallCounter;


        }
        private void GenerateMazeVisuals()
        {
            Vector3 tileOffset = maze.SingleTileModelSpaceOffset();
            tileScale = Mathf.Max(tileOffset.x, tileOffset.y, tileOffset.z);
            GenerateMesh();
            return;
        }
        private async UniTask GenerateMazeVisualsAsync(CancelBoolRef cancelRef, ProgressFloatRef progressRef)
        {
        }

        public bool IsTileVisible(T coord)
        {
            return tileVisibility.ContainsKey(coord) && tileVisibility[coord];
        }
        public virtual void SetTileVisibility(T coord, bool isVisible)
        {
            if (!tileVisibility.ContainsKey(coord) || tileVisibility[coord] != isVisible)
            {
                tileVisibility[coord] = isVisible;
                chunkRegenByIndex.Add(chunkHandler.ChunkID(coord)); 
            }
        }

    }

    /// <summary>
    /// Abstract base class for partitioning a set of tile coordinates into chunks.
    /// </summary>
    /// <typeparam name="T">
    /// The coordinate type. Must implement <see cref="ITileCoordinate{T}"/>.
    /// </typeparam>
    public abstract class Chunker<T> where T : ITileCoordinate<T>
    {
        /// <summary>
        /// Total number of chunks this <see cref="Chunker{T}"/> divides coordinates into.
        /// </summary>
        int numChunks;
        T size;

        private List<List<T>> chunkCoordinateLists;
        private Dictionary<T, int> chunkIDbyCoordinate;



        /// <summary>
        /// Initializes a new instance of the <see cref="Chunker{T}"/> class.
        /// </summary>
        /// <param name="numChunks">The number of chunks to divide the coordinate space into.</param>
        /// <param name="size">The overall size of the coordinate space. Used by subclasses to generate chunks.</param>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="numChunks"/> is less than 1.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <see cref="GenerateChunks"/> assigns a coordinate to more than one chunk.
        /// </exception>
        public Chunker( int numChunks, T size)
        {
            if (numChunks < 1)
                throw new System.ArgumentException("Number of chunks must be >= 1.", nameof(numChunks));

            this.numChunks = numChunks;
            this.size = size;

        }


        public void Build()
        {
            chunkCoordinateLists = GenerateChunks(numChunks, size);
            chunkIDbyCoordinate = new Dictionary<T, int>();

            for (int chunkCounter = 0; chunkCounter < numChunks; chunkCounter++)
            {
                List<T> coordList = chunkCoordinateLists[chunkCounter];
                foreach (T c in coordList)
                {
                    // Using Add as a sanity check: throws if duplicate coordinate found across chunks
                    chunkIDbyCoordinate.Add(c, chunkCounter);
                }
            }
        }

        /// <summary>
        /// Gets the chunk ID for the specified coordinate.
        /// </summary>
        /// <param name="coord">The coordinate to query.</param>
        /// <returns>The ID of the chunk containing <paramref name="coord"/>.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if <paramref name="coord"/> is not present in any chunk.
        /// </exception>
        public int ChunkID(T coord)
        {
            return chunkIDbyCoordinate[coord];
        }

        /// <summary>
        /// Gets all coordinates belonging to a given chunk.
        /// </summary>
        /// <param name="chunkID">The ID of the chunk to retrieve.</param>
        /// <returns>A read-only list of coordinates in the specified chunk.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="chunkID"/> is outside the valid range [0, <see cref="numChunks"/>-1].
        /// </exception>
        public IReadOnlyList<T> CoordsInChunk(int chunkID)
        {
            if (chunkID < 0 || chunkID >= numChunks)
                throw new System.ArgumentOutOfRangeException("Values passed to CoordsInChunk must equal to or greater than 0 and less than numChunks["+numChunks+ "]");

            return chunkCoordinateLists[chunkID];
        }

        public IReadOnlyList<IReadOnlyList<T>> ChunkCoordinateLists()
        {
            return chunkCoordinateLists;
        }

        /// <summary>
        /// Implemented by subclasses to generate the chunk partitioning of coordinates.
        /// The function is responsible for ensuring that all coordinate, in the given size, are present somewhere in the returned lists.
        /// </summary>
        /// <returns>A list of chunks, where each chunk is a list of coordinates.</returns>
        protected abstract List<List<T>> GenerateChunks(int numChunks, T size);
    }


    public class WallMeshChunkComputerGeneric<T> where T:ITileCoordinate<T>
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
        protected GenericMazeMap<T> map;
        protected MazeMeshDrawGeneric<T> mazeDrawer;
        protected float wallThickness;
        protected float wallHeight;

        public List<Mesh> CreateWallsMeshChunks(
                GenericMazeMap<T> data,
                 MazeMeshDrawGeneric<T> mazeDrawer,
                float wallThickness,
                float wallHeight,
                IReadOnlyList<IReadOnlyList<T>> facesPerChuck)
        {
            this.map = data;
            this.mazeDrawer = mazeDrawer;
            this.wallThickness = wallThickness;
            this.wallHeight = wallHeight;
            Debug.Log("Wall mesh computer running now");

            //build chunk by tile coord dic
            chunckIndexByFaceCoord = new Dictionary<T, int>();
            for (int chunkCounter = 0; chunkCounter < facesPerChuck.Count; chunkCounter++)
            {
                IReadOnlyList<T> listOfFaces = facesPerChuck[chunkCounter];
                foreach (T coord in listOfFaces)
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

        protected virtual Vector3 NormalAtCoord(T coord) { return Vector3.forward; }// coord.modelspace positon, normalized for faces
        protected virtual Vector3 NormalAtModelSpacePosition(Vector3 pos) { return Vector3.forward; }//  vector normalized for faces
        protected virtual Vector3 ComputeCornerPos(T coord, int neighborIndex)// use mesh verticies for faces
        {
            int neighborCount = coord.NumberOfNeighbors();
            T neighbor = coord.GetNeighbor(neighborIndex);
            Vector3 tilePosition = map.GetModelSpacePosition(coord);
            Vector3 neighborPosition = map.GetModelSpacePosition(neighbor);
            
            Vector3 wallPosition = (tilePosition + neighborPosition) / 2;
            Quaternion wallRotation = map.NeighborBorderOrientation(coord, neighborIndex);
            float neighborDist = (tilePosition - neighborPosition).magnitude;
            float computedEdgeLength = neighborDist * Mathf.Tan(Mathf.PI / neighborCount);
            Vector3 C1 = wallPosition + wallRotation * (computedEdgeLength * 0.5f * Vector3.forward);
            return C1;
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
        Dictionary<T, int> chunckIndexByFaceCoord;
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
            return v + (NormalAtModelSpacePosition(v) * wallHeight);
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

        Dictionary<T, List<int>> cornerIndecesByCoordinate = new Dictionary<T, List<int>>();
        protected virtual void BuildUniqueCorners()
        {
            List<Vector3> uniqueCornerPositions = new List<Vector3>();
           
            foreach (T coord in map.allMapCoords)
            {
                int neighborCount= coord.NumberOfNeighbors();
                List<int> faceUniqueCornerIndeces = new List<int>();
                for (int n = 0; n < neighborCount; n++)
                {
                    int n1=(n + 1).RingIndex(neighborCount);
                    Vector3 cornerPos = ComputeCornerPos(coord, n);
                    int cornerIndex = uniqueCornerPositions.FindIndex((x)=> (x-cornerPos).sqrMagnitude<0.0001f);//to do: approx == instead
                    if (cornerIndex == -1)
                    {
                        cornerIndex = uniqueCornerPositions.Count;
                        uniqueCornerPositions.Add(cornerPos);
                    }
                    faceUniqueCornerIndeces.Add(cornerIndex);
                }
                cornerIndecesByCoordinate.Add(coord,faceUniqueCornerIndeces);
            }
            foreach (Vector3 cornerVertex in uniqueCornerPositions)
            {
                uniqueCorners.Add(new Corner { position = cornerVertex });
            }
        }
        /*primary internal functions*/
        /*protected virtual void FACEBuildUniqueCorners()  //uses source mesh for facemazes.. but computes for generic 2d
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
            Vector3[] meshVertices = map.meshRef.vertices;
            int vertexCount = meshVertices.Length;

            oldToNewCornerIndex = new List<int>(new int[vertexCount]);
            Dictionary<Vector3, int> cornerMap = new();

            for (int i = 0; i < map.faceDetails.Count; i++)
            {
                var face = map.faceDetails[i];
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
        */
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
            foreach (T coord in map.allMapCoords)
            {

                int cornerCount = coord.NumberOfNeighbors();
                for (int i = 0; i < cornerCount; i++)
                {

                    int currentCornerA = cornerIndecesByCoordinate[coord][i];
                    int currentCornerB = cornerIndecesByCoordinate[coord][(i+1).RingIndex(cornerCount)];
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

                    bool hasWall = map.Walls[coord][i];
                    edge.hasVisibleWall = hasWall && (mazeDrawer.IsTileVisible(coord) || mazeDrawer.IsTileVisible(coord.GetNeighbor(i)));

                    logstr += ("\nedge between tiles " + coord + " and " + coord.GetNeighbor(i) + ", has visible wall: " + edge.hasVisibleWall + "  edge dir: " + EdgeDirFrom(edge, currentCornerA));
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

                    int edgeChunk = Mathf.Min(chunckIndexByFaceCoord[coord], chunckIndexByFaceCoord[coord.GetNeighbor(i)]);
                    edgesByChunk[edgeChunk].Add(edge);
                }
            }
            Debug.Log(logstr);
        }
        /*void FACEBuildUniqueEdges()
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

                FaceDetails face = map.faceDetails[faceCoord.faceIndex];

                // oldToNewCornerIndex  will map old corner mesh indices to unique corner indices: lookup via uniqueCornerIndex to get original Corner Index
                int cornerCount = face.cornerVertexMeshIndices.Count;

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
        }*/
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
