using System;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Kungsbacka.DS
{
    [DirectoryRdnPrefix("CN")]
    [DirectoryObjectClass("group")]
    public class ADGroup : GroupPrincipal
    {
        private static readonly Guid memberAttributeGuid = new Guid("bf9679c0-0de6-11d0-a285-00aa003049e2");
        private SecurityIdentifier managedBySid;

        public ADGroup(PrincipalContext context) : base(context) { }

        public static new ADGroup FindByIdentity(PrincipalContext context, IdentityType identityType, string identityValue)
        {
            return (ADGroup)FindByIdentityWithType(context, typeof(ADGroup), identityType, identityValue);
        }

        public new string ToString()
        {
            return DistinguishedName;
        }

        [DirectoryProperty("location")]
        public string Location
        {
            get
            {
                object[] values = ExtensionGet("location");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set => ExtensionSet("location", value);
        }

        [DirectoryProperty("managedBy")]
        public string ManagedBy
        {
            get
            {
                object[] values = ExtensionGet("managedBy");
                if (values.Length != 1)
                {
                    return null;
                }
                return (string)values[0];
            }
            set => ExtensionSet("managedBy", value);
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

        private void UpdateManagedBySidCache()
        {
            if (managedBySid != null)
            {
                return;
            }
            string managedBy = ManagedBy;
            if (string.IsNullOrEmpty(managedBy))
            {
                return;
            }
            using (ADGroup manager = DSFactory.FindGroupByDistinguishedName(managedBy))
            {
                managedBySid = manager.Sid;
            }
        }

        private bool AreAccessRulesEqual(ActiveDirectoryAccessRule rule1, ActiveDirectoryAccessRule rule2)
        {
            return rule1.IdentityReference == rule2.IdentityReference &&
                rule1.ActiveDirectoryRights == rule2.ActiveDirectoryRights &&
                rule1.AccessControlType == rule2.AccessControlType &&
                rule1.InheritedObjectType == rule2.InheritedObjectType;
        }

        private ActiveDirectoryAccessRule GetNewUpdateMembershipRule()
        {
            UpdateManagedBySidCache();
            return new ActiveDirectoryAccessRule(
                managedBySid,
                ActiveDirectoryRights.WriteProperty,
                AccessControlType.Allow,
                memberAttributeGuid
            );
        }

        public void SetManagerCanUpdateMembership(bool enabled)
        {
            DirectoryEntry de = (DirectoryEntry)GetUnderlyingObject();
            AuthorizationRuleCollection sec = de.ObjectSecurity.GetAccessRules(true, false, typeof(SecurityIdentifier));
            ActiveDirectoryAccessRule newRule = GetNewUpdateMembershipRule();
            ActiveDirectoryAccessRule rule = sec.Cast<ActiveDirectoryAccessRule>().FirstOrDefault(r => AreAccessRulesEqual(r, newRule));
            if (enabled && rule == null)
            {
                de.ObjectSecurity.AddAccessRule(newRule);
            }
            else if (!enabled && rule != null)
            {
                de.ObjectSecurity.RemoveAccessRule(rule);
            }
        }

        public bool GetManagerCanUpdateMembership()
        {
            ActiveDirectoryAccessRule rule = GetNewUpdateMembershipRule();
            DirectoryEntry de = (DirectoryEntry)GetUnderlyingObject();
            AuthorizationRuleCollection sec = de.ObjectSecurity.GetAccessRules(true, false, typeof(SecurityIdentifier));
            return sec.Cast<ActiveDirectoryAccessRule>().Any(r => AreAccessRulesEqual(r, rule));
        }
    }
}
