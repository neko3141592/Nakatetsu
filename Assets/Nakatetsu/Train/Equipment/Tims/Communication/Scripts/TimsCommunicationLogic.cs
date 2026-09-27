using System;
using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Tims.Bus;

namespace Nakatetsu.Train.Equipment.Tims.Communication
{
    public static class TimsCommunicationLogic
    {
        /// <summary>初回は即時収集し、以後は指定したシミュレーション時間ごとに収集する。</summary>
        public static bool ShouldCollectSources(
            TimsCommunicationState state,
            float deltaTimeSeconds,
            float intervalSeconds)
        {
            if (float.IsNaN(deltaTimeSeconds) || float.IsInfinity(deltaTimeSeconds) || deltaTimeSeconds <= 0f ||
                float.IsNaN(intervalSeconds) || float.IsInfinity(intervalSeconds))
            {
                return false;
            }

            if (!state.hasCollectedSources || intervalSeconds <= 0f)
            {
                state.hasCollectedSources = true;
                state.collectionElapsedSeconds = 0d;
                return true;
            }

            state.collectionElapsedSeconds += deltaTimeSeconds;
            // floatのtick幅を加算したときの丸め誤差を吸収する。
            const double toleranceSeconds = 0.0000001d;
            if (state.collectionElapsedSeconds + toleranceSeconds < intervalSeconds)
            {
                return false;
            }

            // 端数を残し、tick幅によって毎回転送周期が伸びないようにする。
            // 複数周期を越えても、現在値の収集はこのtickで1回だけ行う。
            double intervals = Math.Floor((state.collectionElapsedSeconds + toleranceSeconds) / intervalSeconds);
            state.collectionElapsedSeconds = Math.Max(0d, state.collectionElapsedSeconds - intervals * intervalSeconds);
            return true;
        }

        /// <summary>Collect sources that can write to an initialized car terminal.</summary>
        public static void Calculate(TimsCommunicationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Output.availableSourceIds.Clear();
            context.Workspace.sourceIds.Clear();

            foreach (var source in context.Input.sources)
            {
                if (!context.Workspace.sourceIds.Add(source.sourceId))
                {
                    throw new ArgumentException("Transmission source IDs must be unique.");
                }
            }

            foreach (var source in context.Input.sources)
            {
                if (!HasTerminal(context, source.carIndex))
                {
                    continue;
                }

                context.Output.availableSourceIds.Add(source.sourceId);
            }
        }

        public static void CollectFloatFromCars(TimsCommunicationContext context, TimsTagKey key,
            List<float> values, List<bool> founds)
        {
            values.Clear();
            founds.Clear();
            foreach (var terminal in context.State.terminals)
            {
                float value = 0f;
                bool hasValue = terminal?.localBus != null &&
                    terminal.localBus.TryGetFloat(key, out value);
                values.Add(value);
                founds.Add(hasValue);
            }
        }

        private static bool HasTerminal(TimsCommunicationContext context, int carIndex)
        {
            foreach (var terminal in context.State.terminals)
            {
                if (terminal != null &&
                    terminal.carIndex == carIndex &&
                    terminal.localBus != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
