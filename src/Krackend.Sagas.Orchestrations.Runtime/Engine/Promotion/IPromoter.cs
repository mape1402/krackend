namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion
{
    internal interface IPromoter
    {
        Task<PromotionResult> PromoteToInstanceAsync(PromotionRequest request, CancellationToken cancellationToken = default);
    }
}
