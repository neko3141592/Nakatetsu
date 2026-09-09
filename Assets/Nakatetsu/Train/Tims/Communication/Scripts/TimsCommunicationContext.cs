using System;
using System.Collections.Generic;
using Nakatetsu.Train.Tims.Bus;

namespace Nakatetsu.Train.Tims.Communication
{
    /// <summary>Pure communication data. Source IDs must be stable and unique within one TIMS instance.</summary>
    public sealed class TimsCommunicationContext
    {
        public TimsCommunicationState State { get; } = new();
        public TimsCommunicationInput Input { get; } = new();
        public TimsCommunicationOutput Output { get; } = new();
        public TimsCommunicationWorkspace Workspace { get; } = new();
    }

    [Serializable]
    public sealed class TimsCommunicationState
    {
        public TimsBusState masterBus = new();
        public readonly List<TimsCarTerminalState> terminals = new();
    }

    [Serializable]
    public sealed class TimsCarTerminalState
    {
        public int carIndex;
        public TimsBusState localBus = new();
    }

    public sealed class TimsCommunicationInput
    {
        public readonly List<TimsTransmissionInput> sources = new();
    }

    public struct TimsTransmissionInput
    {
        public int sourceId;
        public int carIndex;
    }

    public sealed class TimsCommunicationOutput
    {
        public readonly List<int> availableSourceIds = new();
    }

    public sealed class TimsCommunicationWorkspace
    {
        internal readonly HashSet<int> sourceIds = new();
    }
}
