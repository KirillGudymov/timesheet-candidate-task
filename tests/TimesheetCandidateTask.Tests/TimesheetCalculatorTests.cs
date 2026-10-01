using TimesheetCandidateTask.Api.Application;
using TimesheetCandidateTask.Api.Domain;
using Xunit;

namespace TimesheetCandidateTask.Tests;

public sealed class TimesheetCalculatorTests
{
    [Fact]
    public void Holiday_hours_are_counted_once_in_total()
    {
        var line = Line(
            Day(1, 8, TimesheetDayType.Holiday),
            Day(2, 8, TimesheetDayType.Workday));

        var totals = TimesheetCalculator.Calculate(line);

        Assert.Equal(16, totals.TotalHours);
        Assert.Equal(16, totals.WorkedHours);
        Assert.Equal(8, totals.HolidayHours);
    }

    [Fact]
    public void Weekend_and_absence_days_do_not_add_hours()
    {
        var line = Line(
            Day(3, 0, TimesheetDayType.Weekend),
            Day(4, 0, TimesheetDayType.Absence),
            Day(5, 8, TimesheetDayType.Workday));

        var totals = TimesheetCalculator.Calculate(line);

        Assert.Equal(8, totals.TotalHours);
        Assert.Equal(0, totals.HolidayHours);
    }

    [Fact]
    public void Line_without_days_has_zero_totals()
    {
        var totals = TimesheetCalculator.Calculate(Line());

        Assert.Equal(0, totals.TotalHours);
    }

    private static TimesheetLine Line(params TimesheetDay[] days) => new() { Days = days.ToList() };

    private static TimesheetDay Day(int day, decimal hours, TimesheetDayType type) => new()
    {
        Date = new DateTime(2026, 1, day),
        Hours = hours,
        DayType = type
    };
}