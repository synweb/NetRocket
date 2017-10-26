using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace NetRocket.Tests
{
    public class CredentialsTests
    {
        public static readonly List<object[]> CredentialsEqualityTestsData = new List<object[]>
        {
            new object[] {new Credentials("Petya", "100500"), new Credentials("Petya", "100500"), true},
            new object[] {new Credentials("Petya", "password"), new Credentials("PeTYa", "password"), true},
            new object[] {new Credentials("Petya", "paSSWord"), new Credentials("Petya", "password"), false},
            new object[] {new Credentials("Petya", "100500"), new Credentials("petya", "100500"), true},
            new object[] {new Credentials("petya", "100500"), new Credentials("Petya", "100500"), true},
            new object[] {new Credentials("Petya", "100"), new Credentials("Petya", "100500"), false},
            new object[] {new Credentials("Pasha", "100500"), new Credentials("Petya", "100500"), false},
        };

        [Theory, MemberData(nameof(CredentialsEqualityTestsData))]
        public void CredentialsEqualityTests(Credentials cred1, Credentials cred2, bool expectedEqual)
        {
            Assert.Equal(cred1.Equals(cred2), expectedEqual);
            Assert.Equal(cred1 == cred2, expectedEqual);
            Assert.Equal(cred2 == cred1, expectedEqual);
            Assert.Equal(Equals(cred2, cred1), expectedEqual);
        }

        [Fact]
        public void CredentialsEqualityWithAnotherClassTest()
        {
            var cred1 = new Credentials("Pasha", "100500");
            var cred2 = "Меня заперли в духовке!";
            Assert.Equal(cred1.Equals(cred2), false);
            Assert.Equal(Equals(cred2, cred1), false);
        }
    }
}
