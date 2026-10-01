using System.Net.Http;
using TimesheetCandidateTask.Api.Infrastructure;
using TimesheetCandidateTask.Wpf.Application;
using TimesheetCandidateTask.Wpf.ViewModels;

namespace TimesheetCandidateTask.Wpf;

public partial class MainWindow : System.Windows.Window
{
    private const string DefaultApiUrl = "http://localhost:52137";
    public MainWindow()
    {
        InitializeComponent();
        var baseUrl = Environment.GetEnvironmentVariable("TIMESHEET_API_URL") ?? DefaultApiUrl;
        var http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        var workspace = new ApiTimesheetWorkspace(http, TimesheetSeeder.RetailId, new DateTime(2026, 1, 1));
        DataContext = new TimesheetViewModel(workspace);
    }
}
