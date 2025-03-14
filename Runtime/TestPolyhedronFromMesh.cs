using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestPolyhedronFromMesh : MonoBehaviour
{
    public Polyhedron polyhedron;
    public Mesh inputMesh;
    public bool generate=false;
    private void Update()
    {
        if (generate)
        {
            generate = false;
            polyhedron = new Polyhedron(inputMesh);
        }
    }
}
