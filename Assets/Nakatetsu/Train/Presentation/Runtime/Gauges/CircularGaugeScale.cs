using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nakatetsu.Train.Presentation.Gauges
{
    /// <summary>
    /// Configurable circular scale shared by speedometers, pressure gauges, and other UI instruments.
    /// Angles are measured clockwise from 12 o'clock.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Nakatetsu/Gauges/Circular Gauge Scale")]
    public sealed class CircularGaugeScale : MaskableGraphic
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
            [Min(0f)] public float radius;
            public float startAngle;
            public float sweepAngle;
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
            [Min(0f)] public float radius;
            public float startAngle;
            public float sweepAngle;
            [Min(0f)] public float fontSize;
            public Color color;
            [Tooltip("C# numeric format, for example: 0, 0.0, or 000")]
            public string numberFormat;
            [Tooltip("Rotate each label so its top points away from the gauge center.")]
            public bool rotateWithScale;
        }

        private const int MaximumElementCount = 10000;
        private const string LabelRootName = "Generated Gauge Labels";

        [Header("Ticks")]
        [SerializeField] private TickSettings ticks = new TickSettings
        {
            minimum = 0f,
            maximum = 120f,
            interval = 2f,
            mediumInterval = 10f,
            majorInterval = 20f,
            radius = 100f,
            startAngle = -120f,
            sweepAngle = 240f,
            minor = new TickStyle(6f, 1f, Color.white),
            medium = new TickStyle(10f, 2f, Color.white),
            major = new TickStyle(16f, 3f, Color.white)
        };

        [Header("Number Labels")]
        [SerializeField] private LabelSettings labels = new LabelSettings
        {
            visible = true,
            minimum = 0f,
            maximum = 120f,
            interval = 20f,
            radius = 75f,
            startAngle = -120f,
            sweepAngle = 240f,
            fontSize = 18f,
            color = Color.white,
            numberFormat = "0",
            rotateWithScale = false
        };

        [SerializeField] private TMP_FontAsset labelFont;

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
            if (!labelsDirty)
            {
                return;
            }

            labelsDirty = false;
            RebuildLabels();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (!TryGetCount(ticks.minimum, ticks.maximum, ticks.interval, out int count))
            {
                return;
            }

            Vector2 center = rectTransform.rect.center;
            for (int index = 0; index < count; index++)
            {
                float value = GetValue(ticks.minimum, ticks.maximum, ticks.interval, index);
                float angle = ValueToAngle(value, ticks.minimum, ticks.maximum, ticks.startAngle, ticks.sweepAngle);
                AddTick(vertexHelper, center, angle, ticks.radius, SelectStyle(value));
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

        private static void AddTick(VertexHelper vertices, Vector2 center, float angle, float radius, TickStyle style)
        {
            if (style.length <= 0f || style.thickness <= 0f)
            {
                return;
            }

            float radians = angle * Mathf.Deg2Rad;
            Vector2 outward = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            Vector2 tangent = new Vector2(outward.y, -outward.x) * (style.thickness * 0.5f);
            Vector2 outer = center + outward * radius;
            Vector2 inner = center + outward * (radius - style.length);
            int first = vertices.currentVertCount;

            vertices.AddVert(outer - tangent, style.color, Vector2.zero);
            vertices.AddVert(outer + tangent, style.color, Vector2.zero);
            vertices.AddVert(inner + tangent, style.color, Vector2.zero);
            vertices.AddVert(inner - tangent, style.color, Vector2.zero);
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
            bool valid = labels.visible && TryGetCount(labels.minimum, labels.maximum, labels.interval, out int count);
            count = valid ? count : 0;

            while (labelPool.Count < count)
            {
                GameObject labelObject = new GameObject($"Label {labelPool.Count}", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(labelRoot, false);
                TMP_Text text = labelObject.GetComponent<TMP_Text>();
                text.alignment = TextAlignmentOptions.Center;
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
                float angle = ValueToAngle(value, labels.minimum, labels.maximum, labels.startAngle, labels.sweepAngle);
                float radians = angle * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
                RectTransform textTransform = text.rectTransform;
                textTransform.anchorMin = new Vector2(0.5f, 0.5f);
                textTransform.anchorMax = new Vector2(0.5f, 0.5f);
                textTransform.pivot = new Vector2(0.5f, 0.5f);
                textTransform.anchoredPosition = direction * labels.radius;
                textTransform.sizeDelta = new Vector2(labels.fontSize * 4f, labels.fontSize * 1.5f);
                textTransform.localRotation = labels.rotateWithScale
                    ? Quaternion.Euler(0f, 0f, -angle)
                    : Quaternion.identity;
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

        public static float ValueToAngle(float value, float minimum, float maximum, float startAngle, float sweepAngle)
        {
            return Mathf.Approximately(minimum, maximum)
                ? startAngle
                : startAngle + Mathf.InverseLerp(minimum, maximum, value) * sweepAngle;
        }

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
