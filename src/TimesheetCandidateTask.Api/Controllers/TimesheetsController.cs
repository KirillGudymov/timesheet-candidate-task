using Microsoft.AspNetCore.Mvc;
using TimesheetCandidateTask.Api.Application;
using TimesheetCandidateTask.Api.Contracts;

namespace TimesheetCandidateTask.Api.Controllers;

[ApiController]
[Route("api/timesheets")]
public sealed class TimesheetsController : ControllerBase
{
    private readonly TimesheetService _service;

    public TimesheetsController(TimesheetService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(TimesheetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TimesheetResponse>> Get(Guid retailId, DateTime periodStart, CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(retailId, periodStart, cancellationToken);
        return result is null
            ? NotFound(new ErrorResponse("Табель за указанный месяц не найден."))
            : Ok(result);
    }

    [HttpPut]
    [ProducesResponseType(typeof(TimesheetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TimesheetResponse>> Save(SaveTimesheetRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.SaveAsync(request, cancellationToken));

    [HttpPost("{retailId:guid}/{periodStart:datetime}/lines")]
    [ProducesResponseType(typeof(TimesheetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TimesheetResponse>> AddLine(Guid retailId, DateTime periodStart, AddTimesheetLineRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.AddLineAsync(retailId, periodStart, request, cancellationToken));

    [HttpPut("{retailId:guid}/{periodStart:datetime}/lines/{lineId:int}/comment")]
    [ProducesResponseType(typeof(TimesheetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TimesheetResponse>> UpdateComment(Guid retailId, DateTime periodStart, int lineId, UpdateDayCommentRequest request, CancellationToken cancellationToken) =>
        Ok(await _service.UpdateDayCommentAsync(retailId, periodStart, lineId, request, cancellationToken));
}
