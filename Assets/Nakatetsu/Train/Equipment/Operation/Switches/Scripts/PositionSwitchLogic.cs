using System;

namespace Nakatetsu.Train.Equipment.Operation.Switches
{
    public static class PositionSwitchLogic
    {
        public static void Configure(
            PositionSwitchContext context,
            int firstPosition,
            int lastPosition,
            bool hasSpringReturn,
            int springReturnPosition)
        {
            ThrowIfNull(context);

            PositionSwitchSettings settings = context.Settings;
            settings.minimumPosition = Math.Min(firstPosition, lastPosition);
            settings.maximumPosition = Math.Max(firstPosition, lastPosition);
            settings.hasSpringReturn = hasSpringReturn;
            settings.springReturnPosition = Clamp(
                springReturnPosition,
                settings.minimumPosition,
                settings.maximumPosition);
            context.State.position = ClampToRange(context, context.State.position);
        }

        public static void Initialize(PositionSwitchContext context, int initialPosition)
        {
            ThrowIfNull(context);
            context.State.position = ClampToRange(context, initialPosition);
        }

        public static bool SetPosition(PositionSwitchContext context, int position)
        {
            ThrowIfNull(context);
            int resolvedPosition = ClampToRange(context, position);
            if (context.State.position == resolvedPosition)
            {
                return false;
            }

            context.State.position = resolvedPosition;
            return true;
        }

        public static bool MoveOneStep(PositionSwitchContext context, int direction)
        {
            ThrowIfNull(context);
            int step = Math.Sign(direction);
            return step != 0 && SetPosition(context, context.State.position + step);
        }

        public static bool Release(PositionSwitchContext context)
        {
            ThrowIfNull(context);
            return context.Settings.hasSpringReturn &&
                SetPosition(context, context.Settings.springReturnPosition);
        }

        private static int ClampToRange(PositionSwitchContext context, int position) =>
            Clamp(
                position,
                context.Settings.minimumPosition,
                context.Settings.maximumPosition);

        private static int Clamp(int value, int minimum, int maximum) =>
            Math.Min(Math.Max(value, minimum), maximum);

        private static void ThrowIfNull(PositionSwitchContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
        }
    }
}
