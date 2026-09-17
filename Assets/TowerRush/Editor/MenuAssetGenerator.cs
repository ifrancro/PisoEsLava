using System.IO;
using UnityEditor;
using UnityEngine;

namespace HeatRise.EditorTools
{
    public static class MenuAssetGenerator
    {
        const string OutDir = "Assets/TowerRush/UI/Sprites/Generated";
        public const string LavaTexturePath = "Assets/TowerRush/UI/Textures/textura_lava.png";

        public static void EnsureLavaTextureImport()
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(LavaTexturePath);
            if (importer == null)
            {
                Debug.LogError("[MenuAssetGenerator] Missing lava texture at: " + LavaTexturePath);
                return;
            }
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        [MenuItem("HeatRise/Menu/Generate UI Sprites")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(OutDir);

            WriteRoundedRect("Round14All", 32, 14, 14, 14, 14, false, 0);
            WriteRoundedRect("Round14Top", 32, 14, 14, 0, 0, false, 0);
            WriteRoundedRect("Round14Bottom", 32, 0, 0, 14, 14, false, 0);
            WriteRoundedRect("Round10All", 24, 10, 10, 10, 10, false, 0);
            WriteRoundedRect("Round8All", 20, 8, 8, 8, 8, false, 0);
            WriteRoundedRect("Ring10", 24, 10, 10, 10, 10, true, 2f);
            WriteRoundedRect("Ring14", 32, 14, 14, 14, 14, true, 2f);
            WriteCircle("Circle", 16);
            WriteCircleSoft("CircleSoft", 64);
            WriteVerticalGradient("BackgroundGradient", 4, 128,
                new[] { (0f, new Color32(0x17, 0x0a, 0x08, 0xff)), (0.55f, new Color32(0x10, 0x08, 0x07, 0xff)), (1f, new Color32(0x0a, 0x05, 0x04, 0xff)) });
            WriteRoundedRectGradient("Round14CTA", 32, 14, new Color32(0xff, 0x6b, 0x1a, 0xff), new Color32(0xd9, 0x4e, 0x00, 0xff));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MenuAssetGenerator] Generated sprites in " + OutDir);
        }

        static float SdRoundBox(Vector2 p, Vector2 halfSize, float tl, float tr, float bl, float br)
        {
            float rTop = p.x > 0f ? tr : tl;
            float rBot = p.x > 0f ? br : bl;
            float r = p.y > 0f ? rTop : rBot;
            Vector2 q = new Vector2(Mathf.Abs(p.x) - halfSize.x + r, Mathf.Abs(p.y) - halfSize.y + r);
            float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
            return Mathf.Min(Mathf.Max(q.x, q.y), 0f) + outside - r;
        }

        static void WriteRoundedRect(string name, int size, float tl, float tr, float bl, float br, bool ring, float strokeWidth)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            Vector2 half = new Vector2(size * 0.5f, size * 0.5f);
            const float aa = 1f;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f - half.x, y + 0.5f - half.y);
                    float sd = SdRoundBox(p, half, tl, tr, bl, br);
                    float a;
                    if (ring)
                    {
                        float outerA = 1f - Mathf.Clamp01((sd + aa) / (2f * aa));
                        float innerA = 1f - Mathf.Clamp01((sd + strokeWidth + aa) / (2f * aa));
                        a = Mathf.Clamp01(outerA - innerA);
                    }
                    else
                    {
                        a = 1f - Mathf.Clamp01((sd + aa) / (2f * aa));
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            float border = Mathf.Max(tl, tr, bl, br, strokeWidth + 2f) + 1f;
            SaveSprite(tex, name, new Vector4(border, border, border, border));
        }

        static void WriteRoundedRectGradient(string name, int size, float radius, Color32 top, Color32 bottom)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            Vector2 half = new Vector2(size * 0.5f, size * 0.5f);
            const float aa = 1f;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                Color rowColor = Color.Lerp(bottom, top, y / (float)(size - 1));
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f - half.x, y + 0.5f - half.y);
                    float sd = SdRoundBox(p, half, radius, radius, radius, radius);
                    float a = 1f - Mathf.Clamp01((sd + aa) / (2f * aa));
                    Color c = rowColor;
                    c.a = a;
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            SaveSprite(tex, name, new Vector4(radius + 1f, radius + 1f, radius + 1f, radius + 1f));
        }

        static void WriteCircle(string name, int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.5f - 1f;
            const float aa = 1f;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) - radius;
                    float a = 1f - Mathf.Clamp01((d + aa) / (2f * aa));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            SaveSprite(tex, name, Vector4.zero);
        }

        static void WriteCircleSoft(string name, int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.5f;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / radius;
                    float a = 1f - Mathf.Clamp01(d);
                    a *= a;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            SaveSprite(tex, name, Vector4.zero);
        }

        static void WriteVerticalGradient(string name, int width, int height, (float stop, Color32 color)[] stops)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = 1f - y / (float)(height - 1);
                Color c = stops[0].color;
                for (int i = 0; i < stops.Length - 1; i++)
                {
                    if (t >= stops[i].stop && t <= stops[i + 1].stop)
                    {
                        float local = Mathf.InverseLerp(stops[i].stop, stops[i + 1].stop, t);
                        c = Color.Lerp(stops[i].color, stops[i + 1].color, local);
                        break;
                    }
                    c = stops[i + 1].color;
                }
                for (int x = 0; x < width; x++) pixels[y * width + x] = c;
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            SaveSprite(tex, name, Vector4.zero);
        }

        static void SaveSprite(Texture2D tex, string name, Vector4 border)
        {
            string path = OutDir + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}
