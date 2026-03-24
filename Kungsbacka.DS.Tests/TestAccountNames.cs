using System;
using Xunit;

namespace Kungsbacka.DS.UnitTests
{
    public class TestAccountNames
    {
        [Fact]
        public void TestGetSamAccountName()
        {
            Assert.Equal("givsur", AccountNamesFactory.GetSamAccountName("Givenname", "Surname"));
            Assert.Equal("givsur", AccountNamesFactory.GetSamAccountName("Givénname", "Sürname"));
            Assert.Equal("00givsur", AccountNamesFactory.GetSamAccountName("Givenname", "Surname", "000000000000"));
            Assert.Equal("ab", AccountNamesFactory.GetSamAccountName("--a--", " b123 "));
        }

        [Fact]
        public void TestGetUpnNamePart()
        {
            Assert.Equal("givenname.surname", AccountNamesFactory.GetUpnNamePart("Givenname", "Surname"));
            Assert.Equal("givenname.surnamea.surnameb", AccountNamesFactory.GetUpnNamePart("Givenname", "SurnameA SurnameB"));
            Assert.Equal("givennamea.givennameb.surname", AccountNamesFactory.GetUpnNamePart("GivennameA GivennameB", "Surname"));
            Assert.Equal("givenname.surnamea-surnameb.surnamec", AccountNamesFactory.GetUpnNamePart("Givenname", "SurnameA-SurnameB..SurnameC"));
            Assert.Equal("given.su", AccountNamesFactory.GetUpnNamePart("Givén", "Su"));
            Assert.Equal("-a-.b", AccountNamesFactory.GetUpnNamePart("--a--", " b123 "));
        }

        [Fact]
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
                Assert.Equal(tuple.Item1, AccountNamesFactory.GetName(tuple.Item2));
            }
        }

        [Fact]
        public void TestGetDisplayName()
        {
            Assert.Equal("Givenname Surname", AccountNamesFactory.GetDisplayName("Givenname", "Surname"));
            Assert.Equal("Givénname Sürname", AccountNamesFactory.GetDisplayName("Givénname ", " Sürname"));
            Assert.Equal("Given-Name SurnameA SurnameB", AccountNamesFactory.GetDisplayName("Given-Name", "SurnameA   SurnameB"));
        }

        [Fact]
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
            Assert.Equal(2, an.GetNextAvailableSuffix("a"));    // a
            Assert.Equal(2, an.GetNextAvailableSuffix("b"));    // b
            Assert.Equal(-1, an.GetNextAvailableSuffix("c"));   // c
            Assert.Equal(3, an.GetNextAvailableSuffix("d"));    // d
            Assert.Equal(-1, an.GetNextAvailableSuffix("e"));   // e
            Assert.Equal(-1, an.GetNextAvailableSuffix("f"));   // f
            Assert.Equal(2, an.GetNextAvailableSuffix("g"));    // g
            Assert.Equal(-1, an.GetNextAvailableSuffix("h"));   // h
            Assert.Equal(2, an.GetNextAvailableSuffix("i"));    // i
            Assert.Equal(7, an.GetNextAvailableSuffix("j", 7)); // j7
            Assert.Equal(6, an.GetNextAvailableSuffix("j", 5)); // j5
            Assert.Equal(6, an.GetNextAvailableSuffix("k"));    // k
        }

        [Fact]
        public void TestGetNextAvailableSuffixWithPrimedCache()
        {
            var an = new AccountNamesFactory(new string[] { "a", "a2", "b", "b3", "d", "e@x.y", "e2@x.y" });
            Assert.Equal(3, an.GetNextAvailableSuffix("a"));
            Assert.Equal(2, an.GetNextAvailableSuffix("b"));
            Assert.Equal(-1, an.GetNextAvailableSuffix("c"));
            Assert.Equal(2, an.GetNextAvailableSuffix("d"));
            Assert.Equal(3, an.GetNextAvailableSuffix("e"));
        }

        [Fact]
        public void TestGetAccountNames1()
        {
            var an = new AccountNamesFactory();
            var names = an.GetNames("Unique", "Name", "example.com", "199700000000");
            var list = DSFactory.SearchUser(UserSearchProperty.SamAccountName, names.SamAccountName);
            Assert.Empty(list);
            list = DSFactory.SearchUser(UserSearchProperty.UserPrincipalName, names.UserPrincipalName);
            Assert.Empty(list);
            list = DSFactory.SearchUser(UserSearchProperty.CommonName, names.CommonName);
            Assert.Empty(list);
        }

        [Fact]
        public void TestGetAccountNames2()
        {
            var an = new AccountNamesFactory();
            var names = an.GetNames("Unique", "Name", "example.com");
            Assert.Equal("uninam", names.SamAccountName);
            Assert.Equal("unique.name@example.com", names.UserPrincipalName);
            Assert.Equal("Unique Name", names.CommonName);
        }

        [Fact]
        public void TestGetAccountNames3()
        {
            var an = new AccountNamesFactory();
            var names = an.GetNames("Unique", "Name", "example.com", "1234");
            Assert.Equal("unique.name@example.com", names.UserPrincipalName);
        }

        [Fact]
        public void TestGetAccountNames4()
        {
            var an = new AccountNamesFactory();
            var names = an.GetNames("Unique", "Name", "example.com", false);
            Assert.Equal("unique.name@example.com", names.UserPrincipalName);
            Assert.Equal("uninam", names.SamAccountName);
            Assert.Equal("Unique Name", names.CommonName);
        }

        [Fact]
        public void TestGetAccountNames5()
        {
            var an = new AccountNamesFactory();
            var names1 = an.GetNames("First", "Last", "example.com", "199001010101");
            var names2 = an.GetNames("First", "Last", "example.com", "199001010101");
            var names3 = an.GetNames("First", "Last", "example.com", "199001010101");
            Assert.NotEqual(names1.SamAccountName, names2.SamAccountName);
            Assert.NotEqual(names1.SamAccountName, names3.SamAccountName);
            Assert.NotEqual(names2.SamAccountName, names3.SamAccountName);
        }

        [Fact]
        public void TestGetAccountNames6()
        {
            var an = new AccountNamesFactory();
            var names1 = an.GetNames("First", "Last", "example.com", "199001010101", true);
            var names2 = an.GetNames("First", "Last", "example.com", "199001010101", true);
            var names3 = an.GetNames("First", "Last", "example.com", "199001010101", true);
            Assert.NotEqual(names1.UserPrincipalName, names2.UserPrincipalName);
            Assert.NotEqual(names1.UserPrincipalName, names3.UserPrincipalName);
            Assert.NotEqual(names2.UserPrincipalName, names3.UserPrincipalName);
        }
    }
}
