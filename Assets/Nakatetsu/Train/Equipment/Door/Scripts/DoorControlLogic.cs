using Nakatetsu.Train.Simulation.Door;

namespace Nakatetsu.Train.Equipment.Door
{
    public interface IDoorInputSource
    {
        bool TryGetSpeedMps(out float speedMps);
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
            Resolve(ref context.leftCommand, context.hasLeftRequest, context.leftRequest, speedAllowed && context.leftOpenPermitted);
            Resolve(ref context.rightCommand, context.hasRightRequest, context.rightRequest, speedAllowed && context.rightOpenPermitted);
        }

        private static void Resolve(ref DoorMotionCommand output, bool hasRequest, DoorMotionCommand request, bool canOpen)
        {
            if (hasRequest) output = request;
            // 拒否された開指令は保留しない。速度/許可復帰後は再操作が必要。
            if (output == DoorMotionCommand.Open && !canOpen) output = DoorMotionCommand.Hold;
        }
    }
}
