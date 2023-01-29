// Copyright (c) ClrCoder community. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace NHttpBench
{
    using JetBrains.Annotations;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public class NHttpBenchOperation : IDisposable
    {
        private readonly string _uri;
        private readonly bool _keepAlive;
        private readonly Version _protocolVersion;
        private readonly List<HttpClient> _httpClients = new List<HttpClient>();
        private readonly BenchWorkItem[] _workItems;

        private int _nextWorkItemToProcess;
        private readonly Stopwatch _stopwatch;
        private int _processedItems;
        private int _activeWorkItems;

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Reviewed")]
        public NHttpBenchOperation(
            string uri,
            int connectionsCount,
            int tasksCount,
            int requestsCount,
            bool keepAlive,
            Version protocolVersion)
        {
            ServicePointManager.DefaultConnectionLimit = connectionsCount;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;
            _uri = uri;
            _keepAlive = keepAlive;
            _protocolVersion = protocolVersion;
            try
            {
                HttpMessageHandler messageHandler;
#if NETFRAMEWORK
                var handler = new HttpClientHandler();
                handler.MaxConnectionsPerServer = connectionsCount;
                handler.CheckCertificateRevocationList = false;
                handler.UseCookies = false;
                handler.UseProxy = false;
                messageHandler = handler;
#else
                var handler = new SocketsHttpHandler();
                handler.MaxConnectionsPerServer = connectionsCount;
                handler.UseCookies = false;
                handler.UseProxy = false;

                messageHandler = handler;
#endif
                var globalClient = new HttpClient(messageHandler, false);
                globalClient.Timeout = TimeSpan.FromMinutes(1);
                for (var i = 0; i < tasksCount; i++)
                {
                    var client = globalClient;
                    if (!keepAlive)
                    {
                        client.DefaultRequestHeaders.Connection.Add("close");
                    }

                    _httpClients.Add(client);
                }

                _stopwatch = new Stopwatch();
                _workItems = new BenchWorkItem[requestsCount];
            }
            catch
            {
                ReleaseResources();
                throw;
            }
        }

        public int ActiveWorkItems => _activeWorkItems;

        /// <inheritdoc />
        public void Dispose()
        {
            ReleaseResources();
        }

        public Task Run(CancellationToken cancellationToken)
        {
            _stopwatch.Start();
            return ParallelForEachAsync(cancellationToken);
        }

        public ReadOnlySpan<BenchWorkItem> GetProcessedRequests()
        {
            return new ReadOnlySpan<BenchWorkItem>(_workItems, 0, _processedItems);
        }

        [ContractAnnotation("task:null=>null; task:notnull => notnull")]
        private static Task? EnsureStarted(Task? task)
        {
            if (task == null)
            {
                return null;
            }

            if (task.Status == TaskStatus.WaitingToRun)
            {
                task.Start();
            }

            return task;
        }

        private async Task ProcessWorkItem(HttpClient httpClient, int itemIndex)
        {
            try
            {
                _workItems[itemIndex].StartInstant = _stopwatch.Elapsed.TotalSeconds;
                using var message = new HttpRequestMessage(HttpMethod.Get, _uri)
                {
                    Version = _protocolVersion,
                };

                using var responseMessage = await httpClient.SendAsync(message).ConfigureAwait(false);
                using var lengthCounterStream = new LengthCounterStream();
                await responseMessage.Content.CopyToAsync(lengthCounterStream).ConfigureAwait(false);
                _workItems[itemIndex].ContentLength = lengthCounterStream.Length;
                responseMessage.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _workItems[itemIndex].Exception = ex;
            }
            finally
            {
                _workItems[itemIndex].EndInstant = _stopwatch.Elapsed.TotalSeconds;
            }
        }

        private Task ParallelForEachAsync(
            CancellationToken cancellationToken = default,
            TaskScheduler? scheduler = null)
        {
            if (scheduler == null)
            {
                scheduler = TaskScheduler.Default;
            }

            async Task WorkSequenceProc(HttpClient httpClient)
            {
                while (true)
                {
                    var itemIndex = Interlocked.Increment(ref _nextWorkItemToProcess) - 1;
                    if (itemIndex >= _workItems.Length)
                    {
                        break;
                    }

                    var isYieldRequired = false;
                    try
                    {
                        Task t = null;
                        try
                        {
                            Interlocked.Increment(ref _activeWorkItems);
                            t = ProcessWorkItem(httpClient, itemIndex);
                        }
                        catch
                        {
                            // Do nothing.
                        }

                        isYieldRequired = t == null || t.IsCompleted;

                        if (t != null)
                        {
                            await t;
                        }
                    }
                    catch
                    {
                        // Do nothing.
                    }

                    Interlocked.Decrement(ref _activeWorkItems);
                    Interlocked.Increment(ref _processedItems);
                    if (isYieldRequired)
                    {
                        await Task.Yield();
                    }
                }
            }

            var tasks = new Task[_httpClients.Count];

            for (var i = 0; i < tasks.Length; i++)
            {
                var httpClient = _httpClients[i];
                tasks[i] = Task.Factory.StartNew(
                    () => WorkSequenceProc(httpClient),
                    cancellationToken,
                    TaskCreationOptions.None,
                    scheduler).Unwrap();
            }

            return Task.WhenAll(tasks);
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Review")]
        private void ReleaseResources()
        {
            while (_httpClients.Any())
            {
                var client = _httpClients[_httpClients.Count - 1];
                try
                {
                    client.Dispose();
                }
                catch
                {
                    // Do nothing.
                }

                _httpClients.RemoveAt(_httpClients.Count - 1);
            }
        }
    }
}
