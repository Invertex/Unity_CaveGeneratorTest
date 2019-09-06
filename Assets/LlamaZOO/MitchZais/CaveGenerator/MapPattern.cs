using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LlamaZOO.MitchZais.CaveGenerator
{
    [System.Serializable]
    public class MapPattern
    {
        private CellType[,] map;
        private MapParams mapParams;

        public int Width { get { return (map != null) ? map.GetLength(1) : 0; } }
        public int Height { get { return (map != null) ? map.GetLength(0) : 0; } }

        public MapPattern(int width, int height, int seed = -1, float density = 0.5f, int refinementSteps = 3, int subdivisions = 2)
        {
            if (seed == -1) { seed = Random.Range(int.MinValue, int.MaxValue); }
            mapParams = new MapParams(seed, width, height, density, refinementSteps, subdivisions);
        }
        public MapPattern(MapParams saveData) { this.mapParams = saveData; }
        
        public void Generate()
        {
            Random.InitState(mapParams.seed);
            map = new CellType[mapParams.height, mapParams.width]; //y,x order for proper contigious mapping to 2D space

            RandomFill(map, mapParams.density);
            RefineWalls(map, mapParams.refinementSteps);
            SubdivideMapWithRefinement(mapParams.subdivs);
        }

        private void RandomFill(CellType[,] map, float density)
        {
            int limitY = Height - 1;
            int limitX = Width - 1;

            //Start from 1 and stop before edge so our map defaults with a filled wall border
            for(int y = 1; y < limitY; y++)
            {
                for (int x = 1; x < limitX; x++)
                {
                    map[y, x] = (Random.value < density) ? CellType.Floor : CellType.Wall;
                }
            }
        }

        private void RefineWalls(CellType[,] map, int iterations)
        {
            for (int i = 0; i < iterations; i++)
            {
                for (int y = 1; y < Height - 1; y++)
                {
                    for (int x = 1; x < Width - 1; x++)
                    {
                        int walls = GetSurroundingCellCount(map, (y, x), CellType.Wall);

                        if (walls > 4) { map[y, x] = CellType.Wall; }
                        else if (walls < 4) { map[y, x] = CellType.Floor; }
                    }
                }
            }
        }

        private int GetSurroundingCellCount(CellType[,] map, (int y, int x) coord, CellType matchCellType) 
        {
            int found = 0;
            
            for(int y = coord.y - 1; y <= coord.y + 1; y++)
            {
                for (int x = coord.x - 1; x <= coord.x + 1; x++)
                {
                    //We don't need to check for <0 or >length as we only call this method from a loop that stays within the map wall perimeter for now
                    if (x == coord.x && y == coord.y) { continue; }
                    if(map[y,x] == matchCellType) { found++; }
                }
            }
            return found;
        }

        private void SubdivideMapWithRefinement(int subdivisions)
        {
            for(int i = 0; i < subdivisions; i++)
            {
                map = GetSubdividedMap(1);
                RefineWalls(map, 2);
            }
        }

        private CellType[,] GetSubdividedMap(int subdivisions)
        {
            int SubdividedSize(int val, int divisions)
            {
                for(int i = 0; i < divisions; i++)
                {
                    val *= 2;
                }
                return val;
            }

            int scaledWidth = SubdividedSize(Width, subdivisions);
            int scaledHeight = SubdividedSize(Height, subdivisions);
            int scaledIdxMult = SubdividedSize(1, subdivisions);

            CellType[,] scaledMap = new CellType[scaledHeight, scaledWidth];

            for(int y = 0; y < Height; y++)
            {
                for(int x = 0; x < Width; x++)
                {
                    CellType cell = map[y, x];

                    int scaledYOffset = y * scaledIdxMult;

                    for (int scaledY = scaledYOffset; scaledY < scaledYOffset + scaledIdxMult; scaledY ++)
                    {
                        int scaledXOffset = x * scaledIdxMult;

                        for (int scaledX = scaledXOffset; scaledX < scaledXOffset + scaledIdxMult; scaledX++)
                        {
                            scaledMap[scaledY, scaledX] = cell;
                        }
                    }
                }
            }

            return scaledMap;
        }

        public void ApplyMapToTexture2D(ref Texture2D tex)
        {
            int yLen = Height;
            int xLen = Width;

            if (tex == null) { tex = new Texture2D(xLen, yLen); }
            else { tex.Resize(xLen, yLen); }

            var textureBuffer = tex.GetRawTextureData<Color32>();
            int pixelIdx = 0;
            
            for (int y = 0; y < yLen; y++)
            {
                for (int x = 0; x < xLen; x++)
                {
                    Color pixelColor = (map[y, x] == CellType.Wall) ? Color.black : Color.white;
                    textureBuffer[pixelIdx++] = pixelColor;
                }
            }

            tex.Apply();
        }
    }
}