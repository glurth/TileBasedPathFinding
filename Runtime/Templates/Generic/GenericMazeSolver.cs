using System.Collections.Generic;
using UnityEngine;
using Eye.Maps;

public class GenericMazeSolver<T> : MonoBehaviour where T : ITileCoordinate<T>
{
    public MazeDrawGeneric<T> mazeSource;
    public bool solve;
    public LineRenderer lineRenderer;
    public int loopLimit = 100000;
    TileOnPath<T> path;
    // Start is called before the first frame update
    void Start()
    {

    }

    public void Solve(GenericMazeMap<T> map)
    {
        if (!map.IsWithinBounds(map.end))
            Debug.Log("End out of bounds");


        path = TileAStarPathFinder<T>.GetPathFromTo(map, map.start, map.end,-1f,false, loopLimit);

    }

    // Update is called once per frame
    void Update()
    {
        if (solve)
        {
            Solve(mazeSource.maze);
            solve = false;


            if (path != null && lineRenderer != null)
            {

                List<Vector3> positions = new List<Vector3>();
                foreach (TileOnPath<T> step in path.StartToEnd())
                    positions.Add(mazeSource.maze.GetWorldPosition((T)step.coordinate));
                Debug.Log("Found path of " + positions.Count + " steps in length");
                lineRenderer.positionCount = positions.Count;
                lineRenderer.SetPositions(positions.ToArray());
            }
        }
    }
}
