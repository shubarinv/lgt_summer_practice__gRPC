using System;
using System.IO;

namespace test_file_generator
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("How many lines should file have?");
            var lim = Convert.ToInt32(Console.ReadLine());
            const int blockSize = 1024 * 8;
            const int blocksPerMb = 1024 * 1024 / blockSize;
            var data = new byte[blockSize];
            var rng = new Random();
            using var stream = File.OpenWrite("../../../../grpc_server/bin/Debug/net5.0/test_file.txt");
            // There 
            for (var i = 0; i < lim * blocksPerMb; i++)
            {
                rng.NextBytes(data);
                stream.Write(data, 0, data.Length);
            }
        }
    }
}