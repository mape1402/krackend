namespace Krackend.Domain.Entities
{
    /// <summary>
    /// Represents a system variable.
    /// </summary>
    public abstract class BaseSystemVariable
    {
        /// <summary>
        /// Gets or sets the key of variable.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the value of variable.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets the description of variable.
        /// </summary>
        public string Description { get; set; }
    }
}
