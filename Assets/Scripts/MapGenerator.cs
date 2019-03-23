using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class MapGenerator : MonoBehaviour {

    public MeshSettings meshSettings;
    public HeightMapSettings heightMapSettings;
    public TextureData textureData;

    public Material terrainMaterial;

    public enum DrawMode { NoiseMap,Mesh,FalloffMap};
    public DrawMode drawMode;

    [Range(0,MeshSettings.numSupportedLODs-1)]
    public int editorLOD;

    public bool autoUpdate;

    float[,] falloffMap;


    Queue<MapThreadInfo<HeightMap>> heightMapThreadInfoQueue = new Queue<MapThreadInfo<HeightMap>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    void Start()
    {
        textureData.ApplyToMaterial(terrainMaterial);
        textureData.UpdateMeshHeights(terrainMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);
    }

    void OnValuesUpdated()
    {
        if (!Application.isPlaying)
        {
            EditorDrawMap();
        }
    }

    void OnTextureValuesUpdated()
    {
        textureData.ApplyToMaterial(terrainMaterial);
    }

    public void EditorDrawMap()
    {
        textureData.UpdateMeshHeights(terrainMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);

        HeightMap heightMap = HeightMapGenerator.GenerateHeightMap(meshSettings.numVertsPerLine, meshSettings.numVertsPerLine, heightMapSettings, Vector2.zero);

        MapDisplay mapDisplay = FindObjectOfType<MapDisplay>();
        if (drawMode == DrawMode.NoiseMap)
        {
            mapDisplay.TextureDrawer(TextureGenerator.TextureFromHeightMap(heightMap.values));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            mapDisplay.DrawMesh(MeshGenerator.TerrainMeshGenerator(heightMap.values, meshSettings,editorLOD));
        }
        else if (drawMode == DrawMode.FalloffMap)
        {
            mapDisplay.TextureDrawer(TextureGenerator.TextureFromHeightMap(FalloffMap.GenerateFalloffMap(meshSettings.numVertsPerLine)));
        }
    }

    public void RequestheightMap(Vector2 center, Action<HeightMap> callback)
    {
        ThreadStart threadStart = delegate
        {
            heightMapThread(center, callback);
        };
        new Thread(threadStart).Start();
    }

    void heightMapThread(Vector2 center, Action<HeightMap> callback)
    {
        HeightMap heightMap = HeightMapGenerator.GenerateHeightMap(meshSettings.numVertsPerLine, meshSettings.numVertsPerLine, heightMapSettings, center);
        lock (heightMapThreadInfoQueue)
        {
            heightMapThreadInfoQueue.Enqueue(new MapThreadInfo<HeightMap>(callback, heightMap));
        }
    }
 
    public void RequestMeshData(HeightMap heightMap, int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(heightMap, lod, callback);
        };
        new Thread(threadStart).Start();
    }

    void MeshDataThread(HeightMap heightMap, int lod, Action<MeshData> callback)
    {
        MeshData meshData = MeshGenerator.TerrainMeshGenerator(heightMap.values, meshSettings, lod);
        lock (meshDataThreadInfoQueue)
        {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    void Update()
    {
        if (heightMapThreadInfoQueue.Count > 0)
        {
            for(int i =0; i < heightMapThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<HeightMap> threadInfo = heightMapThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
        if (meshDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    void OnValidate()
    {
        if (meshSettings != null)
        {
            meshSettings.OnValueUpdate -= OnValuesUpdated;
            meshSettings.OnValueUpdate += OnValuesUpdated;
        }
        if (heightMapSettings != null)
        {
            heightMapSettings.OnValueUpdate -= OnValuesUpdated;
            heightMapSettings.OnValueUpdate += OnValuesUpdated;
        }
        if (textureData != null)
        {
            textureData.OnValueUpdate -= OnTextureValuesUpdated;
            textureData.OnValueUpdate += OnTextureValuesUpdated;
        }

    }

    struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public readonly T parameter;
        public MapThreadInfo(Action <T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}

