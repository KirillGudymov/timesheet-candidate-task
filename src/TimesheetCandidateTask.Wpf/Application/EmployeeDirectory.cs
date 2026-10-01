using TimesheetCandidateTask.Api.Infrastructure;

namespace TimesheetCandidateTask.Wpf.Application;

public static class EmployeeDirectory
{
    public static readonly Guid DefaultPositionId = Guid.Parse("7b442775-f9ee-4cf5-b3c3-75687650a81f");

    private static readonly (Guid Id, string Name)[] Employees =
    {
        (TimesheetSeeder.EmployeeId, "Иванов И.И."),
        (Guid.Parse("3f6b8d1e-2c47-4a9b-9a5e-1d7c0b4e8f21"), "Петрова А.С."),
        (Guid.Parse("a1d2c3b4-5e6f-4789-8a9b-0c1d2e3f4a5b"), "Сидоров П.Н.")
    };

    public static IReadOnlyList<Guid> Ids { get; } = Employees.Select(employee => employee.Id).ToList();

    public static string GetName(Guid employeeId)
    {
        foreach (var employee in Employees)
        {
            if (employee.Id == employeeId)
            {
                return employee.Name;
            }
        }

        return "Сотрудник " + employeeId.ToString()[..8];
    }
}