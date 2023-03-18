using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Slon.Pg.Converters;

// NOTE: We don't inherit from ValueConverter to be able to avoid the virtual calls for ConvertFrom/ConvertTo.

/// A value converter that converts number types, it delegates all behavior to the effective converter.
sealed class NumberValueConverter<T, TEffective> : PgConverter<T>
#if !NETSTANDARD2_0
    where T : INumberBase<T> where TEffective : INumberBase<TEffective?>
#endif
{
    readonly PgConverter<TEffective> _effectiveConverter;
    public NumberValueConverter(PgConverter<TEffective> effectiveConverter)
        : base(effectiveConverter.DbNullPredicateKind is DbNullPredicate.Extended)
        => _effectiveConverter = effectiveConverter;

#if !NETSTANDARD2_0
    T? ConvertFrom(TEffective? value) => T.CreateChecked(value);
    [return: NotNull]
    TEffective ConvertTo([DisallowNull]T value) => TEffective.CreateChecked(value)!;
#else
// TODO
    T? ConvertFrom(TEffective? value) => throw new NotImplementedException();
    [return: NotNull]
    TEffective ConvertTo([DisallowNull]T value) => throw new NotImplementedException();
#endif

    protected override bool IsDbNull(T? value)
    {
        DebugShim.Assert(value is not null);
        return _effectiveConverter.IsDbNullValue(ConvertTo(value));
    }

    public override bool CanConvert(DataFormat format) => _effectiveConverter.CanConvert(format);

    public override ValueSize GetSize(ref SizeContext context, [DisallowNull]T value)
        => _effectiveConverter.GetSize(ref context, ConvertTo(value));

    public override T? Read(PgReader reader)
        => ConvertFrom(_effectiveConverter.Read(reader));

    public override void Write(PgWriter writer, [DisallowNull]T value)
        => _effectiveConverter.Write(writer, ConvertTo(value));
}
