namespace Tinkwell.Cli;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CommandForAttribute : Attribute
{
    public CommandForAttribute(string name, string? alias, Type? parent)
    {
        Parent = parent;
        Alias = alias;
        Name = name;
    }

    public CommandForAttribute(string name) : this(name, null, null)
    {
    }

    public CommandForAttribute(string name, string? alias) : this(name, alias, null)
    {
    }

    public CommandForAttribute(string name, Type? parent) : this(name, null, parent)
    {
    }

    public Type? Parent { get; }

    public string? Alias { get; }
   
    public string Name { get; }
}
