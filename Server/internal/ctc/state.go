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

type TrackCircuitState struct {
	IsOccupied bool
	TrainID    string // 最後尾側の基準回路にだけ保持する。未登録なら空文字。
}

type TrackingState struct {
	// 全回路と全列車の情報を受信し、初期登録済み。
	IsInitialized bool

	// 直前に処理した在線状態と、回路に登録された追跡用ID。
	CircuitsByID map[string]TrackCircuitState

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
}
