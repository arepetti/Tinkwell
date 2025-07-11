namespace Tinkwell.Bootstrapper.Ensamble;

/// <summary>
/// Defines a contract for conditional definitions that may have an associated condition string.
/// </summary>
public interface IConditionalDefinition
{
    /// <summary>
    /// Gets the condition string for the definition, or null if none is set.
    /// </summary>
    string? Condition { get; }
}

/// <summary>
/// Defines a contract for evaluating conditions and filtering ensamble definitions.
/// </summary>
public interface IEnsambleConditionEvaluator
{
    /// <summary>
    /// Filters the provided definitions based on their conditions.
    /// </summary>
    /// <typeparam name="T">The type of definition, must implement <see cref="IConditionalDefinition"/>.</typeparam>
    /// <param name="definitions">The definitions to filter.</param>
    /// <returns>The filtered definitions.</returns>
    IEnumerable<T> Filter<T>(IEnumerable<T> definitions) where T : IConditionalDefinition;

    /// <summary>
    /// Gets the parameters used for condition evaluation.
    /// </summary>
    /// <returns>A dictionary of parameter names and values.</returns>
    Dictionary<string, string?> GetParameters();
}