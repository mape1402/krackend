namespace Krackend.Domain.Entities
{
    /// <summary>
    /// Represents the base for the generator of integer consecutive values.
    /// </summary>
    public abstract class BaseConsecutiveGenerator
    {
        /// <summary>
        /// Gets or sets the type of entity.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the next value of the consecutive.
        /// </summary>
        public int Next { get; set; }
    }
}
