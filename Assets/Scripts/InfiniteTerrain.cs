using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfiniteTerrain : MonoBehaviour {

    const float viewerMoveThreshhold = 25f;
    const float sqrViewerMoveThreshhold = viewerMoveThreshhold * viewerMoveThreshhold;
    const float colliderDistanceThreshold = 5;

    public int colliderLODIndex;
    public LODInfo[] detailLevel;
    public static float maxDistView;

    public Material mapMaterial;

    public Transform viewer;
    public static Vector2 viewerPos;
    Vector2 oldViewerPos;
    static MapGenerator mapGenerator;
    float meshWorldSize;
    int chunkVisibleInView;
    

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> visibleTerrainChunks = new List<TerrainChunk>();

    void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();

        maxDistView = detailLevel[detailLevel.Length - 1].visibleThreshold;
        meshWorldSize = mapGenerator.meshSettings.meshWorldSize;
        chunkVisibleInView = Mathf.RoundToInt(maxDistView / meshWorldSize);

        UpdateVisibleLand();
    }

    void Update()
    {
        viewerPos = new Vector2(viewer.position.x, viewer.position.z);

        if (viewerPos != oldViewerPos)
        {
            foreach(TerrainChunk chunk in visibleTerrainChunks)
            {
                chunk.UpdateCollisionMesh();
            }
        }

        if ((oldViewerPos - viewerPos).sqrMagnitude > sqrViewerMoveThreshhold)
        {
            oldViewerPos = viewerPos;
            UpdateVisibleLand();
        }
    }

    void UpdateVisibleLand()
    {
        HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();
        for(int i = visibleTerrainChunks.Count-1; i >=0; i--)
        {
            alreadyUpdatedChunkCoords.Add(visibleTerrainChunks[i].coord);
            visibleTerrainChunks[i].UpdateTerrainChunk();
        }

        int currentChunkCoordX = Mathf.RoundToInt(viewerPos.x / meshWorldSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPos.y / meshWorldSize);

        for(int yOffset=-chunkVisibleInView; yOffset <= chunkVisibleInView; yOffset++)
        {
            for(int xOffset = -chunkVisibleInView; xOffset <= chunkVisibleInView; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
                if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord))
                {
                    if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                    {
                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                    }
                    else
                    {
                        terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, meshWorldSize, detailLevel, colliderLODIndex, transform, mapMaterial));
                    }
                }
            }
        }
    }

    public class TerrainChunk
    {
        public Vector2 coord;

        GameObject meshObj;
        Vector2 sampleCenter;
        Bounds bounds;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        HeightMap mapData;
        bool mapDataReceived;
        
        LODInfo[] detailLevel;
        LODMesh[] lodMeshes;
        int colliderLODIndex;

        int previousLODIndex = -1;
        bool hasSetCollider;

        public TerrainChunk(Vector2 coord, float meshWorldSize, LODInfo[] detailLevel, int colliderLODIndex, Transform parent, Material material)
        {
            this.coord = coord;
            this.detailLevel = detailLevel;
            this.colliderLODIndex = colliderLODIndex;

            sampleCenter = coord * meshWorldSize / mapGenerator.meshSettings.meshScale;
            Vector2 position = coord * meshWorldSize;
            bounds = new Bounds(position, Vector2.one * meshWorldSize);

            meshObj = new GameObject("Terrain Chunk");
            meshRenderer = meshObj.AddComponent<MeshRenderer>();
            meshFilter = meshObj.AddComponent<MeshFilter>();
            meshCollider = meshObj.AddComponent<MeshCollider>();
            meshRenderer.material = material;

            meshObj.transform.position = new Vector3(position.x, 0, position.y);
            meshObj.transform.parent = parent;
            
            SetVisible(false);

            lodMeshes = new LODMesh[detailLevel.Length];
            for(int i= 0; i < detailLevel.Length; i++)
            {
                lodMeshes[i] = new LODMesh(detailLevel[i].lod);
                lodMeshes[i].updateCallback += UpdateTerrainChunk;
                if (i == colliderLODIndex)
                {
                    lodMeshes[i].updateCallback += UpdateCollisionMesh;
                }
            }
            mapGenerator.RequestheightMap(sampleCenter, OnMapDataReceived);
        }

        void OnMapDataReceived(HeightMap mapData)
        {
            this.mapData = mapData;
            mapDataReceived = true;

            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk()//finding a point on its perimeter that is the closest to viewer position
        {
            if (mapDataReceived)
            {
                float viewerDistFromNearEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPos));

                bool wasVisible = IsVisible();
                bool visible = viewerDistFromNearEdge <= maxDistView;

                if (visible)
                {
                    int lodIndex = 0;
                    for (int i = 0; i < detailLevel.Length - 1; i++)
                    {
                        if (viewerDistFromNearEdge > detailLevel[i].visibleThreshold)
                        {
                            lodIndex = i + 1;

                        }
                        else
                        {
                            break;
                        }
                    }
                    if (lodIndex != previousLODIndex)
                    {
                        LODMesh lodMesh = lodMeshes[lodIndex];
                        if (lodMesh.hasMesh)
                        {
                            previousLODIndex = lodIndex;
                            meshFilter.mesh = lodMesh.mesh;
                        }
                        else if (!lodMesh.requestMesh)
                        {
                            lodMesh.RequestMesh(mapData);
                        }
                    }

                }
                if (wasVisible != visible)
                {
                    if (visible)
                    {
                        visibleTerrainChunks.Add(this);
                    }
                    else
                    {
                        visibleTerrainChunks.Remove(this);
                    }

                    SetVisible(visible);
                }
            }
            
        }

        public void UpdateCollisionMesh()
        {
            if (!hasSetCollider)
            {
                float sqrDstFromViewerEdge = bounds.SqrDistance(viewerPos);

                if (sqrDstFromViewerEdge < detailLevel[colliderLODIndex].sqrVisibleDstThreshold)
                {
                    if (!lodMeshes[colliderLODIndex].requestMesh)
                    {
                        lodMeshes[colliderLODIndex].RequestMesh(mapData);
                    }
                }
                if (sqrDstFromViewerEdge < colliderDistanceThreshold * colliderDistanceThreshold)
                {
                    if (lodMeshes[colliderLODIndex].hasMesh)
                    {
                        meshCollider.sharedMesh = lodMeshes[colliderLODIndex].mesh;
                        hasSetCollider = true;
                    }
                }
            }
        }

        public void SetVisible(bool visible)
        {
            meshObj.SetActive(visible);
        }

        public bool IsVisible()
        {
            return meshObj.activeSelf;
        }
    }

    class LODMesh
    {
        public Mesh mesh;
        public bool requestMesh;
        public bool hasMesh;
        int lod;
        public event System.Action updateCallback;

        public LODMesh(int lod)
        {
            this.lod = lod;
        }

        void OnMeshDataReceived(MeshData meshData)
        {
            mesh = meshData.CreateMesh();
            hasMesh = true;

            updateCallback();
        }

        public void RequestMesh(HeightMap mapData)
        {
            requestMesh = true;
            mapGenerator.RequestMeshData(mapData, lod, OnMeshDataReceived);
        }
    }

    [System.Serializable]
    public struct LODInfo
    {
        [Range(0, MeshSettings.numSupportedLODs - 1)]
        public int lod;
        public float visibleThreshold;
        public float sqrVisibleDstThreshold
        {
            get
            {
                return visibleThreshold * visibleThreshold;
            }
        }
    }
}
