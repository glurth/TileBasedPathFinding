using UnityEngine;
using System.Collections.Generic;

namespace Eye.Maps.Templates
{
    public abstract class MazeDrawGeneric<T> : MonoBehaviour where T : ITileCoordinate<T>
    {
        public GenericMazeMap<T> maze;
        public T mazeSize;

        public float tileScale = 1f;               // Size of the tiles
        public float wallThicknessFraction = 0.2f; // Thickness of the walls (fraction of tile size)
        public GameObject wallPrefab;              // Prefab for walls
        public GameObject floorPrefab;             // Prefab for floor tiles
        public bool drawBorderWalls = true;        // Determines if border walls should be drawn

        private List<Matrix4x4> floorMatrices = new List<Matrix4x4>(); // Transformation matrices for floors
        private List<Matrix4x4> wallMatrices = new List<Matrix4x4>();  // Transformation matrices for walls

        private Mesh floorMesh;
        private Material floorMaterial;
        private Mesh wallMesh;
        private Material wallMaterial;

        private void Reset()
        {
            mazeSize = DefaultMazeSize();
        }

        void OnEnable()
        {
            maze = CreateMazeMap();
            GenerateMazeVisuals();
            if (floorPrefab != null)
            {
                floorMesh = floorPrefab.GetComponent<MeshFilter>().sharedMesh;
                floorMaterial = floorPrefab.GetComponent<MeshRenderer>().sharedMaterial;
            }
            if (wallPrefab != null)
            {
                wallMesh = wallPrefab.GetComponent<MeshFilter>().sharedMesh;
                wallMaterial = wallPrefab.GetComponent<MeshRenderer>().sharedMaterial;
            }
        }

        void Update()
        {
            DrawInstances(); // Draw the instances each frame
        }

        // Abstract method for creating the specific maze map
        protected abstract GenericMazeMap<T> CreateMazeMap();
        protected abstract T DefaultMazeSize();

        private void GenerateMazeVisuals()
        {
            float wallThickness = this.tileScale * wallThicknessFraction;
            floorMatrices.Clear();
            wallMatrices.Clear();

            if (floorPrefab != null)
            {
                foreach (T coord in maze.allMapCoords)
                {
                    Vector3 tilePosition = maze.GetWorldPosition(coord);
                    Quaternion tileRotation = Quaternion.identity;
                    Vector3 tileScale = floorPrefab.transform.localScale * this.tileScale;
                    Matrix4x4 floorMatrix = Matrix4x4.TRS(tilePosition, tileRotation, tileScale);
                    floorMatrices.Add(floorMatrix);
                }
            }

            foreach (T coord in maze.allMapCoords)
            {
                Vector3 tilePosition = maze.GetWorldPosition(coord);
                bool[] wallsForTile = maze.Walls[coord];

                int neighborCount = coord.NumberOfNeighbors();
                for (int i = 0; i < neighborCount; i++)
                {
                    if (wallsForTile[i])
                    {
                        T neighbor = coord.GetNeighbor(i);

                        // Skip creating border walls if drawBorderWalls is false and the neighbor is outside the maze bounds
                        if (!drawBorderWalls && !maze.IsWithinBounds(neighbor))
                        {
                            continue;
                        }

                        Vector3 neighborPosition = maze.GetWorldPosition(neighbor);
                        Vector3 wallPosition = (tilePosition + neighborPosition) / 2;
                        Quaternion wallRotation = maze.NeighborBorderOrientation(coord, i);
                        Vector3 wallScale = new Vector3(
                            wallPrefab.transform.localScale.x,
                            wallThickness,
                            wallPrefab.transform.localScale.z
                        );

                        Matrix4x4 wallMatrix = Matrix4x4.TRS(wallPosition, wallRotation, wallScale);

                        wallMatrices.Add(wallMatrix);
                    }
                }
            }

            UpdateWorldMatricies();
        }

        private void UpdateWorldMatricies()
        {
            Matrix4x4 w = transform.localToWorldMatrix;
            worldWallMatrices.Clear();
            foreach (Matrix4x4 m in wallMatrices)
                worldWallMatrices.Add(w * m);

            worldFloorMatrices.Clear();
            foreach (Matrix4x4 m in floorMatrices)
                worldFloorMatrices.Add(w * m);
        }

        List<Matrix4x4> worldWallMatrices = new List<Matrix4x4>();
        List<Matrix4x4> worldFloorMatrices = new List<Matrix4x4>();
        private void DrawInstances()
        {
            if (transform.hasChanged)
            {
                transform.hasChanged = false;
                UpdateWorldMatricies();

            }

            if (floorPrefab != null)
            {
                Graphics.DrawMeshInstanced(floorMesh, 0, floorMaterial, worldFloorMatrices);
            }

            if (wallPrefab != null)
            {

                Graphics.DrawMeshInstanced(wallMesh, 0, wallMaterial, worldWallMatrices);
            }
        }
    }
}
