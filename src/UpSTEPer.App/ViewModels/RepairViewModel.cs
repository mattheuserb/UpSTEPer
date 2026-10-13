using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpSTEPer.Core;

namespace UpSTEPer.App.ViewModels;

/// <summary>State of the check-and-repair dialog: options, check findings and the repair report.</summary>
internal sealed partial class RepairViewModel : ObservableObject
{
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

    /// <summary>Gets or sets a value indicating whether spline faces get exact surfaces.</summary>
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
    public partial IReadOnlyList<PartCheck> CheckRows { get; set; } = [];

    /// <summary>Gets or sets the report of the last repair.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PartRepair> RepairRows { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether the repair report (not the check) is shown.</summary>
    [ObservableProperty]
    public partial bool ShowsRepairReport { get; set; }

    /// <summary>Gets or sets the one-line summary under the table.</summary>
    [ObservableProperty]
    public partial string Summary { get; set; } = "Check the model to see what a repair would change.";

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
            CheckRows = parts;
            ShowsRepairReport = false;
        }

        Summary = Main.Status;
    }

    [RelayCommand(CanExecute = nameof(CanRepair))]
    private async Task RepairAsync()
    {
        var report = await Main.RepairModelAsync(new RepairOptions(SelectedSteps, ToleranceMillimetres)).ConfigureAwait(true);
        if (report is not null)
        {
            RepairRows = report;
            ShowsRepairReport = true;
        }

        Summary = Main.Status;
    }

    private double ToleranceMillimetres => (double)(Tolerance ?? (decimal)RepairOptions.Default.Tolerance);

    private bool CanRepair() => SelectedSteps != RepairSteps.None;
}
