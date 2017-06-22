using System;
using System.Linq;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;

namespace Kungsbacka.DS
{
    public enum SearchProperty
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

    public static class DSFactory
    {
        static PrincipalContext principalContext;

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

        public static IList<ADUser> SearchUser(SearchProperty attribute, string value)
        {
            var list = new List<ADUser>();
            using (ADUser qbePrincipal = new ADUser(DefaultContext))
            {
                // Filter out unwanted objects like computers
                qbePrincipal.ObjectCategory = Schema.GetSchemaClassDistinguishedName("person");
                switch (attribute)
                {
                    case SearchProperty.AmbiguousNameResolution:
                        qbePrincipal.AmbiguousNameResolution = value;
                        break;
                    case SearchProperty.CommonName:
                        qbePrincipal.Name = value;
                        break;
                    case SearchProperty.EmailAddress:
                        qbePrincipal.EmailAddress = value;
                        break;
                    case SearchProperty.ProxyAddresses:
                        qbePrincipal.ProxyAddresses = new string[] { value };
                        break;
                    case SearchProperty.SamAccountName:
                        qbePrincipal.SamAccountName = value;
                        break;
                    case SearchProperty.UserPrincipalName:
                        qbePrincipal.UserPrincipalName = value;
                        break;
                    case SearchProperty.EmployeeNumber:
                        qbePrincipal.EmployeeNumber = value;
                        break;
                    case SearchProperty.SeeAlso:
                        qbePrincipal.SeeAlso = value;
                        break;
                    default:
                        throw new NotImplementedException(attribute.ToString());
                }
                using (var principalSearcher = new PrincipalSearcher(qbePrincipal))
                {
                    list.AddRange(principalSearcher.FindAll().Cast<ADUser>());
                }
            }
            return list;
        }

        public static ADUser FindUserByDistinguishedName(string distinguishedName)
        {
            return ADUser.FindByIdentity(DefaultContext, IdentityType.DistinguishedName, distinguishedName);
        }

        public static ADUser FindUserBySamAccountName(string samAccountName)
        { 
            return ADUser.FindByIdentity(DefaultContext, IdentityType.SamAccountName, samAccountName);
        }

        public static ADUser FindUserBySid(SecurityIdentifier sid)
        {
            return ADUser.FindByIdentity(DefaultContext, IdentityType.Sid, sid.Value);
        }

        public static GroupPrincipal FindGroupByDistinguishedName(string distinguishedName)
        {
            return GroupPrincipal.FindByIdentity(DefaultContext, IdentityType.DistinguishedName, distinguishedName);
        }
    }
}
