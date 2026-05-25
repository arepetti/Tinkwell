using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tinkwell.Studio.ViewModels;

namespace Tinkwell.Studio.Views;

/// <summary>
/// Code-behind for the grouped measures view. The grouped layout renders
/// one <c>TableView</c> per category, but only one row should ever look
/// selected and the drawer must follow whichever row was just clicked.
/// We can't rely on a TwoWay <c>SelectedItem</c> binding to a shared
/// view-model property: when one table pushes the new selection up, the
/// sibling tables react to the property change by setting their own
/// <c>SelectedItem</c> to a value that isn't in their items source — most
/// of them collapse it back to <c>null</c>, which round-trips through the
/// binding and immediately closes the drawer.
///
/// Instead the view tracks every inner table that gets loaded, hooks each
/// one's <c>SelectionChanged</c> event, and:
///   • on a row select, pushes that row to <c>VM.Selected</c> and clears
///     the visual selection on every other table;
///   • when <c>VM.Selected</c> goes to <c>null</c> (drawer closed), clears
///     all tables in one pass so the highlight doesn't linger.
/// </summary>
public sealed partial class MeasuresView : UserControl
{
    private readonly List<WinUI.TableView.TableView> _innerTables = new();
    private MeasuresViewModel? _vm;
    private bool _suppressSelectionPush;

    public MeasuresView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm = args.NewValue as MeasuresViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        foreach (var tv in _innerTables.ToArray())
            DetachTable(tv);
        _innerTables.Clear();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MeasuresViewModel.Selected))
            return;
        if (_vm?.Selected is null)
            ClearAllTableSelections();
    }

    public void OnInnerTableLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not WinUI.TableView.TableView tv)
            return;
        if (_innerTables.Contains(tv))
            return;
        _innerTables.Add(tv);
        tv.SelectionChanged += OnInnerTableSelectionChanged;
    }

    public void OnInnerTableUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is WinUI.TableView.TableView tv)
            DetachTable(tv);
    }

    private void DetachTable(WinUI.TableView.TableView tv)
    {
        tv.SelectionChanged -= OnInnerTableSelectionChanged;
        _innerTables.Remove(tv);
    }

    private void OnInnerTableSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionPush || _vm is null)
            return;
        if (sender is not WinUI.TableView.TableView source)
            return;
        // We only act on a fresh selection. SelectionChanged also fires when
        // we programmatically clear sibling tables below; ignoring those
        // (AddedItems empty) avoids reentrancy.
        if (e.AddedItems.Count == 0)
            return;
        if (source.SelectedItem is not MeasureEntry entry)
            return;

        try
        {
            _suppressSelectionPush = true;
            foreach (var other in _innerTables)
            {
                if (!ReferenceEquals(other, source) && other.SelectedItem is not null)
                    other.SelectedItem = null;
            }
            _vm.Selected = entry;
        }
        finally
        {
            _suppressSelectionPush = false;
        }
    }

    private void ClearAllTableSelections()
    {
        try
        {
            _suppressSelectionPush = true;
            foreach (var tv in _innerTables)
            {
                if (tv.SelectedItem is not null)
                    tv.SelectedItem = null;
            }
        }
        finally
        {
            _suppressSelectionPush = false;
        }
    }
}
