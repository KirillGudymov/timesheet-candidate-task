namespace TimesheetCandidateTask.Api.Application
{
    public sealed class TimesheetValidationException : ArgumentException
    {
        public TimesheetValidationException(string message) : base(message) { }
    }

    public sealed class TimesheetNotFoundException : KeyNotFoundException
    {
        public TimesheetNotFoundException(string message) : base(message) { }
    }

    public class TimesheetConflictException : InvalidOperationException
    {
        public TimesheetConflictException(string message) : base(message) { }
    }

    public sealed class TimesheetApprovedException : TimesheetConflictException
    {
        public TimesheetApprovedException() : base("Табель утверждён (Approved), изменять его нельзя.") { }
    }

    public sealed class TimesheetConcurrencyException : TimesheetConflictException
    {
        public TimesheetConcurrencyException()
            : base("Табель одновременно изменён другим запросом. Повторите операцию.") { }
    }
}
