package timetable

import "gorm.io/gorm"

// The old globally shared, train-number-keyed data is intentionally not migrated.
// Keep equipment masters, train types and running patterns; start timetable editing empty.
// This reset runs once and is atomic. Subsequent starts preserve the new data.
func resetLegacyTimetableSchema(db *gorm.DB) error {
	if !db.Migrator().HasTable("timetable_trains") {
		return nil
	}
	var columns []struct{ Name string }
	if err := db.Raw("PRAGMA table_info(timetable_trains)").Scan(&columns).Error; err != nil {
		return err
	}
	for _, column := range columns {
		if column.Name == "id" {
			return nil
		}
	}
	return db.Transaction(func(tx *gorm.DB) error {
		for _, table := range []string{"timetable_point_routes", "timetable_points", "timetable_operation_trains", "timetable_train_links", "timetable_operation_links", "timetable_operations", "timetable_operation_plans", "timetable_trains", "timetables"} {
			if err := tx.Exec("DROP TABLE IF EXISTS " + table).Error; err != nil {
				return err
			}
		}
		return migrateModels(tx)
	})
}
