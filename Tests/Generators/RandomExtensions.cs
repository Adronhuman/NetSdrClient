using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Generators
{
    public static class RandomExtensions
    {
        public static byte[] GenerateNRandomBytes(int n)
        {
            var random = new Random();
            var byteArray = new byte[n];
            random.NextBytes(byteArray);
            return byteArray;
        }
    }
}
