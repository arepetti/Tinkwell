using Tinkwell.Encoding;

namespace Tinkwell.Lwm2m;

/// <summary>
/// Curated registry of commonly used IPSO Smart Objects. Object IDs 3300–3399 are the
/// OMA IPSO range; this class lists a subset the library currently models, not the full
/// OMA registry.
/// Full registry: https://technical.openmobilealliance.org/OMNA/LwM2M/LwM2MRegistry.html
/// </summary>
public static class IpsoObjectRegistry
{
    /// <summary>
    /// Common IPSO resource IDs shared across sensor objects
    /// (OMA-TS-LightweightM2M_Core-V1_1, Appendix D).
    /// </summary>
    public static class CommonResources
    {
        /// <summary>IPSO resource 5700: last measured value from the sensor.</summary>
        public const int SensorValue = 5700;
        /// <summary>IPSO resource 5601: minimum value observed in the current measurement period.</summary>
        public const int MinMeasuredValue = 5601;
        /// <summary>IPSO resource 5602: maximum value observed in the current measurement period.</summary>
        public const int MaxMeasuredValue = 5602;
        /// <summary>IPSO resource 5603: minimum value of the sensor's operable range.</summary>
        public const int MinRangeValue = 5603;
        /// <summary>IPSO resource 5604: maximum value of the sensor's operable range.</summary>
        public const int MaxRangeValue = 5604;
        /// <summary>IPSO resource 5701: unit for the sensor value (e.g. <c>°C</c>).</summary>
        public const int SensorUnits = 5701;
        /// <summary>IPSO resource 5605: resets min/max measured value statistics.</summary>
        public const int ResetMinMaxMeasuredValues = 5605;
        /// <summary>IPSO resource 5750: human-readable application type string.</summary>
        public const int ApplicationType = 5750;
    }

    private static readonly Lwm2mResourceDefinition SensorValueResource = new(
        CommonResources.SensorValue, "Sensor Value",
        PayloadType.Float, Lwm2mOperations.Read, Mandatory: true);

    private static readonly Lwm2mResourceDefinition SensorUnitsResource = new(
        CommonResources.SensorUnits, "Sensor Units",
        PayloadType.String, Lwm2mOperations.Read);

    private static readonly Lwm2mResourceDefinition MinMeasuredResource = new(
        CommonResources.MinMeasuredValue, "Min Measured Value",
        PayloadType.Float, Lwm2mOperations.Read);

    private static readonly Lwm2mResourceDefinition MaxMeasuredResource = new(
        CommonResources.MaxMeasuredValue, "Max Measured Value",
        PayloadType.Float, Lwm2mOperations.Read);

    private static readonly Lwm2mResourceDefinition AppTypeResource = new(
        CommonResources.ApplicationType, "Application Type",
        PayloadType.String, Lwm2mOperations.ReadWrite);

    private static readonly Lwm2mResourceDefinition[] StandardSensorResources =
    [
        SensorValueResource,
        SensorUnitsResource,
        MinMeasuredResource,
        MaxMeasuredResource,
        AppTypeResource,
    ];

    private static readonly Dictionary<int, Lwm2mObjectDefinition> Objects = new()
    {
        [3] = new(3, "Device"),
        [3300] = new(3300, "Generic Sensor", Resources: StandardSensorResources),
        [3301] = new(3301, "Illuminance", Resources: StandardSensorResources),
        [3302] = new(3302, "Presence", Resources: StandardSensorResources),
        [3303] = new(3303, "Temperature", Resources: StandardSensorResources),
        [3304] = new(3304, "Humidity", Resources: StandardSensorResources),
        [3305] = new(3305, "Power Measurement", Resources: StandardSensorResources),
        [3306] = new(3306, "Actuation", Resources:
        [
            new(5850, "On/Off", PayloadType.Boolean, Lwm2mOperations.ReadWrite, Mandatory: true),
            AppTypeResource,
        ]),
        [3308] = new(3308, "Set Point", Resources:
        [
            new(5900, "Set Point Value", PayloadType.Float, Lwm2mOperations.ReadWrite, Mandatory: true),
            SensorUnitsResource,
            AppTypeResource,
        ]),
        [3310] = new(3310, "Load Control"),
        [3311] = new(3311, "Light Control", Resources:
        [
            new(5850, "On/Off", PayloadType.Boolean, Lwm2mOperations.ReadWrite, Mandatory: true),
            new(5851, "Dimmer", PayloadType.Integer, Lwm2mOperations.ReadWrite),
            AppTypeResource,
        ]),
        [3313] = new(3313, "Accelerometer", Resources: StandardSensorResources),
        [3314] = new(3314, "Magnetometer", Resources: StandardSensorResources),
        [3315] = new(3315, "Barometer", Resources: StandardSensorResources),
        [3316] = new(3316, "Voltage", Resources: StandardSensorResources),
        [3317] = new(3317, "Current", Resources: StandardSensorResources),
        [3318] = new(3318, "Frequency", Resources: StandardSensorResources),
        [3323] = new(3323, "Pressure", Resources: StandardSensorResources),
        [3325] = new(3325, "Concentration", Resources: StandardSensorResources),
    };

    /// <summary>
    /// Looks up an object definition by ID. Returns null if unknown.
    /// </summary>
    /// <example>
    /// <para>Get metadata (name, resources) for a standard IPSO object such as 3303 (Temperature).</para>
    /// <code language="csharp">
    /// Lwm2mObjectDefinition? def = IpsoObjectRegistry.Find(3303);
    /// if (def is not null) { string name = def.Name; // "Temperature" }
    /// </code>
    /// </example>
    /// <param name="objectId">OMA object identifier (e.g. <c>3303</c> for Temperature).</param>
    public static Lwm2mObjectDefinition? Find(int objectId) =>
        Objects.GetValueOrDefault(objectId);

    /// <summary>
    /// Returns all registered object definitions.
    /// </summary>
    public static IReadOnlyCollection<Lwm2mObjectDefinition> All => Objects.Values;

    /// <summary>
    /// Returns true if the object ID is a known IPSO sensor object.
    /// </summary>
    /// <example>
    /// <para>Check whether a parsed path segment is a curated IPSO type before using <see cref="Find"/> details.</para>
    /// <code language="csharp">
    /// if (IpsoObjectRegistry.IsKnown(3303))
    /// {
    ///     Lwm2mObjectDefinition? def = IpsoObjectRegistry.Find(3303);
    /// }
    /// </code>
    /// </example>
    /// <param name="objectId">OMA object identifier to look up (e.g. <c>3303</c>).</param>
    public static bool IsKnown(int objectId) => Objects.ContainsKey(objectId);
}
