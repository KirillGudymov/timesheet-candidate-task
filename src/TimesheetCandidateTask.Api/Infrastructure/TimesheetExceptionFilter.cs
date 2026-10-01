using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TimesheetCandidateTask.Api.Application;
using TimesheetCandidateTask.Api.Contracts;

namespace TimesheetCandidateTask.Api.Infrastructure;

public sealed class TimesheetExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        int? statusCode = context.Exception switch
        {
            TimesheetValidationException => StatusCodes.Status400BadRequest,
            TimesheetNotFoundException => StatusCodes.Status404NotFound,
            TimesheetConflictException => StatusCodes.Status409Conflict,
            _ => null
        };

        if (statusCode is null)
        {
            return; // неизвестные ошибки остаются 500, их нельзя маскировать
        }

        context.Result = new ObjectResult(new ErrorResponse(context.Exception.Message))
        {
            StatusCode = statusCode
        };
        context.ExceptionHandled = true;
    }
}