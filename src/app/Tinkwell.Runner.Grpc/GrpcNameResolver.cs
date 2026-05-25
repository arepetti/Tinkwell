using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Grpc.Core;

namespace Tinkwell.Runner.Grpc;

/// <summary>
/// Resolves the protobuf fully-qualified service name from a gRPC service
/// implementation type by reflecting over the generated base class hierarchy.
/// </summary>
internal static class GrpcNameResolver
{
    /// <summary>
    /// Returns the protobuf <c>ServiceDescriptor.FullName</c> for the given
    /// gRPC service implementation type (e.g. <c>"tinkwell.sensors.TemperatureReader"</c>).
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The type is not a compatible gRPC service implementation.
    /// </exception>
    public static string Resolve(Type serviceType)
    {
        var baseType = serviceType.BaseType;
        ThrowIfNull(baseType, serviceType);

        var bindAttr = baseType.GetCustomAttribute<BindServiceMethodAttribute>();
        ThrowIfNull(bindAttr, serviceType);

        var bindType = bindAttr.BindType;
        ThrowIfNull(bindType, serviceType);

        var descriptorProp = bindType.GetProperty(
            "Descriptor",
            BindingFlags.Public | BindingFlags.Static);
        ThrowIfNull(descriptorProp, serviceType);

        var descriptor = descriptorProp.GetValue(null);
        ThrowIfNull(descriptor, serviceType);

        var fullNameProp = descriptorProp.PropertyType.GetProperty("FullName");
        ThrowIfNull(fullNameProp, serviceType);

        return Convert.ToString(fullNameProp.GetValue(descriptor), CultureInfo.InvariantCulture)
            ?? throw new NotSupportedException(
                $"Type {serviceType.FullName} is not a compatible gRPC service.");
    }

    private static void ThrowIfNull(
        [NotNull] object? value, Type serviceType)
    {
        if (value is null)
            throw new NotSupportedException(
                $"Type {serviceType.FullName} is not a compatible gRPC service.");
    }
}
