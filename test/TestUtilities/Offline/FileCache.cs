// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace Microsoft.TestUtilities.Offline;

public class FileCache : IDistributedCache
{
    private readonly string _storageRootPath;

    public FileCache(string storageRootPath)
    {
        Directory.CreateDirectory(storageRootPath);
        _storageRootPath = Path.GetFullPath(storageRootPath);
    }

    public void Reset()
    {
        foreach (string file in Directory.EnumerateFiles(_storageRootPath))
        {
            File.Delete(file);
        }
    }

    private string GetPathForKey(string key) => Path.Combine(_storageRootPath, key);

    public byte[]? Get(string key)
    {
        string contentsFilePath = GetPathForKey(key);
        return File.Exists(contentsFilePath) ? File.ReadAllBytes(contentsFilePath) : null;
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        string contentsFilePath = GetPathForKey(key);

        if (!File.Exists(contentsFilePath))
        {
            return null;
        }

#if NET
        return await File.ReadAllBytesAsync(contentsFilePath, token);
#else
        using var stream =
            new FileStream(
                contentsFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

        byte[] buffer = new byte[stream.Length];

        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            token.ThrowIfCancellationRequested();

            int read =
                await stream.ReadAsync(
                    buffer,
                    offset: totalRead,
                    count: buffer.Length - totalRead,
                    token).ConfigureAwait(false);

            totalRead += read;

            if (read == 0)
            {
                // End of stream reached.

                if (buffer.Length is not 0 && totalRead != buffer.Length)
                {
                    throw new EndOfStreamException(
                        $"End of stream reached for {contentsFilePath} with {totalRead} bytes read, but {buffer.Length} bytes were expected.");
                }
                else
                {
                    break;
                }
            }
        }

        return buffer;
#endif
    }

    /// <inheritdoc/>
    public void Refresh(string key)
    {
        // Refresh not supported in this implementation.
    }

    /// <inheritdoc/>
    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        // Refresh not supported in this implementation.
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Remove(string key)
    {
        string contentsFilePath = GetPathForKey(key);
        if (File.Exists(contentsFilePath))
        {
            File.Delete(contentsFilePath);
        }
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        string contentsFilePath = GetPathForKey(key);
        if (File.Exists(contentsFilePath))
        {
            File.Delete(contentsFilePath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        string contentsFilePath = GetPathForKey(key);
        File.WriteAllBytes(contentsFilePath, value);
    }

    /// <inheritdoc/>
    public async Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        string contentsFilePath = GetPathForKey(key);

#if NET
        await File.WriteAllBytesAsync(contentsFilePath, value, token).ConfigureAwait(false);
#else
        using var stream =
            new FileStream(
                contentsFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Write,
                bufferSize: 4096,
                useAsync: true);

        await stream.WriteAsync(value, 0, value.Length, token).ConfigureAwait(false);
#endif
    }

}
