using System.Globalization;
using UpSTEPer.Core;

namespace UpSTEPer.App.ViewModels;

/// <summary>Outcome class of a part in the check and repair dialog; shown with an icon, a text and a colour.</summary>
internal enum PartStatus
{
    /// <summary>Nothing to do or everything repaired.</summary>
    Ok = 0,

    /// <summary>The repair left the part as it was, and it had no problems to begin with.</summary>
    Unchanged = 1,

    /// <summary>Problems found or left, a step skipped, or colours merged.</summary>
    Warning = 2,

    /// <summary>A check or repair step failed on broken geometry.</summary>
    Error = 3,
}

/// <summary>One labelled value in the details of a part.</summary>
/// <param name="Label">What the value is.</param>
/// <param name="Value">The value, already formatted.</param>
internal sealed record DetailItem(string Label, string Value);

/// <summary>A part's check result as the dialog shows it: status, the findings in one line, all values in the details.</summary>
/// <param name="Part">The check result.</param>
internal sealed record CheckRow(PartCheck Part)
{
    /// <summary>Gets the status: failed sub-checks are errors, any other finding a warning.</summary>
    public PartStatus Status => Part.FailedChecks > 0 ? PartStatus.Error : Part.HasProblems ? PartStatus.Warning : PartStatus.Ok;

    /// <summary>Gets the status as text.</summary>
    public string StatusText => Status switch
    {
        PartStatus.Error => "Check failed",
        PartStatus.Warning => "Problems",
        _ => "OK",
    };

    /// <summary>Gets the findings in one line, e.g. "24 free edges · 3 mergeable faces".</summary>
    public string Findings
    {
        get
        {
            var parts = new List<string>();
            Add(parts, Part.FreeEdges, "free edge", "free edges");
            Add(parts, Part.InvalidShapes, "invalid sub-shape", "invalid sub-shapes");
            Add(parts, Part.CanonicalFaces, "spline face can be exact", "spline faces can be exact");
            Add(parts, Part.CanonicalEdges, "spline edge can be exact", "spline edges can be exact");
            Add(parts, Part.MergeableFaces, "mergeable face", "mergeable faces");
            Add(parts, Part.SmallEdges, "small edge", "small edges");
            Add(parts, Part.FailedChecks, "sub-check failed", "sub-checks failed");
            return parts.Count == 0 ? "No problems" : string.Join(" · ", parts);
        }
    }

    /// <summary>Gets every value of the check.</summary>
    public IReadOnlyList<DetailItem> Details =>
    [
        Item("Solids", Part.SolidCount),
        Item("Faces", Part.FaceCount),
        Item("Free edges", Part.FreeEdges),
        Item("Invalid sub-shapes", Part.InvalidShapes),
        new("Spline faces (exact possible)", Pair(Part.SplineFaces, Part.CanonicalFaces)),
        new("Spline edges (exact possible)", Pair(Part.SplineEdges, Part.CanonicalEdges)),
        Item("Mergeable faces", Part.MergeableFaces),
        Item("Small edges", Part.SmallEdges),
        Item("Failed sub-checks", Part.FailedChecks),
    ];

    internal static void Add(List<string> parts, int count, string singular, string plural)
    {
        if (count > 0)
        {
            parts.Add(string.Create(CultureInfo.CurrentCulture, $"{count:N0} {(count == 1 ? singular : plural)}"));
        }
    }

    internal static DetailItem Item(string label, int value) => new(label, value.ToString("N0", CultureInfo.CurrentCulture));

    private static string Pair(int total, int exact) => string.Create(CultureInfo.CurrentCulture, $"{total:N0} ({exact:N0})");
}

/// <summary>A part's repair result as the dialog shows it: status, the changes in one line, all values in the details.</summary>
/// <param name="Part">The repair result.</param>
internal sealed record RepairRow(PartRepair Part)
{
    /// <summary>
    /// Gets the status. Changed is not success by itself and unchanged is not a failure: what
    /// counts is whether a step failed or was skipped, defects remain, or colours had to be merged.
    /// </summary>
    public PartStatus Status =>
        Part.Failed ? PartStatus.Error
        : Part.Reverted || Part.FreeEdgesAfter > 0 || Part.InvalidAfter > 0 || Part.ColourConflicts > 0 ? PartStatus.Warning
        : Part.Changed ? PartStatus.Ok
        : PartStatus.Unchanged;

    /// <summary>Gets the status as text.</summary>
    public string StatusText => Status switch
    {
        PartStatus.Error => "Step failed",
        PartStatus.Warning when Part.Reverted => "Step skipped",
        PartStatus.Warning when Part.FreeEdgesAfter > 0 || Part.InvalidAfter > 0 => "Defects remain",
        PartStatus.Warning => "Colours merged",
        PartStatus.Ok => "Repaired",
        _ => "Unchanged",
    };

    /// <summary>Gets the changes in one line, e.g. "Faces 6 → 3 · Free edges 24 → 0 · 3 exact surfaces".</summary>
    public string Changes
    {
        get
        {
            var parts = new List<string>();
            AddChange(parts, "Faces", Part.FacesBefore, Part.FacesAfter);
            AddChange(parts, "Solids", Part.SolidsBefore, Part.SolidsAfter);
            AddChange(parts, "Free edges", Part.FreeEdgesBefore, Part.FreeEdgesAfter);
            AddChange(parts, "Invalid", Part.InvalidBefore, Part.InvalidAfter);
            CheckRow.Add(parts, Part.CanonicalFaces, "exact surface", "exact surfaces");
            CheckRow.Add(parts, Part.CanonicalEdges, "exact edge", "exact edges");
            if (Part.CanonicalFaces + Part.CanonicalEdges > 0)
            {
                parts.Add($"max. deviation {Deviation(Part.MaxDeviation)} mm");
            }

            CheckRow.Add(parts, Part.ColourConflicts, "colour conflict", "colour conflicts");
            if (Part.Reverted)
            {
                parts.Add("a step made it worse and was skipped");
            }

            if (Part.Failed)
            {
                parts.Add("a step failed");
            }

            return parts.Count == 0 ? "No changes" : string.Join(" · ", parts);
        }
    }

    /// <summary>Gets every value of the repair report.</summary>
    public IReadOnlyList<DetailItem> Details =>
    [
        new("Changed", Part.Changed ? "Yes" : "No"),
        Change("Faces", Part.FacesBefore, Part.FacesAfter),
        Change("Solids", Part.SolidsBefore, Part.SolidsAfter),
        Change("Free edges", Part.FreeEdgesBefore, Part.FreeEdgesAfter),
        Change("Invalid sub-shapes", Part.InvalidBefore, Part.InvalidAfter),
        CheckRow.Item("Exact surfaces", Part.CanonicalFaces),
        CheckRow.Item("Exact edges", Part.CanonicalEdges),
        new("Max. deviation (mm)", Deviation(Part.MaxDeviation)),
        CheckRow.Item("Colour conflicts", Part.ColourConflicts),
        new("Step skipped (worse)", Part.Reverted ? "Yes" : "No"),
        new("Step failed", Part.Failed ? "Yes" : "No"),
    ];

    // Four decimals; a deviation that would round to zero but is not zero says so.
    private static string Deviation(double millimetres) =>
        millimetres is > 0 and < 0.00005
            ? "< " + 0.0001.ToString("0.####", CultureInfo.CurrentCulture)
            : millimetres.ToString("0.####", CultureInfo.CurrentCulture);

    private static void AddChange(List<string> parts, string label, int before, int after)
    {
        if (before != after)
        {
            parts.Add(string.Create(CultureInfo.CurrentCulture, $"{label} {before:N0} → {after:N0}"));
        }
    }

    private static DetailItem Change(string label, int before, int after) =>
        new(label, before == after
            ? before.ToString("N0", CultureInfo.CurrentCulture)
            : string.Create(CultureInfo.CurrentCulture, $"{before:N0} → {after:N0}"));
}
