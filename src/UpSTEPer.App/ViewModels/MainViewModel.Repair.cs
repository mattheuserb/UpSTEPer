using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using UpSTEPer.App.Localization;
using UpSTEPer.Core;

namespace UpSTEPer.App.ViewModels;

/// <summary>Check and repair of the open model, with undo of the last repair.</summary>
internal sealed partial class MainViewModel
{
    // The model as it was before the last repair (STEP with colours), for "Undo repair".
    private string? _repairSnapshot;
    private bool _dirtyBeforeRepair;

    /// <summary>Gets or sets a value indicating whether the last repair can be undone.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoRepairCommand))]
    public partial bool CanUndoRepair { get; set; }

    /// <summary>Checks every part for defects (read-only).</summary>
    /// <param name="tolerance">Tolerance in mm for canonical geometry and small edges.</param>
    /// <returns>The findings, or <see langword="null"/> without a model or on error (see <see cref="Status"/>).</returns>
    public async Task<IReadOnlyList<PartCheck>?> CheckModelAsync(double tolerance = 0.01)
    {
        if (_session is null)
        {
            return null;
        }

        IsBusy = true;
        Status = Strings.Status_Checking;
        try
        {
            var parts = await _session.RunAsync(document => document.Check(tolerance)).ConfigureAwait(true);
            var withProblems = parts.Count(p => p.HasProblems);
            Status = withProblems == 0
                ? Localizer.Format(Strings.Status_CheckNoProblems, parts.Count)
                : Localizer.Format(Strings.Status_CheckProblems, withProblems, parts.Count);
            return parts;
        }
        catch (GeometryOperationException exception)
        {
            Status = Localizer.Format(Strings.Status_Error, exception.Message);
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Repairs the model and shows the result. The model before the repair is kept as a temporary
    /// STEP file (with colour edits) for <see cref="UndoRepairCommand"/>.
    /// </summary>
    /// <param name="options">Steps and tolerance.</param>
    /// <returns>The report, or <see langword="null"/> without a model, on cancellation or error (see <see cref="Status"/>).</returns>
    public async Task<IReadOnlyList<PartRepair>?> RepairModelAsync(RepairOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_session is null)
        {
            return null;
        }

        var quality = DisplayQuality;
        var snapshot = Path.Combine(Path.GetTempPath(), $"upsteper-before-repair-{Guid.NewGuid():N}.stp");
        IsBusy = true;
        Status = Strings.Status_Repairing;
        var (progress, cancellationToken) = BeginOperation();
        try
        {
            var (report, model) = await _session.RunAsync(document =>
            {
                document.SaveStep(snapshot);
                var report = document.Repair(options, new ScaledProgress(progress, 0, 0.8), cancellationToken);

                // The repair has been applied: rebuild the view unconditionally (no cancellation).
                return (report, BuildModel(document, quality, new ScaledProgress(progress, 0.8, 1), CancellationToken.None));
            }).ConfigureAwait(true);

            DeleteRepairSnapshot();
            _repairSnapshot = snapshot;
            _dirtyBeforeRepair = IsDirty;
            ShowModel(model, quality);
            IsDirty = true;
            CanUndoRepair = true;
            var changed = report.Count(p => p.Changed);
            Status = Localizer.Format(Strings.Status_Repaired, changed, report.Count, report.Sum(p => p.CanonicalFaces), report.Sum(p => p.CanonicalEdges));
            LogRepaired(_logger, changed, report.Count);
            return report;
        }
        catch (OperationCanceledException)
        {
            File.Delete(snapshot);
            Status = Strings.Status_RepairCancelled;
            return null;
        }
        catch (Exception exception) when (exception is GeometryOperationException or IOException)
        {
            File.Delete(snapshot);
            Status = Localizer.Format(Strings.Status_Error, exception.Message);
            LogRepairFailed(_logger, exception);
            return null;
        }
        finally
        {
            EndOperation();
            IsBusy = false;
        }
    }

    /// <summary>Restores the model as it was before the last repair.</summary>
    [RelayCommand(CanExecute = nameof(CanUndoRepair))]
    private async Task UndoRepairAsync()
    {
        if (_repairSnapshot is not { } snapshot)
        {
            return;
        }

        var quality = DisplayQuality;
        IsBusy = true;
        Status = Strings.Status_RestoringBeforeRepair;
        var (progress, cancellationToken) = BeginOperation();
        try
        {
            var loaded = await Task.Run(() => LoadDocument(snapshot, quality, progress, cancellationToken)).ConfigureAwait(true);
            _session?.Dispose();
            _session = new DocumentSession(loaded.Document);
            ShowModel(loaded, quality);
            IsDirty = _dirtyBeforeRepair;
            DeleteRepairSnapshot();
            Status = Strings.Status_RepairUndone;
        }
        catch (OperationCanceledException)
        {
            Status = Strings.Status_UndoRepairCancelled;
        }
        catch (Exception exception) when (exception is FileNotFoundException or StepImportException or GeometryOperationException)
        {
            Status = Localizer.Format(Strings.Status_Error, exception.Message);
        }
        finally
        {
            EndOperation();
            IsBusy = false;
        }
    }

    private void DeleteRepairSnapshot()
    {
        if (_repairSnapshot is { } snapshot)
        {
            File.Delete(snapshot);
        }

        _repairSnapshot = null;
        CanUndoRepair = false;
    }

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Repaired {ChangedCount} of {PartCount} part(s)")]
    private static partial void LogRepaired(ILogger logger, int changedCount, int partCount);

    [LoggerMessage(EventId = 12, Level = LogLevel.Error, Message = "Repair failed")]
    private static partial void LogRepairFailed(ILogger logger, Exception exception);
}
