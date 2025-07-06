namespace Tinkwell.Cli;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CommandForAttribute : Attribute
{
    public CommandForAttribute(string name) : this(name, null)
    {
    }

    public CommandForAttribute(string name, Type? parent)
    {
        Parent = parent;
        Name = name;
    }

    public Type? Parent { get; }

    public string Name { get; }
}
