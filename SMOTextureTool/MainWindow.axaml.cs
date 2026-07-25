using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using SMOTextureTool.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SMOTextureTool;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private static readonly FilePickerFileType SmoType = new("SMO model")
        { Patterns = ["*.smo"] };
    private static readonly FilePickerFileType ImageType = new("Images")
        { Patterns = ["*.png", "*.bmp", "*.jpg", "*.jpeg"] };

    private SmoDocument? _document;
    private string? _sourcePath;
    private string _status = "Откройте исходный файл модели.";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public ObservableCollection<TextureRowViewModel> Rows { get; } = [];
    public string FileName => _sourcePath is null ? "Файл не открыт" : Path.GetFileName(_sourcePath);
    public string Summary => _document is null
        ? "—"
        : $"{_document.Textures.Count} текстур · {_document.Length:N0} байт · " +
          $"{Rows.Count(row => row.HasReplacement)} замен";
    public bool HasDocument => _document is not null;
    public bool HasReplacements => Rows.Any(row => row.HasReplacement);
    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    private async void OpenFile_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Открыть SMO", AllowMultiple = false, FileTypeFilter = [SmoType]
        });
        if (files.Count == 0)
            return;

        try
        {
            string path = files[0].Path.LocalPath;
            SmoDocument document = SmoDocument.Load(path);
            ClearRows();
            _document = document;
            _sourcePath = path;
            foreach (TextureInfo texture in document.Textures)
            {
                using SixLabors.ImageSharp.Image<Rgba32> image = document.Decode(texture);
                Rows.Add(new TextureRowViewModel
                {
                    Texture = texture,
                    OriginalPreview = ToBitmap(image)
                });
            }
            Status = document.Textures.Count == 0
                ? "Поддерживаемые текстуры не найдены."
                : "Файл разобран и проверен.";
            NotifyDocumentState();
        }
        catch (Exception ex)
        {
            Status = $"Не удалось открыть файл: {ex.Message}";
        }
    }

    private async void ExtractAll_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null)
            return;
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Папка для текстур", AllowMultiple = false });
        if (folders.Count == 0)
            return;
        try
        {
            _document.ExportAll(folders[0].Path.LocalPath);
            Status = $"Экспортировано текстур: {_document.Textures.Count}.";
        }
        catch (Exception ex)
        {
            Status = $"Ошибка экспорта: {ex.Message}";
        }
    }

    private async void SelectFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null)
            return;
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Папка с заменами", AllowMultiple = false });
        if (folders.Count == 0)
            return;

        int found = 0;
        foreach (TextureRowViewModel row in Rows)
        {
            string path = Path.Combine(folders[0].Path.LocalPath, row.Texture.FileName);
            if (!File.Exists(path))
                continue;
            row.SetReplacement(path, new Bitmap(path));
            found++;
        }
        Status = $"Найдено замен: {found} из {Rows.Count}.";
        NotifyReplacementState();
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null || _sourcePath is null)
            return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить новый SMO",
            SuggestedFileName = Path.GetFileNameWithoutExtension(_sourcePath) + "_mod.smo",
            DefaultExtension = "smo", FileTypeChoices = [SmoType]
        });
        if (file is null)
            return;
        try
        {
            var replacements = Rows.Where(row => row.ReplacementPath is not null)
                .ToDictionary(row => row.Texture.Index, row => row.ReplacementPath!);
            byte[] result = _document.Repack(replacements);
            await File.WriteAllBytesAsync(file.Path.LocalPath, result);
            Status = $"Новый SMO сохранён и повторно проверен ({result.Length:N0} байт).";
        }
        catch (Exception ex)
        {
            Status = $"Пересборка отменена: {ex.Message}";
        }
    }

    private async void ExportOne_Click(object? sender, RoutedEventArgs e)
    {
        if (_document is null || sender is not Button { Tag: TextureRowViewModel row })
            return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить текстуру", SuggestedFileName = row.Texture.FileName,
            DefaultExtension = "png"
        });
        if (file is null)
            return;
        try
        {
            _document.ExportTexture(row.Texture, file.Path.LocalPath);
            Status = $"Сохранено: {row.Texture.FileName}";
        }
        catch (Exception ex)
        {
            Status = $"Ошибка экспорта: {ex.Message}";
        }
    }

    private async void ChooseReplacement_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TextureRowViewModel row })
            return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Замена для {row.Texture.FileName}", AllowMultiple = false,
            FileTypeFilter = [ImageType]
        });
        if (files.Count == 0)
            return;
        try
        {
            string path = files[0].Path.LocalPath;
            using SixLabors.ImageSharp.Image<Rgba32> image =
                SixLabors.ImageSharp.Image.Load<Rgba32>(path);
            ValidateDimensions(row.Texture, image.Width, image.Height);
            row.SetReplacement(path, ToBitmap(image));
            Status = $"Выбрана замена для текстуры {row.Texture.Index}.";
            NotifyReplacementState();
        }
        catch (Exception ex)
        {
            Status = $"Изображение не принято: {ex.Message}";
        }
    }

    private void ResetReplacement_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TextureRowViewModel row })
            return;
        row.ResetReplacement();
        Status = $"Замена текстуры {row.Texture.Index} сброшена.";
        NotifyReplacementState();
    }

    protected override void OnClosed(EventArgs e)
    {
        ClearRows();
        base.OnClosed(e);
    }

    private static void ValidateDimensions(TextureInfo texture, int width, int height)
    {
        bool resized = width != texture.Width || height != texture.Height;
        if (resized && !texture.CanResize)
            throw new SmoFormatException("Для этого формата требуется исходный размер.");
        if (resized && (!IsPowerOfTwo(width) || !IsPowerOfTwo(height)))
            throw new SmoFormatException("Стороны нового изображения должны быть степенями двойки.");
        if (resized &&
            (width > texture.MaxResizableDimension || height > texture.MaxResizableDimension))
            throw new SmoFormatException(
                $"Максимальный подтверждённый размер — {texture.MaxResizableDimension}×" +
                $"{texture.MaxResizableDimension}.");
    }

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;

    private static Bitmap ToBitmap(SixLabors.ImageSharp.Image<Rgba32> image)
    {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }

    private void ClearRows()
    {
        foreach (TextureRowViewModel row in Rows)
            row.Dispose();
        Rows.Clear();
    }

    private void NotifyDocumentState()
    {
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(HasReplacements));
    }

    private void NotifyReplacementState()
    {
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(HasReplacements));
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
