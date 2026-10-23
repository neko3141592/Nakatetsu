package prc

type TimetablePlan struct {
	TimetableID string
	DisplayName string
	Revision    int64

	// 運用に所属していない列車も含む、ダイヤ内の全列車。
	TrainsByNumber map[string]TrainPlan

	OperationsByNumber map[string]OperationPlan
	RouteUsagesByID    map[RouteUsageID]RouteUsage
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

	RouteUsageIDs []RouteUsageID
}

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
