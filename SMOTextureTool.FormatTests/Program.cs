using SMOTextureTool.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Buffers.Binary;

var expectedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
{
    ["Alfea01.smo"] = 64,
    ["Alfea02.smo"] = 86,
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
        Assert(document.Textures.All(texture => texture.Material is not null),
            $"{name}: не для всех текстур найден контейнер spMaterialData.");
        Assert(document.Textures.All(texture =>
                texture.Material is
                {
                    MaterialRenderStates.Count: 11,
                    LayerTextureStates.Count: 9
                }),
            $"{name}: граф материала не содержит полные массивы состояний 11+9.");
        Assert(document.Textures.All(texture =>
                texture.Material is { } material &&
                material.ColorOperation == material.LayerTextureStates[1] &&
                material.AlphaOperation == material.LayerTextureStates[2] &&
                material.AddressU == material.LayerTextureStates[3] &&
                material.AddressV == material.LayerTextureStates[4] &&
                material.BorderColor == material.LayerTextureStates[5] &&
                material.Filter == material.LayerTextureStates[6] &&
                material.TextureCoordinateIndex == material.LayerTextureStates[7] &&
                material.TextureTransformFlags == material.LayerTextureStates[8]),
            $"{name}: именованные состояния текстурного слоя читаются неверно.");
        Assert(document.Textures.All(texture =>
                texture.ContentKind == TextureContentKind.Monochrome ==
                texture.Channels.RgbChannelsIdentical),
            $"{name}: тип содержимого не соответствует фактическим RGB-каналам.");
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

        if (name.Equals("Alfea02.smo", StringComparison.OrdinalIgnoreCase))
        {
            TextureInfo phoneTexture = document.Textures[34];
            using Image<Rgba32> phonePreview = document.Decode(phoneTexture);
            Assert(document.TryApplyVertexColors(
                    phoneTexture, phonePreview,
                    out VertexColorBindingInfo? phoneBinding),
                "Alfea02: не удалось применить цвета вершин к текстуре телефона №35.");
            Assert(phoneBinding is
                {
                    ModelOffset: 0x175314,
                    MeshOffset: 0x185421,
                    VertexCount: 474,
                    TriangleCount: 296,
                    InfluencingVertexIndices.Count: 474,
                    ConflictingPixelWrites: 18568
                },
                $"Alfea02: неверная привязка телефона: {phoneBinding}.");
            bool containsColor = false;
            phonePreview.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height && !containsColor; y++)
                {
                    foreach (Rgba32 pixel in accessor.GetRowSpan(y))
                    {
                        if (pixel.R != pixel.G || pixel.G != pixel.B)
                        {
                            containsColor = true;
                            break;
                        }
                    }
                }
            });
            Assert(containsColor,
                "Alfea02: окрашенный предпросмотр телефона остался монохромным.");
        }

        Console.WriteLine($"OK  {name,-20} textures={document.Textures.Count}");
    }

    Assert(expectedCounts.Count == Directory.EnumerateFiles(samples, "*.smo").Count(),
        "Набор Samples и таблица ожиданий расходятся.");

    string resizeSource = Path.Combine(samples, "butterfly.smo");
    byte[] resizeOriginal = File.ReadAllBytes(resizeSource);
    SmoDocument resizeDocument = SmoDocument.Parse(resizeOriginal);
    string resizedPng = Path.Combine(temporary, "resized-2048.png");
    using (var resizedImage = new Image<Rgba32>(2048, 2048, new Rgba32(20, 40, 60, 255)))
        resizedImage.SaveAsPng(resizedPng);
    byte[] resizedFile = resizeDocument.Repack(new Dictionary<int, string> { [1] = resizedPng });
    SmoDocument resizedDocument = SmoDocument.Parse(resizedFile);
    Assert(resizedDocument.Textures[0].Width == 2048 &&
           resizedDocument.Textures[0].Height == 2048,
        "Изменённые размеры текстуры не сохранились.");
    Assert(resizedFile.Length == resizeOriginal.Length + (2048 * 2048 - 64 * 64) * 4,
        "Итоговый размер SMO пересчитан неверно.");
    TextureInfo resizedTexture = resizedDocument.Textures[0];
    uint resizedPixelBytes = checked((uint)resizedTexture.PixelDataSize);
    Assert(ReadUInt32(resizedFile, resizedTexture.BlockOffset + 0x09) == resizedPixelBytes + 0x32 &&
           ReadUInt32(resizedFile, resizedTexture.BlockOffset + 0x1A) == resizedPixelBytes + 0x20 &&
           ReadUInt32(resizedFile, resizedTexture.BlockOffset + 0x1F) == resizedPixelBytes + 0x1A,
        "32-битные размеры вложенных ABGR-блоков записаны неверно.");

    if (args.Contains("--emit-alfea-2048", StringComparer.OrdinalIgnoreCase))
    {
        string alfeaSource = Path.Combine(samples, "Alfea01.smo");
        SmoDocument alfeaDocument = SmoDocument.Load(alfeaSource);
        string alfeaPng = Path.Combine(temporary, "alfea-texture-2048.png");
        using (Image<Rgba32> alfeaImage = alfeaDocument.Decode(alfeaDocument.Textures[0]))
        {
            alfeaImage.Mutate(context => context.Resize(2048, 2048));
            alfeaImage.SaveAsPng(alfeaPng);
        }

        byte[] alfeaHd = alfeaDocument.Repack(
            new Dictionary<int, string> { [1] = alfeaPng });
        string generated = Path.Combine(samples, "Generated");
        Directory.CreateDirectory(generated);
        string output = Path.Combine(generated, "Alfea01_hd_2048_test.smo");
        File.WriteAllBytes(output, alfeaHd);
        Console.WriteLine($"EMIT: {output}");
    }

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
    uint bloomPixelBytes = checked((uint)bloomBody.PixelDataSize);
    Assert(ReadUInt32(bloomHd, bloomBody.BlockOffset + 0x09) == bloomPixelBytes + 0x29 &&
           ReadUInt32(bloomHd, bloomBody.BlockOffset + 0x11) == bloomPixelBytes + 0x20 &&
           ReadUInt32(bloomHd, bloomBody.BlockOffset + 0x16) == bloomPixelBytes + 0x1A,
        "32-битные размеры вложенных BGRA-блоков записаны неверно.");
    Assert(ReadUInt32(bloomHd, bloomBody.BlockOffset + 0x1B) == 1024 &&
           ReadUInt32(bloomHd, bloomBody.BlockOffset + 0x1F) == 1024,
        "32-битные размеры BGRA-блока пересчитаны неверно.");

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

static uint ReadUInt32(byte[] data, int offset) =>
    BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
