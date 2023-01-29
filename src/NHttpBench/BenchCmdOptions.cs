// Copyright (c) ClrCoder community. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace NHttpBench
{
    using CommandLine;

    internal class BenchCmdOptions
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
