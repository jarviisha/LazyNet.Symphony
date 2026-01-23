namespace LazyNet.Symphony.Core;

/// <summary>
/// Represents a void return type for requests that don't return a meaningful value.
/// This is the functional programming equivalent of void, but as a proper type.
/// </summary>
/// <remarks>
/// Use <see cref="Unit"/> as the response type for commands or requests that
/// perform an action but don't need to return data.
/// </remarks>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
    /// <summary>
    /// Gets the singleton value of <see cref="Unit"/>.
    /// </summary>
    public static Unit Value => default;

    /// <summary>
    /// Gets a completed task containing the <see cref="Unit"/> value.
    /// Use this to avoid allocating a new Task for each Unit return.
    /// </summary>
    public static Task<Unit> Task { get; } = System.Threading.Tasks.Task.FromResult(Value);

    /// <inheritdoc />
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <inheritdoc />
    public override string ToString() => "()";

    /// <inheritdoc />
    public int CompareTo(Unit other) => 0;

    /// <inheritdoc />
    public int CompareTo(object? obj) => 0;

    /// <summary>
    /// Determines whether two <see cref="Unit"/> instances are equal.
    /// </summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Determines whether two <see cref="Unit"/> instances are not equal.
    /// </summary>
    public static bool operator !=(Unit left, Unit right) => false;
}
