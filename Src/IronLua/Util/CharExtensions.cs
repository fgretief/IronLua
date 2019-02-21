namespace IronLua.Util
{
    static class CharExtensions
    {
        public static bool IsDecimal(this char c)
        {
            return c >= '0' && c <= '9';
        }

        public static bool IsHex(this char c)
        {
            return
                (c >= '0' && c <= '9') ||
                (c >= 'a' && c <= 'f') ||
                (c >= 'A' && c <= 'F');
        }

        public static bool IsPunctuation(this char c)
        {
            switch (c)
            {
                case '+':
                case '-':
                case '*':
                case '/':
                case '%':
                case '^':
                case '#':
                case '~':
                case '<':
                case '>':
                case '=':
                case '(':
                case ')':
                case '{':
                case '}':
                case '[':
                case ']':
                case ';':
                case ':':
                case ',':
                case '.':
                case '&': // Lua 5.3: bitwise AND
                case '|': // Lua 5.3: bitwise OR
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsIdentifierStart(this char c)
        {
            return
                (c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c == '_');
        }

        public static bool IsIdentifier(this char c)
        {
            return
                (c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                (c == '_');
        }

        public static bool IsWhiteSpace(this char c)
        {
            switch (c)
            {
                case ' ': // space
                case '\f': // form feed
                case '\t': // horizontal tab
                case '\v': // vertical tab
                case '\n': // line feed
                case '\r': // carriage return
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsHexValue(this char c, out int value)
        {
            if ('0' <= c && c <= '9')
            {
                value = c - '0';
                return true;
            }

            if ('a' <= c && c <= 'f')
            {
                value = c - 'a' + 10;
                return true;
            }

            if ('A' <= c && c <= 'F')
            {
                value = c - 'A' + 10;
                return true;
            }

            value = default(int);
            return false;
        }

        public static bool IsDecimalValue(this char c, out int value)
        {
            if ('0' <= c && c <= '9')
            {
                value = c - '0';
                return true;
            }

            value = default(int);
            return false;
        }
    }
}
