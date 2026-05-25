using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace Tinkwell.Studio;

/// <summary>
/// Entry point. Bootstraps the WinUI 3 XAML dispatcher queue and STA COM, then
/// hands control to <see cref="App"/>. Standard shape for an unpackaged WinUI 3
/// app: <see cref="Application.Start"/> owns the thread until the main window
/// closes.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var queue = DispatcherQueue.GetForCurrentThread();
            var context = new DispatcherQueueSynchronizationContext(queue);
            SynchronizationContext.SetSynchronizationContext(context);
            // Construction registers the App instance on Application.Current.
            new App();
        });
    }
}
