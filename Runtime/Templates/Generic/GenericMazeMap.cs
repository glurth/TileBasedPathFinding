using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

using EyE.Threading;

using System;
using System.Collections;

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
        public abstract IEnumerable<ITileCoordinateBase> allMapBaseCoords { get; }
        abstract public ITileCoordinateBase GetCoordinate(Vector3 pos);
        //protected Dictionary<T, TeleportDestination> teleporters = null;
        protected TeleporterCollection teleporters;
        //public IReadOnlyDictionary<T, TeleportDestination> Teleporters => teleporters;
        public TeleporterCollection Teleporters => teleporters;
        public void SetTeleports(TeleporterCollection collectionToUse)
        {
            teleporters = collectionToUse;
        }
    }


    /// <summary>
    /// Represents the destination of a teleporter along with neighbor ordering metadata.
    /// </summary>
    public struct TeleportDestination
    {
        /// <summary>
        /// The coordinate this teleporter leads to.
        /// </summary>
        public ITileCoordinateBase coord;

        /// <summary>
        /// For two-way teleporters, determines whether this destination coordinate
        /// defines the first neighbor ordering. When false, the source coordinate defines it.
        /// </summary>
        public bool firstNeighbors;

        /// <summary>
        /// Creates a new teleport destination.
        /// </summary>
        /// <param name="coord">The coordinate this teleporter leads to.</param>
        /// <param name="firstNeighbors">
        /// True if this destination defines the first neighbor ordering; otherwise false.
        /// </param>
        public TeleportDestination(ITileCoordinateBase coord, bool firstNeighbors)
        {
            this.coord = coord;
            this.firstNeighbors = firstNeighbors;
        }
    }

    /// <summary>
    /// Stores and manages one-way and two-way teleporter relationships between tile coordinates.
    /// </summary>
    public class TeleporterCollection
    {
        /// <summary>
        /// Maps teleporter sources to their destinations.
        /// </summary>
        private Dictionary<ITileCoordinateBase, TeleportDestination> _forward;
        public IReadOnlyDictionary<ITileCoordinateBase, TeleportDestination> GetForward()
        { return _forward; }


        /// <summary>
        /// Maps teleporter destinations to all sources that point to them.
        /// </summary>
        private Dictionary<ITileCoordinateBase, HashSet<ITileCoordinateBase>> _reverse;
        public IReadOnlyDictionary<ITileCoordinateBase, HashSet<ITileCoordinateBase>> GetReverse()
        { return _reverse; }




        /// <summary>
        /// Initializes a new empty teleporter collection.
        /// </summary>
        public TeleporterCollection()
        {
            _forward = new Dictionary<ITileCoordinateBase, TeleportDestination>();
            _reverse = new Dictionary<ITileCoordinateBase, HashSet<ITileCoordinateBase>>();
        }
        public TeleporterCollection(Dictionary<ITileCoordinateBase, TeleportDestination> forward, Dictionary<ITileCoordinateBase, HashSet<ITileCoordinateBase>> reverse)
        {
            _forward = forward;
            _reverse = reverse;
        }
        /// <summary>
        /// Adds a one-way teleporter from <paramref name="source"/> to <paramref name="destination"/>.
        /// </summary>
        /// <param name="source">The source coordinate.</param>
        /// <param name="destination">The destination coordinate.</param>
        /// <param name="destinationDefinesFirstNeighbors">
        /// Determines whether the destination coordinate defines the first neighbor ordering.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the source already has a teleporter.
        /// </exception>
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

        /// <summary>
        /// Adds a two-way teleporter between <paramref name="a"/> and <paramref name="b"/>.
        /// </summary>
        /// <param name="a">First coordinate.</param>
        /// <param name="b">Second coordinate.</param>
        /// <param name="aDefinesFirstNeighbors">
        /// If true, coordinate <paramref name="a"/> defines the first neighbor ordering.
        /// </param>
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

        /// <summary>
        /// Attempts to get the teleport destination for a given source.
        /// </summary>
        /// <param name="source">The source coordinate.</param>
        /// <param name="destination">The resulting teleport destination if found.</param>
        /// <returns>True if a teleporter exists for the source; otherwise false.</returns>
        public bool TryGetDestination(
            ITileCoordinateBase source,
            out TeleportDestination destination)
        {
            return _forward.TryGetValue(source, out destination);
        }

        /// <summary>
        /// Attempts to get the teleport destination Coordinate for a given source.
        /// </summary>
        /// <param name="source">The source coordinate.</param>
        /// <param name="destination">The resulting teleport destination if found.</param>
        /// <returns>True if a teleporter exists for the source; otherwise false.</returns>
        public bool TryGetDestination(
            ITileCoordinateBase source,
            out ITileCoordinateBase destination)
        {
            if (_forward.TryGetValue(source, out TeleportDestination telDest))
            {
                destination = telDest.coord;
                return true;
            }
            destination = default(ITileCoordinateBase);
            return false;
        }

        /// <summary>
        /// Attempts to get all sources that teleport to a given destination.
        /// </summary>
        /// <param name="destination">The destination coordinate.</param>
        /// <param name="sources">All sources that teleport to the destination.</param>
        /// <returns>True if at least one source teleports to the destination; otherwise false.</returns>
        public bool TryGetSources(
            ITileCoordinateBase destination,
            out HashSet<ITileCoordinateBase> sources)
        {
            return _reverse.TryGetValue(destination, out sources);
        }

        /// <summary>
        /// Determines whether the specified coordinate is a teleporter source.
        /// </summary>
        public bool IsTeleporterSource(ITileCoordinateBase coord)
        {
            return _forward.ContainsKey(coord);
        }

        /// <summary>
        /// Determines whether the specified coordinate is a teleporter destination.
        /// </summary>
        public bool IsTeleporterDestination(ITileCoordinateBase coord)
        {
            return _reverse.ContainsKey(coord);
        }

        /// <summary>
        /// Determines whether the specified coordinate participates in a two-way teleporter.
        /// </summary>
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

        /// <summary>
        /// Gets all teleporter source coordinates.
        /// </summary>
        /// <returns>A collection of all coordinates that act as teleporter sources.</returns>
        public IEnumerable<ITileCoordinateBase> GetAllSources()
        {
            return _forward.Keys;
        }

        /// <summary>
        /// Gets all coordinates that are destinations but not themselves teleporter sources.
        /// </summary>
        /// <returns>
        /// A collection of destination-only coordinates (excludes those participating as sources,
        /// such as in two-way teleporters).
        /// </returns>
        public IEnumerable<ITileCoordinateBase> GetAllDestinationOnlys()
        {
            foreach (KeyValuePair<ITileCoordinateBase, HashSet<ITileCoordinateBase>> pair in _reverse)
            {
                if (!_forward.ContainsKey(pair.Key))
                {
                    yield return pair.Key;
                }
            }
        }


        /// <summary>
        /// Resolves a path neighbor index back to the underlying spatial coordinate
        /// and its spatial neighbor index, accounting for teleport offset rules.
        /// </summary>
        /// <param name="coord">The coordinate from which the path neighbor originates.</param>
        /// <param name="pathNeighborIndex">Neighbor index in path-space.</param>
        /// <param name="spatialNeighbor">Resolved spatial neighbor coordinate.</param>
        /// <param name="spatialNeighborIndex">
        /// The neighbor index relative to the spatial coordinate.
        /// </param>
        /// <returns>True if resolution succeeds; otherwise false.</returns>
        public bool TryResolvePathNeighborReverse(
            ITileCoordinateBase coord,
            int pathNeighborIndex,
            out ITileCoordinateBase spatialNeighbor,
            out int spatialNeighborIndex)
        {
            spatialNeighbor = null;
            spatialNeighborIndex = -1;

            ITileCoordinateBase effectiveCoord = coord;
            bool firstNeighbors = true;

            TeleportDestination destination;

            if (_forward.TryGetValue(coord, out destination))
            {
                effectiveCoord = destination.coord;
                firstNeighbors = destination.firstNeighbors;
            }

            int neighborCount = effectiveCoord.NumberOfNeighbors();

            if (pathNeighborIndex < 0)
            {
                return false;
            }

            if (pathNeighborIndex >= neighborCount)
            {
                return false;
            }

            int resolvedIndex = pathNeighborIndex;

            if (!firstNeighbors)
            {
                resolvedIndex = pathNeighborIndex - 1;

                if (resolvedIndex < 0)
                {
                    resolvedIndex = neighborCount - 1;
                }
            }

            spatialNeighbor = effectiveCoord.GetSpatialNeighborBase(resolvedIndex);
            spatialNeighborIndex = resolvedIndex;

            return true;
        }
    }


    //this version has double-sided walls (since there may be an odd number of neighbors- we can't easily do single walls.
    abstract public partial class GenericMazeMap<T> : GenericMazeMapBase, IMap<T>, IMapDrawable<T> where T : ITileCoordinate<T>
    {
        private T _size;


        protected Dictionary<T, bool[]> walls = new Dictionary<T, bool[]>();
        protected Dictionary<T, bool[]> oneWayWalls = new Dictionary<T, bool[]>();
        // oneWayWalls[coord][neighborIndex] = true means wall only blocks FROM coord TO neighborOfCoordIndex
        // This allows movement FROM neighbor back TO coord but not the reverse

        public IReadOnlyDictionary<T, bool[]> OneWayWalls { get { return oneWayWalls; } }
        public void SetOneWayWalls(Dictionary<T, bool[]> oneWayWalls) { this.oneWayWalls = oneWayWalls; }


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
        public override IEnumerable<ITileCoordinateBase> allMapBaseCoords {
            get
            {
                foreach (T coord in allMapCoords)
                {
                    yield return coord;
                }
            }
        }

        

        //generation parameters
        int numTeleportTilesToGenerate = 3;//used during mesh generation
        bool alwaysReverseTeleport = true;//used during mesh generation
        int numSolutionsCounter = 1;
        int oneWayTileCount;
        int seed;

        public GenericMazeMap(T size, T start, T end, int numTeleportTiles = 0, bool alwaysReverseTeleport = true, float worldScale = 1f, int numSolutions = 1, int oneWayTileCount = 0)
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
            this.oneWayTileCount = oneWayTileCount;
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

        /// <summary>
        /// Asynchronously generates a maze using time-sliced yielding.
        /// </summary>
        /// <param name="cancelRef">A reference used to support cancellation mid-process.</param>
        /// <param name="testAllWalls">If true, skips path and branch generation, only initializes walls/visited.</param>
        /// <param name="progressRef">Optional progress reference for external monitoring (0 to 1).</param>
        public override async UniTask GenerateMazeAsync(TaskHandler taskContext, bool testAllWalls = false)
        {
            /*Debug.Log("MazeGen Starting");
            await GenerateFromTopologyAsync(ExampleTopologies.BuildSampleTopology(), taskContext);
            Debug.Log("MazeGen Completed");
            return;*/

            //var yieldTimer = new YieldTimer(cancelRef, cancelRef==null);
            int totalSteps = 0;
            List<T> cachedAllCoords = new();
            foreach (ITileCoordinate<T> tileCoord in allMapCoords)
            {
                cachedAllCoords.Add((T)tileCoord);
                totalSteps++;
            }
            int completedSteps = 0;
            foreach (ITileCoordinate<T> tileCoord in allMapCoords)
            {
                bool[] wallsArray = new bool[tileCoord.NumberOfNeighbors()];
                bool[] oneWayArray = new bool[tileCoord.NumberOfNeighbors()];
                for (int i = 0; i < tileCoord.NumberOfNeighbors(); i++)
                {
                    wallsArray[i] = true;
                    oneWayArray[i] = false;
                }

                walls[tileCoord.value] = wallsArray;
                oneWayWalls[tileCoord.value] = oneWayArray;
                visited[tileCoord.value] = false;

                completedSteps++;
                taskContext.IncrementProgress((float)completedSteps / totalSteps);
                await taskContext.Yield();
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
        protected virtual async UniTask<List<T>> GenerateMainPathAsync(T start, T end, TaskHandler taskContext)
        {
            Stack<T> stack = new Stack<T>();
            List<T> path = new List<T>();
            stack.Push(start);
            visited[start] = true;

            int oneWayPlacementsRemaining = oneWayTileCount;
            int pathStepsSinceStart = 0;

            while (stack.Count > 0)
            {
                T current = stack.Peek();
                path.Add(current);

                if (current.Equals(end))
                    break;

                List<NeighborDetails> neighbors = GetUnvisitedNeighbors(current);

                if (neighbors.Count > 0)
                {
                    T next = neighbors[random.Next(neighbors.Count)].pathNeighbor;

                    // Decide if this connection should be one-way
                    bool useOneWay = oneWayPlacementsRemaining > 0 &&
                                    pathStepsSinceStart > 0 &&  // Don't place one-way at start
                                    random.NextDouble() < 0.5;   // 50% chance

                    if (useOneWay)
                    {
                        RemoveWallAsOneWay(current, next);
                        oneWayPlacementsRemaining--;
                    }
                    else
                    {
                        RemoveWall(current, next);
                    }

                    visited[next] = true;
                    stack.Push(next);
                    pathStepsSinceStart++;
                }
                else
                {
                    stack.Pop();
                }

                await taskContext.Yield();
            }

            return path;
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

            List<NeighborDetails> unVisitedneighbors = new List<NeighborDetails>();
            int numNeighbors = tile.NumberOfNeighbors();
            T[] spatialNeighbors = tile.GetSpatialNeighbors();
            T[] pathNeighbors = tile.GetPathNeighbors(teleporters);
            for (int i = 0; i < numNeighbors; i++)
            {
                T spatialNeighbor = spatialNeighbors[i];
                T pathNeighbor = pathNeighbors[i];
                if (IsWithinBounds(spatialNeighbor) && IsWithinBounds(pathNeighbor) && !visited[spatialNeighbor] && !visited[pathNeighbor])
                    unVisitedneighbors.Add(new NeighborDetails(spatialNeighbor, pathNeighbors[i], teleporters.IsTeleporterSource(spatialNeighbor)));
            }
            return unVisitedneighbors;

        }

        /// <summary>
        /// spatial
        /// </summary>
        /// <param name="current"></param>
        /// <param name="neighbor"></param>
        /// <returns>-1 if not a neighbor</returns>
        public int GetSpatialNeighborIndexOf(T current, T neighbor)
        {
            int neighborIndexCounter = 0;
            //Debug.Log("searching for "+ current + "'s neighborIndex of coord " + neighbor);
            foreach (T n in current.GetSpatialNeighbors())
            {
                //Debug.Log("    Checking neighbor: " + n + "  neighborIndexCounter:"+ neighborIndexCounter);
                if (n.Equals(neighbor))
                    return neighborIndexCounter;

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
            if (teleporters != null && neighborIndex == -1)
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
            try
            {
                walls[current][neighborIndex] = false;
                walls[next][reverseNeighborIndex] = false;
            }
            catch (System.Exception e)
            {
                string s = "!!Exception throw accessing walls array. ";
                s += "\ncurrentTileIndex: " + current + "  num walls: " + walls[current].Length;
                s += "\nnextTileIndex: " + next + "  num walls: " + walls[next].Length;
                s += "\nneighborIndex(from current): " + neighborIndex;
                s += "\nreverseNeighborIndex: " + reverseNeighborIndex;
                Debug.Log(s);
                throw (e);
            }
        }

        private void RemoveWallAsOneWay(T from, T to)
        {
            // Remove wall from 'from' to 'to' (one-way)
            // Keep wall from 'to' back to 'from' (one-way)

            int neighborIndex = GetSpatialNeighborIndexOf(from, to);
            int reverseNeighborIndex = GetSpatialNeighborIndexOf(to, from);

            walls[from][neighborIndex] = false;        // Open the wall
            walls[to][reverseNeighborIndex] = false;    // open the wall

            oneWayWalls[from][neighborIndex] = false;   // Mark as "no wall" FROM this tile (can go)
            oneWayWalls[to][reverseNeighborIndex] = true; // Mark the reverse as one-way (no go)
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
            T neighborCoord = coordT.GetPathNeighbor(neighborIndex, teleporters);


            if (IsWithinBounds(neighborCoord))
            {
                // Check for physical walls
                if (walls[coord][neighborIndex])
                    return -1;

                // Check for one-way restrictions
                if (oneWayWalls != null && oneWayWalls.ContainsKey(coord))
                {
                    if (oneWayWalls[coord][neighborIndex])
                        return -1;  // One-way blocked in this direction
                }

                return 1;
            }
            return -1;
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

        override public Quaternion GetModelSpaceOrientation(ITileCoordinateBase coordBase)
        {
            T coord = (T)coordBase;
            return GetModelSpaceOrientation(coord);
        }
        /// <summary>
        /// This function is defined in descendant classes that have more information about the type of tiles map map.
        /// </summary>
        /// <param name="coord"></param>
        /// <returns></returns>
        virtual public Quaternion GetModelSpaceOrientation(T coord)
        {
            return Quaternion.identity;
        }
        /// <summary>
        /// Get the coordinate at/closest to a given world position.  Very slow- do not use if avoidable
        /// </summary>
        /// <param name="pos">model space position</param>
        /// <returns>return the closest coordinate to the given position, or possibly a unique "invalid coordinate" value- depending on T</returns>
        override public ITileCoordinateBase GetCoordinate(Vector3 pos)
        {
            T closest = default(T);
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

        //TODO: fix->hard code rect here- function should be abstract- defined by implementer of specific coord type
        protected ITileCoordinate<T> UVToCoord(Vector2 uv)
        {
            RectangularCoord s = //(RectangularCoord)(object)size;
                    new RectangularCoord( ((RectangularCoord)(object)size).x-1, ((RectangularCoord)(object)size).y-1);

            int x = (int)((float)s.x * uv.x);
            int y = (int)((float)s.y * uv.y);
            return (ITileCoordinate<T>)(object)new RectangularCoord(x, y);
        }


        class ExpansionResult
        {
            public int pathIndex;
            public int startIndex;
            public List<T> sectionToAdd;
        }

        public async UniTask GenerateFromTopologyAsync(Topology topo, TaskHandler taskContext)
        {
            //-------------------------------------------------
            //Setup wall arrays
            //-------------------------------------------------
            int totalSteps = 0;
            //List<T> cachedAllCoords = new();
            HashSet<T> validTiles = new HashSet<T>();
            foreach (T coord in allMapCoords)
            {
                validTiles.Add(coord);
                totalSteps++;
            }
            /*foreach (ITileCoordinate<T> tileCoord in allMapCoords)
            {
                cachedAllCoords.Add((T)tileCoord);
                totalSteps++;
            }*/
            int completedSteps = 0;
            foreach (ITileCoordinate<T> tileCoord in allMapCoords)
            {
                bool[] wallsArray = new bool[tileCoord.NumberOfNeighbors()];
                bool[] oneWayArray = new bool[tileCoord.NumberOfNeighbors()];
                for (int i = 0; i < tileCoord.NumberOfNeighbors(); i++)
                {
                    wallsArray[i] = true;
                    oneWayArray[i] = false;
                }

                walls[tileCoord.value] = wallsArray;
                oneWayWalls[tileCoord.value] = oneWayArray;
                visited[tileCoord.value] = false;

                completedSteps++;
                taskContext.IncrementProgress((float)completedSteps / totalSteps);
                await taskContext.Yield();
            }

            System.Random rng = new System.Random();

            //initially stores node owner for each used coord.
            List<List<T>> edgeCoordLists = new List<List<T>>();
            List<List<T>> nodeCoordLists = new List<List<T>>();
            HashSet<T> allNodeLineCoords = new HashSet<T>();
            Dictionary<T, int> ownerByCoordinate = new Dictionary<T, int>();

            //-------------------------------------------------
            // node skeletons first
            //-------------------------------------------------

            for (int nodeIndex = 0; nodeIndex < topo.nodes.Length; nodeIndex++)
            {
                Topology.Node node = topo.nodes[nodeIndex];

                List<T> nodeConnections = new List<T>();

                for (int i = 0; i < node.edgeSequence.Length; i++)
                {
                    Vector2 uv = node.GetEdgeConnectionPosition(i);
                    nodeConnections.Add((T)UVToCoord(uv));
                }

                List<T> nodeLine = new List<T>();
                HashSet<T> uniqueness = new HashSet<T>();
                //populate nodeLine
                if (nodeConnections.Count == 1)
                {
                    nodeLine.Add(nodeConnections[0]);
                }
                else
                {
                    for (int i = 0; i < nodeConnections.Count-1; i++)
                    {
                        T start = nodeConnections[i];
                        T end = nodeConnections[i + 1];

                        TileOnPath<T> foundPath =
                            TileAStarPathFinder<T>.GetPathFromTo(
                                start,
                                end,
                                (coord, neighborIndex) =>
                                {
                                    T neighbor = coord.GetSpatialNeighbor(neighborIndex);

                                    if (!IsWithinBounds(neighbor))
                                        return -1;

                                    return 1;
                                },
                                teleporters);

                        if (foundPath == null)
                        {
                            Debug.LogWarning(
                                "Failed node perimeter path " +
                                start + " -> " + end +
                                " node " + nodeIndex);

                            continue;
                        }
                       // foundPath.ToCoordinateList(nodeLine);
                        List<T> pathList = foundPath.ToCoordinateList();
                        int startIndex = 0;
                        if (i > 0) startIndex = 1; //skip first coord for all but first node
                        
                        for (int step = startIndex; step < pathList.Count; step++)
                        {
                            T coord = pathList[step];
                            if (!uniqueness.Contains(coord))
                            {
                                uniqueness.Add(coord);
                                nodeLine.Add(coord);
                            }

                        }
                    }
                }
                //record nodeline
                int newNodeLineIndex = nodeCoordLists.Count;
                nodeCoordLists.Add(nodeLine);


                foreach (T coord in nodeLine)
                {
                    if (ownerByCoordinate.TryGetValue(coord, out int existingOwner))
                    {
                        if (existingOwner != nodeIndex)
                        {
                            Debug.LogWarning(
                                "Node overlap: " +
                                coord +
                                " owned by " +
                                existingOwner +
                                " and " +
                                nodeIndex);
                        }
                    }
                    else
                    {
                        ownerByCoordinate.Add(coord, nodeIndex);
                        allNodeLineCoords.Add(coord);

                    }
                }


                await taskContext.Yield();

            }

            //-------------------------------------------------
            // edge skeletons
            //-------------------------------------------------

            RectangularCoord tst = (RectangularCoord)(object)size; // rectangular for simple testing now

            for (int edgeIndex = 0; edgeIndex < topo.edges.Length; edgeIndex++)
            {
                Topology.Edge edge = topo.edges[edgeIndex];

                int nodeA = edge.nodeAidx;
                int nodeB = edge.nodeBidx;

                T start =
                    (T)UVToCoord(edge.GetNodeAConnectionPosition());

                T end =
                    (T)UVToCoord(edge.GetNodeBConnectionPosition());


                TileOnPath<T> foundEdgePath =
                    TileAStarPathFinder<T>.GetPathFromTo(
                        start,
                        end,
                        (coord, neighborIndex) =>
                        {
                            T neighbor = coord.GetSpatialNeighbor(neighborIndex);

                            if (!IsWithinBounds(neighbor))
                                return -1;

                            if (neighbor.Equals(end))
                                return 1;

                            if (ownerByCoordinate.TryGetValue(neighbor, out int owner))
                            {
                                //if (owner != edge.nodeAidx && owner != edge.nodeBidx)
                                    return -1;
                            }

                            return 1;
                        },
                        teleporters);


                List<T> edgeLine = new List<T>();

                if (foundEdgePath != null)
                {
                    List<T> fullPath = new List<T>();
                    foundEdgePath.ToCoordinateList(fullPath);
                    // remove node endpoint ownership (start at index 1 and end 1 before last)
                    for (int i = 1; i < fullPath.Count - 1; i++)
                    {
                        T coord = fullPath[i];
                        if (!allNodeLineCoords.Contains(coord))
                            edgeLine.Add(fullPath[i]);
                        else
                            Debug.LogWarning("Skipping element["+i+"] in edge line["+ edgeIndex + "], because it exists in a nodeline");
                    }
                    //Debug.Log("Generated unquie edge line[" + edgeIndex + "], final length: "+ edgeLine.Count);

                }
                else
                {
                    Debug.LogWarning(
                        "Failed edge path " +
                        start + " -> " + end +
                        " nodes: "+ edge.nodeAidx + " -> "+ edge.nodeBidx+
                        " edge: " + edgeIndex);
                }


                edgeCoordLists.Add(edgeLine);


                //-------------------------------------------------
                // assign edge ownership split
                //-------------------------------------------------

                int splitIndex =
                    Mathf.Clamp(
                        Mathf.RoundToInt(edgeLine.Count * 0.5f),
                        0,
                        edgeLine.Count);


                for (int i = 0; i < edgeLine.Count; i++)
                {
                    int owner = i < splitIndex ? nodeA : nodeB;

                    T coord = edgeLine[i];

                    if (ownerByCoordinate.TryGetValue(coord, out int existingOwner))
                    {
                        if (existingOwner != owner)
                        {
                            Debug.LogWarning(
                                "Edge overlap: " +
                                coord +
                                " already owned by " +
                                existingOwner +
                                " trying to assign " +
                                owner);
                        }
                    }
                    else
                    {
                        ownerByCoordinate.Add(coord, owner);
                    }
                }
                await UniTask.SwitchToMainThread();

                GenerateRegionDebugTexture(ownerByCoordinate, tst.x, tst.y, "regionOwnerDebugByEdgeIteration" + edgeIndex + ".png");
                await UniTask.SwitchToThreadPool();
                await taskContext.Yield();
                
            }

            //this.ownerByCoordinate = ownerByCoordinate;


            //-------------------------------
            // done skeleton- draw debug
            //-------------------------------

            //RectangularCoord tst = (RectangularCoord)(object)size; // rectangular for simple testing now
            await UniTask.SwitchToMainThread();
            
            GenerateRegionDebugTexture(ownerByCoordinate, tst.x, tst.y, "regionDebug.png");
            await UniTask.SwitchToThreadPool();

            //-------------------------------
            // prepare for and do PATH EXPANSIONS
            //-------------------------------


            List<List<T>> allPathsList = new List<List<T>>();

            ownerByCoordinate.Clear();//we are now switching this dictionary- above it stored nodeIndexs, in the below funcs it will use path indexes
            //add all node and edge lists to a single allPathsList
            for (int i = 0; i < nodeCoordLists.Count; i++)
            {
                int pathIndex = allPathsList.Count;
                List<T> nodePath = nodeCoordLists[i];
                allPathsList.Add(nodePath);
                foreach (T coord in nodePath)
                {
                    if (!ownerByCoordinate.ContainsKey(coord))
                        ownerByCoordinate.Add(coord, pathIndex);
                    else
                        Debug.Log("Node list[" + pathIndex + "] contains duplicate coord("+coord+ ").  Owner: " + ownerByCoordinate[coord]);
                }

            }
            Debug.Log("allPathsList- Starting Edge list index: " + allPathsList.Count);
            for (int i = 0; i < edgeCoordLists.Count; i++)
            {
                int pathIndex = allPathsList.Count;
                List<T> edgePath = edgeCoordLists[i];
                allPathsList.Add(edgePath);
                foreach (T coord in edgePath)
                    if (!ownerByCoordinate.ContainsKey(coord))
                        ownerByCoordinate.Add(coord, pathIndex);
                    else
                        Debug.Log("Edge list[" + i + "] contains duplicate coord(" + coord + ").  Owner: "+ ownerByCoordinate[coord]);
            }



            //Dictionary<T, float> randomTileAdditionalCost= new Dictionary<T, float>();

            await NewExpandPathsToFillRegion(allPathsList, validTiles, ownerByCoordinate, taskContext);

            await UniTask.SwitchToMainThread();
            GenerateRegionDebugTexture(ownerByCoordinate, tst.x, tst.y, "postExpansionDebug.png");
            await UniTask.SwitchToThreadPool();

            //-------------------------------------------------
            // carve walls along paths
            //-------------------------------------------------
            HashSet<T> unqiueSanityCheck = new HashSet<T>();
            foreach (List<T> finalPath in allPathsList)
            {
                await CarvePathInWalls(finalPath, taskContext);
                foreach (T c in finalPath)
                {
                    if (unqiueSanityCheck.Contains(c))
                        Debug.Log("Sanity check failed: found " + c + " more than once.");
                    else unqiueSanityCheck.Add(c);
                }
            }



            //-------------------------------------------------
            // carve walls at connections between nodes and edges
            //-------------------------------------------------
          
            for (int edgeIndex = 0; edgeIndex < topo.edges.Length; edgeIndex++)
            {
                Topology.Edge edge = topo.edges[edgeIndex];

                T nodeACoord = (T)UVToCoord(edge.GetNodeAConnectionPosition());
                T nodeBCoord = (T)UVToCoord(edge.GetNodeBConnectionPosition());

                List<T> edgePath = edgeCoordLists[edgeIndex];

                if (edgePath.Count > 1)
                {
                    if (GetSpatialNeighborIndexOf(nodeACoord, edgePath[0]) != -1)
                    {
                        RemoveWall(nodeACoord, edgePath[0]);
                        RemoveWall(nodeBCoord, edgePath[edgePath.Count - 1]);
                      //  Debug.Log("Removing walls between nodeACoord("+ nodeACoord+ ") and edgePath[0]("+edgePath[0]+") ");
                      //  Debug.Log("and between nodeBCoord(" + nodeBCoord + ") and edgePath[last](" + edgePath[edgePath.Count - 1] + ") ");
                    }
                    else
                    {
                        RemoveWall(nodeBCoord, edgePath[0]); // null ref here..
                        RemoveWall(nodeACoord, edgePath[edgePath.Count - 1]);
                     //   Debug.Log("Removing walls between nodeBCoord(" + nodeBCoord + ") and edgePath[0](" + edgePath[0] + ") ");
                     //   Debug.Log("and between nodeACoord(" + nodeACoord + ") and edgePath[last](" + edgePath[edgePath.Count - 1] + ") ");
                    }
                }

                await taskContext.Yield();
            }
           
            async UniTask ExpandPathsToFillRegion(IList<List<T>> allPaths,HashSet<T> region,Dictionary<T, int> ownerByCoordinate,TaskHandler taskContext)
            {
                int debugNumberOfSuccessfulExpandSingleStepCalls = 0;
                int debugNumberOfFailedExpandSingleStepCalls = 0;

                System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
                timer.Start();

                List<List<T>> expandedPaths = new List<List<T>>(allPaths);

                List<RandomSeq> randomPathOrders = new List<RandomSeq>();
                List<int> nextSequenceEntry = new List<int>();

                for (int i = 0; i < expandedPaths.Count; i++)
                {
                    randomPathOrders.Add(new RandomSeq(Mathf.Max(0, expandedPaths[i].Count - 1)));
                    nextSequenceEntry.Add(0);
                }

                int maxIterations = 1000000;
                int[] failedToExpandPathCounts = new int[allPaths.Count];
                int noneExpandedCount = 0;

                while (maxIterations-- > 0)
                {
                    bool anyExpanded = false;

                    for (int i = 0; i < expandedPaths.Count; i++)
                    {
                        List<T> path = expandedPaths[i];

                        if (failedToExpandPathCounts[i] > path.Count + 2)
                            continue;

                        bool expanded = await TryExpandSingleStep(path,i,randomPathOrders[i],nextSequenceEntry,ownerByCoordinate,region,taskContext);

                        if (expanded)
                        {
                            debugNumberOfSuccessfulExpandSingleStepCalls++;
                            failedToExpandPathCounts[i] = 0;
                            anyExpanded = true;
                        }
                        else
                        {
                            failedToExpandPathCounts[i]++;
                            debugNumberOfFailedExpandSingleStepCalls++;
                        }
                    }

                    if (!anyExpanded)
                    {
                        noneExpandedCount++;

                        if (noneExpandedCount > expandedPaths.Count)
                            break;
                    }

                    if (maxIterations < 1)
                    {
                        await UniTask.SwitchToMainThread();
                        Debug.Log("Exceeded max of 1M iterations- ceasing and completing");
                        await UniTask.SwitchToThreadPool();
                    }

                    taskContext.IncrementProgress(0.1f);
                    await taskContext.Yield();
                }

                timer.Stop();

                await UniTask.SwitchToMainThread();
                Debug.Log(
                    "Expansion step complete process time: " +
                    timer.Elapsed +
                    "       ExpandPath calls,  success: " +
                    debugNumberOfSuccessfulExpandSingleStepCalls +
                    "   failed:" +
                    debugNumberOfFailedExpandSingleStepCalls);
                await UniTask.SwitchToThreadPool();
            }

            async UniTask<bool> TryExpandSingleStep(List<T> path,int pathIndex,RandomSeq randomEdges,List<int> randomEdgePositions,
                                    Dictionary<T, int> ownerByCoordinate,HashSet<T> region,TaskHandler taskContext)
            {
                if (randomEdges.Count == 0)
                    return false;

                int position = randomEdgePositions[pathIndex];

                for (int attempt = 0; attempt < randomEdges.Count; attempt++)
                {
                    if (position >= randomEdges.Count)
                        position = 0;

                    int startIndex = randomEdges[position];
                    position++;

                    if (startIndex >= path.Count - 1)
                        continue;

                    int endIndex = startIndex + 1;

                    T startCoordinate = path[startIndex];
                    T endCoordinate = path[endIndex];

                    float MoveCost(ITileCoordinate<T> coord, int neighborIndex)
                    {
                        T neighbor = (T)coord.GetSpatialNeighbor(neighborIndex);

                        if ((coord.Equals(startCoordinate) && neighbor.Equals(endCoordinate)) ||
                            (coord.Equals(endCoordinate) && neighbor.Equals(startCoordinate)))
                            return -1;

                        if (!region.Contains(neighbor))
                            return -1;

                        if (neighbor.Equals(startCoordinate) || neighbor.Equals(endCoordinate))
                            return 1;

                        if (ownerByCoordinate.ContainsKey(neighbor))
                            return -1;

                        return 1;
                    }

                    TileOnPath<T> found =TileAStarPathFinder<T>.GetPathFromTo(startCoordinate,endCoordinate,MoveCost,teleporters);

                    await taskContext.Yield();

                    if (found == null)
                        continue;

                    List<T> sectionToAdd = found.ToCoordinateList();
                    sectionToAdd.RemoveAt(0);
                    sectionToAdd.RemoveAt(sectionToAdd.Count - 1);

                    path.InsertRange(startIndex + 1, sectionToAdd);

                    foreach (T coord in sectionToAdd)
                    {
                        if (ownerByCoordinate.TryGetValue(coord, out int foundPathIndex))
                        {
                            if (foundPathIndex != pathIndex)
                                throw new Exception(
                                    "attempt to add coord(" +
                                    coord +
                                    ") to ownerByCoordinate for path(" +
                                    pathIndex +
                                    ") failed- coord already exists as part of a different path(" +
                                    foundPathIndex +
                                    ").");
                        }
                        else
                        {
                            ownerByCoordinate.Add(coord, pathIndex);
                        }
                    }

                    // One new edge is created for every inserted vertex.
                    for (int i = 0; i < sectionToAdd.Count; i++)
                        randomEdges.Expand(startIndex + 1);

                    randomEdgePositions[pathIndex] = position;

                    return true;
                }

                randomEdgePositions[pathIndex] = position;

                return false;
            }


            static void GenerateRegionDebugTexture(Dictionary<T, int> ownerByCoordinate, int width, int height, string filename)
            {
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;

                Color32[] pixels = new Color32[width * height];

                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color.black;
                }

                Dictionary<int, Color32> ownerColors =
                    new Dictionary<int, Color32>();

                System.Random rng = new System.Random(12345);

                foreach (int owner in ownerByCoordinate.Values)
                {
                    if (ownerColors.ContainsKey(owner))
                        continue;

                    byte r = (byte)rng.Next(40, 256);
                    byte g = (byte)rng.Next(40, 256);
                    byte b = (byte)rng.Next(40, 256);

                    ownerColors.Add(owner, new Color32(r, g, b, 255));
                }

                foreach (KeyValuePair<T, int> kv in ownerByCoordinate)
                {
                    RectangularCoord coord = (RectangularCoord)(object)kv.Key;

                    if (coord.x < 0
                        || coord.y < 0
                        || coord.x >= width
                        || coord.y >= height)
                    {
                        continue;
                    }

                    int index = coord.x + coord.y * width;

                    pixels[index] = ownerColors[kv.Value];
                }

                tex.SetPixels32(pixels);
                tex.Apply();

                byte[] png = tex.EncodeToPNG();

                string fullPath =
                    System.IO.Path.Combine(
                        Application.dataPath,
                        filename);

                System.IO.File.WriteAllBytes(fullPath, png);

#if UNITY_EDITOR
                UnityEditor.AssetDatabase.ImportAsset(
                    "Assets/" + filename,
                    UnityEditor.ImportAssetOptions.ForceUpdate);
#endif

                Debug.Log("Saved region debug texture: " + fullPath);
            }



            async UniTask NewExpandPathsToFillRegion(IList<List<T>> allPaths,HashSet<T> region,Dictionary<T, int> ownerByCoordinate,TaskHandler taskContext)
            {
                int debugNumberOfSuccessfulExpandSingleStepCalls = 0;
                int debugNumberOfFailedExpandSingleStepCalls = 0;

                System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
                timer.Start();

                List<List<T>> expandedPaths = new List<List<T>>(allPaths);

                List<RandomSeq> randomPathOrders = new List<RandomSeq>();
                List<int> nextSequenceEntry = new List<int>();

                for (int i = 0; i < expandedPaths.Count; i++)
                {
                    randomPathOrders.Add(new RandomSeq(Mathf.Max(0, expandedPaths[i].Count - 1)));
                    nextSequenceEntry.Add(0);
                }

                int maxIterations = 1000000;
                int[] failedToExpandPathCounts = new int[allPaths.Count];
                int noneExpandedCount = 0;

                while (maxIterations-- > 0)
                {
                    bool anyExpanded = false;

                    List<UniTask<ExpansionProposal<T>>> expansionTasks = new List<UniTask<ExpansionProposal<T>>>();

                    for (int i = 0; i < expandedPaths.Count; i++)
                    {
                        if (failedToExpandPathCounts[i] > expandedPaths[i].Count + 2)
                        {
                            expansionTasks.Add(UniTask.FromResult<ExpansionProposal<T>>(null));
                            continue;
                        }

                        expansionTasks.Add(
                            FindExpansionProposal(expandedPaths[i],i,randomPathOrders[i],nextSequenceEntry,ownerByCoordinate,region,taskContext));
                    }

                    UniTask<ExpansionProposal<T>[]> allTasks =
                        UniTask.WhenAll(expansionTasks);

                    while (!allTasks.Status.IsCompleted())
                    {
                        taskContext.IncrementProgress(0.1f);
                        await taskContext.Yield();
                    }

                    ExpansionProposal<T>[] proposals = await allTasks;

                    for (int i = 0; i < proposals.Length; i++)
                    {
                        ExpansionProposal<T> proposal = proposals[i];

                        if (proposal == null)
                            continue;

                        if (CommitExpansion(proposal,expandedPaths,randomPathOrders,nextSequenceEntry,ownerByCoordinate))
                        {
                            debugNumberOfSuccessfulExpandSingleStepCalls++;
                            failedToExpandPathCounts[i] = 0;
                            anyExpanded = true;
                        }
                        else
                        {
                            failedToExpandPathCounts[i]++;
                            debugNumberOfFailedExpandSingleStepCalls++;
                        }
                    }

                    if (!anyExpanded)
                    {
                        noneExpandedCount++;

                        if (noneExpandedCount > expandedPaths.Count)
                            break;
                    }
                    else
                    {
                        noneExpandedCount = 0;
                    }

                    if (maxIterations < 1)
                    {
                        await UniTask.SwitchToMainThread();
                        Debug.Log("Exceeded max of 1M iterations- ceasing and completing");
                        await UniTask.SwitchToThreadPool();
                    }

                    taskContext.IncrementProgress(0.1f);
                    await taskContext.Yield();
                }

                timer.Stop();

                await UniTask.SwitchToMainThread();

                Debug.Log(
                    "Expansion step complete process time: " +
                    timer.Elapsed +
                    "       ExpandPath calls,  success: " +
                    debugNumberOfSuccessfulExpandSingleStepCalls +
                    "   failed:" +
                    debugNumberOfFailedExpandSingleStepCalls);

                await UniTask.SwitchToThreadPool();
            }
            async UniTask<ExpansionProposal<T>> FindExpansionProposal(List<T> path,int pathIndex,RandomSeq randomEdges,List<int> randomEdgePositions,Dictionary<T, int> ownerByCoordinate,HashSet<T> region,TaskHandler taskContext)
            {
                if (randomEdges.Count == 0)
                    return null;

                int position = randomEdgePositions[pathIndex];

                for (int attempt = 0; attempt < randomEdges.Count; attempt++)
                {
                    if (position >= randomEdges.Count)
                        position = 0;

                    int startIndex = randomEdges[position];
                    position++;

                    if (startIndex >= path.Count - 1)
                        continue;

                    int endIndex = startIndex + 1;

                    T startCoordinate = path[startIndex];
                    T endCoordinate = path[endIndex];

                    float MoveCost(ITileCoordinate<T> coord, int neighborIndex)
                    {
                        T neighbor = (T)coord.GetSpatialNeighbor(neighborIndex);

                        if ((coord.Equals(startCoordinate) && neighbor.Equals(endCoordinate)) ||
                            (coord.Equals(endCoordinate) && neighbor.Equals(startCoordinate)))
                            return -1;

                        if (!region.Contains(neighbor))
                            return -1;

                        if (neighbor.Equals(startCoordinate) || neighbor.Equals(endCoordinate))
                            return 1;

                        if (ownerByCoordinate.ContainsKey(neighbor))
                            return -1;

                        return 1;
                    }

                    TileOnPath<T> found = TileAStarPathFinder<T>.GetPathFromTo(startCoordinate,endCoordinate,MoveCost,teleporters);

                    await taskContext.Yield();

                    if (found == null)
                        continue;

                    List<T> sectionToAdd = found.ToCoordinateList();
                    sectionToAdd.RemoveAt(0);
                    sectionToAdd.RemoveAt(sectionToAdd.Count - 1);

                    return new ExpansionProposal<T>
                    {
                        PathIndex = pathIndex,
                        StartIndex = startIndex,
                        NextRandomPosition = position,
                        SectionToAdd = sectionToAdd
                    };
                }

                return new ExpansionProposal<T>
                {
                    PathIndex = pathIndex,
                    NextRandomPosition = position,
                    SectionToAdd = null
                };
            }

            bool CommitExpansion(ExpansionProposal<T> proposal,IList<List<T>> paths,List<RandomSeq> randomPathOrders,List<int> randomEdgePositions,Dictionary<T, int> ownerByCoordinate)
            {
                if (proposal.SectionToAdd == null)
                {
                    randomEdgePositions[proposal.PathIndex] =
                        proposal.NextRandomPosition;
                    return false;
                }

                foreach (T coord in proposal.SectionToAdd)
                {
                    if (ownerByCoordinate.TryGetValue(coord, out int foundPathIndex))
                    {
                        if (foundPathIndex != proposal.PathIndex)
                            return false;
                    }
                }

                List<T> path = paths[proposal.PathIndex];

                path.InsertRange(
                    proposal.StartIndex + 1,
                    proposal.SectionToAdd);

                foreach (T coord in proposal.SectionToAdd)
                {
                    if (!ownerByCoordinate.ContainsKey(coord))
                        ownerByCoordinate.Add(coord, proposal.PathIndex);
                }

                for (int i = 0; i < proposal.SectionToAdd.Count; i++)
                    randomPathOrders[proposal.PathIndex].Expand(proposal.StartIndex + 1);

                randomEdgePositions[proposal.PathIndex] =
                    proposal.NextRandomPosition;

                return true;
            }

        }
        class ExpansionProposal<S>
        {
            public int PathIndex;
            public int StartIndex;
            public int NextRandomPosition;
            public List<S> SectionToAdd;
        }

        async UniTask CarvePathInWalls(IList<T> finalPath, TaskHandler taskContext)
        {
            //-------------------------------------------------
            // carve final path
            //-------------------------------------------------

            for (int i = 0; i < finalPath.Count - 1; i++)
            {
                //debug sanity check
                if (!new List<T>(finalPath[i].GetSpatialNeighbors()).Contains(finalPath[i + 1]))
                {
                    await UniTask.SwitchToMainThread();
                    Debug.LogError("Invalid path provided to CarvePathInWalls.  element["+i+"]: "+ finalPath[i] + " does not have element["+(i+1)+"]: "+ finalPath[i+1] + " as  valid spatial neighbor");
                    await UniTask.SwitchToThreadPool();
                }


                RemoveWall(finalPath[i], finalPath[i + 1]);

                if ((i & 31) == 0)
                    await taskContext.Yield();
            }
          //  await UniTask.SwitchToMainThread();
          //  Debug.Log("Region output path" + string.Join(",", finalPath));
          //  await UniTask.SwitchToThreadPool();
        }


        protected void Shuffle<K>(List<K> list)
        {
            int n = list.Count;

            while (n > 1)
            {
                n--;

                int k = random.Next(n + 1);

                K temp = list[k];
                list[k] = list[n];
                list[n] = temp;
            }
        }
    }
}

class RandomSeq:IReadOnlyList<int>
{
    
    public int Count => sequence.Count;

    public int this[int index] => ((IReadOnlyList<int>)sequence)[index];

    protected List<int> sequence;
    protected System.Random rng = new System.Random();

    public RandomSeq(int length)
    {
        sequence = new List<int>(length);
        for (int i = 0; i < length; i++)
        {
            sequence.Add(i);
        }
        Shuffle();
    }
    public void Expand()
    {
        Expand(0);
    }
    public void Expand(int after)
    {
        int newValue = sequence.Count;
        if (after > newValue)
            throw new Exception("'after' Parameter passed to RandomSeq.Expand is larger than the sequence itself.");
        int newIndex = rng.Next(after, newValue + 1);
        sequence.Insert(newIndex, newValue);
    }
    protected void Shuffle()
    {
        int n = sequence.Count;
        
        while (n > 1)
        {
            n--;

            int k = rng.Next(n + 1);

            int temp = sequence[k];
            sequence[k] = sequence[n];
            sequence[n] = temp;
        }
    }

    public IEnumerator<int> GetEnumerator()
    {
        return ((IEnumerable<int>)sequence).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)sequence).GetEnumerator();
    }
}