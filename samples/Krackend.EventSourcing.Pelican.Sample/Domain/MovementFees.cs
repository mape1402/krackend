namespace Krackend.EventSourcing.Pelican.Sample.Domain;

public static class MovementFees
{
    public static decimal Calculate(decimal amount)
        => amount < 0m ? 2.50m : 0m;
}
