using Krackend.Sagas.Orchestration.Controller.Dispatching;

namespace Krackend.Sagas.Orchestration.Tests.Controller
{
    public class DefaultSagaIdGeneratorTests
    {
        [Fact]
        public async Task CreateNew_ReturnsUniqueUlidString()
        {
            var generator = new DefaultSagaIdGenerator();
            var id1 = await generator.CreateNew();
            var id2 = await generator.CreateNew();
            Assert.False(string.IsNullOrWhiteSpace(id1));
            Assert.False(string.IsNullOrWhiteSpace(id2));
            Assert.NotEqual(id1, id2);
        }
    }
}
