// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.TestUtilities.Offline;

#pragma warning disable CA1063 // Implement IDisposable Correctly

public class TempFolderProvider : IDisposable
{
    private readonly List<string> _tempStorage = [];

    public string UseTempStoragePath()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        _tempStorage.Add(path);
        return path;
    }

    ~TempFolderProvider() => Dispose();

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        lock (_tempStorage)
        {
            foreach (string path in _tempStorage)
            {
                try
                {
                    Directory.Delete(path, true);
                }
#pragma warning disable CA1031 // Do not catch general exception types.
                catch
#pragma warning restore CA1031
                {
                    // Best effort delete, don't crash on exceptions.
                }
            }

            _tempStorage.Clear();
        }
    }
}
