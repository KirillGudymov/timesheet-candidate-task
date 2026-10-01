using System.Collections.ObjectModel;
using System.Net.Http;
using TimesheetCandidateTask.Api.Contracts;
using TimesheetCandidateTask.Api.Domain;
using TimesheetCandidateTask.Wpf.Application;
using TimesheetCandidateTask.Wpf.ViewModels;

public sealed class TimesheetViewModel : ObservableObject
{
    private readonly ITimesheetWorkspace _workspace;
    private string _message = "Нажмите «Загрузить», чтобы открыть табель.";
    private TimesheetResponse? _timesheet;

    public TimesheetViewModel(ITimesheetWorkspace workspace)
    {
        _workspace = workspace;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        AddEmployeeCommand = new AsyncRelayCommand(AddEmployeeAsync, CanEdit);
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanEdit);
        ApproveCommand = new AsyncRelayCommand(ApproveAsync, CanEdit);
    }

    public ObservableCollection<TimesheetLineViewModel> Lines { get; } = new();
    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand AddEmployeeCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand ApproveCommand { get; }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public string StatusText => _timesheet is null
        ? "Не загружен"
        : _timesheet.Status == TimesheetStatus.Draft ? "Черновик" : "Утверждён";

    /// <summary>Таблица доступна для редактирования только у загруженного черновика.</summary>
    public bool IsReadOnly => !CanEdit();

    public decimal OverallTotal => Lines.Sum(line => line.TotalHours);

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            Apply(await _workspace.LoadAsync());
            Message = "Табель загружен.";
        });
    }

    private async Task AddEmployeeAsync()
    {
        if (_timesheet is null)
        {
            return;
        }

        if (HasPendingChanges())
        {
            Message = "Сначала сохраните изменения комментариев.";
            return;
        }

        var present = _timesheet.Lines.Select(line => line.EmployeeId).ToHashSet();
        var next = EmployeeDirectory.Ids.Where(id => !present.Contains(id)).Select(id => (Guid?)id).FirstOrDefault();
        if (next is null)
        {
            Message = "Все доступные сотрудники уже добавлены в табель.";
            return;
        }

        await RunAsync(async () =>
        {
            Apply(await _workspace.AddEmployeeAsync(next.Value));
            Message = "Сотрудник добавлен.";
        });
    }

    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            var saved = await SaveCommentsAsync();
            Message = saved == 0 ? "Нет изменений для сохранения." : "Изменения сохранены.";
        });
    }

    private async Task ApproveAsync()
    {
        await RunAsync(async () =>
        {
            await SaveCommentsAsync();

            if (_timesheet is null)
            {
                return;
            }

            Apply(await _workspace.SaveAsync(_timesheet, TimesheetStatus.Approved));
            Message = "Табель утверждён.";
        });
    }

    private async Task<int> SaveCommentsAsync()
    {
        var saved = 0;
        foreach (var line in Lines.ToList())
        {
            foreach (var change in line.GetPendingChanges().ToList())
            {
                _timesheet = await _workspace.UpdateCommentAsync(line.LineId, change.Date, change.Comment);
                line.MarkSaved(change);
                saved++;
            }
        }

        if (saved > 0 && _timesheet is not null)
        {
            Apply(_timesheet);
        }

        return saved;
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (TimesheetApiException exception)
        {
            Message = exception.Message;
        }
        catch (HttpRequestException)
        {
            Message = "Не удалось связаться с сервером API. Проверьте, что он запущен.";
        }
        catch (TaskCanceledException)
        {
            Message = "Сервер API не ответил вовремя.";
        }
    }

    private bool HasPendingChanges() => Lines.Any(line => line.GetPendingChanges().Any());

    private bool CanEdit() => _timesheet is { Status: TimesheetStatus.Draft };

    private void Apply(TimesheetResponse timesheet)
    {
        _timesheet = timesheet;
        Lines.Clear();
        foreach (var line in timesheet.Lines)
        {
            Lines.Add(new TimesheetLineViewModel(line, timesheet.PeriodStart, OnLineChanged));
        }

        NotifyStateChanged();
    }

    private void OnLineChanged() => Message = "Есть несохранённые изменения комментариев.";

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(OverallTotal));
        AddEmployeeCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
        ApproveCommand.RaiseCanExecuteChanged();
    }
}