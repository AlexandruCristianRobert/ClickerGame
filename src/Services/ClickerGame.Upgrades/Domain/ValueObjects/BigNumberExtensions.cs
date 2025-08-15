namespace ClickerGame.Upgrades.Domain.ValueObjects
{
    public static class BigNumberExtensions
    {
        public static BigNumber Percentage(this BigNumber value, decimal percentage)
        {
            return value * (percentage / 100m);
        }

        public static BigNumber ApplyMultiplier(this BigNumber value, decimal multiplier)
        {
            return value * multiplier;
        }

        public static bool IsZeroOrNegative(this BigNumber value)
        {
            return value <= BigNumber.Zero;
        }

        public static BigNumber Min(BigNumber a, BigNumber b)
        {
            return a <= b ? a : b;
        }

        public static BigNumber Max(BigNumber a, BigNumber b)
        {
            return a >= b ? a : b;
        }

        public static BigNumber Clamp(this BigNumber value, BigNumber min, BigNumber max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}