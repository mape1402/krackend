using Krackend.Sagas.Orchestration.Controller.Dispatching;

namespace Krackend.Sagas.Orchestration.Tests.Controller
{
    public class DispatcherTests
    {
        [Fact]
        public void Constructor_NullStateManager_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new Dispatcher(null));
        }
    }
}
