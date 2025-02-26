// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.TestUtilities.Offline;
using Xunit;

namespace Microsoft.Extensions.AI.Evaluation.Tests;

public class CachingMessageHandlerTests
{
    private class StaticMessageHandler : DelegatingHandler
    {
        private readonly HttpResponseMessage _response;
        public StaticMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    private class DoNotCallMessageHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("This handler should not be called");
        }
    }

    [Fact]
    public async Task TestWriteAndRead()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com")
        {
            Content = new StringContent("Hello")
        };

        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("World!")
        };

        using var staticHandler = new StaticMessageHandler(response);
        var cache = new MemoryDistributedCache(Options.Options.Create(new MemoryDistributedCacheOptions()));
        using var handler = new CachingMessageHandler(cache, online: true, staticHandler);
        using var client = new HttpClient(handler);

        // Simulate online request
        var result = await client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("World!", await result.Content.ReadAsStringAsync());

        // Go offline and try to get the cached response
        handler.Online = false;

        using var request2 = new HttpRequestMessage(HttpMethod.Post, "https://example.com")
        {
            Content = new StringContent("Hello")
        };

        var result2 = await client.SendAsync(request2);
        Assert.Equal(System.Net.HttpStatusCode.OK, result2.StatusCode);
        Assert.Equal("World!", await result2.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TestReadNotFound()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com")
        {
            Content = new StringContent("Hello")
        };

        using var donotcall = new DoNotCallMessageHandler();
        var cache = new MemoryDistributedCache(Options.Options.Create(new MemoryDistributedCacheOptions()));
        using var handler = new CachingMessageHandler(cache, online: false, donotcall);
        using var client = new HttpClient(handler);

        // Simulate online request
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Incorrect analyzer")]
    public async Task TestRequestHashUniqueness()
    {
        IEnumerable<HttpRequestMessage> requests = [
            new HttpRequestMessage(HttpMethod.Get, "https://example.com"),
            new HttpRequestMessage(HttpMethod.Get, "https://example.com"),
            new HttpRequestMessage(HttpMethod.Get, "https://example.com/something"),
            new HttpRequestMessage(HttpMethod.Post, "https://example.com"),
            new HttpRequestMessage(HttpMethod.Post, "https://example.com")
            {
                Content = new StringContent("Hello")
            },
            new HttpRequestMessage(HttpMethod.Post, "https://example.com")
            {
                Content = new StringContent("World")
            },
            new HttpRequestMessage(HttpMethod.Post, "https://example.com")
            {
                Content = new StringContent("World"),
                Headers = { { "Content-Type", "test/plain" } },
            },
        ];

        HashSet<string> hashes = [];

        foreach (var req in requests)
        {
            string hash = await req.GetRequestHashAsync(CancellationToken.None);
            Assert.True(hashes.Add(hash));
            req.Dispose();
        }
    }

}
