using System.Collections.Generic;
using UnityEngine;
using System;
namespace EyE.TestCoordTopol
{
    public interface ITileCoordinateBase
    {
        INeighborTopology neighborTopology { get; }
        ISpatialTopology spatialTopology { get; }
    }
    public interface ITileCoordinate<T> : IEquatable<T>, ITileCoordinateBase where T : ITileCoordinate<T>
    {

    }
    public interface INeighborTopology
    {
        /// <summary>
        /// Returns the number of neighboring tiles for this tile.
        /// </summary>
        /// <returns>The number of neighbors.</returns>
        public int NumberOfNeighbors();

        /// <summary>
        /// Returns the neighboring tile at the specified index.
        /// </summary>
        /// <param name="neighborIndex">The index of the neighbor.</param>
        /// <returns>The neighboring tile coordinate.</returns>
        public ITileCoordinateBase GetNeighborBase(ITileCoordinateBase coord,int neighborIndex);

        /// <summary>
        /// Calculates the heuristic distance from this tile to the specified end tile.
        /// Used in pathfinding to estimate the cost to the target.
        /// </summary>
        /// <param name="end">The target tile coordinate.</param>
        /// <returns>The heuristic distance.</returns>
        public float HeuristicDistanceTo(ITileCoordinateBase fromCoord, ITileCoordinateBase end);

        /// <summary>
        /// Finds the cost to move from a particular tile to its specified neighbor.
        /// </summary>
        /// <param name="fromCoord">The starting tile coordinate.</param>
        /// <param name="neighbor">The index of the neighboring tile.</param>
        /// <param name="max">The maximum allowable cost (optional).</param>
        /// <param name="bothdir">Whether the movement is bidirectional (optional).</param>
        /// <returns>The move cost. A value less than zero indicates impassable terrain.</returns>
        public float GetMoveCost(ITileCoordinateBase fromCoord, int neighbor, float max = -1, bool bothdir = false);


        /// <summary>
        /// enumerates through all valid coordinates in this topology
        /// </summary>
        public IEnumerable<ITileCoordinateBase> allValidCoords { get; }

        /// <summary>
        /// Checks if the specified tile coordinate is within the bounds of the map.
        /// </summary>
        /// <param name="coord">The tile coordinate to check.</param>
        /// <returns>True if the coordinate is within bounds; otherwise, false.</returns>
        public bool IsWithinBounds(ITileCoordinateBase coord);
    }
    public interface ISpatialTopology
    {

        /// <summary>
        /// Gets the modelspace position corresponding to the specified tile coordinate.
        /// This is used to determine where the tile is located in the model.
        /// </summary>
        /// <param name="coord">The tile coordinate for which to get the world position.</param>
        /// <returns>The world position as a Vector3.</returns>
        public Vector3 GetModelSpacePosition(ITileCoordinateBase coord);


        /// <summary>
        /// Gets the modelspace orientation or the tile corresponding to the specified tile coordinate.
        /// </summary>
        /// <param name="coord">The tile coordinate for which to get the normal.</param>
        /// <returns>The world position as a Vector3.</returns>
        public Quaternion GetModelSpaceTileOrientation(ITileCoordinateBase coord);

        /// <summary>
        /// Get the coordinate at/closest to a given world position
        /// </summary>
        /// <param name="pos">would position</param>
        /// <returns>return the closest coordinate to the given position, or possibly a unique "invalid coordinate" value- depending on T</returns>
        public ITileCoordinateBase GetCoordinate(Vector3 pos);


        /// <summary>
        /// Retrieves the orientation of the border between the specified tile coordinate and its neighbor.
        /// This information can be used for rendering purposes, such as aligning graphics.
        /// </summary>
        /// <param name="coord">The tile coordinate for which to get the neighbor's border orientation.</param>
        /// <param name="neighborIndex">The index of the neighboring tile.</param>
        /// <returns>The orientation as a Quaternion.</returns>
        public Quaternion NeighborBorderOrientation(ITileCoordinateBase coord, int neighborIndex);
    }
}
namespace EyE.Maps
{
    //test creation of base interface
    public interface ITileCoordinateBase {
        /// <summary>
        /// Returns the number of neighboring tiles for this tile.
        /// </summary>
        /// <returns>The number of neighbors.</returns>
        public int NumberOfNeighbors();

        /// <summary>
        /// Returns the neighboring tile at the specified index.
        /// </summary>
        /// <param name="neighborIndex">The index of the neighbor.</param>
        /// <returns>The neighboring tile coordinate.</returns>
        public ITileCoordinateBase GetSpatialNeighborBase(int neighborIndex);



    }
    public static class ITileCoordinateExtensions
    {
        /// <summary>
        /// Returns the neighboring tile at the specified index.
        /// </summary>
        /// <param name="neighborIndex">The index of the neighbor.</param>
        /// <returns>The neighboring tile coordinate.</returns>
        static public ITileCoordinateBase GetPathNeighborBase(this ITileCoordinateBase coord, int neighborIndex, IReadOnlyDictionary<ITileCoordinateBase, ITileCoordinateBase> teleporters)
        {
            ITileCoordinateBase spatialNeighbor = coord.GetSpatialNeighborBase(neighborIndex);
            if (teleporters == null) return spatialNeighbor;
            if (teleporters.TryGetValue(spatialNeighbor, out ITileCoordinateBase teleportDestination))
                return teleportDestination;
            return spatialNeighbor;
        }

        /// <summary>
        /// Returns the neighboring tile at the specified index.  Similar to the base interface version, but of a specific coordinate type
        /// </summary>
        /// <param name="neighborIndex">The index of the neighbor.</param>
        /// <returns>The neighboring tile coordinate.</returns>
        static public T GetPathNeighbor<T>(this ITileCoordinate<T> coord, int neighborIndex, IReadOnlyDictionary<T, T> teleporters) where T : ITileCoordinate<T>
        {
            T spatialNeighbor = coord.GetSpatialNeighbor(neighborIndex);
            if (teleporters == null) return spatialNeighbor;
            if (teleporters.TryGetValue((T)coord, out T teleportDestination))//is coord a teleporter source?
                return teleportDestination.GetSpatialNeighbor(neighborIndex);//, teleporters);
            if (teleporters.TryGetValue(spatialNeighbor, out teleportDestination))
                return teleportDestination;
            return spatialNeighbor;
        }

        /// <summary>
        /// Returns an array of neighboring tile coordinates.
        /// </summary>
        /// <returns>An array of neighboring tile coordinates.</returns>
        static public T[] GetPathNeighbors<T>(this ITileCoordinate<T> coord, IReadOnlyDictionary<T, T> teleporters) where T : ITileCoordinate<T>
        {
            T[] spatialNeighbors = coord.GetSpatialNeighbors();
            if (teleporters == null) return spatialNeighbors;
            T[] pathNeighbors = new T[spatialNeighbors.Length];
            for (int i = 0; i < spatialNeighbors.Length; i++)
            {
                if (teleporters.TryGetValue(spatialNeighbors[i], out T teleportDestination))
                    pathNeighbors[i]=teleportDestination;
                pathNeighbors[i] = spatialNeighbors[i]; ;
            }
            return pathNeighbors;
        }

    }

    /// <summary>
    /// Represents a tile coordinate with methods for neighbor retrieval and heuristic distance calculation.
    /// </summary>
    /// <typeparam name="T">The type that implements the TileCoordinate interface.</typeparam>
    public interface ITileCoordinate<T> : IEquatable<T>, ITileCoordinateBase where T : ITileCoordinate<T>
    {
        /// <summary>
        /// The current value of the tile coordinate.
        /// </summary>
        public T value { get; }

        /// <summary>
        /// Returns an array of neighboring tile coordinates.
        /// </summary>
        /// <returns>An array of neighboring tile coordinates.</returns>
        public T[] GetSpatialNeighbors();




        /// <summary>
        /// Returns the neighboring tile at the specified index.  Similar to the base interface version, but of a specific coordinate type
        /// </summary>
        /// <param name="neighborIndex">The index of the neighbor.</param>
        /// <returns>The neighboring tile coordinate.</returns>
        public T GetSpatialNeighbor(int neighborIndex);

        /// <summary>
        /// Calculates the heuristic distance from this tile to the specified end tile.
        /// Used in pathfinding to estimate the cost to the target.
        /// </summary>
        /// <param name="end">The target tile coordinate.</param>
        /// <returns>The heuristic distance.</returns>
        public float HeuristicDistanceTo(ITileCoordinate<T> end);

        
    }

    /// <summary>
    /// Represents a map of tile coordinates and provides methods for move cost calculation.
    /// </summary>
    /// <typeparam name="T">The type that implements the TileCoordinate interface.</typeparam>
    public interface IMap<T> where T : ITileCoordinate<T>
    {
        /// <summary>
        /// Finds the cost to move from a particular tile to its specified neighbor.
        /// </summary>
        /// <param name="coord">The starting tile coordinate.</param>
        /// <param name="neighbor">The index of the neighboring tile.</param>
        /// <param name="max">The maximum allowable cost (optional).</param>
        /// <param name="bothdir">Whether the movement is bidirectional (optional).</param>
        /// <returns>The move cost. A value less than zero indicates impassable terrain.</returns>
        public float GetMoveCost(ITileCoordinate<T> coord, int neighbor, float max = -1, bool bothdir = false);

        /// <summary>
        /// Represents the size of the map.
        /// </summary>
        public T size { get; }

        /// <summary>
        /// Represents the world scale factor for the map.
        /// </summary>
        public float worldScale { get; }

        public IEnumerable<T> allMapCoords { get; }


        //  public Vector3 GetWorldPosition(T coord);
    }

    /// <summary>
    /// An extension of IMap that specifies functions that provide information on how the map should be drawn.
    /// </summary>
    /// <typeparam name="T">The type that implements the TileCoordinate interface.</typeparam>
    public interface IMapDrawable<T>:IMap<T> where T : ITileCoordinate<T>
    {
        /// <summary>
        /// Checks if the specified tile coordinate is within the bounds of the map.
        /// </summary>
        /// <param name="coord">The tile coordinate to check.</param>
        /// <returns>True if the coordinate is within bounds; otherwise, false.</returns>
        public bool IsWithinBounds(T coord);

        /// <summary>
        /// Gets the world position corresponding to the specified tile coordinate.
        /// This is used to determine where the tile is located in the game world.
        /// </summary>
        /// <param name="coord">The tile coordinate for which to get the world position.</param>
        /// <returns>The world position as a Vector3.</returns>
        public Vector3 GetModelSpacePosition(T coord);

        public Quaternion GetModelSpaceOrientation(T coord);

        /// <summary>
        /// Get the coordinate at/closest to a given world position
        /// </summary>
        /// <param name="pos">would position</param>
        /// <returns>return the closest coordinate to the given position, or possibly a unique "invalid coordinate" value- depending on T</returns>
        public T GetCoordinate(Vector3 pos);


        /// <summary>
        /// Retrieves the orientation of the border between the specified tile coordinate and its neighbor.
        /// This information can be used for rendering purposes, such as aligning graphics.
        /// </summary>
        /// <param name="coord">The tile coordinate for which to get the neighbor's border orientation.</param>
        /// <param name="neighborIndex">The index of the neighboring tile.</param>
        /// <returns>The orientation as a Quaternion.</returns>
        public Quaternion NeighborBorderOrientation(T coord, int neighborIndex);
    }
}