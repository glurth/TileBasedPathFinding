using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using EyE.Threading;
using Cysharp.Threading.Tasks;
using System.Threading;
using EyE.Geometry;

static public class strext
{
    /// <summary>
    /// Concatenates the string representations of the elements in a sequence,
    /// using the provided function to convert each element to a string, and inserting the
    /// specified separator between each converted string.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="items">The sequence of elements to join.</param>
    /// <param name="customToString">A function that converts each element into a string.</param>
    /// <param name="separator">The string to use as a separator. Default is ", ".</param>
    /// <returns>
    /// A single concatenated string of the selected values, separated by the
    /// specified separator. Returns an empty string if <paramref name="items"/> is empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="items"/>, <paramref name="customToString"/>, or 
    /// <paramref name="separator"/> is null.
    /// </exception>
    /// <exception cref="Exception">
    /// Any exception thrown by <paramref name="customToString"/> for an element will propagate.
    /// </exception>
    public static string Join<T>(IEnumerable<T> items, System.Func<T, string> customToString, string separator = ", ")
    {
        System.Text.StringBuilder sb = new();
        bool first = true;

        foreach (T item in items)
        {
            if (!first) sb.Append(separator);
            sb.Append(customToString(item));
            first = false;
        }

        return sb.ToString();
    }

}



namespace EyE.Maps.Templates
{

    public abstract class MazeMeshDrawBase : MonoBehaviour
    {
        // ================================
        // Map Abstraction (non-generic)
        // ================================

        public abstract GenericMazeMapBase mazeBase { get; }
        public abstract UniTask SetMazeAsync<T>(GenericMazeMap<T> toValue, TaskHandler taskContext) where T : ITileCoordinate<T>;
        public abstract UniTask SetMazeAsync(GenericMazeMapBase toValue, TaskHandler taskContext);

        public Vector3 mazeNormal = Vector3.up;
        /// <summary>
        /// Radius of a circle INSCRIBED on the tile
        /// </summary>
        public float TileScale { get => tileScale; }
        protected float tileScale = 1f;
        public bool wallDimensionsAsFractionOfTileScale=false;
        public float wallThicknessFraction = 0.2f;
        public float wallHeightFraction = 0.2f;
        public float wallWidthFraction = 1f;
        public bool drawBorderWalls = true;        // Determines if border walls should be drawn
        public bool startHidden = false;

        public GameObject startPositionMarkerPrefab;             // Prefab for floor tiles
        public GameObject endPositionMarkerPrefab;             // Prefab for floor tiles
        /// <summary>
        /// mostly used for testing- automatically create a random maze upon enable.
        /// </summary>
        public bool createMazeOnEnable = true;
        public Material wallChunkMaterial;
        public Material wallChunkSubMeshMaterial;

        public abstract void BakeMapTexture(RenderTexture targetTexture);
        public abstract bool IsTileVisible(ITileCoordinateBase coord);
        public abstract void SetTileVisibility(ITileCoordinateBase coord, bool isVisible);

        public class WallDrawConfig
        {
            public enum DrawType { Quads, SurfaceDepth, MeshTiling }
            public DrawType drawOption= DrawType.Quads;

            public Texture2D surfaceDepthMap=null;
            public Vector2Int surfaceDepthTiling;

            public MeshData normalizedMesh;
            public Vector2 preNormalizedRelativeBounds;

        }
    }

    /// <summary>
    /// Base class for drawing a maze using mesh chunks.
    /// Handles maze assignment, visibility, and asynchronous mesh generation.
    /// Subclass this to implement specific maze coordinate systems (e.g., grid, hex, polar).
    /// </summary>
    /// <typeparam name="T">
    /// Coordinate type implementing <see cref="ITileCoordinate{T}"/> used to index maze tiles.
    /// </typeparam>
    public abstract class MazeMeshDrawGeneric<T> : MazeMeshDrawBase, IMazeDrawer<T> where T : ITileCoordinate<T>
    {
       // public Vector3 mazeNormal = Vector3.up;//-Vector3.forward;

        public override GenericMazeMapBase mazeBase{get => _maze;}
        public override UniTask SetMazeAsync(GenericMazeMapBase toValue, TaskHandler taskContext)
        {
            return SetMazeAsync<T>((GenericMazeMap<T>)toValue, taskContext);
        }
        public override void SetTileVisibility(ITileCoordinateBase coord, bool isVisible)
        {
            SetTileVisibility((T)coord, isVisible);
        }
        public override bool IsTileVisible(ITileCoordinateBase coord)
        {
            return IsTileVisible((T)coord);
        }

        [SerializeField]//for debug
        GenericMazeMap<T> _maze = null;
        public GenericMazeMap<T> maze{get => _maze;}

        /// <summary>
        /// Sets an external visibility dictionary reference for per-tile visibility control.
        /// </summary>
        /// <param name="tileVisibility">Dictionary mapping tile coordinates to visibility state.</param>
        public void SetMazeVisibilityRef(Dictionary<T, bool> tileVisibility)
        {
            this.tileVisibility = tileVisibility;
        }

        protected bool mazeGenerationRunning = false;// locks Update function

        /// <summary>
        /// Asynchronously assigns a maze and builds its mesh on a background thread.
        /// </summary>
        /// <param name="toValue">Maze to assign.</param>
        /// <param name="cancelRef">Optional cancellation flag reference.</param>
        /// <param name="progressRef">Optional progress tracking reference (0–1).</param>
        /// <returns>Awaitable UniTask that completes when mesh generation finishes.</returns>
        public override async UniTask SetMazeAsync<TCoord>(GenericMazeMap<TCoord> toValue, TaskHandler taskContext) 
        // public async UniTask SetMazeAsync(GenericMazeMap<T> toValue, TaskHandler taskContext)
        {

            this.taskContext = taskContext;
            mazeGenerationRunning = true;
            //await UniTask.SwitchToThreadPool();

            await taskContext.SetStageMessageAndYield("Starting Tasks");

            if (toValue is GenericMazeMap<T> typedMaze)
            {
                _maze = typedMaze;
            }
            else
            {
                throw new ArgumentException("Maze object passed to " + GetType() + ".SetMazeAsync(), is not the correct type of maze: " + toValue.GetType());
            }
            //_maze = toValue;
            await taskContext.SetStageMessageAndYield("Building Chunks");
            await BuildChucksAsync(taskContext);
            await taskContext.SetStageMessageAndYield("Building Maze Visuals");
            await GenerateMazeVisualsAsync(taskContext);
            await taskContext.SetStageMessageAndYield("Done");
            mazeGenerationRunning = false;
            taskContext.SetComplete();
            //processingIndicator.SetActive(false);
            chunkRegenByIndex.Clear();//ensure we dont regen right away
            
        }
        
        public T mazeSize
        {
            get
            {
                if (_maze != null) return _maze.size;
                return DefaultMazeSize();
            }
        }

        //instance refs
        public Texture2D sideDepthMap;
        public Mesh notNormalizedWallMesh;
        private GameObject instantiatedStartPositionMarker;
        private GameObject instantiatedEndPositionMarker;

        //visibility stuff
        private Dictionary<T, bool> tileVisibility = new Dictionary<T, bool>();


        //chunking
      //  public int numberOfChunks;
        protected Chunker<T> chunkHandler;
        HashSet<int> chunkRegenByIndex = new HashSet<int>();
        /// <summary>
        /// Rebuilds a single chunk’s wall mesh and assigns it to its MeshFilter.
        /// Override if you want custom rebuild logic or pooled mesh reuse.
        /// </summary>
        /// <param name="chunkIndex">Index of the chunk to regenerate.</param>
        protected virtual void RegenChunkMesh(int chunkIndex)
        {
            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
            timer.Start();
           // wallChunkMeshFilters[chunkIndex].sharedMesh = meshComputer.RebuildSingleChunk(chunkIndex);
           Mesh newMesh = meshComputer.RebuildSingleChunk(chunkIndex);
        
            if (wallChunkMeshes[chunkIndex]!=null)
                Destroy(wallChunkMeshes[chunkIndex]);
            wallChunkMeshes[chunkIndex] = newMesh;// meshComputer.RebuildSingleChunk(chunkIndex);
            timer.Stop();
            Debug.Log("Regen of chunk ["+chunkIndex+"] time: " + timer.Elapsed);
        }

        private TaskHandler taskContext;// = new TaskContext();
        /// <summary>
        /// Called when the component is enabled.  
        /// Default implementation optionally creates and assigns a new maze if <see cref="createMazeOnEnable"/> is true.
        /// </summary>
        /// <remarks>
        /// Override if you need to inject dependencies or control initialization order,  
        /// but be sure to call <c>base.OnEnable()</c> unless you intentionally skip maze creation.
        /// </remarks>
        protected void OnEnable()
        {
            if (createMazeOnEnable && !mazeGenerationRunning)
            {
             //   GenericMazeMap<T> newMaze = CreateMazeMap();
             //   SetMaze(newMaze);
             //   return;
                taskContext = new TaskHandler();
                // taskContext.task = UniTask.RunOnThreadPool(() => SetMazeAsync(newMaze, taskContext));
                Debug.Log("Launching Maze Gen Process");
                mazeGenerationRunning = true;
                taskContext.AssignRunningTask(UniTask.RunOnThreadPool(async () =>
                {
                    try
                    {
                        await taskContext.SetStageMessageAndYield("Creating random maze");
                        GenericMazeMap<T> newMaze = await CreateMazeMapAsync(taskContext);
                        await taskContext.Yield();
                        await SetMazeAsync(newMaze, taskContext);
                    }
                    catch (System.OperationCanceledException e)
                    {
                        //taskContext.CancellationSource.Dispose();
                        taskContext.SetComplete();
                        mazeGenerationRunning = false;
                        chunkRegenByIndex.Clear();
                        throw e;
                        //   processingIndicator.gameObject.SetActive(false);
                    }
                    catch (System.Exception e)
                    {
                        await UniTask.SwitchToMainThread();
                        Debug.LogException(e);
                        throw;
                    }
                }));
                // taskContext.task = SetMazeAsync(newMaze, taskContext); //launch the async SetMazeFunction, and proceed with it running asynchronously
                if(processingIndicator!=null)
                    processingIndicator.SetContext(taskContext);//.SetActive(true);
            }
            
        }

        /// <summary>
        /// Called internally to (re)allocate and initialize maze chunks.
        /// Subclasses should not override directly — instead, override <see cref="GetChunker"/> to define how chunking is performed.
        /// </summary>
        void BuildChucks()
        {
           // Debug.Log("Allocating chunks");
            chunkHandler = GetChunker(trisPerWall, idealTrisPerChunk);
            chunkHandler.Build();
          //  Debug.Log("Allocated " + chunkHandler.ChunkCoordinateLists().Count + " chunks");
        }
        /// <summary>
        /// Called internally to (re)allocate and initialize maze chunks.
        /// Subclasses should not override directly — instead, override <see cref="GetChunker"/> to define how chunking is performed.
        /// </summary>
        async UniTask BuildChucksAsync(TaskHandler taskContext)
        {
            //  Debug.Log("Allocating chunks");
            await taskContext.Yield();
            chunkHandler = GetChunker(trisPerWall, idealTrisPerChunk);
            await chunkHandler.BuildAsync(taskContext);
          //  Debug.Log("Allocated " + chunkHandler.ChunkCoordinateLists().Count + " chunks");
        }


        /// <summary>
        /// Must return a fully constructed <see cref="Chunker{T}"/> instance suitable for the maze type.
        /// Called by <see cref="BuildChucks"/> when assigning or rebuilding a maze.
        /// </summary>
        /// <remarks>
        /// Typical implementation: create a concrete Chunker subclass configured for the current maze coordinate system.
        /// </remarks>
        /// <example>
        /// <code>
        /// protected override Chunker<GridCoord> GetChunker()
        /// {
        ///     return new GridChunker(maze, chunkSize: 8);
        /// }
        /// </code>
        /// </example>
        protected abstract Chunker<T> GetChunker(int trisPerWall = 12, int idealTrisPerChunk = 1000);
        public int idealTrisPerChunk = 1000;


        public TaskContextDisplay processingIndicator;
        /// <summary>
        /// Called once per frame to rebuild any chunks marked for regeneration.
        /// Override only to change how chunk updates are applied (e.g., batching or deferred updates).
        /// </summary>
        /// <remarks>
        /// The default implementation regenerates all marked chunks immediately using <see cref="RegenChunkMesh"/>.
        /// </remarks>
        protected virtual void Update()
        {
            if (taskContext==null || !taskContext.IsComplete)
            {
                return;//still processing- dont update yet
            }
            if (maze == null) return;
            if (startPositionMarkerPrefab != null)
            {
                if (instantiatedStartPositionMarker == null)
                {
                    instantiatedStartPositionMarker = Instantiate(startPositionMarkerPrefab, this.transform);
                    instantiatedStartPositionMarker.transform.localPosition = maze.GetModelSpacePosition(maze.start);
                    instantiatedStartPositionMarker.transform.rotation = maze.GetModelSpaceOrientation(maze.start);
                    instantiatedStartPositionMarker.transform.localScale = Vector3.one * tileScale;
                }
            }
            if (endPositionMarkerPrefab != null)
            {
                if (instantiatedEndPositionMarker == null)
                {
                    instantiatedEndPositionMarker = Instantiate(endPositionMarkerPrefab, this.transform);
                    instantiatedEndPositionMarker.transform.localPosition = maze.GetModelSpacePosition(maze.end);
                    instantiatedEndPositionMarker.transform.rotation = maze.GetModelSpaceOrientation(maze.end);
                    instantiatedEndPositionMarker.transform.localScale = Vector3.one * tileScale;
                }
            }

            foreach (int chunkIndex in chunkRegenByIndex)
            {
                RegenChunkMesh(chunkIndex);
            }
            chunkRegenByIndex.Clear();
        }

        public bool showDebugGUI = false;
        void OnGUI()
        {
            if(showDebugGUI)
                taskContext?.OnGUIDebug();
        }

        WallMeshChunkComputerGeneric<T> meshComputer = null;
        
        /// <summary>
        /// Creates a new <see cref="WallMeshChunkComputerGeneric{T}"/> instance.
        /// Override to provide a specialized mesh computer (e.g. different geometry rules).
        /// </summary>
        protected virtual WallMeshChunkComputerGeneric<T> GetNewMeshComputer()
        {
            return new WallMeshChunkComputerGeneric<T>();
        }

        async UniTask GenerateWallChunkMeshesAsync(TaskHandler taskContext)
        {
            //   Debug.Log("Setting visiblity for all tiles to: " + !startHidden);
            await taskContext.SetStageMessageAndYield("Generation: resetting tile visiblity, creating generation object");
            foreach (T coord in maze.allMapCoords)
            {
                SetTileVisibility(coord, !startHidden);
            }

            meshComputer = GetNewMeshComputer();
            float scale = 1f;
            if (wallDimensionsAsFractionOfTileScale) scale = tileScale;
            MeshData normalizedWallMesh = null;
            if (notNormalizedWallMesh != null)
            {
                normalizedWallMesh = new MeshData(notNormalizedWallMesh);
                normalizedWallMesh.Normalize();
            }
            List<MeshData> chunkMeshes = await meshComputer.CreateWallsMeshChunksAsync(maze, this, scale * wallThicknessFraction, scale * wallHeightFraction, chunkHandler.ChunkCoordinateLists(), taskContext,true, sideDepthMap, normalizedWallMesh);

            if (taskContext.IsCancellationRequested) return;

            await UniTask.SwitchToMainThread();
            wallChunkMeshes = new List<Mesh>(chunkMeshes.Count);
            for (int i = 0; i < chunkMeshes.Count; i++)
            {
                wallChunkMeshes.Add(chunkMeshes[i].ToMesh());
            }
            Debug.Log("Number of chunks generated: " + wallChunkMeshes.Count);
        }
        
        void GenerateWallChunkMeshes()
        {
            TaskHandler taskContext = new TaskHandler(false);

            try
            {
                // 1. Call the async method, which returns a UniTask.
                // 2. Call .Wait() to BLOCK the current thread until the UniTask completes.
                GenerateWallChunkMeshesAsync(taskContext).GetAwaiter().GetResult();
              
              
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            return;
        }



        private List<Mesh> wallChunkMeshes = new List<Mesh>();
        private Matrix4x4 cachedWorldTransform; // Stores the last calculated matrix
        //renders chunckmeshes to main camera using Graphics.DrawMesh
        void LateUpdate()
        {
            // Check if we have meshes and material
            if (wallChunkMeshes == null || wallChunkMaterial == null)
            {
                return;
            }

            // --- OPTIMIZATION STEP: Check if the transform moved ---
            if (transform.hasChanged)
            {
                cachedWorldTransform = transform.localToWorldMatrix;
                transform.hasChanged = false;
            }

            // If the transform hasn't changed, we draw using the old cachedWorldTransform.

            const int WallLayer = 0;

          //  Debug.Log("drawing chucnkmeshes now: " + wallChunkMeshes.Count);
            // Issue a draw call for every single unique mesh
            foreach (Mesh mesh in wallChunkMeshes)
            {
                if (mesh != null)
                {
                    Graphics.DrawMesh(
                        mesh,
                        cachedWorldTransform, // Draw with the cached matrix
                        wallChunkMaterial,
                        WallLayer,
                        Camera.main,
                        0,
                        null,
                        true,
                        true
                    );
                    Graphics.DrawMesh(
                        mesh,
                        cachedWorldTransform, // Draw with the cached matrix
                        wallChunkSubMeshMaterial,
                        WallLayer,
                        Camera.main,
                        1,
                        null,
                        true,
                        true
                    );
                }
            }
        }

        /// <summary>
        /// Renders the current state of the wallChunkMeshes to a target RenderTexture 
        /// using a dedicated orthographic top-down camera view, and cleans up all temporary objects.
        /// </summary>
        /// <param name="targetTexture">The RenderTexture to draw the map onto.</param>
        public override void BakeMapTexture(RenderTexture targetTexture)
        {
            if (wallChunkMeshes == null || wallChunkMeshes.Count == 0 || wallChunkMaterial == null)
            {
                Debug.LogError("Bake failed: Wall meshes or material not set.");
                return;
            }

            // --- NESTED HELPER FUNCTIONS START ---

            // Helper function to transform a local Bounds object into world space.
            Bounds GetTransformedBounds(Bounds localBounds, Matrix4x4 transformMatrix)
            {
                Bounds newBounds = new Bounds();
                Vector3 min = localBounds.min;
                Vector3 max = localBounds.max;

                // The 8 corners of the local AABB (Axis-Aligned Bounding Box)
                Vector3[] corners = new Vector3[]
                {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z)
                };

                // Transform and encapsulate all 8 corners
                foreach (Vector3 corner in corners)
                {
                    newBounds.Encapsulate(transformMatrix.MultiplyPoint(corner));
                }
                return newBounds;
            }

            // Calculates the total world-space bounding box that encompasses all wall geometry.
            Bounds CalculateCombinedWorldBounds()
            {
                Matrix4x4 mazeWorldTransform = transform.localToWorldMatrix;

                // Initialize the bounds with the first mesh, transformed to world space.
                Bounds combinedBounds = GetTransformedBounds(wallChunkMeshes[0].bounds, mazeWorldTransform);

                // Iterate over the rest of the meshes and encapsulate the bounds
                for (int i = 1; i < wallChunkMeshes.Count; i++)
                {
                    Bounds localBounds = wallChunkMeshes[i].bounds;
                    Bounds worldBounds = GetTransformedBounds(localBounds, mazeWorldTransform);
                    combinedBounds.Encapsulate(worldBounds);
                }

                return combinedBounds;
            }

            // --- NESTED HELPER FUNCTIONS END ---


            // --- BAKE LOGIC START ---

            // 1. Calculate the Precise World Bounds
            Bounds totalWorldBounds = CalculateCombinedWorldBounds();

            // 2. Camera Setup (Create and Configure)
            GameObject cameraGO = new GameObject("Temp_Map_Bake_Camera", typeof(Camera));
            Camera bakeCameraInstance = cameraGO.GetComponent<Camera>();

            const int BakeLayer = 30; // Dedicated layer for the bake process

            // Set Target
            bakeCameraInstance.targetTexture = targetTexture;

            // View Setup
            bakeCameraInstance.orthographic = true;
            bakeCameraInstance.transform.position = totalWorldBounds.center + Vector3.up * 100f; // Position high above center
            bakeCameraInstance.transform.rotation = Quaternion.Euler(90f, 0f, 0f);           // Look straight down

            // Sizing
            bakeCameraInstance.orthographicSize = Mathf.Max(totalWorldBounds.size.x, totalWorldBounds.size.z) / 2f;
            bakeCameraInstance.nearClipPlane = 1f;
            bakeCameraInstance.farClipPlane = 200f;

            // Rendering Setup
            bakeCameraInstance.cullingMask = 1 << BakeLayer; // Only see the wall geometry
            bakeCameraInstance.clearFlags = CameraClearFlags.SolidColor;
            bakeCameraInstance.backgroundColor = Color.black;

            // 3. Prepare Geometry (Temporarily set layer)
            int originalLayer = gameObject.layer;
            gameObject.layer = BakeLayer;

            // NOTE: This assumes your LateUpdate() logic or mesh combination logic 
            // will be run and issue the Graphics.DrawMesh calls for this frame, 
            // or that the single combined mesh is already assigned to this GameObject 
            // and is placed on the BakeLayer.

            // 4. Forced Render (The Rasterization Step)
            bakeCameraInstance.Render();

            // 5. Cleanup

            // Reset the layer of the main object
            gameObject.layer = originalLayer;

            // Destroy the temporary camera and its GameObject
            DestroyImmediate(cameraGO);

            Debug.Log("Map Bake Complete. Output Resolution: " + targetTexture.width + "x" + targetTexture.height);
        }

        /// <summary>
        /// Implemented by subclasses to construct and return a new maze map instance.
        /// Called during initialization or when resetting the maze.
        /// </summary>
        /// <remarks>
        /// Should use <see cref="mazeSize"/> to define dimensions or shape.
        /// </remarks>
        /// <example>
        /// <code>
        /// protected override GenericMazeMap<GridCoord> CreateMazeMap()
        ///     => new GenericMazeMap<GridCoord>(mazeSize);
        /// </code>
        /// </example>
        protected abstract GenericMazeMap<T> CreateMazeMap();

        protected abstract UniTask<GenericMazeMap<T>> CreateMazeMapAsync(TaskHandler taskContext);

        public T editorDefinedCreateOnEnableSize;
        /// <summary>
        /// Provides the default maze size used by <see cref="Reset"/> and first-time initialization.
        /// </summary>
        /// <example>
        /// <code>
        /// protected override GridCoord DefaultMazeSize() => new GridCoord(10, 10);
        /// </code>
        /// </example>
        protected virtual T DefaultMazeSize() { return editorDefinedCreateOnEnableSize; }
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

        /// <summary>
        /// Called by <see cref="Update"/> and mesh generation routines to count adjacent walls meeting at a corner.
        /// Subclasses generally do not override this unless the maze coordinate system defines nonstandard wall connectivity.
        /// </summary>
        /// <param name="coord">Tile coordinate owning the wall.</param>
        /// <param name="wallIndex">Index of the wall being tested.</param>
        /// <param name="isRightEnd">Whether to check the right end of the wall.</param>
        /// <param name="isAcute">Outputs whether the corner forms an acute angle.</param>
        /// <returns>Number of touching walls at that wall end.</returns>
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
            tileScale = 0.5f * tileOffset.magnitude;//   Mathf.Max(Mathf.Abs(tileOffset.x), Mathf.Abs(tileOffset.y), Mathf.Abs(tileOffset.z));
            GenerateWallChunkMeshes();
            return;
        }
        
        private async UniTask GenerateMazeVisualsAsync(TaskHandler taskContext)
        {
            Vector3 tileOffset = maze.SingleTileModelSpaceOffset();
            tileScale = 0.5f*tileOffset.magnitude;// Mathf.Max(Mathf.Abs(tileOffset.x), Mathf.Abs(tileOffset.y), Mathf.Abs(tileOffset.z));
            await GenerateWallChunkMeshesAsync(taskContext);
            return;
        }


        public bool IsTileVisible(T coord)
        {
            return tileVisibility.ContainsKey(coord) && tileVisibility[coord];
        }


        public virtual void SetTileVisibility(T coord, bool isVisible)
        {
          //  if (!maze.IsWithinBounds(coord)) return;
            if (!tileVisibility.ContainsKey(coord) || tileVisibility[coord] != isVisible)
            {
                tileVisibility[coord] = isVisible;
                int thisCoordChunk = chunkHandler.ChunkID(coord);
                chunkRegenByIndex.Add(thisCoordChunk);
                //add neighborchunks, incase on border
                foreach (T neighbor in coord.GetSpatialNeighbors())
                {
                    if (maze.IsWithinBounds(neighbor))
                    {
                        int neighborChunk = chunkHandler.ChunkID(neighbor);
                        if (neighborChunk != thisCoordChunk)
                            chunkRegenByIndex.Add(neighborChunk); //hashset willjust skip if already present.
                    }
                }
            }
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in wallChunkMeshes)
                if (mesh != null)
                    Destroy(mesh);
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
        readonly int trisPerWall = 12;
        //readonly int maxTrisPerMesh = 65000;
        readonly int idealTrisPerChunk = 1000;

        /// <summary>
        /// Total number of chunks this <see cref="Chunker{T}"/> divides coordinates into.
        /// </summary>
        int numChunks;
        //
        T size;
        // this list contains a list for each chunk- which each of those lists containing all the coordinates in that chunk. Populated on Build().
        private List<List<T>> chunkCoordinateLists;
        // used for fast lookup of which chunk a given coordinate belongs to.  Populated on Build().
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
        public Chunker(T size,int trisPerWall=12,  int idealTrisPerChunk=1000)
        {
            this.idealTrisPerChunk = idealTrisPerChunk;
            this.size = size;
            this.trisPerWall = trisPerWall;
        }

        public void Build()
        {
            BuildAsync(new TaskHandler(false)).AsTask().GetAwaiter().GetResult();
            return;
        }

        async public UniTask BuildAsync(TaskHandler taskContext)
        {
            int totalNumTiles = NumberOfTilesInSize(size);
            int totalTris = (size.NumberOfNeighbors() * trisPerWall * totalNumTiles) / 2; /// ISSUE : for poly/face mazes- numneghbors is per face.  need virtual/abstract totalTri's?
            if (totalTris < idealTrisPerChunk) numChunks = 1;
            else
            {
                numChunks = totalTris / idealTrisPerChunk;
            }

            await taskContext.SetStageMessageAndYield("Chunk Generation");
            chunkCoordinateLists =await GenerateChunksAsync(numChunks, size, taskContext);
            numChunks = chunkCoordinateLists.Count;
            chunkIDbyCoordinate = new Dictionary<T, int>();
            await taskContext.SetStageMessageAndYield("Chunks generated, create reverse lookup");

            //string logstr = "";
            for (int chunkCounter = 0; chunkCounter < numChunks; chunkCounter++)
            {
                List<T> coordList = chunkCoordinateLists[chunkCounter];
                foreach (T c in coordList)
                {
                    // Using Add as a sanity check: throws if duplicate coordinate found across chunks
                    chunkIDbyCoordinate.Add(c, chunkCounter);
                    //  logstr += "\nCoordinate " + c + " is in chunk " + chunkCounter;
                }
            }
            await  taskContext.SetStageMessageAndYield("Chunks generation complete");
            //  Debug.Log("Built chunks (num:"+numChunks+"): " + logstr);
        }

        protected List<List<T>> GenerateVector2IntChunks(
                int numChunks,
                T size,
                System.Func<T, Vector2Int> ToVector,
                System.Func<Vector2Int, T> FromVec)
        {
            return GenerateVector2IntChunksAsync(numChunks, size, ToVector, FromVec, new TaskHandler(false)).AsTask().GetAwaiter().GetResult();
            /*int chunksX = Mathf.CeilToInt(Mathf.Sqrt(numChunks));
            int chunksY = Mathf.CeilToInt(numChunks / (float)chunksX);

            Vector2Int s = ToVector(size);
            int chunkWidth = Mathf.CeilToInt(s.x / (float)chunksX);
            int chunkHeight = Mathf.CeilToInt(s.y / (float)chunksY);

            List<List<T>> coordinatesPerChunk = new List<List<T>>();

            for (int cy = 0; cy < chunksY; cy++)
            {
                for (int cx = 0; cx < chunksX; cx++)
                {
                    var cluster = new List<T>();
                    int startX = cx * chunkWidth;
                    int startY = cy * chunkHeight;
                    int endX = Mathf.Min(s.x, startX + chunkWidth);
                    int endY = Mathf.Min(s.y, startY + chunkHeight);

                    for (int x = startX; x < endX; x++)
                    {
                        for (int y = startY; y < endY; y++)
                        {
                            cluster.Add(FromVec(new Vector2Int(x, y)));
                        }
                    }
                    coordinatesPerChunk.Add(cluster);
                }
            }
            return coordinatesPerChunk;*/
        }

        protected async UniTask<List<List<T>>> GenerateVector2IntChunksAsync(
                            int numChunks,
                            T size,
                            System.Func<T, Vector2Int> ToVector,
                            System.Func<Vector2Int, T> FromVec,
                            TaskHandler taskContext)
        {
            int chunksX = Mathf.CeilToInt(Mathf.Sqrt(numChunks));
            int chunksY = Mathf.CeilToInt(numChunks / (float)chunksX);

            Vector2Int s = ToVector(size);
            int chunkWidth = Mathf.CeilToInt(s.x / (float)chunksX);
            int chunkHeight = Mathf.CeilToInt(s.y / (float)chunksY);

            List<List<T>> coordinatesPerChunk = new List<List<T>>();
            float progressInc = 1f / numChunks;
            for (int cy = 0; cy < chunksY; cy++)
            {
                for (int cx = 0; cx < chunksX; cx++)
                {
                    var cluster = new List<T>();
                    int startX = cx * chunkWidth;
                    int startY = cy * chunkHeight;
                    int endX = Mathf.Min(s.x, startX + chunkWidth);
                    int endY = Mathf.Min(s.y, startY + chunkHeight);

                    for (int x = startX; x < endX; x++)
                    {
                        for (int y = startY; y < endY; y++)
                        {
                            cluster.Add(FromVec(new Vector2Int(x, y)));
                        }
                    }
                    coordinatesPerChunk.Add(cluster);
                    taskContext.IncrementProgress(progressInc);
                    await taskContext.Yield();
                }

            }
            return coordinatesPerChunk;
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
        protected List<List<T>> GenerateChunks(int numChunks, T size)
        {
            return GenerateChunksAsync(numChunks,size,new TaskHandler(false)).AsTask().GetAwaiter().GetResult();
        }

        protected abstract UniTask<List<List<T>>> GenerateChunksAsync(int numChunks, T size,TaskHandler taskContext);

        protected abstract int NumberOfTilesInSize(T size);
    }


    /// <summary>
    /// Uses Generic ITileCoordinates to generate meshes for chunks of mazes.
    /// Class exists to keep mesh generation logic, which is a lot, encapsulated.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class WallMeshChunkComputerGeneric<T> where T : ITileCoordinate<T>
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
        // Step 4: Generate vertex positions (use param- wallThickness to compute end-side points (front to back thickness) )
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
        protected bool displayBorderWalls;

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
        public virtual List<Mesh> CreateWallsMeshChunks(
                GenericMazeMap<T> data,
                MazeMeshDrawGeneric<T> mazeDrawer,
                float wallThickness,
                float wallHeight,
                IReadOnlyList<IReadOnlyList<T>> coordsPerChuck,
            bool displayBorderWalls = true)
        {
            this.map = data;
            this.mazeDrawer = mazeDrawer;
            this.wallThickness = wallThickness;
            this.wallHeight = wallHeight;
            this.displayBorderWalls = displayBorderWalls;

            Debug.Log("Wall mesh computer running now");

            uniqueCorners = new List<Corner>();
            oldToNewCornerIndex = new List<int>();
            uniqueEdges = new List<Edge>();
            edgesByChunk = new List<List<Edge>>();
            //build chunk by coordinate dictionary (for fast lookup later)
            chunckIndexByFaceCoord = new Dictionary<T, int>();

            for (int chunkCounter = 0; chunkCounter < coordsPerChuck.Count; chunkCounter++)
            {
                IReadOnlyList<T> listOfFaces = coordsPerChuck[chunkCounter];
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
        async public virtual UniTask<List<MeshData>> CreateWallsMeshChunksAsync(
                GenericMazeMap<T> data,
                MazeMeshDrawGeneric<T> mazeDrawer,
                float wallThickness,
                float wallHeight,
                IReadOnlyList<IReadOnlyList<T>> coordsPerChuck,
                TaskHandler taskContext,
                bool displayBorderWalls = true,
                Texture2D sideDepthMap = null, MeshData normalizedWallMesh=null)
        {

            this.map = data;
            this.mazeDrawer = mazeDrawer;
            this.wallThickness = wallThickness;
            this.wallHeight = wallHeight;
            this.displayBorderWalls = displayBorderWalls;

            await AsyncInit();
            // Debug.Log("Wall mesh computer running now");

            uniqueCorners = new List<Corner>();
            oldToNewCornerIndex = new List<int>();
            uniqueEdges = new List<Edge>();
            edgesByChunk = new List<List<Edge>>();
            //build chunk by coordinate dictionary (for fast lookup later)
            chunckIndexByFaceCoord = new Dictionary<T, int>();
            await taskContext.SetStageMessageAndYield("Generation: reverse indexing chunk with coordinates");
            if (taskContext.IsCancellationRequested) return null;
            for (int chunkCounter = 0; chunkCounter < coordsPerChuck.Count; chunkCounter++)
            {
                IReadOnlyList<T> listOfFaces = coordsPerChuck[chunkCounter];
                foreach (T coord in listOfFaces)
                    chunckIndexByFaceCoord[coord] = chunkCounter;
                edgesByChunk.Add(new List<Edge>());
            }
            await taskContext.SetStageMessageAndYield("Generation: computing corner positions");
            await BuildUniqueCornersAsync(taskContext);
            if (taskContext.IsCancellationRequested) return null;
            await taskContext.SetStageMessageAndYield("Generation: building Unique Edges");
            await BuildUniqueEdgesAsync(taskContext);
            if (taskContext.IsCancellationRequested) return null;
            await taskContext.SetStageMessageAndYield("Generation: sorting corner edges by angle");
            await SortCornerEdgesClockwiseAsync(taskContext);
            if (taskContext.IsCancellationRequested) return null;
            await taskContext.SetStageMessageAndYield("Generation: computing mesh vertices");
            await GenerateVertexPositionsAsync(taskContext);
            if (taskContext.IsCancellationRequested) return null;
            await taskContext.SetStageMessageAndYield("Generation: launching " + edgesByChunk.Count + " tasks to generate chunk meshes");
            UniTask<MeshData>[] tasks = new UniTask<MeshData>[edgesByChunk.Count];
           // List<MeshData> outputMeshes = new List<MeshData>();
            for (int i = 0; i < edgesByChunk.Count; i++)

            {
                List<Edge> localEdges = edgesByChunk[i]; // capture per iteration
                tasks[i] = GenerateWallModelAsync(localEdges, taskContext, normalizedWallMesh,Vector3.one, sideDepthMap);
            }
            MeshData[] results;
            if (taskContext.IsCancellationRequested) return null;
            await taskContext.SetStageMessageAndYield("Generation: generating maze wall models");
            try
            {
                results = await UniTask.WhenAll(tasks);
            }
            catch (System.OperationCanceledException)
            {
                return default;
            }
            await taskContext.SetStageMessageAndYield("Generation: converting resulting into unity meshes");
            return new List<MeshData>(results);
        }

        protected virtual async UniTask AsyncInit()
        {
            await UniTask.Yield();
            // await UniTask.SwitchToMainThread();
            //  await UniTask.SwitchToThreadPool();
        }


        /// <summary>
        /// returns the direction of the wall's height.  the base version is suitable for 2d mazes. 
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        protected virtual Vector3 NormalAtModelSpacePosition(Vector3 pos) { return mazeDrawer.mazeNormal; }//  vector normalized for faces

        protected virtual Vector3 GeneralLocalComputeCornerPos(T coord, int neighborIndex)
        {
            Vector3 tilePosition = map.GetModelSpacePosition(coord);
            Vector3 neighborPosition = map.GetModelSpacePosition(coord.GetSpatialNeighbor(neighborIndex));

            Vector3 wallPosition = (tilePosition + neighborPosition) * 0.5f;

            Vector3 edgeDir = (neighborPosition - tilePosition).normalized;
            Vector3 normal = NormalAtModelSpacePosition(tilePosition);
            Vector3 right = Vector3.Cross(normal, edgeDir).normalized;

            float neighborDist = Vector3.Distance(tilePosition, neighborPosition);
            float computedEdgeLength = neighborDist * Mathf.Tan(Mathf.PI / coord.NumberOfNeighbors());

            return wallPosition + right * (computedEdgeLength * 0.5f);
        }
        protected virtual Vector3 ComputeCornerPos(T coord, int neighborIndex)
        {
            //compute corner using orientation.
            int neighborCount = coord.NumberOfNeighbors();
            T neighbor = coord.GetSpatialNeighbor(neighborIndex);
            Vector3 tilePosition = map.GetModelSpacePosition(coord);
            Vector3 neighborPosition = map.GetModelSpacePosition(neighbor);

            Vector3 wallPosition = (tilePosition + neighborPosition) * 0.5f;
            Quaternion wallRotation = map.NeighborBorderOrientation(coord, neighborIndex);
            float neighborDist = (tilePosition - neighborPosition).magnitude;
            float computedEdgeLength = neighborDist * Mathf.Tan(Mathf.PI / neighborCount);
            Vector3 C1 = wallPosition + wallRotation * (computedEdgeLength * 0.5f * Vector3.forward);//.left);
            return C1;

        }

        public virtual Mesh RebuildSingleChunk(int chunk)
        {
            // BuildUniqueEdges();
            // SortCornerEdgesClockwise();
            UpdateEdgesVisibility();
            GenerateVertexPositions();// based on visibility


            List<Edge> chuckEdges = edgesByChunk[chunk];
            //string chunkEdgeDetails = strext.Join<Edge>(chuckEdges, (e) => e.cornerA.ToString() + "-" + e.cornerB.ToString(), "\n");
            // Debug.Log("Created Mesh for chunk ("+chunk+") containing " + chuckEdges.Count + " edges.");

            return GenerateWallModel(chuckEdges);
        }

        //internal storage
        protected List<Corner> uniqueCorners = new List<Corner>();
        List<int> oldToNewCornerIndex = new List<int>();
        protected List<Edge> uniqueEdges = new List<Edge>();
        protected List<List<Edge>> edgesByChunk = new List<List<Edge>>();
        Dictionary<T, int> chunckIndexByFaceCoord;
        protected Dictionary<T, List<int>> cornerIndecesByCoordinate = new Dictionary<T, List<int>>();
        //internal structures
        protected class Corner
        {
            public Vector3 position;
            //  public List<int> edgeTo;  //indexes into the uniqueCorners list
            public List<int> edges;  //indexes into the uniqueEdges list
            public List<int> sortedEdges;  //indexes into the edges list
            public Vector3[] fanRing;
            public Vector3? tipVert;
        }
        protected class Edge
        {
            public int cornerA;
            public int cornerB;
            // public List<T> touchingCoords= new List<T>();
            public T sideACoord;  //  to replace touching coords list
            public int sideAEdgeNeighborIndex;  //  to replace touching coords list
            public T sideBCoord;  //  to replace touching coords list
            public int sideBEdgeNeighborIndex;  //  to replace touching coords list

            public bool hasVisibleWall;
            public Vector3[] wallEndAVerts;
            public Vector3[] wallEndATopVerts;
            public Vector3? wallEndATipVert;
            public Vector3? wallEndATopTipVert;
            public Vector3[] wallEndBVerts;
            public Vector3[] wallEndBTopVerts;
            public Vector3? wallEndBTipVert;
            public Vector3? wallEndBTopTipVert;
            public bool wallEndAIsJunction;
            public bool wallEndBIsJunction;
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
        void AssignToEdge(Edge e, int whichCornerIndex, Vector3 wallEndVertFront, Vector3 wallEndVertBack, Vector3? tip, bool isJunction, Color debugColor)
        {
            // Debug.Log("\n    Assigning Edge corner [" + whichCornerIndex + "]: vert avg: " + (wallEndVertFront+wallEndVertBack)*0.5f);

            if (e.cornerA == whichCornerIndex)
            {
                e.wallEndAVerts = new Vector3[] { wallEndVertFront, wallEndVertBack };
                e.wallEndATopVerts = new Vector3[] { Extrude(wallEndVertBack), Extrude(wallEndVertFront) };
                e.wallEndATipVert = tip;
                if (tip == null) e.wallEndATopTipVert = null;
                else e.wallEndATopTipVert = Extrude(tip.Value);
                e.wallEndAIsJunction = isJunction;
                e.wallEndADebugColor = debugColor;
            }
            else//assumes (e.cornerB == whichCornerIndex)
            {
                e.wallEndBVerts = new Vector3[] { wallEndVertBack, wallEndVertFront };
                e.wallEndBTopVerts = new Vector3[] { Extrude(wallEndVertFront), Extrude(wallEndVertBack) };
                e.wallEndBTipVert = tip;
                if (tip == null) e.wallEndBTopTipVert = null;
                else e.wallEndBTopTipVert = Extrude(tip.Value);
                e.wallEndBIsJunction = isJunction;
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
            float invDenom = 1f / denom;
            float s = (b * f - c * e) * invDenom;
            float t = (a * f - b * c) * invDenom;

            closestPointOnA = p1 + d1 * s;
            closestPointOnB = p2 + d2 * t;
            return true;
        }


        void BuildUniqueCorners()
        {
            BuildUniqueCornersAsync(new TaskHandler(false)).GetAwaiter().GetResult();
            return;
        }


        class Vector3ApproxComparer : IEqualityComparer<Vector3>
        {
            private readonly float epsilon;
            public Vector3ApproxComparer(float e) { epsilon = e; }

            public bool Equals(Vector3 a, Vector3 b)
            {
                return (a - b).sqrMagnitude <= epsilon;
            }

            public int GetHashCode(Vector3 v)
            {
                return 0; // forces Equals check every time
            }
        }

        /// <summary>
        /// populates cornerIndecesByCoordinate and uniqueCorners
        /// </summary>
        /// <param name="taskContext"></param>
        /// <returns></returns>
        protected virtual async UniTask BuildUniqueCornersAsync(TaskHandler taskContext)
        {

            List<Vector3> uniqueCornerPositions = new List<Vector3>();
            Dictionary<Vector3, int> cornerIndexByPosition = new Dictionary<Vector3, int>(new Vector3ApproxComparer(.0001f));
            string logstr = "Building corner for map " + GetType();
            int cCount = 0;
            foreach (T coord in map.allMapCoords)
            {
                int neighborCount = coord.NumberOfNeighbors();
                List<int> tileUniqueCornerIndeces = new List<int>();
                logstr += "\ncomputing corners of coord " + coord;
                for (int n = 0; n < neighborCount; n++)
                {
                    Vector3 cornerPos = ComputeCornerPos(coord, n);
                    logstr += "\n     neighbor " + n + " pos: " + cornerPos;
                    if (!cornerIndexByPosition.TryGetValue(cornerPos, out int cornerIndex))
                    {
                        cornerIndex = uniqueCornerPositions.Count;
                        uniqueCornerPositions.Add(cornerPos);
                        cornerIndexByPosition.Add(cornerPos, cornerIndex);
                        logstr += " newIndex: " + cornerIndex;
                    }
                    else
                    {
                        logstr += " foundIndex: " + cornerIndex;
                    }
                    tileUniqueCornerIndeces.Add(cornerIndex);
                }

                cornerIndecesByCoordinate.Add(coord, tileUniqueCornerIndeces);
                await taskContext.Yield();
                taskContext.IncrementProgress(0.1f);
                cCount++;
            }

            await taskContext.SetStageMessageAndYield("Finalizing corners.");
            foreach (Vector3 cornerVertex in uniqueCornerPositions)
            {
                uniqueCorners.Add(new Corner { position = cornerVertex });
            }
            logstr += "\n Total corners: " + cCount;
            await UniTask.SwitchToMainThread();
            Debug.Log(logstr);
            await UniTask.SwitchToThreadPool();
        }


        /*primary internal functions*/
        void BuildUniqueEdges() //to do: break into build unique, and build visible- build visible can be called  during chunk recompute without doing the whole maze
        {
            BuildUniqueEdgesAsync(new TaskHandler(false)).GetAwaiter().GetResult();
            return;
        }


        async UniTask BuildUniqueEdgesAsync(TaskHandler taskContext)
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
            //Dictionary<(int, int), int> edgeLookup = new Dictionary<(int, int), int>();
            PairedKeyDictionary<int, int> edgeLookup = new PairedKeyDictionary<int, int>();

            foreach (List<Edge> chuckEdges in edgesByChunk)
                chuckEdges.Clear();

            //string logstr = "";
            foreach (T coord in map.allMapCoords)
            {
                int cornerCount = coord.NumberOfNeighbors();
                List<int> cornerIndiciesForCurrentCoord = cornerIndecesByCoordinate[coord];
                int edgeChunk = chunckIndexByFaceCoord[coord];
                for (int i = 0; i < cornerCount; i++)
                {
                    T neighborCoord = coord.GetSpatialNeighbor(i);

                    int currentCornerA = cornerIndiciesForCurrentCoord[i];
                    int currentCornerB = cornerIndiciesForCurrentCoord[(i + 1).RingIndex(cornerCount)];

                    // see if an edge exists that uses these corners- if not create it
                    //  void swap(ref int a, ref int b) { int temp = a; a = b; b = temp; }
                    //if (currentCornerA < currentCornerB) swap(ref currentCornerA, ref currentCornerB);
                    int currentEdgeIndex;
                    if (!edgeLookup.TryGetValue(currentCornerA, currentCornerB, out currentEdgeIndex))
                        //if (!edgeLookup.TryGetValue((currentCornerB, currentCornerA), out currentEdgeIndex))
                        currentEdgeIndex = -1;

                    //int currentEdgeIndex = uniqueEdges.FindIndex(0, (Edge e) => (e.cornerA == currentCornerA && e.cornerB == currentCornerB) || (e.cornerA == currentCornerB && e.cornerB == currentCornerA));
                    Edge edge;
                    int GetNeighborIndex(T source, T neighbor)
                    {
                        int i = 0;
                        foreach (T test in source.GetSpatialNeighbors())
                        {
                            if (test.Equals(neighbor))
                                return i;
                            i++;
                        }
                        throw new GeometryException("Tiles are not neighbors: [" + source + "] , [" + neighbor + "]");
                    }
                    if (currentEdgeIndex == -1)// if not, create one
                    {
                        edge = new Edge() { cornerA = currentCornerA, cornerB = currentCornerB };
                        edge.hasVisibleWall = false;//for now
                        edge.sideACoord = coord;
                        edge.sideAEdgeNeighborIndex = i;
                        edge.sideBCoord = neighborCoord;
                        edge.sideBEdgeNeighborIndex = GetNeighborIndex(neighborCoord, coord);
                        // edge.touchingCoords.Add(coord);
                        // edge.touchingCoords.Add(neighborCoord);
                        currentEdgeIndex = uniqueEdges.Count;
                        uniqueEdges.Add(edge);
                        edgeLookup.Add(currentCornerA, currentCornerB, currentEdgeIndex);
                        edge.hasVisibleWall = CheckIsEdgeVisible(edge);// coord, i, neighborCoord);

                        //   logstr += ("\nedge between tiles " + coord + " and " + coord.GetNeighbor(i) + ", has visible wall: " + edge.hasVisibleWall + "  edge dir: " + EdgeDirFrom(edge, currentCornerA));
                        //   logstr += ("\n    corners ["+ currentCornerA + "]: "+ uniqueCorners[currentCornerA].position + " And [" + currentCornerB + "]: " + uniqueCorners[currentCornerB].position);


                        // Add edge index to corners' edge lists if not already present
                        Corner cornerAObj = uniqueCorners[currentCornerA];
                        Corner cornerBObj = uniqueCorners[currentCornerB];
                        if (cornerAObj.edges == null)
                            cornerAObj.edges = new List<int>();
                        if (cornerBObj.edges == null)
                            cornerBObj.edges = new List<int>();

                        //  we are only in here if a new edge created: no need to check if it exists in a list
                        int edgeIndex = currentEdgeIndex;// uniqueEdges.IndexOf(edge);
                                                         //  if (!cornerAObj.edges.Contains(edgeIndex))
                        cornerAObj.edges.Add(edgeIndex);
                        //   if (!cornerBObj.edges.Contains(edgeIndex))
                        cornerBObj.edges.Add(edgeIndex);


                        // addref to this edge to chunk lists  to do: separate for recompute

                        if (map.IsWithinBounds(neighborCoord))
                            edgeChunk = Mathf.Min(edgeChunk, chunckIndexByFaceCoord[neighborCoord]);
                        //logstr += "\n    adding edge[" + currentEdgeIndex + "] to chunk[" + edgeChunk + "]";

                        //    if (!edgesByChunk[edgeChunk].Contains(edge))
                        edgesByChunk[edgeChunk].Add(edge);
                        //else
                        //  logstr += "  DUPLICATE  edge";
                        /*
                      if (logstr.Length > 2048 * 4)
                      {
                          //Debug.Log(logstr);
                          logstr = "";
                      }*/
                    }//edgealready exists
                }
                taskContext.IncrementProgress(0.1f);
                await taskContext.Yield();
            }

            //Debug.Log(logstr);
        }


        void SortCornerEdgesClockwise()
        {
            SortCornerEdgesClockwiseAsync(new TaskHandler(false)).GetAwaiter().GetResult();
            return;
        }

        async UniTask SortCornerEdgesClockwiseAsync(TaskHandler taskContext)
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
                Vector3 axis = NormalAtModelSpacePosition(c.position);
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
                taskContext.IncrementProgress(0.1f);
                await taskContext.Yield();
            }
        }

        bool CheckIsEdgeVisible(Edge e)
        {
            if (map.IsWithinBounds(e.sideACoord))//in bounds
            {
                if (map.Walls[e.sideACoord][e.sideAEdgeNeighborIndex])//wall on edge
                    if (mazeDrawer.IsTileVisible(e.sideACoord))//tile visible
                        return true;
            }
            if (map.IsWithinBounds(e.sideBCoord))
            {
                if (map.Walls[e.sideBCoord][e.sideBEdgeNeighborIndex])
                    if (mazeDrawer.IsTileVisible(e.sideBCoord))
                        return true;

            }
            return false;

        }
        void UpdateEdgesVisibility()
        {
            foreach (Edge e in uniqueEdges)
            {
                e.hasVisibleWall = CheckIsEdgeVisible(e);//.sideACoord, e.touchingCoords[1]);
                                                         //    e.hasVisibleWall = CheckIsEdgeVisible(e.touchingCoords[0], e.touchingCoords[1]);
            }
        }

        void GenerateVertexPositions()
        {
            GenerateVertexPositionsAsync(new TaskHandler(false)).GetAwaiter().GetResult();
            return;
        }


        async UniTask GenerateVertexPositionsAsync(TaskHandler taskContext)
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
            //string logstr = "";
            float halfT = wallThickness * 0.5f;

            for (int cIndex = 0; cIndex < uniqueCorners.Count; cIndex++)
            {
                Corner c = uniqueCorners[cIndex];
                // logstr += "\nComputing visible edges for unique corner[" + cIndex + "] at position:" + c.position + "  Total edges touching corner:" + c.edges.Count;
                if (c.edges.Count == 0) continue;

                Vector3 cornerNormal = NormalAtModelSpacePosition(c.position); // cache once per corner

                List<Edge> visibleEdges = new List<Edge>();
                foreach (int edgeIndex in c.sortedEdges)
                {
                    Edge e = uniqueEdges[c.edges[edgeIndex]];
                    if (e.hasVisibleWall)
                    {
                        visibleEdges.Add(e);
                    }
                }

                int visibleCount = visibleEdges.Count;
                if (visibleCount == 1)
                {
                    Edge e = visibleEdges[0];
                    Vector3 thicknessOffset = halfT * -Vector3.Cross(cornerNormal, EdgeDirFrom(e, cIndex));
                    AssignToEdge(e, cIndex, c.position + thicknessOffset, c.position - thicknessOffset, null,false, Color.black);
                }
                else if (visibleCount > 1)
                {
                    c.fanRing = new Vector3[visibleCount];

                    Vector3 axis = cornerNormal;
                    /*Vector3 refDir;
                    if (Mathf.Abs(axis.z) < 0.99f)
                        refDir = Vector3.Cross(axis, Vector3.forward);
                    else
                        refDir = Vector3.Cross(axis, Vector3.right);
                    refDir.Normalize();
                    */
                    Vector3 avgRingPos = Vector3.zero;

                    for (int eCounter = 0; eCounter < visibleCount; eCounter++)
                    {
                        Edge e = visibleEdges[eCounter];
                        Edge nextEdge = visibleEdges.RingIndex(eCounter + 1);

                        Vector3 edgeDir = EdgeDirFrom(e, cIndex);
                        Vector3 nextEdgeDir = EdgeDirFrom(nextEdge, cIndex);

                        Vector3 edgeThicknessOffset = -halfT * Vector3.Cross(cornerNormal, edgeDir);
                        Vector3 nextEdgethicknessOffset = -halfT * Vector3.Cross(cornerNormal, nextEdgeDir);

                        Vector3 posOnEdgeSide = c.position + edgeThicknessOffset;
                        Vector3 posOnNextEdgeSide = c.position - nextEdgethicknessOffset;

                        Vector3 closestSidePoint;
                        Vector3 diff = posOnEdgeSide - posOnNextEdgeSide;

                        if (diff.sqrMagnitude > 0.0001f)
                        {
                            Ray side = new Ray(posOnEdgeSide, edgeDir);
                            Ray nextSide = new Ray(posOnNextEdgeSide, nextEdgeDir);
                            Vector3 closestNextSidePoint;
                            if (!LineIntersection(side, nextSide, out closestSidePoint, out closestNextSidePoint))
                            {
                                closestSidePoint += closestNextSidePoint;
                                closestSidePoint *= 0.5f;
                            }
                        }
                        else
                        {
                            closestSidePoint = posOnEdgeSide;
                        }

                        c.fanRing[eCounter] = closestSidePoint;
                        avgRingPos += closestSidePoint;
                    }

                    avgRingPos /= visibleCount;
                    bool isJunction = (visibleCount > 1);
                    if (visibleCount > 2)
                    {
                        c.tipVert = avgRingPos;
                    }

                    for (int eCounter = 0; eCounter < visibleCount; eCounter++)
                    {
                        Edge e = visibleEdges[eCounter];
                        Color color = Color.red * ((float)eCounter / visibleCount);
                        AssignToEdge(e, cIndex, c.fanRing.RingIndex(eCounter), c.fanRing.RingIndex(eCounter - 1), c.tipVert, isJunction, color);
                    }
                }// end more than one visible edge touching corner

                taskContext.IncrementProgress(0.1f);
                await taskContext.Yield();
            }

            //  Debug.Log(logstr);
        }


        Mesh GenerateWallModel(List<Edge> edgesInChunk, MeshData normalizedMeshBase = null, Vector3 meshAspectRatio = default(Vector3), Texture2D sideDepthTexture = null, Vector2Int depthSegements = default(Vector2Int))
        {
            return GenerateWallModelAsync(edgesInChunk, new TaskHandler(false), normalizedMeshBase , meshAspectRatio, sideDepthTexture , depthSegements).GetAwaiter().GetResult().ToMesh();
        }
        struct CacheWallPoint
        {
            public Vector3 pointOnQuadL;
            public Vector3 pointOnQuadR;
            public Vector3 pointOnQuadCenter;
            public float depth;
        }
        async UniTask<MeshData> GenerateWallModelAsync(List<Edge> edgesInChunk, TaskHandler taskContext, MeshData normalizedMeshBase=null, Vector3 meshAspectRatio= default(Vector3), Texture2D sideDepthTexture = null, Vector2Int depthSegements = default(Vector2Int))
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
            List<int> subMeshTris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            if (depthSegements == default(Vector2Int))
                depthSegements = new Vector2Int(64, 64);
            string logstr = "";
            List<MeshData> mergeableMeshes= new List<MeshData>();

            for (int edgeIndexInChunk = 0; edgeIndexInChunk < edgesInChunk.Count; edgeIndexInChunk++)
            {
                if (taskContext.IsCancellationRequested) return new MeshData();
                Edge e = edgesInChunk[edgeIndexInChunk];
                logstr += "\nEdgeInChunk[" + edgeIndexInChunk + "]:  corners[" + e.cornerA + "] , [" + e.cornerB + "]";
                if (!e.hasVisibleWall) continue;
                if (e.wallEndAVerts == null)
                    throw new System.Exception("e.wallEndAVerts is null.  edge in chunk index: " + edgeIndexInChunk + "  edge corner a index: " + e.cornerA + "  edge corner b index: " + e.cornerB);
                if (e.wallEndBVerts == null)
                    throw new System.Exception("e.wallEndBVerts is null.  edge in chunk index: " + edgeIndexInChunk + "  edge corner a index: " + e.cornerA + "  edge corner b index: " + e.cornerB);
                logstr += "\n     vert avg: " + (e.wallEndAVerts[0] + e.wallEndAVerts[1]) * 0.5f + " and " + (e.wallEndBVerts[0] + e.wallEndBVerts[1]) * 0.5f;
                logstr += "\n     cornerPos: " + uniqueCorners[e.cornerA].position + " and " + uniqueCorners[e.cornerB].position;

                // Convenience handles
                var aBot = e.wallEndAVerts;
                var aTop = e.wallEndATopVerts;
                var aTip = e.wallEndATipVert;
                var aTipTop = e.wallEndATopTipVert;
                var aColor = e.wallEndADebugColor;
                bool aIsJunction = e.wallEndAIsJunction;

                var bBot = e.wallEndBVerts;
                var bTop = e.wallEndBTopVerts;
                var bTip = e.wallEndBTipVert;
                var bTipTop = e.wallEndBTopTipVert;
                var bColor = e.wallEndBDebugColor;
                bool bIsJunction = e.wallEndBIsJunction;

               

                if (sideDepthTexture == null)
                {
                    if (normalizedMeshBase != null)
                    {
                        int numCopies = 3;
                        float tInc = 1f / numCopies;
                        float t0 = 0;
                        for (int i = 0; i < numCopies; i++,t0+=tInc)
                        {
                            float t1 = t0+tInc;
                            if (t1 > 1f) t1 = 1f;

                            // match your original parameter mapping exactly
                            Vector3 seg_aBot0 = Vector3.Lerp(aBot[0], bBot[0], t0);
                            Vector3 seg_aBot1 = Vector3.Lerp(aBot[1], bBot[1], t0);
                            Vector3 seg_aTop1 = Vector3.Lerp(aTop[1], bTop[1], t0);
                            Vector3 seg_aTop0 = Vector3.Lerp(aTop[0], bTop[0], t0);

                            Vector3 seg_bBot0 = Vector3.Lerp(aBot[0], bBot[0], t1);
                            Vector3 seg_bBot1 = Vector3.Lerp(aBot[1], bBot[1], t1);
                            Vector3 seg_bTop1 = Vector3.Lerp(aTop[1], bTop[1], t1);
                            Vector3 seg_bTop0 = Vector3.Lerp(aTop[0], bTop[0], t1);

                            MeshData wallMesh = TransformNormaizedMeshAlongWall(
                                normalizedMeshBase,
                                seg_aBot0, seg_aBot1, seg_aTop1, seg_aTop0,
                                seg_bBot0, seg_bBot1, seg_bTop1, seg_bTop0
                            );

                            mergeableMeshes.Add(wallMesh);
                        }
                        /*
                        int numCopies = 3;//test with hard coded 3, for now
                        // add loop to go along length of wall (from start to end) "numCopies" amount of times, and create a transformed meshData, for each section.
                        {
                            //note: normalized mesh has all verts defined between 0,0,0 and 1,1,1

                            //definition:  MeshData TransformNormaizedMeshAlongWall(MeshData normalizdMesh,
                            //   Vector3 cornerBottomLeftStart, Vector3 cornerBottomRightStart, Vector3 cornerTopLeftStart, Vector3 cornerTopRightStart,
                            //   Vector3 cornerBottomLeftEnd, Vector3 cornerBottomRightEnd, Vector3 cornerTopLeftEnd, Vector3 cornerTopRightEnd)
                            MeshData wallMesh = TransformNormaizedMeshAlongWall(normalizedMeshBase, aBot[0], aBot[1], aTop[1], aTop[0],
                                bBot[0], bBot[1], bTop[1], bTop[0]);
                            mergeableMeshes.Add(wallMesh);
                        }*/
                    }
                    else
                    {
                        // Bottom quad (A[0]→B[1], A[1]→B[0])
                        AddQuad(aBot[0], aBot[1], bBot[1], bBot[0], aColor, bColor, verts, subMeshTris, uvs, colors);
                        // Top quad
                        AddQuad(aTop[0], aTop[1], bTop[1], bTop[0], aColor, bColor, verts, subMeshTris, uvs, colors);

                        // Front face: A[0]→ATop[0]→BTop[1]→B[1]
                        AddQuad(aBot[1], aTop[0], bTop[0], bBot[1], aColor, bColor, verts, tris, uvs, colors);

                        // Back face: A[1]→ATop[1]→BTop[0]→B[0]
                        AddQuad(aTop[1], aBot[0], bBot[0], bTop[1], aColor, bColor, verts, tris, uvs, colors);
                    }
                }
                else
                {

                    BuildLengthwiseWallFaces(
                            aBot[0], aBot[1], bBot[1], bBot[0],
                            aTop[0], aTop[1], bTop[1], bTop[0],
                            aColor, bColor, verts, tris, subMeshTris, uvs, colors);

                }

                // End A
                if (aTip.HasValue && aTipTop.HasValue)//only junctions of more that two edges has a tip
                {
                    AddTri(aBot[1], aBot[0], aTip.Value, aColor, verts, subMeshTris, uvs, colors);
                    AddTri(aTop[1], aTop[0], aTipTop.Value, aColor, verts, subMeshTris, uvs, colors);
                }
                else
                {
                    if(!aIsJunction && normalizedMeshBase==null)// only draw end cap when NO junction at all
                        AddQuad(aBot[1], aBot[0], aTop[1], aTop[0], aColor, aColor, verts, subMeshTris, uvs, colors);
                }

                // End B
                if (bTip.HasValue && bTipTop.HasValue)//only junctions of more that two edges has a tip
                {
                    AddTri(bBot[0], bBot[1], bTip.Value, bColor, verts, subMeshTris, uvs, colors);
                    AddTri(bTop[0], bTop[1], bTipTop.Value, bColor, verts, subMeshTris, uvs, colors);
                }
                else
                {
                    if(!bIsJunction && normalizedMeshBase == null)// only draw end cap when NO junction at all
                        AddQuad(bBot[0], bBot[1], bTop[0], bTop[1], bColor, bColor, verts, subMeshTris, uvs, colors);
                }
                taskContext.IncrementProgress(0.5f);
                await taskContext.Yield();
            }
            // Debug.Log(logstr);
            MeshData mesh = new MeshData();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetTriangles(subMeshTris, 1);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.RecalculateBounds();

            if (mergeableMeshes.Count > 0)
            {
                mergeableMeshes.Add(mesh);
                mesh = MeshData.MergeMeshes(mergeableMeshes);
            }

            mesh.RecalculateNormals();
            
            taskContext.IncrementProgress(0.2f);
            // string chunkEdgeDetails = strext.Join<Edge>(edgesInChunk, (e) => e.cornerA.ToString() + "-" + e.cornerB.ToString(), "\n");
            // Debug.Log("Created Mesh for chunk containing " + edgesInChunk.Count + " edges. Final vertex count: " + verts.Count + "\n" + chunkEdgeDetails);

            return mesh;


            float SampleDepth(Vector2 uv)
            {
                Color c = sideDepthTexture.GetPixelBilinear(uv.x, uv.y);
                return 1f - c.grayscale; // scale externally if needed
            }
            void AddQuad(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br,
                Color colorL, Color colorR,
                List<Vector3> v, List<int> t, List<Vector2> uv, List<Color> col, List<Vector2> useUvs = null)
            {
                int start = v.Count;
                v.Add(bl); v.Add(tl); v.Add(tr); v.Add(br);
                t.Add(start + 0); t.Add(start + 1); t.Add(start + 2);
                t.Add(start + 0); t.Add(start + 2); t.Add(start + 3);

                if (useUvs == null)
                {
                    uv.Add(new Vector2(0, 0)); // bl
                    uv.Add(new Vector2(0, 1)); // tl
                    uv.Add(new Vector2(1, 1)); // tr
                    uv.Add(new Vector2(1, 0)); // br
                }
                else
                {
                    uv.Add(useUvs[0]); uv.Add(useUvs[1]); uv.Add(useUvs[2]); uv.Add(useUvs[3]);
                }
                col.Add(colorL);
                col.Add(colorL);
                col.Add(colorR);
                col.Add(colorR);


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

            MeshData TransformNormaizedMeshAlongWall(MeshData normalizdMesh,
            Vector3 cornerBottomLeftStart, Vector3 cornerBottomRightStart, Vector3 cornerTopLeftStart, Vector3 cornerTopRightStart,
            Vector3 cornerBottomLeftEnd, Vector3 cornerBottomRightEnd, Vector3 cornerTopLeftEnd, Vector3 cornerTopRightEnd)
            {
                MeshData result = new MeshData(normalizdMesh);
                Vector3[] verts = result.vertices;

                // Precompute vertical offset (constant over shape)
                Vector3 heightOffset = cornerTopLeftStart - cornerBottomLeftStart;

                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 v = verts[i];

                    float x = v.x; // left -> right
                    float y = v.y; // bottom -> top
                    float z = v.z; // start -> end

                    // 1) move along the two side rails (these are parallel by constraint)
                    Vector3 leftPos = Vector3.Lerp(cornerBottomLeftStart, cornerBottomLeftEnd, z);
                    Vector3 rightPos = Vector3.Lerp(cornerBottomRightStart, cornerBottomRightEnd, z);

                    // 2) interpolate across width (handles non-parallel start/end edges)
                    Vector3 basePos = Vector3.Lerp(leftPos, rightPos, x);

                    // 3) apply vertical offset and assign back to array
                    verts[i] = basePos + heightOffset * y;
                }

                //result.vertices = verts;// shouldn't be needed- we are working on the arrayRef
                return result;
            }
            void BuildLengthwiseWallFaces(
                Vector3 botA0, Vector3 botA1, Vector3 botB1, Vector3 botB0,
                Vector3 topA0, Vector3 topA1, Vector3 topB1, Vector3 topB0,
                Color colorA, Color colorB,
                List<Vector3> verts, List<int> tris, List<int> subMeshTris,List<Vector2> uvs, List<Color> colors)
            {
                float incU = 1f / depthSegements.x;
                float incV = 1f / depthSegements.y;

                int uCount = depthSegements.x + 1;
                int vCount = depthSegements.y + 1;

                //center quad- we will LERP towards UV's on this, rather than offset by a normal for depth (old method)
                Vector3 botACenter = (botA0 + botA1) * 0.5f;
                Vector3 botBCenter = (botB0 + botB1) * 0.5f;
                Vector3 topACenter = (topA0 + topA1) * 0.5f;
                Vector3 topBCenter = (topB0 + topB1) * 0.5f;

                // Cache all depth samples up front
                CacheWallPoint[,] wallPointCache = new CacheWallPoint[uCount, vCount];

                float[,] depthCache = new float[uCount, vCount];
                for (int ui = 0; ui < uCount; ui++)
                    for (int vi = 0; vi < vCount; vi++)
                    {
                        Vector2 uv = new Vector2(ui * incU, vi * incV);
                        depthCache[ui, vi] = SampleDepth(uv);
                        CacheWallPoint cachePt;
                        cachePt.depth = SampleDepth(uv);
                        cachePt.pointOnQuadL = GetPointOnQuad(botA1, botB1, topA0, topB0, uv);
                        cachePt.pointOnQuadR = GetPointOnQuad(botA0, botB0, topA1, topB1, uv);
                        cachePt.pointOnQuadCenter = GetPointOnQuad(botACenter, botBCenter, topACenter, topBCenter, uv);
                        wallPointCache[ui, vi] = cachePt;
                    }

                for (int j = 0; j < depthSegements.y; j++)
                {
                    float V = j * incV;
                    float VNext = V + incV;
                    bool isBottomRow = j == 0;
                    bool isTopRow = j == depthSegements.y - 1;

                    for (int i = 0; i < depthSegements.x; i++)
                    {
                        float U = i * incU;
                        float UNext = U + incU;

                        CacheWallPoint cachePtBL = wallPointCache[i, j];
                        CacheWallPoint cachePtBR = wallPointCache[i + 1, j];
                        CacheWallPoint cachePtTL = wallPointCache[i, j + 1];
                        CacheWallPoint cachePtTR = wallPointCache[i + 1, j + 1];

                        float dBL = cachePtBL.depth;// depthCache[i, j];
                        float dBR = cachePtBR.depth;//depthCache[i + 1, j];
                        float dTL = cachePtTL.depth;//depthCache[i, j + 1];
                        float dTR = cachePtTR.depth;//depthCache[i + 1, j + 1];

                        // ── BOTTOM FACE (first V row only) ──────────────────────────────
                        if (isBottomRow && !(dBL >= 0.99f && dBR >= 0.99f))
                        {
                            // Bottom face rail: lerp between botA0↔botB0 and botA1↔botB1
                            Vector3 posBL = cachePtBL.pointOnQuadL;//  Vector3.Lerp(botA0, botB0, U);
                            Vector3 posBR = cachePtBL.pointOnQuadR;// Vector3.Lerp(botA1, botB1, U);
                            Vector3 posTL = cachePtBR.pointOnQuadL;// Vector3.Lerp(botA0, botB0, UNext);
                            Vector3 posTR = cachePtBR.pointOnQuadR;// Vector3.Lerp(botA1, botB1, UNext);

                            Vector3 posCenter0 = cachePtBL.pointOnQuadCenter;// Vector3.Lerp(botACenter, botBCenter, U);
                            Vector3 posCenter1 = cachePtBR.pointOnQuadCenter;// Vector3.Lerp(botACenter, botBCenter, UNext);

                            posBL = Vector3.Lerp(posBL, posCenter0, dBL);
                            posBR = Vector3.Lerp(posBR, posCenter0, dBL);
                            posTL = Vector3.Lerp(posTL, posCenter1, dBR);
                            posTR = Vector3.Lerp(posTR, posCenter1, dBR);

                            //top and bottom faces, edges depth cuts into uv, rather than squishing
                            float uvL0 = dBL * 0.5f;        // left edge trimmed inward
                            float uvR0 = 1f - (dBL * 0.5f); // right edge trimmed inward
                            float uvL1 = dBR * 0.5f;
                            float uvR1 = 1f - (dBR * 0.5f);

                            AddQuad(posBL, posBR, posTR, posTL, colorA, colorB, verts, subMeshTris, uvs, colors,
                                new List<Vector2> {
                                    new Vector2(uvL0, 0),
                                    new Vector2(uvL1, 1),   // note: U/UNext axis is V here (along wall length)
                                    new Vector2(uvR1, 1),
                                    new Vector2(uvR0, 0) });
                        }

                        // ── SIDE FACES (front + back, every row) ────────────────────────
                        if (!(dBL >= 0.99f && dBR >= 0.99f && dTL >= 0.99f && dTR >= 0.99f))
                        {
                            Vector2 UV_BL = new Vector2(U, V);
                            Vector2 UV_BR = new Vector2(UNext, V);
                            Vector2 UV_TL = new Vector2(U, VNext);
                            Vector2 UV_TR = new Vector2(UNext, VNext);

                            Vector3 centerBottomLeft = cachePtBL.pointOnQuadCenter;// GetPointOnQuad(botACenter, botBCenter, topACenter, topBCenter, UV_BL);
                            Vector3 centerBottomRight = cachePtBR.pointOnQuadCenter;//GetPointOnQuad(botACenter, botBCenter, topACenter, topBCenter, UV_BR);
                            Vector3 centerTopLeft = cachePtTL.pointOnQuadCenter;//GetPointOnQuad(botACenter, botBCenter, topACenter, topBCenter, UV_TL);
                            Vector3 centerTopRight = cachePtTR.pointOnQuadCenter;//GetPointOnQuad(botACenter, botBCenter, topACenter, topBCenter, UV_TR);

                            // left face: botA1→topA0→topB0→botB1
                            {
                                Vector3 bl = cachePtBL.pointOnQuadL;//GetPointOnQuad(botA1, botB1, topA0, topB0, UV_BL);
                                Vector3 br = cachePtBR.pointOnQuadL;//GetPointOnQuad(botA1, botB1, topA0, topB0, UV_BR);
                                Vector3 tl = cachePtTL.pointOnQuadL;//GetPointOnQuad(botA1, botB1, topA0, topB0, UV_TL);
                                Vector3 tr = cachePtTR.pointOnQuadL;//GetPointOnQuad(botA1, botB1, topA0, topB0, UV_TR);

                                bl = Vector3.Lerp(bl, centerBottomLeft, dBL);
                                br = Vector3.Lerp(br, centerBottomRight, dBR);
                                tl = Vector3.Lerp(tl, centerTopLeft, dTL);
                                tr = Vector3.Lerp(tr, centerTopRight, dTR);
                                AddQuad(bl,tl,tr,br,
                                        colorA, colorB, verts, tris, uvs, colors,
                                        new List<Vector2> { UV_BL, UV_TL, UV_TR, UV_BR });

                            }

                            // right face: botA0→topA1→topB1→botB0
                            {
                                Vector3 bl = cachePtBL.pointOnQuadR;//GetPointOnQuad(botA0, botB0, topA1, topB1, UV_BL);
                                Vector3 br = cachePtBR.pointOnQuadR;//GetPointOnQuad(botA0, botB0, topA1, topB1, UV_BR);
                                Vector3 tl = cachePtTL.pointOnQuadR;//GetPointOnQuad(botA0, botB0, topA1, topB1, UV_TL);
                                Vector3 tr = cachePtTR.pointOnQuadR;//GetPointOnQuad(botA0, botB0, topA1, topB1, UV_TR);
                                bl = Vector3.Lerp(bl, centerBottomLeft, dBL);
                                br = Vector3.Lerp(br, centerBottomRight, dBR);
                                tl = Vector3.Lerp(tl, centerTopLeft, dTL);
                                tr = Vector3.Lerp(tr, centerTopRight, dTR);

                                UV_BL.x = 1f - UV_BL.x;
                                UV_BR.x = 1f - UV_BR.x;
                                UV_TL.x = 1f - UV_TL.x;
                                UV_TR.x = 1f - UV_TR.x;

                                AddQuad(tl,bl,br,tr,
                                        colorA, colorB, verts, tris, uvs, colors,
                                        new List<Vector2> { UV_TL, UV_BL, UV_BR, UV_TR });
                                /* old
                                Vector3 bNormal = Vector3.Cross(tl - bl, br - bl).normalized * -1f;
                                AddQuad(
                                    tl - bNormal * dTL * wallThickness * 0.5f,
                                    bl - bNormal * dBL * wallThickness * 0.5f,
                                    br - bNormal * dBR * wallThickness * 0.5f,
                                    tr - bNormal * dTR * wallThickness * 0.5f,
                                    colorA, colorB, verts, tris, uvs, colors,
                                    new List<Vector2> { UV_TL, UV_BL, UV_BR, UV_TR });*/
                            }
                        }

                        // ── TOP FACE (last V row only) ───────────────────────────────────
                        if (isTopRow && !(dTL >= 0.99f && dTR >= 0.99f))
                        {
                            // Top face rail: lerp between topA0↔topB0 and topA1↔topB1
                            Vector3 posL0 = cachePtTL.pointOnQuadL;//  Vector3.Lerp(topA0, topB0, U);
                            Vector3 posR0 = cachePtTL.pointOnQuadR;//  Vector3.Lerp(topA1, topB1, U);
                            Vector3 posL1 = cachePtTR.pointOnQuadL;//  Vector3.Lerp(topA0, topB0, UNext);
                            Vector3 posR1 = cachePtTR.pointOnQuadR;//  Vector3.Lerp(topA1, topB1, UNext);

                            Vector3 posCenter0 = cachePtTL.pointOnQuadCenter;// Vector3.Lerp(topACenter, topBCenter, U);
                            Vector3 posCenter1 = cachePtTR.pointOnQuadCenter;//Vector3.Lerp(topACenter, topBCenter, UNext);

                            Vector3 vBL = Vector3.Lerp(posL0, posCenter0, dTL);
                            Vector3 vBR = Vector3.Lerp(posR0, posCenter0, dTL);
                            Vector3 vTL = Vector3.Lerp(posL1, posCenter1, dTR);
                            Vector3 vTR = Vector3.Lerp(posR1, posCenter1, dTR);

                            //top and bottom faces, edges depth cuts into uv, rather than squishing
                            float vOffset = dTL * 0.5f;
                            float vOffsetNext = dTR * 0.5f;
                            // Facing up (matches original BuildTopWallMesh isTop=true winding)
                            AddQuad(vBL, vBR, vTR, vTL, colorA, colorB, verts, subMeshTris, uvs, colors,
                                new List<Vector2> {
                                    new Vector2(U, vOffset),// dTL * 0.5f),        // bl
                                    new Vector2(U, 1f-vOffset),//dTR * 0.5f);//,        // br
                                    new Vector2(UNext, 1f-vOffsetNext),//1f - dTR * 0.5f),   // tr
                                    new Vector2(UNext, vOffsetNext) });// 1f - dTL * 0.5f) }); // tl

                        }
                    }
                }

                static Vector3 GetPointOnQuad(Vector3 p00, Vector3 p10, Vector3 p01, Vector3 p11, Vector2 uv)
                {
                    return Vector3.Lerp(
                        Vector3.Lerp(p00, p10, uv.x),
                        Vector3.Lerp(p01, p11, uv.x),
                        uv.y);
                }
            }
        }
    }
}


public class PairedKeyDictionary<K, V> : IEnumerable<KeyValuePair<(K, K), V>>
{
    private readonly Dictionary<(K, K), V> _dict;
    private readonly IEqualityComparer<K> _keyComparer;

    public PairedKeyDictionary() : this(EqualityComparer<K>.Default) { }

    public PairedKeyDictionary(IEqualityComparer<K> comparer)
    {
        _keyComparer = comparer;
        _dict = new Dictionary<(K, K), V>(new UnorderedPairComparer<K>(comparer));
    }

    public void Add(K a, K b, V value)
    {
        _dict.Add((a, b), value);
    }

    public bool TryGetValue(K a, K b, out V value)
    {
        return _dict.TryGetValue((a, b), out value);
    }

    public bool ContainsKeys(K a, K b)
    {
        return _dict.ContainsKey((a, b));
    }

    public bool Remove(K a, K b)
    {
        return _dict.Remove((a, b));
    }

    public V this[K a, K b]
    {
        get => _dict[(a, b)];
        set => _dict[(a, b)] = value;
    }

    public IEnumerator<KeyValuePair<(K, K), V>> GetEnumerator() => _dict.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private class UnorderedPairComparer<T> : IEqualityComparer<(T, T)>
    {
        private readonly IEqualityComparer<T> _cmp;

        public UnorderedPairComparer(IEqualityComparer<T> cmp)
        {
            _cmp = cmp;
        }

        public bool Equals((T, T) x, (T, T) y)
        {
            return (_cmp.Equals(x.Item1, y.Item1) && _cmp.Equals(x.Item2, y.Item2))
                || (_cmp.Equals(x.Item1, y.Item2) && _cmp.Equals(x.Item2, y.Item1));
        }

        public int GetHashCode((T, T) pair)
        {
            // Order-insensitive hash: XOR of individual hashes
            int h1 = _cmp.GetHashCode(pair.Item1);
            int h2 = _cmp.GetHashCode(pair.Item2);
            return h1 ^ h2;
        }
    }
}