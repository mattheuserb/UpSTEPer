using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using UpSTEPer.App.ViewModels;

namespace UpSTEPer.App.Controls;

/// <summary>
/// A part status as icon, text and colour (never colour alone): pseudo-classes <c>:ok</c>,
/// <c>:unchanged</c>, <c>:warning</c> and <c>:error</c> select icon and brush in the control theme.
/// </summary>
internal sealed class StatusBadge : TemplatedControl
{
    /// <summary>Defines the <see cref="Status"/> property.</summary>
    public static readonly StyledProperty<PartStatus> StatusProperty =
        AvaloniaProperty.Register<StatusBadge, PartStatus>(nameof(Status));

    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<StatusBadge, string?>(nameof(Text));

    /// <summary>Initializes a new instance of the <see cref="StatusBadge"/> class.</summary>
    public StatusBadge() => UpdatePseudoClasses(Status);

    /// <summary>Gets or sets the status.</summary>
    public PartStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>Gets or sets the text next to the icon.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            UpdatePseudoClasses(change.GetNewValue<PartStatus>());
        }
    }

    private void UpdatePseudoClasses(PartStatus status)
    {
        PseudoClasses.Set(":ok", status == PartStatus.Ok);
        PseudoClasses.Set(":unchanged", status == PartStatus.Unchanged);
        PseudoClasses.Set(":warning", status == PartStatus.Warning);
        PseudoClasses.Set(":error", status == PartStatus.Error);
    }
}
