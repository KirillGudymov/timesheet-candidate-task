using System.Net;
using System.Net.Http.Json;
using TimesheetCandidateTask.Api.Contracts;
using TimesheetCandidateTask.Api.Domain;
using TimesheetCandidateTask.Api.Infrastructure;
using Xunit;

namespace TimesheetCandidateTask.Tests;

public sealed class TimesheetApiTests : IClassFixture<TimesheetApiFactory>
{
    private static readonly DateTime Period = new(2026, 1, 1);
    private static readonly Guid PositionId = Guid.Parse("7b442775-f9ee-4cf5-b3c3-75687650a81f");
    private static readonly Guid EmployeeA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EmployeeB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly HttpClient _client;

    public TimesheetApiTests(TimesheetApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_returns_seeded_timesheet_with_correct_totals()
    {
        var response = await _client.GetAsync($"api/timesheets?retailId={TimesheetSeeder.RetailId}&periodStart=2026-01-01");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var line = Assert.Single((await Read(response)).Lines);
        Assert.Equal(16, line.Totals.TotalHours);
        Assert.Equal(8, line.Totals.HolidayHours);
    }

    [Fact]
    public async Task Get_returns_404_for_month_without_timesheet()
    {
        var response = await _client.GetAsync($"api/timesheets?retailId={TimesheetSeeder.RetailId}&periodStart=2026-02-01");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_400_when_period_is_not_first_day_of_month()
    {
        var response = await _client.GetAsync($"api/timesheets?retailId={TimesheetSeeder.RetailId}&periodStart=2026-01-15");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_creates_timesheet_that_can_be_read_back()
    {
        var retailId = Guid.NewGuid();

        var saved = await Create(retailId, TimesheetStatus.Draft, Line(EmployeeA, Day(1, 8, TimesheetDayType.Holiday), Day(2, 8)));
        var loaded = await _client.GetFromJsonAsync<TimesheetResponse>(GetUrl(retailId));

        Assert.NotNull(loaded);
        Assert.Equal(saved.Id, loaded!.Id);
        Assert.Equal(TimesheetStatus.Draft, loaded.Status);
        Assert.Equal(16, Assert.Single(loaded.Lines).Totals.TotalHours);
    }

    [Fact]
    public async Task Put_removes_lines_that_are_absent_from_request()
    {
        var retailId = Guid.NewGuid();
        await Create(retailId, TimesheetStatus.Draft, Line(EmployeeA, Day(1, 8)), Line(EmployeeB, Day(1, 4)));

        var saved = await Create(retailId, TimesheetStatus.Draft, Line(EmployeeB, Day(1, 4)));

        Assert.Equal(EmployeeB, Assert.Single(saved.Lines).EmployeeId);
    }

    [Fact]
    public async Task Put_rejects_duplicate_employee_in_one_timesheet()
    {
        var response = await Put(Guid.NewGuid(), TimesheetStatus.Draft, Line(EmployeeA, Day(1, 8)), Line(EmployeeA, Day(2, 8)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_rejects_period_that_is_not_first_day_of_month()
    {
        var request = new SaveTimesheetRequest(Guid.NewGuid(), new DateTime(2026, 1, 10), TimesheetStatus.Draft, Array.Empty<SaveTimesheetLineRequest>());

        var response = await _client.PutAsJsonAsync("api/timesheets", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_rejects_day_from_another_month()
    {
        var other = new SaveTimesheetDayRequest(new DateTime(2026, 2, 1), 8, TimesheetDayType.Workday, null, null);

        var response = await Put(Guid.NewGuid(), TimesheetStatus.Draft, Line(EmployeeA, other));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(25)]
    public async Task Put_rejects_hours_outside_of_0_to_24(int hours)
    {
        var response = await Put(Guid.NewGuid(), TimesheetStatus.Draft, Line(EmployeeA, Day(1, hours)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_cannot_change_approved_timesheet()
    {
        var retailId = Guid.NewGuid();
        await Create(retailId, TimesheetStatus.Approved, Line(EmployeeA, Day(1, 8)));

        var response = await Put(retailId, TimesheetStatus.Draft, Line(EmployeeA, Day(1, 1)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Error));
    }

    [Fact]
    public async Task Put_keeps_line_id_of_existing_employee()
    {
        var retailId = Guid.NewGuid();
        var first = await Create(retailId, TimesheetStatus.Draft, Line(EmployeeA, Day(1, 8)));

        var second = await Create(retailId, TimesheetStatus.Draft, Line(EmployeeA, Day(1, 6)));

        Assert.Equal(Assert.Single(first.Lines).Id, Assert.Single(second.Lines).Id);
        Assert.Equal(6, Assert.Single(second.Lines).Totals.TotalHours);
    }

    [Fact]
    public async Task Put_can_approve_draft_and_then_it_is_locked()
    {
        var retailId = Guid.NewGuid();
        await Create(retailId, TimesheetStatus.Draft, Line(EmployeeA, Day(1, 8)));

        var approved = await Create(retailId, TimesheetStatus.Approved, Line(EmployeeA, Day(1, 8)));
        var again = await Put(retailId, TimesheetStatus.Approved, Line(EmployeeA, Day(1, 8)));

        Assert.Equal(TimesheetStatus.Approved, approved.Status);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Post_line_adds_employee_to_timesheet()
    {
        var retailId = Guid.NewGuid();
        await Create(retailId, TimesheetStatus.Draft, Line(EmployeeA, Day(1, 8)));

        var response = await PostLine(retailId, EmployeeB);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var timesheet = await Read(response);
        Assert.Equal(new[] { EmployeeA, EmployeeB }, timesheet.Lines.Select(line => line.EmployeeId).ToArray());
    }

    [Fact]
    public async Task Post_line_for_existing_employee_returns_409_and_does_not_duplicate()
    {
        var retailId = Guid.NewGuid();
        await Create(retailId, TimesheetStatus.Draft, Line(EmployeeA, Day(1, 8)));

        var first = await PostLine(retailId, EmployeeA);
        var second = await PostLine(retailId, EmployeeA);

        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var timesheet = await _client.GetFromJsonAsync<TimesheetResponse>(GetUrl(retailId));
        Assert.Single(timesheet!.Lines);
    }

    [Fact]
    public async Task Post_line_returns_404_when_timesheet_does_not_exist()
    {
        var response = await PostLine(Guid.NewGuid(), EmployeeA);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_line_returns_409_for_approved_timesheet()
    {
        var retailId = Guid.NewGuid();
        await Create(retailId, TimesheetStatus.Approved, Line(EmployeeA, Day(1, 8)));

        var response = await PostLine(retailId, EmployeeB);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Comment_is_saved_to_the_requested_day_only()
    {
        var retailId = Guid.NewGuid();
        var created = await Create(retailId, TimesheetStatus.Draft, Line(EmployeeA, Day(1, 8, comment: "старый-1"), Day(2, 8, comment: "старый-2")));
        var lineId = Assert.Single(created.Lines).Id;

        var response = await PutComment(retailId, lineId, new DateTime(2026, 1, 1), "новый комментарий за 01.01");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var days = Assert.Single((await Read(response)).Lines).Days.ToDictionary(day => day.Date);
        Assert.Equal("новый комментарий за 01.01", days[new DateTime(2026, 1, 1)].Comment);
        Assert.Equal("старый-2", days[new DateTime(2026, 1, 2)].Comment);
    }

    [Fact]
    public async Task Comment_for_second_day_can_be_updated_too()
    {
        var retailId = Guid.NewGuid();
        var created = await Create(retailId, TimesheetStatus.Draft, Line(EmployeeA, Day(1, 8, comment: "первый"), Day(2, 8)));
        var lineId = Assert.Single(created.Lines).Id;

        var response = await PutComment(retailId, lineId, new DateTime(2026, 1, 2), "второй");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var days = Assert.Single((await Read(response)).Lines).Days.ToDictionary(day => day.Date);
        Assert.Equal("первый", days[new DateTime(2026, 1, 1)].Comment);
        Assert.Equal("второй", days[new DateTime(2026, 1, 2)].Comment);
    }

    [Fact]
    public async Task Comment_returns_404_for_unknown_line()
    {
        var retailId = Guid.NewGuid();
        await Create(retailId, TimesheetStatus.Draft, Line(EmployeeA, Day(1, 8)));

        var response = await PutComment(retailId, lineId: 999999, new DateTime(2026, 1, 1), "x");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Comment_returns_404_for_day_that_is_not_in_the_line()
    {
        var retailId = Guid.NewGuid();
        var created = await Create(retailId, TimesheetStatus.Draft, Line(EmployeeA, Day(1, 8)));

        var response = await PutComment(retailId, Assert.Single(created.Lines).Id, new DateTime(2026, 1, 20), "x");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Comment_returns_409_for_approved_timesheet_and_keeps_old_value()
    {
        var retailId = Guid.NewGuid();
        var created = await Create(retailId, TimesheetStatus.Approved, Line(EmployeeA, Day(1, 8, comment: "было")));
        var lineId = Assert.Single(created.Lines).Id;

        var response = await PutComment(retailId, lineId, new DateTime(2026, 1, 1), "стало");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var loaded = await _client.GetFromJsonAsync<TimesheetResponse>(GetUrl(retailId));
        Assert.Equal("было", Assert.Single(Assert.Single(loaded!.Lines).Days).Comment);
    }

    [Fact]
    public async Task Parallel_requests_to_add_same_employee_create_only_one_line()
    {
        var retailId = Guid.NewGuid();
        await Create(retailId, TimesheetStatus.Draft, Line(EmployeeA, Day(1, 8)));

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PostLine(retailId, EmployeeB)));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.All(
            responses.Where(response => response.StatusCode != HttpStatusCode.OK),
            response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
        var timesheet = await _client.GetFromJsonAsync<TimesheetResponse>(GetUrl(retailId));
        Assert.Single(timesheet!.Lines, line => line.EmployeeId == EmployeeB);
        Assert.Equal(2, timesheet.Lines.Count);
    }

    [Fact]
    public async Task Parallel_requests_to_create_same_timesheet_leave_a_single_timesheet()
    {
        var retailId = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Put(retailId, TimesheetStatus.Draft, Line(EmployeeA, Day(1, 8)))));

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.All(responses, response => Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        var timesheet = await _client.GetFromJsonAsync<TimesheetResponse>(GetUrl(retailId));
        Assert.Equal(EmployeeA, Assert.Single(timesheet!.Lines).EmployeeId);
    }

    private static string GetUrl(Guid retailId) => $"api/timesheets?retailId={retailId}&periodStart=2026-01-01";

    private static string LinesUrl(Guid retailId) => $"api/timesheets/{retailId}/2026-01-01/lines";

    private static SaveTimesheetDayRequest Day(int day, decimal hours, TimesheetDayType type = TimesheetDayType.Workday, string? comment = null) =>
        new(new DateTime(2026, 1, day), hours, type, null, comment);

    private static SaveTimesheetLineRequest Line(Guid employeeId, params SaveTimesheetDayRequest[] days) =>
        new(employeeId, PositionId, EmploymentType.Main, false, days);

    private Task<HttpResponseMessage> Put(Guid retailId, TimesheetStatus status, params SaveTimesheetLineRequest[] lines) =>
        _client.PutAsJsonAsync("api/timesheets", new SaveTimesheetRequest(retailId, Period, status, lines));

    private async Task<TimesheetResponse> Create(Guid retailId, TimesheetStatus status, params SaveTimesheetLineRequest[] lines)
    {
        var response = await Put(retailId, status, lines);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Read(response);
    }

    private Task<HttpResponseMessage> PostLine(Guid retailId, Guid employeeId) =>
        _client.PostAsJsonAsync(
            LinesUrl(retailId),
            new AddTimesheetLineRequest(employeeId, PositionId, EmploymentType.Main, false, new[] { Day(1, 0), Day(2, 0) }));

    private Task<HttpResponseMessage> PutComment(Guid retailId, int lineId, DateTime date, string? comment) =>
        _client.PutAsJsonAsync($"{LinesUrl(retailId)}/{lineId}/comment", new UpdateDayCommentRequest(date, comment));

    private static async Task<TimesheetResponse> Read(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<TimesheetResponse>())!;
}
