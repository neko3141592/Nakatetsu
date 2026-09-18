using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nakatetsu.Train.Presentation.Gauges
{
    /// <summary>
    /// 下端を最小値、上端を最大値とする縦目盛り。右端から左へ目盛りを描く。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("Nakatetsu/Gauges/Vertical Gauge Scale")]
    public sealed class VerticalGaugeScale : MaskableGraphic
    {
        [Serializable]
        public struct TickStyle
        {
            [Min(0f)] public float length;
            [Min(0f)] public float thickness;
            public Color color;

            public TickStyle(float length, float thickness, Color color)
            {
                this.length = length;
                this.thickness = thickness;
                this.color = color;
            }
        }

        [Serializable]
        public struct TickSettings
        {
            public float minimum;
            public float maximum;
            [Min(0.0001f)] public float interval;
            [Min(0f)] public float mediumInterval;
            [Min(0f)] public float majorInterval;
            public TickStyle minor;
            public TickStyle medium;
            public TickStyle major;
        }

        [Serializable]
        public struct LabelSettings
        {
            public bool visible;
            public float minimum;
            public float maximum;
            [Min(0.0001f)] public float interval;
            [Min(0f)] public float fontSize;
            public Color color;
            [Tooltip("C# numeric format, for example: 0, 0.0, or 000")]
            public string numberFormat;
        }

        private const int MaximumElementCount = 10000;
        private const string LabelRootName = "Generated Gauge Labels";

        [Header("Ticks")]
        [SerializeField] private TickSettings ticks = new TickSettings
        {
            minimum = 0f,
            maximum = 500f,
            interval = 10f,
            mediumInterval = 50f,
            majorInterval = 100f,
            minor = new TickStyle(6f, 1f, Color.white),
            medium = new TickStyle(10f, 2f, Color.white),
            major = new TickStyle(16f, 3f, Color.white)
        };

        [Header("Number Labels")]
        [SerializeField] private LabelSettings labels = new LabelSettings
        {
            visible = true,
            minimum = 0f,
            maximum = 500f,
            interval = 100f,
            fontSize = 18f,
            color = Color.white,
            numberFormat = "0"
        };

        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField, Min(0f)] private float labelGap = 4f;

        [Tooltip("オンで左端から右に目盛りを描く（右側目盛り用）。")]
        [SerializeField] private bool ticksFromLeft;

        public float EndPadding => Mathf.Max(labels.visible ? labels.fontSize * 0.75f : 0f,
            Mathf.Max(ticks.minor.thickness, Mathf.Max(ticks.medium.thickness, ticks.major.thickness)) * 0.5f);

        public void SetMaximum(float maximum)
        {
            if (ticks.maximum == maximum && labels.maximum == maximum) return;
            ticks.maximum = labels.maximum = maximum;
            SetVerticesDirty();
            labelsDirty = true;
        }

        private RectTransform labelRoot;
        private readonly List<TMP_Text> labelPool = new List<TMP_Text>();
        private bool labelsDirty = true;

        public TickSettings Ticks
        {
            get => ticks;
            set
            {
                ticks = value;
                SetVerticesDirty();
                labelsDirty = true;
            }
        }

        public LabelSettings Labels
        {
            get => labels;
            set
            {
                labels = value;
                labelsDirty = true;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            labelsDirty = true;
        }

        private void LateUpdate()
        {
            if (labelsDirty)
            {
                labelsDirty = false;
                RebuildLabels();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (!TryGetCount(ticks.minimum, ticks.maximum, ticks.interval, out int count))
            {
                return;
            }

            Rect area = rectTransform.rect;
            for (int index = 0; index < count; index++)
            {
                float value = GetValue(ticks.minimum, ticks.maximum, ticks.interval, index);
                float y = Mathf.Lerp(area.yMin, area.yMax, Mathf.InverseLerp(ticks.minimum, ticks.maximum, value));
                TickStyle style = SelectStyle(value);
                style.length = Mathf.Min(style.length, area.width);
                AddTick(vertexHelper, new Vector2(ticksFromLeft ? area.xMin : area.xMax, y), style, ticksFromLeft);
            }
        }

        private TickStyle SelectStyle(float value)
        {
            if (IsInterval(value, ticks.minimum, ticks.majorInterval))
            {
                return ticks.major;
            }

            return IsInterval(value, ticks.minimum, ticks.mediumInterval) ? ticks.medium : ticks.minor;
        }

        private static void AddTick(VertexHelper vertices, Vector2 edge, TickStyle style, bool fromLeft)
        {
            if (style.length <= 0f || style.thickness <= 0f) return;
            Vector2 halfThickness = Vector2.up * (style.thickness * 0.5f);
            Vector2 inner = edge + Vector2.right * (fromLeft ? style.length : -style.length);
            int first = vertices.currentVertCount;
            vertices.AddVert(edge - halfThickness, style.color, Vector2.zero);
            vertices.AddVert(edge + halfThickness, style.color, Vector2.zero);
            vertices.AddVert(inner + halfThickness, style.color, Vector2.zero);
            vertices.AddVert(inner - halfThickness, style.color, Vector2.zero);
            vertices.AddTriangle(first, first + 1, first + 2);
            vertices.AddTriangle(first + 2, first + 3, first);
        }

        private void RebuildLabels()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            EnsureLabelRoot();
            int count = 0;
            bool valid = labels.visible && TryGetCount(labels.minimum, labels.maximum, labels.interval, out count);
            count = valid ? count : 0;

            while (labelPool.Count < count)
            {
                GameObject labelObject = new GameObject($"Label {labelPool.Count}", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(labelRoot, false);
                TMP_Text text = labelObject.GetComponent<TMP_Text>();
                text.alignment = TextAlignmentOptions.Right;
                text.raycastTarget = false;
                labelPool.Add(text);
            }

            for (int index = 0; index < labelPool.Count; index++)
            {
                TMP_Text text = labelPool[index];
                bool active = index < count;
                text.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                float value = GetValue(labels.minimum, labels.maximum, labels.interval, index);
                float normalized = Mathf.InverseLerp(ticks.minimum, ticks.maximum, value);
                RectTransform textTransform = text.rectTransform;
                textTransform.anchorMin = textTransform.anchorMax = new Vector2(ticksFromLeft ? 0f : 1f, normalized);
                textTransform.pivot = new Vector2(ticksFromLeft ? 0f : 1f, 0.5f);
                text.alignment = ticksFromLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
                float tickLength = Mathf.Max(ticks.minor.length, Mathf.Max(ticks.medium.length, ticks.major.length));
                float inset = Mathf.Min(tickLength + labelGap, rectTransform.rect.width);
                textTransform.anchoredPosition = new Vector2(ticksFromLeft ? inset : -inset, 0f);
                textTransform.sizeDelta = new Vector2(Mathf.Max(0f, rectTransform.rect.width - inset), labels.fontSize * 1.5f);
                text.enableAutoSizing = true;
                text.fontSizeMin = 1f;
                text.fontSizeMax = Mathf.Max(1f, labels.fontSize);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                if (labelFont != null)
                {
                    text.font = labelFont;
                }

                text.fontSize = labels.fontSize;
                text.color = labels.color;
                text.text = FormatValue(value, labels.numberFormat);
            }
        }

        private void EnsureLabelRoot()
        {
            if (labelRoot != null)
            {
                return;
            }

            Transform existing = transform.Find(LabelRootName);
            if (existing != null)
            {
                labelRoot = (RectTransform)existing;
            }
            else
            {
                GameObject rootObject = new GameObject(LabelRootName, typeof(RectTransform));
                labelRoot = (RectTransform)rootObject.transform;
                labelRoot.SetParent(rectTransform, false);
                labelRoot.anchorMin = Vector2.zero;
                labelRoot.anchorMax = Vector2.one;
                labelRoot.offsetMin = Vector2.zero;
                labelRoot.offsetMax = Vector2.zero;
            }

            labelPool.Clear();
            for (int index = 0; index < labelRoot.childCount; index++)
            {
                TMP_Text text = labelRoot.GetChild(index).GetComponent<TMP_Text>();
                if (text != null)
                {
                    labelPool.Add(text);
                }
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            labelsDirty = true;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            ticks.interval = Mathf.Max(0.0001f, ticks.interval);
            labels.interval = Mathf.Max(0.0001f, labels.interval);
            SetVerticesDirty();
            labelsDirty = true;
        }
#endif

        private static bool TryGetCount(float minimum, float maximum, float interval, out int count)
        {
            count = 0;
            if (float.IsNaN(minimum) || float.IsInfinity(minimum) ||
                float.IsNaN(maximum) || float.IsInfinity(maximum) ||
                float.IsNaN(interval) || float.IsInfinity(interval) ||
                maximum < minimum || interval <= 0f)
            {
                return false;
            }

            count = Mathf.FloorToInt((maximum - minimum) / interval + 0.0001f) + 1;
            return count > 0 && count <= MaximumElementCount;
        }

        private static float GetValue(float minimum, float maximum, float interval, int index)
        {
            return Mathf.Min(minimum + interval * index, maximum);
        }

        private static bool IsInterval(float value, float minimum, float interval)
        {
            if (interval <= 0f)
            {
                return false;
            }

            float steps = (value - minimum) / interval;
            return Mathf.Abs(steps - Mathf.Round(steps)) <= 0.0001f;
        }

        private static string FormatValue(float value, string format)
        {
            string safeFormat = string.IsNullOrWhiteSpace(format) ? "0" : format;
            try
            {
                return value.ToString(safeFormat);
            }
            catch (FormatException)
            {
                return value.ToString("0");
            }
        }
    }
}
