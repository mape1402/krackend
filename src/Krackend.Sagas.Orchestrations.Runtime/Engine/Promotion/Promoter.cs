namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion
{
    internal class Promoter : IPromoter
    {
        public async Task<PromotionResult> PromoteToInstanceAsync(PromotionRequest request, CancellationToken cancellationToken = default)
        {
            // Search artifact by id from storage then create and save new SAGA instance. Only, create and save, actions are promoted by decision engine.

            await Task.Delay(10);
            return new PromotionResult { Success = true, SagaId = "12345678900" };
        }
    }
}
