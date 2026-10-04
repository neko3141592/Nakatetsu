using System;
using System.Collections.Generic;
using Nakatetsu.Train.Equipment.Tims.Bus;

namespace Nakatetsu.Train.Equipment.Tims.Communication
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
        // 各車の機器からLocalBusへの収集状態。
        public bool hasCollectedSources;
        public double collectionElapsedSeconds;
        // 編成共通の機器からMasterBusへの収集状態。
        public bool hasCollectedMasterSources;
        public double masterCollectionElapsedSeconds;
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
