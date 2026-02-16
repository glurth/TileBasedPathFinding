using UnityEngine;
using System.Collections.Generic;
using EyE.Threading;
using Cysharp.Threading.Tasks;

namespace EyE.Maps.Templates
{

    public interface IMazeDrawer<T> where T : ITileCoordinate<T>
    {
        public GenericMazeMap<T> maze { get; }
        public void SetTileVisibility(T coord, bool isVisible);
    }


    public abstract class MazeDrawGeneric<T> : MonoBehaviour, IMazeDrawer<T> where T : ITileCoordinate<T>
    {
        public GenericMazeMap<T> _maze=null;
        public GenericMazeMap<T> maze
        {
            get => _maze;
        }
        public void SetMaze(GenericMazeMap<T> toValue)
        {
            _maze = toValue;
            GenerateMazeVisuals();
        }
        public void SetMazeVisibilityRef(Dictionary<T, bool> tileVisibility)
        {
            this.tileVisibility = tileVisibility;
        }
        
        protected bool skipUpdateMazeGenerationRunning = false;// locks Update function
        public async UniTask SetMazeAsync(GenericMazeMap<T> toValue, TaskHandler taskContext, ProgressFloatRef progressRef=null)
        {
            skipUpdateMazeGenerationRunning = true;
            _maze = toValue;
            await GenerateMazeVisualsAsync(taskContext,progressRef);
            skipUpdateMazeGenerationRunning = false;
        }
        public T mazeSize;

        public float tileScale = 1f;               // Size of the tiles
        public float wallThicknessFraction = 0.2f; // Thickness of the walls (fraction of tile size)
        public float wallHeightFraction = 0.2f; // Thickness of the walls (fraction of tile size)
        public float wallWidthFraction = 1f; // Thickness of the walls (fraction of tile size)

        
        public GameObject wallPrefab;              // Prefab for walls
        public GameObject floorPrefab;             // Prefab for floor tiles
        public GameObject startPositionMarkerPrefab;             // Prefab for floor tiles
        public GameObject endPositionMarkerPrefab;             // Prefab for floor tiles
        public bool drawBorderWalls = true;        // Determines if border walls should be drawn
        public bool startHidden = false;

        private List<Matrix4x4> floorMatrices = new List<Matrix4x4>(); // Transformation matrices for floors
 
        /// <summary>
        /// Stores lists of MODELSPACE matrices we will use to generate world matrices for each mesh
        /// </summary>
        private Dictionary<Mesh, List<Matrix4x4>> wallMatricesByMeshRef = new Dictionary<Mesh, List<Matrix4x4>>();


        List<Matrix4x4> worldFloorMatrices = new List<Matrix4x4>();
        //List<Matrix4x4> worldWallMatrices = new List<Matrix4x4>();
       // Dictionary<MaterialPropertyBlock, List<Matrix4x4>> OLDworldWallMatricesByPropertyBlock = new Dictionary<MaterialPropertyBlock, List<Matrix4x4>>();
        /// <summary>
        /// Stores lists of WORLD SPACE matrices we will use to draw the key mesh
        /// </summary>
        Dictionary<Mesh, List<Matrix4x4>> worldWallMatricesByMeshRef = new Dictionary<Mesh, List<Matrix4x4>>();
        
        //visibility stuff
        private Dictionary<T, bool> tileVisibility = new Dictionary<T, bool>();
        private bool visibilityChangedSinceLastDraw = true;
        private Dictionary<T, int> coordToWallIndices = new Dictionary<T, int>();

       // private List<Matrix4x4> visibleWallMatrices = new List<Matrix4x4>();   //will go away
        //private Dictionary<MaterialPropertyBlock, List<Matrix4x4>> OLDvisibleWallMatricesByPropertyBlock = new Dictionary<MaterialPropertyBlock, List<Matrix4x4>>();
        private Dictionary<Mesh, List<Matrix4x4>> visibleWallMatricesByMeshRef = new Dictionary<Mesh, List<Matrix4x4>>();


        //protected virtual bool generateCustomWallMesh => false;
        //protected virtual void GenerateMesh() { }
        //HashSet<int> chunkRegenByIndex = new HashSet<int>();
        //protected virtual void RegenChunkMesh(int meshChuckIndex) {  }
        /// <summary>
        /// //storesa reference to a mesh, and and index into the tile's wall matrixes
        /// </summary>
        class WallMatrixIndex
        {
            //public MaterialPropertyBlock mpbKey;
            public Mesh meshKey;
            public int listIndex;

            public WallMatrixIndex(Mesh meshKey, int listIndex)
            {
                this.meshKey = meshKey;
                this.listIndex = listIndex;
            }
        }
        struct CoordWallKey : System.IEquatable<CoordWallKey>
        {
            public T coord;
            public int neighborIndex;

            public CoordWallKey(T coord, int neighborIndex)
            {
                this.coord = coord;
                this.neighborIndex = neighborIndex;
            }

            public override bool Equals(object obj) => obj is CoordWallKey other && Equals(other);
            public bool Equals(CoordWallKey other) => EqualityComparer<T>.Default.Equals(coord, other.coord) && neighborIndex == other.neighborIndex;

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + EqualityComparer<T>.Default.GetHashCode(coord);
                    hash = hash * 31 + neighborIndex;
                    return hash;
                }
            }
        }
        Dictionary<CoordWallKey, WallMatrixIndex> wallMatrixIndexByCoordWallKey = new Dictionary<CoordWallKey, WallMatrixIndex>();
        //refs
        private GameObject instantiatedStartPositionMarker;
        private GameObject instantiatedEndPositionMarker;
        private Mesh floorMesh;
        private Material floorMaterial;
        private Mesh wallMesh;
        private Material wallMaterial;

      //  List<MaterialPropertyBlock> materialBlocks= new List<MaterialPropertyBlock>();



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

        protected virtual void Update()
        {
            if(!skipUpdateMazeGenerationRunning)
                DrawInstances(); // Draw the instances each frame
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
            T neighbor1 = coord.GetSpatialNeighbor(wallIndex);
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
                    dest = start.GetSpatialNeighbor(currentFromTo.Item2);

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
                    T nextNeighborToCheck = dest.GetSpatialNeighbor(i);
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

            GenerateMazeVisualsAsync(null, null).AsTask().GetAwaiter().GetResult();//fire and wait
            return;
        }

        private async UniTask GenerateMazeVisualsAsync(TaskHandler taskContext, ProgressFloatRef progressRef)
        {
            Debug.Log("Generating MazeWallMatrix");
            bool runSync = !taskContext.IsAsynchrnousProcess;//  cancelRef == null;
          //  EyE.Threading.YieldTimer yieldTimer = new EyE.Threading.YieldTimer(cancelRef, runSync);
            if (progressRef != null) progressRef.StageMessage = "Generating Visuals";
            //we don't instantiate prefabs- we just get their mats and meshes
            if (!runSync)
                await UniTask.SwitchToMainThread();



            if (floorPrefab != null)
            {
                floorMesh = floorPrefab.GetComponent<MeshFilter>().sharedMesh;
                if (floorMesh == null)
                {
                    floorMesh = RegularPolygonMesh.GeneratePolygon(maze.size.NumberOfNeighbors(), 1f, RegularPolygonMesh.SizeSpecification.Edgelength);
                }
                floorMaterial = floorPrefab.GetComponent<MeshRenderer>().sharedMaterial;
            }
            if (wallPrefab != null)
            {
                wallMesh = wallPrefab.GetComponent<MeshFilter>().sharedMesh;
                wallMaterial = wallPrefab.GetComponent<MeshRenderer>().sharedMaterial;
            }
           // Debug.Log("drawing with wall mesh: " + wallMesh.name);

            floorMatrices.Clear();
            //wallMatrices.Clear();
            WallMeshGen.GenAllVariants(wallThicknessFraction);
          //  materialBlocks.Clear();


            //trimaps
            // zero touch walls use flat on all
            // one acute angle touch - use triDisp on center only
            // all others - use triDisp on Corner and/or End ??

            //rectmaps
            // zero touch walls use flat on all
            // all others - use rectDisp on Corner

            // hexmaps
            // zero touch walls use flat on all
            // all others - use hexDisp on Corner and/or end ??



            //materialBlocks.Add();

            if (floorPrefab != null)
            {
                if (progressRef != null) progressRef.StageMessage = "Generating Visuals: floors";
                foreach (T coord in maze.allMapCoords)
                {
                    Vector3 tilePosition = maze.GetModelSpacePosition(coord);
                    Quaternion tileRotation = maze.GetModelSpaceOrientation(coord);
                    Vector3 tileScale = floorPrefab.transform.localScale;// * this.tileScale;
                    Matrix4x4 floorMatrix = Matrix4x4.TRS(tilePosition, tileRotation, tileScale);
                    floorMatrices.Add(floorMatrix);
                    //coordToFloorIndices[coord] = floorMatrices.Count - 1;//*******visibility

                    await taskContext.Yield();// yieldTimer.YieldOnTimeSlice();
                }
            }
            if(!runSync)
                await UniTask.SwitchToThreadPool();
            if (progressRef != null)
            {
                progressRef.StageMessage = "Generating Visuals: walls";
                progressRef.Value += 0.5f;
            }
            string addLog = "";
            string logstr = "";
            int wallMatrixCount = 0;
            foreach (T coord in maze.allMapCoords)
            {
                Vector3 tilePosition = maze.GetModelSpacePosition(coord);
                bool[] wallsForTile = maze.Walls[coord];
                // Debug.Log("Coord " + coord + " walls: " + string.Join(",", wallsForTile));
                int neighborCount = coord.NumberOfNeighbors();
                coordToWallIndices[coord] = wallMatrixCount;// wallMatrices.Count;  //*******visibility
                SetTileVisibility(coord, !startHidden);
                //tileVisibility[coord] = !startHidden; //*******visibility
                // Debug.Log("creating walls for coord: " + coord + "  newighbors: " + string.Join(',', coord.GetNeighbors()));
                for (int i = 0; i < neighborCount; i++)
                {
                    logstr += ("\nedge between faces " + coord + " and " + coord.GetSpatialNeighbor(i) + ", has visible wall: " + wallsForTile[i] + "  edge dir: " + GetNeighborWallMatrix(coord, i, tilePosition, neighborCount).MultiplyVector(Vector3.right).normalized);
                    if (wallsForTile[i])
                    {
                        T neighbor = coord.GetSpatialNeighbor(i);

                        // Skip creating border walls if drawBorderWalls is false and the neighbor is outside the maze bounds
                        if (!drawBorderWalls && !maze.IsWithinBounds(neighbor))
                        {
                            //Debug.Log("skipping neighbor["+i+"] with face index:"+neighbor+" due to out of bounds");
                            continue;
                        }
                       // Debug.Log("creating wall for coord: " + coord + "  neighbor idx: " + i + " (neighbor coord: " + neighbor + ")");

                        Matrix4x4 wallMatrix = GetNeighborWallMatrix(coord, i, tilePosition, neighborCount);

                        /*MaterialPropertyBlock mpb = GetNeighborWallPropertyBlock(coord, i, tilePosition, neighborCount);
                        if (!wallMatricesByPropertyBlocks.TryGetValue(mpb, out var list))// create and add empty list todic if we have not used this mpb before
                        {
                            list = new List<Matrix4x4>();
                            wallMatricesByPropertyBlocks.Add(mpb, list);
                        }*/

                        Mesh wallMesh= GetNeighborWallMesh(coord, i, tilePosition, neighborCount);
                        if (!wallMatricesByMeshRef.TryGetValue(wallMesh, out var list))// create and add empty list todic if we have not used this mpb before
                        {
                            list = new List<Matrix4x4>();
                            wallMatricesByMeshRef.Add(wallMesh, list);
                        }

                        list.Add(wallMatrix);
                        addLog += "\n***added matrix for tile["+coord+"] neighbor["+ neighbor + "]: \n" + wallMatrix;
                        wallMatrixCount++;
                        //wallMatrixIndexByCoordWallKey.Add(new CoordWallKey(coord, i), new WallMatrixIndex(mpb, list.Count - 1));
                        wallMatrixIndexByCoordWallKey.Add(new CoordWallKey(coord, i), new WallMatrixIndex(wallMesh, list.Count - 1));


                    }
                    else
                    {
                        addLog += "\n*NO wall for coord: " + coord + "  neighbor idx: " + i;
                        // Debug.Log("NO wall for coord: " + coord + "  neighbor idx: " + i + " (neighbor coord: " + coord.GetNeighbor(i) + ")");
                    }
                }
                await taskContext.Yield();// yieldTimer.YieldOnTimeSlice();
            }
            Debug.Log(logstr);


            if (!runSync)
                await UniTask.SwitchToMainThread();

            Debug.Log("Total metric count:"+ wallMatrixCount+"  log:"+addLog);
            if (startPositionMarkerPrefab != null)
            {
                if (instantiatedStartPositionMarker == null)
                    instantiatedStartPositionMarker = Instantiate(startPositionMarkerPrefab, this.transform);
                instantiatedStartPositionMarker.transform.localPosition = maze.GetModelSpacePosition(maze.start);
                instantiatedStartPositionMarker.transform.rotation = maze.GetModelSpaceOrientation(maze.start);
                instantiatedStartPositionMarker.transform.localScale = Vector3.one * tileScale;
            }
            if (endPositionMarkerPrefab != null)
            {
                if (instantiatedEndPositionMarker == null)
                    instantiatedEndPositionMarker = Instantiate(endPositionMarkerPrefab, this.transform);
                instantiatedEndPositionMarker.transform.localPosition = maze.GetModelSpacePosition(maze.end);
                instantiatedEndPositionMarker.transform.rotation = maze.GetModelSpaceOrientation(maze.end);
                instantiatedEndPositionMarker.transform.localScale = Vector3.one * tileScale;
            }
            await taskContext.Yield();// yieldTimer.YieldOnTimeSlice();
            if (progressRef != null)
            {
                progressRef.StageMessage = "Generating Visuals: transforms";
                progressRef.Value += 0.3f;
            }
            Matrix4x4 localToWorldMatrix = transform.localToWorldMatrix;
            if (!runSync)
                await UniTask.SwitchToThreadPool();
            UpdateWorldMatricies(localToWorldMatrix);
        }

        protected virtual Matrix4x4 GetNeighborWallMatrix(T coord, int neighborIndex, Vector3 tilePosition, int neighborCount)
        {
            T neighbor = coord.GetSpatialNeighbor(neighborIndex);
            float wallThickness = //this.tileScale *
                wallThicknessFraction;
            float wallHeight = //this.tileScale * 
                wallHeightFraction;
            Vector3 neighborPosition = maze.GetModelSpacePosition(neighbor);
            Vector3 wallPosition = (tilePosition + neighborPosition) / 2;
            Quaternion wallRotation = maze.NeighborBorderOrientation(coord, neighborIndex);
            float neighborDist = (tilePosition - neighborPosition).magnitude;
            float computedEdgeLength = neighborDist * Mathf.Tan(Mathf.PI / neighborCount);
            Vector3 wallScale = new Vector3(
                computedEdgeLength * wallWidthFraction,
                neighborDist * wallThickness,
                neighborDist * wallHeight
            );
           // Debug.Log("Generating wall TRS with scale: " + wallScale);
            return Matrix4x4.TRS(wallPosition, wallRotation, wallScale);
        }
        protected virtual Mesh GetNeighborWallMesh(T coord, int neighborIndex, Vector3 tilePosition, int neighborCount)
        {
            bool leftWallAcute;
            int numeLeftWall = NumTouchingWallsAtWallEnd(coord, neighborIndex, false, out leftWallAcute);
            bool rightWallAcute;
            int numeRightWall = NumTouchingWallsAtWallEnd(coord, neighborIndex, true, out rightWallAcute);
           
            return WallMeshGen.GetVariant(neighborCount, numeRightWall, rightWallAcute, numeLeftWall, leftWallAcute);
        }

        private void UpdateWorldMatricies(Matrix4x4 localToWorldMatrix)
        {
            Matrix4x4 w = localToWorldMatrix;


            Debug.Log("Recomputing wall output matricies");

            worldWallMatricesByMeshRef.Clear();
            foreach (KeyValuePair<Mesh, List<Matrix4x4>> kvp in wallMatricesByMeshRef)
            {
                List<Matrix4x4> worldMatrices = new List<Matrix4x4>();
                foreach (Matrix4x4 m in kvp.Value)
                    worldMatrices.Add(w * m);
                worldWallMatricesByMeshRef.Add(kvp.Key, worldMatrices);
            }

            worldFloorMatrices.Clear();
            foreach (Matrix4x4 m in floorMatrices)
                worldFloorMatrices.Add(w * m);
        }
        public static bool AreNotNull(params object[] objs)
        {
            foreach (object obj in objs)
                if (obj== null || obj.Equals(null) ) return false;
            return true;
        }

        /// <summary>
        /// Splits a list of matrices into chunks of up to maxSetCount each.
        /// </summary>
        public static IEnumerable<List<Matrix4x4>> InChunks(List<Matrix4x4> matrices, int maxSetCount)
        {
            if (matrices == null || matrices.Count == 0 || maxSetCount <= 0)
                yield break;

            for (int i = 0; i < matrices.Count; i += maxSetCount)
            {
                int count = System.Math.Min(maxSetCount, matrices.Count - i);
                yield return matrices.GetRange(i, count);
            }
        }

        private void DrawInstances()
        {
            if (transform.hasChanged)
            {
                transform.hasChanged = false;
                UpdateWorldMatricies(transform.localToWorldMatrix);
            }
            if (visibilityChangedSinceLastDraw)//*******visibility
                UpdateVisibleMatrices();
            const int MaxInstances = 1023;
            if (AreNotNull(floorMesh, floorMaterial))//(floorPrefab != null)
            {
                foreach (List<Matrix4x4> floorSet in InChunks(worldFloorMatrices, MaxInstances))
                //foreach (List<Matrix4x4> floorSet in InChunks(visibleFloorMatrices, MaxInstances))
                    Graphics.DrawMeshInstanced(floorMesh, 0, floorMaterial, floorSet);
            }



            //vesrion with MPBs
            int wallDrawCounter = 0;
            if (AreNotNull(wallMesh, wallMaterial))
            {
                foreach (KeyValuePair<Mesh, List<Matrix4x4>> kvp in visibleWallMatricesByMeshRef)
                {
                    Mesh mesh = kvp.Key;
                
                    foreach (List<Matrix4x4> wallSet in InChunks(kvp.Value, MaxInstances))
                    {
                        Graphics.DrawMeshInstanced(mesh, 0, wallMaterial, wallSet);

                    }
                    wallDrawCounter += kvp.Value.Count;
                }
            }
            Debug.Log("Num walls drawn: " + wallDrawCounter);
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

                visibilityChangedSinceLastDraw = true;
            }
        }
        private void UpdateVisibleMatrices()
        {
            // Clear all visible lists

            foreach (var kvp in visibleWallMatricesByMeshRef)
                kvp.Value.Clear();


            foreach (KeyValuePair<T, bool> kvp in tileVisibility)
            {
                if (!kvp.Value) continue;
                T coord = kvp.Key;


                int wallCount = coord.NumberOfNeighbors();
                for (int i = 0; i < wallCount; i++)
                {
                    if (maze.Walls[coord][i])
                    {

                        WallMatrixIndex idx = wallMatrixIndexByCoordWallKey[new CoordWallKey(coord, i)];// want an exception thrown if not valid-it SHOULD/MUST be
                        Matrix4x4 worldMatrix = worldWallMatricesByMeshRef[idx.meshKey][idx.listIndex];// want an exception thrown if not valid-it SHOULD/MUST be
                        if (!visibleWallMatricesByMeshRef.TryGetValue(idx.meshKey, out List<Matrix4x4> list))
                        {
                            list = new List<Matrix4x4>();
                            visibleWallMatricesByMeshRef.Add(idx.meshKey, list);
                        }


                        list.Add(worldMatrix);
                    }
                }

            }

            visibilityChangedSinceLastDraw = false;
        }
    }

    public struct WallMeshVariantKey
    {

        static int AsInt(int numTileNeighbors, int numTouchingWallsRightEnd, bool rightWallAcuteAngle, int numTouchingWallsLeftEnd, bool leftWallAcuteAngle)
        {
            int i = 0;
            if (numTileNeighbors == 3)//9 combos
            {
                if (numTouchingWallsRightEnd == 0)
                    i += 0;
                else if (numTouchingWallsRightEnd == 1 && rightWallAcuteAngle)
                    i += 1;
                else //
                    i += 2;

                if (numTouchingWallsLeftEnd == 0)
                    i += 0;
                else if (numTouchingWallsLeftEnd == 1 && leftWallAcuteAngle)
                    i += 3;
                else //
                    i += 6;
            }
            else if (numTileNeighbors == 4)//4 combos
            {
                i = 9;//skip 9 above

                if (numTouchingWallsLeftEnd == 0)
                    i += 0;
                else
                    i += 1;
                if (numTouchingWallsRightEnd == 0)
                    i += 0;
                else
                    i += 2;

            }
            else if (numTileNeighbors > 4)// 4 combos
            {
                i = 13;//skip 13 above

                if (numTouchingWallsLeftEnd != 0)
                    i += 1;
                if (numTouchingWallsRightEnd != 0)
                    i += 2;

            }

            return i;
        }
        static public void ParamsFromInt(int i, out int numTileNeighbors, out int numTouchingWallsRightEnd, out bool rightWallAcuteAngle, out int numTouchingWallsLeftEnd, out bool leftWallAcuteAngle)
        {
            if (i < 9) // 3 neighbors
            {
                numTileNeighbors = 3;
                int r = i % 3;
                int l = i / 3;

                if (r == 0) { numTouchingWallsRightEnd = 0; rightWallAcuteAngle = false; }
                else if (r == 1) { numTouchingWallsRightEnd = 1; rightWallAcuteAngle = true; }
                else { numTouchingWallsRightEnd = 1; rightWallAcuteAngle = false; }

                if (l == 0) { numTouchingWallsLeftEnd = 0; leftWallAcuteAngle = false; }
                else if (l == 1) { numTouchingWallsLeftEnd = 1; leftWallAcuteAngle = true; }
                else { numTouchingWallsLeftEnd = 1; leftWallAcuteAngle = false; }
            }
            else if (i < 13) // 4 neighbors
            {
                numTileNeighbors = 4;
                int x = i - 9;

                numTouchingWallsLeftEnd = (x & 1) != 0 ? 1 : 0;
                leftWallAcuteAngle = false;

                numTouchingWallsRightEnd = (x & 2) != 0 ? 1 : 0;
                rightWallAcuteAngle = false;
            }
            else // >4 neighbors
            {
                numTileNeighbors = 5; // any number > 4
                int x = i - 13;

                numTouchingWallsLeftEnd = (x & 1) != 0 ? 1 : 0;
                leftWallAcuteAngle = false;

                numTouchingWallsRightEnd = (x & 2) != 0 ? 1 : 0;
                rightWallAcuteAngle = false;
            }
        }

        int key;
        public const int maxValue = 17;
        public WallMeshVariantKey(int numTileNeighbors, int numTouchingWallsRightEnd, bool rightWallAcuteAngle, int numTouchingWallsLeftEnd, bool leftWallAcuteAngle)
        {
            key = AsInt( numTileNeighbors,  numTouchingWallsRightEnd,  rightWallAcuteAngle,  numTouchingWallsLeftEnd,  leftWallAcuteAngle);
        }
        public override bool Equals(object obj)
        {
            if (!(obj is WallMeshVariantKey)) return false;
            WallMeshVariantKey other = (WallMeshVariantKey)obj;
            return key == other.key;
        }

        public override int GetHashCode() { return key; }
        public override string ToString() { return key.ToString(); }
    }
 
}
