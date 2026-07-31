using System;
using System.Collections.Generic;
using UnityEngine;

namespace EyE.Maps
{
    /// <summary>
    /// Finds a path through an ordered sequence of destinations,
    /// without ever reusing a tile.
    ///
    /// Internally this uses an A*-style search over an expanded state space:
    ///
    ///     (currentCoordinate, destinationIndex, visitedPath)
    ///
    /// Unlike normal A*, multiple states may exist at the same coordinate,
    /// because their prior visited tiles differ.
    ///
    /// This is required to properly solve non-overlapping waypoint routing.
    /// </summary>
    /*
    public class TileOrderedPathFinder<T>
        where T : ITileCoordinate<T>
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orderedDestinations"></param>
        /// <param name="validRegion"></param>
        /// <param name="teleporters"></param>
        /// <param name="uniquePath"></param>
        /// <param name="loopLimit"></param>
        /// <returns></returns>
        public static List<T> GetPaths(IList<T> orderedDestinations, HashSet<T> validRegion, Templates.TeleporterCollection teleporters, bool uniquePath = true, int loopLimit = 200000)
        {

            List<TileOnPath<T>> segments = new List<TileOnPath<T>>();
            List<HashSet<T>> segementsUnordered = new List<HashSet<T>>();

            List<T> ToCoordinateList(TileOnPath<T> path)
            {
                List<T> retVal = new List<T>();
                foreach (TileOnPath<T> tile in path.StartToEnd())
                    retVal.Add((T)tile.coordinate);
                return retVal;
            }

            TileOnPath<T> BuildSegment(int segmentIndex, HashSet<T> forbiddenTiles)
            {
                float MoveCost(ITileCoordinate<T> coor, int neighborIndex)
                {
                    T neighbor = coor.GetSpatialNeighbor(neighborIndex);

                    if (!validRegion.Contains(neighbor))
                        return -1;

                    if (forbiddenTiles.Contains(neighbor))
                        return -1;

                    return 1;
                }

                return TileAStarPathFinder<T>.GetPathFromTo(
                    orderedDestinations[segmentIndex],
                    orderedDestinations[segmentIndex + 1],
                    MoveCost,
                    teleporters);
            }

            // checks if the two segments share any verts other than start/end verts.  specifically excludes start and end point of the segement
            bool HasOverlap(HashSet<T> a, HashSet<T> b, HashSet<T> allowedShared)
            {
                HashSet<T> overlap = new HashSet<T>();

                foreach (T tile in a)
                {
                    if (allowedShared.Contains(tile))
                        continue;

                    if (b.Contains(tile))
                        return true;
                }

                return false;
            }
            // creates set of all used T's, except those in this segment.  specifically excludes start and end point of the segement
            HashSet<T> BuildForbiddenSet(int segmentToRebuild)
            {
                HashSet<T> forbidden = new HashSet<T>();

                for (int i = 0; i < segementsUnordered.Count; i++)
                {
                    if (i == segmentToRebuild)
                        continue;
                    
                    foreach (T tile in segementsUnordered[i])
                        forbidden.Add(tile);
                }

                forbidden.Remove(orderedDestinations[segmentToRebuild]);
                forbidden.Remove(orderedDestinations[segmentToRebuild + 1]);

                return forbidden;
            }

            HashSet<T> unorderedDestinations = new HashSet<T>(orderedDestinations);

            for (int i = 0; i < orderedDestinations.Count - 1; i++)
            {
                TileOnPath<T> pathSegment = BuildSegment(i, new HashSet<T>());

                if (pathSegment == null)
                {
                    Debug.LogError(
                        "Unable to find path for segment[" + i + "] between coords [" +
                        orderedDestinations[i] + "] and [" +
                        orderedDestinations[i + 1] + "]");

                    return null;
                }

                segments.Add(pathSegment);
                segementsUnordered.Add(new HashSet<T>(ToCoordinateList(pathSegment)));
            }

            if (uniquePath)
            {
                //loop - not forever
                for (int iteration = 0; iteration < loopLimit; iteration++)
                {
                    bool changed = false;
                    int[] overlapsPerSegment = new int[segments.Count];
                    for (int a = 0; a < segments.Count; a++)
                    {
                        for (int b = a + 1; b < segments.Count; b++)
                        {
                            if (HasOverlap(segementsUnordered[a], segementsUnordered[b], unorderedDestinations))
                            {
                                overlapsPerSegment[a]++;
                                overlapsPerSegment[b]++;
                            }
                        }
                    }

                    bool failedAtLeastOne = false;
                    for (int a = 0; a < segments.Count; a++)
                    {
                        for (int b = a + 1; b < segments.Count; b++)
                        {
                            if (!HasOverlap(segementsUnordered[a], segementsUnordered[b], unorderedDestinations))
                                continue;

                            int segementToRecompute = b;
                            int otherSegment = a;
                            if (overlapsPerSegment[a] > overlapsPerSegment[b])
                            {
                                segementToRecompute = a;
                                otherSegment = b;
                            }

                            //try redo of segementToRecompute first
                            HashSet<T> forbiddenTiles = BuildForbiddenSet(segementToRecompute);
                            TileOnPath<T> rebuilt = BuildSegment(a, forbiddenTiles);
                            if (rebuilt == null)//failed to rebuild segement with these forbidden tiles
                            {
                                //try other segement
                                segementToRecompute = otherSegment;
                                forbiddenTiles = BuildForbiddenSet(segementToRecompute);
                                rebuilt = BuildSegment(a, forbiddenTiles);

                            }
                            if (rebuilt == null)
                            {
                                //Debug.LogError("Unable to redraw either overlapping segements [" + a + "] or [" + b + "]");
                                failedAtLeastOne = true;
                            }
                            else
                            {
                                //sucessfully rebuilt segment
                                //Debug.Log("Rebuilt segement["+segementToRecompute+"] \n old route: " + string.Join(",", segments[segementToRecompute]) + "  \n to new route: " + string.Join(",", rebuilt));
                                segments[segementToRecompute] = rebuilt;
                                segementsUnordered[segementToRecompute] = new HashSet<T>(ToCoordinateList(rebuilt));
                            }
                            changed = true;
                        }
                    }
                    if (!changed && !failedAtLeastOne)
                        break;
                }
            }

            List<T> fullPath = new List<T>();

            for (int i = 0; i < segments.Count; i++)
            {
                List<T> segmentPath = ToCoordinateList(segments[i]);

                if (i > 0)
                    segmentPath.RemoveAt(0);

                fullPath.AddRange(segmentPath);
            }
        //    Debug.Log("Full path for region generatred: " + string.Join(",", fullPath));
            return fullPath;
        }
        public static List<T> GetPathsOLD(IList<T> orderedDestinations,HashSet<T> validRegion,Templates.TeleporterCollection teleporters, bool uniquePath=true, int loopLimit = 200000)
        {
            List<T> ToCoordinateList(TileOnPath<T> path)
            {
                List<T> retVal = new List<T>();
                foreach (TileOnPath<T> tile in path.StartToEnd())
                    retVal.Add((T)tile.coordinate);
                return retVal;
            }

            float MoveCost(ITileCoordinate<T> coor, int neighborIndex)
            {
                T neighborCoord= coor.GetSpatialNeighbor(neighborIndex);
                if (!validRegion.Contains(neighborCoord)) return -1;
                return 1;
            }

            List<TileOnPath<T>> segments= new List<TileOnPath<T>>();
            List<HashSet<T>> segementsUnordered = new List<HashSet<T>>();
            List<T> fullPath = new List<T>();
            for (int i = 0; i < orderedDestinations.Count - 1; i++)
            {
                TileOnPath<T>  pathSegement = TileAStarPathFinder<T>.GetPathFromTo(orderedDestinations[i], orderedDestinations[i + 1], MoveCost, teleporters);
                if (pathSegement == null)
                    Debug.LogError("Unable to find any path for segement["+i+"] between coords ["+ orderedDestinations[i] + "] and [" + orderedDestinations[i+1] + "]   valid region: "+ string.Join("], [",validRegion));
                segments.Add(pathSegement);
                List<T> pathSegementAsList = ToCoordinateList(pathSegement);
                fullPath.AddRange(pathSegementAsList);
                segementsUnordered.Add( new HashSet<T>(pathSegementAsList) );
            }
            if (!uniquePath) return fullPath;

            //fill in the rest.. ensure no coordinates are shared between segements

            return fullPath;
        }
        public delegate float MoveCost(ITileCoordinate<T> coord, int neighborIndex);

        private class SearchNode : Priority_Queue.PriorityHeapNodeBase
        {
            public ITileCoordinate<T> Coordinate;
            public SearchNode Previous;

            public float StepCost;
            public float CostSoFar;

            public int DestinationIndex;

            public override double Priority
            {
                get
                {
                    if (DestinationIndex >= targetCoordinates.Count)
                        return CostSoFar;

                    return CostSoFar +
                           Coordinate.HeuristicDistanceTo(
                               targetCoordinates[DestinationIndex]);
                }
                set { }
            }

            public SearchNode(
                ITileCoordinate<T> coordinate,
                float stepCost,
                SearchNode previous,
                int destinationIndex)
            {
                Coordinate = coordinate;
                Previous = previous;

                StepCost = stepCost;
                DestinationIndex = destinationIndex;

                if (previous == null)
                    CostSoFar = stepCost;
                else
                    CostSoFar = previous.CostSoFar + stepCost;
            }

            public bool ContainsCoordinate(
                ITileCoordinate<T> coordinate)
            {
                SearchNode current = this;

                while (current != null)
                {
                    if (current.Coordinate.Equals(coordinate))
                        return true;

                    current = current.Previous;
                }

                return false;
            }
        }

        private struct StateKey : IEquatable<StateKey>
        {
            public ITileCoordinate<T> Coordinate;
            public int DestinationIndex;
            public int PathHash;

            public bool Equals(StateKey other)
            {
                return Coordinate.Equals(other.Coordinate)
                    && DestinationIndex == other.DestinationIndex
                    && PathHash == other.PathHash;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is StateKey))
                    return false;

                return Equals((StateKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;

                    hash = hash * 31 + Coordinate.GetHashCode();
                    hash = hash * 31 + DestinationIndex;
                    hash = hash * 31 + PathHash;

                    return hash;
                }
            }
        }

        private static readonly Priority_Queue.PriorityHeap<SearchNode>
            staticPriorityQueue =
                new Priority_Queue.PriorityHeap<SearchNode>(10000);

        private static List<ITileCoordinate<T>> targetCoordinates =
            new List<ITileCoordinate<T>>();

        public static TileOnPath<T> GetPathOnMap(
            IMap<T> map,
            T startCoordinate,
            IList<T> orderedDestinations,
            Templates.TeleporterCollection teleporters,
            float maxSlope = -1,
            bool bothDir = false,
            int loopLimit = 200000)
        {

            return GetPath(
                startCoordinate,
                orderedDestinations,
                (ITileCoordinate<T> a, int n) =>
                {
                    return map.GetMoveCost(
                        a,
                        n,
                        maxSlope,
                        bothDir);
                },
                teleporters,
                loopLimit);
        }

        public static TileOnPath<T> GetPath(
            T startCoordinate,
            IList<T> orderedDestinations,
            MoveCost getMoveCost,
            Templates.TeleporterCollection teleporters,
            int loopLimit = 200000)
        {
            if (orderedDestinations == null)
                throw new ArgumentNullException(nameof(orderedDestinations));

            if (orderedDestinations.Count == 0)
                return null;

            targetCoordinates.Clear();

            for (int i = 0; i < orderedDestinations.Count; i++)
                targetCoordinates.Add(orderedDestinations[i]);

            Priority_Queue.PriorityHeap<SearchNode> frontier =
                staticPriorityQueue;

            frontier.Clear();

            Dictionary<StateKey, float> bestCostByState =
                new Dictionary<StateKey, float>();

            int initialDestinationIndex = 0;

            if (startCoordinate.Equals(
                orderedDestinations[0]))
            {
                initialDestinationIndex = 1;

                if (initialDestinationIndex >=
                    orderedDestinations.Count)
                {
                    return new TileOnPath<T>(
                        startCoordinate,
                        0,
                        null);
                }
            }

            SearchNode startNode =
                new SearchNode(
                    startCoordinate,
                    0,
                    null,
                    initialDestinationIndex);

            frontier.Enqueue(startNode);

            int loopCounter = 0;

            while (frontier.Count > 0)
            {
                loopCounter++;

                if (loopCounter > loopLimit)
                {
                    Debug.Log(
                        "Ordered path search loop limit exceeded.");

                    return null;
                }

                SearchNode current =
                    frontier.Dequeue();

                if (current.DestinationIndex >=
                    orderedDestinations.Count)
                {
                    return ConvertToTilePath(current);
                }

                int neighborCount =
                    current.Coordinate.NumberOfNeighbors();

                for (int i = 0; i < neighborCount; i++)
                {
                    ITileCoordinate<T> neighbor =
                        current.Coordinate.GetPathNeighbor(
                            i,
                            teleporters);

                    float moveCost =
                        getMoveCost(current.Coordinate, i);

                    if (moveCost <= 0)
                        continue;

                    if (current.ContainsCoordinate(neighbor))
                        continue;

                    int nextDestinationIndex =
                        current.DestinationIndex;

                    if (neighbor.Equals(
                        orderedDestinations[nextDestinationIndex]))
                    {
                        nextDestinationIndex++;
                    }

                    SearchNode nextNode =
                        new SearchNode(
                            neighbor,
                            moveCost,
                            current,
                            nextDestinationIndex);

                    StateKey stateKey =
                        new StateKey()
                        {
                            Coordinate = neighbor,
                            DestinationIndex =
                                nextDestinationIndex,

                            PathHash =
                                ComputePathHash(nextNode)
                        };

                    float bestKnownCost;

                    if (bestCostByState.TryGetValue(
                        stateKey,
                        out bestKnownCost))
                    {
                        if (bestKnownCost <=
                            nextNode.CostSoFar)
                        {
                            continue;
                        }

                        bestCostByState[stateKey] =
                            nextNode.CostSoFar;
                    }
                    else
                    {
                        bestCostByState.Add(
                            stateKey,
                            nextNode.CostSoFar);
                    }

                    frontier.Enqueue(nextNode);
                }
            }

            Debug.Log(
                "Ordered non-overlapping path not found.");

            return null;
        }

        private static int ComputePathHash(
            SearchNode node)
        {
            unchecked
            {
                int hash = 17;

                SearchNode current = node;

                while (current != null)
                {
                    hash =
                        hash * 31 +
                        current.Coordinate.GetHashCode();

                    current = current.Previous;
                }

                return hash;
            }
        }

        private static TileOnPath<T> ConvertToTilePath(
            SearchNode node)
        {
            Stack<SearchNode> reverse =
                new Stack<SearchNode>();

            SearchNode current = node;

            while (current != null)
            {
                reverse.Push(current);
                current = current.Previous;
            }

            TileOnPath<T> previous = null;

            while (reverse.Count > 0)
            {
                SearchNode step = reverse.Pop();

                TileOnPath<T> tile =
                    new TileOnPath<T>(
                        step.Coordinate,
                        step.StepCost,
                        previous);

                previous = tile;
            }

            return previous;
        }
    }
    */
}