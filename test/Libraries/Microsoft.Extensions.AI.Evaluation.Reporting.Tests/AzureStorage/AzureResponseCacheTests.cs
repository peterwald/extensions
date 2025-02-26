// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.TestUtilities.Offline;
using Xunit;

namespace Microsoft.Extensions.AI.Evaluation.Reporting.Tests;

public class AzureResponseCacheTests : ResponseCacheTester, IAsyncLifetime
{
    private static readonly DataLakeFileSystemClient? _fsClient;
    private static CachingMessageHandler? _cachingMessageHandler;

    static AzureResponseCacheTests()
    {
        DataLakeClientOptions clientOptions = new();

        switch (Settings.Current.TestMode)
        {
            case TestMode.Skip:
                return;

            case TestMode.Online:
            {
                IDistributedCache cache = new FileCache(Settings.Current.OfflineCachePath);
                _cachingMessageHandler = new CachingMessageHandler(cache, online: true);
                clientOptions = new DataLakeClientOptions { Transport = new HttpClientTransport(_cachingMessageHandler) };
                break;
            }

            case TestMode.Offline:
            {
                IDistributedCache cache = new FileCache(Settings.Current.OfflineCachePath);
                _cachingMessageHandler = new CachingMessageHandler(cache, online: false);
                clientOptions = new DataLakeClientOptions { Transport = new HttpClientTransport(_cachingMessageHandler) };
                break;
            }
        }

        _fsClient = new(
            new Uri(
                baseUri: new Uri(Settings.Current.StorageAccountEndpoint),
                relativeUri: Settings.Current.StorageContainerName),
            new DefaultAzureCredential(),
            clientOptions);

    }

    private readonly DataLakeDirectoryClient? _dirClient;

    public AzureResponseCacheTests()
    {
        _dirClient = _fsClient?.GetDirectoryClient(Path.GetRandomFileName());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Settings.Current.TestMode != TestMode.Skip)
        {
            await CreateResponseCacheProvider().ResetAsync();
        }

        _cachingMessageHandler?.Dispose();
        _cachingMessageHandler = null;
    }

    internal override bool ShouldSkip => Settings.Current.TestMode == TestMode.Skip;

    internal override IResponseCacheProvider CreateResponseCacheProvider()
        => new AzureStorageResponseCacheProvider(_dirClient!);

    internal override IResponseCacheProvider CreateResponseCacheProvider(Func<DateTime> provideDateTime)
        => new AzureStorageResponseCacheProvider(_dirClient!, timeToLiveForCacheEntries: null, provideDateTime);
}
