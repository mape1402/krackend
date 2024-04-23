namespace Krackend.Domain.Contracts
{
    /// <summary>
    /// Represents a database entity with auditable fields, row version and soft delete.
    /// </summary>
    public interface ITrackedEntity : IAuditable, IUseSoftDelete, IUseRowVersion
    {
    }
}
