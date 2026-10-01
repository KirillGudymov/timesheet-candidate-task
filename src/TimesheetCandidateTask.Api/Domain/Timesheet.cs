namespace TimesheetCandidateTask.Api.Domain;

public sealed class Timesheet
{
    public int Id { get; set; }
    public Guid RetailId { get; set; }
    public DateTime PeriodStart { get; set; }
    public TimesheetStatus Status { get; set; } = TimesheetStatus.Draft;
    public List<TimesheetLine> Lines { get; set; } = new();
    /// <summary>“окен оптимистичной блокировки: увеличиваетс€ при каждом изменении табел€.</summary>
    public int Version { get; set; }
}

public sealed class TimesheetLine
{
    public int Id { get; set; }
    public int TimesheetId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid PositionId { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public bool IsNight { get; set; }
    public List<TimesheetDay> Days { get; set; } = new();
}

public sealed class TimesheetDay
{
    public int Id { get; set; }
    public int TimesheetLineId { get; set; }
    public DateTime Date { get; set; }
    public decimal Hours { get; set; }
    public TimesheetDayType DayType { get; set; }
    public string? AbsenceCode { get; set; }
    public string? Comment { get; set; }
}

public enum TimesheetStatus
{
    Draft,
    Approved
}

public enum EmploymentType
{
    Main,
    PartTime,
    Contractor
}

public enum TimesheetDayType
{
    Workday,
    Weekend,
    Holiday,
    Absence
}
