using TimesheetCandidateTask.Api.Application;
using TimesheetCandidateTask.Api.Domain;
using Xunit;

namespace TimesheetCandidateTask.Tests;

public sealed class TimesheetPeriodTests
{
    [Fact]
    public void New_timesheet_starts_as_draft_with_no_lines()
    {
        var timesheet = new Timesheet
        {
            RetailId = Guid.NewGuid(),
            PeriodStart = new DateTime(2026, 2, 1)
        };

        Assert.Equal(TimesheetStatus.Draft, timesheet.Status);
        Assert.Empty(timesheet.Lines);
    }

    [Fact]
    public void Ordinary_workday_is_included_in_line_totals()
    {
        var line = new TimesheetLine
        {
            Days = new List<TimesheetDay>
            {
                new() { Date = new DateTime(2026, 2, 2), Hours = 8, DayType = TimesheetDayType.Workday }
            }
        };

        var totals = TimesheetCalculator.Calculate(line);

        Assert.Equal(8, totals.WorkedHours);
        Assert.Equal(0, totals.HolidayHours);
        Assert.Equal(8, totals.TotalHours);
    }

    [Fact]
    public void Day_preserves_its_absence_details()
    {
        var day = new TimesheetDay
        {
            Date = new DateTime(2026, 2, 3),
            Hours = 0,
            DayType = TimesheetDayType.Absence,
            AbsenceCode = "Б",
            Comment = "Больничный"
        };

        Assert.Equal("Б", day.AbsenceCode);
        Assert.Equal("Больничный", day.Comment);
    }
}
