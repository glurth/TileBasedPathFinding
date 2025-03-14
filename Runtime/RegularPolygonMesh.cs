using System.Collections.Generic;
using UnityEngine;

public static class RegularPolygonMesh
{

    private static Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

    public enum SizeSpecification
    {
        Inscribed, Subscribed, Edgelength
    }

    private static float ComputeRadius(float size, int sides, SizeSpecification type)
    {
        switch (type)
        {
            case SizeSpecification.Inscribed:
                return size;
            case SizeSpecification.Subscribed:
                return size / Mathf.Cos(Mathf.PI / sides);
            case SizeSpecification.Edgelength:
                return size / (2 * Mathf.Sin(Mathf.PI / sides));
            default:
                return size;
        }
    }


    /// <summary>
    /// Generates a regular polygon mesh.
    /// </summary>
    /// <param name="sides">Number of sides of the polygon (minimum 3).</param>
    /// <param name="size">The size parameter, interpreted based on the type.</param>
    /// <param name="type">How the size param should be interpreted by the function</param>
    /// <returns>Procedurally generated Mesh.</returns>
    public static Mesh GeneratePolygon(int sides, float size, SizeSpecification type = SizeSpecification.Inscribed)
    {
        if (sides < 3)
        {
            Debug.LogError("Polygon must have at least 3 sides.");
            return null;
        }

        float radius = ComputeRadius(size, sides, type);

        string cacheKey = $"{sides}_{radius}";
        if (meshCache.TryGetValue(cacheKey, out Mesh cachedMesh))
        {
            return cachedMesh;
        }

        Mesh mesh = CreatePolygonMesh(sides, radius);
        meshCache[cacheKey] = mesh;
        return mesh;
    }

    private static Mesh CreatePolygonMesh(int sides, float radius)
    {
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv2 = new List<Vector2>();

        vertices.Add(Vector3.zero); // Center
        Vector2 centerUV = new Vector2(0.5f, 0.5f);
        uv0.Add(centerUV);
        uv2.Add(centerUV);

        float angleStep = 2f * Mathf.PI / sides;
        float angleOffset = Mathf.PI/2f ;
        for (int i = 0; i < sides; i++) // Loop from 0 to sides - 1
        {
            float angle = angleOffset + i * angleStep;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            vertices.Add(new Vector3(x, y, 0));
            Vector2 uv = new Vector2((x / (2 * radius)) + 0.5f, (y / (2 * radius)) + 0.5f);
            uv0.Add(uv);
            uv2.Add( ((uv- centerUV).normalized *0.5f) + centerUV );

            if (i > 0)
            {
                triangles.Add(0); // Center
                triangles.Add(i+1);
                triangles.Add(i);
            }
        }

        // Close the loop by connecting the last vertex to the first
        triangles.Add(0);
        triangles.Add(1);
        triangles.Add(sides);

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv2);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }



}
