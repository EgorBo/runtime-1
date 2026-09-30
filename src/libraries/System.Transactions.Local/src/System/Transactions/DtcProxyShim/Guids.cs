// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace System.Transactions.DtcProxyShim;

internal static class Guids
{
    internal const string IID_ITransactionDispenser = "3A6AD9E1-23B9-11cf-AD60-00AA00A74CCD";
    internal const string IID_IResourceManager = "13741D21-87EB-11CE-8081-0080C758527E";
    internal const string IID_ITransactionOutcomeEvents = "3A6AD9E2-23B9-11cf-AD60-00AA00A74CCD";
    internal const string IID_ITransaction = "0fb15084-af41-11ce-bd2b-204c4f4f5020";

    internal static Guid IID_ITransactionDispenser_Guid => new Guid(0x3A6AD9E1, 0x23B9, 0x11CF, 0xAD, 0x60, 0x00, 0xAA, 0x00, 0xA7, 0x4C, 0xCD);
    internal static Guid IID_IResourceManager_Guid => new Guid(0x13741D21, 0x87EB, 0x11CE, 0x80, 0x81, 0x00, 0x80, 0xC7, 0x58, 0x52, 0x7E);
    internal static Guid IID_ITransactionOutcomeEvents_Guid => new Guid(0x3A6AD9E2, 0x23B9, 0x11CF, 0xAD, 0x60, 0x00, 0xAA, 0x00, 0xA7, 0x4C, 0xCD);
    internal static Guid IID_ITransaction_Guid => new Guid(0x0FB15084, 0xAF41, 0x11CE, 0xBD, 0x2B, 0x20, 0x4C, 0x4F, 0x4F, 0x50, 0x20);
}
