using UnityEditor;
using UnityEngine;
using Nifti.NET;
using System;
using System.IO;
using System.Collections;

public class HoloDVR_EditorScript : EditorWindow
{
    short datatype;
    string filePath = "";
    string fileName = "";

    Shader dvrShader;

    //Dimensions of imported Dataset
    int width;
    int height;
    int depth;

    Texture3D volumeTexture;
    GameObject parent;

    float truncateThreshold;
    //Dimensions of truncated Dataset
    int trunWidth;
    int trunHeight;
    int trunDepth;

    bool niftiLoaded = false;
    bool textureCreated = false;
    bool createGameObject = false;
    bool attachToParent = false;


    Nifti.NET.Nifti niftiBody = new Nifti.NET.Nifti();

    [MenuItem("HoloDVR/Nifti Dataset Importer")]
    public static void ShowWindow()
    {
        GetWindow(typeof(HoloDVR_EditorScript));
    }

    private void OnGUI()
    {

        //Load Nifti-Data from File
        EditorGUILayout.LabelField("File Name:", fileName);

        if (GUILayout.Button("Select File"))
        {
            SelectFile();
        }

        if (GUILayout.Button("Load Nifti Data"))
        {
            if (filePath != null)
            {
                LoadNifti();
            }
            else
            {
                EditorUtility.DisplayDialog("Select Nifti File", "You must select a Nifti file first!", "OK");
                return;
            }
        }

        EditorGUILayout.LabelField("Width:", width.ToString());
        EditorGUILayout.LabelField("Height:", height.ToString());
        EditorGUILayout.LabelField("Depth:", depth.ToString());
        EditorGUILayout.LabelField("Datatype:", datatype.ToString());
        EditorGUILayout.Space(10);

        //Create 3D Texture from Nifti-Data
        EditorGUI.BeginDisabledGroup(niftiLoaded == false);
        dvrShader = (Shader)EditorGUILayout.ObjectField(dvrShader, typeof(Shader), false);

        createGameObject = EditorGUILayout.Toggle("Create Game Object", createGameObject);

        attachToParent = EditorGUILayout.Toggle("Attach to Game Object", attachToParent);
        parent = (GameObject)EditorGUILayout.ObjectField(parent, typeof(GameObject), true);
        
        if (GUILayout.Button("Create 3D Texture"))
        {
            if (niftiBody == null)
            {
                EditorUtility.DisplayDialog("Load Nifti File", "You must load a Nifti dataset", "OK");
                return;
            }
            else
            {
                CreateTexture();
            }

        }
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(textureCreated == false);
        GUILayout.Space(10);
        volumeTexture = (Texture3D)EditorGUILayout.ObjectField(volumeTexture, typeof(Texture3D), false);
        if (GUILayout.Button("Truncate Volume Texture"))
        {
            if (volumeTexture == null)
            {
                EditorUtility.DisplayDialog("No VolumeTexture found!", "You must create a 3D Texture first!", "OK");
                return;
            }
            else
            {
                truncateTexture();
            }
        }

        EditorGUILayout.LabelField("New Width:", trunWidth.ToString());
        EditorGUILayout.LabelField("New Height:", trunHeight.ToString());
        EditorGUILayout.LabelField("New Depth:", trunDepth.ToString());
        EditorGUI.EndDisabledGroup();
    }
    private void SelectFile()
    {
        filePath = EditorUtility.OpenFilePanel("Overwrite with Nifti", "", "nii");
        fileName = Path.GetFileName(filePath);
    }

    private void LoadNifti()
    {
        niftiBody = NiftiFile.Read(filePath);
        width = niftiBody.Dimensions[0];
        height = niftiBody.Dimensions[1];
        depth = niftiBody.Dimensions[2];
        datatype = niftiBody.Header.datatype;
        niftiLoaded = true;
    }

    private void CreateTexture()
    {
        //Scalar format with 16 bit integer (depends on input datatype (nifti header)) in single channel
        TextureFormat format = TextureFormat.RGBA32;

        //Create 3D Texture with the size of the dataset
        Texture3D dvrTex = new Texture3D(width, height, depth, format, false);
        dvrTex.wrapMode = TextureWrapMode.Clamp;

        //Necessary color array for the 3D Texture
        Color[] cols = new Color[width * height * depth];



        //Normalize Raw Data
        int minDataValue = UInt16.MaxValue;
        int maxDataValue = UInt16.MinValue;

        for (int x = 0; x < depth; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < width; z++)
                {
                    int val = niftiBody[z, y, x];
                    minDataValue = Math.Min(minDataValue, val);
                    maxDataValue = Math.Max(maxDataValue, val);
                }
            }
        }
        
        //Create 3D-Color-Array for VolumeTexture
        for (int i = 0; i < depth; i++)
        {
            for (int j = 0; j < height; j++)
            {
                for (int k = 0; k < width; k++)
                {
                    int it = (i * height * width) + (j * width) + k;
                    float normalizedvalue = (float)(niftiBody[k, j, i] - minDataValue) / (float)(maxDataValue - minDataValue);
                    cols[it] = new Color(normalizedvalue, 0, 0, 0);
                }
            }
        }

        dvrTex.SetPixels(cols);
        dvrTex.Apply();

        string textureName = fileName + "_DVR_TEX";
        AssetDatabase.CreateAsset(dvrTex, "Assets/Resources/VolumeTextures/" + textureName + ".asset");
        Debug.Log("Texture saved as: " + textureName);
        volumeTexture = dvrTex;
        textureCreated = true;

        if (createGameObject)
        {
            string volumeName = Path.GetFileNameWithoutExtension(filePath);
            GameObject volume = GameObject.CreatePrimitive(PrimitiveType.Cube);

            DestroyImmediate(volume.GetComponent<BoxCollider>());
            DestroyImmediate(volume.GetComponent<Material>());

            Material dvrMat = new Material(dvrShader);
            dvrMat.name = volumeName + "_DVR_MAT";
            dvrMat.SetTexture("_MainTex", volumeTexture);
            volume.GetComponent<MeshRenderer>().material = dvrMat;


            volume.name = "Volume Render: " + volumeName;
            Vector3 volumeScale = new Vector3(((float)width / (float)100) - 1.0f, ((float)height / (float)100) - 1.0f, ((float)depth / (float)100) - 1.0f);
            volume.transform.localScale += volumeScale;

            if(attachToParent)
            {
                volume.transform.parent = parent.transform;
            }
        }
    }

private void truncateTexture()
    {
        truncateThreshold = 0.0f; //Truncate empty data values
        width = volumeTexture.width;
        height = volumeTexture.height;
        depth = volumeTexture.depth;

        int depthLowerBound = searchBound(depth, height, width, true);
        int depthUpperBound = searchBound(depth, height, width, false);
        int heightLowerBound = searchBound(height, width, depth, true);
        int heightUpperBound = searchBound(height, width, depth, false);
        int widthLowerBound = searchBound(width, depth, height, true);
        int widthUpperBound = searchBound(width, depth, height, false);

        trunWidth = widthUpperBound - widthLowerBound + 1;
        trunHeight = heightUpperBound - heightLowerBound + 1;
        trunDepth = depthUpperBound - depthLowerBound + 1;

        //Scalar format with 16 bit integer (depends on input datatype (nifti header)) in single channel
        TextureFormat format = TextureFormat.RGBA32;

        //Create 3D Texture with the size of the dataset
        Texture3D truncatedTexture = new Texture3D(trunWidth, trunHeight, trunDepth, format, false);
        truncatedTexture.wrapMode = TextureWrapMode.Clamp;

        //Necessary color array for the 3D Texture
        Color[] cols = new Color[trunWidth * trunHeight * trunDepth];

        for (int i = 0; i < trunDepth; i++)
        {
            for (int j = 0; j < trunHeight; j++)
            {
                for (int k = 0; k < trunWidth; k++)
                {
                    int it = (i * trunHeight * trunWidth) + (j * trunWidth) + k;
                    cols[it] = new Color(volumeTexture.GetPixel(k + widthLowerBound,j + heightLowerBound,i + depthLowerBound).r, 0, 0, 0);
                }
            }
        }

        truncatedTexture.SetPixels(cols);
        truncatedTexture.Apply();
        string textureName;
        if (volumeTexture)
        {
            textureName = volumeTexture.name;
        } else
        {
            textureName = fileName + "_DVR_TEX";
        }

        AssetDatabase.CreateAsset(truncatedTexture, "Assets/Resources/VolumeTextures/" + textureName + "_TRUNCATED" + ".asset");
        Debug.Log("Truncated Texture saved as: " + textureName + "_TRUNCATED");

    }

    private int searchBound(int searchDim, int secondDim, int thirdDim, bool forwardSearch)
    {
        //Search For Lower Bound
        if(forwardSearch)
        {
            //Depth, Height, Width
            if (searchDim == volumeTexture.depth)
            {
                for (int x = 0; x < searchDim; x++)
                {
                    for (int y = 0; y < secondDim; y++)
                    {
                        for (int z = 0; z < thirdDim; z++)
                        {
                            if (volumeTexture.GetPixel(z, y, x).r > truncateThreshold)
                            {
                                return x;
                            }
                        }
                    }
                }

                return 0;

            }
            //Height, Width, Depth
            else if (searchDim == volumeTexture.height)
            {
                for (int x = 0; x < searchDim; x++)
                {
                    for (int y = 0; y < secondDim; y++)
                    {
                        for (int z = 0; z < thirdDim; z++)
                        {
                            if (volumeTexture.GetPixel(y, x, z).r > truncateThreshold)
                            {
                                return x;
                            }
                        }
                    }
                }

                return 0;
            }
            //Width, Depth, Height
            else if (searchDim == volumeTexture.width)
            {
                for (int x = 0; x < searchDim; x++)
                {
                    for (int y = 0; y < secondDim; y++)
                    {
                        for (int z = 0; z < thirdDim; z++)
                        {
                            if (volumeTexture.GetPixel(x, z, y).r > truncateThreshold)
                            {
                                return x;
                            }
                        }
                    }
                }

                return 0;
            }
            else
            {
                Debug.Log("[TRUNCATE]: No valid searchDim");
                return 0;
            }
        }
        //Search for Upper Bound
        else
        {
            //Depth, Height, Width
            if (searchDim == volumeTexture.depth)
            {
                for (int x = searchDim - 1; x >= 0; x--)
                {
                    for (int y = 0; y < secondDim; y++)
                    {
                        for (int z = 0; z < thirdDim; z++)
                        {
                            if (volumeTexture.GetPixel(z, y, x).r > truncateThreshold)
                            {
                                return x;
                            }
                        }
                    }
                }

                return searchDim;

            }
            //Height, Width, Depth
            else if (searchDim == volumeTexture.height)
            {
                for (int x = searchDim - 1; x >= 0; x--)
                {
                    for (int y = 0; y < secondDim; y++)
                    {
                        for (int z = 0; z < thirdDim; z++)
                        {
                            if (volumeTexture.GetPixel(y, x, z).r > truncateThreshold)
                            {
                                return x;
                            }
                        }
                    }
                }

                return searchDim;
            }
            //Width, Depth, Height
            else if (searchDim == volumeTexture.width)
            {
                for (int x = searchDim; x >= 0; x--)
                {
                    for (int y = 0; y < secondDim; y++)
                    {
                        for (int z = 0; z < thirdDim; z++)
                        {
                            if (volumeTexture.GetPixel(x, z, y).r > truncateThreshold)
                            {
                                return x;
                            }
                        }
                    }
                }

                return searchDim;
            }
            else
            {
                Debug.Log("[TRUNCATE]: No valid searchDim");
                return searchDim;
            }
        }
    }
}
