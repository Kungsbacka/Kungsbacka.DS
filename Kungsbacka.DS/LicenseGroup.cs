using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kungsbacka.DS
{
    public class ADLicenseGroup : IComparable<ADLicenseGroup>
    {
        public Guid Guid { get; set; }
        public string DistinguishedName { get; set; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string BaseLicense { get; set; }
        public bool Dynamic { get; set; }
        public bool Standard { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }

        public int CompareTo(ADLicenseGroup that)
        {
            return this.DisplayName.CompareTo(that.DisplayName);
        }
    }
}
