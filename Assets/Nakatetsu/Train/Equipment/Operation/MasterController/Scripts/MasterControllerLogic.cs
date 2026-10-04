using System;

namespace Nakatetsu.Train.Equipment.Operation
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
            bool isInputEnabled,
            bool isKeyInserted = false)
        {
            ThrowIfNull(context);
            context.State.powerPosition = 0;
            context.State.brakePosition = context.Settings.emergencyBrakePosition;
            context.State.reverserPosition = initialReverserPosition;
            context.State.isKeyInserted = isKeyInserted;
            context.State.isInputEnabled = isInputEnabled;
            Normalize(context);
        }

        public static bool TrySetKeyInserted(MasterControllerContext context, bool isInserted)
        {
            ThrowIfNull(context);
            if (context.State.isKeyInserted == isInserted)
            {
                return true;
            }

            // キーはEB・レバーサ中立のときだけ抜き差しできる。
            if (context.State.powerPosition != 0 ||
                context.State.brakePosition != context.Settings.emergencyBrakePosition ||
                context.State.reverserPosition != ReverserPosition.Neutral)
            {
                return false;
            }

            context.State.isKeyInserted = isInserted;
            return true;
        }

        public static void SetPowerPosition(MasterControllerContext context, int position)
        {
            ThrowIfNull(context);
            if (!CanMoveHandle(context))
            {
                return;
            }

            context.State.powerPosition = Clamp(position, 0, context.Settings.maxPowerPosition);
            if (context.State.powerPosition > 0)
            {
                context.State.brakePosition = 0;
            }
        }

        public static void SetBrakePosition(MasterControllerContext context, int position)
        {
            ThrowIfNull(context);
            if (!CanMoveHandle(context))
            {
                return;
            }

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
            // 非常位置への設定は、キーや操作入力の許可にかかわらず受け付ける。
            context.State.powerPosition = 0;
            context.State.brakePosition = context.Settings.emergencyBrakePosition;
        }

        public static void SetNeutral(MasterControllerContext context)
        {
            ThrowIfNull(context);
            if (!CanMoveHandle(context))
            {
                return;
            }

            context.State.powerPosition = 0;
            context.State.brakePosition = 0;
        }

        public static bool TrySetReverserPosition(
            MasterControllerContext context,
            ReverserPosition position)
        {
            ThrowIfNull(context);
            if (!context.State.isInputEnabled || !context.State.isKeyInserted ||
                context.State.powerPosition != 0 ||
                context.State.brakePosition != context.Settings.emergencyBrakePosition ||
                (int)position < (int)ReverserPosition.Reverse ||
                (int)position > (int)ReverserPosition.Forward)
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

            // キーなしはレバーサ中立。中立位置ではマスコンをEBに鎖錠する。
            if (!context.State.isKeyInserted)
            {
                context.State.reverserPosition = ReverserPosition.Neutral;
            }
            if (context.State.reverserPosition == ReverserPosition.Neutral)
            {
                context.State.powerPosition = 0;
                context.State.brakePosition = context.Settings.emergencyBrakePosition;
                return;
            }

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

        private static bool CanMoveHandle(MasterControllerContext context)
        {
            // キー挿入・レバーサ前進または後進のときだけEBから動かせる。
            return context.State.isInputEnabled && context.State.isKeyInserted &&
                (context.State.reverserPosition == ReverserPosition.Forward ||
                 context.State.reverserPosition == ReverserPosition.Reverse);
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
