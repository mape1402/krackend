using Krackend.Sagas.Orchestration.Core;

namespace Krackend.Sagas.Orchestration.Tests.Core
{
    public class OrchestrationContextTests
    {
        [Fact]
        public void SetAndGetMetadata_WorksCorrectly()
        {
            var context = new OrchestrationContext();
            var metadata = new Metadata();
            context.SetMetadata(metadata);
            Assert.True(context.HasMetadata());
            Assert.Equal(metadata, context.GetMetadata());
        }

        [Fact]
        public void SetAsFailure_SetsErrorMetadataAndOperationalResults()
        {
            var context = new OrchestrationContext();
            var metadata = new Metadata();
            context.SetMetadata(metadata);
            var error = new ErrorMetadata { Code = 1, Message = "fail", StackTrace = "stack" };
            context.SetAsFailure(error);
            Assert.True(context.HasError());
            Assert.Equal(error, context.GetErrorMetadata());
            Assert.True(context.GetMetadata().OperationalResults.HasError);
        }

        [Fact]
        public void SetAsSuccess_SetsOperationalResultsToNoError()
        {
            var context = new OrchestrationContext();
            var metadata = new Metadata();
            context.SetMetadata(metadata);
            context.SetAsSuccess();
            Assert.False(context.GetMetadata().OperationalResults.HasError);
        }

        [Fact]
        public void SetMetadata_Null_Throws()
        {
            var context = new OrchestrationContext();
            Assert.Throws<ArgumentNullException>(() => context.SetMetadata(null));
        }

        [Fact]
        public void SetAsFailure_Null_Throws()
        {
            var context = new OrchestrationContext();
            Assert.Throws<ArgumentNullException>(() => context.SetAsFailure(null));
        }
    }
}
