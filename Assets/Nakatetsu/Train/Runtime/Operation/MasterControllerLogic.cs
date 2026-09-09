using System;

namespace Nakatetsu.Train.Operation
{
    public static class MasterControllerLogic
    {
        public static void ConfigureLimits(
            MasterControllerContext context,
            int maxPowerPosition,
            int maxServiceBrakePosition,
            int emergencyBrakePosition)
        {
            ThrowIfNull(context);

            context.Settings.maxPowerPosition = Math.Max(1, maxPowerPosition);
            context.Settings.maxServiceBrakePosition = Math.Max(1, maxServiceBrakePosition);
            context.Settings.emergencyBrakePosition = Math.Max(
                context.Settings.maxServiceBrakePosition + 1,
                emergencyBrakePosition);

            Normalize(context);
        }

        public static void Initialize(
            MasterControllerContext context,
            ReverserPosition initialReverserPosition,
            bool isInputEnabled)
        {
            ThrowIfNull(context);
            context.State.powerPosition = 0;
            context.State.brakePosition = 0;
            context.State.reverserPosition = initialReverserPosition;
            context.State.isInputEnabled = isInputEnabled;
            Normalize(context);
        }

        public static void SetPowerPosition(MasterControllerContext context, int position)
        {
            ThrowIfNull(context);
            context.State.powerPosition = Clamp(position, 0, context.Settings.maxPowerPosition);
            if (context.State.powerPosition > 0)
            {
                context.State.brakePosition = 0;
            }
        }

        public static void SetBrakePosition(MasterControllerContext context, int position)
        {
            ThrowIfNull(context);
            context.State.brakePosition = Clamp(position, 0, context.Settings.emergencyBrakePosition);
            if (context.State.brakePosition > 0)
            {
                context.State.powerPosition = 0;
            }
        }

        public static void SetServiceBrakePosition(MasterControllerContext context, int position)
        {
            ThrowIfNull(context);
            SetBrakePosition(context, Clamp(position, 0, context.Settings.maxServiceBrakePosition));
        }

        public static void SetEmergencyBrake(MasterControllerContext context)
        {
            ThrowIfNull(context);
            SetBrakePosition(context, context.Settings.emergencyBrakePosition);
        }

        public static void SetNeutral(MasterControllerContext context)
        {
            ThrowIfNull(context);
            context.State.powerPosition = 0;
            context.State.brakePosition = 0;
        }

        public static bool TrySetReverserPosition(
            MasterControllerContext context,
            ReverserPosition position)
        {
            ThrowIfNull(context);
            if (context.State.powerPosition > 0)
            {
                return false;
            }

            context.State.reverserPosition = position;
            return true;
        }

        public static void MoveOneStepTowardBrake(MasterControllerContext context)
        {
            ThrowIfNull(context);
            if (context.State.powerPosition > 0)
            {
                SetPowerPosition(context, context.State.powerPosition - 1);
                return;
            }

            SetBrakePosition(context, context.State.brakePosition + 1);
        }

        public static void MoveOneStepTowardPower(MasterControllerContext context)
        {
            ThrowIfNull(context);
            if (context.State.brakePosition > 0)
            {
                SetBrakePosition(context, context.State.brakePosition - 1);
                return;
            }

            SetPowerPosition(context, context.State.powerPosition + 1);
        }

        public static void StepTowardNeutral(MasterControllerContext context)
        {
            ThrowIfNull(context);
            if (context.State.brakePosition > 0)
            {
                SetBrakePosition(context, context.State.brakePosition - 1);
            }
            else if (context.State.powerPosition > 0)
            {
                SetPowerPosition(context, context.State.powerPosition - 1);
            }
        }

        public static void StepTowardServiceMaxBrake(MasterControllerContext context)
        {
            ThrowIfNull(context);
            if (context.State.powerPosition > 0)
            {
                SetPowerPosition(context, context.State.powerPosition - 1);
                return;
            }

            if (context.State.brakePosition < context.Settings.maxServiceBrakePosition)
            {
                SetServiceBrakePosition(context, context.State.brakePosition + 1);
            }
        }

        public static void Normalize(MasterControllerContext context)
        {
            ThrowIfNull(context);
            context.Settings.maxPowerPosition = Math.Max(1, context.Settings.maxPowerPosition);
            context.Settings.maxServiceBrakePosition = Math.Max(1, context.Settings.maxServiceBrakePosition);
            context.Settings.emergencyBrakePosition = Math.Max(
                context.Settings.maxServiceBrakePosition + 1,
                context.Settings.emergencyBrakePosition);

            context.State.powerPosition = Clamp(
                context.State.powerPosition,
                0,
                context.Settings.maxPowerPosition);
            context.State.brakePosition = Clamp(
                context.State.brakePosition,
                0,
                context.Settings.emergencyBrakePosition);

            if (context.State.powerPosition > 0 && context.State.brakePosition > 0)
            {
                context.State.powerPosition = 0;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Min(Math.Max(value, min), max);
        }

        private static void ThrowIfNull(MasterControllerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
        }
    }
}
