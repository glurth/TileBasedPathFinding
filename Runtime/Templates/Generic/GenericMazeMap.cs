using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

using EyE.Threading;

namespace Eye.Maps.Templates
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
        abstract public UniTask GenerateMazeAsync(CancelBoolRef cancelRef, bool testAllWalls = false, ProgressFloatRef progressRef = null);
        abstract public void GenerateMaze(bool testAllWalls = false);
        abstract public System.Type CoordinateType {get;}
    }

    //this version has double-sided walls (since there may be an odd number of neighbors- we can't easily do single walls.
    abstract public class GenericMazeMap<T> : GenericMazeMapBase, IMap<T>, IMapDrawable<T> where T : ITileCoordinate<T>
    {
        private T _size;


        private Dictionary<T, bool[]> walls = new Dictionary<T, bool[]>();
        public void SetWalls(Dictionary<T, bool[]> walls) { this.walls = walls; }
        public IReadOnlyDictionary<T, bool[]> Walls { get { return walls; } }

        private Dictionary<T, bool> visited = new Dictionary<T, bool>();
        private System.Random random = new System.Random();

        public T size { get { return _size; } }
        public float worldScale { get; private set; }

        public T start;
        public T end;
        public abstract IEnumerable<T> allMapCoords { get; }
        
        public GenericMazeMap(T size, T start, T end, float worldScale = 1f)
        {
            this.start = start;
            this.end = end;
            int numWallDimensions = size.NumberOfNeighbors();
            this._size = size;
            this.worldScale = worldScale;


            // Generate the maze
            //GenerateMaze();
        }
        /*
        public void GenerateMaze(bool testAllWalls = false)
        {
            foreach (ITileCoordinate<T> tileCoord in allMapCoords)
            {
                bool[] wallsArray = new bool[size.NumberOfNeighbors()];
                for (int i = 0; i < size.NumberOfNeighbors(); i++)
                    wallsArray[i] = true;
                walls.Add(tileCoord.value, wallsArray);
                visited.Add(tileCoord.value, false);
            }
            if (!testAllWalls)
            {
                List<T> mainPath = GenerateMainPath(start, end);
                GenerateBranches(mainPath);
            }
        }

        private List<T> GenerateMainPath(T start, T end)
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
                {
                    break;  // Main path to end generated
                }

                // Get available directions (randomized to introduce random turns)
                List<T> neighbors = GetUnvisitedNeighbors(current);

                if (neighbors.Count > 0)
                {
                    T next = neighbors[random.Next(neighbors.Count)];
                    RemoveWall(current, next);
                    visited[next] = true;
                    stack.Push(next);
                }
                else
                {
                    stack.Pop();
                }
            }

            return path;
        }

        private void GenerateBranches(List<T> mainPath)
        {
            List<T> allPathSteps = new List<T>(mainPath);
            int maxZeros = 1000;
            int pathLengthZeroCount = 0;
            //for (int i = 0; i < branchCount; i++)
            while (pathLengthZeroCount < maxZeros)
            {
                float curve = Mathf.Pow(Random.value, 2);
                T branchStart = allPathSteps[(int)(curve * allPathSteps.Count)];
                List<T> newPath = GenerateRandomPath(branchStart);
                if (newPath.Count == 0)
                    pathLengthZeroCount++;
                else
                    pathLengthZeroCount = 0;
                allPathSteps.AddRange(newPath);

            }
        }

        private List<T> GenerateRandomPath(T start)
        {
            List<T> path = new List<T>();
            //path.Add(start);
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
            }
            return path;
        }
        */

        /// <summary>
        /// Asynchronously generates a maze using time-sliced yielding.
        /// </summary>
        /// <param name="cancelRef">A reference used to support cancellation mid-process.</param>
        /// <param name="testAllWalls">If true, skips path and branch generation, only initializes walls/visited.</param>
        /// <param name="progressRef">Optional progress reference for external monitoring (0 to 1).</param>
        public override async UniTask GenerateMazeAsync(CancelBoolRef cancelRef, bool testAllWalls = false, ProgressFloatRef progressRef = null)
        {
            var yieldTimer = new YieldTimer(cancelRef, cancelRef==null);
            int totalSteps = 0;
            foreach (ITileCoordinate<T> tileCoord in allMapCoords)
                totalSteps++;
            int completedSteps = 0;

            foreach (ITileCoordinate<T> tileCoord in allMapCoords)
            {
                bool[] wallsArray = new bool[size.NumberOfNeighbors()];
                for (int i = 0; i < size.NumberOfNeighbors(); i++)
                    wallsArray[i] = true;

                walls[tileCoord.value] = wallsArray;
                visited[tileCoord.value] = false;

                completedSteps++;
                if (progressRef != null)
                    progressRef.Value = (float)completedSteps / totalSteps;

                await yieldTimer.YieldOnTimeSlice();
            }

            if (!testAllWalls)
            {
                List<T> mainPath = await GenerateMainPathAsync(start, end, yieldTimer);
                await GenerateBranchesAsync(mainPath, yieldTimer);
            }

            if (progressRef != null)
                progressRef.Value = 1f;
        }

        /// <summary>
        /// Synchronously generates the maze by invoking the async version and backgrounding it.
        /// </summary>
        /// <param name="cancelRef">A reference used to support cancellation mid-process.</param>
        /// <param name="testAllWalls">If true, skips path and branch generation, only initializes walls/visited.</param>
        /// <param name="progressRef">Optional progress reference for external monitoring (0 to 1).</param>
        public override void GenerateMaze(bool testAllWalls = false)
        {
            GenerateMazeAsync(null, testAllWalls, null).GetAwaiter().GetResult();//.Forget();
        }
        public override System.Type CoordinateType { get => typeof(T); }
        /// <summary>
        /// Asynchronously generates the main path from start to end.
        /// </summary>
        /// <param name="start">The starting tile.</param>
        /// <param name="end">The target tile to reach.</param>
        /// <param name="yieldTimer">Used to yield control based on elapsed time.</param>
        private async UniTask<List<T>> GenerateMainPathAsync(T start, T end, YieldTimer yieldTimer)
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
                    T next = neighbors[random.Next(neighbors.Count)];
                    RemoveWall(current, next);
                    visited[next] = true;
                    stack.Push(next);
                }
                else
                {
                    stack.Pop();
                }

                await yieldTimer.YieldOnTimeSlice();
            }

            return path;
        }

        /// <summary>
        /// Asynchronously generates branching paths off the main path.
        /// </summary>
        /// <param name="mainPath">The main path tiles to branch from.</param>
        /// <param name="yieldTimer">Used to yield control based on elapsed time.</param>
        private async UniTask GenerateBranchesAsync(List<T> mainPath, YieldTimer yieldTimer)
        {
            List<T> allPathSteps = new List<T>(mainPath);
            int maxZeros = 1000;
            int pathLengthZeroCount = 0;

            while (pathLengthZeroCount < maxZeros)
            {
                float curve = Mathf.Pow( await ThreadRand.GetRandAsync(), 2);
                T branchStart = allPathSteps[(int)(curve * allPathSteps.Count)];
                List<T> newPath = await GenerateRandomPathAsync(branchStart, yieldTimer);

                if (newPath.Count == 0)
                    pathLengthZeroCount++;
                else
                    pathLengthZeroCount = 0;

                allPathSteps.AddRange(newPath);
                await yieldTimer.YieldOnTimeSlice();
            }
        }

        /// <summary>
        /// Asynchronously generates a random path from a given start tile.
        /// </summary>
        /// <param name="start">Starting tile for the path.</param>
        /// <param name="yieldTimer">Used to yield control based on elapsed time.</param>
        private async UniTask<List<T>> GenerateRandomPathAsync(T start, YieldTimer yieldTimer)
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

                await yieldTimer.YieldOnTimeSlice();
            }

            return path;
        }

        /// <summary>
        /// Returns the list of unvisited neighboring tiles.
        /// </summary>
        /// <param name="tile">Current tile to check from.</param>
        private List<T> GetUnvisitedNeighbors(T tile)
        {
            List<T> unVisitedneighbors = new List<T>();

            foreach (T neighbor in tile.GetNeighbors())
            {
                if (IsWithinBounds(neighbor))
                    if (!visited[neighbor])
                        unVisitedneighbors.Add(neighbor);
            }

            return unVisitedneighbors;
        }

        private int GetNeighborIndexOf(T current, T neighbor)
        {
            int neighborIndexCounter = 0;
            foreach (T n in current.GetNeighbors())
            {
                if (n.Equals(neighbor))
                    return neighborIndexCounter;
                neighborIndexCounter++;
            }
            // Debug.LogError("Unable to find neighbor Index!  current: " + current + "  neighbor: " + neighbor);
            return -1;
        }
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
        public float GetMoveCost(ITileCoordinate<T> coordT, int neighborIndex, float max = -1, bool bothdir = false)
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



        abstract public Vector3 GetModelSpacePosition(T coord);

        virtual public LineSegment GetModelSpaceEdge(T coord, int neighborIndex)
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