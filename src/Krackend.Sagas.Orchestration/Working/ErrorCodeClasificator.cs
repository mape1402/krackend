namespace Krackend.Sagas.Orchestration.Working
{
    /// <summary>
    /// Represents a delegate that classifies an exception type and returns an associated error code.
    /// </summary>
    /// <param name="exceptionType">The type of the exception to classify.</param>
    /// <returns>The error code associated with the exception type.</returns>
    public delegate int ErrorCodeClasificator(Type exceptionType);
}
