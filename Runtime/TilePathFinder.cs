using System.Collections.Generic;
using UnityEngine;
using System;

namespace Eye.Maps
{
    /// <summary>
    /// Represents a tile on a path, used in pathfinding algorithms like A*.
    /// Stores the tile's coordinate, its cost, and the previous step in the path.
    /// </summary>
    /// <typeparam name="T">The type that implements the TileCoordinate interface.</typeparam>
        //  [Serializable]
    public class TileOnPath<T> : Priority_Queue.PriorityHeapNodeBase, IComparable, IComparer<TileOnPath<T>> where T : ITileCoordinate<T>
    {
        /// <summary>
        /// The current tile's coordinate.
        /// </summary>
        public ITileCoordinate<T> coordinate;

        /// <summary>
        /// The previous step in the path, leading to this tile.
        /// </summary>
        public TileOnPath<T> previousStep;

        /// <summary>
        /// The movement cost of this tile.
        /// </summary>
        public float tileCost;

        /// <summary>
        /// The accumulated cost from the start to the previous step.
        /// </summary>
        float costToPreviousStep = 0;

        /// <summary>
        /// Initializes a new instance of the TileOnPath class with the specified coordinate, move cost, and previous step.
        /// </summary>
        /// <param name="coordinate">The current tile coordinate.</param>
        /// <param name="moveCost">The move cost to this tile.</param>
        /// <param name="previousStep">The previous step in the path.</param>
        public TileOnPath(ITileCoordinate<T> coordinate, float moveCost, TileOnPath<T> previousStep)
        {
            this.coordinate = coordinate;
            this.previousStep = previousStep;
            tileCost = moveCost;
            if (previousStep != null)
                costToPreviousStep = previousStep.GetTotalCost();
        }

        /// <summary>
        /// Compares this TileOnPath instance with another object, used for priority queue sorting.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>An integer indicating the comparison result.</returns>
        public int CompareTo(object obj)
        {
            if (obj == null) return 1;

            TileOnPath<T> other = obj as TileOnPath<T>;
            if (other != null)
                return this.Compare(this, other);
            else
                throw new ArgumentException("Object is not a TileOnPath");
        }

        /// <summary>
        /// Compares two TileOnPath instances based on their total cost.
        /// </summary>
        /// <param name="x">The first TileOnPath instance.</param>
        /// <param name="y">The second TileOnPath instance.</param>
        /// <returns>An integer indicating the comparison result.</returns>
        public int Compare(TileOnPath<T> x, TileOnPath<T> y)
        {
            float xCost = x.GetTotalCost();
            float yCost = y.GetTotalCost();
            if (xCost < yCost) return -1;
            if (xCost == yCost) return 0;
            return 1;
        }

        /// <summary>
        /// Calculates the total cost to reach this tile, including the cost from previous steps.
        /// </summary>
        /// <returns>The total cost to this tile.</returns>
        public float GetTotalCost()
        {
            if (previousStep == null)
                return tileCost;
            return tileCost + costToPreviousStep;
        }

        /// <summary>
        /// Recomputes the costs of all tiles in the path.
        /// </summary>
        public void RecomputeCosts()
        {
            float sum = 0;
            foreach (TileOnPath<T> tile in StartToEnd())
            {
                tile.costToPreviousStep = sum;
                sum += tile.tileCost;
            }
        }
        /// <summary>
        /// The target tile coordinate for pathfinding.
        /// </summary>
        static public ITileCoordinate<T> targetTileCoord;  //consider moving inside TileCoordinate<T> as member-  bigger but can handle finding multiple paths at once

        /// <summary>
        /// Gets the priority of the tile based on total cost of path so far and heuristic distance to target.
        /// </summary>
        public override double Priority
        {
            get => GetTotalCost() + coordinate.HeuristicDistanceTo(targetTileCoord);
            set { }
        }

        /// <summary>
        /// Enumerates the path from the end to the start.
        /// </summary>
        /// <returns>An enumerable of tiles from the end to the start.</returns>
        public IEnumerable<TileOnPath<T>> EndToStart()
        {
            TileOnPath<T> currentStep = this;
            do
            {
                yield return currentStep;
                currentStep = currentStep.previousStep;
            } while (currentStep != null);
        }

        /// <summary>
        /// Enumerates the path from the start to the end. NOTE: less performant that the reverse.
        /// </summary>
        /// <returns>An enumerable of tiles from the start to the end.</returns>
        public IEnumerable<TileOnPath<T>> StartToEnd()
        {
            Stack<TileOnPath<T>> reverseStack = new Stack<TileOnPath<T>>();
            TileOnPath<T> currentStep = this;
            while (currentStep != null)
            {
                reverseStack.Push(currentStep);
                currentStep = currentStep.previousStep;
            }
            while (reverseStack.Count > 0)
            {
                yield return reverseStack.Pop();
            }
        }
        /// <summary>
        /// Prepends a new path to the current path. Recomputes moves costs.
        /// </summary>
        /// <param name="newPath">The new path to prepend.</param>
        public void Prepend(TileOnPath<T> newPath)
        {
            TileOnPath<T> pathStart = this;
            while (pathStart.previousStep != null)
                pathStart = pathStart.previousStep;
            pathStart.previousStep = newPath;
            RecomputeCosts();
        }
        /// <summary>
        /// Creates a new distinct copy of the current path.
        /// </summary>
        /// <returns>A new TileOnPath representing the copied path.</returns>
        public TileOnPath<T> CopyPath()
        {
            TileOnPath<T> previous = null;
            TileOnPath<T> copy = null;
            foreach (TileOnPath<T> tile in StartToEnd())
            {
                copy = new TileOnPath<T>(tile.coordinate, tile.tileCost, previous);
                previous = copy;
            }
            return copy;

        }
    }
    
    /// <summary>
    /// A class to find paths using the A* algorithm.
    /// Two version of the function are available.  When using a map that Implements IMap<typeparamref name="T"/> call the  GetPathFromTo that takes it as a aparameter.  The IMap will provide the functionality to compute the move cost.
    /// If not using an IMap, or if a custom cost calculation is desired, use the version that takes a MoveCost delegate as a parameter.
    /// </summary>
    /// <typeparam name="T">Type of the tile coordinate.</typeparam>
    public class TileAStarPathFinder<T> where T : ITileCoordinate<T>
    {
        //this delegate defines the signature that a move cost function must have
        public delegate float MoveCost(ITileCoordinate<T> coord, int neighborIndex);

        static Priority_Queue.PriorityHeap<TileOnPath<T>> staticPriorityQueue = new Priority_Queue.PriorityHeap<TileOnPath<T>>(1000);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="map"></param>
        /// <param name="startCoordinate"></param>
        /// <param name="endCoordinate"></param>
        /// <param name="maxSlope"></param>
        /// <param name="bothDir"></param>
        /// <returns>return null if no path can be found</returns>
        static public TileOnPath<T> GetPathFromTo(IMap<T> map, ITileCoordinate<T> startCoordinate, ITileCoordinate<T> endCoordinate, float maxSlope = -1, bool bothDir = false, int loopLimit = 10000)
        {
            return TileAStarPathFinder<T>.GetPathFromTo(
                startCoordinate
                , endCoordinate
                , (ITileCoordinate<T> a, int n) => { return map.GetMoveCost(a, n, maxSlope, bothDir); }
                , loopLimit);
        }


        static public TileOnPath<T> GetPathFromTo(ITileCoordinate<T> startCoordinate, ITileCoordinate<T> endCoordinate, MoveCost GetMoveCost, int loopLimit = 10000)
        {

            //GetMoveCostFrom(HexIndex2D loc, int direction);


            System.Diagnostics.Stopwatch findTimer = new System.Diagnostics.Stopwatch();
            findTimer.Start();
            TileOnPath<T>.targetTileCoord = endCoordinate;// startCoordinate;
                                                          //int loopLimit = 1800000;
            int loopCounter = 0;

            Dictionary<ITileCoordinate<T>, TileOnPath<T>> tileByCoordinateForRemoval = new Dictionary<ITileCoordinate<T>, TileOnPath<T>>();

            Priority_Queue.PriorityHeap<TileOnPath<T>> frontier = staticPriorityQueue;// new Priority_Queue.PriorityHeap<TileOnPath>(1000000);
            frontier.Clear();
            frontier.Enqueue(new TileOnPath<T>(startCoordinate, 0, null));

            while (frontier.Count > 0)
            {
                if (loopLimit-- < 0)
                {
                    Debug.Log("FindPath loop limit exceeded");
                    return null;
                }

                TileOnPath<T> currentTile = frontier.Dequeue();

                if (currentTile.coordinate.Equals(endCoordinate))
                {
                    findTimer.Stop();
                    //  Debug.Log("route found- loop count: " + loopCounter.ToString() + " timer(ms): " + findTimer.ElapsedMilliseconds);
                    return currentTile;// user can recurse though returned tile's previousStep to find path
                }


                int numNeighbors = currentTile.coordinate.NumberOfNeighbors();
                //  Debug.Log("    current Tile: " + currentTile.coordinate.ToString());
                for (int i = 0; i < numNeighbors; i++)
                {
                    loopCounter++;
                    ITileCoordinate<T> neighborCoord = currentTile.coordinate.GetNeighbor(i);
                    //   Debug.Log("    neighbor " + i + ": " + neighborCoord.ToString());
                    float moveCost = GetMoveCost(currentTile.coordinate, i);

                    if (moveCost > 0)
                    {
                        TileOnPath<T> newNeighborTile = new TileOnPath<T>(neighborCoord, moveCost, currentTile);
                        //check if path to new tile already exists on frontier
                        TileOnPath<T> otherPathToNeighbor;
                        if (!tileByCoordinateForRemoval.TryGetValue(neighborCoord, out otherPathToNeighbor))// == null)
                        {
                            frontier.Enqueue(newNeighborTile);
                            tileByCoordinateForRemoval.Add(neighborCoord, newNeighborTile);
                        }
                        else
                        {
                            if (otherPathToNeighbor.GetTotalCost() > newNeighborTile.GetTotalCost())
                            {
                                frontier.Remove(otherPathToNeighbor);
                                tileByCoordinateForRemoval.Remove(neighborCoord);


                                frontier.Enqueue(newNeighborTile);
                                tileByCoordinateForRemoval.Add(neighborCoord, newNeighborTile);
                            }
                        }

                    }
                    
                }// end loop current tile neighbors

            }//  end while frontier contains any elements
            findTimer.Stop();
            Debug.Log("route NOT found- loop count: " + loopCounter.ToString() + " timer(ms): " + findTimer.ElapsedMilliseconds);
            return null;


        }

    }

}