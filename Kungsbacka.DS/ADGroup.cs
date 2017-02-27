using System;
using System.Linq;
using System.DirectoryServices.AccountManagement;

namespace Kungsbacka.DS
{
    class ADGroup : GroupPrincipal
    {
        bool ea15IsParsed;
        string groupSource;

        public ADGroup(PrincipalContext context) : base(context) { }

        public static new ADGroup FindByIdentity(PrincipalContext context, IdentityType identityType, string identityValue)
        {
            return (ADGroup)FindByIdentityWithType(context, typeof(ADGroup), identityType, identityValue);
        }

        public new string ToString()
        {
            return DistinguishedName;
        }

        void ParseEa15()
        {
            if (!ea15IsParsed)
            {
                var rawValue = ExtensionGet("extensionAttribute15");
                if (rawValue.Length > 0)
                {
                    var value = rawValue[0] as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        var parts = value.Split(';');
                        var source = parts.FirstOrDefault(s => s.StartsWith("source=", StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrEmpty(source))
                        {
                            parts = source.Split('=');
                            groupSource = parts[1];
                        }
                    }
                }
                ea15IsParsed = true;
            }
        }

        [DirectoryProperty("extensionAttribute15")]
        public string GroupSource
        {
            get
            {
                ParseEa15();
                return groupSource;
            }
        }
    }
}
