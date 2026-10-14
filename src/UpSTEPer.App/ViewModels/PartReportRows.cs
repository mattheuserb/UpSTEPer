using System.Globalization;
using UpSTEPer.App.Localization;
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
        PartStatus.Error => Strings.PartStatus_CheckFailed,
        PartStatus.Warning => Strings.PartStatus_Problems,
        _ => Strings.PartStatus_Ok,
    };

    /// <summary>Gets the findings in one line, e.g. "24 free edges · 3 mergeable faces".</summary>
    public string Findings
    {
        get
        {
            var parts = new List<string>();
            Add(parts, Part.FreeEdges, Strings.Findings_FreeEdges_One, Strings.Findings_FreeEdges_Many);
            Add(parts, Part.InvalidShapes, Strings.Findings_Invalid_One, Strings.Findings_Invalid_Many);
            Add(parts, Part.CanonicalFaces, Strings.Findings_CanonicalFaces_One, Strings.Findings_CanonicalFaces_Many);
            Add(parts, Part.CanonicalEdges, Strings.Findings_CanonicalEdges_One, Strings.Findings_CanonicalEdges_Many);
            Add(parts, Part.MergeableFaces, Strings.Findings_Mergeable_One, Strings.Findings_Mergeable_Many);
            Add(parts, Part.SmallEdges, Strings.Findings_SmallEdges_One, Strings.Findings_SmallEdges_Many);
            Add(parts, Part.FailedChecks, Strings.Findings_FailedChecks_One, Strings.Findings_FailedChecks_Many);
            return parts.Count == 0 ? Strings.Findings_None : string.Join(" · ", parts);
        }
    }

    /// <summary>Gets every value of the check.</summary>
    public IReadOnlyList<DetailItem> Details =>
    [
        Item(Strings.Detail_Solids, Part.SolidCount),
        Item(Strings.Detail_Faces, Part.FaceCount),
        Item(Strings.Detail_FreeEdges, Part.FreeEdges),
        Item(Strings.Detail_Invalid, Part.InvalidShapes),
        new(Strings.Detail_SplineFaces, Pair(Part.SplineFaces, Part.CanonicalFaces)),
        new(Strings.Detail_SplineEdges, Pair(Part.SplineEdges, Part.CanonicalEdges)),
        Item(Strings.Detail_Mergeable, Part.MergeableFaces),
        Item(Strings.Detail_SmallEdges, Part.SmallEdges),
        Item(Strings.Detail_FailedChecks, Part.FailedChecks),
    ];

    // one / many: formats with the count as {0}.
    internal static void Add(List<string> parts, int count, string one, string many)
    {
        if (count > 0)
        {
            parts.Add(Localizer.Format(count == 1 ? one : many, count));
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
        PartStatus.Error => Strings.PartStatus_StepFailed,
        PartStatus.Warning when Part.Reverted => Strings.PartStatus_StepSkipped,
        PartStatus.Warning when Part.FreeEdgesAfter > 0 || Part.InvalidAfter > 0 => Strings.PartStatus_DefectsRemain,
        PartStatus.Warning => Strings.PartStatus_ColoursMerged,
        PartStatus.Ok => Strings.PartStatus_Repaired,
        _ => Strings.PartStatus_Unchanged,
    };

    /// <summary>Gets the changes in one line, e.g. "Faces 6 → 3 · Free edges 24 → 0 · 3 exact surfaces".</summary>
    public string Changes
    {
        get
        {
            var parts = new List<string>();
            AddChange(parts, Strings.Changes_Faces, Part.FacesBefore, Part.FacesAfter);
            AddChange(parts, Strings.Changes_Solids, Part.SolidsBefore, Part.SolidsAfter);
            AddChange(parts, Strings.Changes_FreeEdges, Part.FreeEdgesBefore, Part.FreeEdgesAfter);
            AddChange(parts, Strings.Changes_Invalid, Part.InvalidBefore, Part.InvalidAfter);
            CheckRow.Add(parts, Part.CanonicalFaces, Strings.Changes_ExactSurfaces_One, Strings.Changes_ExactSurfaces_Many);
            CheckRow.Add(parts, Part.CanonicalEdges, Strings.Changes_ExactEdges_One, Strings.Changes_ExactEdges_Many);
            if (Part.CanonicalFaces + Part.CanonicalEdges > 0)
            {
                parts.Add(Localizer.Format(Strings.Changes_MaxDeviation, Deviation(Part.MaxDeviation)));
            }

            CheckRow.Add(parts, Part.ColourConflicts, Strings.Changes_ColourConflicts_One, Strings.Changes_ColourConflicts_Many);
            if (Part.Reverted)
            {
                parts.Add(Strings.Changes_Skipped);
            }

            if (Part.Failed)
            {
                parts.Add(Strings.Changes_StepFailed);
            }

            return parts.Count == 0 ? Strings.Changes_None : string.Join(" · ", parts);
        }
    }

    /// <summary>Gets every value of the repair report.</summary>
    public IReadOnlyList<DetailItem> Details =>
    [
        new(Strings.Detail_Changed, YesNo(Part.Changed)),
        Change(Strings.Detail_Faces, Part.FacesBefore, Part.FacesAfter),
        Change(Strings.Detail_Solids, Part.SolidsBefore, Part.SolidsAfter),
        Change(Strings.Detail_FreeEdges, Part.FreeEdgesBefore, Part.FreeEdgesAfter),
        Change(Strings.Detail_Invalid, Part.InvalidBefore, Part.InvalidAfter),
        CheckRow.Item(Strings.Detail_ExactSurfaces, Part.CanonicalFaces),
        CheckRow.Item(Strings.Detail_ExactEdges, Part.CanonicalEdges),
        new(Strings.Detail_MaxDeviation, Deviation(Part.MaxDeviation)),
        CheckRow.Item(Strings.Detail_ColourConflicts, Part.ColourConflicts),
        new(Strings.Detail_StepSkipped, YesNo(Part.Reverted)),
        new(Strings.Detail_StepFailed, YesNo(Part.Failed)),
    ];

    private static string YesNo(bool value) => value ? Strings.Common_Yes : Strings.Common_No;

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
