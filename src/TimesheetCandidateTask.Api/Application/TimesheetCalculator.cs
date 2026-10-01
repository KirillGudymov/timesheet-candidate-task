using TimesheetCandidateTask.Api.Contracts;
using TimesheetCandidateTask.Api.Domain;

namespace TimesheetCandidateTask.Api.Application;

public static class TimesheetCalculator
{
    public static TimesheetTotalsResponse Calculate(TimesheetLine line)
    {
        var workedHours = line.Days
            .Where(day => day.DayType is TimesheetDayType.Workday or TimesheetDayType.Holiday)
            .Sum(day => day.Hours);
        var holidayHours = line.Days
            .Where(day => day.DayType == TimesheetDayType.Holiday)
            .Sum(day => day.Hours);

        return new TimesheetTotalsResponse(workedHours, holidayHours, workedHours);
    }
}
