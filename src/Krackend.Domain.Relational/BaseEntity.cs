namespace Krackend.Domain.Relational
{
    using Krackend.Domain.Contracts;

    ///<inheritdoc/>
    public abstract class BaseEntity : IEntity<Guid>
    {
        ///<inheritdoc/>
        public Guid Id { get; set; }
    }
}
