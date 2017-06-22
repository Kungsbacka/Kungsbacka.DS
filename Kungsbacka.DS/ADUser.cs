﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Kungsbacka.DS
{
    [Flags]
    public enum AccountProcessingRules
    {
        None = 0,
        SourcedFromPersonec = 1,
        SourcedFromProcapita = 2
    };

    [DirectoryRdnPrefix("CN")]
    [DirectoryObjectClass("user")]
    public class ADUser : UserPrincipal
    {
        bool objectCategoryChanged;
        PropertyValueCollection allowedAttributesEffective;

        public ADUser(PrincipalContext context) : base(context) { }

        public ADUser(PrincipalContext context, string samAccountName, string password, bool enabled) :
            base(context, samAccountName, password, enabled) { }

        public static new ADUser FindByIdentity(PrincipalContext context, IdentityType identityType, string identityValue)
        {
            return (ADUser)FindByIdentityWithType(context, typeof(ADUser), identityType, identityValue);
        }

        public new void Save()
        {
            if (objectCategoryChanged)
            {
                throw new InvalidOperationException("Cannot save object when property ObjectCategory has changed.");
            }
            base.Save();
        }

        public bool CanWriteAttibute(string attribute)
        {
            if (allowedAttributesEffective == null)
            {
                DirectoryEntry de = (DirectoryEntry)GetUnderlyingObject();
                de.RefreshCache(new string[] { "allowedAttributesEffective" });
                allowedAttributesEffective = de.Properties["allowedAttributesEffective"];
            }
            return allowedAttributesEffective.Contains(attribute);
        }

        // This assumes a lot about the environment:
        // - Most attributes that we are interested in are readable by normal users
        // - Permission to read a confidential attributes is assigned to groups only
        public bool CanReadAttribute(string attribute)
        {
            if (Schema.IsAttributeConfidential(attribute))
            {
                // Domain Admin?
                using (var currentUser = DSFactory.FindUserBySid(WindowsIdentity.GetCurrent().User))
                {
                    var domainAdminsSid = new SecurityIdentifier(WellKnownSidType.AccountDomainAdminsSid, currentUser.Sid.AccountDomainSid);
                    if (currentUser.IsMemberOf(currentUser.Context, IdentityType.Sid, domainAdminsSid.Value))
                    {
                        return true;
                    }
                }
                // Member of a group that grants access?
                var userSchemaGuid = Schema.GetClassSchemaGuid("user");
                var attributeSchemaGuid = Schema.GetAttributeSchemaGuid(attribute);
                var groups = WindowsIdentity.GetCurrent().Groups;
                var de = (DirectoryEntry)GetUnderlyingObject();
                var acl = de.ObjectSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier));
                foreach (ActiveDirectoryAccessRule ace in acl)
                {
                    if (ace.ActiveDirectoryRights == ActiveDirectoryRights.ExtendedRight && ace.ObjectType == attributeSchemaGuid && ace.InheritedObjectType == userSchemaGuid)
                    {
                        if (groups.Contains(ace.IdentityReference))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            return true;
        }

        public new void Delete()
        {
            CheckDisposedOrDeleted();
            var directoryEntry = (DirectoryEntry)GetUnderlyingObject();
            directoryEntry.DeleteTree();
            Dispose();
        }

        public new string ToString()
        {
            return DistinguishedName;
        }

        public new DateTime? AccountExpirationDate
        {
            get
            {
                return base.AccountExpirationDate?.ToLocalTime();
            }
            set
            {
                base.AccountExpirationDate = value?.ToUniversalTime();
            }
        }

        public bool AccountHasExpired
        {
            get
            {
                if (AccountExpirationDate == null)
                {
                    return false;
                }
                return AccountExpirationDate < DateTime.Now;
            }
        }

        [DirectoryProperty("homeMDB")]
        [DirectoryProperty("mailNickname")]
        public bool AccountIsMailEnabled
        {
            get
            {
                return ExtensionGet("homeMDB").Length == 1 && ExtensionGet("mailNickname").Length == 1;
            }
        }

        [DirectoryProperty("msRTCSIP-UserEnabled")]
        public bool AccountIsSipEnabled
        {
            get
            {
                object[] values = ExtensionGet("msRTCSIP-UserEnabled");
                if (values.Length != 1 || null == values[0])
                {
                    return false;
                }
                return (bool)values[0];
            }
        }

        public new DateTime? AccountLockoutTime
        {
            get
            {
                return base.AccountLockoutTime?.ToLocalTime();
            }
        }

        public string AccountLocation
        {
            get
            {
                string dn = DistinguishedName;
                int pos = dn.IndexOf(",OU=", StringComparison.OrdinalIgnoreCase);
                dn = dn.Substring(pos + 1, dn.Length - pos - 17);
                int start = dn.Length - 1;
                pos = -1;
                string location = "";
                do
                {
                    pos = dn.LastIndexOf(",OU=", start, StringComparison.OrdinalIgnoreCase);
                    location += dn.Substring(pos + 4, start - pos - 3) + "/";
                    start = pos - 1;
                }
                while (pos > -1);
                return location.TrimEnd('/');
            }
        }

        [DirectoryProperty("extensionAttribute15")]
        public string AccountType
        {
            get
            {
                object[] values = ExtensionGet("extensionAttribute15");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set
            {
                switch (value)
                {
                    case "personal":
                    case "personal-fg":
                    case "personal-gv":
                    case "personal-ex":
                    case "elev-gv":
                    case "elev-fg":
                    case "elev-ex":
                    case "mailbox":
                    case "extern":
                    case "leverantör":
                        ExtensionSet("extensionAttribute15", value);
                        break;
                    default:
                        throw new ArgumentException("Unknown account type");
                }
            }
        }

        [DirectoryProperty("gidNumber")]
        public int? AccountProcessingRules
        {
            get
            {
                object[] values = ExtensionGet("gidNumber");
                if (values.Length != 1)
                {
                    return null;
                }
                return (int?)values[0];
            }
            set
            {
                if (null == value || value < 1)
                {
                    ExtensionSet("gidNumber", null);
                }
                else
                {
                    ExtensionSet("gidNumber", value);
                }
            }
        }

        [DirectoryProperty("anr")]
        public string AmbiguousNameResolution
        {
            get
            {
                object[] values = ExtensionGet("anr");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set
            {
                ExtensionSet("anr", value);
            }
        }

        public int DaysBeforeNextPasswordChange
        {
            get
            {
                if (null == LastPasswordSet)
                {
                    return 0;
                }
                int days = 180 - (DateTime.Now - (DateTime)LastPasswordSet).Days;
                return days > 0 ? days : 0;
            }
        }

        [DirectoryProperty("department")]
        public string Department
        {
            get
            {
                object[] values = ExtensionGet("department");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set
            {
                ExtensionSet("department", value);
            }
        }

        [DirectoryProperty("employeeNumber")]
        public string EmployeeNumber
        {
            get
            {
                object[] values = ExtensionGet("employeeNumber");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set
            {
                ExtensionSet("employeeNumber", value);
            }
        }

        [DirectoryProperty("initials")]
        public string Initials
        {
            get
            {
                object[] values = ExtensionGet("initials");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set
            {
                ExtensionSet("initials", value);
            }
        }

        [DirectoryProperty("whenCreated")]
        public DateTime WhenCreated
        {
            get
            {
                return (DateTime)ExtensionGet("whenCreated")[0];
            }
        }

        [DirectoryProperty("homeMDB")]
        public string ExchangeDatabase
        {
            get
            {
                object[] values = ExtensionGet("homeMDB");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set
            {
                ExtensionSet("homeMDB", value);
            }
        }

        [DirectoryProperty("mDBStorageQuota")]
        public int? IssueWarningQuota
        {
            get
            {
                object[] values = ExtensionGet("mDBStorageQuota");
                if (values.Length != 1)
                {
                    return null;
                }
                return (int?)values[0];
            }
            set
            {
                ExtensionSet("mDBStorageQuota", value);
            }
        }

        [DirectoryProperty("manager")]
        public string Manager
        {
            get
            {
                object[] values = ExtensionGet("manager");
                if (values.Length != 1)
                {
                    return null;
                }
                var value = (string)values[0];
                if (value.IndexOf(",CN=Deleted Objects,DC=", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return null;
                }
                return value;
            }
            set
            {
                ExtensionSet("manager", value);
            }
        }

        public new DateTime? LastPasswordSet
        {
            get
            {
                return base.LastPasswordSet?.ToLocalTime();
            }
        }

        [DirectoryProperty("memberOf")]
        public object[] MemberOf
        {
            get
            {
                object[] values = ExtensionGet("memberOf");
                if (values.Length == 0)
                {
                    return null;
                }
                return values;
            }
        }

        [DirectoryProperty("mobile")]
        public string MobilePhone
        {
            get
            {
                object[] values = ExtensionGet("mobile");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set
            {
                ExtensionSet("mobile", value);
            }
        }

        [DirectoryProperty("objectCategory")]
        public string ObjectCategory
        {
            get
            {
                return (string)ExtensionGet("objectCategory")[0];
            }
            set
            {
                ExtensionSet("objectCategory", value);
                objectCategoryChanged = true;
            }
        }

        [DirectoryProperty("physicalDeliveryOfficeName")]
        public string Office
        {
            get
            {
                object[] values = ExtensionGet("physicalDeliveryOfficeName");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set
            {
                ExtensionSet("physicalDeliveryOfficeName", value);
            }
        }

        [DirectoryProperty("mDBOverQuotaLimit")]
        public int? ProhibitSendQuota
        {
            get
            {
                object[] values = ExtensionGet("mDBOverQuotaLimit");
                if (values.Length != 1)
                {
                    return null;
                }
                return (int?)values[0];
            }
            set
            {
                ExtensionSet("mDBOverQuotaLimit", value);
            }
        }

        [DirectoryProperty("mDBOverHardQuotaLimit")]
        public int? ProhibitSendReceiveQuota
        {
            get
            {
                object[] values = ExtensionGet("mDBOverHardQuotaLimit");
                if (values.Length != 1)
                {
                    return null;
                }
                return (int?)values[0];
            }
            set
            {
                ExtensionSet("mDBOverHardQuotaLimit", value);
            }
        }

        [DirectoryProperty("proxyAddresses")]
        public object[] ProxyAddresses
        {
            get
            {
                object[] values = ExtensionGet("proxyAddresses");
                if (values.Length == 0)
                {
                    return null;
                }
                return values;
            }
            set
            {
                ExtensionSet("proxyAddresses", value);
            }
        }

        [DirectoryProperty("carLicense")]
        public string ResourceManagerTasks
        {
            get
            {
                object[] values = ExtensionGet("carLicense");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set
            {
                ExtensionSet("carLicense", value);
            }
        }

        [DirectoryProperty("extensionAttribute14")]
        public string SamlId
        {
            get
            {
                object[] values = ExtensionGet("extensionAttribute14");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set
            {
                ExtensionSet("extensionAttribute14", value);
            }
        }

        [DirectoryProperty("extensionAttribute13")]
        public string SchoolUnitCode
        {
            get
            {
                object[] values = ExtensionGet("extensionAttribute13");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set
            {
                ExtensionSet("extensionAttribute13", value);
            }
        }

        [DirectoryProperty("seeAlso")]
        public string SeeAlso
        {
            get
            {
                object[] values = ExtensionGet("seeAlso");
                if (values.Length != 1)
                {
                    return null;
                }
                var value = (string)values[0];
                if (value.IndexOf(",CN=Deleted Objects,DC=", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return null;
                }
                return value;
            }
            set
            {
                ExtensionSet("seeAlso", value);
            }
        }

        [DirectoryProperty("extensionAttribute11")]
        public bool Synchronized
        {
            get
            {
                object[] values = ExtensionGet("extensionAttribute11");
                return values.Length > 0 && null != values[0];
            }
            set
            {
                if (value)
                {
                    ExtensionSet("extensionAttribute11", "SYNC_ME");
                }
                else
                {
                    ExtensionSet("extensionAttribute11", null);
                }
            }
        }

        [DirectoryProperty("targetAddress")]
        public string TargetAddress
        {
            get
            {
                object[] values = ExtensionGet("targetAddress");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set
            {
                ExtensionSet("targetAddress", value);
            }
        }

        [DirectoryProperty("telephoneNumber")]
        public string TelephoneNumber
        {
            get
            {
                object[] values = ExtensionGet("telephoneNumber");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set
            {
                ExtensionSet("telephoneNumber", value);
            }
        }

        [DirectoryProperty("title")]
        public string Title
        {
            get
            {
                object[] values = ExtensionGet("title");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set
            {
                ExtensionSet("title", value);
            }
        }

        [DirectoryProperty("mDBUseDefaults")]
        public bool UseDatabaseQuotaDefaults
        {
            get
            {
                object[] values = ExtensionGet("mDBUseDefaults");
                return values.Length == 1 && (bool)values[0];
            }
            set
            {
                ExtensionSet("mDBUseDefaults", value);
            }
        }
    }
}
