namespace Krackend.Domain.Contracts
{
    /// <summary>
    /// Represents an object as database entity.
    /// </summary>
    public interface IEntity
    {
    }

    ///<inheritdoc/>
    /// <typeparam name="TKey">Id type.</typeparam>
    public interface IEntity<TKey> : IEntity
    {
        /// <summary>
        /// Gets or sets the id field.
        /// </summary>
        public TKey Id { get; set; }
    }
}
