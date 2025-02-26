// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using Microsoft.TestUtilities.Offline;
using Xunit;

namespace Microsoft.Extensions.AI.Evaluation.Reporting.Tests;

public class DiskBasedResultStoreTests : ResultStoreTester, IAsyncLifetime
{
    private readonly TempFolderProvider _tempFolders = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _tempFolders.Dispose();
        return Task.CompletedTask;
    }

    public override bool ShouldSkip => true;

    public override IResultStore CreateResultStore()
        => new DiskBasedResultStore(_tempFolders.UseTempStoragePath());

}
