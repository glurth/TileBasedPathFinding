# Pathfinding Library for Unity

This repository provides a generic pathfinding library for Unity, designed to work with any shape of tile-based maps. The library includes interfaces for defining tile coordinates and maps, as well as a static class that implements the A* algorithm for pathfinding. The main components are the `Eye.Maps` namespace interfaces and pathfinding classes.

## Project Structure

The repository contains the following main files:

### 1. `ITileCoordinate.cs`

This file defines interfaces for representing tile coordinates and maps:

- **ITileCoordinate<T>**
  - Represents a tile coordinate in a map.
  - Includes methods for retrieving neighboring tiles and calculating heuristic distances.
  - Generic type parameter `T` represents the type that implements the `ITileCoordinate<T>` interface.

- **IMap<T>**
  - Defines a map of tile coordinates, providing methods for calculating movement costs.
  - Supports features such as bidirectional movement and maximum allowable movement costs.
  - Generic type parameter `T` represents the type that implements the `ITileCoordinate<T>` interface.

- **IMapDrawable<T>**
  - Extends the `IMap<T>` interface, adding methods for visual representation.
  - Provides functions for checking map boundaries, retrieving world positions of tiles, and determining border orientations for rendering.

### 2. `TilePathFinder.cs`

This file contains the implementation of the A* pathfinding algorithm:

- **TileOnPath<T>**
  - Represents a tile in the pathfinding algorithm, storing its coordinates, movement cost, and the previous tile in the path.
  - Implements priority queue-related methods for efficient path sorting.
  - Supports various operations, including cost recalculation, path copying, and path enumeration.

- **TileAStarPathFinder<T>**
  - Provides static methods for performing A* pathfinding on maps.
  - Supports two modes of pathfinding:
    1. Using a map that implements the `IMap<T>` interface to calculate movement costs.
    2. Using a custom delegate for calculating movement costs.
  - Uses a priority queue to manage the pathfinding frontier.

### 3. TemplateFolder
  **This folder contains functional examples that show how to implement the interfaces and use the path-finding class.**
  - Contains concrete examples of coordinate systems that implement the ITileCoordinate interface.
    - Rectangular
    - Hex
    - Triangluar
    - Cubic (3D)
  - It also contains generic MAZE generation and drawing classes, plus with concrete implementations of them using each of the above coordinate types.  
  - A Generic maze SOLVING class is also provided, using a lineRenderer to display the result.

	
## Key Features

- **Modular Design**: The library is built using interfaces and generic types to allow for easy extension and customization.
- **Flexible Pathfinding**: Supports both standard map-based pathfinding and custom cost functions.
- **Integration with Unity**: Uses Unity's `Vector3` and `Quaternion` types for world positioning and orientation, making it suitable for game development.
- **Heuristic Distance Calculation**: The library provides heuristic functions for efficient pathfinding, commonly used in A* algorithms.
- **The project uses, and includes, a generic priority queue implementation for efficient pathfinding.

## Getting Started

1. Add the `Interfaces.cs` and `Pathfinding.cs` files to your Unity project.
2. Implement the `ITileCoordinate<T>` and `IMap<T>` interfaces for your specific tile map setup.
3. Use the `TileAStarPathFinder<T>` class to find paths by providing a map and start/end coordinates.

### Example Usage

```csharp
var startCoord = new MyTileCoordinate(...);
var endCoord = new MyTileCoordinate(...);
var myMap = new MyMap(...);

TileOnPath<MyTileCoordinate> path = TileAStarPathFinder<MyTileCoordinate>.GetPathFromTo(myMap, startCoord, endCoord);

if (path != null)
{
    foreach (var tile in path.StartToEnd())
    {
        Debug.Log(tile.coordinate);
    }
}
else
{
    Debug.Log("Path not found");
}
```

## Dependencies

  Requires Unity (using UnityEngine types such as Vector3 and Quaternion).
    
## License

All rights reserved.

No license is granted for use, modification, distribution, or any other purpose without prior written permission.

If you're an independent developer and would like to use this software, email glurth at gmail.com to request a license. I usually approve such requests for free.  Businesses may contact me for pricing.

## Contributions

While contributions are welcome, they cannot be used without your explicit written permission, as this project will remain proprietary software.