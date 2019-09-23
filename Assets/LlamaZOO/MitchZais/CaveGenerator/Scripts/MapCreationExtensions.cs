using System.Runtime.CompilerServices;
using UnityEngine;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LlamaZOO.MitchZais.CaveGenerator
{
    public static class MapCreationExtensions
    {
        public static void ApplyMapToTexture2D(this MapPattern map, ref Texture2D tex, bool colorCodedRooms = false)
        {
            int yLen = map.Height;
            int xLen = map.Width;
            float roomCount = (map.Regions != null) ? map.Regions[CellType.Floor].Count : 0;

            if (tex == null) { tex = new Texture2D(xLen, yLen); }
            else { tex.Resize(xLen, yLen); }

            var textureBuffer = tex.GetRawTextureData<Color32>();

            var mtOpts = new ParallelOptions() { MaxDegreeOfParallelism = System.Environment.ProcessorCount * 2 };

            Parallel.For(0, yLen, mtOpts, y =>
            {
                for (int x = 0; x < xLen; x++)
                {
                    Cell cell = map.Cells[y, x];
                    Color pixelColor = (cell == CellType.Wall) ? Color.black : Color.white;

                    if (colorCodedRooms)
                    {
                        if (cell.edgeCell && cell == CellType.Wall)
                        {
                            pixelColor = new Color(0.18f, 0.18f, 0.18f);
                        }
                        else if (cell != CellType.Wall)
                        {
                            pixelColor = Color.HSVToRGB((float)cell.regionNum / roomCount, cell.edgeCell ? 0.65f : 1, cell.edgeCell ? 0.8f : 1, false);
                        }
                    }
 
                    textureBuffer[(y * xLen) + x] = pixelColor;
                }
            });

            tex.Apply();
        }
        
        /// <summary>
        /// Scales this texture using a temporary RenderTexture target.
        /// This texture is modified directly without breaking references, returned result is simply in case of a need for quick assignment or chaining.
        /// </summary>
        /// <param name="tex"></param>
        /// <param name="newWidth"></param>
        /// <param name="newHeight"></param>
        /// <returns></returns>
        public static Texture2D Scale(this Texture2D tex, int newWidth, int newHeight)
        {
            var lastActiveRT = RenderTexture.active;
            var RT = new RenderTexture(newWidth, newHeight, 0);
            RenderTexture.active = RT;
            Graphics.Blit(tex, RT);
            RenderTexture.active = lastActiveRT;

            int mipCnt = tex.mipmapCount;
            tex.Resize(newWidth, newHeight);
            tex.ReadPixels(new Rect(0, 0, RT.width, RT.height), 0, 0, mipCnt > 0);
            tex.Apply();

            return tex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static MapRegion GetRegion(this MapPattern map, Cell cell)
        {
            return map.GetRegion(cell.cellType, cell.regionNum);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static MapRegion GetRegion(this MapPattern map, CellType cellType, int regionNum)
        {
            return map.Regions[cellType][regionNum - 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOutsideMap(this MapPattern map, Vector2Int coord)
        {
            return (coord.y < 0 || coord.x < 0 || coord.y >= map.Height || coord.x >= map.Width);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOutsideMap(this Cell[,] map, Vector2Int coord)
        {
            return (coord.y < 0 || coord.x < 0 || coord.y >= map.GetLength(0) || coord.x >= map.GetLength(1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetSurroundingCellCount(this Cell[,] map, Vector2Int coord, int radius, CellType matchCellType, bool outOfBoundsMatch = false)
        {
            int found = 0;

            for (int y = coord.y - radius; y <= coord.y + radius; y++)
            {
                for (int x = coord.x - radius; x <= coord.x + radius; x++)
                {
                    if (x == coord.x && y == coord.y) { continue; }

                    if (map.IsOutsideMap(new Vector2Int(x, y)))
                    {
                        if (outOfBoundsMatch) { found++; }
                        continue;
                    }

                    if (map[y, x] == matchCellType) { found++; }
                }
            }
            return found;
        }
    }
}
