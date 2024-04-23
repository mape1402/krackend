namespace Krackend.Domain.Relational
{
    using Krackend.Domain.Contracts;
    using Krackend.Domain.Entities;

    ///<inheritdoc/>
    public class ConsecutiveGenerator : BaseConsecutiveGenerator, IEntity<Guid>
    {
        ///<inheritdoc/>
        public Guid Id { get; set; }
    }
}
