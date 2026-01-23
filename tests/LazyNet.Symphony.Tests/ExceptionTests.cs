using FluentAssertions;
using LazyNet.Symphony.Exceptions;
using LazyNet.Symphony.Interfaces;

namespace LazyNet.Symphony.Tests;

/// <summary>
/// Tests for Symphony exception classes.
/// </summary>
public class ExceptionTests
{
    #region HandlerNotFoundException Tests

    [Fact]
    public void HandlerNotFoundException_ShouldSetRequestType()
    {
        // Arrange
        var requestType = typeof(TestRequest);

        // Act
        var exception = new HandlerNotFoundException(requestType);

        // Assert
        exception.RequestType.Should().Be(requestType);
        exception.ExpectedHandlerType.Should().BeNull();
    }

    [Fact]
    public void HandlerNotFoundException_ShouldSetExpectedHandlerType()
    {
        // Arrange
        var requestType = typeof(TestRequest);
        var handlerType = typeof(TestRequestHandler);

        // Act
        var exception = new HandlerNotFoundException(requestType, handlerType);

        // Assert
        exception.RequestType.Should().Be(requestType);
        exception.ExpectedHandlerType.Should().Be(handlerType);
    }

    [Fact]
    public void HandlerNotFoundException_ShouldHaveCorrectErrorCode()
    {
        // Arrange
        var exception = new HandlerNotFoundException(typeof(TestRequest));

        // Assert
        exception.ErrorCode.Should().Be("SYMPHONY_HANDLER_NOT_FOUND");
    }

    [Fact]
    public void HandlerNotFoundException_ShouldContainRequestTypeInMessage()
    {
        // Arrange
        var requestType = typeof(TestRequest);

        // Act
        var exception = new HandlerNotFoundException(requestType);

        // Assert
        exception.Message.Should().Contain("TestRequest");
    }

    [Fact]
    public void HandlerNotFoundException_ShouldPopulateContext()
    {
        // Arrange
        var requestType = typeof(TestRequest);
        var handlerType = typeof(TestRequestHandler);

        // Act
        var exception = new HandlerNotFoundException(requestType, handlerType);

        // Assert
        exception.Context.Should().ContainKey("RequestType");
        exception.Context.Should().ContainKey("ExpectedHandlerType");
        exception.Context["RequestType"].Should().Be(requestType.FullName);
        exception.Context["ExpectedHandlerType"].Should().Be(handlerType.FullName);
    }

    [Fact]
    public void HandlerNotFoundException_WithInnerException_ShouldSetInnerException()
    {
        // Arrange
        var requestType = typeof(TestRequest);
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new HandlerNotFoundException(requestType, null, innerException);

        // Assert
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void HandlerNotFoundException_ToString_ShouldIncludeErrorCode()
    {
        // Arrange
        var exception = new HandlerNotFoundException(typeof(TestRequest));

        // Act
        var result = exception.ToString();

        // Assert
        result.Should().Contain("[SYMPHONY_HANDLER_NOT_FOUND]");
    }

    [Fact]
    public void HandlerNotFoundException_ToString_ShouldIncludeContext()
    {
        // Arrange
        var exception = new HandlerNotFoundException(typeof(TestRequest), typeof(TestRequestHandler));

        // Act
        var result = exception.ToString();

        // Assert
        result.Should().Contain("Context:");
        result.Should().Contain("RequestType=");
    }

    #endregion

    #region MethodResolutionException Tests

    [Fact]
    public void MethodResolutionException_ShouldSetTargetTypeAndMethodName()
    {
        // Arrange
        var targetType = typeof(TestRequestHandler);
        var methodName = "Handle";

        // Act
        var exception = new MethodResolutionException(targetType, methodName);

        // Assert
        exception.TargetType.Should().Be(targetType);
        exception.MethodName.Should().Be(methodName);
        exception.ExpectedParameterTypes.Should().BeNull();
    }

    [Fact]
    public void MethodResolutionException_ShouldHaveCorrectErrorCode()
    {
        // Arrange
        var exception = new MethodResolutionException(typeof(TestRequestHandler), "Handle");

        // Assert
        exception.ErrorCode.Should().Be("SYMPHONY_METHOD_RESOLUTION_FAILED");
    }

    [Fact]
    public void MethodResolutionException_WithParameterTypes_ShouldSetExpectedParameterTypes()
    {
        // Arrange
        var targetType = typeof(TestRequestHandler);
        var methodName = "Handle";
        var parameterTypes = new[] { typeof(TestRequest), typeof(CancellationToken) };

        // Act
        var exception = new MethodResolutionException(targetType, methodName, parameterTypes);

        // Assert
        exception.ExpectedParameterTypes.Should().BeEquivalentTo(parameterTypes);
    }

    [Fact]
    public void MethodResolutionException_WithParameterTypes_ShouldPopulateContext()
    {
        // Arrange
        var targetType = typeof(TestRequestHandler);
        var methodName = "Handle";
        var parameterTypes = new[] { typeof(TestRequest), typeof(CancellationToken) };

        // Act
        var exception = new MethodResolutionException(targetType, methodName, parameterTypes);

        // Assert
        exception.Context.Should().ContainKey("ExpectedParameterTypes");
        exception.Context["ExpectedParameterTypes"].Should().Be("TestRequest, CancellationToken");
    }

    [Fact]
    public void MethodResolutionException_WithInnerException_ShouldSetInnerException()
    {
        // Arrange
        var targetType = typeof(TestRequestHandler);
        var methodName = "Handle";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new MethodResolutionException(targetType, methodName, innerException);

        // Assert
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void MethodResolutionException_ShouldContainMethodNameInMessage()
    {
        // Arrange
        var exception = new MethodResolutionException(typeof(TestRequestHandler), "Handle");

        // Assert
        exception.Message.Should().Contain("Handle");
        exception.Message.Should().Contain("TestRequestHandler");
    }

    #endregion

    #region HandlerValidationException Tests

    [Fact]
    public void HandlerValidationException_ShouldSetHandlerTypeAndValidationRule()
    {
        // Arrange
        var handlerType = typeof(TestRequestHandler);
        var validationRule = "ConcreteClass";
        var message = "Handler must be a concrete class";

        // Act
        var exception = new HandlerValidationException(handlerType, validationRule, message);

        // Assert
        exception.HandlerType.Should().Be(handlerType);
        exception.ValidationRule.Should().Be(validationRule);
    }

    [Fact]
    public void HandlerValidationException_ShouldHaveCorrectErrorCode()
    {
        // Arrange
        var exception = new HandlerValidationException(typeof(TestRequestHandler), "Rule", "Message");

        // Assert
        exception.ErrorCode.Should().Be("SYMPHONY_HANDLER_VALIDATION_FAILED");
    }

    [Fact]
    public void HandlerValidationException_ShouldPopulateContext()
    {
        // Arrange
        var handlerType = typeof(TestRequestHandler);
        var validationRule = "ConcreteClass";

        // Act
        var exception = new HandlerValidationException(handlerType, validationRule, "Message");

        // Assert
        exception.Context.Should().ContainKey("HandlerType");
        exception.Context.Should().ContainKey("ValidationRule");
        exception.Context["HandlerType"].Should().Be(handlerType.FullName);
        exception.Context["ValidationRule"].Should().Be(validationRule);
    }

    [Fact]
    public void HandlerValidationException_WithInnerException_ShouldSetInnerException()
    {
        // Arrange
        var handlerType = typeof(TestRequestHandler);
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new HandlerValidationException(handlerType, "Rule", "Message", innerException);

        // Assert
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void HandlerValidationException_ShouldContainHandlerTypeInMessage()
    {
        // Arrange
        var exception = new HandlerValidationException(typeof(TestRequestHandler), "Rule", "Custom message");

        // Assert
        exception.Message.Should().Contain("TestRequestHandler");
        exception.Message.Should().Contain("Custom message");
    }

    #endregion

    #region SymphonyException Base Tests

    [Fact]
    public void SymphonyException_WithContext_ShouldAddContextData()
    {
        // Arrange
        var exception = new HandlerNotFoundException(typeof(TestRequest));

        // Act
        exception.WithContext("CustomKey", "CustomValue");

        // Assert
        exception.Context.Should().ContainKey("CustomKey");
        exception.Context["CustomKey"].Should().Be("CustomValue");
    }

    [Fact]
    public void SymphonyException_WithContext_ShouldSupportChaining()
    {
        // Arrange
        var exception = new HandlerNotFoundException(typeof(TestRequest));

        // Act
        var result = exception
            .WithContext("Key1", "Value1")
            .WithContext("Key2", 42)
            .WithContext("Key3", true);

        // Assert
        result.Should().BeSameAs(exception);
        exception.Context.Should().HaveCount(5); // 2 auto-added + 3 manual
        exception.Context["Key1"].Should().Be("Value1");
        exception.Context["Key2"].Should().Be(42);
        exception.Context["Key3"].Should().Be(true);
    }

    [Fact]
    public void SymphonyException_WithContext_ShouldOverwriteExistingKey()
    {
        // Arrange
        var exception = new HandlerNotFoundException(typeof(TestRequest));
        exception.WithContext("Key", "OldValue");

        // Act
        exception.WithContext("Key", "NewValue");

        // Assert
        exception.Context["Key"].Should().Be("NewValue");
    }

    [Fact]
    public void SymphonyException_WithContext_ShouldSupportNullValue()
    {
        // Arrange
        var exception = new HandlerNotFoundException(typeof(TestRequest));

        // Act
        exception.WithContext("NullKey", null);

        // Assert
        exception.Context.Should().ContainKey("NullKey");
        exception.Context["NullKey"].Should().BeNull();
    }

    [Fact]
    public void SymphonyException_ToString_WithoutContext_ShouldNotIncludeContextSection()
    {
        // Arrange
        var exception = new HandlerNotFoundException(typeof(TestRequest));
        exception.Context.Clear(); // Clear auto-added context

        // Act
        var result = exception.ToString();

        // Assert
        result.Should().NotContain("Context:");
    }

    #endregion

    #region Test Types

    public record TestRequest : IRequest<string>;

    public class TestRequestHandler : IRequestHandler<TestRequest, string>
    {
        public Task<string> Handle(TestRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("Result");
        }
    }

    #endregion
}
