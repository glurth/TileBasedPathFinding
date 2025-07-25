using UnityEngine;
using System.Collections.Generic;
using EyE.Threading;
using Cysharp.Threading.Tasks;

namespace Eye.Maps.Templates
{
    public abstract class MazeDrawGeneric<T> : MonoBehaviour where T : ITileCoordinate<T>
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
        bool skipUpdateMazeGenerationRunning = false;// locks Update function
        public async UniTask SetMazeAsync(GenericMazeMap<T> toValue, CancelBoolRef cancelRef=null, ProgressFloatRef progressRef=null)
        {
            skipUpdateMazeGenerationRunning = true;
            _maze = toValue;
            await GenerateMazeVisualsAsync(cancelRef,progressRef);
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
        private List<Matrix4x4> wallMatrices = new List<Matrix4x4>();  // Transformation matrices for walls

        //visibility stuff
        private Dictionary<T, bool> tileVisibility = new Dictionary<T, bool>();
        private bool visibilityChangedSinceLastDraw = true;
       // private Dictionary<T, int> coordToFloorIndices = new Dictionary<T, int>();
        private Dictionary<T, int> coordToWallIndices = new Dictionary<T, int>();
       // private List<Matrix4x4> visibleFloorMatrices = new List<Matrix4x4>();
        private List<Matrix4x4> visibleWallMatrices = new List<Matrix4x4>();

        //refs
        private GameObject instantiatedStartPositionMarker;
        private GameObject instantiatedEndPositionMarker;
        private Mesh floorMesh;
        private Material floorMaterial;
        private Mesh wallMesh;
        private Material wallMaterial;




        private void Reset()
        {
            mazeSize = DefaultMazeSize();
        }

        public bool createMazeOnEnable = true;
        void OnEnable()
        {
            if (createMazeOnEnable)
            {
                GenericMazeMap<T> newMaze = CreateMazeMap();
                /*foreach (T coord in newMaze.allMapCoords)
                {
                    bool[] wallsForTile = newMaze.Walls[coord];
                    Debug.Log("created map Coord " + coord + " walls: " + string.Join(",", wallsForTile));
                }*/
                SetMaze(newMaze);
                //maze = newMaze;
            }
            //if (maze != null)
              //  GenerateMazeVisuals();
        }

        void Update()
        {
            if(!skipUpdateMazeGenerationRunning)
                DrawInstances(); // Draw the instances each frame
        }

        // Abstract method for creating the specific maze map
        protected abstract GenericMazeMap<T> CreateMazeMap();
        protected abstract T DefaultMazeSize();

        private void GenerateMazeVisuals()
        {
            GenerateMazeVisualsAsync(null, null).AsTask().GetAwaiter().GetResult();//fire and wait
            return;
        }

        private async UniTask GenerateMazeVisualsAsync(CancelBoolRef cancelRef,ProgressFloatRef progressRef)
        {
            bool runSync = cancelRef == null;
            EyE.Threading.YieldTimer yieldTimer = new EyE.Threading.YieldTimer(cancelRef, runSync);
            if (progressRef != null) progressRef.StageMessage = "Generating Visuals";
            //we don't instantiate prefabs- we just get their mats and meshes
            if (!runSync)
                await UniTask.SwitchToMainThread();

            Vector3 tileOffset = maze.SingleTileModelSpaceOffset();
            tileScale = Mathf.Max(tileOffset.x, tileOffset.y, tileOffset.z);

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


            floorMatrices.Clear();
            wallMatrices.Clear();

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
                    
                    await yieldTimer.YieldOnTimeSlice();
                }
            }
            if(!runSync)
                await UniTask.SwitchToThreadPool();
            if (progressRef != null)
            {
                progressRef.StageMessage = "Generating Visuals: walls";
                progressRef.Value += 0.5f;
            }
            foreach (T coord in maze.allMapCoords)
            {
                Vector3 tilePosition = maze.GetModelSpacePosition(coord);
                bool[] wallsForTile = maze.Walls[coord];
                // Debug.Log("Coord " + coord + " walls: " + string.Join(",", wallsForTile));
                int neighborCount = coord.NumberOfNeighbors();
                coordToWallIndices[coord] = wallMatrices.Count;  //*******visibility
                SetTileVisibility(coord, !startHidden);
                //tileVisibility[coord] = !startHidden; //*******visibility
                // Debug.Log("creating walls for coord: " + coord + "  newighbors: " + string.Join(',', coord.GetNeighbors()));
                for (int i = 0; i < neighborCount; i++)
                {
                    if (wallsForTile[i])
                    {
                        T neighbor = coord.GetNeighbor(i);

                        // Skip creating border walls if drawBorderWalls is false and the neighbor is outside the maze bounds
                        if (!drawBorderWalls && !maze.IsWithinBounds(neighbor))
                        {
                            //Debug.Log("skipping neighbor["+i+"] with face index:"+neighbor+" due to out of bounds");
                            continue;
                        }

                        Matrix4x4 wallMatrix = GetNeighborWallMatrix(coord, i, tilePosition, neighborCount);
                        wallMatrices.Add(wallMatrix);

                        //await yieldTimer.YieldOnTimeSlice();
                        //Debug.Log("created wall between " + coord + " and " + neighbor);
                    }
                }
                await yieldTimer.YieldOnTimeSlice();
            }
            if (!runSync)
                await UniTask.SwitchToMainThread();
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
            await yieldTimer.YieldOnTimeSlice();
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
            T neighbor = coord.GetNeighbor(neighborIndex);
            float wallThickness = this.tileScale * wallThicknessFraction;
            float wallHeight = this.tileScale * wallHeightFraction;
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
            return Matrix4x4.TRS(wallPosition, wallRotation, wallScale);
        }

        private void UpdateWorldMatricies(Matrix4x4 localToWorldMatrix)
        {
            Matrix4x4 w = localToWorldMatrix;
            worldWallMatrices.Clear();
            foreach (Matrix4x4 m in wallMatrices)
                worldWallMatrices.Add(w * m);

            worldFloorMatrices.Clear();
            foreach (Matrix4x4 m in floorMatrices)
                worldFloorMatrices.Add(w * m);
        }

        List<Matrix4x4> worldWallMatrices = new List<Matrix4x4>();
        List<Matrix4x4> worldFloorMatrices = new List<Matrix4x4>();

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

            if (AreNotNull(wallMesh, wallMaterial))//wallPrefab != null)
            {
                //foreach (List<Matrix4x4> wallSet in InChunks(worldWallMatrices, MaxInstances))
                foreach (List<Matrix4x4> wallSet in InChunks(visibleWallMatrices, MaxInstances))
                    Graphics.DrawMeshInstanced(wallMesh, 0, wallMaterial, wallSet);
            }
        }


        public void SetTileVisibility(T coord, bool isVisible)
        {
            if (!tileVisibility.ContainsKey(coord) || tileVisibility[coord] != isVisible)
            {
                tileVisibility[coord] = isVisible;
                visibilityChangedSinceLastDraw = true;
            }
        }
        private void UpdateVisibleMatrices()
        {
            //visibleFloorMatrices.Clear();
            visibleWallMatrices.Clear();

            foreach (KeyValuePair<T, bool> kvp in tileVisibility)
            {
                if (!kvp.Value) continue;
                T coord = kvp.Key;
            //    if (coordToFloorIndices.TryGetValue(kvp.Key, out int floorIdx))
            //        visibleFloorMatrices.Add(worldFloorMatrices[floorIdx]);
                if (coordToWallIndices.TryGetValue(coord, out int startIdx))
                {
                    int wallCount = coord.NumberOfNeighbors();
                    int wallIdx = startIdx;
                    for (int i = 0; i < wallCount; i++)
                    {
                        if (maze.Walls[coord][i])
                        {   
                            if (wallIdx < worldWallMatrices.Count)
                                visibleWallMatrices.Add(worldWallMatrices[wallIdx]);
                            wallIdx++;
                        }
                    }
                }
            }

            visibilityChangedSinceLastDraw = false;
        }
    }
}
