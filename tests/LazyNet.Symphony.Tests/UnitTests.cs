using FluentAssertions;
using LazyNet.Symphony.Core;

namespace LazyNet.Symphony.Tests;

/// <summary>
/// Tests for the Unit struct.
/// </summary>
public class UnitTests
{
    [Fact]
    public void Value_ShouldReturnDefaultUnit()
    {
        // Act
        var unit = Unit.Value;

        // Assert
        unit.Should().Be(default(Unit));
    }

    [Fact]
    public async Task Task_ShouldReturnCompletedTask()
    {
        // Act
        var task = Unit.Task;

        // Assert
        task.Should().NotBeNull();
        task.IsCompleted.Should().BeTrue();
        var result = await task;
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public void Task_ShouldReturnSameInstance()
    {
        // Act
        var task1 = Unit.Task;
        var task2 = Unit.Task;

        // Assert
        task1.Should().BeSameAs(task2);
    }

    [Fact]
    public void Equals_WithUnit_ShouldReturnTrue()
    {
        // Arrange
        var unit1 = Unit.Value;
        var unit2 = Unit.Value;

        // Act & Assert
        unit1.Equals(unit2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithObject_WhenUnit_ShouldReturnTrue()
    {
        // Arrange
        var unit = Unit.Value;
        object obj = Unit.Value;

        // Act & Assert
        unit.Equals(obj).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithObject_WhenNotUnit_ShouldReturnFalse()
    {
        // Arrange
        var unit = Unit.Value;
        object obj = "not a unit";

        // Act & Assert
        unit.Equals(obj).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithObject_WhenNull_ShouldReturnFalse()
    {
        // Arrange
        var unit = Unit.Value;
        object? obj = null;

        // Act & Assert
        unit.Equals(obj).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_ShouldReturnZero()
    {
        // Arrange
        var unit = Unit.Value;

        // Act & Assert
        unit.GetHashCode().Should().Be(0);
    }

    [Fact]
    public void ToString_ShouldReturnParentheses()
    {
        // Arrange
        var unit = Unit.Value;

        // Act & Assert
        unit.ToString().Should().Be("()");
    }

    [Fact]
    public void CompareTo_WithUnit_ShouldReturnZero()
    {
        // Arrange
        var unit1 = Unit.Value;
        var unit2 = Unit.Value;

        // Act & Assert
        unit1.CompareTo(unit2).Should().Be(0);
    }

    [Fact]
    public void CompareTo_WithObject_ShouldReturnZero()
    {
        // Arrange
        var unit = Unit.Value;
        object obj = Unit.Value;

        // Act & Assert
        unit.CompareTo(obj).Should().Be(0);
    }

    [Fact]
    public void CompareTo_WithNullObject_ShouldReturnZero()
    {
        // Arrange
        var unit = Unit.Value;
        object? obj = null;

        // Act & Assert
        unit.CompareTo(obj).Should().Be(0);
    }

    [Fact]
    public void EqualityOperator_ShouldReturnTrue()
    {
        // Arrange
        var unit1 = Unit.Value;
        var unit2 = Unit.Value;

        // Act & Assert
        (unit1 == unit2).Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_ShouldReturnFalse()
    {
        // Arrange
        var unit1 = Unit.Value;
        var unit2 = Unit.Value;

        // Act & Assert
        (unit1 != unit2).Should().BeFalse();
    }

    [Fact]
    public void DefaultUnit_ShouldEqualValue()
    {
        // Arrange
        var defaultUnit = default(Unit);
        var valueUnit = Unit.Value;

        // Act & Assert
        defaultUnit.Should().Be(valueUnit);
    }

    [Fact]
    public void Unit_ShouldBeValueType()
    {
        // Assert
        typeof(Unit).IsValueType.Should().BeTrue();
    }

    [Fact]
    public void Unit_ShouldImplementIEquatable()
    {
        // Assert
        typeof(Unit).Should().Implement<IEquatable<Unit>>();
    }

    [Fact]
    public void Unit_ShouldImplementIComparable()
    {
        // Assert
        typeof(Unit).Should().Implement<IComparable>();
        typeof(Unit).Should().Implement<IComparable<Unit>>();
    }
}
