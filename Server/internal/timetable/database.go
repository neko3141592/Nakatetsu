package timetable

import (

	"gorm.io/driver/sqlite"
	"gorm.io/gorm"
	"gorm.io/gorm/logger"
)

func MigrateModels(db *gorm.DB) error {
	return db.AutoMigrate(
		&TrainType{}, &Station{}, &TrackCircuit{}, &Route{},
		&TimetableTrain{}, &TimetablePoint{}, &TimetablePointRoute{},
		&TimetableOperation{}, &TimetableOperationTrain{}, &Timetable{},
		&StationRunningTime{},
	)
}

func OpenDatabase(path string) (*gorm.DB, error) {
	db, err := gorm.Open(sqlite.Open(path+"?_foreign_keys=on&_busy_timeout=5000"), &gorm.Config{
		Logger: logger.Default.LogMode(logger.Silent),
	})
	if err != nil {
		return nil, err
	}

	sqlDB, err := db.DB()
	if err != nil {
		return nil, err
	}

	sqlDB.SetMaxOpenConns(1)
	if err := MigrateModels(db); err != nil {
		_ = sqlDB.Close()
		return nil, err
	}

	return db, nil
}
