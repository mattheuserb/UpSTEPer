using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
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
        Status = "Checking the model ...";
        try
        {
            var parts = await _session.RunAsync(document => document.Check(tolerance)).ConfigureAwait(true);
            var withProblems = parts.Count(p => p.HasProblems);
            Status = withProblems == 0
                ? $"Check: no problems in {parts.Count} part(s)."
                : $"Check: {withProblems} of {parts.Count} part(s) have problems.";
            return parts;
        }
        catch (GeometryOperationException exception)
        {
            Status = $"Error: {exception.Message}";
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
        Status = "Repairing ...";
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
            Status = string.Create(
                CultureInfo.CurrentCulture,
                $"Repair: {changed} of {report.Count} part(s) changed, {report.Sum(p => p.CanonicalFaces)} exact surface(s); Undo repair restores the previous model.");
            LogRepaired(_logger, changed, report.Count);
            return report;
        }
        catch (OperationCanceledException)
        {
            File.Delete(snapshot);
            Status = "Repair cancelled; the model is unchanged.";
            return null;
        }
        catch (Exception exception) when (exception is GeometryOperationException or IOException)
        {
            File.Delete(snapshot);
            Status = $"Error: {exception.Message}";
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
        Status = "Restoring the model before the repair ...";
        var (progress, cancellationToken) = BeginOperation();
        try
        {
            var loaded = await Task.Run(() => LoadDocument(snapshot, quality, progress, cancellationToken)).ConfigureAwait(true);
            _session?.Dispose();
            _session = new DocumentSession(loaded.Document);
            ShowModel(loaded, quality);
            IsDirty = _dirtyBeforeRepair;
            DeleteRepairSnapshot();
            Status = "Repair undone.";
        }
        catch (OperationCanceledException)
        {
            Status = "Undo repair cancelled; the repaired model stays open.";
        }
        catch (Exception exception) when (exception is FileNotFoundException or StepImportException or GeometryOperationException)
        {
            Status = $"Error: {exception.Message}";
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
