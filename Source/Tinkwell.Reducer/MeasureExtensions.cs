using Tinkwell.Services;

namespace Tinkwell.Reducer;

static class MeasureExtensions
{
    public static StoreRegisterRequest ToStoreRegisterRequest(this Measure measure, bool useConstants)
    {
        // A "declaration" is when we do not have an exxpression for the measure, technically
        // runners should register their own measures but when integrating with external services
        // (for example MQTT) is cleaner to have a place to "declare" those measures and let the
        // others simply consume them.
        bool isDeclaration = string.IsNullOrWhiteSpace(measure.Expression);
        bool isConstant = !isDeclaration && measure.Dependencies.Count == 0 && useConstants;
        bool isDerived = !isDeclaration && measure.Dependencies.Count > 0;

        var request = new StoreRegisterRequest
        {
            Definition = new()
            {
                Name = measure.Name,
                Type = StoreDefinition.Types.Type.Number,
                Attributes = (isDerived ? 2 : 0) | (isConstant ? 1 : 0),
                QuantityType = measure.QuantityType,
                Unit = measure.Unit,
            },
            Metadata = new()
            {
                Tags = { measure.Tags },
            }
        };

        if (measure.Minimum.HasValue)
            request.Definition.Minimum = measure.Minimum.Value;

        if (measure.Maximum.HasValue)
            request.Definition.Maximum = measure.Maximum.Value;

        if (measure.Precision.HasValue)
            request.Definition.Precision = measure.Precision.Value;

        if (measure.Description is not null)
            request.Metadata.Description = measure.Description;

        if (measure.Category is not null)
            request.Metadata.Category = measure.Category;

        return request;
    }
}