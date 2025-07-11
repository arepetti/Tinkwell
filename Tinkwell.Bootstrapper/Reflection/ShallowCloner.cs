using System.Reflection;

namespace Tinkwell.Bootstrapper.Reflection;

/// <summary>
/// Provides shallow cloning utilities for copying public properties between objects.
/// </summary>
public static class ShallowCloner
{
    /// <summary>
    /// Copies all public properties from the source object to the target object.
    /// </summary>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TTarget">The type of the target object.</typeparam>
    /// <param name="source">The source object.</param>
    /// <param name="target">The target object.</param>
    /// <returns>The target object with copied properties.</returns>
    /// <exception cref="ArgumentNullException">Thrown if source or target is null.</exception>
    public static TTarget CopyAllPublicProperties<TSource, TTarget>(TSource source, TTarget target)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));
        ArgumentNullException.ThrowIfNull(target, nameof(target));

        var sourceType = source.GetType();
        var targetType = target.GetType();

        foreach (var sourceProperty in sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var targetProperty = targetType.GetProperty(sourceProperty.Name, BindingFlags.Public | BindingFlags.Instance);
            if (targetProperty is not null)
                targetProperty.SetValue(target, sourceProperty.GetValue(source));
        }

        return target;
    }
}