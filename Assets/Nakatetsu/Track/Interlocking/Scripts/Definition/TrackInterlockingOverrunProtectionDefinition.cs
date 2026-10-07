using System;
using System.Collections.Generic;

namespace Nakatetsu.Track.Interlocking
{
    [Serializable]
    public sealed class TrackInterlockingOverrunProtectionDefinition
    {
        // 過走防護が有効か。有効でない場合には防護方式「None」を採用
        public bool isEnabled;

        public List<TurnoutRequirement> turnoutRequirements = new();

        public float releaseSeconds;

    }
}