using System;
using System.Collections.Generic;
using Nakatetsu.Train.Tims.Bus;

namespace Nakatetsu.Train.Tims.Communication
{
    public static class TimsCommunicationLogic
    {
        /// <summary>Collect due sources only. The caller performs I/O and acknowledges successful sends.</summary>
        public static void Calculate(TimsCommunicationContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Output.dueSourceIds.Clear();
            context.Workspace.sourceIds.Clear();
            foreach (var source in context.Input.sources)
            {
                if (!context.Workspace.sourceIds.Add(source.sourceId))
                    throw new ArgumentException("Transmission source IDs must be unique.");
            }
            foreach (var source in context.Input.sources)
            {
                if (!HasTerminal(context, source.carIndex)) continue;
                if (!context.Workspace.nextTransmissionTimesSeconds.TryGetValue(source.sourceId, out float nextTime) ||
                    context.Input.timeSeconds >= nextTime)
                    context.Output.dueSourceIds.Add(source.sourceId);
            }
        }

        public static void MarkTransmitted(TimsCommunicationContext context, int sourceId)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            foreach (var source in context.Input.sources)
            {
                if (source.sourceId != sourceId) continue;
                context.Workspace.nextTransmissionTimesSeconds[sourceId] =
                    context.Input.timeSeconds + Math.Max(0.001f, source.transmissionIntervalSeconds);
                return;
            }
            throw new ArgumentException("Unknown transmission source ID.", nameof(sourceId));
        }

        /// <summary>Call when the set of registered sources is rebuilt, as in legacy RefreshDataSources.</summary>
        public static void ResetSchedule(TimsCommunicationContext context)
        {
            context.Workspace.nextTransmissionTimesSeconds.Clear();
            context.Output.dueSourceIds.Clear();
            context.Workspace.sourceIds.Clear();
        }

        public static void CollectFloatFromCars(TimsCommunicationContext context, TimsTagKey key,
            List<float> values, List<bool> founds)
        {
            values.Clear();
            founds.Clear();
            foreach (var terminal in context.State.terminals)
            {
                float value = 0f;
                bool hasValue = terminal?.localBus != null && terminal.localBus.TryGetFloat(key, out value);
                values.Add(value);
                founds.Add(hasValue);
            }
        }

        private static bool HasTerminal(TimsCommunicationContext context, int carIndex)
        {
            foreach (var terminal in context.State.terminals)
                if (terminal != null && terminal.carIndex == carIndex && terminal.localBus != null) return true;
            return false;
        }
    }
}
