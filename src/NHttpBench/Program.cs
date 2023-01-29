// Copyright (c) ClrCoder community. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace NHttpBench
{
    using System;
    using System.Threading;
    using CommandLine;

    internal class Program
    {
        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed(
                o =>
                {
                    string protocolVersionString = o.ProtocolVersion ?? "1.1";
                    var protocolVersion = Version.Parse(protocolVersionString);
                    Console.WriteLine($"KeepAlive = {o.KeepAlive}; HTTP {protocolVersionString}");
                    CancellationTokenSource cts = new CancellationTokenSource();
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
        }

        public class Options
        {
            [Option('c', "connections", Required = true, HelpText = "Number of concurrent connections.")]
            public int Connections { get; set; }

            [Option('t', "tasks", Required = true, HelpText = "Number of concurrent tasks connections.")]
            public int Tasks { get; set; }

            [Option('n', "num-requests", Required = true, HelpText = "Number of requests.")]
            public int Requests { get; set; }

            [Option('u', "uri", Required = true, HelpText = "The uri to request")]
            public string Uri { get; set; }

            [Option('k', "keep-alive", Required = false, HelpText = "Use keep alive mode")]
            public bool KeepAlive { get; set; }

            [Option('p', "protocol-version", Required = false, HelpText = "Protocol Version 1.0, 1.1 or 2.0")]
            public string? ProtocolVersion { get; set; }
        }
    }
}
