using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

using EyE.Threading;

namespace EyE.Maps.Templates
{

    public struct LineSegment
    {
        public Vector3 A, B;

        public LineSegment(Vector3 a, Vector3 b)
        {
            A = a;
            B = b;
        }
    }

    abstract public class GenericMazeMapBase
    {
        abstract public UniTask GenerateMazeAsync(TaskHandler taskContext, bool testAllWalls = false);
        abstract public void GenerateMaze(bool testAllWalls = false);
        abstract public System.Type CoordinateType {get;}
        /// <summary>
        /// derived versions will throw excpetions if an improper coordinate type is passed in.
        /// </summary>
        /// <param name="coordBase"></param>
        /// <returns></returns>
        abstract public Vector3 GetModelSpacePosition(ITileCoordinateBase coordBase);
        abstract public Quaternion GetModelSpaceOrientation(ITileCoordinateBase coordBase);
        abstract public Bounds GetModelSpaceBounds();
        abstract public Vector3 SingleTileModelSpaceOffset();  //should provide the model space offset between the first tile, and a tile with all coorinate dimensions incremented by one
        abstract public ITileCoordinateBase SizeAsCoord { get; }
    }


    public struct TeleportDestination
    {
        public ITileCoordinateBase coord;
        public bool firstNeighbors;  //  for two  way teleporters determines if THIS (destination) coord defines the first of the neighbors (when false, the dictionary's "source" coord does)

        public TeleportDestination(ITileCoordinateBase coord, bool firstNeighbors)
        {
            this.coord = coord;
            this.firstNeighbors = firstNeighbors;
        }
    }
    public sealed class TeleporterCollection
    {
        private readonly Dictionary<ITileCoordinateBase, TeleportDestination> _forward;
        private readonly Dictionary<ITileCoordinateBase, HashSet<ITileCoordinateBase>> _reverse;

        public TeleporterCollection()
        {
            _forward = new Dictionary<ITileCoordinateBase, TeleportDestination>();
            _reverse = new Dictionary<ITileCoordinateBase, HashSet<ITileCoordinateBase>>();
        }

        public void AddOneWay(
            ITileCoordinateBase source,
            ITileCoordinateBase destination,
            bool destinationDefinesFirstNeighbors)
        {
            if (_forward.ContainsKey(source))
            {
                throw new System.InvalidOperationException("Source already has a teleporter.");
            }

            TeleportDestination teleportDestination =
                new TeleportDestination(destination, destinationDefinesFirstNeighbors);

            _forward.Add(source, teleportDestination);

            if (!_reverse.ContainsKey(destination))
            {
                _reverse.Add(destination, new HashSet<ITileCoordinateBase>());
            }

            _reverse[destination].Add(source);
        }

        public void AddTwoWay(
            ITileCoordinateBase a,
            ITileCoordinateBase b,
            bool aDefinesFirstNeighbors)
        {
            // A -> B
            AddOneWay(a, b, !aDefinesFirstNeighbors);

            // B -> A
            AddOneWay(b, a, aDefinesFirstNeighbors);
        }

        public bool TryGetDestination(
            ITileCoordinateBase source,
            out TeleportDestination destination)
        {
            return _forward.TryGetValue(source, out destination);
        }

        public bool TryGetSources(
            ITileCoordinateBase destination,
            out HashSet<ITileCoordinateBase> sources)
        {
            return _reverse.TryGetValue(destination, out sources);
        }

        public bool IsTeleporterSource(ITileCoordinateBase coord)
        {
            return _forward.ContainsKey(coord);
        }

        public bool IsTeleporterDestination(ITileCoordinateBase coord)
        {
            return _reverse.ContainsKey(coord);
        }
        public bool IsPartOfTwoWay(ITileCoordinateBase coord)
        {
            if (!_forward.ContainsKey(coord))
            {
                return false;
            }

            TeleportDestination destination = _forward[coord];

            if (!_forward.ContainsKey(destination.coord))
            {
                return false;
            }

            TeleportDestination back = _forward[destination.coord];

            if (!EqualityComparer<ITileCoordinateBase>.Default.Equals(back.coord, coord))
            {
                return false;
            }

            return true;
        }

    }
    //this version has double-sided walls (since there may be an odd number of neighbors- we can't easily do single walls.
    abstract public class GenericMazeMap<T> : GenericMazeMapBase, IMap<T>, IMapDrawable<T> where T : ITileCoordinate<T>
    {
        private T _size;


        protected Dictionary<T, bool[]> walls = new Dictionary<T, bool[]>();



        //protected Dictionary<T, TeleportDestination> teleporters = null;
        protected TeleporterCollection teleporters;
        //public IReadOnlyDictionary<T, TeleportDestination> Teleporters => teleporters;
        public TeleporterCollection Teleporters => teleporters;
        /// <summary>
        /// this defines the maze itself.  Walls are expected to be double sided (e.g. a true in the bool array for both coords the wall touches. Using the correct index in the array for each coord, as defined by the GetNeighbor(index) function.)
        /// </summary>
        /// <param name="walls"></param>
        public void SetWalls(Dictionary<T, bool[]> walls) { this.walls = walls; }
        public IReadOnlyDictionary<T, bool[]> Walls { get { return walls; } }

        protected Dictionary<T, bool> visited = new Dictionary<T, bool>();
        protected System.Random random;


        public T size { get { return _size; } }
        public override ITileCoordinateBase SizeAsCoord { get => _size; }
        public float worldScale { get; private set; }

        public T start;
        public T end;
        public abstract IEnumerable<T> allMapCoords { get; }


        //generation parameters
        int numTeleportTilesToGenerate = 3;//used during mesh generation
        bool alwaysReverseTeleport = true;//used during mesh generation
        int numSolutionsCounter = 1;
        int seed;

        public GenericMazeMap(T size, T start, T end, int numTeleportTiles=0,bool alwaysReverseTeleport=true, float worldScale = 1f, int numSolutions = 1)
        {
            this.start = start;
            this.end = end;
            //int numWallDimensions = size.NumberOfNeighbors();
            this._size = size;
            this.worldScale = worldScale;
            this.seed = System.Environment.TickCount;
            this.random = new System.Random(seed);
            this.numSolutionsCounter = numSolutions;
            this.numTeleportTilesToGenerate = numTeleportTiles;
            this.alwaysReverseTeleport = alwaysReverseTeleport;
        }
        public GenericMazeMap(T size, T start, T end, int seed, int numTeleportTiles = 0, bool alwaysReverseTeleport = true, float worldScale = 1f, int numSolutions = 1)
        {
            this.start = start;
            this.end = end;
            //int numWallDimensions = size.NumberOfNeighbors();
            this._size = size;
            this.worldScale = worldScale;
            this.seed = seed;
            this.random = new System.Random(seed);
            this.numSolutionsCounter = numSolutions;
            this.numTeleportTilesToGenerate = numTeleportTiles;
            this.alwaysReverseTeleport = alwaysReverseTeleport;
        }



        void SanityCheckWalls()
        {
            int GetNeighborIndex(T source, T neighor)
            {
                for (int n = 0; n < source.NumberOfNeighbors(); n++)
                {
                    if (source.GetSpatialNeighbor(n).Equals(neighor))
                        return n;
                }
                throw new System.Exception("inavlid neighbor");
            }

            foreach (T tileCoord in allMapCoords)
            {
                for (int n = 0; n < tileCoord.NumberOfNeighbors(); n++)
                {
                    T neighborCoord = tileCoord.GetSpatialNeighbor(n);
                    if (IsWithinBounds(neighborCoord))
                    {
                        int reverseNeighborIndex = GetNeighborIndex(neighborCoord, tileCoord);
                        if (walls[tileCoord][n] != walls[neighborCoord][reverseNeighborIndex])
                            Debug.LogWarning("Wall mismatch- [" + tileCoord + "],[" + neighborCoord + "]");
                    }
                }
            }
            Debug.Log("Wallcheck complete");
        }

        private float WeightAgainstCrowding(T candidate, Stack<T> currentPath)
        {
            int used = 0;
            foreach (T n in candidate.GetSpatialNeighbors())
            {
                //if (visited.ContainsKey(n) && visited[n] && currentPath.Contains(n))
                if (currentPath.Contains(n))
                    used += 30;
                else
                {
                    foreach (T m in n.GetSpatialNeighbors())
                        if (currentPath.Contains(m))
                            used += 10;
                }
            }
            // more used neighbors → smaller weight
            return 1f / (1f + used);
        }
        protected virtual T PickWeighted(List<T> list, Stack<T> path)
        {
            float total = 0f;
            float[] w = new float[list.Count];

            for (int i = 0; i < list.Count; i++)
            {
                float weight = WeightAgainstCrowding(list[i], path);
                w[i] = weight;
                total += weight;
            }

            float r = (float)random.NextDouble() * total;
            for (int i = 0; i < list.Count; i++)
            {
                r -= w[i];
                if (r <= 0f)
                    return list[i];
            }
            return list[list.Count - 1]; // fallback
        }

        /// <summary>
        /// Asynchronously generates a maze using time-sliced yielding.
        /// </summary>
        /// <param name="cancelRef">A reference used to support cancellation mid-process.</param>
        /// <param name="testAllWalls">If true, skips path and branch generation, only initializes walls/visited.</param>
        /// <param name="progressRef">Optional progress reference for external monitoring (0 to 1).</param>
        public override async UniTask GenerateMazeAsync(TaskHandler taskContext, bool testAllWalls = false)
        {
            //var yieldTimer = new YieldTimer(cancelRef, cancelRef==null);
            int totalSteps = 0;
            List<T> cachedAllCoords= new();
            foreach (ITileCoordinate<T> tileCoord in allMapCoords)
            {
                cachedAllCoords.Add((T)tileCoord);
                totalSteps++;
            }
            int completedSteps = 0;

            foreach (ITileCoordinate<T> tileCoord in allMapCoords)
            {
                bool[] wallsArray = new bool[tileCoord.NumberOfNeighbors()];
                for (int i = 0; i < tileCoord.NumberOfNeighbors(); i++)
                    wallsArray[i] = true;

                walls[tileCoord.value] = wallsArray;
                visited[tileCoord.value] = false;

                completedSteps++;
                taskContext.IncrementProgress((float)completedSteps / totalSteps);
                //if (progressRef != null)
                //    progressRef.Value = (float)completedSteps / totalSteps;

                await taskContext.Yield();// yieldTimer.YieldOnTimeSlice();
            }


            //do shuffle of cachedAllCoords
            int n = cachedAllCoords.Count;
            while (n > 1)
            {
                n--;
                int k = (int)(random.NextDouble() * (n + 1));// Random.Range(0, n + 1);
                T value = cachedAllCoords[k];
                cachedAllCoords[k] = cachedAllCoords[n];
                cachedAllCoords[n] = value;
            }
            int currentIndex = 0;
            teleporters = new TeleporterCollection();// new Dictionary<T, TeleportDestination>();
            for (int i = 0; i < numTeleportTilesToGenerate; i++)
            {
                // Check to prevent IndexOutOfRangeException
                if (currentIndex + 1 >= cachedAllCoords.Count) break;

                // Grab the next two random tiles from our shuffled list
                T source = cachedAllCoords[currentIndex++];
                T dest = cachedAllCoords[currentIndex++];

                //teleporters..Add(source, new TeleportDestination(dest,false));

                if (alwaysReverseTeleport)
                {
                    teleporters.AddTwoWay(source, dest, true);
                }
                else
                {
                    teleporters.AddOneWay(source, dest, true);
                }
            }



            if (!testAllWalls)
            {
                List<T> mainPath = await GenerateMainPathAsync(start, end, taskContext);//.Yield yieldTimer);
                Debug.Log("Main path generated: " + string.Join(",", mainPath));
                await GenerateBranchesAsync(mainPath, taskContext);
            }

            //SanityCheckWalls();
        }

        /// <summary>
        /// Synchronously generates the maze by invoking the async version and backgrounding it.
        /// </summary>
        /// <param name="cancelRef">A reference used to support cancellation mid-process.</param>
        /// <param name="testAllWalls">If true, skips path and branch generation, only initializes walls/visited.</param>
        /// <param name="progressRef">Optional progress reference for external monitoring (0 to 1).</param>
        public override void GenerateMaze(bool testAllWalls = false)
        {
            GenerateMazeAsync(new TaskHandler(false), testAllWalls).AsTask().GetAwaiter().GetResult();//.Forget();
        }
        public override System.Type CoordinateType { get => typeof(T); }
        /// <summary>
        /// Asynchronously generates the main path from start to end.
        /// </summary>
        /// <param name="start">The starting tile.</param>
        /// <param name="end">The target tile to reach.</param>
        /// <param name="yieldTimer">Used to yield control based on elapsed time.</param>
        protected virtual async UniTask<List<T>> GenerateMainPathAsync(T start, T end, TaskHandler taskContext)//YieldTimer yieldTimer)
        {
            Stack<T> stack = new Stack<T>();
            List<T> path = new List<T>();
            stack.Push(start);
            visited[start] = true;

            while (stack.Count > 0)
            {
                T current = stack.Peek();
                path.Add(current);

                if (current.Equals(end))
                    break;

               // List<T> neighbors = GetUnvisitedNeighbors(current);
                List<NeighborDetails> neighborsDetails = GetUnvisitedNeighbors(current);
                if (neighborsDetails.Count > 0)
                {
                    List<T> pathNeighbors = new();
                    foreach (NeighborDetails details in neighborsDetails)
                        pathNeighbors.Add(details.pathNeighbor);
                    //T next = neighbors[random.Next(neighbors.Count)];
                    T next = PickWeighted(pathNeighbors, stack);
                    int neighborIndex = pathNeighbors.IndexOf(next);
                    //  while (DistFromPath(next) < 2 && ((random.Next()&0x01)==0))
                    {
                        //    next = neighbors[random.Next(neighbors.Count)];
                    }

                    RemoveWall(current, next);
                    visited[next] = true;
                    visited[neighborsDetails[neighborIndex].spatialNeighbor] = true;
                    stack.Push(next);
                }
                else
                {
                    stack.Pop();
                }
                taskContext.IncrementProgress(0.1f);
                await taskContext.Yield();// yieldTimer.YieldOnTimeSlice();
            }

            return path;
            /*
            float DistFromPath(T checkCoord, Stack<T> stack)
            {
                float min = float.PositiveInfinity;
                foreach (T pathStep in stack)
                {
                    float dist = checkCoord.HeuristicDistanceTo(pathStep);
                    if (dist < min) min = dist;
                }
                return min;
            }*/
        }


        /// <summary>
        /// Asynchronously generates branching paths off the main path.
        /// </summary>
        /// <param name="mainPath">The main path tiles to branch from.</param>
        /// <param name="yieldTimer">Used to yield control based on elapsed time.</param>
        protected virtual async UniTask GenerateBranchesAsync(List<T> mainPath, TaskHandler taskContext)//YieldTimer yieldTimer)
        {
            List<T> allPathSteps = new List<T>(mainPath);
            int maxZeros = 1000;
            int pathLengthZeroCount = 0;

            while (pathLengthZeroCount < maxZeros)
            {
                float curve = Mathf.Pow((float)random.NextDouble(), 2);
                T branchStart = allPathSteps[(int)(curve * allPathSteps.Count)];
                List<T> newPath = await GenerateRandomPathAsync(branchStart, taskContext);// yieldTimer);

                if (newPath.Count == 0)  //No path steps generated
                    pathLengthZeroCount++;
                else
                {
                    pathLengthZeroCount = 0;
                    if (numSolutionsCounter > 2)// do we need to create a hole to main path?
                    {
                        if (TryFindRandomCoordOnPathNeighoringMainPath(mainPath, newPath, out T mainPathCoord, out T pathCoord))
                        {
                            RemoveWall(mainPathCoord, pathCoord);
                            numSolutionsCounter--;
                        }
                    }

                }

                allPathSteps.AddRange(newPath);
                taskContext.IncrementProgress(0.1f);
                await taskContext.Yield();// yieldTimer.YieldOnTimeSlice();
            }
        }

        bool TryFindRandomCoordOnPathNeighoringMainPath(List<T> mainPath, List<T> pathToCheck, out T mainPathCoord, out T pathCoord)
        {
            //start at end of mainPath
            for (int i = mainPath.Count - 1; i >= 0; i--)
            {
                T currentMainPathCoord = mainPath[i];
                //start at end of checkPath
                for (int j = pathToCheck.Count - 1; j >= 0; j--)
                {
                    T currentPathCoord = pathToCheck[j];
                    if (currentMainPathCoord.HeuristicDistanceTo(currentPathCoord) == 1)
                    {
                        mainPathCoord = currentMainPathCoord;
                        pathCoord = currentPathCoord;
                        return true;
                    }
                }
            }
            mainPathCoord = default(T);
            pathCoord = default(T);
            return false;
        }

        /// <summary>
        /// Asynchronously generates a random path from a given start tile.
        /// </summary>
        /// <param name="start">Starting tile for the path.</param>
        /// <param name="yieldTimer">Used to yield control based on elapsed time.</param>
        protected virtual async UniTask<List<T>> GenerateRandomPathAsync(T start, TaskHandler taskContext)//YieldTimer yieldTimer)
        {
            List<T> path = new List<T>();
            Stack<T> stack = new Stack<T>();
            stack.Push(start);

            while (stack.Count > 0)
            {
                T current = stack.Peek();
               // List<T> neighbors = GetUnvisitedNeighbors(current);
                List<NeighborDetails> neighborsDetails = GetUnvisitedNeighbors(current);

                if (neighborsDetails.Count > 0)
                {
                    NeighborDetails currentNeighborDetails = neighborsDetails[random.Next(neighborsDetails.Count)];
                    T next = currentNeighborDetails.pathNeighbor;
                    RemoveWall(current, next);// could simplify Removewall back to original version by passing in spatialneighbor instead
                    visited[next] = true;
                    visited[currentNeighborDetails.spatialNeighbor] = true;
                    path.Add(next);
                    stack.Push(next);
                }
                else
                {
                    stack.Pop();
                }
                taskContext.IncrementProgress(0.1f);
                await taskContext.Yield();// yieldTimer.YieldOnTimeSlice();
            }

            return path;
        }

        protected struct NeighborDetails
        {
            public T pathNeighbor;       // The logical tile we move TO (the destination if teleporting)
            public T spatialNeighbor;     // The physical tile we move THROUGH (the source if teleporting)
            public bool IsTeleport;

            public NeighborDetails(T pathNeighbor, T spatialNeighbor, bool isTeleport)
            {
                this.pathNeighbor = pathNeighbor;
                this.spatialNeighbor = spatialNeighbor;
                IsTeleport = isTeleport;
            }
        }

        /// <summary>
        /// Returns the list of unvisited neighboring tiles.
        /// </summary>
        /// <param name="tile">Current tile to check from.</param>
        protected virtual List<NeighborDetails> GetUnvisitedNeighbors(T tile)
        {
            /*List<T> unVisitedneighbors = new List<T>();

            foreach (T neighbor in tile.GetPathNeighbors(teleporters))
            {
                if (IsWithinBounds(neighbor))
                {
                    // Debug.Log(" checking neighbor for visited-  current: " + tile + "  neighbor: " + neighbor);
                    if (!visited[neighbor])
                        unVisitedneighbors.Add(neighbor);
                }
            }

            return unVisitedneighbors;*/
            List<NeighborDetails> unVisitedneighbors = new List<NeighborDetails>();
            int numNeighbors = tile.NumberOfNeighbors();
            T[] spatialNeighbors = tile.GetSpatialNeighbors();
            T[] pathNeighbors = tile.GetPathNeighbors(teleporters);
            for (int i = 0; i < numNeighbors; i++)
            {
                T spatialNeighbor = spatialNeighbors[i];
                T pathNeighbor = pathNeighbors[i];
                if (IsWithinBounds(spatialNeighbor) && !visited[spatialNeighbor] && !visited[pathNeighbor])
                    unVisitedneighbors.Add(new NeighborDetails(spatialNeighbor, pathNeighbors[i], teleporters.IsTeleporterSource(spatialNeighbor)));
            }
            return unVisitedneighbors;

        }

        /// <summary>
        /// spatial
        /// </summary>
        /// <param name="current"></param>
        /// <param name="neighbor"></param>
        /// <returns></returns>
        public int GetSpatialNeighborIndexOf(T current, T neighbor)
        {
            int neighborIndexCounter = 0;
            foreach (T n in current.GetSpatialNeighbors())
            {
                if (n.Equals(neighbor))
                    return neighborIndexCounter;
                //Debug.Log("Found neighbor-  current: " + current + "  neighbor: " + n);
                neighborIndexCounter++;
            }
            //  Debug.LogError("Unable to find neighbor Index!  current: " + current + "  neighbor: " + neighbor);
            return -1;
        }

        public int GetPathNeighborIndexOf(T current, T neighbor)
        {
            int neighborIndexCounter = 0;
            foreach (T n in current.GetPathNeighbors(teleporters))
            {
                if (n.Equals(neighbor))
                    return neighborIndexCounter;
                //Debug.Log("Found neighbor-  current: " + current + "  neighbor: " + n);
                neighborIndexCounter++;
            }
            //  Debug.LogError("Unable to find neighbor Index!  current: " + current + "  neighbor: " + neighbor);
            return -1;
        }
        /// <summary>
        /// Does not performa ny checks, will throw index exceptions for out of bounds coordinates
        /// </summary>
        /// <param name="current"></param>
        /// <param name="next"></param>
        private void RemoveWall(T current, T next)
        {

            int neighborIndex = GetSpatialNeighborIndexOf(current, next);

            // If they aren't spatial neighbors, 'next' must be a teleport destination.
            if (neighborIndex == -1)
            {
                // Find which spatial neighbor of 'current' leads to 'next' via teleport
                foreach (T spatialNeighbor in current.GetSpatialNeighbors())
                {
                    if (teleporters.TryGetDestination(spatialNeighbor, out Templates.TeleportDestination dest) && dest.coord.Equals(next))
                    {
                        // The wall we actually need to break is between 
                        // 'current' and the 'spatialNeighbor' (the teleport entrance).
                        int actualIndex = GetSpatialNeighborIndexOf(current, spatialNeighbor);
                        int reverseIndex = GetSpatialNeighborIndexOf(spatialNeighbor, current);

                        walls[current][actualIndex] = false;
                        walls[spatialNeighbor][reverseIndex] = false;
                        return;
                    }
                }
                Debug.LogWarning("Unable to find source of teleporter destination:[" + next + "]  Given source-neighbor [" + current + "].  No walls removed.");
                return; // Fallback if no link found
            }

            // Standard spatial wall removal
            int reverseNeighborIndex = GetSpatialNeighborIndexOf(next, current);
            walls[current][neighborIndex] = false;
            walls[next][reverseNeighborIndex] = false;

            /*
// old-pre teleport Standard spatial wall removal
int nieghborIndex = GetSpatialNeighborIndexOf(current, next);
int reverseNeighborIndex = GetSpatialNeighborIndexOf(next, current);
walls[current][nieghborIndex] = false;
walls[next][reverseNeighborIndex] = false;*/
        }

        //returns cost to move from one tile to it's neighbor, returns -1 if impassible, or not neighbors
        public float GetMoveCost(ITileCoordinate<T> coordT, ITileCoordinate<T> coordTDest, float max = -1, bool bothdir = false)
        {
            int neighborIndex = GetPathNeighborIndexOf(coordT.value, coordTDest.value);
            if (neighborIndex == -1) return -1;
            return GetMoveCost(coordT, neighborIndex, max, bothdir);
        }

        // Get move cost between neighboring tiles, -1 means impassable
        public virtual float GetMoveCost(ITileCoordinate<T> coordT, int neighborIndex, float max = -1, bool bothdir = false)
        {
            T coord = coordT.value;
            if (!IsWithinBounds(coord)) return -1;
            T neighborCoord = coordT.GetPathNeighbor(neighborIndex,teleporters);

            if (IsWithinBounds(neighborCoord))
            {
                T coordWithWall = coord;
                /*int wallIndex = neighborIndex;
                int numDim = (int)(size.NumberOfNeighbors() / 2f);
                if (neighborIndex >= numDim)//second half of neighbor index, swap roles- opposite will hold wall
                {
                    coordWithWall = neighborCoord;
                    neighborIndex -= numDim;
                }*/
                if (walls[coordWithWall][neighborIndex])
                    return -1;

                return 1;// walls[coord.value.x, coord.value.y] ? -1 : 1;
            }
            return -1; // Impassable if out of bounds or blocked
        }

        override public Bounds GetModelSpaceBounds()
        {
            Bounds bounds;
            Vector3 sizePos = GetModelSpacePosition(size);
            Vector3 singleTileOffset = SingleTileModelSpaceOffset();
            //sizePos-= singleTileOffset * 0.5f;
            //this is the center of 1 tile past x,y (for 2d) - need to subtract half a tiles worth
            // but since 0,0 is talso the center of a tile, we nee to ADD half- so cancels
            bounds = new Bounds(sizePos / 2, sizePos);

           //Vector3 singleTileOffset = SingleTileModelSpaceOffset();
          //  bounds.size += singleTileOffset;
            bounds.center -= singleTileOffset * 0.5f;
            return bounds;
        }

        override public Vector3 GetModelSpacePosition(ITileCoordinateBase coordBase)
        {
            T coord = (T)coordBase;
            return GetModelSpacePosition(coord);
        }
        abstract public Vector3 GetModelSpacePosition(T coord);

        /*virtual public LineSegment GetModelSpaceEdge(T coord, int neighborIndex)
        {
            // the below fails for curved surface mazes because the distance from the origin is based on face centers, not model verticies
            Vector3 tilePosition = GetModelSpacePosition(coord);
            int neighborCount = coord.NumberOfNeighbors();
            Quaternion edgeRotation = NeighborBorderOrientation(coord, neighborIndex);
            T neighbor = coord.GetNeighbor(neighborIndex);

            Vector3 neighborPosition = GetModelSpacePosition(neighbor);//returns a position even for out of bounds coords
            Vector3 wallPosition = (tilePosition + neighborPosition) / 2;
            Quaternion wallRotation = NeighborBorderOrientation(coord, neighborIndex);
            float neighborDist = (tilePosition - neighborPosition).magnitude;
            float computedEdgeLength = neighborDist * Mathf.Tan(Mathf.PI / neighborCount);
            Vector3 wallLength =  wallRotation * Vector3.right * computedEdgeLength*0.5f;
            return new LineSegment(wallPosition + wallLength, wallPosition - wallLength);
        }
        */
        override public Quaternion GetModelSpaceOrientation(ITileCoordinateBase coordBase)
        {
            T coord = (T)coordBase;
            return GetModelSpaceOrientation(coord);
        }
        virtual public Quaternion GetModelSpaceOrientation(T coord)
        {
            return Quaternion.identity;
        }
        /// <summary>
        /// Get the coordinate at/closest to a given world position
        /// </summary>
        /// <param name="pos">model space position</param>
        /// <returns>return the closest coordinate to the given position, or possibly a unique "invalid coordinate" value- depending on T</returns>
        virtual public T GetCoordinate(Vector3 pos)
        {
            T closest= default(T);
            float minDist = float.PositiveInfinity;
            foreach (T coord in allMapCoords)
            {
                float distsq = (GetModelSpacePosition(coord) - pos).sqrMagnitude;
                if (distsq < minDist)
                {
                    closest = coord;
                    minDist = distsq;
                }
            }
            return closest;
        }

        // Check if a given coordinate is within the bounds of the maze
        abstract public bool IsWithinBounds(T coord);

        abstract public Quaternion NeighborBorderOrientation(T coord, int neighborIndex);
    }
}

