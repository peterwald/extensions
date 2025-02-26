// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace Microsoft.TestUtilities.Offline;

public class CachingMessageHandler : DelegatingHandler, IDisposable
{
    private readonly IDistributedCache _requestCache;
    public bool Online { get; set; }

    public CachingMessageHandler(IDistributedCache requestCache, bool online, HttpMessageHandler? innerHandler = null)
    {
        Online = online;
        _requestCache = requestCache;
        InnerHandler = innerHandler ?? new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string requestHash = await request.GetRequestHashAsync(cancellationToken);

        if (Online)
        {
            // dispatch request to the underlying handler
            var response = await base.SendAsync(request, cancellationToken);

            // save response to the cache
            var (responseBytes, cacheBytes) = await response.ToCacheBytesAsync(cancellationToken);
            await _requestCache.SetAsync(requestHash, cacheBytes, cancellationToken);

            // replace response contents with bytes now that we have read the stream and the stream can't be arbitrarily re-seeked
            response.Content = new ByteArrayContent(responseBytes);

            return response;
        }
        else
        {
            // lookup response in the cache
            var responseBytes = await _requestCache.GetAsync(requestHash, cancellationToken);
            if (responseBytes is not null)
            {
                return new ReadOnlySpan<byte>(responseBytes).ToHttpResponseMessage();
            }

            throw new InvalidOperationException($"No cached response found for request {requestHash}");
        }
    }

}
