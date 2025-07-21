namespace Tinkwell.Bootstrapper.Hosting;

/// <summary>
/// Generic interface implemented by plugins in charge of performing an initialization action.
/// </summary>
/// <remarks>
/// Usually this interface is used for actions that do not need the rest of the application: for
/// example setting up the environment and similar tasks.
/// </remarks>
public interface IApplicationInitializer
{
    /// <summary>
    /// Performs an initialization.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <c>Task</c> representing the asynchronous operation.</returns>
    Task InitializeAsync(CancellationToken cancellationToken);
}