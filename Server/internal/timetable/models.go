package timetable

import (
	"errors"
	"strings"

	"gorm.io/gorm"
)

type PointHandling string

const (
	HandlingPass        PointHandling = "pass"
	HandlingStop        PointHandling = "stop"
	HandlingUnspecified PointHandling = "unspecified"
)

type Timetable struct {
	ID          string `gorm:"primaryKey;not null" json:"id"`
	DisplayName string `gorm:"not null" json:"displayName"`
	Revision    int64  `gorm:"not null;default:0;check:revision >= 0" json:"revision"`

	// DBでは臨時列車の紐づけ。LoadTimetableでは運用の所属列車も含める。
	Trains                []TimetableTrain `gorm:"many2many:timetable_train_links;foreignKey:ID;joinForeignKey:TimetableID;references:TrainNumber;joinReferences:TrainNumber;constraint:OnUpdate:CASCADE,OnDelete:CASCADE" json:"trains"`
	TemporaryTrainNumbers []string         `gorm:"-" json:"temporaryTrainNumbers"`

	// 平日・休日など、複数のダイヤから同じ運用定義を共有する。
	Operations []TimetableOperation `gorm:"many2many:timetable_operation_links;foreignKey:ID;joinForeignKey:TimetableID;references:OperationNumber;joinReferences:OperationNumber;constraint:OnUpdate:CASCADE,OnDelete:CASCADE" json:"operations"`
}

func (t *Timetable) BeforeCreate(_ *gorm.DB) error {
	if strings.TrimSpace(t.ID) == "" {
		return errors.New("timetable ID must not be empty")
	}
	return nil
}

// TimetableOperationはダイヤ間で共有する所定の運用定義。
type TimetableOperation struct {
	OperationNumber string `gorm:"primaryKey;not null;check:trim(operation_number) <> ''" json:"operationNumber"`

	Trains []TimetableOperationTrain `gorm:"foreignKey:OperationNumber;references:OperationNumber;constraint:OnUpdate:CASCADE,OnDelete:CASCADE" json:"trains"`
}

// TimetableOperationTrainは運用内の列車とその順序を保持する。
// 同じ列車定義を別の運用へ組み込む場合も、順序はこの関連ごとに指定する。
type TimetableOperationTrain struct {
	OperationNumber string `gorm:"primaryKey;not null;uniqueIndex:idx_operation_train_sequence,priority:1;check:trim(operation_number) <> ''" json:"operationNumber"`
	TrainNumber     string `gorm:"primaryKey;not null;check:trim(train_number) <> ''" json:"trainNumber"`
	SequenceIndex   int    `gorm:"not null;uniqueIndex:idx_operation_train_sequence,priority:2;check:sequence_index >= 0" json:"sequenceIndex"`

	// 関連行は列車を参照する。運用削除時も、共有する列車定義は削除しない。
	Train TimetableTrain `gorm:"belongsTo:Train;foreignKey:TrainNumber;references:TrainNumber;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"train"`
}

// TimetableTrainは列番で識別する共通の列車定義。時刻・経路も共有される。
type TimetableTrain struct {
	TrainNumber          string  `gorm:"primaryKey;not null;check:trim(train_number) <> ''" json:"trainNumber"`
	TrainTypeID          string  `gorm:"default:null;index;check:trim(train_type_id) <> ''" json:"trainTypeId"`
	OriginStationID      string  `gorm:"default:null;index;check:trim(origin_station_id) <> ''" json:"originStationId"`
	DestinationStationID string  `gorm:"default:null;index;check:trim(destination_station_id) <> ''" json:"destinationStationId"`
	OriginStation        Station `gorm:"belongsTo:OriginStation;foreignKey:OriginStationID;references:StationID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"-"`
	DestinationStation   Station `gorm:"belongsTo:DestinationStation;foreignKey:DestinationStationID;references:StationID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"-"`

	TrainType TrainType        `gorm:"belongsTo:TrainType;foreignKey:TrainTypeID;references:TrainTypeID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"trainType"`
	Points    []TimetablePoint `gorm:"foreignKey:TrainNumber;references:TrainNumber;constraint:OnUpdate:CASCADE,OnDelete:CASCADE" json:"points"`
}

type TimetablePoint struct {
	TimetablePointID int64         `gorm:"primaryKey" json:"timetablePointId"`
	TrainNumber      string        `gorm:"not null;uniqueIndex:idx_train_point_sequence,priority:1;check:trim(train_number) <> ''" json:"trainNumber"`
	SequenceIndex    int           `gorm:"not null;uniqueIndex:idx_train_point_sequence,priority:2;check:sequence_index >= 0" json:"sequenceIndex"`
	StationID        *string       `gorm:"index;check:station_id IS NULL OR trim(station_id) <> ''" json:"stationId"`
	TrackCircuitID   string        `gorm:"not null;index;check:trim(track_circuit_id) <> ''" json:"trackCircuitId"`
	Handling         PointHandling `gorm:"type:text;default:'unspecified';not null;check:handling IN ('pass','stop','unspecified')" json:"handling"`

	// 運行日0時からの秒数。nilと0時を区別し、24時以降も保持する。
	ArrivalSeconds   *int `gorm:"check:arrival_seconds IS NULL OR arrival_seconds >= 0" json:"arrivalSeconds"`
	DepartureSeconds *int `gorm:"check:(departure_seconds IS NULL OR departure_seconds >= 0) AND (arrival_seconds IS NULL OR departure_seconds IS NULL OR arrival_seconds <= departure_seconds)" json:"departureSeconds"`

	Station      *Station              `gorm:"belongsTo:Station;foreignKey:StationID;references:StationID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"station"`
	TrackCircuit TrackCircuit          `gorm:"belongsTo:TrackCircuit;foreignKey:TrackCircuitID;references:TrackCircuitID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"trackCircuit"`
	Routes       []TimetablePointRoute `gorm:"foreignKey:TimetablePointID;references:TimetablePointID;constraint:OnUpdate:CASCADE,OnDelete:CASCADE" json:"routes"`
}

type TimetablePointRoute struct {
	TimetablePointID int64  `gorm:"primaryKey;autoIncrement:false;not null" json:"timetablePointId"`
	SequenceIndex    int    `gorm:"primaryKey;autoIncrement:false;not null;check:sequence_index >= 0" json:"sequenceIndex"`
	ControlStationID string `gorm:"not null;check:trim(control_station_id) <> ''" json:"controlStationId"`
	RouteID          string `gorm:"not null;check:trim(route_id) <> ''" json:"routeId"`

	Route Route `gorm:"belongsTo:Route;foreignKey:ControlStationID,RouteID;references:ControlStationID,RouteID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"route"`
}

type TrainType struct {
	TrainTypeID string `gorm:"primaryKey;not null;check:trim(train_type_id) <> ''" json:"trainTypeId"`
	DisplayName string `gorm:"not null" json:"displayName"`
}

type Station struct {
	StationID   string `gorm:"primaryKey;not null;check:trim(station_id) <> ''" json:"stationId"`
	DisplayName string `gorm:"not null" json:"displayName"`
}

type StationTrack struct {
	StationID      string `gorm:"primaryKey;not null;check:trim(station_id) <> ''" json:"stationId"`
	TrackID        string `gorm:"primaryKey;not null;check:trim(track_id) <> ''" json:"trackId"`
	DisplayName    string `gorm:"not null;check:trim(display_name) <> ''" json:"displayName"`
	TrackCircuitID string `gorm:"not null;index;check:trim(track_circuit_id) <> ''" json:"trackCircuitId"`

	Station      Station      `gorm:"belongsTo:Station;foreignKey:StationID;references:StationID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"-"`
	TrackCircuit TrackCircuit `gorm:"belongsTo:TrackCircuit;foreignKey:TrackCircuitID;references:TrackCircuitID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"-"`
}

// TrackCircuitとRouteは設備IDの参照カタログ。連動条件は設備定義側が持つ。
type TrackCircuit struct {
	TrackCircuitID string `gorm:"primaryKey;not null;check:trim(track_circuit_id) <> ''" json:"trackCircuitId"`
	DisplayName    string `gorm:"not null;default:''" json:"displayName"`
}

type Route struct {
	ControlStationID string `gorm:"primaryKey;not null;check:trim(control_station_id) <> ''" json:"controlStationId"`
	RouteID          string `gorm:"primaryKey;not null;check:trim(route_id) <> ''" json:"routeId"`
}

type StationRunningTime struct {
	TrainTypeID    string `gorm:"primaryKey;not null" json:"trainTypeId"`
	FromStationID  string `gorm:"primaryKey;not null" json:"fromStationId"`
	ToStationID    string `gorm:"primaryKey;not null" json:"toStationId"`
	RunningSeconds int    `gorm:"not null;check:running_seconds > 0" json:"runningSeconds"`

	TrainType   TrainType `gorm:"belongsTo:TrainType;foreignKey:TrainTypeID;references:TrainTypeID;constraint:OnUpdate:CASCADE,OnDelete:CASCADE" json:"trainType"`
	FromStation Station   `gorm:"belongsTo:FromStation;foreignKey:FromStationID;references:StationID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"fromStation"`
	ToStation   Station   `gorm:"belongsTo:ToStation;foreignKey:ToStationID;references:StationID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"toStation"`
}
