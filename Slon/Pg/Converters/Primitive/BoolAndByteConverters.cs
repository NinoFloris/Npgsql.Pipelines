using System.Runtime.CompilerServices;

namespace Slon.Pg.Converters;

sealed class BoolConverter : PgBufferedConverter<bool>
{
    protected override bool ReadCore(PgReader reader) => reader.ReadByte() != 0;
    public override ValueSize GetSize(ref SizeContext context, bool value) => sizeof(byte);
    public override void Write(PgWriter writer, bool value) => writer.WriteByte(Unsafe.As<bool, byte>(ref value));
}

sealed class ByteConverter : PgBufferedConverter<byte>
{
    protected override byte ReadCore(PgReader reader) => reader.ReadByte();
    public override ValueSize GetSize(ref SizeContext context, byte value) => sizeof(byte);
    public override void Write(PgWriter writer, byte value) => writer.WriteByte(value);
}

sealed class SByteConverter : PgBufferedConverter<sbyte>
{
    protected override sbyte ReadCore(PgReader reader) => (sbyte)reader.ReadByte();
    public override ValueSize GetSize(ref SizeContext context, sbyte value) => sizeof(sbyte);
    public override void Write(PgWriter writer, sbyte value) => writer.WriteByte((byte)value);
}
