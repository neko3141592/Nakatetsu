using System;

namespace Nakatetsu.Track.Atc
{
    public static class TrackAtcLogic
    {
        public static void Calculate(TrackAtcContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // 工程1の入力スナップショットはControllerで完了している。
            TrackAtcValidationLogic.UpdateValidation(context);
            TrackAtcPathLogic.UpdatePath(context);
            TrackAtcProtectionModeLogic.UpdateProtectionMode(context);
            TrackAtcTelegramLogic.UpdateTelegram(context);

            // 子Stateの結果を変更せず、今回の全体状態だけを取りまとめる。
            context.State.isAtcHealthy = IsAtcHealthy(context);

            // 失敗や送信対象なしの場合も、前回のOutputを今回の結果で置き換える。
            TrackAtcOutputLogic.UpdateOutput(context);
        }

        private static bool IsAtcHealthy(TrackAtcContext context)
        {
            if (!context.State.validation.isGraphValid ||
                !context.State.validation.isSimulationTimeValid)
            {
                return false;
            }

            foreach (var circuit in context.State.telegram.circuitsById.Values)
            {
                if (!circuit.isValid)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
