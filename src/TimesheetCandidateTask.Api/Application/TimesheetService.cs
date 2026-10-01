using Microsoft.EntityFrameworkCore;
using TimesheetCandidateTask.Api.Contracts;
using TimesheetCandidateTask.Api.Domain;
using TimesheetCandidateTask.Api.Infrastructure;
using Microsoft.Data.Sqlite;

namespace TimesheetCandidateTask.Api.Application;

public sealed class TimesheetService
{
    private readonly TimesheetDbContext _db;
    private const int MaxAttempts = 3;

    // Коды SQLite: 5 = BUSY, 6 = LOCKED, 19 = CONSTRAINT (в том числе нарушение уникального индекса).
    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;
    private const int SqliteConstraint = 19;

    private async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (TimesheetConcurrencyException) when (attempt < MaxAttempts)
            {
                _db.ChangeTracker.Clear(); // забываем устаревшие данные и читаем заново
            }
        }
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new TimesheetConcurrencyException();
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException
        {
            SqliteErrorCode: SqliteBusy or SqliteLocked or SqliteConstraint
        })
        {
            throw new TimesheetConcurrencyException();
        }
    }

    private void MergeLines(Timesheet timesheet, IReadOnlyCollection<SaveTimesheetLineRequest> requested)
    {
        var requestedEmployees = requested.Select(line => line.EmployeeId).ToHashSet();

        foreach (var obsolete in timesheet.Lines.Where(line => !requestedEmployees.Contains(line.EmployeeId)).ToList())
        {
            _db.TimesheetDays.RemoveRange(obsolete.Days.ToList());
            _db.TimesheetLines.Remove(obsolete);
            timesheet.Lines.Remove(obsolete);
        }

        foreach (var source in requested)
        {
            var existing = timesheet.Lines.SingleOrDefault(line => line.EmployeeId == source.EmployeeId);
            if (existing is null)
            {
                timesheet.Lines.Add(ToEntity(source));
                continue;
            }

            existing.PositionId = source.PositionId;
            existing.EmploymentType = source.EmploymentType;
            existing.IsNight = source.IsNight;

            _db.TimesheetDays.RemoveRange(existing.Days.ToList());
            existing.Days.Clear();
            existing.Days.AddRange(source.Days.Select(ToEntity));
        }
    }
    public TimesheetService(TimesheetDbContext db)
    {
        _db = db;
    }

    public async Task<TimesheetResponse?> GetAsync(Guid retailId, DateTime periodStart, CancellationToken cancellationToken)
    {
        var period = NormalizePeriod(periodStart);
        var timesheet = await Query()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.RetailId == retailId && item.PeriodStart == period, cancellationToken);
        return timesheet is null ? null : ToResponse(timesheet);
    }

    public async Task<TimesheetResponse> SaveAsync(SaveTimesheetRequest request, CancellationToken cancellationToken)
    {
        var period = NormalizePeriod(request.PeriodStart);
        ValidateLines(period, request.Lines);

        return await RunAsync(async () =>
        {
            var timesheet = await FindAsync(request.RetailId, period, cancellationToken);

            if (timesheet is null)
            {
                timesheet = new Timesheet
                {
                    RetailId = request.RetailId,
                    PeriodStart = period,
                    Status = request.Status
                };
                foreach (var line in request.Lines)
                {
                    timesheet.Lines.Add(ToEntity(line));
                }

                _db.Timesheets.Add(timesheet);
            }
            else
            {
                EnsureEditable(timesheet);
                timesheet.Status = request.Status;
                MergeLines(timesheet, request.Lines);
                timesheet.Version++;
            }

            await SaveChangesAsync(cancellationToken);
            return ToResponse(timesheet);
        });
    }

    public async Task<TimesheetResponse> AddLineAsync(Guid retailId, DateTime periodStart, AddTimesheetLineRequest request, CancellationToken cancellationToken)
    {
        var period = NormalizePeriod(periodStart);
        ValidateDays(period, request.Days);

        return await RunAsync(async () =>
        {
            var timesheet = await RequireAsync(retailId, period, cancellationToken);
            EnsureEditable(timesheet);

            if (timesheet.Lines.Any(line => line.EmployeeId == request.EmployeeId))
            {
                throw new TimesheetConflictException("Сотрудник уже добавлен в этот табель.");
            }

            timesheet.Lines.Add(ToEntity(request));
            timesheet.Version++;
            await SaveChangesAsync(cancellationToken);
            return ToResponse(timesheet);
        });
    }

    public async Task<TimesheetResponse> UpdateDayCommentAsync(Guid retailId, DateTime periodStart, int lineId, UpdateDayCommentRequest request, CancellationToken cancellationToken)
    {
        var period = NormalizePeriod(periodStart);
        if (request.Comment is { Length: > MaxCommentLength })
        {
            throw new TimesheetValidationException($"Комментарий не должен быть длиннее {MaxCommentLength} символов.");
        }

        return await RunAsync(async () =>
        {
            var timesheet = await RequireAsync(retailId, period, cancellationToken);
            EnsureEditable(timesheet);

            var line = timesheet.Lines.SingleOrDefault(item => item.Id == lineId)
                ?? throw new TimesheetNotFoundException("Строка сотрудника не найдена.");

            var date = request.Date.Date;
            var day = line.Days.SingleOrDefault(item => item.Date.Date == date)
                ?? throw new TimesheetNotFoundException("День табеля не найден.");

            day.Comment = request.Comment;
            timesheet.Version++;
            await SaveChangesAsync(cancellationToken);
            return ToResponse(timesheet);
        });
    }
    private IQueryable<Timesheet> Query() =>
    _db.Timesheets
        .Include(item => item.Lines)
        .ThenInclude(line => line.Days);

    private async Task<Timesheet> RequireAsync(Guid retailId, DateTime period, CancellationToken cancellationToken) =>
        await FindAsync(retailId, period, cancellationToken)
        ?? throw new TimesheetNotFoundException("Табель за указанный месяц не найден.");

    private Task<Timesheet?> FindAsync(Guid retailId, DateTime period, CancellationToken cancellationToken) =>
        Query().SingleOrDefaultAsync(item => item.RetailId == retailId && item.PeriodStart == period, cancellationToken);

    private static TimesheetLine ToEntity(AddTimesheetLineRequest source) => new()
    {
        EmployeeId = source.EmployeeId,
        PositionId = source.PositionId,
        EmploymentType = source.EmploymentType,
        IsNight = source.IsNight,
        Days = source.Days.Select(ToEntity).ToList()
    };

    private static TimesheetLine ToEntity(SaveTimesheetLineRequest source) => new()
    {
        EmployeeId = source.EmployeeId,
        PositionId = source.PositionId,
        EmploymentType = source.EmploymentType,
        IsNight = source.IsNight,
        Days = source.Days.Select(ToEntity).ToList()
    };

    private static TimesheetDay ToEntity(SaveTimesheetDayRequest source) => new()
    {
        Date = source.Date.Date,
        Hours = source.Hours,
        DayType = source.DayType,
        AbsenceCode = source.AbsenceCode,
        Comment = source.Comment
    };

    private static TimesheetResponse ToResponse(Timesheet source) => new(
        source.Id,
        source.RetailId,
        source.PeriodStart,
        source.Status,
        source.Lines.Select(line => new TimesheetLineResponse(
            line.Id,
            line.EmployeeId,
            line.PositionId,
            line.EmploymentType,
            line.IsNight,
            TimesheetCalculator.Calculate(line),
            line.Days.OrderBy(day => day.Date).Select(day => new TimesheetDayResponse(day.Date, day.Hours, day.DayType, day.AbsenceCode, day.Comment)).ToList())).ToList());

    private static void ValidatePeriod(DateTime periodStart)
    {
        if (periodStart.Date.Day != 1)
        {
            throw new TimesheetValidationException("Начало периода должно приходиться на первый день месяца.");
        }
    }

    private const decimal MaxHoursPerDay = 24m;
    private const int MaxCommentLength = 1000;

    private static DateTime NormalizePeriod(DateTime periodStart)
    {
        var date = DateTime.SpecifyKind(periodStart.Date, DateTimeKind.Unspecified);
        if (date.Day != 1)
        {
            throw new TimesheetValidationException("Начало периода должно приходиться на первый день месяца.");
        }

        return date;
    }

    private static void ValidateLines(DateTime period, IReadOnlyCollection<SaveTimesheetLineRequest> lines)
    {
        var duplicate = lines.GroupBy(line => line.EmployeeId).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new TimesheetValidationException($"Сотрудник {duplicate.Key} указан в табеле более одного раза.");
        }

        foreach (var line in lines)
        {
            ValidateDays(period, line.Days);
        }
    }

    private static void ValidateDays(DateTime period, IReadOnlyCollection<SaveTimesheetDayRequest> days)
    {
        var periodEnd = period.AddMonths(1);
        var seen = new HashSet<DateTime>();

        foreach (var day in days)
        {
            var date = day.Date.Date;

            if (date < period || date >= periodEnd)
            {
                throw new TimesheetValidationException($"День {date:dd.MM.yyyy} не относится к периоду табеля.");
            }

            if (!seen.Add(date))
            {
                throw new TimesheetValidationException($"День {date:dd.MM.yyyy} указан более одного раза.");
            }

            if (day.Hours < 0 || day.Hours > MaxHoursPerDay)
            {
                throw new TimesheetValidationException($"Часы за {date:dd.MM.yyyy} должны быть в диапазоне от 0 до {MaxHoursPerDay}.");
            }

            if (day.Comment is { Length: > MaxCommentLength })
            {
                throw new TimesheetValidationException($"Комментарий за {date:dd.MM.yyyy} не должен быть длиннее {MaxCommentLength} символов.");
            }
        }
    }

    private static void EnsureEditable(Timesheet timesheet)
    {
        if (timesheet.Status == TimesheetStatus.Approved)
        {
            throw new TimesheetApprovedException();
        }
    }
}
