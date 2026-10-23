package timetable

type PointHandling string

const (
	HandlingPass        PointHandling = "pass"
	HandlingStop        PointHandling = "stop"
	HandlingUnspecified PointHandling = "unspecified"
)

type Timetable struct {
	ID          string `gorm:"primaryKey;not null;check:trim(id) <> ''"`
	DisplayName string `gorm:"not null"`
	Revision    int64  `gorm:"not null;default:0;check:revision >= 0"`

	// ダイヤに含む全列車。運用への割当てとは独立して、列車定義を共有する。
	Trains []TimetableTrain `gorm:"many2many:timetable_train_links;foreignKey:ID;joinForeignKey:TimetableID;references:TrainNumber;joinReferences:TrainNumber;constraint:OnUpdate:CASCADE,OnDelete:CASCADE"`

	// 平日・休日など、複数のダイヤから同じ運用定義を共有する。
	Operations []TimetableOperation `gorm:"many2many:timetable_operation_links;foreignKey:ID;joinForeignKey:TimetableID;references:OperationNumber;joinReferences:OperationNumber;constraint:OnUpdate:CASCADE,OnDelete:CASCADE"`
}

// TimetableOperationはダイヤ間で共有する所定の運用定義。
type TimetableOperation struct {
	OperationNumber string `gorm:"primaryKey;not null;check:trim(operation_number) <> ''"`

	Trains []TimetableOperationTrain `gorm:"foreignKey:OperationNumber;references:OperationNumber;constraint:OnUpdate:CASCADE,OnDelete:CASCADE"`
}

// TimetableOperationTrainは運用内の列車とその順序を保持する。
// 同じ列車定義を別の運用へ組み込む場合も、順序はこの関連ごとに指定する。
type TimetableOperationTrain struct {
	OperationNumber string `gorm:"primaryKey;not null;uniqueIndex:idx_operation_train_sequence,priority:1;check:trim(operation_number) <> ''"`
	TrainNumber     string `gorm:"primaryKey;not null;check:trim(train_number) <> ''"`
	SequenceIndex   int    `gorm:"not null;uniqueIndex:idx_operation_train_sequence,priority:2;check:sequence_index >= 0"`

	// 関連行は列車を参照する。運用削除時も、共有する列車定義は削除しない。
	Train TimetableTrain `gorm:"belongsTo:Train;foreignKey:TrainNumber;references:TrainNumber;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT"`
}

// TimetableTrainは列番で識別する共通の列車定義。時刻・経路も共有される。
type TimetableTrain struct {
	TrainNumber string `gorm:"primaryKey;not null;check:trim(train_number) <> ''"`
	TrainTypeID string `gorm:"not null;index;check:trim(train_type_id) <> ''"`

	TrainType TrainType        `gorm:"belongsTo:TrainType;foreignKey:TrainTypeID;references:TrainTypeID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT"`
	Points    []TimetablePoint `gorm:"foreignKey:TrainNumber;references:TrainNumber;constraint:OnUpdate:CASCADE,OnDelete:CASCADE"`
}

type TimetablePoint struct {
	TimetablePointID int64         `gorm:"primaryKey"`
	TrainNumber      string        `gorm:"not null;uniqueIndex:idx_train_point_sequence,priority:1;check:trim(train_number) <> ''"`
	SequenceIndex    int           `gorm:"not null;uniqueIndex:idx_train_point_sequence,priority:2;check:sequence_index >= 0"`
	StationID        *string       `gorm:"index;check:station_id IS NULL OR trim(station_id) <> ''"`
	TrackCircuitID   string        `gorm:"not null;index;check:trim(track_circuit_id) <> ''"`
	Handling         PointHandling `gorm:"type:text;default:'unspecified';not null;check:handling IN ('pass','stop','unspecified')"`

	// 運行日0時からの秒数。nilと0時を区別し、24時以降も保持する。
	ArrivalSeconds   *int `gorm:"check:arrival_seconds IS NULL OR arrival_seconds >= 0"`
	DepartureSeconds *int `gorm:"check:(departure_seconds IS NULL OR departure_seconds >= 0) AND (arrival_seconds IS NULL OR departure_seconds IS NULL OR arrival_seconds <= departure_seconds)"`

	Station      *Station              `gorm:"belongsTo:Station;foreignKey:StationID;references:StationID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT"`
	TrackCircuit TrackCircuit          `gorm:"belongsTo:TrackCircuit;foreignKey:TrackCircuitID;references:TrackCircuitID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT"`
	Routes       []TimetablePointRoute `gorm:"foreignKey:TimetablePointID;references:TimetablePointID;constraint:OnUpdate:CASCADE,OnDelete:CASCADE"`
}

type TimetablePointRoute struct {
	TimetablePointID int64  `gorm:"primaryKey;autoIncrement:false;not null"`
	SequenceIndex    int    `gorm:"primaryKey;autoIncrement:false;not null;check:sequence_index >= 0"`
	ControlStationID string `gorm:"not null;check:trim(control_station_id) <> ''"`
	RouteID          string `gorm:"not null;check:trim(route_id) <> ''"`

	Route Route `gorm:"belongsTo:Route;foreignKey:ControlStationID,RouteID;references:ControlStationID,RouteID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT"`
}

type TrainType struct {
	TrainTypeID string `gorm:"primaryKey;not null;check:trim(train_type_id) <> ''"`
	DisplayName string `gorm:"not null"`
}

type Station struct {
	StationID   string `gorm:"primaryKey;not null;check:trim(station_id) <> ''"`
	DisplayName string `gorm:"not null"`
}

// TrackCircuitとRouteは設備IDの参照カタログ。連動条件は設備定義側が持つ。
type TrackCircuit struct {
	TrackCircuitID string `gorm:"primaryKey;not null;check:trim(track_circuit_id) <> ''"`
}

type Route struct {
	ControlStationID string `gorm:"primaryKey;not null;check:trim(control_station_id) <> ''"`
	RouteID          string `gorm:"primaryKey;not null;check:trim(route_id) <> ''"`
}

type StationRunningTime struct {
	TrainTypeID    string `gorm:"primaryKey;not null"`
	FromStationID  string `gorm:"primaryKey;not null"`
	ToStationID    string `gorm:"primaryKey;not null"`
	RunningSeconds int    `gorm:"not null;check:running_seconds > 0"`

	TrainType   TrainType `gorm:"belongsTo:TrainType;foreignKey:TrainTypeID;references:TrainTypeID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT"`
	FromStation Station   `gorm:"belongsTo:FromStation;foreignKey:FromStationID;references:StationID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT"`
	ToStation   Station   `gorm:"belongsTo:ToStation;foreignKey:ToStationID;references:StationID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT"`
}
