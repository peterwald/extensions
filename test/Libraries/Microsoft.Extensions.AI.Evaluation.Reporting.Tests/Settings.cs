// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.AI.Evaluation.Reporting.Tests;

public enum TestMode
{
    Skip,       // Skip all online tests
    Offline,    // Run tests offline with cached data
    Online,     // Run tests online with live services
}

public class Settings
{
    public TestMode TestMode { get; }
    public string OfflineCachePath { get; } = string.Empty;
    public string StorageAccountEndpoint { get; }
    public string StorageContainerName { get; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "N/A")]
    public Settings(IConfiguration config)
    {
        TestMode = config.GetValue<TestMode>("TestMode", TestMode.Skip);

        OfflineCachePath = config.GetValue<string>("OfflineCachePath") ?? string.Empty;

        if (TestMode != TestMode.Skip && string.IsNullOrEmpty(OfflineCachePath))
        {
            throw new InvalidOperationException("OfflineCachePath must be set when running offline or online tests.");
        }

        OfflineCachePath = GetPathRelativeToRepoRoot(OfflineCachePath);

        StorageAccountEndpoint =
            config.GetValue<string>("StorageAccountEndpoint")
            ?? throw new ArgumentNullException(nameof(StorageAccountEndpoint));

        StorageContainerName =
            config.GetValue<string>("StorageContainerName")
            ?? throw new ArgumentNullException(nameof(StorageContainerName));
    }

    private static Settings? _currentSettings;

    public static Settings Current
    {
        get
        {
            _currentSettings ??= GetCurrentSettings();
            return _currentSettings;
        }
    }

    private static Settings GetCurrentSettings()
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        return new Settings(config);
    }

    private static string GetPathRelativeToRepoRoot(string filePath, [CallerFilePath] string? thisfilepath = null)
    {
        if (Path.IsPathRooted(filePath))
        {
            return filePath;
        }

        // NOTE! This gets the path relative to the repo root by using the location of this file in the tree.
        // If this file is moved, the path below will need to be updated.
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisfilepath) ?? "", "..", "..", "..", filePath));
    }

}
