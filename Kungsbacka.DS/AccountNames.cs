using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static System.FormattableString;
using System.Globalization;
using Kungsbacka.CommonExtensions;
using System.Text;

namespace Kungsbacka.DS
{
    /* Algorithms for creating account names:
     * SamAccountName
     *   First three letters from the first name plus the first three letters from
     *   the last name. If a collistion occurs, add a number suffix to the end (se
     *   suffixes below). If personnummer is supplied, the year (without century)
     *   is added to the beginning.
     * 
     * UserPrincipalName
     *   First name + dot (.) + last name + at (@) + upnDomain. If a collision
     *   occurs, add a number suffix before at (@) (see suffixes below).
     *
     * CommonName
     *   First name + space ( ) + last name. If a collision occurs, a number suffix
     *   is added to the end (see suffixes below).
     *
     * Normalization
     *   All names are normalized to fit the different use cases. This include
     *   removing diacritics (SamAccountName and UserPrincipalName), removing
     *   characters that can not be used in Active Directory and mapping letters
     *   from other alphabets to letters in the english alphabet (this only maps
     *   a small subset of letters that covers our use cases).
     *
     * Suffixes
     *   Number suffixes are used to avoid collisions. This is the algorithm used:
     *     1. Active Directory is searched for the new name. Both
     *        UserPrincipalName and ProxyAddresses are searched for the same
     *        name. This guarantees that the UserPrincipalName can be used as the
     *        primary SMTP address.
     *     2. If the name is found and it has a suffix, the number suffix is stored
     *        in a list (or -1 if there is no suffix)
     *     3. When the new name is constructed, the algorithm looks at the list
     *        and selects the first available suffix starting from -1 (no suffix)
     *        and incrementing by 1 until it finds a free suffix. The suffix 1 is
     *        skipped over (first account has no suffix, second account gets the
     *        suffix 2).
     *     4. Add suffix to UPN, CN and SAM if needed. UPN only gets a suffix if
     *        there is an UPN collision. CN and SAM always gets a suffix if one
     *        of the names (UPN, CN, SAM) needs a suffix. The same suffix is added
     *        to all names.
     *        
     *   Collisions are more likely to occur for SamAccountNames compared to
     *   UserPrincipalNames. That is why the algorithm tries to avoid adding
     *   an unneccesary suffix to the UserPrincipalName (which is also the
     *   primary SMTP address).
     *   
     *   Important to note is that suffixes are cached to minimize searches in
     *   Active Directory when creating users in bulk. This introduces a risk
     *   for collisions when creating multiple accounts with the same factory
     *   object.
     */

    public class AccountNames
    {
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string SamAccountName { get; private set; }
        public string UserPrincipalName { get; private set; }
        public string CommonName { get; private set; }
        public string DisplayName { get; private set; }
        public AccountNames(string firstName, string lastName, string samAccountName, string userPrincipalName, string commonName, string displayName)
        {
            FirstName = firstName;
            LastName = lastName;
            SamAccountName = samAccountName;
            UserPrincipalName = userPrincipalName;
            CommonName = commonName;
            DisplayName = displayName;
        }
    }
    public class AccountNamesFactory
    {
        readonly Dictionary<string, List<int>> suffixCache;
        static CultureInfo swedishCulture = CultureInfo.GetCultureInfo("sv-SE");

        public AccountNamesFactory()
        {
            suffixCache = new Dictionary<string, List<int>>();
        }

        public static string GetName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            return name.Trim().RemoveRepeating(new char[] { ' ' });
        }

        public static string GetSamAccountName(string firstName, string lastName)
        {
            return GetSamAccountName(firstName, lastName, null);
        }

        public static string GetSamAccountName(string firstName, string lastName, string employeeNumber)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                throw new ArgumentException("firstName and lastName can not be null, empty or contain only whitespace.");
            }
            string clean(string str)
            {
                str = Regex.Replace(
                    str
                        .Trim()
                        .RemoveDiacritic()
                        .ToLower(swedishCulture)
                        .Replace('ø', 'o')
                        .Replace('æ', 'a'),
                    "[^a-z]", "");
                return str.Substring(0, Math.Min(3, str.Length));
            }
            string sam = clean(firstName) + clean(lastName);
            if (!string.IsNullOrEmpty(employeeNumber) && employeeNumber.Length > 3)
            {
                sam = employeeNumber.Substring(2, 2) + sam;
            }
            return sam;
        }

        public static string GetUpnNamePart(string firstName, string lastName)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                throw new ArgumentException("firstName and lastName can not be null, empty or contain only whitespace.");
            }
            string clean(string str)
            {
                return Regex.Replace(
                    str
                        .Trim()
                        .RemoveDiacritic()
                        .RemoveRepeating(new char[] { ' ', '-' })
                        .ToLower(swedishCulture)
                        .Replace('ø', 'o')
                        .Replace('æ', 'a')
                        .Replace(' ', '-'),
                    "[^a-z-]", "");
            }
            return clean(firstName) + "." + clean(lastName);
        }

        public static string GetCommonName(string firstName, string lastName)
        {
            // Remove characters that can not be part of a Distinguished Name in Active Directory
            return Regex.Replace(GetName(firstName) + " " + GetName(lastName), "[,+\"\\<>;\r\n=/]", "");
        }

        public static string GetDisplayName(string firstName, string lastName)
        {
            return GetName(firstName) + " " + GetName(lastName);
        }

        public void CacheSuffix(string key, int suffix)
        {
            if (suffixCache.TryGetValue(key, out List<int> list))
            {
                if (!list.Contains(suffix))
                {
                    list.Add(suffix);
                    list.Sort();
                }
            }
            else
            {
                suffixCache.Add(key, new List<int> { suffix });
            }
        }

        public void ClearSuffixCache()
        {
            suffixCache.Clear();
        }

        public int GetNextAvailableSuffix(string key)
        {
            return GetNextAvailableSuffix(key, -1);
        }

        public int GetNextAvailableSuffix(string key, int min)
        {
            if (min < 2)
            {
                min = -1;
            }
            if (!suffixCache.TryGetValue(key, out List<int> list))
            {
                return min;
            }
            if (list[0] > min)
            {
                return min;
            }
            if (min < 2)
            {
                min = 2;
            }
            if (list.Count == 1)
            {
                if (list[0] < min)
                {
                    return min;
                }
                return min + 1;
            }
            for (int i = 1; i < list.Count; i++)
            {
                int s2 = list[i] < 1 ? 1 : list[i];
                if (s2 <= min)
                {
                    continue;
                }
                int s1 = list[i - 1] < 1 ? 1 : list[i - 1];
                if (s2 - s1 > 1)
                {
                    if (s1 >= min)
                    {
                        return s1 + 1;
                    }
                    return min;
                }
            }
            int last = list[list.Count - 1];
            if (last == min)
            {
                return min + 1;
            }
            if (last > min)
            {
                return last + 1;
            }
            return min;
        }

        public AccountNames GetNames(string firstName, string lastName, string upnDomain)
        {
            return GetNames(firstName,lastName, upnDomain, 
                employeeNumber: null,
                excludeSam: false, 
                samPrefix: null,
                linkUpnAndSam: false
            );
        }

        public AccountNames GetNames(string firstName, string lastName, string upnDomain, string employeeNumber)
        {
            return GetNames(firstName, lastName, upnDomain, employeeNumber,
                excludeSam: false,
                samPrefix: null,
                linkUpnAndSam: false
            );
        }

        public AccountNames GetNames(string firstName, string lastName, string upnDomain, bool excludeSam)
        {
            return GetNames(firstName, lastName, upnDomain,
                employeeNumber: null,
                excludeSam: excludeSam,
                samPrefix: null,
                linkUpnAndSam: false
            );
        }

        public AccountNames GetNames(string firstName, string lastName, string upnDomain, string employeeNumber, bool excludeSam)
        {
            return GetNames(firstName, lastName, upnDomain, employeeNumber, excludeSam,
                samPrefix: null,
                linkUpnAndSam: false
            );
        }

        public AccountNames GetNames(string firstName, string lastName, string upnDomain, string employeeNumber, bool excludeSam, string samPrefix, bool linkUpnAndSam)
        {
            upnDomain = upnDomain.TrimStart('@');
            string sam = GetSamAccountName(firstName, lastName, employeeNumber);
            if (!string.IsNullOrEmpty(samPrefix))
            {
                sam = samPrefix + sam;
            }
            string upn = sam;
            if (!linkUpnAndSam)
            {
                upn = GetUpnNamePart(firstName, lastName);
            }
            string cn = GetCommonName(firstName, lastName);
            string cnWithoutDiacritics = cn.RemoveDiacritic();
            if (!excludeSam)
            {
                if (!suffixCache.ContainsKey(sam))
                {
                    foreach (ADUser user in DSFactory.SearchUser(UserSearchProperty.SamAccountName, Invariant($"{sam}*")))
                    {
                        string foundSam = user.SamAccountName;
                        string foundSamNoSuffix = foundSam.TrimEnd(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
                        if (foundSamNoSuffix.Equals(sam, StringComparison.OrdinalIgnoreCase))
                        {
                            CacheSuffix(foundSamNoSuffix, foundSam.GetNumberSuffix());
                        }
                        user.Dispose();
                    }
                }
            }
            if (!suffixCache.ContainsKey(upn))
            {
                foreach(ADUser user in DSFactory.SearchUser(UserSearchProperty.UserPrincipalName, Invariant($"{upn}*@{upnDomain}")))
                {
                    string foundUpn = user.UserPrincipalName.Split('@')[0];
                    string foundUpnNoSuffix = foundUpn.TrimEnd(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
                    if (foundUpnNoSuffix.Equals(upn, StringComparison.OrdinalIgnoreCase))
                    {
                        CacheSuffix(foundUpnNoSuffix, foundUpn.GetNumberSuffix());
                    }
                    user.Dispose();
                }
                foreach (ADUser user in DSFactory.SearchUser(UserSearchProperty.ProxyAddresses, Invariant($"SMTP:{upn}*@{upnDomain}")))
                {
                    foreach (string address in user.ProxyAddresses)
                    {
                        if (address.StartsWith("SMTP:", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] parts = address.Substring(5).Split('@');
                            string name = parts[0];
                            string domain = parts[1];
                            string nameTrimmed = name.TrimEnd(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
                            if (nameTrimmed.Equals(upn, StringComparison.OrdinalIgnoreCase)
                                && domain.Equals(upnDomain, StringComparison.OrdinalIgnoreCase))
                            {
                                CacheSuffix(nameTrimmed, name.GetNumberSuffix());
                            }
                        }
                    }
                    user.Dispose();
                }
            }
            if (!suffixCache.ContainsKey(cnWithoutDiacritics))
            {
                foreach (ADUser user in DSFactory.SearchUser(UserSearchProperty.CommonName, Invariant($"{cn}*")))
                {
                    // You can not have two accounts with common names that only differ by diactitics.
                    // AD considers the common names "Anders Öst" and "Anders Ost" as equal. That is why
                    // diacritics has to be removed before comparison.
                    string foundCn = user.Name.RemoveDiacritic();
                    string foundCnNoSuffix = foundCn.TrimEnd(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
                    if (foundCnNoSuffix.Equals(cnWithoutDiacritics, StringComparison.OrdinalIgnoreCase))
                    {
                        CacheSuffix(cnWithoutDiacritics, foundCn.GetNumberSuffix());
                    }
                    user.Dispose();
                }
            }
            AccountNames accountNames;
            int suffix = GetNextAvailableSuffix(upn);
            int initialUpnSuffix = suffix;
            if (excludeSam)
            {
                int suffix2 = GetNextAvailableSuffix(cnWithoutDiacritics);
                while (suffix != suffix2)
                {
                    int max = Math.Max(suffix, suffix2);
                    suffix = GetNextAvailableSuffix(upn, max);
                    suffix2 = GetNextAvailableSuffix(cnWithoutDiacritics, max);
                }
                int upnSuffix = suffix;
                if (suffix > -1)
                {
                    // If no suffix was needed for UPN going in, we don't add one
                    if (initialUpnSuffix > -1)
                    {
                        upn += suffix;
                    }
                    else
                    {
                        upnSuffix = -1;
                    }
                    cn += suffix;
                }
                CacheSuffix(upn, upnSuffix);
                CacheSuffix(cnWithoutDiacritics, suffix);
                accountNames = new AccountNames(
                    GetName(firstName),
                    GetName(lastName),
                    string.Empty,
                    Invariant($"{upn}@{upnDomain}"),
                    cn,
                    GetDisplayName(firstName, lastName)
                );
            }
            else
            {
                int suffix2 = GetNextAvailableSuffix(cnWithoutDiacritics);
                int suffix3 = GetNextAvailableSuffix(sam);
                while (suffix != suffix2 || suffix2 != suffix3)
                {
                    int max = Math.Max(suffix, Math.Max(suffix2, suffix3));
                    suffix = GetNextAvailableSuffix(upn, max);
                    suffix2 = GetNextAvailableSuffix(cnWithoutDiacritics, max);
                    suffix3 = GetNextAvailableSuffix(sam, max);
                }
                int upnSuffix = suffix;
                if (suffix > -1)
                {
                    // If no suffix was needed for UPN going in, we don't add one
                    if (initialUpnSuffix > -1)
                    {
                        upn += suffix;
                    }
                    else
                    {
                        upnSuffix = -1;
                    }
                    cn += suffix;
                    sam += suffix;
                }
                CacheSuffix(upn, upnSuffix);
                CacheSuffix(cnWithoutDiacritics, suffix);
                CacheSuffix(sam, suffix);
                accountNames = new AccountNames(
                    GetName(firstName),
                    GetName(lastName),
                    sam,
                    Invariant($"{upn}@{upnDomain}"),
                    cn,
                    GetDisplayName(firstName, lastName)
                );
            }
            return accountNames;
        }
    }
}
