using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using TimesheetCandidateTask.Api.Contracts;
using TimesheetCandidateTask.Api.Domain;

namespace TimesheetCandidateTask.Wpf.Application;

public sealed class ApiTimesheetWorkspace : ITimesheetWorkspace
{
    private readonly HttpClient _http;
    private readonly Guid _retailId;
    private readonly DateTime _periodStart;

    public ApiTimesheetWorkspace(HttpClient http, Guid retailId, DateTime periodStart)
    {
        _http = http;
        _retailId = retailId;
        _periodStart = periodStart.Date;
    }

    public async Task<TimesheetResponse> LoadAsync(CancellationToken cancellationToken = default)
    {
        var url = $"api/timesheets?retailId={_retailId}&periodStart={Format(_periodStart)}";
        using var response = await _http.GetAsync(url, cancellationToken);
        return await ReadAsync(response, cancellationToken);
    }

    public async Task<TimesheetResponse> SaveAsync(TimesheetResponse timesheet, TimesheetStatus status, CancellationToken cancellationToken = default)
    {
        var request = new SaveTimesheetRequest(
            timesheet.RetailId,
            timesheet.PeriodStart,
            status,
            timesheet.Lines
                .Select(line => new SaveTimesheetLineRequest(
                    line.EmployeeId,
                    line.PositionId,
                    line.EmploymentType,
                    line.IsNight,
                    line.Days
                        .Select(day => new SaveTimesheetDayRequest(day.Date, day.Hours, day.DayType, day.AbsenceCode, day.Comment))
                        .ToList()))
                .ToList());

        using var response = await _http.PutAsJsonAsync("api/timesheets", request, cancellationToken);
        return await ReadAsync(response, cancellationToken);
    }

    public async Task<TimesheetResponse> AddEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        // Экран показывает два дня (01.01 и 02.01), поэтому новая строка создаётся с этими днями и нулевыми часами.
        var request = new AddTimesheetLineRequest(
            employeeId,
            EmployeeDirectory.DefaultPositionId,
            EmploymentType.Main,
            false,
            new[]
            {
                new SaveTimesheetDayRequest(_periodStart, 0, TimesheetDayType.Workday, null, null),
                new SaveTimesheetDayRequest(_periodStart.AddDays(1), 0, TimesheetDayType.Workday, null, null)
            });

        using var response = await _http.PostAsJsonAsync($"api/timesheets/{_retailId}/{Format(_periodStart)}/lines", request, cancellationToken);
        return await ReadAsync(response, cancellationToken);
    }

    public async Task<TimesheetResponse> UpdateCommentAsync(int lineId, DateTime date, string? comment, CancellationToken cancellationToken = default)
    {
        var request = new UpdateDayCommentRequest(date.Date, comment);

        using var response = await _http.PutAsJsonAsync($"api/timesheets/{_retailId}/{Format(_periodStart)}/lines/{lineId}/comment", request, cancellationToken);
        return await ReadAsync(response, cancellationToken);
    }

    private static string Format(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<TimesheetResponse> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var timesheet = await response.Content.ReadFromJsonAsync<TimesheetResponse>(cancellationToken: cancellationToken);
            return timesheet ?? throw new TimesheetApiException("Сервер вернул пустой ответ.", response.StatusCode);
        }

        throw new TimesheetApiException(await ReadErrorAsync(response, cancellationToken), response.StatusCode);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(error?.Error))
            {
                return error!.Error;
            }
        }
        catch (JsonException)
        {
            // Тело ответа не в нашем формате (например, стандартный ProblemDetails) - используем общий текст.
        }
        catch (NotSupportedException)
        {
        }

        return $"Сервер вернул ошибку {(int)response.StatusCode} {response.ReasonPhrase}.";
    }
}