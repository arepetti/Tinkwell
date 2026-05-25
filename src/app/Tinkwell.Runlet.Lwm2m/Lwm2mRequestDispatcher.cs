using System.Net;
using Microsoft.Extensions.Logging;
using Tinkwell.Runlet.Lwm2m.Configuration;
using Tinkwell.Encoding;
using Tinkwell.Coap;
using Tinkwell.Integration;
using Tinkwell.Lwm2m;
using CoapContentFormat = Tinkwell.Coap.CoapContentFormat;
using Tinkwell.Lwm2m.Registration;
using SysEncoding = System.Text.Encoding;

namespace Tinkwell.Runlet.Lwm2m;

/// <summary>
/// Routes incoming CoAP requests to the appropriate LwM2M handler based on
/// the URI path. Implements the server-side interfaces defined in:
/// - Registration: OMA-TS-LightweightM2M_Transport-V1_1, Section 5.3
/// - Device Management: OMA-TS-LightweightM2M_Core-V1_1, Section 5.4
/// </summary>
internal sealed class Lwm2mRequestDispatcher
{
    private readonly Lwm2mServerDefinition _server;
    private readonly RegistrationDirectory _registrationDir;
    private readonly ResourceStore _resourceStore;
    private readonly IReadOnlyDictionary<(int ObjectId, int ResourceId), Lwm2mResourceRegistration> _codeResources;
    private readonly ILogger _logger;

    public Lwm2mRequestDispatcher(
        Lwm2mServerDefinition server,
        RegistrationDirectory registrationDir,
        ResourceStore resourceStore,
        IEnumerable<Lwm2mResourceRegistration> codeResources,
        ILogger logger)
    {
        _server = server;
        _registrationDir = registrationDir;
        _resourceStore = resourceStore;
        _codeResources = BuildCodeResourceMap(codeResources, logger);
        _logger = logger;
    }

    private static Dictionary<(int ObjectId, int ResourceId), Lwm2mResourceRegistration>
        BuildCodeResourceMap(IEnumerable<Lwm2mResourceRegistration> registrations, ILogger logger)
    {
        var map = new Dictionary<(int, int), Lwm2mResourceRegistration>();
        foreach (var reg in registrations)
        {
            var key = (reg.ObjectId, reg.ResourceId);
            if (!map.TryAdd(key, reg))
            {
                logger.LogWarning(
                    "Duplicate code-driven LwM2M resource registration for /{ObjectId}/x/{ResourceId} — keeping first",
                    reg.ObjectId, reg.ResourceId);
            }
        }
        return map;
    }

    /// <summary>
    /// Dispatches a CoAP request to the correct handler.
    /// Returns (responseCode, payload, contentFormat).
    /// </summary>
    public (byte Code, byte[]? Payload, CoapContentFormat? ContentFormat) HandleRequest(
        CoapMessage request, IPEndPoint remoteEndpoint)
    {
        var path = request.UriPath;

        // OMA-TS-LightweightM2M_Transport-V1_1, Section 5.3: Registration interface at /rd
        if (path.Equals("/rd", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/rd/", StringComparison.OrdinalIgnoreCase))
        {
            return HandleRegistration(request, remoteEndpoint);
        }

        // Device management operations on LwM2M object paths (/{objectId}/...)
        if (Lwm2mPath.TryParse(path, out var lwPath))
        {
            return HandleObjectRequest(request, lwPath, remoteEndpoint);
        }

        return (CoapCode.NotFound, null, null);
    }

    /// <summary>
    /// Registration interface (OMA-TS-LightweightM2M_Transport-V1_1, Section 5.3).
    /// POST /rd = Register, POST /rd/{location} = Update, DELETE /rd/{location} = Deregister.
    /// </summary>
    private (byte Code, byte[]? Payload, CoapContentFormat? ContentFormat) HandleRegistration(
        CoapMessage request, IPEndPoint remoteEndpoint)
    {
        var path = request.UriPath;

        // POST /rd — new registration (Section 5.3.1)
        if (path.Equals("/rd", StringComparison.OrdinalIgnoreCase) && request.Code == CoapCode.Post)
        {
            var reg = RegistrationParser.Parse(
                request.UriQuery, request.PayloadString, remoteEndpoint);

            var registered = _registrationDir.Register(reg);

            _logger.LogInformation(
                "LwM2M client registered: endpoint={Endpoint}, location={Location}, lifetime={Lifetime}s, objects=[{Objects}]",
                registered.Endpoint, registered.Location, registered.Lifetime,
                string.Join(", ", registered.Objects));

            OtMetrics.Registrations.Add(1,
                new("lwm2m.endpoint", registered.Endpoint),
                new("lwm2m.operation", "register"));
            OtMetrics.ActiveClients.Add(1);

            var locationPayload = SysEncoding.UTF8.GetBytes(registered.Location);
            return (CoapCode.Created, locationPayload, CoapContentFormat.TextPlain);
        }

        // POST /rd/{location} — registration update (Section 5.3.2)
        if (path.StartsWith("/rd/", StringComparison.OrdinalIgnoreCase) && request.Code == CoapCode.Post)
        {
            var queryParams = RegistrationParser.ParseQueryParameters(request.UriQuery);
            int? newLifetime = null;
            if (queryParams.TryGetValue("lt", out var ltStr) && int.TryParse(ltStr, out var lt))
                newLifetime = lt;

            if (_registrationDir.Update(path, newLifetime))
            {
                OtMetrics.Registrations.Add(1);
                _logger.LogDebug("LwM2M registration updated: {Location}", path);
                return (CoapCode.Changed, null, null);
            }

            return (CoapCode.NotFound, null, null);
        }

        // DELETE /rd/{location} — deregistration (Section 5.3.4)
        if (path.StartsWith("/rd/", StringComparison.OrdinalIgnoreCase) && request.Code == CoapCode.Delete)
        {
            if (_registrationDir.Deregister(path))
            {
                OtMetrics.ActiveClients.Add(-1);
                _logger.LogInformation("LwM2M client deregistered: {Location}", path);
                return (CoapCode.Deleted, null, null);
            }

            return (CoapCode.NotFound, null, null);
        }

        return (CoapCode.MethodNotAllowed, null, null);
    }

    /// <summary>
    /// Handles Read and Write operations on LwM2M objects
    /// (OMA-TS-LightweightM2M_Core-V1_1, Section 5.4).
    /// </summary>
    private (byte Code, byte[]? Payload, CoapContentFormat? ContentFormat) HandleObjectRequest(
        CoapMessage request, Lwm2mPath lwPath, IPEndPoint remoteEndpoint)
    {
        return request.Code switch
        {
            // Read operation (Section 5.4.1)
            CoapCode.Get => HandleRead(request, lwPath),
            // Write operation (Section 5.4.3)
            CoapCode.Put or CoapCode.Post => HandleWrite(request, lwPath, remoteEndpoint),
            _ => (CoapCode.MethodNotAllowed, null, null),
        };
    }

    /// <summary>
    /// Read operation (OMA-TS-LightweightM2M_Core-V1_1, Section 5.4.1).
    /// Returns the current value of a resource from the resource store.
    /// Supports text/plain, TLV, and SenML-JSON response formats based
    /// on the Accept option.
    /// </summary>
    private (byte Code, byte[]? Payload, CoapContentFormat? ContentFormat) HandleRead(
        CoapMessage request, Lwm2mPath lwPath)
    {
        if (!lwPath.IsResource)
        {
            return HandleInstanceRead(request, lwPath);
        }

        if (_codeResources.TryGetValue((lwPath.ObjectId, lwPath.ResourceId!.Value), out var codeReg))
        {
            var val = codeReg.OnRead();
            if (val is null)
                return (CoapCode.NotFound, null, null);

            var acceptFormat = GetPreferredFormat(request);
            var pv = PayloadValue.FromString(val);
            var (payload, format) = EncodeResourceValue(lwPath, pv, null, acceptFormat);
            return (CoapCode.Content, payload, format);
        }

        var entry = _resourceStore.Get(lwPath.ToString());
        if (entry is null)
            return (CoapCode.NotFound, null, null);

        var mapping = FindMapping(lwPath.ObjectId, lwPath.ResourceId!.Value);
        var preferredFormat = GetPreferredFormat(request);

        var (payloadBytes, contentFormat) = EncodeResourceValue(
            lwPath, entry.Value, mapping, preferredFormat);

        return (CoapCode.Content, payloadBytes, contentFormat);
    }

    /// <summary>
    /// Instance-level Read: returns all resources for the instance in TLV
    /// or SenML-JSON format (OMA-TS-LightweightM2M_Core-V1_1, Section 5.4.1).
    /// </summary>
    private (byte Code, byte[]? Payload, CoapContentFormat? ContentFormat) HandleInstanceRead(
        CoapMessage request, Lwm2mPath lwPath)
    {
        var prefix = lwPath.ToString();
        var entries = _resourceStore.GetByPrefix(prefix + "/");

        if (entries.Count == 0)
            return (CoapCode.NotFound, null, null);

        var acceptFormat = GetPreferredFormat(request);

        if (acceptFormat == CoapContentFormat.ApplicationSenmlJson)
        {
            var records = new List<SenmlRecord>();
            foreach (var (path, entry) in entries)
            {
                if (Lwm2mPath.TryParse(path, out var rp) && rp.ResourceId.HasValue)
                    records.Add(new SenmlRecord(rp.ResourceId.Value, entry.Value));
            }

            var json = SenmlJsonCodec.Encode(
                lwPath.ObjectId, lwPath.InstanceId ?? 0, records);
            return (CoapCode.Content, json, CoapContentFormat.ApplicationSenmlJson);
        }

        // Default: TLV encoding for instance-level reads
        var tlvRecords = new List<TlvRecord>();
        foreach (var (path, entry) in entries)
        {
            if (!Lwm2mPath.TryParse(path, out var rp) || !rp.ResourceId.HasValue)
                continue;

            var mapping = FindMapping(lwPath.ObjectId, rp.ResourceId.Value);
            var resourceDef = IpsoObjectRegistry.Find(lwPath.ObjectId)
                ?.Resources?.FirstOrDefault(r => r.ResourceId == rp.ResourceId.Value);
            var valueType = resourceDef?.Type ?? PayloadType.Float;

            tlvRecords.Add(new TlvRecord(
                TlvRecordType.Resource, rp.ResourceId.Value, entry.Value, valueType));
        }

        var tlv = TlvEncoder.Encode(tlvRecords);
        return (CoapCode.Content, tlv, CoapContentFormat.ApplicationLwm2mTlv);
    }

    /// <summary>
    /// Write operation (OMA-TS-LightweightM2M_Core-V1_1, Section 5.4.3).
    /// Decodes the incoming payload and updates the resource store.
    /// </summary>
    private (byte Code, byte[]? Payload, CoapContentFormat? ContentFormat) HandleWrite(
        CoapMessage request, Lwm2mPath lwPath, IPEndPoint remoteEndpoint)
    {
        if (!lwPath.IsResource)
            return (CoapCode.BadRequest,
                SysEncoding.UTF8.GetBytes("Write requires a full resource path"),
                CoapContentFormat.TextPlain);

        if (_codeResources.TryGetValue((lwPath.ObjectId, lwPath.ResourceId!.Value), out var codeReg))
        {
            if (codeReg.OnWrite is null)
                return (CoapCode.MethodNotAllowed, null, null);

            var text = request.PayloadString;
            codeReg.OnWrite(text);

            _logger.LogDebug("LwM2M code-driven Write: {Path} = {Value} (from {Endpoint})",
                lwPath, text, remoteEndpoint);
            return (CoapCode.Changed, null, null);
        }

        var mapping = FindMapping(lwPath.ObjectId, lwPath.ResourceId!.Value);
        if (mapping is null)
            return (CoapCode.NotFound, null, null);

        var resourceDef = IpsoObjectRegistry.Find(lwPath.ObjectId)
            ?.Resources?.FirstOrDefault(r => r.ResourceId == lwPath.ResourceId!.Value);
        var expectedType = resourceDef?.Type ?? PayloadType.Float;

        // Determine content-format: use request header, fall back to text/plain
        var contentFormat = request.RequestContentFormat ?? CoapContentFormat.TextPlain;

        PayloadValue value;
        try
        {
            value = PayloadCodec.DecodeSingleResource(
                request.Payload, contentFormat, expectedType);
        }
        catch (OutOfMemoryException) { Environment.FailFast("Out of memory"); throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode Write payload for {Path}", lwPath);
            return (CoapCode.BadRequest,
                SysEncoding.UTF8.GetBytes($"Payload decode error: {ex.Message}"),
                CoapContentFormat.TextPlain);
        }

        _resourceStore.Set(lwPath.ToString(), value);
        OtMetrics.Writes.Add(1,
            new("lwm2m.object", lwPath.ObjectId),
            new("lwm2m.resource", lwPath.ResourceId!.Value));

        _logger.LogDebug(
            "LwM2M Write: {Path} = {Value} (from {Endpoint})",
            lwPath, value.AsString(), remoteEndpoint);

        return (CoapCode.Changed, null, null);
    }

    private Lwm2mObjectMapping? FindMapping(int objectId, int resourceId) =>
        _server.Objects.FirstOrDefault(m =>
            m.ObjectId == objectId && m.ResourceId == resourceId);

    private static CoapContentFormat GetPreferredFormat(CoapMessage request)
    {
        var accepts = request.AcceptFormats;
        if (accepts.Count > 0)
            return accepts[0];
        return CoapContentFormat.TextPlain;
    }

    private static (byte[] Payload, CoapContentFormat Format) EncodeResourceValue(
        Lwm2mPath path, PayloadValue value,
        Lwm2mObjectMapping? mapping, CoapContentFormat preferredFormat)
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
            var records = new List<SenmlRecord>
            {
                new(path.ResourceId!.Value, value),
            };
            var json = SenmlJsonCodec.Encode(
                path.ObjectId, path.InstanceId ?? 0, records);
            return (json, CoapContentFormat.ApplicationSenmlJson);
        }

        // Default: text/plain
        var text = SysEncoding.UTF8.GetBytes(value.AsString());
        return (text, CoapContentFormat.TextPlain);
    }
}