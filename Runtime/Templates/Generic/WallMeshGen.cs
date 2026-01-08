#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System.IO;
using System.Collections.Generic;
namespace EyE.Maps.Templates
{
    static public class WallMeshGen
    {

        public static Mesh GenCubeWithExtrasMesh()
        {
            var mesh = new Mesh();
            mesh.name = "CubeWithExtras";
            const float h = 0.5f;
            const float nh = -0.5f;

            Vector3[] verts = new Vector3[]
            {

            // --- Original (unaffected) faces ---
            // Top face (Y +0.5)
            new(nh, h, nh), new(h, h, nh),
            new(h,  h, h), new(nh, h, h),
            
            // Bottom face (Y +0.5)
            new(nh, nh, nh), new(h, nh, nh),
            new(h,  nh, h), new(nh, nh, h),
            
            // --- Split faces (with Z = 0 vertices) ---
            // Left face (X = -0.5)
            new(nh, h, nh), new(nh, h, h),
            new(nh, nh,  h), new(nh, nh, nh),
            new(nh, 0, h), new(nh, 0, nh),

            // Right face (X = +0.5)
            new(h, h, nh), new(h, h, h),
            new(h, nh,  h), new(h, nh, nh),
            new(h, 0, h), new(h, 0, nh),

            // Back face (Z = -0.5)
            new(nh, h, nh), new(h, h, nh),
            new(h, nh,  nh), new(nh, nh, nh),
            new(nh, 0, nh), new(h, 0, nh),

            // Front face (Z = +0.5)
            new(h, h, h), new(nh, h, h),
            new(nh, nh,  h), new(h, nh,  h),
            new(nh, 0, h), new(h, 0, h),
            };

            int[] tris = new int[]
            {
                // face (0–3)
                0, 2, 1,
                0, 3, 2,

                // face (4–7)
                4, 5, 6,
                4, 6, 7,

                // face (8–13)
                8, 12, 9,
                8, 13, 12,
                13, 10,12,
                13,11,10,

                // face (14–19)
                14,15,18,
                14,18,19,
                19,18,17,
                17,18,16,

                // face (20–25)
                20, 21, 24,
                21, 25, 24,
                24, 25, 23,
                25, 22, 23,

                // face (26–31)
                27, 30, 26,
                26, 30, 31,
                30, 28, 31,
                31, 28, 29
            };

            Vector2[] uvs = new Vector2[]
           {
                // face
                new(0,0), new(1,0), new(1,1), new(0,1),
                // face
                new(0,0), new(1,0), new(1,1), new(0,1),

                // split face
                new(0,0), new(1,0), new(1,1), new(0,1), new(0.5f,1), new(0.5f,0),

                // split face
                new(0,0), new(1,0), new(1,1), new(0,1), new(0.5f,1), new(0.5f,0),

                // split face
                new(0,0), new(1,0), new(1,1), new(0,1), new(0,0.5f), new(1,0.5f),

                // split face
                new(0,0), new(1,0), new(1,1), new(0,1), new(0,0.5f), new(1,0.5f),
           };

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.uv = uvs;
            return mesh;
        }
#if UNITY_EDITOR
        [MenuItem("Assets/Generate/CubeWithExtraXYVerts")]
#endif
        public static void GenCubeWithExtras()
        {
            Mesh mesh = GenCubeWithExtrasMesh();
            SaveMeshAsset(mesh, "CubeWithExtras");
        }

#if UNITY_EDITOR
        private static void SaveMeshAsset(Mesh mesh, string name)
        {
            string dir = "Assets/GeneratedMeshes";
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name + ".asset");
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved mesh to: " + path);
        }
#endif
        static Dictionary<EyE.Maps.Templates.WallMeshVariantKey, Mesh> internalStorage;
        static WallMeshGen()
        {
          //  internalStorage = GenAllVariants();
        }
        public static Dictionary<EyE.Maps.Templates.WallMeshVariantKey, Mesh> GetAllVariants()
        {
            return internalStorage;
        }
        static float lastCallWallThickness = -1;
        public static Dictionary<WallMeshVariantKey, Mesh> GenAllVariants(float wallThickness)
        {
            if (internalStorage != null && lastCallWallThickness == wallThickness)
                return internalStorage;

            Dictionary<WallMeshVariantKey, Mesh> variants = new Dictionary<WallMeshVariantKey, Mesh>();
            Mesh baseMsh = GenCubeWithExtrasMesh();
            int[] tileOptions = { 3 , 4, 6 };
            int[] wallCounts = { 0, 1, 2 };
            bool[] trueFalse = { true, false };
           
            foreach (int tiles in tileOptions)
            {

                foreach (int leftWalls in wallCounts)
                {
                    foreach (bool leftAcute in trueFalse)
                    {
                        foreach (int rightWalls in wallCounts)
                        {
                            foreach (bool rightAcute in trueFalse)
                            {
                                WallMeshVariantKey key = new WallMeshVariantKey
                                (
                                    numTileNeighbors: tiles,
                                    numTouchingWallsLeftEnd: leftWalls,
                                    leftWallAcuteAngle: leftAcute,
                                    numTouchingWallsRightEnd: rightWalls,
                                    rightWallAcuteAngle: rightAcute
                                );

                                variants[key] = Mesh.Instantiate(baseMsh);
                                GenVariant(variants[key], wallThickness, tiles, rightWalls, rightAcute, leftWalls, leftAcute);
                            }
                        }
                    }
                }
            }
            internalStorage = variants;
            return variants;
        }

        public static Mesh GetVariant(int numTileNeighbors, int numTouchingWallsRightEnd, bool rightWallAcuteAngle, int numTouchingWallsLeftEnd, bool leftWallAcuteAngle)
        {
            WallMeshVariantKey key = new WallMeshVariantKey
                                (
                                    numTileNeighbors: numTileNeighbors,
                                    numTouchingWallsLeftEnd: numTouchingWallsLeftEnd,
                                    leftWallAcuteAngle: leftWallAcuteAngle,
                                    numTouchingWallsRightEnd: numTouchingWallsRightEnd,
                                    rightWallAcuteAngle: rightWallAcuteAngle
                                );

            return internalStorage[key];
        }


        static int[] leftCorner = new int[] { 4, 7, 10, 11, 23, 28 };
        static int[] leftEnd = new int[] { 0, 3, 8, 9, 20, 27 };
        static int[] leftCenter = new int[] { 12, 13, 24 };
        
        static int[] rightCorner = new int[] { 5, 6, 16, 17, 22, 29 };
        static int[] rightEnd = new int[] { 1, 2, 14, 15, 21, 26 };
        static int[] rightCenter = new int[] { 18, 19, 25 };
        private static void GenVariant(Mesh meshtoVary, float wallThickness, int numTileNeighbors, int numTouchingWallsRightEnd, bool rightWallAcuteAngle, int numTouchingWallsLeftEnd, bool leftWallAcuteAngle)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            float triDisp = wallThickness * 1f / 6f; // 1/3
            float hexDisp = wallThickness * 0.5f;
            float rectDisp = wallThickness * .5f;//1
            Vector3[] verts = meshtoVary.vertices;
            
            void IncX(int[] vertSet, float dispacement)
            {
                foreach (int vertInx in vertSet)
                {
                    Vector3 vert = verts[vertInx];
                    vert.x += dispacement;
                    verts[vertInx] = vert;
                }
            }

        
            if (numTileNeighbors == 3)
            {


                if (numTouchingWallsLeftEnd > 0)
                {
                    if (leftWallAcuteAngle && numTouchingWallsLeftEnd == 1)
                    {
                        IncX(leftCenter, -triDisp);
                    }
                    else
                    {
                      //  IncX(leftCorner, -triDisp);
                      //  IncX(leftEnd, -triDisp);
                    }
                }

                if (numTouchingWallsRightEnd > 0)
                {
                    if (rightWallAcuteAngle && numTouchingWallsRightEnd == 1)
                    {
                        IncX(rightCenter, triDisp);
                    }
                    else
                    {
                       // IncX(rightCorner, triDisp);
                       // IncX(rightEnd, triDisp);
                    }
                }
            }


            
            if (numTileNeighbors == 4)
            {
                if (numTouchingWallsLeftEnd > 0)
                {
                    IncX(leftCorner, -rectDisp);
                }

                if (numTouchingWallsRightEnd > 0)
                {
                    IncX(rightCorner, rectDisp);
                }
            }

            if (numTileNeighbors > 4)
            {
                if (numTouchingWallsLeftEnd > 0)
                {
                    IncX(leftCorner, -hexDisp);
                    IncX(leftEnd, -hexDisp);
                }

                if (numTouchingWallsRightEnd > 0)
                {
                    IncX(rightCorner, hexDisp);
                    IncX(rightEnd, hexDisp);
                }
            }
            
            meshtoVary.vertices=verts;
            return;
        }

    }
}