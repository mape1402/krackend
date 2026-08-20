using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine
{
    internal static class RuntimeIdParser
    {
        public static Id Parse(string value)
            => new(Ulid.Parse(value));
    }
}
