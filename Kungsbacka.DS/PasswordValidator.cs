using System;
using System.Text.RegularExpressions;

namespace Kungsbacka.DS
{
    public static class PasswordValidator
    {
        public static bool IsPasswordComplex(string password, string samAccountName, string displayName)
        {
            if (string.IsNullOrEmpty(password))
            {
                return false;
            }
            int complexity = 0;
            if (Regex.IsMatch(password, "[a-z]"))
                complexity++;
            if (Regex.IsMatch(password, "[A-Z]"))
                complexity++;
            if (Regex.IsMatch(password, "[0-9]"))
                complexity++;
            if (Regex.IsMatch(password, "[^a-zA-Z0-9]"))
                complexity++;
            if (!string.IsNullOrEmpty(displayName))
            {
                string[] parts = Regex.Split(displayName, @"[^\w]");
                foreach (string part in parts)
                {
                    if (part.Length > 2)
                    {
                        if (password.IndexOf(part, StringComparison.OrdinalIgnoreCase) > -1)
                        {
                            complexity = 0;
                            break;
                        }
                    }
                }
            }
            if (!string.IsNullOrEmpty(samAccountName))
            {
                if (password.IndexOf(samAccountName, StringComparison.OrdinalIgnoreCase) > -1)
                {
                    complexity = 0;
                }
            }
            return complexity > 2;
        }
    }
}
