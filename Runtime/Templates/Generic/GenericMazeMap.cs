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
        abstract public Vector3 GetModelSpacePosition(ITileCoordinateBase coordBase);
        abstract public Quaternion GetModelSpaceOrientation(ITileCoordinateBase coordBase);
        abstract public Bounds GetModelSpaceBounds();
        abstract public Vector3 SingleTileModelSpaceOffset();  //should provide the model space offset between the first tile, and a tile with all coorinate dimensions incremented by one
    }

    //this version has double-sided walls (since there may be an odd number of neighbors- we can't easily do single walls.
    abstract public class GenericMazeMap<T> : GenericMazeMapBase, IMap<T>, IMapDrawable<T> where T : ITileCoordinate<T>
    {
        private T _size;


        protected Dictionary<T, bool[]> walls = new Dictionary<T, bool[]>();
        public void SetWalls(Dictionary<T, bool[]> walls) { this.walls = walls; }
        public IReadOnlyDictionary<T, bool[]> Walls { get { return walls; } }

        protected Dictionary<T, bool> visited = new Dictionary<T, bool>();
        protected System.Random random;
        public readonly int seed;

        public T size { get { return _size; } }
        public float worldScale { get; private set; }

        public T start;
        public T end;
        public abstract IEnumerable<T> allMapCoords { get; }

        public GenericMazeMap(T size, T start, T end, float worldScale = 1f, int numSolutions = 1)
        {
            this.start = start;
            this.end = end;
            //int numWallDimensions = size.NumberOfNeighbors();
            this._size = size;
            this.worldScale = worldScale;
            this.seed = System.Environment.TickCount;
            this.random = new System.Random(seed);
            this.numSolutionsCounter = numSolutions;
        }
        public GenericMazeMap(T size, T start, T end, int seed, float worldScale = 1f, int numSolutions = 1)
        {
            this.start = start;
            this.end = end;
            //int numWallDimensions = size.NumberOfNeighbors();
            this._size = size;
            this.worldScale = worldScale;
            this.seed = seed;
            this.random = new System.Random(seed);
            this.numSolutionsCounter = numSolutions;
        }



        void SanityCheckWalls()
        {
            int GetNeighborIndex(T source, T neighor)
            {
                for (int n = 0; n < source.NumberOfNeighbors(); n++)
                {
                    if (source.GetNeighbor(n).Equals(neighor))
                        return n;
                }
                throw new System.Exception("inavlid neighbor");
            }

            foreach (T tileCoord in allMapCoords)
            {
                for (int n = 0; n < tileCoord.NumberOfNeighbors(); n++)
                {
                    T neighborCoord = tileCoord.GetNeighbor(n);
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
            foreach (T n in candidate.GetNeighbors())
            {
                //if (visited.ContainsKey(n) && visited[n] && currentPath.Contains(n))
                if (currentPath.Contains(n))
                    used += 30;
                else
                {
                    foreach (T m in n.GetNeighbors())
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
            foreach (ITileCoordinate<T> tileCoord in allMapCoords)
                totalSteps++;
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

                List<T> neighbors = GetUnvisitedNeighbors(current);

                if (neighbors.Count > 0)
                {
                    //T next = neighbors[random.Next(neighbors.Count)];
                    T next = PickWeighted(neighbors, stack);
                    //  while (DistFromPath(next) < 2 && ((random.Next()&0x01)==0))
                    {
                        //    next = neighbors[random.Next(neighbors.Count)];
                    }

                    RemoveWall(current, next);
                    visited[next] = true;
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

            float DistFromPath(T checkCoord, Stack<T> stack)
            {
                float min = float.PositiveInfinity;
                foreach (T pathStep in stack)
                {
                    float dist = checkCoord.HeuristicDistanceTo(pathStep);
                    if (dist < min) min = dist;
                }
                return min;
            }
        }

        int numSolutionsCounter = 1;
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
                List<T> neighbors = GetUnvisitedNeighbors(current);

                if (neighbors.Count > 0)
                {
                    T next = neighbors[random.Next(neighbors.Count)];
                    RemoveWall(current, next);
                    visited[next] = true;
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

        /// <summary>
        /// Returns the list of unvisited neighboring tiles.
        /// </summary>
        /// <param name="tile">Current tile to check from.</param>
        protected virtual List<T> GetUnvisitedNeighbors(T tile)
        {
            List<T> unVisitedneighbors = new List<T>();

            foreach (T neighbor in tile.GetNeighbors())
            {
                if (IsWithinBounds(neighbor))
                {
                    // Debug.Log(" checking neighbor for visited-  current: " + tile + "  neighbor: " + neighbor);
                    if (!visited[neighbor])
                        unVisitedneighbors.Add(neighbor);
                }
            }

            return unVisitedneighbors;
        }

        public int GetNeighborIndexOf(T current, T neighbor)
        {
            int neighborIndexCounter = 0;
            foreach (T n in current.GetNeighbors())
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
            int nieghborIndex = GetNeighborIndexOf(current, next);
            int reverseNeighborIndex = GetNeighborIndexOf(next, current);
            walls[current][nieghborIndex] = false;
            walls[next][reverseNeighborIndex] = false;
        }

        //returns cost to move from one tile to it's neighbor, returns -1 if impassible, or not neighbors
        public float GetMoveCost(ITileCoordinate<T> coordT, ITileCoordinate<T> coordTDest, float max = -1, bool bothdir = false)
        {
            int neighborIndex = GetNeighborIndexOf(coordT.value, coordTDest.value);
            if (neighborIndex == -1) return -1;
            return GetMoveCost(coordT, neighborIndex, max, bothdir);
        }

        // Get move cost between neighboring tiles, -1 means impassable
        public virtual float GetMoveCost(ITileCoordinate<T> coordT, int neighborIndex, float max = -1, bool bothdir = false)
        {
            T coord = coordT.value;
            if (!IsWithinBounds(coord)) return -1;
            T neighborCoord = coordT.GetNeighbor(neighborIndex);

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
            //this is the center of 1 tile past x,y (for 2d) - need to subtract half a tiles worth
            bounds = new Bounds(sizePos / 2, sizePos);
            Vector3 singleTileOffset = SingleTileModelSpaceOffset();
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

