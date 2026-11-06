package timetable

import (
	"errors"
	"strings"

	"github.com/google/uuid"
	"gorm.io/gorm"
)

type PointHandling string

const (
	HandlingPass        PointHandling = "pass"
	HandlingStop        PointHandling = "stop"
	HandlingUnspecified PointHandling = "unspecified"
)

type PointSection string

const (
	PointSectionService    PointSection = "service"
	PointSectionDepotExit  PointSection = "depot_exit"
	PointSectionDepotEntry PointSection = "depot_entry"
)

// Timetable owns its trains. Operation plans reference a timetable separately.
type Timetable struct {
	ID          string           `gorm:"primaryKey;not null" json:"id"`
	DisplayName string           `gorm:"not null" json:"displayName"`
	Revision    int64            `gorm:"not null;default:0;check:revision >= 0" json:"revision"`
	Trains      []TimetableTrain `gorm:"foreignKey:TimetableID;constraint:OnDelete:CASCADE" json:"trains"`
}

func (t *Timetable) BeforeCreate(_ *gorm.DB) error {
	if strings.TrimSpace(t.ID) == "" {
		return errors.New("timetable ID must not be empty")
	}
	return nil
}

type TimetableOperationPlan struct {
	ID          string               `gorm:"primaryKey;not null;uniqueIndex:idx_plan_scope,priority:1" json:"id"`
	TimetableID string               `gorm:"not null;index;uniqueIndex:idx_plan_scope,priority:2" json:"timetableId"`
	DisplayName string               `gorm:"not null" json:"displayName"`
	Timetable   Timetable            `gorm:"foreignKey:TimetableID;constraint:OnDelete:CASCADE" json:"-"`
	Operations  []TimetableOperation `gorm:"foreignKey:OperationPlanID;constraint:OnDelete:CASCADE" json:"operations"`
}

func (p *TimetableOperationPlan) BeforeCreate(_ *gorm.DB) error {
	if p.ID == "" {
		p.ID = uuid.NewString()
	}
	return nil
}

type TimetableOperation struct {
	ID              string                    `gorm:"primaryKey;not null;uniqueIndex:idx_operation_scope,priority:1" json:"id"`
	OperationPlanID string                    `gorm:"not null;uniqueIndex:idx_operation_number,priority:1;uniqueIndex:idx_operation_scope,priority:2" json:"operationPlanId"`
	TimetableID     string                    `gorm:"not null;uniqueIndex:idx_operation_scope,priority:3" json:"timetableId"`
	OperationNumber string                    `gorm:"not null;uniqueIndex:idx_operation_number,priority:2;check:trim(operation_number) <> ''" json:"operationNumber"`
	Plan            TimetableOperationPlan    `gorm:"foreignKey:OperationPlanID,TimetableID;references:ID,TimetableID;constraint:OnDelete:CASCADE" json:"-"`
	Trains          []TimetableOperationTrain `gorm:"foreignKey:OperationID;constraint:OnDelete:CASCADE" json:"trains"`
}

func (o *TimetableOperation) BeforeCreate(_ *gorm.DB) error {
	if o.ID == "" {
		o.ID = uuid.NewString()
	}
	return nil
}

// Scope columns enforce membership and prevent assigning a train twice in one plan.
type TimetableOperationTrain struct {
	OperationID     string             `gorm:"primaryKey;not null;uniqueIndex:idx_operation_train_sequence,priority:1" json:"operationId"`
	TrainID         string             `gorm:"primaryKey;not null;uniqueIndex:idx_plan_train,priority:2" json:"trainId"`
	OperationPlanID string             `gorm:"not null;uniqueIndex:idx_plan_train,priority:1" json:"operationPlanId"`
	TimetableID     string             `gorm:"not null" json:"timetableId"`
	SequenceIndex   int                `gorm:"not null;uniqueIndex:idx_operation_train_sequence,priority:2;check:sequence_index >= 0" json:"sequenceIndex"`
	Operation       TimetableOperation `gorm:"foreignKey:OperationID,OperationPlanID,TimetableID;references:ID,OperationPlanID,TimetableID;constraint:OnDelete:CASCADE" json:"-"`
	Train           TimetableTrain     `gorm:"foreignKey:TrainID,TimetableID;references:ID,TimetableID;constraint:OnDelete:CASCADE" json:"train"`
}

// Train numbers are labels; UUIDs identify trains even when numbers repeat.
type TimetableTrain struct {
	ID                   string           `gorm:"primaryKey;not null;uniqueIndex:idx_train_scope,priority:1" json:"id"`
	TimetableID          string           `gorm:"not null;index;uniqueIndex:idx_train_scope,priority:2" json:"timetableId"`
	TrainNumber          string           `gorm:"not null;index;check:trim(train_number) <> ''" json:"trainNumber"`
	Timetable            Timetable        `gorm:"foreignKey:TimetableID;constraint:OnDelete:CASCADE" json:"-"`
	TrainTypeID          string           `gorm:"default:null;index;check:trim(train_type_id) <> ''" json:"trainTypeId"`
	OriginStationID      string           `gorm:"default:null;index;check:trim(origin_station_id) <> ''" json:"originStationId"`
	DestinationStationID string           `gorm:"default:null;index;check:trim(destination_station_id) <> ''" json:"destinationStationId"`
	OriginStation        Station          `gorm:"foreignKey:OriginStationID;references:StationID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"-"`
	DestinationStation   Station          `gorm:"foreignKey:DestinationStationID;references:StationID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"-"`
	TrainType            TrainType        `gorm:"belongsTo:TrainType;foreignKey:TrainTypeID;references:TrainTypeID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"trainType"`
	Points               []TimetablePoint `gorm:"foreignKey:TrainID;references:ID;constraint:OnDelete:CASCADE" json:"points"`
}

func (t *TimetableTrain) BeforeCreate(_ *gorm.DB) error {
	if t.ID == "" {
		t.ID = uuid.NewString()
	}
	return nil
}

type TimetablePoint struct {
	TimetablePointID int64         `gorm:"primaryKey" json:"timetablePointId"`
	TrainID          string        `gorm:"not null;uniqueIndex:idx_train_point_sequence,priority:1" json:"trainId"`
	SequenceIndex    int           `gorm:"not null;uniqueIndex:idx_train_point_sequence,priority:2;check:sequence_index >= 0" json:"sequenceIndex"`
	Section          PointSection  `gorm:"type:text;not null;default:'service';check:section IN ('service','depot_exit','depot_entry')" json:"section"`
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
	Color       string `gorm:"not null;default:'#2563eb'" json:"color"`
}

const DefaultTrainTypeColor = "#2563eb"

type TrainTypeStation struct {
	TrainTypeID string        `gorm:"primaryKey;not null" json:"trainTypeId"`
	StationID   string        `gorm:"primaryKey;not null" json:"stationId"`
	Handling    PointHandling `gorm:"type:text;not null;check:handling IN ('stop','pass')" json:"handling"`
	TrainType   TrainType     `gorm:"belongsTo:TrainType;foreignKey:TrainTypeID;references:TrainTypeID;constraint:OnUpdate:CASCADE,OnDelete:CASCADE" json:"-"`
	Station     Station       `gorm:"belongsTo:Station;foreignKey:StationID;references:StationID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"-"`
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
