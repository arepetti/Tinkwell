
namespace Tinkwell.Bootstrapper.Ipc;

/// <summary>
/// Represent a factory for <see cref="INamedPipeServer"/> instances.
/// </summary>
public interface INamedPipeServerFactory
{
    /// <summary>
    /// Creates a  new instance of <see cref="INamedPipeServer"/>.
    /// </summary>
    /// <returns>
    /// A new instance of <c>INamedPipeServer</c>.
    /// </returns>
    INamedPipeServer Create();
}
