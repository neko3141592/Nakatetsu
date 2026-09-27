using System;
using System.Globalization;
using Nakatetsu.Core.Time;
using Nakatetsu.Train.Equipment.Tims.Communication;
using Nakatetsu.Train.Equipment.Tims.Speed;
using TMPro;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims.Presentation.Readout
{
    /// <summary>手動指定したTMPの文字列だけを更新する。距離表示は未実装。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Nakatetsu/Tims/Text Readout")]
    public sealed class TimsTextReadout : MonoBehaviour
    {
        [Header("Sources (Automatic)")]
        [Tooltip("未指定なら親のTIMS、または所属TrainRoot内のTIMSを自動取得します。")]
        [SerializeField] private TimsCommunicationController tims;
        [Tooltip("未指定ならシーン内のIWorldTimeSource（ApplicationSimulationController）を自動取得します。")]
        [SerializeField] private MonoBehaviour worldTimeSource;

        [Header("Text (Manual)")]
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text speedText;

        private float nextClockSearchTime;

        private void OnEnable()
        {
            nextClockSearchTime = float.NegativeInfinity;
            Refresh();
        }
        private void LateUpdate() => Refresh();

        public void SetTimsSource(TimsCommunicationController source)
        {
            tims = source;
        }

        public void Refresh()
        {
            ResolveSources();
            if (timeText != null)
            {
                string value = worldTimeSource != null && worldTimeSource is IWorldTimeSource clock
                    ? FormatTime(clock.WorldTimeSeconds)
                    : "－－時－－分－－秒";
                SetText(timeText, value);
            }

            if (speedText != null)
            {
                string value = "－－";
                if (tims != null && tims.isActiveAndEnabled &&
                    tims.MasterBus.TryGetFloat(TimsSpeedController.SpeedKmhKey, out float speedKmh) &&
                    (!tims.MasterBus.TryGetBool(TimsSpeedController.HasValidSpeedKey, out bool valid) || valid))
                {
                    value = FormatSpeed(speedKmh);
                }
                SetText(speedText, value);
            }
        }

        private void ResolveSources()
        {
            if (tims == null)
            {
                tims = GetComponentInParent<TimsCommunicationController>(true);
                if (tims == null)
                {
                    var train = GetComponentInParent<TrainRoot>(true);
                    if (train != null) tims = train.GetComponentInChildren<TimsCommunicationController>(true);
                }
            }

            // Prefab単体や時計の生成前でも、毎フレームのシーン全体検索は避ける。
            if (worldTimeSource != null || Time.unscaledTime < nextClockSearchTime) return;
            nextClockSearchTime = Time.unscaledTime + 1f;
            foreach (var candidate in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!candidate.isActiveAndEnabled || candidate is not IWorldTimeSource) continue;
                worldTimeSource = candidate;
                break;
            }
        }

        public static string FormatTime(double worldTimeSeconds)
        {
            if (double.IsNaN(worldTimeSeconds) || double.IsInfinity(worldTimeSeconds) || worldTimeSeconds < 0d)
                return "－－時－－分－－秒";

            // 世界時刻は巻き戻さず、表示だけ24時間表記にする。
            int seconds = (int)(worldTimeSeconds % 86400d);
            string value = string.Format(CultureInfo.InvariantCulture, "{0:00}時{1:00}分{2:00}秒",
                seconds / 3600, seconds / 60 % 60, seconds % 60);
            return FullWidthDigits(value);
        }

        public static string FormatSpeed(float speedKmh)
        {
            if (float.IsNaN(speedKmh) || float.IsInfinity(speedKmh) || speedKmh < 0f)
                return "－－";

            return FullWidthDigits(Math.Round((double)speedKmh, MidpointRounding.AwayFromZero)
                .ToString("0", CultureInfo.InvariantCulture));
        }

        private static string FullWidthDigits(string value)
        {
            char[] characters = value.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
                if (characters[i] >= '0' && characters[i] <= '9')
                    characters[i] = (char)('０' + characters[i] - '0');
            return new string(characters);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target.text != value) target.text = value;
        }
    }
}
