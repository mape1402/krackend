namespace Krackend.Domain.Relational
{
    using Krackend.Domain.Contracts;
    using Krackend.Domain.Entities;

    ///<inheritdoc/>
    public class SystemVariable : BaseSystemVariable, IEntity<Guid>
    {
        ///<inheritdoc/>
        public Guid Id { get; set; }
    }
}
