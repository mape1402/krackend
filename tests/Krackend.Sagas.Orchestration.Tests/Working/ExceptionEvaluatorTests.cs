using Krackend.Sagas.Orchestration.Working;

namespace Krackend.Sagas.Orchestration.Tests.Working
{
    public class ExceptionEvaluatorTests
    {
        [Fact]
        public void ExtractMetadata_ReturnsExpectedErrorMetadata()
        {
            // Arrange
            var expectedCode = 42;
            ErrorCodeClasificator clasificator = type => type == typeof(InvalidOperationException) ? expectedCode : 0;
            var evaluator = new ExceptionEvaluator(clasificator);
            var exception = new InvalidOperationException("Test message");

            // Act
            var metadata = evaluator.ExtractMetadata(exception);

            // Assert
            Assert.Equal(expectedCode, metadata.Code);
            Assert.Equal("Test message", metadata.Message);
            Assert.NotNull(metadata.StackTrace);
        }
    }
}
