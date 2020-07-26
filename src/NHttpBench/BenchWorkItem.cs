// Copyright (c) ClrCoder community. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace NHttpBench
{
    using System;

    public struct BenchWorkItem
    {
        public double StartInstant;
        public double EndInstant;
        public Exception? Exception;
        public int ContentLength;
    }
}
