using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using EyE.UnityAssetTypes;
namespace Eye.Maps.Templates
{
    public class FaceCoordinate : ITileCoordinate<FaceCoordinate>
    {
        FacesAndNeighbors sourceRef;
        //made public for serialization
        public FacesAndNeighbors SourceRef { get => sourceRef; }
        public int faceIndex;
        public FaceDetails details => sourceRef.faceDetails[faceIndex];

        public FaceCoordinate(FacesAndNeighbors facesAndNeighbors, int index)
        {
            sourceRef = facesAndNeighbors;
            if (facesAndNeighbors == null)
                throw new ArgumentNullException("may not pass null FacesAndNeighbors object to FaceCoordinate constructor");
            if (index >= facesAndNeighbors.faceDetails.Count)
                throw new ArgumentException("Index provided["+index+"] to FaceCoordinate is invalid.  Max value:" + (facesAndNeighbors.faceDetails.Count-1));
            faceIndex = index;
        }

        public FaceCoordinate value => this;

        public override bool Equals(object obj)
        {
            return Equals(obj as FaceCoordinate);
        }

        public bool Equals(FaceCoordinate other)
        {
            if (other == null) return false;
            // Check both faceIndex and sourceRef for equality
            return other.faceIndex == faceIndex && other.sourceRef == sourceRef;
        }

        public override int GetHashCode()
        {
            // Combine sourceRef and faceIndex to create a unique hash code
            unchecked // To handle overflow
            {
                int hash = 17;
                hash = hash * 23 + (sourceRef?.GetHashCode() ?? 0);
                hash = hash * 23 + faceIndex.GetHashCode();
                return hash;
            }
        }

        public FaceCoordinate GetNeighbor(int neighborIndex)
        {
            return new FaceCoordinate(sourceRef, details.neighborIndices[neighborIndex]);
        }

        public FaceCoordinate[] GetNeighbors()
        {
            int numNeighbors = details.neighborIndices.Count;
            FaceCoordinate[] neighbors = new FaceCoordinate[numNeighbors];
            for (int i = 0; i < numNeighbors; i++)
                neighbors[i] = new FaceCoordinate(sourceRef, details.neighborIndices[i]);
            return neighbors;
        }

        public float HeuristicDistanceTo(ITileCoordinate<FaceCoordinate> end)
        {
            return 1f - Vector3.Dot(details.normal, sourceRef.faceDetails[((FaceCoordinate)end).faceIndex].normal);
            
        }

        public int NumberOfNeighbors()
        {
            return details.neighborIndices.Count;
        }
        public override string ToString()
        {
            return faceIndex.ToString();
        }
    }

    public class FaceMazeMap : GenericMazeMap<FaceCoordinate>
    {
        FacesAndNeighbors _sourceMap;
        public FacesAndNeighbors sourceMap
        {
            get => _sourceMap;
            set
            {
                _sourceMap = value;
                if (_sourceMap.meshRef == null)
                    Debug.LogError("FacesAndNeighbors passed to FaceMazeMap.sourceMap does not contain a valid mesh reference");
                else
                {
                    asyncUsableVertexList = _sourceMap.meshRef.vertices;
                    asyncUsableTriangleList = _sourceMap.meshRef.triangles;
                }
            }
        }
        public Vector3[] AsyncUsableVertexList=>asyncUsableVertexList;
        Vector3[] asyncUsableVertexList = null;
        public int[] AsyncUsableTriangleList =>asyncUsableTriangleList;
        int[] asyncUsableTriangleList = null;

        public FaceMazeMap(FacesAndNeighbors sourceMap):base(
            new FaceCoordinate(sourceMap,sourceMap.faceDetails.Count-1),
            new FaceCoordinate(sourceMap, 0),
            new FaceCoordinate(sourceMap, sourceMap.faceDetails.Count - 1))
        {
            if(sourceMap==null) throw new ArgumentNullException("May not pass null to FaceMazeMap constructor");
            if (sourceMap.faceDetails == null || sourceMap.faceDetails.Count==0) throw new ArgumentNullException("May not pass FaceMazeMap with no faceDetails to FaceMazeMap constructor");
            this.sourceMap = sourceMap;
        }

        public override IEnumerable<FaceCoordinate> allMapCoords
        {
            get
            {
                for(int i=0;i< sourceMap.faceDetails.Count;i++)
                {
                    yield return new FaceCoordinate(sourceMap, i);
                }

            }
        }
        public override Vector3 GetModelSpacePosition(FaceCoordinate coord)
        {
            return sourceMap.faceDetails[coord.faceIndex].normal;
            /*List<int> faceTriStarts= sourceMap.faceDetails[coord.faceIndex].triangles;
            if (faceTriStarts==null || faceTriStarts.Count == 0)
                return sourceMap.faceDetails[coord.faceIndex].normal;//fallback
            int triIndex = faceTriStarts[0];
            if (faceTriStarts.Count == 1)
            {                
                Vector3 sumPos = asyncUsableVertexList[asyncUsableTriangleList[triIndex]];
                sumPos += asyncUsableVertexList[asyncUsableTriangleList[triIndex+1]];
                sumPos += asyncUsableVertexList[asyncUsableTriangleList[triIndex + 2]];
                return sumPos / 3f;
            }
            //if multiple triangle make up each face, it must be constructed such that the first triangle index references the center of the face.
            return asyncUsableVertexList[asyncUsableTriangleList[triIndex]];*/
        }
        public override Vector3 SingleTileModelSpaceOffset()
        {
            return GetModelSpacePosition(new FaceCoordinate(sourceMap, 1)) - GetModelSpacePosition(new FaceCoordinate(sourceMap,0));
        }
        public override Bounds GetModelSpaceBounds()
        {
            return new Bounds(Vector3.zero, 2f * Vector3.one); //radius would be 1, diameter is bounds size
        }
        public override Quaternion GetModelSpaceOrientation(FaceCoordinate coord)
        {
            Vector3 coordPos = GetModelSpacePosition(coord);
            Vector3 neighborPos = GetModelSpacePosition(coord.GetNeighbor(0));
            Vector3 diff = neighborPos - coordPos;
            Quaternion orientataion = Quaternion.identity;
            if (coord.NumberOfNeighbors() % 2 != 0)
               diff  = Quaternion.AngleAxis(180 / coord.NumberOfNeighbors(), -sourceMap.faceDetails[coord.faceIndex].normal)* diff;
            orientataion *= Quaternion.LookRotation(-sourceMap.faceDetails[coord.faceIndex].normal, diff);
            return orientataion;
        }
        public override bool IsWithinBounds(FaceCoordinate coord)
        {
            return coord.faceIndex >= 0 && coord.faceIndex < sourceMap.faceDetails.Count;
        }

        public override Quaternion NeighborBorderOrientation(FaceCoordinate coord, int neighborIndex)
        {
            Vector3 coordPos = GetModelSpacePosition(coord);
            Vector3 neighborPos = GetModelSpacePosition(coord.GetNeighbor(neighborIndex));
            Vector3 diff = neighborPos - coordPos;
            Vector3 avg = (coordPos + neighborPos) * 0.5f;
            diff = diff.normalized;
            avg = avg.normalized;
            return Quaternion.LookRotation(avg, diff);// Vector3.Cross(diff, avg));
        }
    }
    /*
    public class PolygonCoordinate : ITileCoordinate<PolygonCoordinate>
    {
        public PolygonCoordinate value => this;  // Implement the interface property
        public int Index { get; private set; }
        public List<int> NeighborIndices { get; private set; }
        public Vector3 Normal { get; private set; }

        private List<PolygonCoordinate> _neighbors;

        public PolygonCoordinate(FacesAndNeighbors facesAndNeighbors, int index)
        {
            Index = index;
            NeighborIndices = facesAndNeighbors.faceDetails[index].neighborIndices;
            Normal = facesAndNeighbors.faceDetails[index].normal;
            _neighbors = new List<PolygonCoordinate>();
            foreach (int i in NeighborIndices)
                _neighbors.Add(new PolygonCoordinate(facesAndNeighbors, i));
        }
        
        public PolygonCoordinate(int index, List<int> neighborIndices, Vector3 normal)
        {
            Index = index;
            NeighborIndices = neighborIndices;
            Normal = normal;
            _neighbors = new List<PolygonCoordinate>();
        }

        public void SetNeighbors(List<PolygonCoordinate> neighbors)
        {
            _neighbors = neighbors;
        }

        public int NumberOfNeighbors() => _neighbors.Count;

        public PolygonCoordinate[] GetNeighbors() => _neighbors.ToArray();

        public PolygonCoordinate GetNeighbor(int neighborIndex) => _neighbors[neighborIndex];

        public float HeuristicDistanceTo(ITileCoordinate<PolygonCoordinate> end)
        {
            // Implement a heuristic distance, e.g., Euclidean or other as needed
            return Vector3.Distance(this.Normal, end.value.Normal);
        }

        public override bool Equals(object obj) => obj is PolygonCoordinate coord && coord.Index == Index;
        public override int GetHashCode() => Index;

        bool IEquatable<PolygonCoordinate>.Equals(PolygonCoordinate other)
        {
            return other.Index == Index;
        }
    }


    public class PolygonMazeMap : GenericMazeMap<PolygonCoordinate>
    {
        private FacesAndNeighbors _facesAndNeighbors;

        public PolygonMazeMap(FacesAndNeighbors facesAndNeighbors, float worldScale = 1f)
            :
            base(
                size: new PolygonCoordinate(facesAndNeighbors, facesAndNeighbors.faceDetails.Count - 1),
                start: new PolygonCoordinate(facesAndNeighbors, 0),
                end: new PolygonCoordinate(facesAndNeighbors, facesAndNeighbors.faceDetails.Count - 1),
                worldScale)
//            base(
  //              size: new PolygonCoordinate(0, facesAndNeighbors.faceDetails[facesAndNeighbors.faceDetails.Count - 1].neighborIndices, facesAndNeighbors.faceDetails[facesAndNeighbors.faceDetails.Count - 1].normal),
    //            start: new PolygonCoordinate(0, facesAndNeighbors.faceDetails[0].neighborIndices, facesAndNeighbors.faceDetails[0].normal),
      //          end: new PolygonCoordinate(0, facesAndNeighbors.faceDetails[facesAndNeighbors.faceDetails.Count-1].neighborIndices,facesAndNeighbors.faceDetails[facesAndNeighbors.faceDetails.Count-1].normal),
        //        worldScale)
        {
            _facesAndNeighbors = facesAndNeighbors;

            // Convert FaceIndices to PolygonCoordinates
            Dictionary<int, PolygonCoordinate> faceToCoord = new Dictionary<int, PolygonCoordinate>();
            foreach (var face in facesAndNeighbors.faceDetails)
            {
                var coord = new PolygonCoordinate(facesAndNeighbors, face.index);//, face.neighborIndices, face.normal);
                faceToCoord[face.index] = coord;
            }

            // Set up neighbors
            foreach (var face in facesAndNeighbors.faceDetails)
            {
                var coord = faceToCoord[face.index];
                var neighbors = face.neighborIndices.Select(index => faceToCoord[index]).ToList();
                coord.SetNeighbors(neighbors);
            }
            
        }

        public override IEnumerable<PolygonCoordinate> allMapCoords
        {
            get
            {
                foreach (FaceDetails i in _facesAndNeighbors.faceDetails)
                {
                    yield return new PolygonCoordinate(_facesAndNeighbors, i.index);
                }

            }
        }

        public override Vector3 GetWorldPosition(PolygonCoordinate coord)
        {
            // Convert face coordinate to world position using Mesh vertices, if available
            return coord.Normal; // Or calculate based on mesh reference
        }

        public override bool IsWithinBounds(PolygonCoordinate coord)
        {
            return _facesAndNeighbors.faceDetails.Any(face => face.index == coord.Index);
        }

        public override Quaternion NeighborBorderOrientation(PolygonCoordinate coord, int neighborIndex)
        {
            var neighbor = coord.GetNeighbor(neighborIndex);
            if (neighbor != null)
            {
                return Quaternion.LookRotation(neighbor.Normal - coord.Normal);
            }
            return Quaternion.identity;
        }
    }
    */
}