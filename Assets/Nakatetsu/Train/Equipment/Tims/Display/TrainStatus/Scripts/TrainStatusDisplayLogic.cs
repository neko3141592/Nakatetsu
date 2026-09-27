using System.Globalization;

namespace Nakatetsu.Train.Equipment.Tims.Presentation.TrainStatus
{
    public enum TrainStatusMotion
    {
        Unavailable,
        Coasting,
        Power,
        Regen
    }

    /// <summary>旧TrainFormationDisplayと同じ実測力の符号・1Nのしきい値を使用する。</summary>
    public static class TrainStatusDisplayLogic
    {
        public static TrainStatusMotion ResolveMotion(bool showTractionRegen, bool motorCar,
            bool hasMeasurement, float tractionForceN)
        {
            if (!showTractionRegen || !motorCar) return TrainStatusMotion.Coasting;
            if (!hasMeasurement || float.IsNaN(tractionForceN) || float.IsInfinity(tractionForceN))
                return TrainStatusMotion.Unavailable;
            if (tractionForceN > 1f) return TrainStatusMotion.Power;
            if (tractionForceN < -1f) return TrainStatusMotion.Regen;
            return TrainStatusMotion.Coasting;
        }

        /// <summary>+1は編成先頭側、-1は編成後方側。中立・無効な運転台は非表示。</summary>
        public static int ResolveDirection(int activatedCab, int reverser)
        {
            if ((activatedCab != 1 && activatedCab != -1) || (reverser != 1 && reverser != -1))
                return 0;
            return activatedCab * reverser;
        }

        public static string FormatCarNumber(int oneBasedNumber)
        {
            if (oneBasedNumber < 1) return string.Empty;
            char[] digits = oneBasedNumber.ToString(CultureInfo.InvariantCulture).ToCharArray();
            for (int i = 0; i < digits.Length; i++) digits[i] = (char)('０' + digits[i] - '0');
            return new string(digits);
        }
    }
}
