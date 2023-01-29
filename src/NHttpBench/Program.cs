// Copyright (c) ClrCoder community. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using CommandLine;
using NHttpBench;
using System;
using System.Threading;

Parser.Default.ParseArguments<BenchCmdOptions>(args).WithParsed(
    o =>
    {
        var protocolVersionString = o.ProtocolVersion ?? "1.1";
        var protocolVersion = Version.Parse(protocolVersionString);
        Console.WriteLine($"KeepAlive = {o.KeepAlive}; HTTP {protocolVersionString}");
        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        using (var benchOperation = new NHttpBenchOperation(
                   o.Uri,
                   o.Connections,
                   o.Tasks,
                   o.Requests,
                   o.KeepAlive,
                   protocolVersion))
        {
            var task = benchOperation.Run(cancellationToken);

            ReadOnlySpan<BenchWorkItem> processedRequests;
            while (true)
            {
                Thread.Sleep(1000);
                processedRequests = benchOperation.GetProcessedRequests();
                Console.WriteLine(
                    $"Processed requests: {processedRequests.Length};   Active Requests: {benchOperation.ActiveWorkItems}");
                if (task.IsCompleted)
                {
                    break;
                }
            }

            processedRequests = benchOperation.GetProcessedRequests();
            var successfullyProcessed = 0;
            var minimalStartTime = double.PositiveInfinity;
            double maximalEndTime = 0;
            long totalTransferred = 0;
            for (var i = 0; i < processedRequests.Length; i++)
            {
                if (processedRequests[i].Exception == null)
                {
                    successfullyProcessed++;
                    minimalStartTime = Math.Min(processedRequests[i].StartInstant, minimalStartTime);
                    maximalEndTime = Math.Max(processedRequests[i].EndInstant, maximalEndTime);
                    totalTransferred += processedRequests[i].ContentLength;
                }
            }

            Console.WriteLine("---------------------------------------------------------------------");
            Console.WriteLine(
                $"Successfully Processed: {successfullyProcessed}. Errors: {processedRequests.Length - successfullyProcessed}");
            Console.WriteLine(
                $"Requests per second: {successfullyProcessed / (maximalEndTime - minimalStartTime):F3}");
            Console.WriteLine(
                $"Throughput: {totalTransferred / 1024.0 / 1024.0 / (maximalEndTime - minimalStartTime):F3} MB/s");
            task.GetAwaiter().GetResult();
        }
    });