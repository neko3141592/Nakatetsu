namespace Nakatetsu.Application.Simulation
{
    // 完了Tickでコピーする値。通信中の列車更新とは切り離して保持する。
    public readonly struct TrainInitializationInfo
    {
        public string ConsistId { get; }
        public string ConsistNumber { get; }
        public string TrainNumber { get; }
        public string OperationNumber { get; }
        public string ReferenceCircuitId { get; }

        public TrainInitializationInfo(string consistId, string consistNumber, string trainNumber,
            string operationNumber, string referenceCircuitId)
        {
            ConsistId = consistId;
            ConsistNumber = consistNumber;
            TrainNumber = trainNumber;
            OperationNumber = operationNumber;
            ReferenceCircuitId = referenceCircuitId;
        }
    }
}
