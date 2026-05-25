using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tinkwell.Studio.ViewModels;

namespace Tinkwell.Studio.Views;

/// <summary>
/// Code-behind for the grouped store view. Mirrors <see cref="MeasuresView"/>:
/// each bucket renders its own <c>TableView</c>, but the drawer follows a
/// single shared selection. A naive TwoWay <c>SelectedItem</c> binding back
/// to the view-model would round-trip through the sibling tables and
/// collapse the selection (the new value isn't in their items source), so
/// we track the loaded tables manually and coordinate the highlight in
/// code.
/// </summary>
public sealed partial class StoreView : UserControl
{
    private readonly List<WinUI.TableView.TableView> _innerTables = new();
    private StoreViewModel? _vm;
    private bool _suppressSelectionPush;

    public StoreView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm = args.NewValue as StoreViewModel;
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
        if (e.PropertyName != nameof(StoreViewModel.Selected))
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
        if (e.AddedItems.Count == 0)
            return;
        if (source.SelectedItem is not StoreEntry entry)
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
