using System;
using System.Collections.Generic;
using Nakatetsu.Train.Tims.Bus;

namespace Nakatetsu.Train.Tims.Communication
{
    public static class TimsCommunicationLogic
    {
        /// <summary>Collect sources that can write to an initialized car terminal.</summary>
        public static void Calculate(TimsCommunicationContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Output.availableSourceIds.Clear();
            context.Workspace.sourceIds.Clear();
            foreach (var source in context.Input.sources)
            {
                if (!context.Workspace.sourceIds.Add(source.sourceId))
                    throw new ArgumentException("Transmission source IDs must be unique.");
            }
            foreach (var source in context.Input.sources)
            {
                if (!HasTerminal(context, source.carIndex)) continue;
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
