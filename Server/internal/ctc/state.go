package ctc

// Unityから受信した進路の状態。
type InterlockingRouteState struct {
	IsAvailable     bool
	IsRouteSet      bool
	PathEstablished bool
	ProceedAllowed  bool
	RouteLocked     bool
	CancelPending   bool
}

type TrackingState struct {
	// 直前に処理した在線状態。
	OccupiedByCircuitID map[string]bool

	// CTCの追跡用IDをキーに管理する。
	TrainsByID map[string]TrainState

	// 引継ぎ元の回路ID → 引継ぎ先の回路ID。
	// 引継ぎが完了したら削除する。
	DynamicLinks map[string]string

	// 定義外の移動などを検知し、追跡の再設定が必要な状態。
	NeedsReset bool
}

type TrainState struct {
	ConsistID       *string // Unityの編成ID。未判明ならnil
	ConsistNumber   string
	TrainNumber     string
	OperationNumber string

	// 追跡上の基準となる、最後尾側の軌道回路。
	CircuitID string
}
