using Microsoft.AspNetCore.Http;

namespace YAGOT_2._0.Integration.SiteState;

public sealed class SiteStateWebhookBodyTooLargeException : Exception;

public static class SiteStateWebhookBodyReader
{
    public static async Task<byte[]> ReadAsync(
        Stream body,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);

        await using var destination = new MemoryStream(
            Math.Min(maximumBytes, 8 * 1024));
        var buffer = new byte[8 * 1024];
        var total = 0;

        while (true)
        {
            var remainingWithOverflowByte = maximumBytes - total + 1;
            var bytesRead = await body.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, remainingWithOverflowByte)),
                cancellationToken);
            if (bytesRead == 0)
            {
                return destination.ToArray();
            }

            total += bytesRead;
            if (total > maximumBytes)
            {
                throw new SiteStateWebhookBodyTooLargeException();
            }

            await destination.WriteAsync(
                buffer.AsMemory(0, bytesRead),
                cancellationToken);
        }
    }
}
