using System.IO;
using UnityEditor;
using UnityEngine;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// Procedural leather textures, written as PNG files that tile in both directions.
    /// Grain: pebbled cells with fine creases between them (a Voronoi pattern), low-frequency mottling and a smoothness map in the
    /// albedo's alpha (the pebbles' tops are shinier than the creases). Suede: fine fibres, matte.
    /// The albedo is near-neutral so the material's colour tints it.
    /// </summary>
    internal static class LeatherTextures
    {
        public const int Size = 512;

        private const float DomeWidth = 0.6f;           // how far in from a pebble's edge its rounded top reaches (in pebble sizes)
        private const float CreaseWidth = 0.1f;         // width of the dark line between two pebbles (in pebble sizes)
        private const float NormalStrength = 2.6f;      // height-map slope -> normal tilt
        private const int MottleCells = 5;              // low-frequency colour variation
        private const float MottleAmount = 0.12f;
        private const float CreaseDarken = 0.22f;
        private const float DomeShade = 0.1f;           // pebble edges are a little darker than their tops
        private const float WrinkleCells = 6;           // big soft wrinkles across the hide
        private const float WrinkleAmount = 0.1f;
        private const float FibreNormalStrength = 2.5f;

        // ------------------------------------------------------------------ public

        public static void WriteGrain(string albedoPath, string normalPath, int cells, int seed)
        {
            int n = Size;
            var height = new float[n * n];
            var albedo = new Color32[n * n];

            Voronoi(n, Mathf.Max(2, cells), seed, out float[] f1, out float[] f2, out float[] cellRand);

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float edge = f2[i] - f1[i];
                    float crease = Smooth(0f, CreaseWidth, edge);
                    float dome = Smooth(0f, DomeWidth, edge);
                    float r = cellRand[i];
                    float wrinkle = Fbm(u, v, (int)WrinkleCells, 3, seed + 101);
                    float fine = Fbm(u, v, cells * 4, 2, seed + 202);
                    height[i] = dome * (0.8f + 0.2f * r) + 0.06f * fine + WrinkleAmount * wrinkle;

                    float mottle = Fbm(u, v, MottleCells, 3, seed + 303);
                    float shade = (1f - CreaseDarken + CreaseDarken * crease) * (1f - DomeShade + DomeShade * dome);
                    float tone = (1f - MottleAmount + 2f * MottleAmount * mottle) * shade;
                    float smooth = 0.4f + 0.6f * dome * (0.7f + 0.3f * r);
                    byte g = ToByte(tone);
                    albedo[i] = new Color32(g, g, g, ToByte(smooth));
                }
            }
            WritePng(albedoPath, albedo, false);
            WritePng(normalPath, NormalFromHeight(height, n, NormalStrength), true);
        }

        public static void WriteSuede(string albedoPath, string normalPath, int seed)
        {
            int n = Size;
            var height = new float[n * n];
            var albedo = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float fibre = 0.5f * Fbm(u, v, 64, 2, seed) + 0.3f * Fbm(u, v, 128, 1, seed + 1) + 0.2f * Fbm(u, v, 192, 1, seed + 2);
                    height[i] = fibre;
                    float tone = 0.82f + 0.18f * fibre + 0.08f * (Fbm(u, v, 4, 2, seed + 3) - 0.5f);
                    albedo[i] = new Color32(ToByte(tone), ToByte(tone), ToByte(tone), 128);
                }
            }
            WritePng(albedoPath, albedo, false);
            WritePng(normalPath, NormalFromHeight(height, n, FibreNormalStrength), true);
        }

        // ------------------------------------------------------------------ pattern maths (all periodic, so the textures tile)

        /// <summary>Distance to the nearest (f1) and second nearest (f2) point of a jittered grid that wraps around, plus a random number per nearest point.</summary>
        private static void Voronoi(int size, int cells, int seed, out float[] f1, out float[] f2, out float[] cellRand)
        {
            var px = new float[cells * cells];
            var py = new float[cells * cells];
            for (int cy = 0; cy < cells; cy++)
            {
                for (int cx = 0; cx < cells; cx++)
                {
                    px[cy * cells + cx] = 0.15f + 0.7f * Hash(cx, cy, seed);
                    py[cy * cells + cx] = 0.15f + 0.7f * Hash(cx, cy, seed + 7);
                }
            }

            f1 = new float[size * size];
            f2 = new float[size * size];
            cellRand = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = (x + 0.5f) / size * cells, fy = (y + 0.5f) / size * cells;
                    int cx = Mathf.FloorToInt(fx), cy = Mathf.FloorToInt(fy);
                    float best1 = float.MaxValue, best2 = float.MaxValue;
                    int bestId = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = cx + dx, ny = cy + dy;
                            int id = Mod(ny, cells) * cells + Mod(nx, cells);
                            float ex = nx + px[id] - fx, ey = ny + py[id] - fy;
                            float d = Mathf.Sqrt(ex * ex + ey * ey);
                            if (d < best1) { best2 = best1; best1 = d; bestId = id; }
                            else if (d < best2) best2 = d;
                        }
                    }
                    int i = y * size + x;
                    f1[i] = best1;
                    f2[i] = best2;
                    cellRand[i] = Hash(bestId, bestId >> 3, seed + 13);
                }
            }
        }

        private static float Fbm(float u, float v, int baseCells, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int cells = Mathf.Max(1, baseCells);
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(u, v, cells, seed + o * 17);
                norm += amp;
                amp *= 0.5f;
                cells *= 2;
            }
            return sum / norm;
        }

        private static float ValueNoise(float u, float v, int cells, int seed)
        {
            float x = u * cells, y = v * cells;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(Mod(x0, cells), Mod(y0, cells), seed);
            float b = Hash(Mod(x0 + 1, cells), Mod(y0, cells), seed);
            float c = Hash(Mod(x0, cells), Mod(y0 + 1, cells), seed);
            float d = Hash(Mod(x0 + 1, cells), Mod(y0 + 1, cells), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static int Mod(int a, int m)
        {
            int r = a % m;
            return r < 0 ? r + m : r;
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static byte ToByte(float v)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
        }

        // ------------------------------------------------------------------ output

        private static Color32[] NormalFromHeight(float[] h, int n, float strength)
        {
            var result = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = h[y * n + Mod(x + 1, n)] - h[y * n + Mod(x - 1, n)];
                    float dy = h[Mod(y + 1, n) * n + x] - h[Mod(y - 1, n) * n + x];
                    Vector3 nrm = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    result[y * n + x] = new Color32(ToByte(nrm.x * 0.5f + 0.5f), ToByte(nrm.y * 0.5f + 0.5f), ToByte(nrm.z * 0.5f + 0.5f), 255);
                }
            }
            return result;
        }

        private static void WritePng(string assetPath, Color32[] pixels, bool isNormalMap)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, isNormalMap);
            tex.SetPixels32(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string full = Path.GetFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !isNormalMap;
            importer.alphaSource = isNormalMap ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false; // the albedo's alpha is the smoothness map, not transparency
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
    }
}
