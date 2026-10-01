using TimesheetCandidateTask.Api.Contracts;
using TimesheetCandidateTask.Wpf.Application;
using TimesheetCandidateTask.Api.Domain;

namespace TimesheetCandidateTask.Wpf.ViewModels;

public sealed record CommentChange(DateTime Date, string? Comment);
public sealed class TimesheetLineViewModel : ObservableObject
{
    private readonly TimesheetLineResponse _line;
    private readonly DateTime _firstDate;
    private readonly DateTime _secondDate;
    private readonly Action _changed;
    private string? _firstDayComment;
    private string? _secondDayComment;
    private string? _savedFirstDayComment;
    private string? _savedSecondDayComment;


    public TimesheetLineViewModel(TimesheetLineResponse line, DateTime periodStart, Action changed)
    {
        _line = line;
        _firstDate = periodStart.Date;
        _secondDate = periodStart.Date.AddDays(1);
        _changed = changed;

        _firstDayComment = _savedFirstDayComment = Normalize(FindDay(_firstDate)?.Comment);
        _secondDayComment = _savedSecondDayComment = Normalize(FindDay(_secondDate)?.Comment);
    }

    public int LineId => _line.Id;
    public Guid EmployeeId => _line.EmployeeId;
    public string EmployeeName => EmployeeDirectory.GetName(_line.EmployeeId);
    public decimal FirstDayHours => FindDay(_firstDate)?.Hours ?? 0;
    public decimal SecondDayHours => FindDay(_secondDate)?.Hours ?? 0;

    public decimal RegularHours => _line.Totals.WorkedHours - _line.Totals.HolidayHours;
    public decimal HolidayHours => _line.Totals.HolidayHours;
    public decimal TotalHours => _line.Totals.TotalHours;

    public string? FirstDayComment
    {
        get => _firstDayComment;
        set
        {
            if (SetProperty(ref _firstDayComment, value))
            {
                _changed();
            }
        }
    }

    public string? SecondDayComment
    {
        get => _secondDayComment;
        set
        {
            if (SetProperty(ref _secondDayComment, value))
            {
                _changed();
            }
        }
    }

    /// <summary>Комментарии, которые пользователь изменил, но ещё не сохранил. Каждый привязан к СВОЕЙ дате.</summary>
    public IEnumerable<CommentChange> GetPendingChanges()
    {
        if (Normalize(_firstDayComment) != _savedFirstDayComment)
        {
            yield return new CommentChange(_firstDate, Normalize(_firstDayComment));
        }

        if (Normalize(_secondDayComment) != _savedSecondDayComment)
        {
            yield return new CommentChange(_secondDate, Normalize(_secondDayComment));
        }
    }

    public void MarkSaved(CommentChange change)
    {
        if (change.Date == _firstDate)
        {
            _savedFirstDayComment = change.Comment;
        }
        else if (change.Date == _secondDate)
        {
            _savedSecondDayComment = change.Comment;
        }
    }

    private TimesheetDayResponse? FindDay(DateTime date) => _line.Days.FirstOrDefault(day => day.Date.Date == date);

    private static string? Normalize(string? comment) => string.IsNullOrEmpty(comment) ? null : comment;
}
