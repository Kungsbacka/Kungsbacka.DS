using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kungsbacka.DS
{
    public class ADLicenseGroup
    {
        public Guid Guid { get; set; }
        public string DistinguishedName { get; set; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string BaseLicense { get; set; }
        public bool Dynamic { get; set; }
        public bool Standard { get; set; }
        public bool MailEnabled { get; set; }

        public override string ToString()
        {
            return DisplayName ?? string.Empty;
        }
    }

    public class ADLicenseGroupNameComparer : IComparer<ADLicenseGroup>
    {
        public int Compare(ADLicenseGroup left, ADLicenseGroup right)
        {
            if (left?.DisplayName == null)
            {
                return right?.DisplayName == null ? 0 : 1;
            }
            if (right?.DisplayName == null)
            {
                return left?.DisplayName == null ? 0 : -1;
            }
            return left.DisplayName.CompareTo(right.DisplayName);
        }
    }
}
