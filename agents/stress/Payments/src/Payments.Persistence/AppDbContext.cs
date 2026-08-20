using Microsoft.EntityFrameworkCore;
using TurtlePath.EntityFrameworkCore;
using TurtlePath.EntityFrameworkCore.Conventions;

namespace Payments.Persistence
{
    /// <summary>
    /// Represents the EF Core database context for the application.
    /// </summary>
    public sealed class AppDbContext : BaseDbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AppDbContext"/> class.
        /// </summary>
        /// <param name="options">The options to configure the context.</param>
        /// <param name="turtlePathOptions">The TurtlePath DbContext options.</param>
        /// <param name="modelConventions">The TurtlePath model conventions.</param>
        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            TurtlePathDbContextOptions turtlePathOptions,
            IEnumerable<ITurtlePathModelConvention> modelConventions)
            : base(options, turtlePathOptions, modelConventions)
        {
        }
    }
}
