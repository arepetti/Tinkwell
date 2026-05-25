using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tinkwell.Studio.ViewModels;

namespace Tinkwell.Studio.Views;

/// <summary>
/// Picks the right <see cref="DataTemplate"/> for each category view model so
/// the shell's single ContentControl renders Home / Runners / Services / ... from
/// the same binding. WinUI has no auto-discovery for <c>DataTemplate DataType=</c>
/// the way Avalonia does; this class is the WinUI-native substitute.
/// </summary>
public sealed class CategoryViewTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HomeTemplate { get; set; }
    public DataTemplate? RunnersTemplate { get; set; }
    public DataTemplate? ServicesTemplate { get; set; }
    public DataTemplate? StoreTemplate { get; set; }
    public DataTemplate? MeasuresTemplate { get; set; }
    public DataTemplate? EventsTemplate { get; set; }
    public DataTemplate? MqttTemplate { get; set; }
    public DataTemplate? CoapTemplate { get; set; }
    public DataTemplate? EnsembleTemplate { get; set; }
    public DataTemplate? CommandLogTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
        => item switch
        {
            HomeViewModel => HomeTemplate!,
            RunnersViewModel => RunnersTemplate!,
            ServicesViewModel => ServicesTemplate!,
            StoreViewModel => StoreTemplate!,
            MeasuresViewModel => MeasuresTemplate!,
            EventsViewModel => EventsTemplate!,
            MqttViewModel => MqttTemplate!,
            CoapViewModel => CoapTemplate!,
            EnsembleViewModel => EnsembleTemplate!,
            CommandLogViewModel => CommandLogTemplate!,
            _ => base.SelectTemplateCore(item),
        };

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
