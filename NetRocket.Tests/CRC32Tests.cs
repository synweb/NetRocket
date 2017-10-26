using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NetRocket.Cryptography;
using Xunit;

namespace NetRocket.Tests
{
    public class CRC32Tests
    {
        [Theory, MemberData(nameof(SimpleChecksumCases))]
        public void SimpleChecksumTests(string input, string expectedResult)
        {
            var crc = new CRC32();
            var inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] checksumBytes = null;
            var iterations = 100000;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < iterations; i++)
            {
                checksumBytes = crc.ComputeHash(inputBytes);
            }
            sw.Stop();
            Console.WriteLine($"{iterations} iterations were computed in {sw.ElapsedMilliseconds} ms");
            var checksumString = Utils.ByteArrayToString(checksumBytes);

            Assert.Equal(expectedResult, checksumString);
        }

        public static object[] SimpleChecksumCases = new[]
        {
            new[] {"simple", "c17b3d02"},
            new[] {"Помогите! Меня заперли в духовке!", "5b7be1b7"},
            new[] {" ", "e96ccf45"},
        };
    }
}
