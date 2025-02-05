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
