// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn.Serialization
{
    public partial class RdnConverter<T>
    {
        internal bool WriteCore(
            Utf8RdnWriter writer,
            in T? value,
            RdnSerializerOptions options,
            ref WriteStack state)
        {
            try
            {
                return TryWrite(writer, value, options, ref state);
            }
            catch (Exception ex)
            {
                if (!state.SupportAsync)
                {
                    // Async serializers should dispose sync and
                    // async disposables from the async root method.
                    state.DisposePendingDisposablesOnException();
                }

                switch (ex)
                {
                    case InvalidOperationException when ex.Source == ThrowHelper.ExceptionSourceValueToRethrowAsRdnException:
                        ThrowHelper.ReThrowWithPath(ref state, ex);
                        break;

                    case RdnException { Path: null } rdnException:
                        // RdnExceptions where the Path property is already set
                        // typically originate from nested calls to RdnSerializer;
                        // treat these cases as any other exception type and do not
                        // overwrite any exception information.
                        ThrowHelper.AddRdnExceptionInformation(ref state, rdnException);
                        break;

                    case NotSupportedException when !ex.Message.Contains(" Path: "):
                        // If the message already contains Path, just re-throw. This could occur in serializer re-entry cases.
                        // To get proper Path semantics in re-entry cases, APIs that take 'state' need to be used.
                        ThrowHelper.ThrowNotSupportedException(ref state, ex);
                        break;
                }

                throw;
            }
        }
    }
}
