using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Kungsbacka.DS.UnitTests
{
    [TestClass]
    public class TestAccountNames
    {
        [TestMethod]
        public void TestGetSamAccountName()
        {
            var an = new AccountNamesFactory();
            Assert.AreEqual("givsur", AccountNamesFactory.GetSamAccountName("Givenname", "Surname"));
            Assert.AreEqual("givsur", AccountNamesFactory.GetSamAccountName("Givénname", "Sürname"));
            Assert.AreEqual("00givsur", AccountNamesFactory.GetSamAccountName("Givenname", "Surname", "000000000000"));
            Assert.AreEqual("ab", AccountNamesFactory.GetSamAccountName("--a--", " b123 "));
        }

        [TestMethod]
        public void TestGetUpnNamePart()
        {
            var an = new AccountNamesFactory();
            Assert.AreEqual("givenname.surname", AccountNamesFactory.GetUpnNamePart("Givenname", "Surname"));
            Assert.AreEqual("givenname.surnamea-surnameb", AccountNamesFactory.GetUpnNamePart("Givenname", "SurnameA SurnameB"));
            Assert.AreEqual("given.su", AccountNamesFactory.GetUpnNamePart("Givén", "Su"));
            Assert.AreEqual("-a-.b", AccountNamesFactory.GetUpnNamePart("--a--", " b123 "));
        }

        [TestMethod]
        public void TestGetName()
        {
            Tuple<string, string>[] list = new Tuple<string, string>[] {
                new Tuple<string, string>("Givenname", "Givenname"),
                new Tuple<string, string>("Givénname", "Givénname"),
                new Tuple<string, string>("Givenname", " Givenname "),
                new Tuple<string, string>("Givenname Surname", "Givenname Surname"),
                new Tuple<string, string>("Givenname Surname", "Givenname   Surname"),
                new Tuple<string, string>("ø", "ø"),
            };
            foreach (var tuple in list)
            {
                Assert.AreEqual(tuple.Item1, AccountNamesFactory.GetName(tuple.Item2));
            }
        }

        [TestMethod]
        public void TestGetDisplayName()
        {
            var an = new AccountNamesFactory();
            Assert.AreEqual("Givenname Surname", AccountNamesFactory.GetDisplayName("Givenname", "Surname"));
            Assert.AreEqual("Givénname Sürname", AccountNamesFactory.GetDisplayName("Givénname ", " Sürname"));
            Assert.AreEqual("Given-Name SurnameA SurnameB", AccountNamesFactory.GetDisplayName("Given-Name", "SurnameA   SurnameB"));

        }

        [TestMethod]
        public void TestGetNextAvailableSuffix()
        {
            var an = new AccountNamesFactory();
            an.CacheSuffix("a", -1);
            an.CacheSuffix("a", 3);
            an.CacheSuffix("a", 5);
            an.CacheSuffix("b", -1);
            // c
            an.CacheSuffix("d", -1);
            an.CacheSuffix("d", 2);
            an.CacheSuffix("e", 2);
            an.CacheSuffix("e", 3);
            an.CacheSuffix("f", 0);
            an.CacheSuffix("g", -1);
            an.CacheSuffix("g", 1);
            an.CacheSuffix("h", 0);
            an.CacheSuffix("h", 1);
            an.CacheSuffix("i", int.MinValue);
            an.CacheSuffix("i", int.MaxValue);
            an.CacheSuffix("j", -1);
            an.CacheSuffix("j", 2);
            an.CacheSuffix("j", 5);
            an.CacheSuffix("k", -1);
            an.CacheSuffix("k", 3);
            an.CacheSuffix("k", 4);
            an.CacheSuffix("k", -1);
            an.CacheSuffix("k", 49);
            an.CacheSuffix("k", 2);
            an.CacheSuffix("k", 5);
            an.CacheSuffix("k", 4);
            an.CacheSuffix("k", 3);
            an.CacheSuffix("k", 25);
            Assert.AreEqual(an.GetNextAvailableSuffix("a"), 2, "a");
            Assert.AreEqual(an.GetNextAvailableSuffix("b"), 2, "b");
            Assert.AreEqual(an.GetNextAvailableSuffix("c"), -1, "c");
            Assert.AreEqual(an.GetNextAvailableSuffix("d"), 3, "d");
            Assert.AreEqual(an.GetNextAvailableSuffix("e"), -1, "e");
            Assert.AreEqual(an.GetNextAvailableSuffix("f"), -1, "f");
            Assert.AreEqual(an.GetNextAvailableSuffix("g"), 2, "g");
            Assert.AreEqual(an.GetNextAvailableSuffix("h"), -1, "h");
            Assert.AreEqual(an.GetNextAvailableSuffix("i"), 2, "i");
            Assert.AreEqual(an.GetNextAvailableSuffix("j", 7), 7, "j7");
            Assert.AreEqual(an.GetNextAvailableSuffix("j", 5), 6, "j5");
            Assert.AreEqual(an.GetNextAvailableSuffix("k"), 6, "k");
        }

        [TestMethod]
        public void TestGetNextAvailableSuffixWithPrimedCaceh()
        {
            var an = new AccountNamesFactory(new string[] { "a", "a2", "b", "b3", "d", "e@x.y", "e2@x.y"});
            Assert.AreEqual(an.GetNextAvailableSuffix("a"), 3, "a");
            Assert.AreEqual(an.GetNextAvailableSuffix("b"), 2, "b");
            Assert.AreEqual(an.GetNextAvailableSuffix("c"), -1, "c");
            Assert.AreEqual(an.GetNextAvailableSuffix("d"), 2, "d");
            Assert.AreEqual(an.GetNextAvailableSuffix("e"), 3, "e");
        }


        [TestMethod]
        public void TestGetAccountNames1()
        {
            var an = new AccountNamesFactory();
            var names = an.GetNames("Unique", "Name", "example.com", "199700000000");
            var list = DSFactory.SearchUser(UserSearchProperty.SamAccountName, names.SamAccountName);
            Assert.AreEqual(0, list.Count);
            list = DSFactory.SearchUser(UserSearchProperty.UserPrincipalName, names.UserPrincipalName);
            Assert.AreEqual(0, list.Count);
            list = DSFactory.SearchUser(UserSearchProperty.CommonName, names.CommonName);
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void TestGetAccountNames2()
        {
            var an = new AccountNamesFactory();
            var names = an.GetNames("Unique", "Name", "example.com");
            Assert.AreEqual("uninam", names.SamAccountName);
            Assert.AreEqual("unique.name@example.com", names.UserPrincipalName);
            Assert.AreEqual("Unique Name", names.CommonName);
        }

        [TestMethod]
        public void TestGetAccountNames3()
        {
            var an = new AccountNamesFactory();
            var names = an.GetNames("Unique", "Name", "example.com", "1234");
            Assert.AreEqual(names.UserPrincipalName, "unique.name@example.com");
        }

        [TestMethod]
        public void TestGetAccountNames4()
        {
            var an = new AccountNamesFactory();
            var names = an.GetNames("Unique", "Name", "example.com", false);
            Assert.AreEqual(names.UserPrincipalName, "unique.name@example.com");
            Assert.AreEqual(names.SamAccountName, "uninam");
            Assert.AreEqual(names.CommonName, "Unique Name");
        }

        [TestMethod]
        public void TestGetAccountNames5()
        {
            var an = new AccountNamesFactory();
            var names1 = an.GetNames("First", "Last", "example.com", "199001010101");
            var names2 = an.GetNames("First", "Last", "example.com", "199001010101");
            var names3 = an.GetNames("First", "Last", "example.com", "199001010101");
            Assert.AreNotEqual(names1.SamAccountName, names2.SamAccountName);
            Assert.AreNotEqual(names1.SamAccountName, names3.SamAccountName);
            Assert.AreNotEqual(names2.SamAccountName, names3.SamAccountName);
        }

        [TestMethod]
        public void TestGetAccountNames6()
        {
            var an = new AccountNamesFactory();
            var names1 = an.GetNames("First", "Last", "example.com", "199001010101", true);
            var names2 = an.GetNames("First", "Last", "example.com", "199001010101", true);
            var names3 = an.GetNames("First", "Last", "example.com", "199001010101", true);
            Assert.AreNotEqual(names1.UserPrincipalName, names2.UserPrincipalName);
            Assert.AreNotEqual(names1.UserPrincipalName, names3.UserPrincipalName);
            Assert.AreNotEqual(names2.UserPrincipalName, names3.UserPrincipalName);
        }

    }
}
