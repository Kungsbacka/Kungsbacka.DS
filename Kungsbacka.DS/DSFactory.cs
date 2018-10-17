using System;
using System.Linq;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;

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
        SeeAlso
    }

    public enum GroupSearchProperty
    {
        DisplayName,
        Location
    }

    public static class DSFactory
    {
        private static PrincipalContext principalContext;

        public static PrincipalContext DefaultContext
        {
            get
            {
                if (null == principalContext)
                {
                    principalContext = CreatePrincipalContext();
                }
                return principalContext;
            }
        }

        public static PrincipalContext CreatePrincipalContext() =>
            new PrincipalContext(ContextType.Domain);

        public static PrincipalContext CreatePrincipalContext(string container) =>
            new PrincipalContext(ContextType.Domain, null, container);

        public static PrincipalContext CreatePrincipalContext(string userName, string password) =>
            new PrincipalContext(ContextType.Domain, null, userName, password);

        public static IList<ADUser> SearchUser(UserSearchProperty searchProperty, string searchString)
        {
            var list = new List<ADUser>();
            using (ADUser qbePrincipal = new ADUser(DefaultContext))
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
                    default:
                        throw new NotImplementedException(searchProperty.ToString());
                }
                using (var principalSearcher = new PrincipalSearcher(qbePrincipal))
                {
                    list.AddRange(principalSearcher.FindAll().Cast<ADUser>());
                }
            }
            return list;
        }

        public static IList<ADGroup> SearchGroup(GroupSearchProperty searchProperty, string searchString)
        {
            var list = new List<ADGroup>();
            using (ADGroup qbeGroup = new ADGroup(DefaultContext))
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
                using (var principalSearcher = new PrincipalSearcher(qbeGroup))
                {
                    list.AddRange(principalSearcher.FindAll().Cast<ADGroup>());
                }
            }
            return list;
        }

        private static ADLicenseGroup LicenseGroupFromADGroup(ADGroup adGroup)
        {
            string json = adGroup.Location.Substring(8);
            var jsonSerializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            var licenseGroup = jsonSerializer.Deserialize<ADLicenseGroup>(json);
            licenseGroup.Guid = (Guid)adGroup.Guid;
            licenseGroup.DistinguishedName = adGroup.DistinguishedName;
            licenseGroup.DisplayName = adGroup.DisplayName;
            return licenseGroup;
        }

        public static IList<ADLicenseGroup> GetLicenseGroups(bool standardOnly)
        {
            var searchResult = SearchGroup(GroupSearchProperty.Location, "license:*");
            if (standardOnly)
            {
                return searchResult.Select(g => LicenseGroupFromADGroup(g)).Where(g => g.Standard && !g.Dynamic).ToList();
            }
            return searchResult.Select(g => LicenseGroupFromADGroup(g)).Where(g => !g.Dynamic).ToList();
        }

        public static IList<ADLicenseGroup> GetLicenseGroups() => GetLicenseGroups(false);

        public static ADUser FindUserByDistinguishedName(string distinguishedName) =>
            ADUser.FindByIdentity(DefaultContext, IdentityType.DistinguishedName, distinguishedName);

        public static ADUser FindUserBySamAccountName(string samAccountName) =>
            ADUser.FindByIdentity(DefaultContext, IdentityType.SamAccountName, samAccountName);

        public static ADUser FindUserBySid(SecurityIdentifier sid) => 
            ADUser.FindByIdentity(DefaultContext, IdentityType.Sid, sid.Value);

        public static ADGroup FindGroupByDistinguishedName(string distinguishedName) =>
            ADGroup.FindByIdentity(DefaultContext, IdentityType.DistinguishedName, distinguishedName);
    }
}
