namespace Tinkwell.Cli.Commands.Init;

/// <summary>
/// A loaded wizard pack: metadata, questions, outputs, and the directory
/// containing template files.
/// </summary>
internal sealed record WizardPack(
    string Name,
    string Title,
    string? Description,
    string PrimaryOutput,
    string PackDirectory,
    QuestionFlow Questions,
    IReadOnlyList<OutputSpec> Outputs);

/// <summary>The ordered question flow for a pack.</summary>
internal sealed record QuestionFlow(IReadOnlyList<QuestionNode> Nodes);

/// <summary>Base type for question flow nodes.</summary>
internal abstract record QuestionNode;

/// <summary>A single question asked to the user.</summary>
internal sealed record QuestionDef(
    string Id,
    QuestionType Type,
    string Prompt,
    string? Description,
    string? Default,
    string? WhenCondition,
    IReadOnlyList<OptionDef> Options) : QuestionNode;

/// <summary>A repeatable group of questions.</summary>
internal sealed record RepeatGroup(
    string Id,
    string ItemName,
    string? ItemLabel,
    string? WhenCondition,
    CountDef Count,
    IReadOnlyList<QuestionDef> Questions) : QuestionNode;

/// <summary>The count control for a repeat group.</summary>
internal sealed record CountDef(
    string Id,
    string Prompt,
    int Default,
    int Minimum,
    int? Maximum);

/// <summary>An option within a choice question.</summary>
internal sealed record OptionDef(string Id, string Label);

/// <summary>An output file to generate.</summary>
internal sealed record OutputSpec(
    string Id,
    string Path,
    string RenderTemplate,
    string? Validator,
    string? WhenCondition);

internal enum QuestionType
{
    Confirm,
    Text,
    Integer,
    Choice
}
