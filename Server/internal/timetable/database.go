package timetable

import (
	"gorm.io/driver/sqlite"
	"gorm.io/gorm"
	"gorm.io/gorm/logger"
)

func MigrateModels(db *gorm.DB) error {
	return db.Connection(func(tx *gorm.DB) (err error) {
		var enabled int
		if err := tx.Raw("PRAGMA foreign_keys").Scan(&enabled).Error; err != nil {
			return err
		}
		if enabled == 1 {
			if err := tx.Exec("PRAGMA foreign_keys = OFF").Error; err != nil {
				return err
			}
			defer func() {
				if restoreErr := tx.Exec("PRAGMA foreign_keys = ON").Error; err == nil {
					err = restoreErr
				}
			}()
		}
		return migrateModels(tx)
	})
}

func migrateModels(db *gorm.DB) error {
	if err := db.AutoMigrate(
		&TrainType{}, &Station{}, &TrackCircuit{}, &Route{}, &StationTrack{},
		&TimetableTrain{}, &TimetablePoint{}, &TimetablePointRoute{},
		&TimetableOperation{}, &TimetableOperationTrain{}, &Timetable{},
		&StationRunningTime{},
	); err != nil {
		return err
	}
	var keys []struct {
		Table    string
		From     string
		OnDelete string
	}
	if err := db.Raw("PRAGMA foreign_key_list(station_running_times)").Scan(&keys).Error; err != nil {
		return err
	}
	for _, key := range keys {
		if key.Table == "train_types" && key.From == "train_type_id" && key.OnDelete != "CASCADE" {
			return db.Migrator().CreateConstraint(&StationRunningTime{}, "TrainType")
		}
	}
	return nil
}

func OpenDatabase(path string) (*gorm.DB, error) {
	db, err := gorm.Open(sqlite.Open(path+"?_foreign_keys=on&_busy_timeout=5000"), &gorm.Config{
		Logger:         logger.Default.LogMode(logger.Silent),
		TranslateError: true,
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
