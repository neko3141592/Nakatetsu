using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Nakatetsu.Train.Equipment.Tims.Bus;
using Nakatetsu.Train.Equipment.Tims.Communication;

namespace Nakatetsu.Train.Presentation.Gauges
{
    /// <summary>
    /// Drives the speed needle, numeric speed, and ATC speed markers from the TIMS master bus.
    /// Attach this component to the root of a speed meter.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Nakatetsu/Tims/Speed Meter")]
    public sealed class TimsSpeedMeter : MonoBehaviour
    {
        [Serializable]
        private struct NumericBinding
        {
            public string deviceName;
            public string itemName;
        }

        [Serializable]
        private struct BoolBinding
        {
            public string deviceName;
            public string itemName;
        }

        private struct AtcMarker
        {
            public float speedKmh;
            public Image image;
        }

        private const int MaximumMarkerCount = 1000;
        private const string GeneratedMarkerRootName = "Generated ATC Speed Markers";
        private const string GeneratedMarkerPrefix = "ATC Marker ";

        [Header("TIMS")]
        [SerializeField] private TimsCommunicationController tims;
        [SerializeField] private NumericBinding speedBinding = new NumericBinding
        {
            deviceName = "Train",
            itemName = "SpeedKmh"
        };
        [SerializeField] private NumericBinding atcSpeedBinding = new NumericBinding
        {
            deviceName = "ATC",
            itemName = "PatternAllowSpeedKmh"
        };
        [SerializeField] private bool requireAtcValidity = true;
        [SerializeField] private BoolBinding atcValidityBinding = new BoolBinding
        {
            deviceName = "ATC",
            itemName = "HasValidPattern"
        };

        [Header("Objects")]
        [SerializeField] private RectTransform needle;
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private RectTransform atcMarkerRoot;

        [Header("Speed Scale")]
        [SerializeField] private float minimumSpeedKmh = 0f;
        [SerializeField] private float maximumSpeedKmh = 140f;
        [Tooltip("Needle Z angle at Minimum Speed Kmh.")]
        [SerializeField] private float minimumNeedleAngle = 120f;
        [Tooltip("Needle Z angle at Maximum Speed Kmh.")]
        [SerializeField] private float maximumNeedleAngle = -120f;
        [Min(0f)]
        [SerializeField] private float speedSmoothing = 10f;
        [Min(0.01f)]
        [SerializeField] private float needleStepKmh = 1f;
        [SerializeField] private string speedNumberFormat = "0";
        [SerializeField] private string unavailableSpeedText = "--";

        [Header("ATC Speed Markers")]
        [Min(0f)]
        [SerializeField] private float maximumAtcMarkerSpeedKmh = 140f;
        [Min(0.1f)]
        [SerializeField] private float atcMarkerIntervalKmh = 5f;
        [SerializeField] private Vector2 atcMarkerSize = new Vector2(1024f, 1024f);
        [SerializeField] private float atcMarkerAngleOffset;
        [SerializeField] private Sprite atcMarkerOnSprite;
        [SerializeField] private Sprite atcMarkerOffSprite;

        private readonly List<AtcMarker> atcMarkers = new List<AtcMarker>();
        private float displayedSpeedKmh;
        private bool hasDisplayedSpeed;

        private void OnEnable()
        {
            ResolveReferences();
            RebuildAtcMarkers();
            ShowSpeedUnavailable();
            SetAllAtcMarkersOff();
        }

        private void LateUpdate()
        {
            ResolveTims();
            UpdateSpeedDisplay();
            UpdateAtcMarkers();
        }

        private void Reset()
        {
            ResolveReferences();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maximumSpeedKmh = Mathf.Max(minimumSpeedKmh + 0.0001f, maximumSpeedKmh);
            needleStepKmh = Mathf.Max(0.01f, needleStepKmh);
            maximumAtcMarkerSpeedKmh = Mathf.Max(0f, maximumAtcMarkerSpeedKmh);
            atcMarkerIntervalKmh = Mathf.Max(0.1f, atcMarkerIntervalKmh);
            atcMarkerSize.x = Mathf.Max(0f, atcMarkerSize.x);
            atcMarkerSize.y = Mathf.Max(0f, atcMarkerSize.y);
            ResolveChildReferences();
        }
#endif

        [ContextMenu("Rebuild ATC Speed Markers")]
        public void RebuildAtcMarkers()
        {
            ResolveMarkerRoot();
            ClearGeneratedMarkers();

            if (atcMarkerRoot == null || atcMarkerOffSprite == null)
            {
                return;
            }

            float interval = Mathf.Max(0.1f, atcMarkerIntervalKmh);
            int count = Mathf.FloorToInt(maximumAtcMarkerSpeedKmh / interval + 0.0001f) + 1;
            count = Mathf.Clamp(count, 0, MaximumMarkerCount);

            for (int index = 0; index < count; index++)
            {
                float markerSpeedKmh = index * interval;
                GameObject markerObject = new GameObject(
                    $"{GeneratedMarkerPrefix}{markerSpeedKmh:0.###} km/h",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                markerObject.layer = gameObject.layer;
                markerObject.transform.SetParent(atcMarkerRoot, false);

                Image markerImage = markerObject.GetComponent<Image>();
                markerImage.sprite = atcMarkerOffSprite;
                markerImage.raycastTarget = false;

                RectTransform markerTransform = markerImage.rectTransform;
                markerTransform.anchorMin = new Vector2(0.5f, 0.5f);
                markerTransform.anchorMax = new Vector2(0.5f, 0.5f);
                markerTransform.pivot = new Vector2(0.5f, 0.5f);
                markerTransform.anchoredPosition = Vector2.zero;
                markerTransform.sizeDelta = atcMarkerSize;
                markerTransform.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    SpeedToAngle(markerSpeedKmh) + atcMarkerAngleOffset);

                atcMarkers.Add(new AtcMarker
                {
                    speedKmh = markerSpeedKmh,
                    image = markerImage
                });
            }
        }

        private void UpdateSpeedDisplay()
        {
            if (!TryReadNumber(speedBinding, out float speedKmh))
            {
                ShowSpeedUnavailable();
                return;
            }

            speedKmh = Mathf.Max(0f, speedKmh);
            if (!hasDisplayedSpeed)
            {
                displayedSpeedKmh = speedKmh;
                hasDisplayedSpeed = true;
            }
            else
            {
                float blend = 1f - Mathf.Exp(-speedSmoothing * Time.unscaledDeltaTime);
                displayedSpeedKmh = speedSmoothing <= 0f
                    ? speedKmh
                    : Mathf.Lerp(displayedSpeedKmh, speedKmh, blend);
            }

            float step = Mathf.Max(0.01f, needleStepKmh);
            float needleSpeedKmh = Mathf.Round(displayedSpeedKmh / step) * step;
            SetNeedleSpeed(needleSpeedKmh);

            if (speedText != null)
            {
                speedText.text = FormatSpeed(displayedSpeedKmh);
            }
        }

        private void UpdateAtcMarkers()
        {
            bool hasAtcSpeed = TryReadNumber(atcSpeedBinding, out float atcSpeedKmh);
            if (hasAtcSpeed && requireAtcValidity)
            {
                hasAtcSpeed = TryReadBool(atcValidityBinding, out bool isValid) && isValid;
            }

            if (!hasAtcSpeed)
            {
                SetAllAtcMarkersOff();
                return;
            }

            float interval = Mathf.Max(0.1f, atcMarkerIntervalKmh);
            float activeSpeedKmh = Mathf.Round(atcSpeedKmh / interval) * interval;
            bool activeSpeedIsInRange = activeSpeedKmh >= 0f &&
                                        activeSpeedKmh <= maximumAtcMarkerSpeedKmh + 0.0001f;

            for (int index = 0; index < atcMarkers.Count; index++)
            {
                AtcMarker marker = atcMarkers[index];
                if (marker.image == null)
                {
                    continue;
                }

                bool isOn = activeSpeedIsInRange &&
                            Mathf.Abs(marker.speedKmh - activeSpeedKmh) <= 0.0001f;
                marker.image.sprite = isOn && atcMarkerOnSprite != null
                    ? atcMarkerOnSprite
                    : atcMarkerOffSprite;
            }
        }

        private void ShowSpeedUnavailable()
        {
            displayedSpeedKmh = 0f;
            hasDisplayedSpeed = false;
            SetNeedleSpeed(0f);
            if (speedText != null)
            {
                speedText.text = unavailableSpeedText;
            }
        }

        private void SetNeedleSpeed(float speedKmh)
        {
            if (needle != null)
            {
                needle.localRotation = Quaternion.Euler(0f, 0f, SpeedToAngle(speedKmh));
            }
        }

        private float SpeedToAngle(float speedKmh)
        {
            float ratio = Mathf.InverseLerp(minimumSpeedKmh, maximumSpeedKmh, speedKmh);
            return Mathf.Lerp(minimumNeedleAngle, maximumNeedleAngle, ratio);
        }

        private bool TryReadNumber(NumericBinding binding, out float value)
        {
            value = 0f;
            TimsBusState bus = tims != null ? tims.MasterBus : null;
            if (bus == null || string.IsNullOrWhiteSpace(binding.deviceName) ||
                string.IsNullOrWhiteSpace(binding.itemName))
            {
                return false;
            }

            var key = new TimsTagKey(binding.deviceName, binding.itemName);
            if (bus.TryGetFloat(key, out value))
            {
                return IsFinite(value);
            }

            if (bus.TryGetInt(key, out int integerValue))
            {
                value = integerValue;
                return true;
            }

            return false;
        }

        private bool TryReadBool(BoolBinding binding, out bool value)
        {
            value = false;
            TimsBusState bus = tims != null ? tims.MasterBus : null;
            return bus != null &&
                   !string.IsNullOrWhiteSpace(binding.deviceName) &&
                   !string.IsNullOrWhiteSpace(binding.itemName) &&
                   bus.TryGetBool(new TimsTagKey(binding.deviceName, binding.itemName), out value);
        }

        private void SetAllAtcMarkersOff()
        {
            for (int index = 0; index < atcMarkers.Count; index++)
            {
                Image image = atcMarkers[index].image;
                if (image != null)
                {
                    image.sprite = atcMarkerOffSprite;
                }
            }
        }

        private string FormatSpeed(float speedKmh)
        {
            string format = string.IsNullOrWhiteSpace(speedNumberFormat) ? "0" : speedNumberFormat;
            try
            {
                return speedKmh.ToString(format);
            }
            catch (FormatException)
            {
                return Mathf.RoundToInt(speedKmh).ToString();
            }
        }

        private void ResolveReferences()
        {
            ResolveChildReferences();
            ResolveTims();
            ResolveMarkerRoot();
        }

        private void ResolveChildReferences()
        {
            if (needle == null)
            {
                needle = transform.Find("Needle") as RectTransform;
            }

            if (speedText == null)
            {
                Transform speedObject = transform.Find("Speed");
                speedText = speedObject != null ? speedObject.GetComponent<TMP_Text>() : null;
            }
        }

        private void ResolveTims()
        {
            if (tims == null)
            {
                tims = FindAnyObjectByType<TimsCommunicationController>();
            }
        }

        private void ResolveMarkerRoot()
        {
            if (atcMarkerRoot != null)
            {
                return;
            }

            Transform existing = transform.Find(GeneratedMarkerRootName);
            if (existing != null)
            {
                atcMarkerRoot = existing as RectTransform;
                return;
            }

            GameObject rootObject = new GameObject(GeneratedMarkerRootName, typeof(RectTransform));
            rootObject.layer = gameObject.layer;
            atcMarkerRoot = (RectTransform)rootObject.transform;
            atcMarkerRoot.SetParent(transform, false);
            atcMarkerRoot.anchorMin = new Vector2(0.5f, 0.5f);
            atcMarkerRoot.anchorMax = new Vector2(0.5f, 0.5f);
            atcMarkerRoot.pivot = new Vector2(0.5f, 0.5f);
            atcMarkerRoot.anchoredPosition = Vector2.zero;
            atcMarkerRoot.sizeDelta = atcMarkerSize;
        }

        private void ClearGeneratedMarkers()
        {
            atcMarkers.Clear();
            if (atcMarkerRoot == null)
            {
                return;
            }

            for (int index = atcMarkerRoot.childCount - 1; index >= 0; index--)
            {
                Transform child = atcMarkerRoot.GetChild(index);
                if (!child.name.StartsWith(GeneratedMarkerPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                child.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
