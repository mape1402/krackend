namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion
{
    public class PromotionResult
    {
        public bool Success { get; set; }

        public string ErrorMessage { get; set; }

        public string SagaId { get; set; }

        public string InstanceId { get; set; }
    }
}
