using System;
using System.Globalization;
using IronLua.Util;

namespace IronLua.Library
{
    static class NumberUtil
    {
        const NumberStyles DECIMAL_NUMBER_STYLE = NumberStyles.AllowDecimalPoint |
                                                  NumberStyles.AllowExponent |
                                                  NumberStyles.AllowTrailingSign;
        // Convert an integer number
        public static bool TryConvertStringToInteger(string number, out long result)
        {
            bool IsNeg(ref int n)
            {
                if (number[n] == '-') { n++; return true; }
                if (number[n] == '+') { n++; } return false;
            }

            result = 0;

            var a = 0ul;
            var empty = true;
            var i = 0;

            // skip initial spaces
            for (; i < number.Length && number[i].IsWhiteSpace(); ++i) { }

            var neg = IsNeg(ref i);

            if (i < number.Length - 1 && number[i] == '0' && // hex?
                (number[i + 1] == 'x' || number[i + 1] == 'X'))
            {
                i += 2; // skip '0x'
                for (; i < number.Length && number[i].IsHexValue(out var h); ++i)
                {
                    a = a * 16 + (uint) h;
                    empty = false;
                }
            }
            else // decimal
            {
                const ulong MAXBY10 = ulong.MaxValue / 10;
                const uint MAXLASTD = (uint) (ulong.MaxValue % 10);
                for (; i < number.Length && number[i].IsDecimalValue(out var d); ++i)
                {
                    if (a >= MAXBY10 && (a > MAXBY10 || d > MAXLASTD + (neg ? 1 : 0)))
                        return false;  /* do not accept it (as integer) */
                    a = a * 10 + (uint) d;
                    empty = false;
                }
            }

            // skip trailing spaces
            for (; i < number.Length && number[i].IsWhiteSpace(); ++i) { }

            if (empty || i != number.Length)
                return false;  // something wrong in the numeral

            result = (long)(neg ? 0ul - a : a);
            return true;
        }

        // convert a decimal number
        public static bool TryConvertStringToDecimal(string number, out double result)
        {
            // maximum number of significant digits to read (to avoid overflows even with single floats)
            const int MAXSIGDIG = 30;

            bool IsNeg(ref int n)
            {
                if (number[n] == '-') { n++; return true; }
                if (number[n] == '+') { n++; }
                return false;
            }

            var i = 0;

            // skip initial spaces
            for (; i < number.Length && char.IsWhiteSpace(number[i]); ++i) { }

            double r = 0.0;  /* result (accumulator) */
            int e = 0;  /* exponent correction */
            int sigdig = 0;  /* number of significant digits */
            int nosigdig = 0;  /* number of non-significant digits */
            var hasdot = false;  /* true after seen a dot */
            var neg = IsNeg(ref i);  // check signal

            // check for hexadecimal numbers
            if (i < number.Length - 1 && number[i] == '0' &&
                (number[i + 1] == 'x' || number[i + 1] == 'X'))
            {
                for (i += 2; i < number.Length; ++i) // skip '0x' and read numeral
                {
                    if (number[i] == '.')
                    {
                        if (hasdot) break; // second dot? stop loop
                        else hasdot = true;
                    }
                    else if (number[i].IsHexValue(out var h))
                    {
                        if (sigdig == 0 && number[i] == '0') /* non-significant digit (zero)? */
                            nosigdig++;
                        else if (++sigdig <= MAXSIGDIG) // can read it without overflow?
                            r = r * 16.0d + h;
                        else
                            e++; // too many digits; ignore, but still count for exponent
                        if (hasdot)
                            e--; // decimal digit? correct exponent
                    }
                    else
                    {
                        break; // neither a dot nor a digit
                    }
                }

                if (nosigdig + sigdig == 0) /* no digits? */
                {
                    result = default(double);
                    return false; /* invalid format */
                }

                e *= 4; // each digit multiplies/divides value by 2^4

                // exponent part?
                if (i < number.Length && (number[i] == 'p' || number[i] == 'P'))
                {
                    i += 1; // skip 'p'
                    var neg1 = IsNeg(ref i); // signal

                    var idx = i;
                    int exp1 = 0; // exponent value
                    for (; i < number.Length && number[i].IsDecimalValue(out var d); ++i)
                    {
                        exp1 = exp1 * 10 + d;
                    }
                    if (idx == i) // did index advance?
                    {
                        result = default(double);
                        return false; // invalid; must have at least one digit
                    }
                    e += neg1 ? -exp1 : exp1;
                }

                result = (neg ? -r : r) * Math.Pow(2, e);
                return true;
            }

            return double.TryParse(number.Substring(i), DECIMAL_NUMBER_STYLE, CultureInfo.InvariantCulture, out result);
        }

        /* Parses a decimal number */
        public static bool TryParseDecimalNumber(string number, out double result)
        {
            return Double.TryParse(number, DECIMAL_NUMBER_STYLE, CultureInfo.InvariantCulture, out result);
        }

        /* Parses a hex number */
        public static bool TryParseHexNumber(string number, bool exponentAllowed, out double result)
        {
            string hexIntPart = null;
            string hexFracPart = null;
            string exponentPart = null;

            var fields = number.Split('p', 'P'); // split between mantissa and exponent
            if (fields.Length >= 1)
            {
                var parts = fields[0].Split('.'); // split on integer and fraction parts
                if (parts.Length >= 1)
                    hexIntPart = parts[0];
                if (parts.Length == 2)
                    hexFracPart = parts[1];
            }
            if (fields.Length == 2)
            {
                exponentPart = fields[1];

                if (!exponentAllowed)
                {
                    result = 0;
                    return false;
                }
            }

            ulong integer = 0;
            double fraction = 0;
            long exponent = 0;

            bool successful = true;

            if (!String.IsNullOrEmpty(hexIntPart))
                successful &= UInt64.TryParse(hexIntPart, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out integer);

            if (!String.IsNullOrEmpty(hexFracPart))
            {
                ulong value;
                successful &= UInt64.TryParse(hexFracPart, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
                fraction = value / Math.Pow(16.0, hexFracPart.Length);
            }

            if (!String.IsNullOrEmpty(exponentPart))
                successful &= Int64.TryParse(exponentPart, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent);

            result = (integer + fraction) * Math.Pow(2.0, exponent);
            return successful;
        }
    }
}
