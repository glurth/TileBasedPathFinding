using UnityEngine;
using System.Collections.Generic;

namespace Eye.Maps.Templates
{
    //this version has double-sided walls (since there may be an odd number of neighbors- we can't easily do single walls.
    abstract public class GenericMazeMap<T> : IMap<T>, IMapDrawable<T> where T : ITileCoordinate<T>
    {
        private T _size;

        private Dictionary<T, bool[]> walls = new Dictionary<T, bool[]>();
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

            foreach (ITileCoordinate<T> tileCoord in allMapCoords)
            {
                bool[] wallsArray = new bool[numWallDimensions];
                for (int i = 0; i < numWallDimensions; i++)
                    wallsArray[i] = true;
                walls.Add(tileCoord.value, wallsArray);
                visited.Add(tileCoord.value, false);
            }
            // Generate the maze
            GenerateMaze();
        }

        private void GenerateMaze()
        {
            //start = new Vector2Int(0, 0);  // Example start
            //end = new Vector2Int(_size.x - 1, _size.y - 1);  // Example end
            List<T> mainPath = GenerateMainPath(start, end);
            GenerateBranches(mainPath);
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


        private void RemoveWall(T current, T next)
        {
            int GetNeighborIndexOf(T current, T neighbor)
            {
                int neighborIndexCounter = 0;
                foreach (T n in current.GetNeighbors())
                {
                    if (n.Equals(neighbor))
                        return neighborIndexCounter;
                    neighborIndexCounter++;
                }
                Debug.LogError("Unable to find neighbor Index!  current: " + current + "  neighbor: " + neighbor);
                return -1;
            }


            int nieghborIndex = GetNeighborIndexOf(current, next);
            int reverseNeighborIndex = GetNeighborIndexOf(next, current);
            walls[current][nieghborIndex] = false;
            walls[next][reverseNeighborIndex] = false;
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



        abstract public Vector3 GetWorldPosition(T coord);

        // Check if a given coordinate is within the bounds of the maze
        abstract public bool IsWithinBounds(T coord);

        abstract public Quaternion NeighborBorderOrientation(T coord, int neighborIndex);
    }
}