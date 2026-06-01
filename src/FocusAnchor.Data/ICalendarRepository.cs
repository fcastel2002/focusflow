using FocusAnchor.Core;

namespace FocusAnchor.Data;

public interface ICalendarRepository
{
    IReadOnlyList<FocusCalendar> GetCalendars();

    FocusCalendar SaveCalendar(FocusCalendar calendar);

    void DeleteCalendar(long calendarId);

    IReadOnlyList<FocusPlan> GetPlans(DateOnly date);

    FocusPlan SavePlan(FocusPlan plan);

    void DeletePlan(long planId);

    DailyGoal? GetDailyGoal(long calendarId, DateOnly date);

    void SetDailyGoal(DailyGoal goal);

    DailyAttentionSummary GetDailySummary(DateOnly date);
}
