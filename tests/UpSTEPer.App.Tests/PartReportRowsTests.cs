using System.Globalization;
using UpSTEPer.App.ViewModels;
using UpSTEPer.Core;

namespace UpSTEPer.App.Tests;

public class PartReportRowsTests
{
    private static readonly PartCheck _clean = new("Box", 1, 6, 0, 0, 0, 0, 0, 0);
    private static readonly PartRepair _unchanged = new("Box", false, false, 6, 6, 1, 1, 0, 0, 0, 0, 0);

    [Fact]
    public void CheckRow_Clean_IsOkWithoutFindings()
    {
        var row = new CheckRow(_clean);

        Assert.Equal((PartStatus.Ok, "OK", "No problems"), (row.Status, row.StatusText, row.Findings));
    }

    [Fact]
    public void CheckRow_Findings_ListEveryProblemOnce()
    {
        var row = new CheckRow(_clean with { FreeEdges = 24, MergeableFaces = 1, SplineEdges = 3, CanonicalEdges = 3 });

        Assert.Equal(PartStatus.Warning, row.Status);
        Assert.Equal("Problems", row.StatusText);
        Assert.Equal("24 free edges · 3 spline edges can be exact · 1 mergeable face", row.Findings);
    }

    [Fact]
    public void CheckRow_FailedSubCheck_IsAnError()
    {
        var row = new CheckRow(_clean with { FailedChecks = 2, FreeEdges = 1 });

        Assert.Equal((PartStatus.Error, "Check failed"), (row.Status, row.StatusText));
        Assert.EndsWith("2 sub-checks failed", row.Findings, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckRow_Details_KeepEveryValue()
    {
        var row = new CheckRow(new PartCheck("P", 2, 9, 3, 4, 5, 1, 6, 7, 8, SplineEdges: 10, CanonicalEdges: 2));

        Assert.Equal(
            ["2", "9", "3", "4", "5 (1)", "10 (2)", "6", "7", "8"],
            row.Details.Select(d => d.Value));
    }

    [Theory]
    [InlineData(false, false, 0, 0, 0, false, (int)PartStatus.Unchanged, "Unchanged")] // nothing to do is not a failure
    [InlineData(true, false, 0, 0, 0, false, (int)PartStatus.Ok, "Repaired")]
    [InlineData(true, false, 3, 0, 0, false, (int)PartStatus.Warning, "Defects remain")] // changed is not success by itself
    [InlineData(false, false, 0, 2, 0, false, (int)PartStatus.Warning, "Defects remain")]
    [InlineData(true, false, 0, 0, 1, false, (int)PartStatus.Warning, "Colours merged")]
    [InlineData(true, false, 0, 0, 0, true, (int)PartStatus.Warning, "Step skipped")]
    [InlineData(true, true, 0, 0, 0, false, (int)PartStatus.Error, "Step failed")]
    public void RepairRow_Status_FollowsTheOutcomeNotTheChangedFlag(
        bool changed, bool failed, int freeEdgesAfter, int invalidAfter, int colourConflicts, bool reverted, int status, string text)
    {
        var row = new RepairRow(_unchanged with
        {
            Changed = changed,
            Failed = failed,
            FreeEdgesAfter = freeEdgesAfter,
            InvalidAfter = invalidAfter,
            ColourConflicts = colourConflicts,
            Reverted = reverted,
        });

        // The status is passed as int: the enum is internal, xUnit theory parameters must be public.
        Assert.Equal(((PartStatus)status, text), (row.Status, row.StatusText));
    }

    [Fact]
    public void RepairRow_Changes_ShowBeforeAndAfterCompactly()
    {
        var row = new RepairRow(new PartRepair("Unsewn box", true, false, 6, 6, 0, 1, 24, 0, 0, 0, 0, CanonicalFaces: 2, MaxDeviation: 0.00012));

        // Numbers follow the UI culture (a decimal comma on a German system).
        var deviation = 0.0001.ToString("0.####", CultureInfo.CurrentCulture);
        Assert.Equal($"Solids 0 → 1 · Free edges 24 → 0 · 2 exact surfaces · max. deviation {deviation} mm", row.Changes);
        Assert.Equal("No changes", new RepairRow(_unchanged).Changes);
    }

    [Fact]
    public void RepairRow_Details_KeepEveryValue()
    {
        var row = new RepairRow(new PartRepair("P", true, true, 6, 3, 1, 1, 24, 0, 2, 2, 1, Reverted: true, CanonicalFaces: 3, MaxDeviation: 0.5, CanonicalEdges: 4));

        Assert.Equal(
            ["Yes", "6 → 3", "1", "24 → 0", "2", "3", "4", 0.5.ToString("0.####", CultureInfo.CurrentCulture), "1", "Yes", "Yes"],
            row.Details.Select(d => d.Value));
    }
}
