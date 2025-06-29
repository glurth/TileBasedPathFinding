using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EyE.UnityAssetTypes;

namespace Eye.Maps.Templates
{
    public class PolyhedronMazeDraw : MazeDrawGeneric<FaceCoordinate>
    {

        public FacesAndNeighbors facesAndNeighbors=> ((FaceMazeMap)maze).sourceMap;

        public FacesAndNeighbors createOnEnableSourceFacesAndNeighbors;
        protected override GenericMazeMap<FaceCoordinate> CreateMazeMap()
        {
            FaceMazeMap maze = new FaceMazeMap(createOnEnableSourceFacesAndNeighbors);
            maze.GenerateMaze();
            return maze;
        }
        
        protected override FaceCoordinate DefaultMazeSize()
        {
            return new FaceCoordinate(facesAndNeighbors,facesAndNeighbors.faceDetails.Count);// facesAndNeighbors.faceDetails[facesAndNeighbors.faceDetails.Count - 1].neighborIndices, facesAndNeighbors.faceDetails[facesAndNeighbors.faceDetails.Count - 1].normal);
        }
        protected override Matrix4x4 GetNeighborWallMatrix(FaceCoordinate coord, int neighborIndex, Vector3 tilePosition, int neighborCount)
        {
            //we will use facesAndNeighbors to get ref to mesh, and vertex index of two coners these neighbors touch. (note the faces will NOT share the same vertex index, but they will have the same 3d value [cuz normals per face])
            //then using these as the endpoints, we will generate a maxtrix for a box, scaled to be thin along the line between them, and normal/flush with the face.
            //the cube will be drawn on both sides/faces and should be offset such that they will exactly touch at the apex of where they meet (and overlap beneath)
            // float wallThicknessFraction, wallHeightFraction will define how thick/deep and tall the line should be, as a fraction of it's length
            Mesh mesh = facesAndNeighbors.meshRef;
            FaceCoordinate neighbor = coord.GetNeighbor(neighborIndex);
            FaceDetails coordFaceDetails = facesAndNeighbors.faceDetails[coord.faceIndex];
            FaceDetails neighborFaceDetails = facesAndNeighbors.faceDetails[neighbor.faceIndex];
            List<Vector3> endpoints = new List<Vector3>(2);
            foreach (int faceCornerVertIndex in coordFaceDetails.cornerVertexMeshIndices)
            {
                Vector3 pos = mesh.vertices[faceCornerVertIndex];
                foreach (int neighborCornerVertIndex in neighborFaceDetails.cornerVertexMeshIndices)
                {
                    Vector3 posN= mesh.vertices[neighborCornerVertIndex];
                    if (posN == pos)
                    {
                        //match
                        endpoints.Add(pos);
                        break;
                    }
                }
                if (endpoints.Count > 1) break;
            }
            if (endpoints.Count < 2) throw new System.Exception("Unexpected processing- unable to find matching corners for faces: ["+ coord + "] ,["+neighbor+"]");
            Vector3 edge = (endpoints[0] - endpoints[1]);
            Vector3 edgeDir = edge.normalized;
            Vector3 edgeCenterPos = (endpoints[0] + endpoints[1])/2f;
            Vector3 edgeNormal = (coordFaceDetails.normal + neighborFaceDetails.normal) * 0.5f;
            if (Mathf.Abs(Vector3.Dot(edgeNormal, edgeDir)) > 0.03f)
                Debug.LogWarning("Error: edge and edge normal are not perpendicular");
           // Debug.Log("Edge Normal:" + edgeNormal + "  Edge dir:" + edgeDir  + "Edge:" + edge);

            Quaternion wallRot = Quaternion.LookRotation(edgeDir, edgeNormal); //rotates forward (z-axis) to look down length of edge, with up pointing directly away from face.
            float edgeLen = edge.magnitude;
            Vector3 wallScale = new Vector3(wallThicknessFraction, wallHeightFraction, 1) * edgeLen;
            return Matrix4x4.TRS(edgeCenterPos, wallRot, wallScale);

        }
    }
}