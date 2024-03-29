﻿using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;

namespace Kungsbacka.DS
{
    public enum AccountSource
    {
        None = 0,
        Personec = 1,
        Ereg = 2,
        Alvis = 3,
        Combine = 4
    };

    [DirectoryRdnPrefix("CN")]
    [DirectoryObjectClass("user")]
    public class ADUser : UserPrincipal
    {
        private const long REMOTE_USER_MAILBOX = 2147483648;
        private const long REMOTE_SHARED_MAILBOX = 34359738368;
        private const long CLOUD_PROVISIONED_MAILBOX = 1;
        private const long MIGRATED_MAILBOX = 4;
        private const long MIGRATED_SHARED_MAILBOX = 100;
        private const long PASSWORD_NEVER_EXPIRES = 0x7fffffffffffffff;


        private bool objectCategoryChanged;
        private PropertyValueCollection allowedAttributesEffective;

        private bool TryGetSingleValuedStringProperty(string propertyName, out string value)
        {
            value = null;
            object[] values = ExtensionGet(propertyName);
            if (values.Length == 1 && values[0] is string str)
            {
                value = str;
                return true;
            }
            return false;
        }

        private bool TryGetLargeIntegerProperty(string propertyName, out long value)
        {
            value = 0;
            object[] values = ExtensionGet(propertyName);
            if (values.Length == 1 && values[0] is IADsLargeInteger largeInt)
            {
                value = largeInt.HighPart << 32 | (uint)largeInt.LowPart;
                return true;
            }
            return false;
        }

        public ADUser(PrincipalContext context) : base(context) { }

        public ADUser(PrincipalContext context, string samAccountName, string password, bool enabled) :
            base(context, samAccountName, password, enabled)
        { }

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

        public new void Delete()
        {
            CheckDisposedOrDeleted();
            ((DirectoryEntry)GetUnderlyingObject()).DeleteTree();
            Dispose();
        }

        public new string ToString()
        {
            return DistinguishedName;
        }

        public new DateTime? AccountExpirationDate
        {
            get => base.AccountExpirationDate?.ToLocalTime();
            set => base.AccountExpirationDate = value?.ToUniversalTime();
        }

        public new DateTime? AccountLockoutTime => base.AccountLockoutTime?.ToLocalTime();

        public string AccountLocation
        {
            get
            {
                string dn = DistinguishedName;
                int pos = dn.IndexOf(",OU=", StringComparison.OrdinalIgnoreCase);
                dn = dn.Substring(pos + 1, dn.Length - pos - 17);
                int start = dn.Length - 1;
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

        [DirectoryProperty("ExtensionAttribute15")]
        public string AccountType
        {
            get
            {
                object[] values = ExtensionGet("ExtensionAttribute15");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set => ExtensionSet("ExtensionAttribute15", value);
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

        [DirectoryProperty("gidNumber")]
        public AccountSource AccountSource
        {
            get
            {
                object[] values = ExtensionGet("gidNumber");
                if (values.Length != 1 || values[0] == null)
                {
                    return AccountSource.None;
                }
                return (AccountSource)((int)values[0] & 15);
            }
            set
            {
                object[] values = ExtensionGet("gidNumber");
                int currentValue = 0;
                if (values.Length == 1)
                {
                    currentValue = (int)values[0];
                }
                // Mask out account source bits (lowest 5 bits). We
                // also mask the sign bit since this is should never
                // be used for any account processing rules.
                int newValue = (currentValue & 2147483616) | (int)value;
                if (newValue != 0)
                {
                    ExtensionSet("gidNumber", newValue);
                }
                else
                {
                    ExtensionSet("gidNumber", null);
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
            set => ExtensionSet("anr", value);
        }

        [DirectoryProperty("company")]
        public string Company
        {
            get
            {
                object[] values = ExtensionGet("company");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set => ExtensionSet("company", value);
        }

        public int DaysBeforeNextPasswordChange
        {
            get
            {
                int days = (PasswordExpiryTime - DateTime.Now).Days;
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
            set => ExtensionSet("department", value);
        }

        [DirectoryProperty("departmentNumber")]
        public string DepartmentNumber
        {
            get
            {
                object[] values = ExtensionGet("departmentNumber");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set => ExtensionSet("departmentNumber", value);
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
            set => ExtensionSet("employeeNumber", value);
        }

        public bool Expired
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

        [DirectoryProperty("msExchHideFromAddressLists")]
        public bool HideFromAddressLists
        {
            get
            {
                object[] values = ExtensionGet("msExchHideFromAddressLists");
                if (values.Length != 1)
                {
                    return false;
                }
                return (bool)values[0];
            }
            set => ExtensionSet("msExchHideFromAddressLists", value);
        }

        [DirectoryProperty("employeeType")]
        public string HiddenMobilePhone
        {
            get
            {
                object[] values = ExtensionGet("employeeType");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set => ExtensionSet("employeeType", value);
        }

        [DirectoryProperty("wWWHomePage")]
        public string HomePage
        {
            get
            {
                object[] values = ExtensionGet("wWWHomePage");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set => ExtensionSet("wWWHomePage", value);
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
            set => ExtensionSet("initials", value);
        }

        [DirectoryProperty("msExchRemoteRecipientType")]
        [DirectoryProperty("msExchRecipientTypeDetails")]
        [DirectoryProperty("mailNickname")]
        public bool MailEnabled
        {
            get
            {
                if (TryGetSingleValuedStringProperty("mailNickname", out _) &&
                    TryGetLargeIntegerProperty("msExchRemoteRecipientType", out long remoteRecipientType) &&
                    TryGetLargeIntegerProperty("msExchRecipientTypeDetails", out long recipientTypeDetails))
                {
                    return (remoteRecipientType == CLOUD_PROVISIONED_MAILBOX || remoteRecipientType == MIGRATED_MAILBOX || remoteRecipientType == MIGRATED_SHARED_MAILBOX)
                        && (recipientTypeDetails == REMOTE_USER_MAILBOX || recipientTypeDetails == REMOTE_SHARED_MAILBOX);
                }
                return false;
            }
        }

        public bool Managed
        {
            get
            {
                return Enabled.HasValue
                    && Enabled.Value
                    && AccountProcessingRules.HasValue
                    && AccountProcessingRules > 0
                    && EmployeeNumber != null;
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
                string value = (string)values[0];
                if (value.IndexOf(",CN=Deleted Objects,DC=", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    return null;
                }
                return value;
            }
            set => ExtensionSet("manager", value);
        }

        public new DateTime? LastPasswordSet => base.LastPasswordSet?.ToLocalTime();

        [DirectoryProperty("memberOf")]
        public IEnumerable<string> MemberOf
        {
            get
            {
                object[] values = ExtensionGet("memberOf");
                if (values.Length == 0)
                {
                    return Enumerable.Empty<string>();
                }
                return values.Cast<string>();
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
            set => ExtensionSet("mobile", value);
        }

        public bool NonDeliveryReceiptRuleEnabled => MemberOf.Any(t => t.StartsWith("CN=U-exch-ndr-mailbox", StringComparison.OrdinalIgnoreCase));

        [DirectoryProperty("objectCategory")]
        public string ObjectCategory
        {
            get => (string)ExtensionGet("objectCategory")[0];
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
            set => ExtensionSet("physicalDeliveryOfficeName", value);
        }

        [DirectoryProperty("msDS-cloudExtensionAttribute2")]
        public string OriginalLocation
        {
            get
            {
                object[] values = ExtensionGet("msDS-cloudExtensionAttribute2");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set => ExtensionSet("msDS-cloudExtensionAttribute2", value);
        }

        [DirectoryProperty("msDS-UserPasswordExpiryTimeComputed")]
        public DateTime PasswordExpiryTime
        {
            get
            {
                ((DirectoryEntry)GetUnderlyingObject()).RefreshCache(new string[] { "msDS-UserPasswordExpiryTimeComputed" });
                if (TryGetLargeIntegerProperty("msDS-UserPasswordExpiryTimeComputed", out long expiryTime))
                {
                    if (expiryTime == PASSWORD_NEVER_EXPIRES)
                    {
                        return DateTime.MaxValue;
                    }
                    else if (expiryTime > 0)
                    {
                        return DateTime.FromFileTime(expiryTime);
                    }
                }
                return DateTime.Now;
            }
        }

        public string PrimarySmtpAddress
        {
            get
            {
                foreach (string address in ProxyAddresses)
                {

                    if (address.StartsWith("SMTP:", StringComparison.Ordinal))
                    {
                        return address.Substring(5);
                    }
                }
                return null;
            }
            set
            {
                List<string> addressList = new List<string>(ProxyAddresses);
                string oldPrimary = PrimarySmtpAddress;
                if (!string.IsNullOrEmpty(oldPrimary))
                {
                    addressList.Remove("SMTP:" + oldPrimary);
                }
                if (!string.IsNullOrEmpty(value))
                {
                    string newPrimary;
                    if (value.StartsWith("smtp:", StringComparison.OrdinalIgnoreCase))
                    {
                        newPrimary = "SMTP:" + value.Substring(5);
                    }
                    else
                    {
                        newPrimary = "SMTP:" + value;
                    }
                    addressList.Add(newPrimary);
                }
                ProxyAddresses = addressList;
            }
        }

        public IEnumerable<string> SecondarySmtpAddresses
        {
            get
            {
                List<string> addressList = new List<string>(3); // Only a few users have more than 3 secondary addresses
                foreach (string address in (ProxyAddresses ?? Enumerable.Empty<object>()).Cast<string>())
                {
                    if (address.StartsWith("smtp:", StringComparison.Ordinal))
                    {
                        addressList.Add(address.Substring(5));
                    }
                }
                return addressList;
            }
            set
            {
                List<string> addressList = new List<string>(ProxyAddresses.Except(SecondarySmtpAddresses.Select(t => "smtp:" + t)));
                foreach (string address in value ?? Enumerable.Empty<string>())
                {
                    if (address.StartsWith("smtp:", StringComparison.OrdinalIgnoreCase))
                    {
                        addressList.Add("smtp:" + address.Substring(5));
                    }
                    else
                    {
                        addressList.Add("smtp:" + address);
                    }
                }
                ProxyAddresses = addressList;
            }
        }

        [DirectoryProperty("msDS-cloudExtensionAttribute9")]
        public string SchoolUnitCode
        {
            get
            {
                object[] values = ExtensionGet("msDS-cloudExtensionAttribute9");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set => ExtensionSet("msDS-cloudExtensionAttribute9", value);
        }

        [DirectoryProperty("proxyAddresses")]
        public IEnumerable<string> ProxyAddresses
        {
            get
            {
                object[] values = ExtensionGet("proxyAddresses");
                if (values.Length == 0)
                {
                    return Enumerable.Empty<string>();
                }
                return values.Cast<string>();
            }
            set => ExtensionSet("proxyAddresses", value.ToArray());
        }

        public bool Quarantined => DistinguishedName.IndexOf(",OU=Quarantine,", StringComparison.OrdinalIgnoreCase) > -1;

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
            set => ExtensionSet("carLicense", value);
        }

        [DirectoryProperty("msDS-cloudExtensionAttribute14")]
        public string SamlId
        {
            get
            {
                object[] values = ExtensionGet("msDS-cloudExtensionAttribute14");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set => ExtensionSet("msDS-cloudExtensionAttribute14", value);
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
                string value = (string)values[0];
                if (value.IndexOf(",CN=Deleted Objects,DC=", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return null;
                }
                return value;
            }
            set => ExtensionSet("seeAlso", value);
        }

        [DirectoryProperty("msDS-cloudExtensionAttribute1")]
        public IEnumerable<Guid> StashedLicenses
        {
            get
            {
                object[] values = ExtensionGet("msDS-cloudExtensionAttribute1");
                if (values.Length != 1)
                {
                    return null;
                }
                string[] parts = ((string)values[0]).Split(',');
                if (parts.Length == 0)
                {
                    return Enumerable.Empty<Guid>();
                }
                Guid[] guids = new Guid[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    if (System.Guid.TryParse(parts[i], out Guid guid))
                    {
                        guids[i] = guid;
                    }
                    else
                    {
                        return Enumerable.Empty<Guid>();
                    }
                }
                return guids;
            }
        }

        [DirectoryProperty("extensionAttribute11")]
        public bool Synchronized
        {
            get
            {
                object[] values = ExtensionGet("extensionAttribute11");
                return values.Length > 0 && values[0] != null;
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
            set => ExtensionSet("targetAddress", value);
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
            set => ExtensionSet("telephoneNumber", value);
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
            set => ExtensionSet("title", value);
        }

        [DirectoryProperty("whenCreated")]
        public DateTime WhenCreated => (DateTime)ExtensionGet("whenCreated")[0];
    }
}
