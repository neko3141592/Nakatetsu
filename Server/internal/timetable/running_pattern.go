package timetable

type RunningPattern struct {
	ID          string                `gorm:"primaryKey;not null;check:trim(id) <> ''" json:"id"`
	TrainTypeID string                `gorm:"not null;index" json:"trainTypeId"`
	DisplayName string                `gorm:"not null;check:trim(display_name) <> ''" json:"displayName"`
	TrainType   TrainType             `gorm:"belongsTo:TrainType;foreignKey:TrainTypeID;references:TrainTypeID;constraint:OnUpdate:CASCADE,OnDelete:CASCADE" json:"-"`
	Points      []RunningPatternPoint `gorm:"foreignKey:RunningPatternID;references:ID;constraint:OnUpdate:CASCADE,OnDelete:CASCADE" json:"points"`
}

type RunningPatternPoint struct {
	RunningPatternID       string        `gorm:"primaryKey;not null" json:"runningPatternId"`
	SequenceIndex          int           `gorm:"primaryKey;autoIncrement:false;check:sequence_index >= 0" json:"sequenceIndex"`
	StationID              string        `gorm:"not null" json:"stationId"`
	TrackCircuitID         string        `gorm:"not null" json:"trackCircuitId"`
	Handling               PointHandling `gorm:"type:text;not null;check:handling IN ('stop','pass')" json:"handling"`
	ArrivalOffsetSeconds   *int          `gorm:"check:arrival_offset_seconds IS NULL OR arrival_offset_seconds >= 0" json:"arrivalOffsetSeconds"`
	DepartureOffsetSeconds *int          `gorm:"check:(departure_offset_seconds IS NULL OR departure_offset_seconds >= 0) AND (arrival_offset_seconds IS NULL OR departure_offset_seconds IS NULL OR arrival_offset_seconds <= departure_offset_seconds)" json:"departureOffsetSeconds"`
	Station                Station       `gorm:"belongsTo:Station;foreignKey:StationID;references:StationID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"-"`
	TrackCircuit           TrackCircuit  `gorm:"belongsTo:TrackCircuit;foreignKey:TrackCircuitID;references:TrackCircuitID;constraint:OnUpdate:CASCADE,OnDelete:RESTRICT" json:"-"`
}
