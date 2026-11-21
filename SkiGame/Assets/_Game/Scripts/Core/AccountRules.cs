namespace Ski.Core
{
    /// <summary>
    /// The account service's rules for a new username and password (Unity Authentication), checked before
    /// anything is sent, so the player hears what is missing instead of a refusal they cannot read.
    /// </summary>
    public static class AccountRules
    {
        /// <summary>For the info note beside CREATE.</summary>
        public const string Rules = "Username: 3-20 letters, digits or . - _ @ (no spaces).\n"
                                  + "Password: 8-30 characters with a capital, a small letter, a number and a symbol, like ! or ?.";

        /// <summary>What is wrong with a new username (null when it is fine), said in a few words.</summary>
        public static string CheckName(string u)
        {
            if (string.IsNullOrEmpty(u) || u.Length < 3 || u.Length > 20) return "Name: 3-20 characters";
            foreach (char ch in u)
                if (!(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || ch == '.' || ch == '-' || ch == '_' || ch == '@'))
                    return ch == ' ' ? "Name: no spaces" : "Name: letters, digits";
            return null;
        }

        /// <summary>What a new password still needs (null when it is fine).</summary>
        public static string CheckPassword(string p)
        {
            if (string.IsNullOrEmpty(p) || p.Length < 8 || p.Length > 30) return "Password: 8-30 long";
            bool up = false, low = false, digit = false, sym = false;
            foreach (char ch in p)
            {
                if (char.IsUpper(ch)) up = true;
                else if (char.IsLower(ch)) low = true;
                else if (char.IsDigit(ch)) digit = true;
                else sym = true;
            }
            if (!up) return "Password needs A-Z";
            if (!low) return "Password needs a-z";
            if (!digit) return "Password needs 0-9";
            if (!sym) return "Password needs a symbol";
            return null;
        }
    }
}
