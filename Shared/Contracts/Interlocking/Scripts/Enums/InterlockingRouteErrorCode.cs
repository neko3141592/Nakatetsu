namespace Nakatetsu.Contracts.Interlocking
{
    /// <summary>進路要求を拒否した理由。数値は追加後も変更しない。</summary>
    public enum InterlockingRouteErrorCode
    {
        /// <summary>拒否理由なし。要求を受付。</summary>
        None = 0,
        /// <summary>必須IDや操作種別などの要求内容が不正。</summary>
        InvalidRequest = 1,
        /// <summary>対象の駅が登録されていない。</summary>
        UnknownStation = 2,
        /// <summary>対象の進路が定義されていない。</summary>
        UnknownRoute = 3,
        /// <summary>要求処理に必要な装置が未初期化。</summary>
        NotInitialized = 4,
        /// <summary>必要な設備情報を取得できない。</summary>
        InputUnavailable = 5,
        /// <summary>設定対象の進路が設定済み。</summary>
        AlreadySet = 6,
        /// <summary>取消対象の進路が未設定。</summary>
        NotSet = 7,
        /// <summary>設定照査に必要な軌道回路が在線。</summary>
        CircuitOccupied = 8,
        /// <summary>必要な軌道回路を他進路が予約中。</summary>
        CircuitReserved = 9,
        /// <summary>他進路と競合。</summary>
        RouteConflict = 10,
        /// <summary>転轍機の要求位置が他進路の保持位置と競合。</summary>
        TurnoutPositionConflict = 11
    }
}
