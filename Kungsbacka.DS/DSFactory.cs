using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Security.Principal;
using Newtonsoft.Json;

namespace Kungsbacka.DS
{
    public enum UserSearchProperty
    {
        SamAccountName,
        UserPrincipalName,
        CommonName,
        EmailAddress,
        ProxyAddresses,
        AmbiguousNameResolution,
        EmployeeNumber,
        SeeAlso,
        Organization
    }

    public enum GroupSearchProperty
    {
        DisplayName,
        Location
    }

    public static class DSFactory
    {
        private static PrincipalContext principalContext;
        // private static DirectoryEntry


        public static PrincipalContext PrincipalContext
        {
            get
            {
                if (principalContext == null)
                {
                    principalContext = CreatePrincipalContext();
                }
                return principalContext;
            }
            set
            {
                if (principalContext != null)
                {
                    principalContext.Dispose();
                }
                principalContext = value;
            }
        }

        public static PrincipalContext CreatePrincipalContext()
        {
            return new PrincipalContext(ContextType.Domain);
        }

        public static PrincipalContext CreatePrincipalContext(string container)
        {
            return new PrincipalContext(ContextType.Domain, null, container);
        }

        public static PrincipalContext CreatePrincipalContext(string userName, string password)
        {
            return new PrincipalContext(ContextType.Domain, null, userName, password);
        }

        public static IList<ADUser> SearchUser(UserSearchProperty searchProperty, string searchString)
        {
            List<ADUser> list = new List<ADUser>();
            using (ADUser qbePrincipal = new ADUser(PrincipalContext))
            {
                // Filter out unwanted objects like computers
                qbePrincipal.ObjectCategory = ADSchema.GetSchemaClassDistinguishedName("person");
                switch (searchProperty)
                {
                    case UserSearchProperty.AmbiguousNameResolution:
                        qbePrincipal.AmbiguousNameResolution = searchString;
                        break;
                    case UserSearchProperty.CommonName:
                        qbePrincipal.Name = searchString;
                        break;
                    case UserSearchProperty.EmailAddress:
                        qbePrincipal.EmailAddress = searchString;
                        break;
                    case UserSearchProperty.ProxyAddresses:
                        qbePrincipal.ProxyAddresses = new string[] { searchString };
                        break;
                    case UserSearchProperty.SamAccountName:
                        qbePrincipal.SamAccountName = searchString;
                        break;
                    case UserSearchProperty.UserPrincipalName:
                        qbePrincipal.UserPrincipalName = searchString;
                        break;
                    case UserSearchProperty.EmployeeNumber:
                        qbePrincipal.EmployeeNumber = searchString;
                        break;
                    case UserSearchProperty.SeeAlso:
                        qbePrincipal.SeeAlso = searchString;
                        break;
                    case UserSearchProperty.Organization:
                        FillOrgSearchQbePrincipal(qbePrincipal, searchString);
                        break;
                    default:
                        throw new NotImplementedException(searchProperty.ToString());
                }
                using (PrincipalSearcher principalSearcher = new PrincipalSearcher(qbePrincipal))
                {
                    list.AddRange(principalSearcher.FindAll().Cast<ADUser>());
                }
            }
            return list;
        }

        private static void FillOrgSearchQbePrincipal(ADUser principal, string searchString)
        {
            if (string.IsNullOrEmpty(searchString))
            {
                throw new ArgumentNullException(nameof(searchString));
            }
            string[] parts = searchString.Split('/');
            if (parts.Length < 1 || parts.Length > 4)
            {
                throw new ArgumentException(nameof(searchString), "Not a valid organizational search string");
            }
            parts = parts.Select(p => p.Trim()).ToArray();
            if (!string.IsNullOrEmpty(parts[1]))
            {
                principal.Department = parts[1];
            }
            if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
            {
                principal.Office = parts[2];
            }
            if (parts.Length > 3 && !string.IsNullOrEmpty(parts[3]))
            {
                principal.Title = parts[3];
            }
        }

        public static IList<ADGroup> SearchGroup(GroupSearchProperty searchProperty, string searchString)
        {
            List<ADGroup> list = new List<ADGroup>();
            using (ADGroup qbeGroup = new ADGroup(PrincipalContext))
            {
                switch (searchProperty)
                {
                    case GroupSearchProperty.DisplayName:
                        qbeGroup.DisplayName = searchString;
                        break;
                    case GroupSearchProperty.Location:
                        qbeGroup.Location = searchString;
                        break;
                    default:
                        throw new NotImplementedException(searchProperty.ToString());
                }
                using (PrincipalSearcher principalSearcher = new PrincipalSearcher(qbeGroup))
                {
                    list.AddRange(principalSearcher.FindAll().Cast<ADGroup>());
                }
            }
            return list;
        }

        private static ADLicenseGroup LicenseGroupFromADGroup(ADGroup adGroup)
        {
            string json = adGroup.Location.Substring(8);
            ADLicenseGroup licenseGroup = JsonConvert.DeserializeObject<ADLicenseGroup>(json);
            licenseGroup.Guid = (Guid)adGroup.Guid;
            licenseGroup.DistinguishedName = adGroup.DistinguishedName;
            licenseGroup.DisplayName = adGroup.DisplayName;
            return licenseGroup;
        }

        public static IList<ADLicenseGroup> GetLicenseGroups(bool standardOnly)
        {
            IList<ADGroup> searchResult = SearchGroup(GroupSearchProperty.Location, "license:*");
            if (standardOnly)
            {
                return searchResult.Select(g => LicenseGroupFromADGroup(g)).Where(g => g.Standard && !g.Dynamic).ToList();
            }
            return searchResult.Select(g => LicenseGroupFromADGroup(g)).Where(g => !g.Dynamic).ToList();
        }

        public static IList<ADLicenseGroup> GetLicenseGroups()
        {
            return GetLicenseGroups(false);
        }

        public static ADUser FindUserByDistinguishedName(string distinguishedName)
        {
            return ADUser.FindByIdentity(PrincipalContext, IdentityType.DistinguishedName, distinguishedName);
        }

        public static ADUser FindUserBySamAccountName(string samAccountName)
        {
            return ADUser.FindByIdentity(PrincipalContext, IdentityType.SamAccountName, samAccountName);
        }

        public static ADUser FindUserBySid(SecurityIdentifier sid)
        {
            return ADUser.FindByIdentity(PrincipalContext, IdentityType.Sid, sid.Value);
        }

        public static ADGroup FindGroupByDistinguishedName(string distinguishedName)
        {
            return ADGroup.FindByIdentity(PrincipalContext, IdentityType.DistinguishedName, distinguishedName);
        }
    }
}
