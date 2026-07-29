using Krackend.EventSourcing.Core;

namespace Krackend.EventSourcing.Pelican.Sample.State;

public sealed class CustomerInitialStateFactory : IInitialStateFactory<CustomerState>
{
    private readonly CustomerInitialStateDefaults _defaults;

    public CustomerInitialStateFactory(CustomerInitialStateDefaults defaults)
    {
        _defaults = defaults;
    }

    public ValueTask<CustomerState> CreateAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(new CustomerState(
            string.Empty,
            string.Empty,
            string.Empty,
            _defaults.StartingBalance,
            IsCreated: false));
    }
}
