using UnityEngine;
using System;
using System.Collections;
using Unity.Mathematics;


namespace Nakatetsu.Train.Presentation.Shared
{
    [Serializable]
    public class DotGlyph
    {
        public string character;
        public int width;
        public int height;
        public string[] pattern;
        public char Character => character[0];
    }

    [Serializable]
    public class DotGlyphCollection
    {
        public DotGlyph[] glyphs;
    }

    public class DotMatrixDisplay : MonoBehaviour
    {
        [SerializeField] private TextAsset glyphJson;

        [Header("Pixels")]
        [SerializeField] private Renderer displayRenderer;
        [SerializeField] private int width;
        [SerializeField] private int height;
        [SerializeField] private int pixelPerDot;
        [SerializeField] private float dotRadius = 6f;
        [SerializeField] private Color litColor;
        [SerializeField] private Color unlitColor;
        [SerializeField] private Color backgroundColor;

        [Header("Rendering")]
        [SerializeField] private float displayInterval = 1f;
        [SerializeField] private string[] displayTexts;

        private DotGlyphCollection glyphData;
        private Texture2D texture;
        private Material displayMaterial;
        private Material originalMaterial;
        private Coroutine displayLoop;

        public void SetTexts(string[] texts, float interval)
        {
            displayTexts = texts;
            displayInterval = interval;
            ValidateSettings();

            if (displayLoop != null)
            {
                StopCoroutine(displayLoop);
                displayLoop = null;
            }

            if (isActiveAndEnabled)
            {
                displayLoop = StartCoroutine(DisplayLoop());
            }
        }

        private void OnValidate()
        {
            ValidateSettings();
        }

        private void ValidateSettings()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            pixelPerDot = Mathf.Max(1, pixelPerDot);
            displayInterval = Mathf.Max(0.01f, displayInterval);

            if (displayTexts == null || displayTexts.Length == 0)
            {
                displayTexts = new[] { string.Empty };
            }
        }

        private void Awake()
        {
            if (glyphJson != null)
            {
                glyphData = JsonUtility.FromJson<DotGlyphCollection>(glyphJson.text);
            }
        }
        
        private IEnumerator DisplayLoop()
        {
            int index = 0;
            while (true)
            {
                Render(displayTexts[index] ?? string.Empty);

                yield return new WaitForSeconds(displayInterval);

                index = (index + 1) % displayTexts.Length;
            }
        }

        private void Start()
        {
            SetTexts(displayTexts, displayInterval);
        }

        private void OnDestroy()
        {
            if (displayRenderer != null && displayMaterial != null &&
                displayRenderer.sharedMaterial == displayMaterial)
            {
                displayRenderer.sharedMaterial = originalMaterial;
            }

            if (texture != null) Destroy(texture);
            if (displayMaterial != null) Destroy(displayMaterial);
        }
        

        private void Render(string text)
        {   
            if (displayRenderer == null)
            {
                return;
            }

            var matrix = ToBoolMatrix(text);
            Texture2D textureOutput = ToTexture2D(matrix);

            if (displayMaterial == null)
            {
                originalMaterial = displayRenderer.sharedMaterial;
                displayMaterial = displayRenderer.material;
            }

            displayMaterial.mainTexture = textureOutput;
            displayMaterial.SetTexture("_EmissionMap", textureOutput);
            displayMaterial.EnableKeyword("_EMISSION");
            displayMaterial.SetColor("_EmissionColor", Color.white * 3f);
        }

        private bool TryFindGlyph(char character, out DotGlyph glyph)
        {
            glyph = null;

            foreach(var currentGlyph in glyphData.glyphs)
            {
                if (character == currentGlyph.Character)
                {
                    glyph = currentGlyph;
                    return true;
                }
            }

            return false;
        }


        private Vector2Int GetTextSize(string text)
        {
            int w = 0, h = 0;

            foreach(var character in text)
            {
                if (!TryFindGlyph(character, out var glyph))
                {
                    continue;
                }

                w += glyph.width;
                h = Mathf.Max(glyph.height, h);
            }

            return new Vector2Int(w, h);
        }


        private bool[, ] ToBoolMatrix(string text)
        {

            bool[, ] matrix = new bool [height, width];

            if (glyphData == null)
            {
                return matrix;
            }

            Vector2Int size = GetTextSize(text);

            // テキストが表示機のサイズを超える場合は表示しない
            if (size.x > width || size.y > height)
            {
                return matrix;
            }

            int offsetX = 0;

            foreach(char character in text)
            {
                if (!TryFindGlyph(character, out var glyph))
                {
                    continue;
                }

                for (int y = 0; y < glyph.height; y++)
                {
                    for (int x = 0; x < glyph.width; x++)
                    {
                        if (glyph.pattern[y][x] == '1')
                        {
                            matrix[y, x+offsetX] = true;
                        }
                    }
                }

                offsetX += glyph.width;
            }

            return matrix;

        }

        private Texture2D ToTexture2D(bool[,] dots)
        {
            int textureWidth = width * pixelPerDot;
            int textureHeight = height * pixelPerDot;
            if (texture == null || texture.width != textureWidth || texture.height != textureHeight)
            {
                if (texture != null) Destroy(texture);
                texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            }

            Color[] pixels = new Color[texture.width * texture.height];
            Color[] litDots = CreateDotPixels(true);
            Color[] unlitDots = CreateDotPixels(false);


            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {                         
                    for (int dy = 0; dy < pixelPerDot; dy++)
                    {
                        for (int dx = 0; dx < pixelPerDot; dx++)
                        {
                            int flippedY = (height - 1 - y) * pixelPerDot + dy;
                            int index = flippedY * texture.width + (x * pixelPerDot + dx);
                            int dotIndex = dy * pixelPerDot + dx;
                            if (dots[y, x])
                            {
                                pixels[index] = litDots[dotIndex];
                            } 
                            else
                            {
                                pixels[index] = unlitDots[dotIndex];
                            }
                        }
                    }
                    
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private Color[] CreateDotPixels(bool lit)
        {
            var pixels = new Color[pixelPerDot * pixelPerDot];

            float center = pixelPerDot / 2f;

            for (int y = 0; y < pixelPerDot; y++)
            {
                for (int x = 0; x < pixelPerDot; x++)
                {
                    float dx = (x + 0.5f) - center;
                    float dy = (y + 0.5f) - center;

                    bool inside =
                        dx * dx + dy * dy <= dotRadius * dotRadius;

                    if (inside)
                    {
                        pixels[y * pixelPerDot + x] =
                            lit ? litColor : unlitColor;
                    }
                    else
                    {
                        pixels[y * pixelPerDot + x] = backgroundColor;
                    }
                    
                }
            }

            return pixels;
        }

        private void DebugDots(bool[,] dots)
        {
            int height = dots.GetLength(0);
            int width = dots.GetLength(1);

            var sb = new System.Text.StringBuilder();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    sb.Append(dots[y, x] ? '●' : '○');
                }

                sb.AppendLine();
            }

            Debug.Log(sb.ToString());
        }
    }
}
