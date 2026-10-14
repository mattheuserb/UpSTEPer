using System.Windows.Input;

namespace UpSTEPer.App.ViewModels;

/// <summary>An entry of File → Recent files.</summary>
/// <param name="Number">Position from 1; also the access key.</param>
/// <param name="FullPath">The file (shown as tooltip).</param>
/// <param name="OpenCommand">Opens the file (parameter: <paramref name="FullPath"/>).</param>
internal sealed record RecentFileEntry(int Number, string FullPath, ICommand OpenCommand)
{
    /// <summary>Gets the menu text, e.g. "_1 bracket.stp" (the underscore marks the access key).</summary>
    public string Header => $"_{Number} {Path.GetFileName(FullPath).Replace("_", "__", StringComparison.Ordinal)}";
}
