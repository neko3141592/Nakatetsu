package prc

// TimetableStateは有効化した1つのTimetablePlanに対応する実行時状態。
// 定義と同じキーで参照する。各mapは状態生成時に初期化し、
// 未登録のキーをゼロ値の状態として扱わず、存在を確認して参照する。
type TimetableState struct {
	TrainsByNumber  map[string]TrainState
	PointsByID      map[int64]PointState
	RouteUsagesByID map[RouteUsageID]RouteUsageState
}

// TrainStateは計画上の列車に対する割当てと進捗。
// 実際の在線・編成位置はCTCが管理する。
type TrainState struct {
	// CTCの追跡用TrainID。列番やUnityのConsistIDとは別で、nilは未割当。
	TrackingTrainID *string

	// 現在の運用割当て。所定運用から変更でき、nilは運用未所属。
	OperationNumber *string

	// 現在照合しているダイヤ地点のID。nilは未照合。
	CurrentPointID *int64

	IsHeld      bool // PRCによる進路要求を保留している。
	IsCancelled bool // 計画上の列車を運休として扱っている。
}

// PointStateは地点の実績時刻。
// 計画と同じ運行日0時基準のシミュレーション秒で、小数秒と24時以降も保持する。
// nilは未観測であり、0秒の実績とは区別する。
type PointState struct {
	ActualArrivalSeconds   *float64
	ActualDepartureSeconds *float64
}

type RouteUsagePhase int

const (
	// ゼロ値を未要求とみなさず、初期化前や不明な状態を区別する。
	RouteUsageUnknown   RouteUsagePhase = iota
	RouteUsagePending                   // 未要求。
	RouteUsageRequested                 // 進路要求を送信済み。確保の確認はまだない。
	RouteUsageReserved                  // 進路確保を確認済み。信号の進行許可とは別。
	RouteUsageInUse                     // 対象列車が進入し、進路を使用中。
	RouteUsageCompleted                 // 対象列車による進路使用の完了を確認済み。
	RouteUsageCancelled                 // この使用予定を取り消した。設備側の解錠完了とは別。
)

// RouteUsageStateは1回の進路使用予定の進捗。
// 進路確保や取消要求の送信だけでは使用完了にしない。
type RouteUsageState struct {
	Phase RouteUsagePhase
}
