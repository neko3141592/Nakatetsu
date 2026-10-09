using System;

namespace Nakatetsu.Track.Interlocking.Management
{
    public static class TrackInterlockingManagementLogic
    {
        public static void Reset(TrackInterlockingManagementContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.State.IsInitialized = false;
            TrackInterlockingManagementOutputLogic.ResetOutput(context);
        }

        public static void Initialize(TrackInterlockingManagementContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.State.IsInitialized = true;
            TrackInterlockingManagementOutputLogic.ResetOutput(context);
        }

        public static void Calculate(TrackInterlockingManagementContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            TrackInterlockingManagementOutputLogic.UpdateOutput(context);
        }
    }
}
