using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Slon.Buffers;
using Slon.Pg.Types;

namespace Slon.Pg;

enum FlushMode
{
    None,
    Blocking,
    NonBlocking
}

// Probably publicly available.
class PgWriter
{
    StreamingWriter<IStreamingWriter<byte>> _writer; // mutable struct, don't make readonly.
    PgTypeCatalog _typeCatalog;
    bool _flushSuppressed;
    FlushMode _flushMode;

    internal StreamingWriter<IStreamingWriter<byte>> Writer => _writer;

    internal PgWriter(IStreamingWriter<byte> output)
    {
        _writer = new StreamingWriter<IStreamingWriter<byte>>(output);
        _typeCatalog = null!;
    }

    internal void Initialize(FlushMode flushMode, PgTypeCatalog typeCatalog)
    {
        if (_typeCatalog is not null)
            throw new InvalidOperationException("Invalid concurrent use or PgWriter was not reset properly.");

        _flushMode = flushMode;
        _typeCatalog = typeCatalog;
    }

    internal void Reset()
    {
        _flushMode = FlushMode.None;
        _typeCatalog = null!;
    }

    internal FlushMode FlushMode => _flushSuppressed ? FlushMode.None : _flushMode;

    public DataFormat DataFormat { get; internal set; }
    // TODO state shouldn't live here.
    public object? State { get; private set; }
    public ValueSize Size { get; private set; }
    // public int BytesWritten { get; }

    // These two should always be updated together, hence the separate method.
    public void UpdateState(object? state, ValueSize size)
    {
        State = state;
        Size = size;
    }

    internal void SuppressFlushes() => _flushSuppressed = true;
    internal void RestoreFlushes() => _flushSuppressed = false;


    // This method lives here to remove the chances oids will be cached on converters inadvertently when data type names should be used.
    // Such a mapping (for instance for array element oids) should be done per operation to ensure it is done in the context of a specific backend.
    public void WriteAsOid(PgTypeId pgTypeId)
    {
        var oid = _typeCatalog.GetOid(pgTypeId);
        WriteUInt32((uint)oid);
    }

    public void Flush(TimeSpan timeout = default)
    {
        if (FlushMode is FlushMode.None)
            return;

        if (FlushMode is FlushMode.NonBlocking)
            throw new NotSupportedException("Cannot call Flush on a non-blocking PgWriter, you might need to override WriteAsync on PgConverter if you want to call flush.");

        _writer.Flush(timeout);
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (FlushMode is FlushMode.None)
            return new ValueTask();

        if (FlushMode is FlushMode.Blocking)
            throw new NotSupportedException("Cannot call FlushAsync on a blocking PgWriter, call Flush instead.");

        return _writer.FlushAsync(cancellationToken);
    }
    // public abstract void Advance(int count);
    // public abstract Memory<byte> GetMemory(int sizeHint = 0);
    // public abstract Span<byte> GetSpan(int sizeHint = 0);

    public void WriteByte(byte value)
    {
        _writer.WriteByte(value);
    }

//
//     public void WriteInt32(short value);
    public void WriteInt32(int value)
    {
        _writer.WriteInt(value);
    }

    public void WriteInt64(long value)
    {
        throw new NotImplementedException();
    }

// #if !NETSTANDARD2_0
//     public void WriteInt32(Int128 value);
// #endif
//
//     public void WriteUInt32(ushort value);
    public void WriteUInt32(uint value)
    {
        _writer.WriteUInt(value);
    }
//     public void WriteUInt32(ulong value);
// #if !NETSTANDARD2_0
//     public void WriteUInt32(UInt128 value)
//     
// #endif

    public void WriteText(string value, Encoding encoding)
        => _writer.WriteEncoded(value.AsSpan(), encoding);

    public void WriteText(ReadOnlySpan<char> value, Encoding encoding)
        => _writer.WriteEncoded(value, encoding);

    public Encoder? WriteTextResumable(ReadOnlySpan<char> value, Encoding encoding, Encoder? encoder = null)
        => _writer.WriteEncodedResumable(value, encoding, encoder);

    // Make sure to loop and flush
    public void WriteRaw(ReadOnlySequence<byte> sequence)
    {

    }

    public ValueTask WriteRawAsync(ReadOnlySequence<byte> sequence, CancellationToken cancellationToken)
    {
        return new ValueTask();
    }

    internal void RestartWriter()
    {
        _writer = new StreamingWriter<IStreamingWriter<byte>>(_writer.Output);
    }

    public void WriteInt16(short value)
    {
        _writer.WriteShort(value);
    }
}

static class PgWriterExtensions
{
    public static ValueTask Flush(this PgWriter writer, bool async, CancellationToken cancellationToken = default)
    {
        if (async)
            return writer.FlushAsync(cancellationToken);

        writer.Flush();
        return new ValueTask();
    }
}
