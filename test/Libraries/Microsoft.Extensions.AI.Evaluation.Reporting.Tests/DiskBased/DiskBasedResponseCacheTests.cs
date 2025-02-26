// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using Microsoft.TestUtilities.Offline;
using Xunit;

namespace Microsoft.Extensions.AI.Evaluation.Reporting.Tests;

public class DiskBasedResponseCacheTests : ResponseCacheTester, IAsyncLifetime
{
    private readonly TempFolderProvider _tempFolders = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _tempFolders.Dispose();
        return Task.CompletedTask;
    }

    internal override bool ShouldSkip => true;

    internal override IResponseCacheProvider CreateResponseCacheProvider()
        => new DiskBasedResponseCacheProvider(_tempFolders.UseTempStoragePath());

    internal override IResponseCacheProvider CreateResponseCacheProvider(Func<DateTime> provideDateTime)
        => new DiskBasedResponseCacheProvider(_tempFolders.UseTempStoragePath(), provideDateTime);
}
