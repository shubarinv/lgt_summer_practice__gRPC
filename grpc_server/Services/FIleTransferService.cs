using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ShubarinSummerPracticeGRPC;

namespace grpc_server
{
    public class TransferService : FileTransferService.FileTransferServiceBase
    {
        private readonly ILogger<TransferService> _logger;

        public TransferService(ILogger<TransferService> logger)
        {
            _logger = logger;
        }

        public override async Task DownloadFile(FileRequest request, IServerStreamWriter<ChunkMsg> responseStream,
            ServerCallContext context)
        {
            var filePath = request.FilePath;

            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);

                var chunk = new ChunkMsg
                {
                    FileName = Path.GetFileName(filePath),
                    FileSize = fileInfo.Length,
                    Md5Cache = CalculateMd5(filePath)
                };

                const int chunkSize = 64 * 1024;

                var fileBytes = File.ReadAllBytes(filePath);
                var fileChunk = new byte[chunkSize];

                var offset = 0;

                while (offset < fileBytes.Length)
                {
                    if (context.CancellationToken.IsCancellationRequested)
                        break;

                    var length = Math.Min(chunkSize, fileBytes.Length - offset);
                    Buffer.BlockCopy(fileBytes, offset, fileChunk, 0, length);

                    offset += length;

                    chunk.ChunkSize = length;
                    chunk.Chunk = ByteString.CopyFrom(fileChunk);
                    await responseStream.WriteAsync(chunk).ConfigureAwait(false);
                }
            }
        }

        private static string CalculateMd5(string filename)
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