using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Slon.Pg.Converters;
using Slon.Pg.Types;

namespace Slon.Pg;

class DefaultConverterInfoResolver: IPgConverterInfoResolver
{
    static ReadOnlyMemoryTextConverter? _romTextConverter;

    static readonly PgConverterFactory[] ConverterFactories = {
        new ArrayConverterFactory()
    };

    [RequiresUnreferencedCode("Reflection used for pg type conversions.")]
    public PgConverterInfo? GetConverterInfo(Type? type, DataTypeName? dataTypeName, PgConverterOptions options)
    {
        if (type is null && dataTypeName is null)
            throw new InvalidOperationException($"At miminum one non-null {nameof(type)} or {nameof(dataTypeName)} is required.");

        // Default mappings.
        var (defaultType, defaultName) = (type, dataTypeName) switch
        {
            // The typed default is important to get all the DataTypeName values lifted into a nullable. Don't simplify to default.
            // Moving the types to the destructure also won't work because a default value is allowed to assign to a nullable of that type.
            (null, null) => default((Type?, DataTypeName?)),
            _ when type == typeof(int) || dataTypeName == DataTypeNames.Int4 => (typeof(int), DataTypeNames.Int4),
            _ when type == typeof(long) || dataTypeName == DataTypeNames.Int8 => (typeof(long), DataTypeNames.Int8),
            _ when type == typeof(short) || dataTypeName == DataTypeNames.Int2 => (typeof(short), DataTypeNames.Int2),
            _ when type == typeof(string) || dataTypeName == DataTypeNames.Text => (typeof(string), DataTypeNames.Text),
            _ => default
        };
        type ??= defaultType;
        dataTypeName ??= defaultName;
        // Either we could find defaults for a given DataTypeName *or* a clr type MUST have been passed for us to do anything.
        if (type is null)
            return null;
        // We want defaultness to be intrinsic to the mapping, not just a result of the absence of a clr type.
        // So (null, DataTypeName.Int4), (typeof(int), null), (typeof(int), DataTypeName.Int4) should all return a default info.
        var isDefaultInfo = dataTypeName is null ? type == defaultType : type == defaultType && dataTypeName == defaultName;

        // Numeric converters.
        // We're using dataTypeName.Value when there is a default mapping to make sure everything stays in sync (or throws).
        // If there is no default name for the clr type we have to provide one, when making a type default be sure to replace it here with .Value.
        var numericInfo = type switch
        {
            _ when type == typeof(int) => CreateNumberInfo<int>(dataTypeName!.Value, () => new Int32Converter()),
            _ when type == typeof(long) => CreateNumberInfo<long>(dataTypeName!.Value, () => new Int64Converter()),
            _ when type == typeof(short) => CreateNumberInfo<short>(dataTypeName!.Value, () => new Int16Converter()),
            _ when type == typeof(byte) => CreateNumberInfo<byte>(dataTypeName ?? DataTypeNames.Int2, null),
            _ => null
        };
        if (numericInfo is not null)
            return numericInfo;

        // Text converters.
        var textInfo = type switch
        {
            _ when type == typeof(string) => CreateTextInfo(new StringTextConverter(_romTextConverter ??= new ReadOnlyMemoryTextConverter())),
            _ when type == typeof(char[]) => CreateTextInfo(new CharArrayTextConverter(_romTextConverter ??= new ReadOnlyMemoryTextConverter())),
            _ when type == typeof(ReadOnlyMemory<char>) => CreateTextInfo(_romTextConverter ??= new ReadOnlyMemoryTextConverter()),
            _ when type == typeof(ArraySegment<char>) => CreateTextInfo(new CharArraySegmentTextConverter(_romTextConverter ??= new ReadOnlyMemoryTextConverter())),
            _ when type == typeof(char) => CreateTextInfo(new CharTextConverter()),
            _ => null
        };
        if (textInfo is not null)
            return textInfo;

        foreach (var factory in ConverterFactories)
            if (factory.CreateConverterInfo(type, options, dataTypeName is null ? null : new(dataTypeName.GetValueOrDefault())) is { } converterInfo)
            {
                // TODO validate returned info.
                return converterInfo;
            }

        return null;

        PgConverterInfo CreateTextInfo<T>(PgConverter<T> converter)
            => PgConverterInfo.Create(options, converter, DataTypeNames.Text, isDefaultInfo, DataRepresentation.Text);

        PgConverterInfo? CreateNumberInfo<T>(DataTypeName dataTypeName, Func<PgConverter<T>>? defaultConverterFunc)
#if !NETSTANDARD2_0
            where T : INumberBase<T>
#endif
            => this.CreateNumberInfo(dataTypeName, defaultConverterFunc, isDefaultInfo, options);
    }

    PgConverterInfo? CreateNumberInfo<T>(DataTypeName dataTypeName, Func<PgConverter<T>>? defaultConverterFunc, bool isDefaultInfo, PgConverterOptions options)
#if !NETSTANDARD2_0
        where T : INumberBase<T>
#endif
    {
        if (isDefaultInfo && defaultConverterFunc is null)
            throw new InvalidOperationException();

        PgConverter<T>? converter = null;
        if (isDefaultInfo)
            converter = defaultConverterFunc!();

        // Explicit conversions.
        converter ??= dataTypeName switch
        {
            _ when dataTypeName == DataTypeNames.Int2 => new NumberValueConverter<T, short>(new Int16Converter()),
            _ when dataTypeName == DataTypeNames.Int4 => new NumberValueConverter<T, int>(new Int32Converter()),
            // TODO
            // DataTypeNames.Float4
            // DataTypeNames.Float8
            // DataTypeNames.Numeric
            _ => null
        };

        return converter is not null ? PgConverterInfo.Create(options, converter, dataTypeName, isDefaultInfo) : null;
    }
}
