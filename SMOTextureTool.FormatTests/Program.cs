using SMOTextureTool.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Buffers.Binary;

var expectedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
{
    ["Alfea01.smo"] = 64,
    ["bloom_ball.smo"] = 2,
    ["Bloom_body.smo"] = 2,
    ["bloom_goth.smo"] = 2,
    ["bloom_hair.smo"] = 1,
    ["bloom_jeans.smo"] = 2,
    ["bloom_school.smo"] = 2,
    ["book.smo"] = 1,
    ["butterfly.smo"] = 1,
    ["Droid.smo"] = 1,
    ["fish.smo"] = 1,
    ["Griffin.smo"] = 2,
    ["Grizelda.smo"] = 3
};

string samples = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal))
    ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Samples"));
string temporary = Path.Combine(Path.GetTempPath(), $"smo-format-tests-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporary);

try
{
    int checkedTextures = 0;
    var checkedLayouts = new HashSet<(TextureLayout Layout, int Width, int Height)>();

    foreach (string file in Directory.EnumerateFiles(samples, "*.smo").Order())
    {
        byte[] original = File.ReadAllBytes(file);
        SmoDocument document = SmoDocument.Parse(original);
        string name = Path.GetFileName(file);

        Assert(expectedCounts.TryGetValue(name, out int expected), $"Нет эталона для {name}.");
        Assert(document.Textures.Count == expected,
            $"{name}: ожидалось {expected} текстур, найдено {document.Textures.Count}.");
        Assert(document.Repack(new Dictionary<int, string>()).SequenceEqual(original),
            $"{name}: пересборка без замен изменила файл.");

        foreach (TextureInfo texture in document.Textures)
        {
            if (!checkedLayouts.Add((texture.Layout, texture.Width, texture.Height)))
                continue;

            string png = Path.Combine(temporary, $"{name}-{texture.Index}.png");
            document.ExportTexture(texture, png);
            byte[] repacked = document.Repack(new Dictionary<int, string>
                { [texture.Index] = png });
            Assert(repacked.SequenceEqual(original),
                $"{name}, текстура {texture.Index}: round-trip изменил байты.");
            checkedTextures++;
        }

        Console.WriteLine($"OK  {name,-20} textures={document.Textures.Count}");
    }

    Assert(expectedCounts.Count == Directory.EnumerateFiles(samples, "*.smo").Count(),
        "Набор Samples и таблица ожиданий расходятся.");

    string resizeSource = Path.Combine(samples, "butterfly.smo");
    byte[] resizeOriginal = File.ReadAllBytes(resizeSource);
    SmoDocument resizeDocument = SmoDocument.Parse(resizeOriginal);
    string resizedPng = Path.Combine(temporary, "resized-512x256.png");
    using (var resizedImage = new Image<Rgba32>(512, 256, new Rgba32(20, 40, 60, 255)))
        resizedImage.SaveAsPng(resizedPng);
    byte[] resizedFile = resizeDocument.Repack(new Dictionary<int, string> { [1] = resizedPng });
    SmoDocument resizedDocument = SmoDocument.Parse(resizedFile);
    Assert(resizedDocument.Textures[0].Width == 512 && resizedDocument.Textures[0].Height == 256,
        "Изменённые размеры текстуры не сохранились.");
    Assert(resizedFile.Length == resizeOriginal.Length + (512 * 256 - 64 * 64) * 4,
        "Итоговый размер SMO пересчитан неверно.");

    string bloomSource = Path.Combine(samples, "Bloom_body.smo");
    byte[] bloomOriginal = File.ReadAllBytes(bloomSource);
    SmoDocument bloomDocument = SmoDocument.Parse(bloomOriginal);
    string bodyPng = Path.Combine(temporary, "bloom-body-1024.png");
    string eyePng = Path.Combine(temporary, "bloom-eye-256.png");
    using (Image<Rgba32> bodyImage = bloomDocument.Decode(bloomDocument.Textures[0]))
    {
        bodyImage.Mutate(context => context.Resize(1024, 1024));
        bodyImage.SaveAsPng(bodyPng);
    }
    using (Image<Rgba32> eyeImage = bloomDocument.Decode(bloomDocument.Textures[1]))
    {
        eyeImage.Mutate(context => context.Resize(256, 256));
        eyeImage.SaveAsPng(eyePng);
    }

    byte[] bloomHd = bloomDocument.Repack(new Dictionary<int, string>
    {
        [1] = bodyPng,
        [2] = eyePng
    });
    SmoDocument bloomHdDocument = SmoDocument.Parse(bloomHd);
    Assert(bloomHdDocument.Textures[0].Width == 1024 &&
           bloomHdDocument.Textures[0].Height == 1024,
        "Основная BGRA-текстура Bloom не получила размер 1024×1024.");
    Assert(bloomHdDocument.Textures[1].Width == 256 &&
           bloomHdDocument.Textures[1].Height == 256,
        "BGRA-текстура глаза Bloom не получила размер 256×256.");

    TextureInfo bloomBody = bloomHdDocument.Textures[0];
    Assert(ReadUInt16(bloomHd, bloomBody.BlockOffset + 0x0A) == 16384 &&
           ReadUInt16(bloomHd, bloomBody.BlockOffset + 0x12) == 16384 &&
           ReadUInt16(bloomHd, bloomBody.BlockOffset + 0x17) == 16384,
        "Счётчики BGRA-блока пересчитаны неверно.");
    Assert(ReadUInt16(bloomHd, bloomBody.BlockOffset + 0x1B) == 1024 &&
           ReadUInt16(bloomHd, bloomBody.BlockOffset + 0x1F) == 1024,
        "16-битные размеры BGRA-блока пересчитаны неверно.");

    if (args.Contains("--emit-bloom-hd", StringComparer.OrdinalIgnoreCase))
    {
        string generated = Path.Combine(samples, "Generated");
        Directory.CreateDirectory(generated);
        string output = Path.Combine(generated, "Bloom_body_hd_test.smo");
        File.WriteAllBytes(output, bloomHd);
        Console.WriteLine($"EMIT: {output}");
    }

    Console.WriteLine(
        $"PASS: {expectedCounts.Count} файлов, {checkedTextures} вариантов round-trip, " +
        "BGRA HD 1024×1024.");
    return 0;
}
finally
{
    Directory.Delete(temporary, recursive: true);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static ushort ReadUInt16(byte[] data, int offset) =>
    BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
