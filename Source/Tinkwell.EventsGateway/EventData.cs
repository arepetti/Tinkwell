namespace Tinkwell.EventsGateway;

sealed class EventData
{
    public required string Id
    {
        get => _id;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Id));
            _id = value;
        }
    }

    public required string Topic
    {
        get => _topic;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Topic));
            _topic = value;
        }
    }

    public required string Subject
    {
        get => _subject;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Subject));
            _subject = value;
        }
    }

    public required string Verb
    {
        get => _verb;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Verb));
            _verb = value;
        }
    }

    public required string Object
    {
        get => _object;
        init
        {
            _object = value ?? throw new ArgumentNullException(nameof(Object));
        }
    }

    public string? Payload { get; init; }

    public required string CorrelationId
    {
        get => _correlationId;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(CorrelationId));
            _correlationId = value;
        }
    }

    public required DateTime OccurredAt { get; init; }

    private readonly string _id = "";
    private readonly string _topic = "";
    private readonly string _subject = "";
    private readonly string _verb = "";
    private readonly string _object = "";
    private readonly string _correlationId = "";
}