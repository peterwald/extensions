// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

#pragma warning disable CA2000 // Dispose objects before losing scope

namespace Microsoft.TestUtilities.Offline;

public static class HttpResponseExtensions
{
    public static async Task<string> GetRequestHashAsync(this HttpRequestMessage request, CancellationToken token)
    {
        byte[] bytes = [];
        if (request.Content is not null)
        {
            bytes = await request.Content.ReadAsByteArrayAsync(token);
        }

        return AIJsonUtilities.HashDataToString([request.RequestUri?.ToString() ?? "", request.Method, bytes]);
    }

    public static async Task<(byte[] responseBytes, byte[] cacheBytes)> ToCacheBytesAsync(this HttpResponseMessage message, CancellationToken token)
    {
        MemoryStream stream = new();
        using StreamWriter writer = new(stream);

        writer.WriteLine(message.StatusCode.ToString());
        foreach (var header in message.Headers)
        {
            writer.WriteLine($"{header.Key}: {string.Join(",", header.Value)}");
        }

        writer.WriteLine();

#if NET
        await writer.FlushAsync(token);
#else
        await writer.FlushAsync();
#endif

        byte[] responseBytes = [];
        if (message.Content is not null)
        {
            responseBytes = await message.Content.ReadAsByteArrayAsync(token);
            stream.Write(responseBytes, 0, responseBytes.Length);
        }

        stream.Seek(0, SeekOrigin.Begin);

        return (responseBytes, stream.ToArray());
    }

    private static ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> bytes, out string line)
    {
        int lineEndingIndex = bytes.IndexOfAny((byte)'\r', (byte)'\n');
        if (lineEndingIndex == -1)
        {
#if NET
            line = Encoding.UTF8.GetString(bytes);
#else
            line = Encoding.UTF8.GetString(bytes.ToArray());
#endif
            return ReadOnlySpan<byte>.Empty;
        }

        byte lineEnding = bytes[lineEndingIndex];
        int nextIndex = lineEndingIndex + 1;
        if (nextIndex < bytes.Length && lineEnding == (byte)'\r' && bytes[nextIndex] == (byte)'\n')
        {
            nextIndex++;
        }

        var lineBytes = bytes.Slice(0, lineEndingIndex);
#if NET
        line = Encoding.UTF8.GetString(lineBytes);
#else
        line = Encoding.UTF8.GetString(lineBytes.ToArray());
#endif
        return bytes.Slice(nextIndex);
    }

    public static HttpResponseMessage ToHttpResponseMessage(this ReadOnlySpan<byte> bytes)
    {
        HttpResponseMessage message = new();

        bytes = ReadLine(bytes, out string statusLine);
#if NET
        message.StatusCode = Enum.Parse<HttpStatusCode>(statusLine);
#else
        message.StatusCode = (HttpStatusCode)Enum.Parse(typeof(HttpStatusCode), statusLine);
#endif

        bytes = ReadLine(bytes, out string headerLine);
        while (!string.IsNullOrEmpty(headerLine))
        {
            int colonIndex = headerLine.IndexOf(':');
            if (colonIndex == -1)
            {
                throw new InvalidOperationException($"Invalid header line: {headerLine}");
            }

            string headerName = headerLine.Substring(0, colonIndex);
            string headerValue = headerLine.Substring(colonIndex + 1).Trim();

            message.Headers.TryAddWithoutValidation(headerName, headerValue.Split(','));

            bytes = ReadLine(bytes, out headerLine);
        }

        message.Content = new ByteArrayContent(bytes.ToArray());

        return message;
    }

#if !NET
#pragma warning disable IDE0060 // Remove unused parameter
    public static async Task<byte[]> ReadAsByteArrayAsync(this HttpContent content, CancellationToken cancellationToken)
    {
        MemoryStream stream = new();
        await content.CopyToAsync(stream);
        return stream.ToArray();
    }
#endif
}
