using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EyE.UnityAssetTypes;

namespace Eye.Maps.Templates
{
    public class PolyhedronMazeDraw : MazeDrawGeneric<FaceCoordinate>
    {
        public FacesAndNeighbors facesAndNeighbors;

        protected override GenericMazeMap<FaceCoordinate> CreateMazeMap()
        {
            FaceMazeMap maze = new FaceMazeMap(facesAndNeighbors);
            maze.GenerateMaze();
            return maze;
        }

        protected override FaceCoordinate DefaultMazeSize()
        {
            return new FaceCoordinate(facesAndNeighbors,facesAndNeighbors.faceDetails.Count);// facesAndNeighbors.faceDetails[facesAndNeighbors.faceDetails.Count - 1].neighborIndices, facesAndNeighbors.faceDetails[facesAndNeighbors.faceDetails.Count - 1].normal);
        }
    }
}