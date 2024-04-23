namespace Krackend.Domain.NoRelational
{
    using Krackend.Domain.Contracts;
    using MongoDB.Bson;

    ///<inheritdoc/>
    public abstract class BaseEntity : IEntity<ObjectId>
    {
        ///<inheritdoc/>
        public ObjectId Id { get; set; }
    }
}
