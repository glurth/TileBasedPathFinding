using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif



/// <summary>
/// Static utility for generating triangular prism meshes with configurable base, height, and orientation.
/// Includes optional asset saving when in the Unity Editor.
/// </summary>
public static class PrismMesh
{
    [MenuItem("Asset/GenPrisimMesh")]
    public static void GenDefaultPrisim()
    {
        FromEdgeLengths(1, 1, 60, 2);
    }

    /// <summary>
    /// Orientation mode for prism: triangle face or rectangle face down.
    /// </summary>
    public enum PrismOrientation
    {
        TriangleDown,   // Prism stands on triangle face (Z-axis aligned)
        RectangleDown   // Prism lies on rectangle side (Y-axis aligned)
    }

    /// <summary>
    /// Generates a triangular prism using two base edge lengths joined at a point.
    /// </summary>
    public static Mesh FromEdgeLengths(float a, float b, float angleDeg, float length, PrismOrientation orientation = PrismOrientation.TriangleDown, string nameHint = null)
    {
        float rads = Mathf.Deg2Rad * angleDeg;
        Vector3 p0 = Vector3.zero;
        Vector3 p1 = new Vector3(a, 0, 0);
        Vector3 p2 = new Vector3(b* Mathf.Cos(rads), b* Mathf.Sin(rads), 0);

        return Build(p0, p1, p2, length, orientation, nameHint ?? $"Prism_{a:F2}_{b:F2}_{length:F2}_{orientation}");
    }


    private static Mesh Build(Vector3 p0, Vector3 p1, Vector3 p2, float length, PrismOrientation orientation, string name)
    {
        Vector3 triCenter = (p0 + p1 + p2) / 3f;
        p0 -= triCenter;
        p1 -= triCenter;
        p2 -= triCenter;

        Vector3 offset = new Vector3(0, 0, length);
        Vector3 p3 = p0 + offset;
        Vector3 p4 = p1 + offset;
        Vector3 p5 = p2 + offset;

        Vector3[] verts = new Vector3[]
            {
                // base tri
                p0, p1, p2,
                // top tri
                p5, p4, p3,
                // side 1
                p0, p3, p1, p4,
                // side 2
                p1, p4, p2, p5,
                // side 3
                p2, p5, p0, p3
            };

        int[] tris = new int[]
        {
            0, 2, 1,
            3, 5, 4,
            6, 8, 7, 8, 9, 7,
            10,12,11, 12,13,11,
            14,16,15, 16,17,15
        };

        if (orientation == PrismOrientation.RectangleDown)
        {
            Quaternion q = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < verts.Length; i++)
                verts[i] = q * verts[i];
        }

        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

#if UNITY_EDITOR
        SaveMeshAsset(mesh, name);
#endif

        return mesh;
    }

#if UNITY_EDITOR
    private static void SaveMeshAsset(Mesh mesh, string name)
    {
        string dir = "Assets/GeneratedMeshes";
        string path = Path.Combine(dir, name + ".asset");
        Directory.CreateDirectory(dir);
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        Debug.Log("Saved prism mesh to: " + path);
    }
#endif
}