using System.Net;
using TimesheetCandidateTask.Api.Contracts;
using TimesheetCandidateTask.Api.Domain;

namespace TimesheetCandidateTask.Wpf.Application;

public interface ITimesheetWorkspace
{
    Task<TimesheetResponse> LoadAsync(CancellationToken cancellationToken = default);
    Task<TimesheetResponse> SaveAsync(TimesheetResponse timesheet, TimesheetStatus status, CancellationToken cancellationToken = default);
    Task<TimesheetResponse> AddEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<TimesheetResponse> UpdateCommentAsync(int lineId, DateTime date, string? comment, CancellationToken cancellationToken = default);
}

/// <summary>Ошибка, которую вернул API (400/404/409...). Message - текст для пользователя.</summary>
public sealed class TimesheetApiException : Exception
{
    public TimesheetApiException(string message, HttpStatusCode? statusCode = null) : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
