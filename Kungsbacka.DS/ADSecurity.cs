using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Text;

namespace Kungsbacka.DS
{
    public static class ADSecurity
    {

        /* Refactoring in progress 
         * 
         * 
        static Dictionary<Guid, PropertyValueCollection> allowedAttributesEffectiveCache;

        public static bool CanReadAttribute(Principal principal, string attribute)
        {

        }

        public static bool CanWriteAttibute(Principal principal, string attribute)
        {
            var underlyingObject = (DirectoryEntry)principal.GetUnderlyingObject();
            PropertyValueCollection allowedAttributesEffective = null;
            if (allowedAttributesEffectiveCache == null)
            {
                allowedAttributesEffectiveCache = new Dictionary<Guid, PropertyValueCollection>();
            }
            else
            {
                allowedAttributesEffectiveCache.TryGetValue(underlyingObject.Guid, out allowedAttributesEffective);
            }
            if (allowedAttributesEffective == null)
            {
                DirectoryEntry de = (DirectoryEntry)GetUnderlyingObject();
                de.RefreshCache(new string[] { "allowedAttributesEffective" });
                allowedAttributesEffective = de.Properties["allowedAttributesEffective"];
            }
            return allowedAttributesEffective.Contains(attribute);
        }
        */

        /*
         *         public bool CanReadAttribute(string attribute)
        {
            if (ADSchema.IsAttributeConfidential(attribute))
            {
                using (var windowsIdentity = WindowsIdentity.GetCurrent())
                {
                    // Domain Admin?
                    using (var currentUser = DSFactory.FindUserBySid(windowsIdentity.User))
                    {
                        var domainAdminsSid = new SecurityIdentifier(WellKnownSidType.AccountDomainAdminsSid, currentUser.Sid.AccountDomainSid);
                        if (currentUser.IsMemberOf(currentUser.Context, IdentityType.Sid, domainAdminsSid.Value))
                        {
                            return true;
                        }
                    }
                    // Member of a group that grants access?
                    var userSchemaGuid = ADSchema.GetClassSchemaGuid("user");
                    var attributeSchemaGuid = ADSchema.GetAttributeSchemaGuid(attribute);
                    var groups = windowsIdentity.Groups;
                    var de = (DirectoryEntry)GetUnderlyingObject();
                    var acl = de.ObjectSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier));
                    // Check ADS_RIGHT_DS_CONTROL_ACCESS where object type is the attribute being checked and inherited object type is user.
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
            }
            return true;
        }
*/
    }
}
