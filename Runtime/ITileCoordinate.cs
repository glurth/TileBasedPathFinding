using System.Collections.Generic;
using UnityEngine;
using System;

namespace Eye.Maps
{
    /// <summary>
    /// Represents a tile coordinate with methods for neighbor retrieval and heuristic distance calculation.
    /// </summary>
    /// <typeparam name="T">The type that implements the TileCoordinate interface.</typeparam>
    public interface ITileCoordinate<T> : IEquatable<T> where T : ITileCoordinate<T>
    {
        /// <summary>
        /// The current value of the tile coordinate.
        /// </summary>
        public T value { get; }

        /// <summary>
        /// Returns the number of neighboring tiles for this tile.
        /// </summary>
        /// <returns>The number of neighbors.</returns>
        public int NumberOfNeighbors();

        /// <summary>
        /// Returns an array of neighboring tile coordinates.
        /// </summary>
        /// <returns>An array of neighboring tile coordinates.</returns>
        public T[] GetNeighbors();

        /// <summary>
        /// Returns the neighboring tile at the specified index.
        /// </summary>
        /// <param name="neighborIndex">The index of the neighbor.</param>
        /// <returns>The neighboring tile coordinate.</returns>
        public T GetNeighbor(int neighborIndex);

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
        public Vector3 GetWorldPosition(T coord);

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