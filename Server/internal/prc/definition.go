package prc

type TimetablePlan struct {
	TimetableID string
	DisplayName string
	Revision    int64

	// 運用に所属していない列車も含む、ダイヤ内の全列車。
	TrainsByNumber map[string]TrainPlan

	OperationsByNumber map[string]OperationPlan
	// 起動時に作る、競合点ごとの計画時刻順の使用予定。
	ConflictUsagesByCircuitID map[string][]ConflictUsage
}

type OperationPlan struct {
	OperationNumber string

	TrainNumbers []string
}

type TrainPlan struct {
	TrainNumber string
	TrainTypeID string

	Points []PointPlan
}

type PointHandling string

const (
	HandlingPass        PointHandling = "pass"
	HandlingStop        PointHandling = "stop"
	HandlingUnspecified PointHandling = "unspecified"
)

type PointPlan struct {
	TimetablePointID int64
	SequenceIndex    int
	StationID        *string
	TrackCircuitID   string
	Handling         PointHandling

	ArrivalSeconds   *int
	DepartureSeconds *int

	ArrivalRoutes   []RouteUsage // この駅への場内進路。進行方向順。
	DepartureRoutes []RouteUsage // この駅からの出発進路。進行方向順。
	ShuntingRoutes  []RouteUsage // この地点から次の地点への入換進路。
}

// ダイヤ上の「1回の進路使用予定」を識別するID
type RouteUsageID struct {
	TimetablePointID   int64
	RouteSequenceIndex int
}

type RouteUsage struct {
	ID               RouteUsageID
	TrainNumber      string
	ControlStationID string
	RouteID          string
}

type ConflictUsage struct {
	Usage            RouteUsage
	ScheduledSeconds int
}

// 軌道回路接近点
type ApproachPoint struct {
	ID             int64
	StationID      string
	TrackCircuitID string
}
