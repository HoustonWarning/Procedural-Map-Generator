using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class NoiseCreator {

    public enum NormalizeMode
    {
        Local,Global
    }

    public static float[,] NoiseMapGenerator(int mapWidth, int mapHeight, NoiseSettings settings, Vector2 sampleCenter)
    {
        float [,] noiseMap = new float[mapWidth, mapHeight];

        System.Random prng = new System.Random(settings.seed);//psudo random number generator based on seed
        Vector2[] octavesOffset = new Vector2[settings.octaves];

        float maxPossibleHeight = 0;
        float amplitude = 1;
        float frequency = 1;

        for (int i = 0; i < settings.octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + settings.offset.x + sampleCenter.x;
            float offsetY = prng.Next(-100000, 100000) - settings.offset.y - sampleCenter.y;//giving a good range, if too high result will stay the same
            octavesOffset[i] = new Vector2(offsetX, offsetY);

            maxPossibleHeight += amplitude;
            amplitude *= settings.persistance;
        }

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;
        float halfHeight = mapHeight / 2f;//in order to zoom in/out to the center of map when we change the scale
        float halfWidth = mapWidth / 2f;

        for(int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                amplitude = 1;
                frequency = 1;
                float noiseHeight = 0;
                for (int i = 0; i < settings.octaves; i++)
                {
                    float xValue = (x- halfWidth + octavesOffset[i].x) / settings.scale * frequency;//the higher the frequency the further sample points will be
                    float yValue = (y- halfHeight + octavesOffset[i].y) / settings.scale * frequency;
                    float perlinValue = Mathf.PerlinNoise(xValue, yValue) * 2 - 1;//to get more interestind values
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= settings.persistance;
                    frequency *= settings.lacunarity;
                }
                if (noiseHeight > maxLocalNoiseHeight)
                {
                    maxLocalNoiseHeight = noiseHeight;
                }
                if (noiseHeight < minLocalNoiseHeight)
                {
                    minLocalNoiseHeight = noiseHeight;
                }

                noiseMap[x, y] = noiseHeight;

                if (settings.normalizeMode == NormalizeMode.Global)
                {
                    float normalizedHeight = (noiseMap[x, y] + 1) / (maxPossibleHeight);
                    noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                }
            }
        }
        if (settings.normalizeMode == NormalizeMode.Local)
        {
            for (int y = 0; y < mapHeight; y++)
        {
                for (int x = 0; x < mapWidth; x++)
                {
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);//normalizing the noise map
                
                }
            }
        }
                return noiseMap;
    }
}

[System.Serializable]
public class NoiseSettings
{

    public NoiseCreator.NormalizeMode normalizeMode;
    public float scale = 40;

    public int octaves = 6;
    [Range(0, 1)]
    public float persistance = 0.6f;
    public float lacunarity = 2;
    public int seed;
    public Vector2 offset;

    public void ValidateValues()
    {
        scale = Mathf.Max(scale, 0.01f);
        octaves = Mathf.Max(octaves, 1);
        lacunarity = Mathf.Max(lacunarity, 1);
        persistance = Mathf.Clamp01(persistance);
    }
}