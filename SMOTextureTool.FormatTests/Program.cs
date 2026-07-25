using SMOTextureTool.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

var expectedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
{
    ["Alfea01.smo"] = 64,
    ["bloom_ball.smo"] = 2,
    ["Bloom_body.smo"] = 2,
    ["bloom_goth.smo"] = 2,
    ["bloom_school.smo"] = 2,
    ["book.smo"] = 1,
    ["butterfly.smo"] = 1,
    ["Droid.smo"] = 1,
    ["fish.smo"] = 1,
    ["Griffin.smo"] = 2,
    ["Grizelda.smo"] = 3
};

string samples = args.FirstOrDefault()
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

    Console.WriteLine($"PASS: {expectedCounts.Count} файлов, {checkedTextures} вариантов round-trip.");
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
