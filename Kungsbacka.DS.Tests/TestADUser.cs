using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.DirectoryServices;

namespace Kungsbacka.DS.UnitTests
{
    [TestClass]
    public class TestADUser
    {
        [TestMethod]
        public void TestInitialize()
        {
            var rootDse = new DirectoryEntry("LDAP://RootDSE");
            string root = (string)rootDse.Properties["rootDomainNamingContext"][0];
            using (var adUser = DSFactory.FindUserByDistinguishedName($"CN=Administrator,CN=Users,{root}"))
            {
                Assert.AreEqual("Administrator", adUser.SamAccountName, true);
            }
        }

        [TestMethod]
        public void TestCustomSearch()
        {
            int count = 0;
            string userName = null;
            IList<ADUser> result = null;
            try
            {
                result = DSFactory.SearchUser(SearchProperty.SamAccountName, "Administrator");
                count = result.Count;
                userName = result[0].SamAccountName;
            }
            finally
            {
                if (null != result)
                {
                    foreach (var user in result)
                    {
                        user.Dispose();
                    }
                }
            
            }
            Assert.AreEqual(1, count);
            Assert.AreEqual("Administrator", userName, true);
        }
    }
}
