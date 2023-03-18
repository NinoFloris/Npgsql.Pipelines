using System.Threading;
using System.Threading.Tasks;

namespace Slon.Pg.Converters;

// NULL writing is always responsibility of the caller writing the length, so there is not much we do here.
// NOTE: We don't inherit from ValueConverter to be able to avoid the virtual calls for ConvertFrom/ConvertTo.

/// Special value converter to be able to use struct converters as System.Nullable converters, it delegates all behavior to the effective converter.
sealed class NullableValueConverter<T> : PgConverter<T?> where T : struct
{
    readonly PgConverter<T> _effectiveConverter;
    public NullableValueConverter(PgConverter<T> effectiveConverter)
        : base(FromDelegatedDbNullPredicate(effectiveConverter.DbNullPredicate, typeof(T)))
        => _effectiveConverter = effectiveConverter;

    T? ConvertFrom(T value) => value;
    T ConvertTo(T? value) => value.GetValueOrDefault();

    protected override bool IsDbNull(T? value)
        => _effectiveConverter.IsDbNullValue(ConvertTo(value));

    public override bool CanConvert(DataFormat format) => _effectiveConverter.CanConvert(format);

    public override ValueSize GetSize(SizeContext context, T? value, ref object? writeState)
        => _effectiveConverter.GetSize(context, ConvertTo(value), ref writeState);

    public override T? Read(PgReader reader)
        => ConvertFrom(_effectiveConverter.Read(reader));

    public override ValueTask<T?> ReadAsync(PgReader reader, CancellationToken cancellationToken = default)
    {
        var task = _effectiveConverter.ReadAsync(reader, cancellationToken);
        return task.IsCompletedSuccessfully ? new(ConvertFrom(task.GetAwaiter().GetResult())) : Core(task);

        async ValueTask<T?> Core(ValueTask<T> task) => ConvertFrom(await task);
    }

    public override void Write(PgWriter writer, T? value)
        => _effectiveConverter.Write(writer, ConvertTo(value));

    public override ValueTask WriteAsync(PgWriter writer, T? value, CancellationToken cancellationToken = default)
        => _effectiveConverter.WriteAsync(writer, ConvertTo(value), cancellationToken);
}
