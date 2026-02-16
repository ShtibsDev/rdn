// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;

namespace Rdn
{
    // This class exists because the serializer needs to catch reader-originated exceptions in order to throw RdnException which has Path information.
    [Serializable]
    internal sealed class RdnReaderException : RdnException
    {
        public RdnReaderException(string message, long lineNumber, long bytePositionInLine) : base(message, path: null, lineNumber, bytePositionInLine)
        {
        }

#if NET
        [Obsolete(Obsoletions.LegacyFormatterImplMessage, DiagnosticId = Obsoletions.LegacyFormatterImplDiagId, UrlFormat = Obsoletions.SharedUrlFormat)]
#endif
        private RdnReaderException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
