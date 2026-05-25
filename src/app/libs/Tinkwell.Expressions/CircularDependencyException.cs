namespace Tinkwell.Expressions;

/// <summary>
/// Thrown by <see cref="DependencyWalker{TItem}"/> when a circular
/// dependency is detected or topological sorting cannot process every item
/// in the set.
/// </summary>
public sealed class CircularDependencyException : TinkwellException
{
    /// <summary>
    /// The names of items that could not be fully ordered. This is a
    /// superset of a minimal cycle: it includes any item with unsatisfied
    /// incoming edges after the walk (cycle members and possibly downstream
    /// dependents that remain blocked). Always non-empty.
    /// </summary>
    public IReadOnlyList<string> CycleParticipants { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CircularDependencyException"/> class.
    /// </summary>
    /// <param name="cycleParticipants">
    /// The names to report. See <see cref="CycleParticipants"/>.
    /// </param>
    public CircularDependencyException(IReadOnlyList<string> cycleParticipants)
        : base(
            "Circular dependency or blocked ordering: the following item names " +
            "could not be placed (the set can include a cycle and downstream " +
            "dependents, not just the nodes of a single cycle): " +
            string.Join(", ", cycleParticipants))
    {
        CycleParticipants = cycleParticipants;
    }
}
