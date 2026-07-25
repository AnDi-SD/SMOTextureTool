using System.Buffers.Binary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SMOTextureTool.Core;

public sealed class SmoDocument
{
    private static ReadOnlySpan<byte> TextureSignature =>
        [0x2B, 0x08, 0xEA, 0x78, 0x53, 0x42, 0x4F, 0x4F];

    private const int FileSizeOffset = 0x0C;
    private const int DataStartOffset = 0x14;
    private const int DataSizeOffset = 0x18;
    private const int MaximumDimension = 4096;

    private readonly byte[] _data;

    private SmoDocument(byte[] data, IReadOnlyList<TextureInfo> textures)
    {
        _data = data;
        Textures = textures;
    }

    public IReadOnlyList<TextureInfo> Textures { get; }
    public int Length => _data.Length;

    public static SmoDocument Load(string path) => Parse(File.ReadAllBytes(path));

    public static SmoDocument Parse(ReadOnlySpan<byte> source)
    {
        if (source.Length < 0x1C)
            throw new SmoFormatException("Файл слишком мал для заголовка SMO.");

        uint declaredFileSize = ReadUInt32(source, FileSizeOffset);
        uint dataStart = ReadUInt32(source, DataStartOffset);
        uint declaredDataSize = ReadUInt32(source, DataSizeOffset);

        if (declaredFileSize != source.Length)
            throw new SmoFormatException(
                $"Размер в заголовке ({declaredFileSize}) не совпадает с фактическим ({source.Length}).");

        if (dataStart > source.Length || declaredDataSize != source.Length - dataStart)
            throw new SmoFormatException("Некорректны границы секции данных SMO.");

        byte[] data = source.ToArray();
        var textures = FindTextures(data);
        return new SmoDocument(data, textures);
    }

    public Image<Rgba32> Decode(TextureInfo texture)
    {
        EnsureOwnedTexture(texture);
        var image = new Image<Rgba32>(texture.Width, texture.Height);
        int offset = texture.PixelDataOffset;

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < texture.Height; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < texture.Width; x++, offset += 4)
                {
                    row[x] = texture.Layout switch
                    {
                        TextureLayout.Abgr => new Rgba32(
                            _data[offset + 3], _data[offset + 2],
                            _data[offset + 1], _data[offset]),
                        TextureLayout.Bgra => new Rgba32(
                            _data[offset + 2], _data[offset + 1],
                            _data[offset], _data[offset + 3]),
                        _ => throw new SmoFormatException("Неизвестная раскладка пикселей.")
                    };
                }
            }
        });

        return image;
    }

    public void ExportTexture(TextureInfo texture, string path)
    {
        using Image<Rgba32> image = Decode(texture);
        image.SaveAsPng(path);
    }

    public void ExportAll(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (TextureInfo texture in Textures)
            ExportTexture(texture, Path.Combine(directory, texture.FileName));
    }

    public byte[] Repack(IReadOnlyDictionary<int, string> replacementFiles)
    {
        var replacements = new List<(TextureInfo Texture, Image<Rgba32> Image)>();
        try
        {
            foreach (TextureInfo texture in Textures)
            {
                if (!replacementFiles.TryGetValue(texture.Index, out string? path))
                    continue;

                Image<Rgba32> image = Image.Load<Rgba32>(path);
                ValidateReplacement(texture, image);
                replacements.Add((texture, image));
            }

            byte[] result = _data.ToArray();
            foreach ((TextureInfo texture, Image<Rgba32> image) in
                     replacements.OrderByDescending(item => item.Texture.PixelDataOffset))
            {
                result = ReplaceOne(result, texture, image);
            }

            PatchFileHeader(result);
            ValidateRepackedFile(result, Textures.Count, replacements);
            return result;
        }
        finally
        {
            foreach (var replacement in replacements)
                replacement.Image.Dispose();
        }
    }

    private static List<TextureInfo> FindTextures(byte[] data)
    {
        var result = new List<TextureInfo>();
        ReadOnlySpan<byte> span = data;
        int searchFrom = 0;

        while (searchFrom <= span.Length - TextureSignature.Length)
        {
            int relative = span[searchFrom..].IndexOf(TextureSignature);
            if (relative < 0)
                break;

            int blockOffset = searchFrom + relative;
            TextureInfo? texture = TryParseTexture(span, blockOffset, result.Count + 1);
            if (texture is not null)
                result.Add(texture);

            searchFrom = blockOffset + TextureSignature.Length;
        }

        return result;
    }

    private static TextureInfo? TryParseTexture(
        ReadOnlySpan<byte> data, int blockOffset, int index)
    {
        if (!CanRead(data, blockOffset + 0x08, 4))
            return null;

        ushort format = (ushort)(ReadUInt32(data, blockOffset + 0x08) & 0xFFFF);
        return format switch
        {
            0x32E3 or 0x43E3 => TryCreateTexture(
                data, index, blockOffset, blockOffset + 0x3C,
                blockOffset + 0x24, blockOffset + 0x28, format, TextureLayout.Abgr),
            0x29E3 => TryCreateTexture(
                data, index, blockOffset, blockOffset + 0x34,
                blockOffset + 0x28, blockOffset + 0x30, format, TextureLayout.Bgra),
            _ => null
        };
    }

    private static TextureInfo? TryCreateTexture(
        ReadOnlySpan<byte> data,
        int index,
        int blockOffset,
        int pixelOffset,
        int widthOffset,
        int heightOffset,
        ushort format,
        TextureLayout layout)
    {
        if (!CanRead(data, widthOffset, 4) || !CanRead(data, heightOffset, 4))
            return null;

        int width = ReadInt32(data, widthOffset);
        int height = ReadInt32(data, heightOffset);
        if (width is <= 0 or > MaximumDimension ||
            height is <= 0 or > MaximumDimension)
            return null;

        long size = (long)width * height * 4;
        if (pixelOffset < 0 || pixelOffset + size > data.Length)
            return null;

        return new TextureInfo(
            index, blockOffset, pixelOffset, width, height, format, layout);
    }

    private static byte[] ReplaceOne(
        byte[] data, TextureInfo texture, Image<Rgba32> image)
    {
        byte[] pixels = EncodePixels(image, texture.Layout);
        int oldEnd = texture.PixelDataOffset + texture.PixelDataSize;
        byte[] result = new byte[
            checked(data.Length - texture.PixelDataSize + pixels.Length)];

        data.AsSpan(0, texture.PixelDataOffset).CopyTo(result);
        pixels.CopyTo(result.AsSpan(texture.PixelDataOffset));
        data.AsSpan(oldEnd).CopyTo(
            result.AsSpan(texture.PixelDataOffset + pixels.Length));

        if (image.Width != texture.Width || image.Height != texture.Height)
            PatchResizableHeader(result, texture, image.Width, image.Height);

        return result;
    }

    private static byte[] EncodePixels(Image<Rgba32> image, TextureLayout layout)
    {
        byte[] result = new byte[checked(image.Width * image.Height * 4)];
        int offset = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < image.Height; y++)
            {
                ReadOnlySpan<Rgba32> row = accessor.GetRowSpan(y);
                foreach (Rgba32 pixel in row)
                {
                    if (layout == TextureLayout.Abgr)
                    {
                        result[offset] = pixel.A;
                        result[offset + 1] = pixel.B;
                        result[offset + 2] = pixel.G;
                        result[offset + 3] = pixel.R;
                    }
                    else
                    {
                        result[offset] = pixel.B;
                        result[offset + 1] = pixel.G;
                        result[offset + 2] = pixel.R;
                        result[offset + 3] = pixel.A;
                    }

                    offset += 4;
                }
            }
        });

        return result;
    }

    private static void ValidateReplacement(
        TextureInfo texture, Image<Rgba32> image)
    {
        if (image.Width is <= 0 or > MaximumDimension ||
            image.Height is <= 0 or > MaximumDimension)
            throw new SmoFormatException("Размер изображения находится вне диапазона 1–4096.");

        bool resized = image.Width != texture.Width || image.Height != texture.Height;
        if (resized && !texture.CanResize)
            throw new SmoFormatException(
                $"Текстуру {texture.Index} формата 0x{texture.FormatCode:X4} " +
                "можно заменить только изображением исходного размера.");

        if (resized && (!IsPowerOfTwo(image.Width) || !IsPowerOfTwo(image.Height)))
            throw new SmoFormatException(
                $"Размер текстуры {texture.Index} должен состоять из степеней двойки.");

        if (resized &&
            (image.Width > texture.MaxResizableDimension ||
             image.Height > texture.MaxResizableDimension))
            throw new SmoFormatException(
                $"Для подтверждённой схемы заголовка максимальный размер — " +
                $"{texture.MaxResizableDimension}×{texture.MaxResizableDimension}.");
    }

    private static void PatchResizableHeader(
        Span<byte> data, TextureInfo texture, int width, int height)
    {
        uint pixelCount = checked((uint)(width * height));
        uint areaDiv64 = pixelCount / 64;
        uint areaDiv16384 = pixelCount / 16384;
        int block = texture.BlockOffset;

        WriteUInt32(data, block + 0x08,
            texture.FormatCode | (areaDiv64 << 16));
        WriteUInt32(data, block + 0x1C, 0x1AE00000u | areaDiv16384);
        WriteUInt32(data, block + 0x20, 0x01000000u | areaDiv64);
        WriteUInt32(data, block + 0x24, (uint)width);
        WriteUInt32(data, block + 0x28, (uint)height);
        WriteUInt32(data, block + 0x2C, 0);
        WriteUInt32(data, block + 0x30, ((uint)width << 8) | 1);
        WriteUInt32(data, block + 0x34, (uint)width << 10);
        WriteUInt32(data, block + 0x38, (uint)width << 8);
    }

    private static void PatchFileHeader(Span<byte> data)
    {
        uint dataStart = ReadUInt32(data, DataStartOffset);
        WriteUInt32(data, FileSizeOffset, checked((uint)data.Length));
        WriteUInt32(data, DataSizeOffset, checked((uint)data.Length) - dataStart);
    }

    private static void ValidateRepackedFile(
        byte[] data,
        int expectedTextureCount,
        IReadOnlyCollection<(TextureInfo Texture, Image<Rgba32> Image)> replacements)
    {
        SmoDocument reparsed = Parse(data);
        if (reparsed.Textures.Count != expectedTextureCount)
            throw new SmoFormatException("После пересборки изменилась структура текстур.");

        foreach (var replacement in replacements)
        {
            TextureInfo? actual = reparsed.Textures.FirstOrDefault(
                item => item.Index == replacement.Texture.Index);
            if (actual is null ||
                actual.Width != replacement.Image.Width ||
                actual.Height != replacement.Image.Height)
                throw new SmoFormatException(
                    $"Не удалось проверить текстуру {replacement.Texture.Index} после пересборки.");
        }
    }

    private void EnsureOwnedTexture(TextureInfo texture)
    {
        if (texture.Index < 1 || texture.Index > Textures.Count ||
            Textures[texture.Index - 1] != texture)
            throw new ArgumentException("Текстура не принадлежит этому документу.", nameof(texture));
    }

    private static bool CanRead(ReadOnlySpan<byte> data, int offset, int length) =>
        offset >= 0 && length >= 0 && offset <= data.Length - length;

    private static bool IsPowerOfTwo(int value) => (value & (value - 1)) == 0;

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));

    private static void WriteUInt32(Span<byte> data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.Slice(offset, 4), value);
}
