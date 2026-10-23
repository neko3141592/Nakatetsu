package prc



// CTCはPRC側が利用するCTCのインターフェース。
type CTC interface{}

type PRC struct {
	ctc CTC
	timetable TimetablePlan
}

func New(ctc CTC, timetalbe TimetablePlan) *PRC {
	return &PRC{
		ctc: ctc,
		timetable: timetalbe,
	}
	
}
