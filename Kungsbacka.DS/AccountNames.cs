using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static System.FormattableString;
using System.Globalization;
using Kungsbacka.CommonExtensions;

namespace Kungsbacka.DS
{
    /* Algorithms for creating account names:
     * SamAccountName
     *   First three letters from the first name plus the first three letters from
     *   the last name. If a collistion occurs, add a number suffix to the end (se
     *   suffixes below). If personnmmer is supplied, the year (w/o century) is added
     *   to the beginning.
     * 
     * UserPrincipalName
     *   First name + dot (.) + last name + at (@) + upnDomain. If a collision
     *   occurs, add a number suffix before at (@) (see suffixes below).
     *
     * CommonName
     *   First name + space + last name. If a collision occurs, a number suffix
     *   is added to the end (see suffixes below).
     *
     * Normalization
     *   All names are normalized to fit the different use cases. This include
     *   removing diacritics (SamAccountName and UserPrincipalName) and
     *   removing characters that are illegal in AD, etc. Normalization does not
     *   cover all cases, especially mapping characters to the english alphabet.
     *
     * Suffixes
     *   Number suffixes are used to avoid collisions. These rules are followed when
     *   adding a suffix.
     *     1. Active Directory is searched for the new name. Both
     *        UserPrincipalName and ProxyAddresses are searched for the same
     *        name. This guarantees that the UserPrincipalName can also be used
     *        as primary SMTP address.
     *     2. If the name is found and it has a suffix, the number suffix is stored
     *        in a list (or -1 if there is no suffix)
     *     3. When the new name is constructed, the algorithm looks at the list
     *        and selects the first available suffix starting from -1 (no suffix)
     *        and incrementing by 1 until it finds a free suffix. The suffix 1 is
     *        skipped over (first account has no suffix, second account gets 2
     *        as suffix).
     *     4. If a suffix is only required for SamAccountName, a suffix is not added
     *        to CommonName or UserPrincipalName.
     *     5. If a suffix is needed for UserPrincipalName, the same suffix
     *        is added to SamAccountName, but not to CommonName.
     *     6. If a suffix is needed for Common Name, but not for UserPrincipalName,
     *        the same suffix is added to SamAccountName
     *
     *   Collisions are more likely to occur for SamAccountNames compared to
     *   UserPrincipalNames. That is why the algorithm tries to avoid adding
     *   an unneccesary suffix to the UserPrincipalName (which is also the
     *   primary SMTP address).
     *   
     *   If you create users in bulk, make sure you instantiate AccountNamesFactory
     *   only once and use that instance for all accounts.
     *   
     *   Functions that tries to convert letters to a corresponding letter in the
     *   english alphabet are adapted to our needs and only covers a very small
     *   subset of letters (e.g. ø => o and æ => a).
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
            Func<string, string> clean = str =>
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
            };
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
            Func<string, string> clean = str =>
            {
                return Regex.Replace(
                    str
                        .Trim()
                        .RemoveDiacritic()
                        .RemoveRepeating(new char[] { ' ', '-'})
                        .ToLower(swedishCulture)
                        .Replace('ø', 'o')
                        .Replace('æ', 'a')
                        .Replace(' ', '-'),
                    "[^a-z-]", "");
            };
            return clean(firstName) + "." + clean(lastName);
        }

        public static string GetCommonName(string firstName, string lastName)
        {
            return Regex.Replace(GetName(firstName) + " " + GetName(lastName), "[,+\"\\<>;\r\n=/]", "");
        }

        public static string GetDisplayName(string firstName, string lastName)
        {
            return GetName(firstName) + " " + GetName(lastName);
        }

        public void CacheSuffix(string key, int suffix)
        {
            if (suffixCache.ContainsKey(key))
            {
                suffixCache[key].Add(suffix);
            }
            else
            {
                var list = new List<int>();
                list.Add(suffix);
                suffixCache.Add(key, list);
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
            if (!suffixCache.ContainsKey(key))
            {
                return min;
            }
            var list = suffixCache[key].Distinct().ToList();
            list.Sort();
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

                    foreach (ADUser user in DSFactory.SearchUser(SearchProperty.SamAccountName, Invariant($"{sam}*")))
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
                foreach(ADUser user in DSFactory.SearchUser(SearchProperty.UserPrincipalName, Invariant($"{upn}*@{upnDomain}")))
                {
                    string foundUpn = user.UserPrincipalName.Split('@')[0];
                    string foundUpnNoSuffix = foundUpn.TrimEnd(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
                    if (foundUpnNoSuffix.Equals(upn, StringComparison.OrdinalIgnoreCase))
                    {
                        CacheSuffix(foundUpnNoSuffix, foundUpn.GetNumberSuffix());
                    }
                    user.Dispose();
                }
                foreach (ADUser user in DSFactory.SearchUser(SearchProperty.ProxyAddresses, Invariant($"SMTP:{upn}*@{upnDomain}")))
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
                foreach (ADUser user in DSFactory.SearchUser(SearchProperty.CommonName, Invariant($"{cn}*")))
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
            int upnSuffix = GetNextAvailableSuffix(upn);
            int initialUpnSuffix = upnSuffix;
            int cnSuffix = GetNextAvailableSuffix(cnWithoutDiacritics);
            int initialCnSuffix = cnSuffix;
            AccountNames accountNames;
            if (excludeSam)
            {
                while (upnSuffix != cnSuffix)
                {
                    int max = Math.Max(upnSuffix, cnSuffix);
                    upnSuffix = GetNextAvailableSuffix(upn, max);
                    cnSuffix = GetNextAvailableSuffix(cnWithoutDiacritics, max);
                }
                if (upnSuffix > -1)
                {
                    if (initialUpnSuffix > -1)
                    {
                        upn += upnSuffix;
                        CacheSuffix(upn, upnSuffix);
                    }
                    if (initialCnSuffix > -1)
                    {
                        cn += upnSuffix;
                        CacheSuffix(cnWithoutDiacritics, upnSuffix);
                    }
                }
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
                int samSuffix = GetNextAvailableSuffix(sam);
                while (upnSuffix != cnSuffix || cnSuffix != samSuffix)
                {
                    int max = Math.Max(upnSuffix, Math.Max(cnSuffix, samSuffix));
                    upnSuffix = GetNextAvailableSuffix(upn, max);
                    cnSuffix = GetNextAvailableSuffix(cnWithoutDiacritics, max);
                    samSuffix = GetNextAvailableSuffix(sam, max);
                }
                CacheSuffix(upn, upnSuffix);
                CacheSuffix(cnWithoutDiacritics, upnSuffix);
                CacheSuffix(sam, upnSuffix);
                if (upnSuffix > -1)
                {
                    if (initialUpnSuffix > 1)
                    {
                        upn += upnSuffix;
                    }
                    if (initialCnSuffix > -1)
                    {
                        cn += upnSuffix;
                    }
                    sam += upnSuffix;
                }
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
