using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using ShubarinSummerPracticeGRPC;

namespace grpc_client
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine(Directory.GetCurrentDirectory());
            await GreetServer();
            await GetFile(@"test_file.txt");
        }

        private static async Task GreetServer()
        {
            // The port number(5001) must match the port of the gRPC server.
            using var channel = GrpcChannel.ForAddress("https://localhost:5001");
            var client = new Greeter.GreeterClient(channel);
            var reply = await client.SayHelloAsync(new HelloRequest {Name = "Client"});
            Console.WriteLine("Server said: " + reply.Message);
        }

        private static async Task GetFile(string filePath)
        {
            var channel = GrpcChannel.ForAddress("https://localhost:5001/", new GrpcChannelOptions
            {
                MaxReceiveMessageSize = 5 * 1024 * 1024, // 5 MB
                MaxSendMessageSize = 5 * 1024 * 1024 // 5 MB
            });

            var client = new FileTransferService.FileTransferServiceClient(channel);

            var request = new FileRequest {FilePath = filePath};

            var tempFile = $"temp_{DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")}.tmp";
            var finalFile = tempFile;
            var fileCache = "";
            var fileExists = false;
            long fileSize = 0;
            using (var call = client.DownloadFile(request))
            {
                await using (var fs = File.OpenWrite(tempFile))
                {
                    var stopWatch = new Stopwatch();
                    stopWatch.Start();
                    await foreach (var chunk in call.ResponseStream.ReadAllAsync().ConfigureAwait(false))
                    {
                        fileExists = chunk.FileSize != -1 && !chunk.IsCanceled;
                        if (!fileExists) break;
                        fileCache = chunk.Md5Cache;
                        fileSize = chunk.FileSize;

                        if (!string.IsNullOrEmpty(chunk.FileName)) finalFile = chunk.FileName;

                        if (chunk.Chunk.Length == chunk.ChunkSize)
                        {
                            fs.Write(chunk.Chunk.ToByteArray());
                        }
                        else
                        {
                            fs.Write(chunk.Chunk.ToByteArray(), 0, chunk.ChunkSize);
                            Console.WriteLine($"final chunk size: {chunk.ChunkSize}");
                        }
                    }

                    stopWatch.Stop();
                    // Get the elapsed time as a TimeSpan value.
                    var ts = stopWatch.Elapsed;

                    // Format and display the TimeSpan value.
                    var elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                        ts.Hours, ts.Minutes, ts.Seconds,
                        ts.Milliseconds);
                    Console.WriteLine("File transfer took: " + elapsedTime);
                }
            }

            Console.WriteLine("Total file size: {0} KB", fileSize / 1024);
            if (fileExists)
            {
                if (finalFile != tempFile)
                    File.Move(tempFile, finalFile);
                Console.WriteLine(
                    fileCache == CheckMd5(finalFile)
                        ? "Caches converged! File: {0} was transferred successfully!"
                        : "Caches are different! File: {0} was NOT transferred successfully!", finalFile);
            }
            else
            {
                File.Delete(tempFile);
                File.Delete(finalFile);
                Console.WriteLine("File does not exist");
            }
        }

        private static string CheckMd5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    return Encoding.Default.GetString(md5.ComputeHash(stream));
                }
            }
        }
    }
}
