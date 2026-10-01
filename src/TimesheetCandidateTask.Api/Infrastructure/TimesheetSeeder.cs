using TimesheetCandidateTask.Api.Domain;

namespace TimesheetCandidateTask.Api.Infrastructure;

public static class TimesheetSeeder
{
    public static readonly Guid RetailId = Guid.Parse("c4335485-a459-424f-90fc-8e8e4445f121");
    public static readonly Guid EmployeeId = Guid.Parse("e8abb9fa-b564-41ae-8d1f-fbbc99c0f54e");

    public static void Seed(TimesheetDbContext db)
    {
        if (db.Timesheets.Any())
        {
            return;
        }

        var line = new TimesheetLine
        {
            EmployeeId = EmployeeId,
            PositionId = Guid.Parse("7b442775-f9ee-4cf5-b3c3-75687650a81f"),
            EmploymentType = EmploymentType.Main,
            Days = new List<TimesheetDay>
            {
                new() { Date = new DateTime(2026, 1, 1), Hours = 8, DayType = TimesheetDayType.Holiday, Comment = "Инвентаризация" },
                new() { Date = new DateTime(2026, 1, 2), Hours = 8, DayType = TimesheetDayType.Workday },
                new() { Date = new DateTime(2026, 1, 3), Hours = 0, DayType = TimesheetDayType.Weekend }
            }
        };

        db.Timesheets.Add(new Timesheet
        {
            RetailId = RetailId,
            PeriodStart = new DateTime(2026, 1, 1),
            Status = TimesheetStatus.Draft,
            Lines = new List<TimesheetLine> { line }
        });
        db.SaveChanges();
    }
}
