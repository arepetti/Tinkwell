using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Tinkwell.Studio.Controls;

/// <summary>
/// Reusable layout shell for the Studio category pages. Wraps the recurring
/// "list / drawer / toolbar" structure (right-docked drawer + bordered toolbar
/// strip + content area) so individual views only have to fill the content
/// slots: <see cref="Toolbar"/>, <see cref="ToolbarRight"/>, <see cref="List"/>,
/// <see cref="Detail"/>, and (optionally) <see cref="DrawerHeader"/>.
/// </summary>
/// <remarks>
/// All slots are <c>object</c>-typed dependency properties because XAML's
/// content-property syntax (e.g. <c>&lt;controls:WorkspacePage.List&gt;...</c>)
/// only works on properties of type <c>object</c>. The control itself does not
/// own a view-model; <see cref="IsDrawerOpen"/>, <see cref="CloseDrawerCommand"/>
/// and friends are forwarded straight to the consumer's bindings.
/// </remarks>
public sealed partial class WorkspacePage : UserControl
{
    public WorkspacePage()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty ToolbarProperty =
        DependencyProperty.Register(nameof(Toolbar), typeof(object), typeof(WorkspacePage),
            new PropertyMetadata(null));

    /// <summary>Left-aligned content of the toolbar strip (actions, counters,
    /// status messages). Bound into the <c>*</c>-width column of the toolbar grid.</summary>
    public object? Toolbar
    {
        get => GetValue(ToolbarProperty);
        set => SetValue(ToolbarProperty, value);
    }

    public static readonly DependencyProperty ToolbarRightProperty =
        DependencyProperty.Register(nameof(ToolbarRight), typeof(object), typeof(WorkspacePage),
            new PropertyMetadata(null));

    /// <summary>Right-aligned content of the toolbar strip (typically a filter
    /// textbox). Bound into the <c>Auto</c>-width column of the toolbar grid.</summary>
    public object? ToolbarRight
    {
        get => GetValue(ToolbarRightProperty);
        set => SetValue(ToolbarRightProperty, value);
    }

    public static readonly DependencyProperty ListProperty =
        DependencyProperty.Register(nameof(List), typeof(object), typeof(WorkspacePage),
            new PropertyMetadata(null));

    /// <summary>Main content area below the toolbar. Usually a <c>TableView</c>
    /// but anything with a <c>SelectedItem</c>/two-way binding works.</summary>
    public object? List
    {
        get => GetValue(ListProperty);
        set => SetValue(ListProperty, value);
    }

    public static readonly DependencyProperty DetailProperty =
        DependencyProperty.Register(nameof(Detail), typeof(object), typeof(WorkspacePage),
            new PropertyMetadata(null));

    /// <summary>Body of the right-docked drawer; rendered below the header.</summary>
    public object? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public static readonly DependencyProperty DrawerHeaderProperty =
        DependencyProperty.Register(nameof(DrawerHeader), typeof(object), typeof(WorkspacePage),
            new PropertyMetadata(null));

    /// <summary>Optional custom drawer header. When set, replaces the default
    /// <see cref="DrawerTitle"/> TextBlock; the close button stays in place.</summary>
    public object? DrawerHeader
    {
        get => GetValue(DrawerHeaderProperty);
        set => SetValue(DrawerHeaderProperty, value);
    }

    public static readonly DependencyProperty DrawerTitleProperty =
        DependencyProperty.Register(nameof(DrawerTitle), typeof(string), typeof(WorkspacePage),
            new PropertyMetadata(string.Empty));

    /// <summary>Convenience header text shown when no <see cref="DrawerHeader"/>
    /// is provided. Bound to a SubtitleTextBlockStyle TextBlock.</summary>
    public string DrawerTitle
    {
        get => (string)GetValue(DrawerTitleProperty);
        set => SetValue(DrawerTitleProperty, value);
    }

    public static readonly DependencyProperty IsDrawerOpenProperty =
        DependencyProperty.Register(nameof(IsDrawerOpen), typeof(bool), typeof(WorkspacePage),
            new PropertyMetadata(false));

    /// <summary>Drives <c>SplitView.IsPaneOpen</c>. Two-way semantics aren't
    /// required because the only path that closes the drawer is the close
    /// button which fires <see cref="CloseDrawerCommand"/>.</summary>
    public bool IsDrawerOpen
    {
        get => (bool)GetValue(IsDrawerOpenProperty);
        set => SetValue(IsDrawerOpenProperty, value);
    }

    public static readonly DependencyProperty DrawerWidthProperty =
        DependencyProperty.Register(nameof(DrawerWidth), typeof(double), typeof(WorkspacePage),
            new PropertyMetadata(460.0));

    /// <summary>Width of the drawer when open. Defaults to 460px which matches
    /// the original ServicesView; smaller pages (Runners, Measures) tighten
    /// this to 420 by setting the property explicitly.</summary>
    public double DrawerWidth
    {
        get => (double)GetValue(DrawerWidthProperty);
        set => SetValue(DrawerWidthProperty, value);
    }

    public static readonly DependencyProperty CloseDrawerCommandProperty =
        DependencyProperty.Register(nameof(CloseDrawerCommand), typeof(ICommand), typeof(WorkspacePage),
            new PropertyMetadata(null));

    /// <summary>Command bound to the close button in the drawer header.</summary>
    public ICommand? CloseDrawerCommand
    {
        get => (ICommand?)GetValue(CloseDrawerCommandProperty);
        set => SetValue(CloseDrawerCommandProperty, value);
    }

    public static readonly DependencyProperty ShowDrawerCloseButtonProperty =
        DependencyProperty.Register(nameof(ShowDrawerCloseButton), typeof(bool), typeof(WorkspacePage),
            new PropertyMetadata(true));

    /// <summary>Set to <c>false</c> to hide the close button (e.g. for drawers
    /// that are dismissed by some other affordance in the body).</summary>
    public bool ShowDrawerCloseButton
    {
        get => (bool)GetValue(ShowDrawerCloseButtonProperty);
        set => SetValue(ShowDrawerCloseButtonProperty, value);
    }
}
