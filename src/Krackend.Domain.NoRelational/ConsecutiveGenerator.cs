namespace Krackend.Domain.NoRelational
{
    using Krackend.Domain.Contracts;
    using Krackend.Domain.Entities;
    using MongoDB.Bson;

    ///<inheritdoc/>
    public class ConsecutiveGenerator : BaseConsecutiveGenerator, IEntity<ObjectId>
    {
        ///<inheritdoc/>
        public ObjectId Id { get; set; }
    }
}
