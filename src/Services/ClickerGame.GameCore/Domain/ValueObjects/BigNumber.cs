namespace ClickerGame.GameCore.Domain.ValueObjects
{
    public record struct BigNumber
    {
        private readonly decimal _mantissa;
        private readonly int _exponent;

        public static readonly BigNumber Zero = new(0, 0);
        public static readonly BigNumber One = new(1, 0);

        public BigNumber(decimal mantissa, int exponent = 0)
        {
            if (mantissa >= 1000)
            {
                var normalizedMantissa = mantissa;
                var additionalExponent = 0;

                while (normalizedMantissa >= 1000)
                {
                    normalizedMantissa /= 1000;
                    additionalExponent += 3;
                }

                _mantissa = normalizedMantissa;
                _exponent = exponent + additionalExponent;
            }
            else
            {
                _mantissa = mantissa;
                _exponent = exponent;
            }
        }

        public static BigNumber operator +(BigNumber left, BigNumber right)
        {
            if (left._exponent == right._exponent)
                return new BigNumber(left._mantissa + right._mantissa, left._exponent);

            var (larger, smaller) = left._exponent > right._exponent ? (left, right) : (right, left);
            var exponentDiff = larger._exponent - smaller._exponent;

            if (exponentDiff > 10) return larger;

            var smallerNormalized = smaller._mantissa / (decimal)Math.Pow(1000, exponentDiff / 3.0);
            return new BigNumber(larger._mantissa + smallerNormalized, larger._exponent);
        }

        public static BigNumber operator *(BigNumber left, BigNumber right)
        {
            return new BigNumber(left._mantissa * right._mantissa, left._exponent + right._exponent);
        }

        public static BigNumber operator *(BigNumber left, decimal right)
        {
            return new BigNumber(left._mantissa * right, left._exponent);
        }

        public static BigNumber operator *(decimal left, BigNumber right)
        {
            return new BigNumber(left * right._mantissa, right._exponent);
        }

        public static bool operator >(BigNumber left, BigNumber right)
        {
            if (left._exponent > right._exponent) return true;
            if (left._exponent < right._exponent) return false;
            return left._mantissa > right._mantissa;
        }

        public static bool operator <(BigNumber left, BigNumber right)
        {
            if (left._exponent < right._exponent) return true;
            if (left._exponent > right._exponent) return false;
            return left._mantissa < right._mantissa;
        }

        public static bool operator >=(BigNumber left, BigNumber right)
        {
            return left > right || left.Equals(right);
        }

        public static bool operator <=(BigNumber left, BigNumber right)
        {
            return left < right || left.Equals(right);
        }

       

        public static BigNumber operator -(BigNumber left, BigNumber right)
        {
            if (left._exponent == right._exponent)
            {
                var result = left._mantissa - right._mantissa;
                if (result < 0) return Zero; 
                return new BigNumber(result, left._exponent);
            }

            var (larger, smaller) = left._exponent > right._exponent ? (left, right) : (right, left);
            var exponentDiff = larger._exponent - smaller._exponent;

            if (exponentDiff > 10) return left; // Small number becomes negligible

            var smallerNormalized = smaller._mantissa / (decimal)Math.Pow(1000, exponentDiff / 3.0);
            var result2 = left._mantissa - smallerNormalized;

            if (result2 < 0) return Zero; 
            return new BigNumber(result2, left._exponent);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_mantissa, _exponent);
        }

        public override string ToString()
        {
            if (_exponent == 0) return _mantissa.ToString("N0");
            if (_exponent < 6) return (_mantissa * (decimal)Math.Pow(1000, _exponent / 3.0)).ToString("N0");

            var suffixes = new[] { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };
            var suffixIndex = _exponent / 3;

            if (suffixIndex < suffixes.Length)
                return $"{_mantissa:F2}{suffixes[suffixIndex]}";
            else
                return $"{_mantissa:F2}e{_exponent}";
        }

        public decimal ToDecimal()
        {
            if (_exponent == 0) return _mantissa;
            if (_exponent > 28) return decimal.MaxValue;

            return _mantissa * (decimal)Math.Pow(1000, _exponent / 3.0);
        }
    }
}