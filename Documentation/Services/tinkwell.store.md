# Store Service

Service definition for the Tinkwell Measures Registry. This service provides methods for registering, updating, querying, and subscribing to measures.

## Methods

### Register(StoreRegisterRequest) returns (google.protobuf.Empty)
Registers a new measure.

### RegisterMany(StoreRegisterManyRequest) returns (google.protobuf.Empty)
Registers multiple measures in a single call.

### Update(StoreUpdateRequest) returns (google.protobuf.Empty)
Updates the value of an existing measure.

### UpdateMany(StoreUpdateManyRequest) returns (google.protobuf.Empty)
Updates the values of multiple existing measures in a single call.

### SetMeasureValue(SetMeasureValueRequest) returns (google.protobuf.Empty)
Sets the value of an existing measure from a string, handling type conversion automatically.

### Find(StoreFindRequest) returns (StoreMeasure)
Finds a single measure by its unique name.

### FindAll(StoreFindAllRequest) returns (stream StoreMeasure)
Finds all measures, optionally filtered by a list of names. If the names list is empty, all measures are returned.

### FindAllDefinitions(StoreFindAllDefinitionsRequest) returns (stream StoreDefinition)
Finds all measure definitions, optionally filtered by a list of names. If the names list is empty, all definitions are returned.

### Search(SearchRequest) returns (stream SearchResponse)
Searches for measures matching the specified criteria.

### ReadMany(StoreReadManyRequest) returns (StoreValueList)
Read the current value of a set of measures. It's lighter thand FindAll() but this function does NOT return a stream, use only when you need the current value of a very small number of measures.

### Subscribe(SubscribeRequest) returns (stream StoreValueChange)
Subscribes to value changes for a single measure.

### SubscribeMany(SubscribeManyRequest) returns (stream StoreValueChange)
Subscribes to value changes for a specific list of measures.

### SubscribeManyMatching(SubscribeManyMatchingRequest) returns (stream StoreValueChange)
Subscribes to value changes for all measures matching a name pattern.

## Messages

### StoreMeasure
Represents the full state of a measure, including its definition, metadata, and current value.

| Field | Type | Description |
|---|---|---|
| `definition` | `StoreDefinition` | The definition of the measure. |
| `metadata` | `StoreMetadata` | The metadata associated with the measure. |
| `value` | `StoreValue` | The current value of the measure. |

### StoreValueList
Represents a set of measures with their value.

| Field | Type | Description |
|---|---|---|
| `items` | `repeated Item` | |

### Item (nested in StoreValueList)

| Field | Type | Description |
|---|---|---|
| `name` | `string` | The definition of the measure. |
| `value` | `StoreValue` | The current value of the measure. |

### StoreMeasureInfo
Represents a measure without its value, for when only definition and metadata are needed.

| Field | Type | Description |
|---|---|---|
| `definition` | `StoreDefinition` | The definition of the measure. |
| `metadata` | `StoreMetadata` | The metadata associated with the measure. |

### StoreDefinition
Represents the definition of a measure, corresponding to the C# MeasureDefinition class.

| Field | Type | Description |
|---|---|---|
| `attributes` | `int32` | Attributes of the measure. This is a bitmask. For example, a value of 3 (binary 11) means Constant and Derived. None = 0: normal measure Constant = 1: constant value (cannot be changed after the first write) Derived = 2: derived measure (informative) SystemGenerated = 4: generated automatically by the system |
| `type` | `Type` | The type of the measure's value. |
| `name` | `string` | The unique name of the measure. |
| `quantity_type` | `optional string` | The type of the quantity (e.g., "Length", "Mass"). |
| `unit` | `optional string` | The unit of the measure (e.g., "Meter", "Kilogram"). |
| `ttl` | `optional google.protobuf.Duration` | The time-to-live for the measure's value. After this duration, the value is considered expired. |
| `minimum` | `optional double` | The minimum allowed value for a numeric measure. |
| `maximum` | `optional double` | The maximum allowed value for a numeric measure. |
| `precision` | `optional int32` | The number of decimal places to round a numeric value to. |

### StoreMetadata
Represents the metadata for a measure, returned by the service.

| Field | Type | Description |
|---|---|---|
| `created_at` | `google.protobuf.Timestamp` | The timestamp when the measure was created. |
| `tags` | `repeated string` | A list of tags associated with the measure for categorization and searching. |
| `category` | `optional string` | The category of the measure. |
| `description` | `optional string` | The description of the measure. |

### StoreMetadataInput
Represents the metadata for a measure provided by the client on creation. This message omits fields that are managed by the service, like `created_at`.

| Field | Type | Description |
|---|---|---|
| `tags` | `repeated string` | A list of tags to associate with the measure. |
| `category` | `optional string` | The category to associate with the measure. |
| `description` | `optional string` | The description of the measure. |

### StoreValue
Represents the value of a measure, corresponding to the C# MeasureValue struct.

| Field | Type | Description |
|---|---|---|
| `timestamp` | `google.protobuf.Timestamp` | The timestamp when the value was recorded. |
| `number_value` | `double` | The actual value, which can be either a number or a string. |
| `string_value` | `string` | The actual value, which can be either a number or a string. |

### StoreValueChange
Represents a notification for a measure value change.

| Field | Type | Description |
|---|---|---|
| `name` | `string` | The name of the measure that changed. |
| `new_value` | `StoreValue` | The new value of the measure. |
| `old_value` | `optional StoreValue` | The old value of the measure, if available. |

### StoreRegisterRequest
Request to register a new measure.

| Field | Type | Description |
|---|---|---|
| `definition` | `StoreDefinition` | The definition of the measure to register. |
| `metadata` | `StoreMetadataInput` | The initial metadata for the measure. |

### StoreRegisterManyRequest
Request to register multiple measures.

| Field | Type | Description |
|---|---|---|
| `items` | `repeated StoreRegisterRequest` | A list of measures to register. |

### StoreUpdateRequest
Request to update the value of a measure.

| Field | Type | Description |
|---|---|---|
| `name` | `string` | The name of the measure to update. |
| `value` | `StoreValue` | The new value for the measure. |

### StoreUpdateManyRequest
Request to update the values of multiple measures.

| Field | Type | Description |
|---|---|---|
| `items` | `repeated StoreUpdateRequest` | A list of measure updates. |

### SetMeasureValueRequest
Request to set the value of a measure from a string.

| Field | Type | Description |
|---|---|---|
| `name` | `string` | The name of the measure to update. |
| `value_string` | `string` | The string representation of the value. The service will parse this based on the measure's type. |

### StoreFindRequest
Request to find a single measure.

| Field | Type | Description |
|---|---|---|
| `name` | `string` | The name of the measure to find. |

### StoreFindAllRequest
Request to find all measures, with an optional filter by name.

| Field | Type | Description |
|---|---|---|
| `names` | `repeated string` | If empty, find all measures. Otherwise, find only the measures with the specified names. |

### StoreReadManyRequest
Request to read the value of a set of measures.

| Field | Type | Description |
|---|---|---|
| `names` | `repeated string` | List of names of the measures to include in the response. |

### StoreFindAllDefinitionsRequest
Request to find all measure definitions, with an optional filter by name.

| Field | Type | Description |
|---|---|---|
| `names` | `repeated string` | If empty, find all definitions. Otherwise, find only the definitions with the specified names. |

### SearchRequest
Request to search for measures.

| Field | Type | Description |
|---|---|---|
| `query` | `optional string` | A query string to match against the measure name. Search is case-insensitive. Wildcards such as * and ? are supported. For example "JournalBearing.Temperature.*" matches all the measures that start with "JournalBearing.Temperature." and "JournalBearing.*.*Oil*" matches all the measures that start with "JournalBearing." and contain "Oil" in the third part of the name." ? works as a single character wildcard, for example "JournalBearing.Temperature.Oil?" matches all the measures that starts with the specified string and ends with any other single character. If you want to search for all the measure that includes a specific word then use "*word*". It suports groups as in "[Bb]lue", it matches both "Blue" and "blue". You can negate groups as as in "[^Bb]lue", it matches "Glue" but not "blue" or "Blue". You can also specify ranges "[a-zA-Z]". |
| `tags` | `repeated string` | A list of tags to match against the measure's tags. |
| `category` | `optional string` | A category to match against the measure's category. |
| `include_values` | `bool` | If true, the response will include the full StoreMeasure with values. If false, the response will include only StoreMeasureInfo (definition and metadata). |

### SearchResponse
Response from a search request.

| Field | Type | Description |
|---|---|---|
| `measure` | `StoreMeasure` | The result of the search, which can be either a full measure or just its info. |
| `info` | `StoreMeasureInfo` | The result of the search, which can be either a full measure or just its info. |

### SubscribeRequest
Request to subscribe to a single measure.

| Field | Type | Description |
|---|---|---|
| `name` | `string` | The name of the measure to subscribe to. |

### SubscribeManyRequest
Request to subscribe to multiple measures.

| Field | Type | Description |
|---|---|---|
| `names` | `repeated string` | The names of the measures to subscribe to. |

### SubscribeManyMatchingRequest
Request to subscribe to measures matching a pattern.

| Field | Type | Description |
|---|---|---|
| `pattern` | `string` | A pattern to match against measure names. Search is case-insensitive. Wildcards such as * and ? are supported. For example "JournalBearing.Temperature.*" matches all the measures that start with "JournalBearing.Temperature." and "JournalBearing.*.*Oil*" matches all the measures that start with "JournalBearing." and contain "Oil" in the third part of the name." ? works as a single character wildcard, for example "JournalBearing.Temperature.Oil?" matches all the measures that starts with the specified string and ends with any other single character. If you want to search for all the measure that includes a specific word then use "*word*". It suports groups as in "[Bb]lue", it matches both "Blue" and "blue". You can negate groups as as in "[^Bb]lue", it matches "Glue" but not "blue" or "Blue". You can also specify ranges "[a-zA-Z]". |

## Enums

### Type (nested in StoreDefinition)

| Name | Value | Description |
|---|---|---|
| `UNSPECIFIED` | 0 | The type is not specified. |
| `DYNAMIC` | 1 | The measure can hold any type of value. |
| `NUMBER` | 2 | The measure holds a numeric value. |
| `STRING` | 3 | The measure holds a string value. |
