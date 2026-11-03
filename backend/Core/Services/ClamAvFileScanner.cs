using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using Shared.Contracts.Platform;

namespace Core.Services;

public interface IPlatformFileMalwareScanner
{
    Task<bool> IsCleanAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
}

public sealed class ClamAvFileScanner(IConfiguration configuration) : IPlatformFileMalwareScanner
{
    private readonly string? host = configuration["TODIR_FILES_SCANNER_HOST"];
    private readonly int port = int.TryParse(configuration["TODIR_FILES_SCANNER_PORT"], out var configuredPort) && configuredPort is > 0 and <= 65535
        ? configuredPort
        : 3310;
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(
        int.TryParse(configuration["TODIR_FILES_SCANNER_TIMEOUT_SECONDS"], out var seconds) && seconds is >= 1 and <= 120 ? seconds : 30);

    public async Task<bool> IsCleanAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new PlatformFileStorageUnavailableException();

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, deadline.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), deadline.Token);

            var header = new byte[sizeof(int)];
            var offset = 0;
            while (offset < content.Length)
            {
                var length = Math.Min(64 * 1024, content.Length - offset);
                BinaryPrimitives.WriteInt32BigEndian(header, length);
                await stream.WriteAsync(header, deadline.Token);
                await stream.WriteAsync(content.Slice(offset, length), deadline.Token);
                offset += length;
            }

            BinaryPrimitives.WriteInt32BigEndian(header, 0);
            await stream.WriteAsync(header, deadline.Token);

            using var response = new MemoryStream();
            var buffer = new byte[256];
            while (response.Length < 4096)
            {
                var read = await stream.ReadAsync(buffer, deadline.Token);
                if (read == 0) break;
                var end = Array.IndexOf(buffer, (byte)0, 0, read);
                response.Write(buffer, 0, end >= 0 ? end : read);
                if (end >= 0) break;
            }

            var result = Encoding.UTF8.GetString(response.ToArray());
            if (result.Contains(": OK", StringComparison.Ordinal)) return true;
            if (result.Contains(" FOUND", StringComparison.Ordinal)) return false;
            throw new PlatformFileStorageUnavailableException();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PlatformFileStorageUnavailableException();
        }
        catch (SocketException)
        {
            throw new PlatformFileStorageUnavailableException();
        }
        catch (IOException)
        {
            throw new PlatformFileStorageUnavailableException();
        }
    }
}
