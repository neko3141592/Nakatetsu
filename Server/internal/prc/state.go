package prc

type TimetableState struct {
	TrainsByNumber  map[string]TrainState
	PointsByID      map[int64]PointState
	RouteUsagesByID map[RouteUsageID]RouteUsageState
}

type TrainState struct {
	// 現在照合しているダイヤ地点のID。nilは未照合。
	CurrentPointID *int64

	IsHeld      bool // PRCによる進路要求を保留している。
	IsCancelled bool // 計画上の列車を運休として扱っている。
}

type PointState struct {
	ActualArrivalSeconds   *float64
	ActualDepartureSeconds *float64
}

type RouteUsagePhase int

const (
	RouteUsageUnknown   RouteUsagePhase = iota
	RouteUsagePending                   // 未要求。
	RouteUsageRequested                 // 進路要求を送信済み。確保の確認はまだない。
	RouteUsageReserved                  // 進路確保を確認済み。信号の進行許可とは別。
	RouteUsageInUse                     // 対象列車が進入し、進路を使用中。
	RouteUsageCompleted                 // 対象列車による進路使用の完了を確認済み。
	RouteUsageCancelled                 // この使用予定を取り消した。設備側の解錠完了とは別。
)

type RouteUsageState struct {
	Phase RouteUsagePhase
}

type ApproachTrain struct {
	StationID       string
	TrackCircuitID  string
	TrackingTrainID string
}
