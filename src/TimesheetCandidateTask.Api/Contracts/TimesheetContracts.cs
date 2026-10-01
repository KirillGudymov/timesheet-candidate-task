using TimesheetCandidateTask.Api.Domain;

namespace TimesheetCandidateTask.Api.Contracts;

public sealed record SaveTimesheetRequest(Guid RetailId, DateTime PeriodStart, TimesheetStatus Status, IReadOnlyCollection<SaveTimesheetLineRequest> Lines);
public sealed record SaveTimesheetLineRequest(Guid EmployeeId, Guid PositionId, EmploymentType EmploymentType, bool IsNight, IReadOnlyCollection<SaveTimesheetDayRequest> Days);
public sealed record SaveTimesheetDayRequest(DateTime Date, decimal Hours, TimesheetDayType DayType, string? AbsenceCode, string? Comment);
public sealed record AddTimesheetLineRequest(Guid EmployeeId, Guid PositionId, EmploymentType EmploymentType, bool IsNight, IReadOnlyCollection<SaveTimesheetDayRequest> Days);
public sealed record UpdateDayCommentRequest(DateTime Date, string? Comment);

public sealed record TimesheetResponse(int Id, Guid RetailId, DateTime PeriodStart, TimesheetStatus Status, IReadOnlyCollection<TimesheetLineResponse> Lines);
public sealed record TimesheetLineResponse(int Id, Guid EmployeeId, Guid PositionId, EmploymentType EmploymentType, bool IsNight, TimesheetTotalsResponse Totals, IReadOnlyCollection<TimesheetDayResponse> Days);
public sealed record TimesheetDayResponse(DateTime Date, decimal Hours, TimesheetDayType DayType, string? AbsenceCode, string? Comment);
public sealed record TimesheetTotalsResponse(decimal WorkedHours, decimal HolidayHours, decimal TotalHours);
public sealed record ErrorResponse(string Error);
