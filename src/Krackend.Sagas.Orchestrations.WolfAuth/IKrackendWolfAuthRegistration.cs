namespace Krackend.Sagas.Orchestrations.WolfAuth;

/// <summary>
/// Marker service indicating that a Krackend host opted into WolfAuth authentication.
/// </summary>
public interface IKrackendWolfAuthRegistration;

internal sealed class KrackendWolfAuthRegistration : IKrackendWolfAuthRegistration;
