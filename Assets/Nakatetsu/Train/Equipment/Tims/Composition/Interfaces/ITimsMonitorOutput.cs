using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Tims.Communication;
using UnityEngine;

namespace Nakatetsu.Train.Equipment.Tims
{
    public interface ITimsMonitorOutput
    {
        int ScreenCount { get; }
        void Initialize(TimsCommunicationController source, IReadOnlyList<RenderTexture> textures);
    }
}
