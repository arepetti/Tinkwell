namespace {{namespace}};

sealed class Options
{
    // TODO: this is the default value for ExampleProperty
    public static readonly string DefaultExampleProperty = "example";

    // TODO: this is an example of a custom property (specified in the "properties"
    // section inside the Ensamble Configuration File).
    public string ExampleProperty { get; init; } = DefaultExampleProperty;
}
