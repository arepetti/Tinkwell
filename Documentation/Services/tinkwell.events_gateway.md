# EventsGateway Service

The EventsGateway service allows publishing events to a message broker.

## Methods

### Publish(PublishEventsRequest) returns (PublishEventsResponse)
Publish an event to the message broker. Returns a correlation_id that can be used to track the event (if a correlation ID has been provided then it's the same one).

### SubscribeTo(SubscribeToEventsRequest) returns (stream SubscribeEventsResponse)
Subscribe to the event specified in the request.

### SubscribeToMatching(SubscribeToMatchingEventsRequest) returns (stream SubscribeEventsResponse)
Subscribe to all the events matching the criteria specified in the request.

### SubscribeToMatchingMany(SubscribeToMatchingManyEventsRequest) returns (stream SubscribeEventsResponse)
Subscribe to all the events matching a set of criterias specified in the request. Callers can assign an ID to each match to determine which match caused the notification.

## Enums

### Verb
Verbs for SVO events, describing what has happened.

| Name | Value | Description |
|---|---|---|
| `CREATED` | 0 | A new resource or entity was created |
| `READ` | 1 | A resource or entity was retrieved/read |
| `UPDATED` | 2 | A resource or entity was updated/modified |
| `DELETED` | 3 | A resource or entity was deleted/removed |
| `CALCULATED` | 4 | A derived measure or calculation was performed |
| `STARTED` | 5 | A process, job or task has started |
| `COMPLETED` | 6 | A process, job or task has finished successfully |
| `STOPPED` | 7 | A process, job or task was stopped or interrupted |
| `PAUSED` | 8 | A process, job or task was paused |
| `RESUMED` | 9 | A previously paused process, job or task has resumed |
| `DETECTED` | 10 | An anomaly or condition was automatically detected |
| `TRIGGERED` | 11 | An alarm or rule evaluation has fired/triggered |
| `CLEARED` | 12 | An alarm has been cleared or is no longer active |
| `RESOLVED` | 13 | An incident, alert or issue has been resolved |
| `FAILED` | 14 | An action, operation or task has failed |
| `SUCCEEDED` | 15 | An action, operation or task has succeeded |
| `PROCESSED` | 16 | A message, event or item has been processed |
| `RECEIVED` | 17 | An event or message was received by a consumer |
| `PUBLISHED` | 18 | An event or message was published/emitted into the bus |
| `ACKNOWLEDGED` | 19 | An event, message or notification was acknowledged |
| `EXPIRED` | 20 | A timed or scheduled item has expired |
| `RETRIED` | 21 | A failed operation or message delivery was retried |
| `ACTED` | 22 | Generic action made by Subject on Object (use only if there is nothing more specific!) |

## Messages

### PublishEventsRequest
The request message for publishing an event.

| Field | Type | Description |
|---|---|---|
| `topic` | `string` | A short, unique identifier for this event type. E.g. "AlarmTriggered", "UserSignedUp" |
| `subject` | `string` | The primary entity this event is about. For an alarm: the measure being monitored, e.g. "avg_cpu" For a user action: the user ID, e.g. "user-1234" |
| `verb` | `Verb` | The action that occurred. E.g. TRIGGERED, CREATED, DELETED, CALCULATED |
| `object` | `string` | The secondary resource or rule this event acted on. For an alarm: the rule ID, e.g. "cpu_high_rule" |
| `payload` | `optional string` | Optional free-form payload (often JSON) carrying event details. E.g. "{\"threshold\":80, \"value\":93.5}" "{\"userEmail\":\"foo@example.com\",\"plan\":\"pro\"}" |
| `correlation_id` | `optional string` | Optional correlation ID to tie together a chain of events. If you omit this, the service will generate one (e.g. a UUID). |
| `occurred_at` | `google.protobuf.Timestamp` | The timestamp when the event occurred. |

### PublishEventsResponse
The response message for publishing an event.

| Field | Type | Description |
|---|---|---|
| `id` | `string` | Unique ID for the published event. |
| `correlation_id` | `string` | The correlation ID of the published event. This is the same as the one provided in the request if one has been provided or a newly generated one otherwise. |

### SubscribeToEventsRequest
Request for subscribing to an event.

| Field | Type | Description |
|---|---|---|
| `topic` | `string` | Topic of the event for which you want to receive notifications. |

### SubscribeToMatchingEventsRequest
Request for subscribing to all the events that match the filter.

| Field | Type | Description |
|---|---|---|
| `topic` | `optional string` | If specified then you're going to receive only events matching this topic. The match is case sensitive. Wildcards such as * and ? are supported. For example "Alarm*" matches all the events that start with "Alarm". ? works as a single character wildcard. It suports groups as in "[Bb]lue", it matches both "Blue" and "blue". You can negate groups as as in "[^Bb]lue", it matches "Glue" but not "blue" or "Blue". You can also specify ranges "[a-zA-Z]". |
| `subject` | `optional string` | If specified the you're going to receive only events with this subject. The match is case sensitive. Same wildcards described for topic can be used here. |
| `verb` | `optional string` | If specified the you're going to receive only events with this verb. The match is case sensitive. Same wildcards described for topic can be used here. |
| `object` | `optional string` | If specified the you're going to receive only events with this object. The match is case sensitive. Same wildcards described for topic can be used here. |

### SubscribeToMatchingManyEventsRequest
Request of subscribing to a set of matching events.

| Field | Type | Description |
|---|---|---|
| `matches` | `repeated Match` | A list of matches to subscribe to. |

### Match (nested in SubscribeToMatchingManyEventsRequest)

| Field | Type | Description |
|---|---|---|
| `topic` | `optional string` | If specified then you're going to receive only events matching this topic. The match is case sensitive. Wildcards such as * and ? are supported. For example "Alarm*" matches all the events that start with "Alarm". ? works as a single character wildcard. It suports groups as in "[Bb]lue", it matches both "Blue" and "blue". You can negate groups as as in "[^Bb]lue", it matches "Glue" but not "blue" or "Blue". You can also specify ranges "[a-zA-Z]". |
| `subject` | `optional string` | If specified the you're going to receive only events with this subject. The match is case sensitive. Same wildcards described for topic can be used here. |
| `verb` | `optional string` | If specified the you're going to receive only events with this verb. The match is case sensitive. Same wildcards described for topic can be used here. |
| `object` | `optional string` | If specified the you're going to receive only events with this object. The match is case sensitive. Same wildcards described for topic can be used here. |
| `match_id` | `string` | An unique ID assigned to this match |

### SubscribeEventsResponse

| Field | Type | Description |
|---|---|---|
| `id` | `string` | An unique UUID for this event. |
| `topic` | `string` | A short, unique identifier for this event type. |
| `subject` | `string` | The primary entity this event is about. |
| `verb` | `Verb` | The action that occurred. |
| `object` | `string` | The secondary resource or rule this event acted on. |
| `payload` | `optional string` | Optional free-form payload (often JSON) carrying event details. |
| `correlation_id` | `string` | Correlation ID to tie together a chain of events. |
| `occurred_at` | `google.protobuf.Timestamp` | The timestamp when the event occurred. |
| `match_id` | `optional string` | The match ID that caused this notification (if specified when creating the subscription). |
