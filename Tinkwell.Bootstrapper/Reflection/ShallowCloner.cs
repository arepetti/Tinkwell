using System.Reflection;

namespace Tinkwell.Bootstrapper.Reflection;

public static class ShallowCloner
{
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