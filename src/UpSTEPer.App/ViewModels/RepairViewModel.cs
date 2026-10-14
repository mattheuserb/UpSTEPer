using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpSTEPer.Core;

namespace UpSTEPer.App.ViewModels;

/// <summary>
/// State of the check-and-repair dialog: options, check findings and the repair report, each with
/// a summary computed from the results and per-part status and details.
/// </summary>
internal sealed partial class RepairViewModel : ObservableObject
{
    /// <summary>Index of the check results in <see cref="SelectedReportIndex"/>.</summary>
    public const int CheckReport = 0;

    /// <summary>Index of the repair report in <see cref="SelectedReportIndex"/>.</summary>
    public const int RepairReport = 1;

    /// <summary>Initializes a new instance of the <see cref="RepairViewModel"/> class.</summary>
    /// <param name="main">The main view model that owns the model.</param>
    public RepairViewModel(MainViewModel main) => Main = main ?? throw new ArgumentNullException(nameof(main));

    /// <summary>Gets the main view model (progress, cancel, undo repair).</summary>
    public MainViewModel Main { get; }

    /// <summary>Gets or sets a value indicating whether general fixes run.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RepairCommand))]
    public partial bool Fix { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether gaps are sewn.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RepairCommand))]
    public partial bool Sew { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether spline faces and edges get exact geometry.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RepairCommand))]
    public partial bool Canonical { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether faces on the same surface are merged.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RepairCommand))]
    public partial bool MergeFaces { get; set; } = true;

    /// <summary>Gets or sets the tolerance in mm (a decimal for the numeric input).</summary>
    [ObservableProperty]
    public partial decimal? Tolerance { get; set; } = (decimal)RepairOptions.Default.Tolerance;

    /// <summary>Gets or sets the findings of the last check.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCheckResults), nameof(PartsChecked), nameof(PartsOk), nameof(PartsWithProblems), nameof(PartsWithCheckErrors))]
    [NotifyPropertyChangedFor(nameof(ShowsCheckSummary))]
    public partial IReadOnlyList<CheckRow> CheckRows { get; set; } = [];

    /// <summary>Gets or sets the report of the last repair.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRepairReport), nameof(PartsRepaired), nameof(PartsUnchanged), nameof(PartsNeedingAttention), nameof(PartsFailed))]
    [NotifyPropertyChangedFor(nameof(ShowsRepairSummary))]
    public partial IReadOnlyList<RepairRow> RepairRows { get; set; } = [];

    /// <summary>Gets or sets the part whose check values are shown in the details.</summary>
    [ObservableProperty]
    public partial CheckRow? SelectedCheckRow { get; set; }

    /// <summary>Gets or sets the part whose repair values are shown in the details.</summary>
    [ObservableProperty]
    public partial RepairRow? SelectedRepairRow { get; set; }

    /// <summary>Gets or sets the report shown: <see cref="CheckReport"/> or <see cref="RepairReport"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRepairReport), nameof(ShowsCheckSummary), nameof(ShowsRepairSummary))]
    public partial int SelectedReportIndex { get; set; }

    /// <summary>Gets a value indicating whether the repair report (not the check) is shown.</summary>
    public bool ShowsRepairReport => SelectedReportIndex == RepairReport;

    /// <summary>Gets or sets the one-line result of the last action, shown under the table.</summary>
    [ObservableProperty]
    public partial string Summary { get; set; } = "Check the model to see what a repair would change.";

    /// <summary>Gets a value indicating whether the summary of the check is shown (check tab, after a check).</summary>
    public bool ShowsCheckSummary => HasCheckResults && !ShowsRepairReport;

    /// <summary>Gets a value indicating whether the summary of the repair is shown (repair tab, after a repair).</summary>
    public bool ShowsRepairSummary => HasRepairReport && ShowsRepairReport;

    /// <summary>Gets a value indicating whether a check has run.</summary>
    public bool HasCheckResults => CheckRows.Count > 0;

    /// <summary>Gets the number of parts checked.</summary>
    public int PartsChecked => CheckRows.Count;

    /// <summary>Gets the number of parts without findings.</summary>
    public int PartsOk => CheckRows.Count(r => r.Status == PartStatus.Ok);

    /// <summary>Gets the number of parts with findings (not counting failed sub-checks).</summary>
    public int PartsWithProblems => CheckRows.Count(r => r.Status == PartStatus.Warning);

    /// <summary>Gets the number of parts on which a sub-check failed.</summary>
    public int PartsWithCheckErrors => CheckRows.Count(r => r.Status == PartStatus.Error);

    /// <summary>Gets a value indicating whether a repair has run.</summary>
    public bool HasRepairReport => RepairRows.Count > 0;

    /// <summary>Gets the number of parts repaired without anything left to look at.</summary>
    public int PartsRepaired => RepairRows.Count(r => r.Status == PartStatus.Ok);

    /// <summary>Gets the number of parts the repair left as they were (nothing to do).</summary>
    public int PartsUnchanged => RepairRows.Count(r => r.Status == PartStatus.Unchanged);

    /// <summary>Gets the number of parts with defects left, a skipped step or merged colours.</summary>
    public int PartsNeedingAttention => RepairRows.Count(r => r.Status == PartStatus.Warning);

    /// <summary>Gets the number of parts on which a repair step failed.</summary>
    public int PartsFailed => RepairRows.Count(r => r.Status == PartStatus.Error);

    /// <summary>Gets the steps selected in the dialog.</summary>
    public RepairSteps SelectedSteps =>
        (Fix ? RepairSteps.Fix : RepairSteps.None)
        | (Sew ? RepairSteps.Sew : RepairSteps.None)
        | (Canonical ? RepairSteps.Canonical : RepairSteps.None)
        | (MergeFaces ? RepairSteps.MergeFaces : RepairSteps.None);

    [RelayCommand]
    private async Task CheckAsync()
    {
        var parts = await Main.CheckModelAsync(ToleranceMillimetres).ConfigureAwait(true);
        if (parts is not null)
        {
            CheckRows = [.. parts.Select(p => new CheckRow(p))];
            SelectedCheckRow = FirstToLookAt(CheckRows, r => r.Status);
            SelectedReportIndex = CheckReport;
        }

        Summary = Main.Status;
    }

    [RelayCommand(CanExecute = nameof(CanRepair))]
    private async Task RepairAsync()
    {
        var report = await Main.RepairModelAsync(new RepairOptions(SelectedSteps, ToleranceMillimetres)).ConfigureAwait(true);
        if (report is not null)
        {
            RepairRows = [.. report.Select(p => new RepairRow(p))];
            SelectedRepairRow = FirstToLookAt(RepairRows, r => r.Status);
            SelectedReportIndex = RepairReport;
        }

        Summary = Main.Status;
    }

    // The most severe row first, so the details open on what needs attention.
    private static T? FirstToLookAt<T>(IReadOnlyList<T> rows, Func<T, PartStatus> status)
        where T : class =>
        rows.OrderByDescending(r => status(r) is PartStatus.Error or PartStatus.Warning ? (int)status(r) : -1).FirstOrDefault();

    private double ToleranceMillimetres => (double)(Tolerance ?? (decimal)RepairOptions.Default.Tolerance);

    private bool CanRepair() => SelectedSteps != RepairSteps.None;
}
