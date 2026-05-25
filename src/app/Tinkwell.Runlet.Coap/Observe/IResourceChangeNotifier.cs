namespace Tinkwell.Runlet.Coap.Observe;

/// <summary>
/// Allows bindings to signal that a resource has changed, triggering
/// Observe notifications to subscribed clients (RFC 7641, Section 4.2).
/// </summary>
internal interface IResourceChangeNotifier
{
    void NotifyChanged(string path);
}
