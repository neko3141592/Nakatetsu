using Nakatetsu.Train.Simulation.Door;

namespace Nakatetsu.Train.Equipment.Door
{
    public interface IDoorInputSource
    {
        bool TryGetSpeedMps(out float speedMps);
        // 同じ操作は同じrevisionを返す。再操作時には同じcommandでもrevisionを更新する。
        bool TryGetCommand(bool leftSide, out DoorMotionCommand command, out int revision);
    }

    public sealed class DoorControlContext
    {
        public bool hasSpeed;
        public float speedMps;
        public float maximumOpeningSpeedMps = 0.1f;
        public bool leftOpenPermitted = true;
        public bool rightOpenPermitted = true;
        public bool hasLeftRequest;
        public bool hasRightRequest;
        public DoorMotionCommand leftRequest;
        public DoorMotionCommand rightRequest;
        public int leftRequestRevision;
        public int rightRequestRevision;
        public bool hasLeftRevision;
        public bool hasRightRevision;
        public int leftRevision;
        public int rightRevision;
        public DoorMotionCommand leftCommand;
        public DoorMotionCommand rightCommand;
        public bool openingInhibited;
    }

    public static class DoorControlLogic
    {
        public static void Calculate(DoorControlContext context)
        {
            bool validSpeed = context.hasSpeed && DoorSimulationLogic.IsFinite(context.speedMps) && context.speedMps >= 0f;
            bool speedAllowed = validSpeed && DoorSimulationLogic.IsFinite(context.maximumOpeningSpeedMps) &&
                context.maximumOpeningSpeedMps >= 0f && context.speedMps <= context.maximumOpeningSpeedMps;
            context.openingInhibited = !speedAllowed;
            Resolve(ref context.leftCommand, context.hasLeftRequest, context.leftRequest, context.leftRequestRevision,
                ref context.hasLeftRevision, ref context.leftRevision, speedAllowed && context.leftOpenPermitted);
            Resolve(ref context.rightCommand, context.hasRightRequest, context.rightRequest, context.rightRequestRevision,
                ref context.hasRightRevision, ref context.rightRevision, speedAllowed && context.rightOpenPermitted);
        }

        private static void Resolve(ref DoorMotionCommand output, bool hasRequest, DoorMotionCommand request,
            int revision, ref bool hasRevision, ref int previousRevision, bool canOpen)
        {
            if (!hasRequest)
            {
                // 通信/割当/指令が失われたら停止。既読の開指令は復帰後も再実行しない。
                output = DoorMotionCommand.Hold;
                return;
            }
            if (!hasRevision || revision != previousRevision) output = request;
            hasRevision = true;
            previousRevision = revision;
            // 拒否された開指令は保留しない。速度/許可復帰後は再操作が必要。
            if (output == DoorMotionCommand.Open && !canOpen) output = DoorMotionCommand.Hold;
        }
    }
}
