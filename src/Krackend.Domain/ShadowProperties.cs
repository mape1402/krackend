namespace Krackend.Domain
{
    /// <summary>
    /// Contains the auditable field names.
    /// </summary>
    public static class ShadowProperties
    {
        /// <summary>
        /// Gets the 'CreatedOn' field name.
        /// </summary>
        public const string CreatedOn = "CreatedOn";

        /// <summary>
        /// Gets the 'CreatedBy' field name.
        /// </summary>
        public const string CreatedBy = "CreatedBy";

        /// <summary>
        /// Gets the 'CreatedAt' field name.
        /// </summary>
        public const string CreatedAt = "CreatedAt";

        /// <summary>
        /// Gets the 'ModifiedOn' field name.
        /// </summary>
        public const string ModifiedOn = "ModifiedOn";

        /// <summary>
        /// Gets the 'ModifiedBy' field name.
        /// </summary>
        public const string ModifiedBy = "ModifiedBy";

        /// <summary>
        /// Gets the 'ModifiedAt' field name.
        /// </summary>
        public const string ModifiedAt = "ModifiedAt";

        /// <summary>
        /// Gets the 'IsDeleted' field name.
        /// </summary>
        public const string IsDeleted = "IsDeleted";

        /// <summary>
        /// Gets the 'RowVersion' field name.
        /// </summary>
        public const string RowVersion = "RowVersion";
    }
}
