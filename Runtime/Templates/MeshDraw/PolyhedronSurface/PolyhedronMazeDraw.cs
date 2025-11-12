using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EyE.UnityAssetTypes;
using Cysharp.Threading.Tasks;
using EyE.Threading;
namespace Eye.Maps.Templates
{
    public class PolyhedronMazeDraw : MazeMeshDrawGeneric<FaceCoordinate>
    {

        public FacesAndNeighbors facesAndNeighbors
        {
            get
            {
                if (maze == null) return createOnEnableSourceFacesAndNeighbors; 
                return ((FaceMazeMap)maze).sourceMap;
            }
        }

        public FacesAndNeighbors createOnEnableSourceFacesAndNeighbors;
        protected override GenericMazeMap<FaceCoordinate> CreateMazeMap()
        {
            FaceMazeMap maze = new FaceMazeMap(createOnEnableSourceFacesAndNeighbors);
            maze.GenerateMaze();
            return maze;
        }

        protected override async UniTask<GenericMazeMap<FaceCoordinate>> CreateMazeMapAsync(TaskHandler taskContext)
        {
            FaceMazeMap maze = await FaceMazeMap.CreateFaceMazeMapAsync(createOnEnableSourceFacesAndNeighbors);//  new FaceMazeMap(createOnEnableSourceFacesAndNeighbors);
            await maze.GenerateMazeAsync(taskContext);// CancelBoolRef()); ;
            return maze;
        }

        protected override FaceCoordinate DefaultMazeSize()
        {
            
            if (facesAndNeighbors == null || facesAndNeighbors.faceDetails == null)
            {
                Debug.Log("facesAndNeighbors is null:" + (facesAndNeighbors == null).ToString());
                return null;
            }
            return new FaceCoordinate(facesAndNeighbors,facesAndNeighbors.faceDetails.Count);// facesAndNeighbors.faceDetails[facesAndNeighbors.faceDetails.Count - 1].neighborIndices, facesAndNeighbors.faceDetails[facesAndNeighbors.faceDetails.Count - 1].normal);
        }

        protected override Chunker<FaceCoordinate> GetChunker()
        {
            return new FaceChunker(mazeSize, facesAndNeighbors);
        }

        protected override WallMeshChunkComputerGeneric<FaceCoordinate> GetNewMeshComputer()
        {
            return  new FaceWallMeshChunkComputerGeneric();
        }

        private void OnValidate()
        {
            mazeSize = DefaultMazeSize();
         //   Debug.Log("mazeSize assigned: is null:" + (mazeSize == null).ToString());
        }

    }

    public class FaceChunker : Chunker<FaceCoordinate>
    {
        FacesAndNeighbors map;
        public FaceChunker(FaceCoordinate size, FacesAndNeighbors map) : base(size)
        {
            this.map = map;
        }
        protected override int NumberOfTilesInSize(FaceCoordinate ignored)
        {
            return map.faceDetails.Count;
        }
        /*
        protected override List<List<FaceCoordinate>> GenerateChunks(int numChunks, FaceCoordinate ignored)
        {
            int faceCount = map.faceDetails.Count;
            List<Vector3> normals = new List<Vector3>(faceCount);
            for (int i = 0; i < faceCount; i++)
                normals.Add(map.faceDetails[i].normal);

            // K-means clustering on normals
            List<Vector3> centroids = new List<Vector3>();
            System.Random rand = new System.Random();
            HashSet<int> used = new HashSet<int>();
            // Initialize centroids randomly
            while (centroids.Count < numChunks)
            {
                int pick = rand.Next(faceCount);
                if (!used.Contains(pick))
                {
                    centroids.Add(normals[pick].normalized);
                    used.Add(pick);
                }
            }

            int maxIter = 25;
            List<int>[] clusters = new List<int>[numChunks];
            for (int i = 0; i < numChunks; i++)
                clusters[i] = new List<int>();

            for (int iter = 0; iter < maxIter; iter++)
            {
                // Clear clusters
                for (int i = 0; i < numChunks; i++) clusters[i].Clear();

                // Assign each face to the nearest centroid
                for (int f = 0; f < faceCount; f++)
                {
                    float bestDot = -2f;
                    int bestC = -1;
                    for (int c = 0; c < numChunks; c++)
                    {
                        float dot = Vector3.Dot(normals[f].normalized, centroids[c]);
                        if (dot > bestDot)
                        {
                            bestDot = dot;
                            bestC = c;
                        }
                    }
                    clusters[bestC].Add(f);
                }

                // Update centroids
                for (int c = 0; c < numChunks; c++)
                {
                    if (clusters[c].Count == 0) continue; // Avoid divide by zero
                    Vector3 avg = Vector3.zero;
                    foreach (int f in clusters[c])
                        avg += normals[f].normalized;
                    avg.Normalize();
                    centroids[c] = avg;
                }
            }

            // Return the clusters as chunks
            List<List<FaceCoordinate>> result = new List<List<FaceCoordinate>>(numChunks);
            for (int i = 0; i < numChunks; i++)
            {
                List<int> clusterFaceIndexes = clusters[i];
                List<FaceCoordinate> clusterFaces = new List<FaceCoordinate>();
                foreach (int idx in clusterFaceIndexes)
                    clusterFaces.Add(new FaceCoordinate(map, idx));
                result.Add(clusterFaces);
            }
            return result;
        }
        */
        protected override async UniTask<List<List<FaceCoordinate>>> GenerateChunksAsync(int numChunks, FaceCoordinate ignored, TaskHandler taskContext)
        {
            int faceCount = map.faceDetails.Count;
            List<Vector3> normals = new List<Vector3>(faceCount);
            for (int i = 0; i < faceCount; i++)
                normals.Add(map.faceDetails[i].normal);

            // K-means clustering on normals
            List<Vector3> centroids = new List<Vector3>();
            System.Random rand = new System.Random();
            HashSet<int> used = new HashSet<int>();
            // Initialize centroids randomly
            while (centroids.Count < numChunks)
            {
                int pick = rand.Next(faceCount);
                if (!used.Contains(pick))
                {
                    centroids.Add(normals[pick]);
                    used.Add(pick);
                    taskContext.IncrementProgress(0.1f);
                }

            }
            await taskContext.Yield();
            int maxIter = 25;
            List<int>[] clusters = new List<int>[numChunks];
            for (int i = 0; i < numChunks; i++)
                clusters[i] = new List<int>();

            for (int iter = 0; iter < maxIter; iter++)
            {
                // Clear clusters
                for (int i = 0; i < numChunks; i++) clusters[i].Clear();

                // Assign each face to the nearest centroid
                for (int f = 0; f < faceCount; f++)
                {
                    float bestDot = -2f;
                    int bestC = -1;
                    for (int c = 0; c < numChunks; c++)
                    {
                        float dot = Vector3.Dot(normals[f], centroids[c]);
                        if (dot > bestDot)
                        {
                            bestDot = dot;
                            bestC = c;
                        }
                    }
                    clusters[bestC].Add(f);
                }
                taskContext.IncrementProgress(0.1f);
                await taskContext.Yield();
                // Update centroids
                for (int c = 0; c < numChunks; c++)
                {
                    if (clusters[c].Count == 0) continue; // Avoid divide by zero
                    Vector3 avg = Vector3.zero;
                    foreach (int f in clusters[c])
                        avg += normals[f];
                    avg /= clusters[c].Count;
                    //avg.Normalize();
                    centroids[c] = avg;
                    taskContext.IncrementProgress(0.1f);
                }
                await taskContext.Yield();
            }
            await taskContext.Yield();
            // Return the clusters as chunks
            List<List<FaceCoordinate>> result = new List<List<FaceCoordinate>>(numChunks);
            for (int i = 0; i < numChunks; i++)
            {
                List<int> clusterFaceIndexes = clusters[i];
                List<FaceCoordinate> clusterFaces = new List<FaceCoordinate>();
                foreach (int idx in clusterFaceIndexes)
                    clusterFaces.Add(new FaceCoordinate(map, idx));
                result.Add(clusterFaces);
                taskContext.IncrementProgress(0.1f);
            }
            await taskContext.Yield();
            return result;
        }
    }



    public class FaceWallMeshChunkComputerGeneric : WallMeshChunkComputerGeneric<FaceCoordinate>
    {
      //  protected override Vector3 NormalAtCoord(FaceCoordinate coord) { return map.GetModelSpacePosition(coord).normalized; }// coord.modelspace positon, normalized for faces
        protected override Vector3 NormalAtModelSpacePosition(Vector3 pos) { return pos.normalized; }//  vector normalized for faces
        /*protected override Vector3 ComputeCornerPos(FaceCoordinate coord, int neighborIndex)// use mesh verticies for faces
        {
            FacesAndNeighbors fn = ((FaceMazeMap)map).sourceMap;
            Mesh mesh = fn.meshRef;
            FaceDetails face=fn.faceDetails[coord.faceIndex];
            return mesh.vertices[face.cornerVertexMeshIndices[neighborIndex]];
        }*/
        protected override async UniTask AsyncInit()
        {
            await UniTask.SwitchToMainThread();
            sourceMapMesh = new EyE.Geometry.MeshData(((FaceMazeMap)map).sourceMap.meshRef);
            await UniTask.SwitchToThreadPool();
        }
        EyE.Geometry.MeshData sourceMapMesh = null;
        protected override Vector3 ComputeCornerPos(FaceCoordinate coord, int neighborIndex)// use mesh verticies for faces
        {
            FacesAndNeighbors fn = ((FaceMazeMap)map).sourceMap;
            if (sourceMapMesh == null)
            {
                UniTask.SwitchToMainThread();
                sourceMapMesh = new EyE.Geometry.MeshData(fn.meshRef);
                UniTask.SwitchToThreadPool();
            }
            FaceDetails face = fn.faceDetails[coord.faceIndex];
            return sourceMapMesh.vertices[face.cornerVertexMeshIndices[neighborIndex]];
        }
    }


}