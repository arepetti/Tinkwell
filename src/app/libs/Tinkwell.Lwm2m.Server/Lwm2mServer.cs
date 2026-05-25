using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Coap;
using Tinkwell.Coap.Server;
using Tinkwell.Encoding;
using Tinkwell.Lwm2m.Registration;
using SysEncoding = System.Text.Encoding;

namespace Tinkwell.Lwm2m.Server;

/// <summary>
/// A standalone LwM2M server built on top of <see cref="CoapServer"/>.
/// Map IPSO resources to handlers with <see cref="MapResource(int, int, ILwm2mResourceHandler)"/>, then
/// start the server with <see cref="RunAsync(CancellationToken)"/>.
/// </summary>
/// <example>
/// <para>Typical use: set port/name options, create the server, map object resources, then run until cancellation.</para>
/// <code language="csharp">
/// var options = new Lwm2mServerOptions { Port = 5683, Name = "gateway" };
/// var server = new Lwm2mServer(options, logger: null);
/// server.MapResource(3303, 5700,
///     onRead: () => PayloadValue.FromFloat(ReadSensorCelsius()),
///     onWrite: v => SaveHighAlarmC((int)v.AsLong()));
/// await server.RunAsync(cancellationToken);
/// </code>
/// </example>
public sealed class Lwm2mServer : BackgroundService
{
    private readonly Lwm2mServerOptions _options;
    private readonly ILogger _logger;
    private readonly RegistrationDirectory _registrations = new();
    private readonly Dictionary<string, ResourceBinding> _resourceBindings = new();
    private CoapServer? _coapServer;

    /// <summary>Initializes a new instance of the <see cref="Lwm2mServer"/> class.</summary>
    /// <param name="options">Server configuration. Must not be null.</param>
    /// <param name="logger">Optional logger. When null, logging is a no-op.</param>
    public Lwm2mServer(Lwm2mServerOptions options, ILogger<Lwm2mServer>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _logger = logger ?? NullLogger<Lwm2mServer>.Instance;
    }

    /// <summary>
    /// Event raised when a new client registers. Handlers run on the thread handling the CoAP request.
    /// </summary>
    public event Action<Lwm2mRegistration>? ClientRegistered;

    /// <summary>
    /// Event raised when a client deregisters (e.g. DELETE to its registration). Handlers
    /// run on the thread handling the CoAP request. Expired registrations removed by the background purger
    /// are not reported here.
    /// </summary>
    public event Action<Lwm2mRegistration>? ClientDeregistered;

    /// <summary>The client registration directory.</summary>
    public RegistrationDirectory Registrations => _registrations;

    /// <summary>
    /// Maps an IPSO object/resource to a handler that will serve reads and writes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call <see cref="MapResource(int, int, ILwm2mResourceHandler)"/> (or the delegate overload) for
    /// all bindings before <see cref="RunAsync(CancellationToken)"/>
    /// (or <see cref="BackgroundService"/> start). The server does not synchronize concurrent
    /// <see cref="MapResource(int, int, ILwm2mResourceHandler)"/> calls with request handling; mapping
    /// after the server has
    /// started is not supported.
    /// </para>
    /// <para>
    /// Mapping the same <paramref name="objectId"/> and <paramref name="resourceId"/> again
    /// replaces the previous handler and binding.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Bind a dedicated <see cref="ILwm2mResourceHandler"/> implementation to an IPSO object and resource id.</para>
    /// <code language="csharp">
    /// public sealed class IlluminanceHandler : ILwm2mResourceHandler
    /// {
    ///     private double _lux;
    ///     private int _thresholdLux;
    ///     public PayloadValue? OnRead() => PayloadValue.FromFloat(_lux);
    ///     public void OnWrite(PayloadValue value) { _thresholdLux = (int)value.AsLong(); }
    /// }
    /// server.MapResource(3301, 5700, new IlluminanceHandler());
    /// </code>
    /// </example>
    /// <param name="objectId">IPSO/LwM2M object identifier (e.g. <c>3303</c> for Temperature, <c>3301</c> for Illuminance).</param>
    /// <param name="resourceId">Resource identifier within the object (e.g. <c>5700</c> for Sensor Value).</param>
    /// <param name="handler">Handler invoked on reads and writes to this resource.</param>
    public Lwm2mServer MapResource(
        int objectId, int resourceId, ILwm2mResourceHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var key = $"/{objectId}/+/{resourceId}";
        _resourceBindings[key] = new ResourceBinding(objectId, resourceId, handler);
        return this;
    }

    /// <summary>
    /// Maps an IPSO object/resource using simple delegates.
    /// </summary>
    /// <remarks>See <see cref="MapResource(int, int, ILwm2mResourceHandler)"/> for timing and replacement rules.</remarks>
    /// <example>
    /// <para>Same as the handler overload, but with inline read/write delegates (here: sensor value and a writable limit).</para>
    /// <code language="csharp">
    /// int lux = 400;
    /// int dimBelowLux = 50;
    /// server.MapResource(3301, 5700,
    ///     onRead: () => PayloadValue.FromInteger(lux),
    ///     onWrite: v => { dimBelowLux = (int)v.AsLong(); });
    /// </code>
    /// </example>
    /// <param name="objectId">IPSO/LwM2M object identifier (e.g. <c>3301</c> for Illuminance).</param>
    /// <param name="resourceId">Resource identifier within the object (e.g. <c>5700</c> for Sensor Value).</param>
    /// <param name="onRead">Delegate invoked on CoAP GET; return the current resource value, or <c>null</c> for 4.04.</param>
    /// <param name="onWrite">Optional delegate invoked on CoAP PUT/POST with the decoded payload. Pass <c>null</c> for read-only resources.</param>
    public Lwm2mServer MapResource(
        int objectId, int resourceId,
        Func<PayloadValue?> onRead,
        Action<PayloadValue>? onWrite = null)
    {
        ArgumentNullException.ThrowIfNull(onRead);
        return MapResource(objectId, resourceId,
            new DelegateResourceHandler(onRead, onWrite));
    }

    /// <summary>
    /// Runs the LwM2M server until <paramref name="ct"/> is cancelled. For standalone use; does not
    /// participate in the generic host's <c>StartAsync</c> pipeline unless this instance is
    /// registered as a <see cref="BackgroundService"/> (where <see cref="BackgroundService.ExecuteAsync"/>
    /// drives the same implementation).
    /// </summary>
    /// <remarks>
    /// Provides a direct awaitable entry point for non-host scenarios. When this instance is used
    /// as a hosted <see cref="BackgroundService"/>, the host invokes
    /// <see cref="BackgroundService.ExecuteAsync(CancellationToken)"/> instead.
    /// </remarks>
    /// <example>
    /// <para>From a console program, run until the user cancels; the CoAP server stops when the token is signalled.</para>
    /// <code language="csharp">
    /// using var cts = new CancellationTokenSource();
    /// Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
    /// var server = new Lwm2mServer(new Lwm2mServerOptions { Port = 5683 });
    /// // server.MapResource(...);
    /// await server.RunAsync(cts.Token);
    /// </code>
    /// </example>
    /// <param name="ct">Token that stops the server and background registration purger when signalled.</param>
    public async Task RunAsync(CancellationToken ct)
    {
        await ExecuteAsync(ct);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _coapServer = new CoapServer(
            new CoapServerOptions { Port = _options.Port, Name = _options.Name },
            _logger as ILogger<CoapServer> ?? NullLogger<CoapServer>.Instance);

        _coapServer.Map("/rd", new RegistrationHandler(
            _registrations, _logger,
            reg => ClientRegistered?.Invoke(reg),
            reg => ClientDeregistered?.Invoke(reg)));
        _coapServer.Map("/#", new Lwm2mObjectHandler(
            _resourceBindings, _logger));

        var purgerTask = RunPurgerAsync(stoppingToken);
        await _coapServer.RunAsync(stoppingToken);
        await purgerTask;
    }

    private async Task RunPurgerAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(1), ct); }
            catch (OperationCanceledException)
            {
                break;
            }

            var purged = _registrations.PurgeExpired();
            if (purged > 0)
                _logger.LogInformation("Purged {Count} expired LwM2M registration(s)", purged);
        }
    }

    private sealed class DelegateResourceHandler(
        Func<PayloadValue?> onRead, Action<PayloadValue>? onWrite)
        : ILwm2mResourceHandler
    {
        public PayloadValue? OnRead() => onRead();
        public void OnWrite(PayloadValue value) => onWrite?.Invoke(value);
    }
}

/// <summary>
/// Handles the /rd registration interface.
/// </summary>
internal sealed class RegistrationHandler(
    RegistrationDirectory directory,
    ILogger logger,
    Action<Lwm2mRegistration> onRegistered,
    Action<Lwm2mRegistration> onDeregistered) : ICoapRequestHandler
{
    public Task<CoapResponse> HandleAsync(CoapRequest request, CancellationToken ct)
    {
        var path = request.Path;

        if (path.Equals("/rd", StringComparison.OrdinalIgnoreCase) && request.Method == CoapMethod.Post)
        {
            var reg = RegistrationParser.Parse(
                request.Query,
                SysEncoding.UTF8.GetString(request.Payload.Span),
                request.RemoteEndpoint);

            var registered = directory.Register(reg);
            logger.LogInformation(
                "LwM2M client registered: endpoint={Endpoint}, location={Location}",
                registered.Endpoint, registered.Location);

            onRegistered(registered);

            var payload = SysEncoding.UTF8.GetBytes(registered.Location);
            return Task.FromResult(CoapResponse.Created(payload, CoapContentFormat.TextPlain));
        }

        if (path.StartsWith("/rd/", StringComparison.OrdinalIgnoreCase) && request.Method == CoapMethod.Post)
        {
            var queryParams = RegistrationParser.ParseQueryParameters(request.Query);
            int? newLifetime = null;
            if (queryParams.TryGetValue("lt", out var ltStr)
                && int.TryParse(ltStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lt))
                newLifetime = lt;

            if (directory.Update(path, newLifetime))
                return Task.FromResult(CoapResponse.Changed());
            return Task.FromResult(CoapResponse.NotFound());
        }

        if (path.StartsWith("/rd/", StringComparison.OrdinalIgnoreCase) && request.Method == CoapMethod.Delete)
        {
            var existing = directory.FindByLocation(path);
            if (directory.Deregister(path))
            {
                logger.LogInformation("LwM2M client deregistered: {Location}", path);
                if (existing is not null)
                    onDeregistered(existing);
                return Task.FromResult(CoapResponse.Deleted());
            }
            return Task.FromResult(CoapResponse.NotFound());
        }

        return Task.FromResult(CoapResponse.MethodNotAllowed());
    }
}

/// <summary>
/// Handles read/write operations on LwM2M object paths.
/// </summary>
internal sealed class Lwm2mObjectHandler(
    Dictionary<string, ResourceBinding> bindings,
    ILogger logger) : ICoapRequestHandler
{
    public Task<CoapResponse> HandleAsync(CoapRequest request, CancellationToken ct)
    {
        if (!Lwm2mPath.TryParse(request.Path, out var lwPath))
            return Task.FromResult(CoapResponse.NotFound());

        return request.Method switch
        {
            CoapMethod.Get => Task.FromResult(HandleRead(request, lwPath)),
            CoapMethod.Put or CoapMethod.Post => Task.FromResult(HandleWrite(request, lwPath)),
            _ => Task.FromResult(CoapResponse.MethodNotAllowed()),
        };
    }

    private CoapResponse HandleRead(CoapRequest request, Lwm2mPath lwPath)
    {
        if (!lwPath.IsResource)
            return CoapResponse.BadRequest("Read requires a full resource path");

        var binding = FindBinding(lwPath.ObjectId, lwPath.ResourceId!.Value);
        if (binding is null)
            return CoapResponse.NotFound();

        var value = binding.Handler.OnRead();
        if (value is null)
            return CoapResponse.NotFound();

        // Only the first Accept option is used; additional values are ignored (LwM2M/CoAP simplification).
        var acceptFormat = request.AcceptFormats.Count > 0
            ? request.AcceptFormats[0]
            : CoapContentFormat.TextPlain;

        // Unrecognized/unsupported content formats are served as text/plain.
        var (payload, format) = EncodeValue(lwPath, value, acceptFormat);
        return CoapResponse.Content(payload, format);
    }

    private CoapResponse HandleWrite(CoapRequest request, Lwm2mPath lwPath)
    {
        if (!lwPath.IsResource)
            return CoapResponse.BadRequest("Write requires a full resource path");

        var binding = FindBinding(lwPath.ObjectId, lwPath.ResourceId!.Value);
        if (binding is null)
            return CoapResponse.NotFound();

        var resourceDef = IpsoObjectRegistry.Find(lwPath.ObjectId)
            ?.Resources?.FirstOrDefault(r => r.ResourceId == lwPath.ResourceId!.Value);
        var expectedType = resourceDef?.Type ?? PayloadType.Float;

        var contentFormat = request.ContentFormat ?? CoapContentFormat.TextPlain;

        PayloadValue value;
        try
        {
            value = PayloadCodec.DecodeSingleResource(
                request.Payload.Span, contentFormat, expectedType);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decode Write payload for {Path}", lwPath);
            return CoapResponse.BadRequest("The request payload is invalid or could not be decoded.");
        }

        binding.Handler.OnWrite(value);
        logger.LogDebug("LwM2M Write: {Path} = {Value}", lwPath, value.AsString());
        return CoapResponse.Changed();
    }

    private ResourceBinding? FindBinding(int objectId, int resourceId)
    {
        var key = $"/{objectId}/+/{resourceId}";
        return bindings.GetValueOrDefault(key);
    }

    private static (byte[] Payload, CoapContentFormat Format) EncodeValue(
        Lwm2mPath path, PayloadValue value, CoapContentFormat preferredFormat)
    {
        if (preferredFormat == CoapContentFormat.ApplicationLwm2mTlv)
        {
            var resourceDef = IpsoObjectRegistry.Find(path.ObjectId)
                ?.Resources?.FirstOrDefault(r => r.ResourceId == path.ResourceId!.Value);
            var type = resourceDef?.Type ?? PayloadType.Float;
            var tlv = TlvEncoder.EncodeSingle(new TlvRecord(
                TlvRecordType.Resource, path.ResourceId!.Value, value, type));
            return (tlv, CoapContentFormat.ApplicationLwm2mTlv);
        }

        if (preferredFormat == CoapContentFormat.ApplicationSenmlJson)
        {
            var records = new List<SenmlRecord> { new(path.ResourceId!.Value, value) };
            var json = SenmlJsonCodec.Encode(
                path.ObjectId, path.InstanceId ?? 0, records);
            return (json, CoapContentFormat.ApplicationSenmlJson);
        }

        // Fallback for unknown Accept values (e.g. not TLV or SenML): encode as text/plain.
        var text = SysEncoding.UTF8.GetBytes(value.AsString());
        return (text, CoapContentFormat.TextPlain);
    }
}

/// <summary>
/// Associates an LwM2M object and resource id with a <see cref="ILwm2mResourceHandler"/> implementation.
/// </summary>
internal sealed record ResourceBinding(
    int ObjectId, int ResourceId, ILwm2mResourceHandler Handler);

/// <summary>
/// Configuration for a <see cref="Lwm2mServer"/> instance.
/// </summary>
public sealed class Lwm2mServerOptions
{
    /// <summary>UDP port. Default: 5683.</summary>
    public int Port { get; set; } = 5683;

    /// <summary>Optional name for logging.</summary>
    public string? Name { get; set; }
}