using System;

namespace Npgsql.Pipelines.Protocol;

[Flags]
enum ExecutionFlags
{
    Default = 0,

    // Relevant CommandBehavior flags for a single command, these are currently mapped to the same integers, otherwise change extension method CommandBehavior.ToExecutionFlags.
    SchemaOnly = 2,  // column info, no data, no effect on database
    KeyInfo = 4,  // column info + primary key information (if available)
    SingleRow = 8, // data, hint single row and single result, may affect database - doesn't apply to child(chapter) results
    SequentialAccess = 16,

    Unprepared = 64,
    Preparing = 128,
    Prepared = 256,
    ErrorBarrier = 512, // Can be combined with one of the three previous flags.
}

static class ExecutionFlagsExtensions
{
    public static bool HasUnprepared(this ExecutionFlags flags) => (flags & ExecutionFlags.Unprepared) == ExecutionFlags.Unprepared;
    public static bool HasErrorBarrier(this ExecutionFlags flags) => (flags & ExecutionFlags.ErrorBarrier) == ExecutionFlags.ErrorBarrier;
    public static bool HasPreparing(this ExecutionFlags flags) => (flags & ExecutionFlags.Preparing) == ExecutionFlags.Preparing;
    public static bool HasPrepared(this ExecutionFlags flags) => (flags & ExecutionFlags.Prepared) == ExecutionFlags.Prepared;
}

/// <summary>
/// Called right before the write commences.
/// </summary>
/// <param name="values">Values containing any updated (effective) ExecutionFlags for the protocol it will execute on.</param>
/// <returns>CommandExecution state that is used for the duration of the execution.</returns>
delegate CommandExecution CreateExecutionDelegate(in ICommand.Values values);

interface ICommand
{
    // The underlying values might change so we hand out a copy.
    Values GetValues();

    // Also exposed as a delegate to help struct composition where carrying an interface constraint or boxing is not desired
    // CreateExecution should not depend on instance state anyway, if it needs any then Values.State is the vehicle of choice.
    CreateExecutionDelegate CreateExecutionDelegate { get; }

    /// <summary>
    /// Called right before the write commences.
    /// </summary>
    /// <param name="values">Values containing any updated (effective) ExecutionFlags for the protocol it will execute on.</param>
    /// <returns>CommandExecution state that is used for the duration of the execution.</returns>
    public CommandExecution CreateExecution(in Values values);

    public readonly record struct Values
    {
        public required CommandParameters CommandParameters { get; init; }
        public required string StatementText { get; init; }
        public required TimeSpan Timeout { get; init; }
        public required ExecutionFlags ExecutionFlags { get; init; }
        public Statement? Statement { get; init; }
        public object? State { get; init; }
    }
}
