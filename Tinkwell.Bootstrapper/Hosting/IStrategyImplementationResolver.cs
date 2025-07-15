namespace Tinkwell.Bootstrapper.Hosting;

/// <summary>
/// Interface implemented by the factory object for strategies.
/// </summary>
/// <remarks>
/// If you implemented a strategy to customize or extend application behaviour then you
/// will need a class implementing this interface in your assembly. It'll be called when
/// the assembly has been loaded to give you a chance to setup your DI and queried to obtain
/// the type of the class implementing the strategy.
/// </remarks>
public interface IStrategyImplementationResolver : IHostedAssemblyRegistrar
{
    /// <summary>
    /// Obtains the type implementing the strategy represented by this object.
    /// </summary>
    /// <returns>
    /// The type of the object implementing the specified strategy contract or
    /// <c>null</c> if the specified strategy is not supported.
    /// </returns>
    Type? GetImplementationType(Type interfaceType);

    /// <summary>
    /// Obtains the name of the strategy represented by this object.
    /// </summary>
    /// <returns>
    /// The name of the implementation of the specified strategy contract or
    /// <c>null</c> if the specified strategy is not supported.
    /// </returns>
    string? GetImplementationName(Type interfaceType);

    /// <summary>
    /// Determines whether the specified strategy contract is supported.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the contract is supported.
    /// </returns>
    bool IsSupported(Type interfaceType)
        => GetImplementationType(interfaceType) is not null;
}