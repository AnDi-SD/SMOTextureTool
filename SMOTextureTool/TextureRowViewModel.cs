using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using SMOTextureTool.Core;

namespace SMOTextureTool;

public sealed class TextureRowViewModel : INotifyPropertyChanged, IDisposable
{
    private Bitmap? _replacementPreview;
    private string? _replacementPath;

    public required TextureInfo Texture { get; init; }
    public required Bitmap OriginalPreview { get; init; }
    public string Header =>
        $"{Texture.FileName} · {Texture.Width}×{Texture.Height} · 0x{Texture.FormatCode:X4}";
    public string Details =>
        $"Блок 0x{Texture.BlockOffset:X} · пиксели 0x{Texture.PixelDataOffset:X} · " +
        $"{Texture.PixelDataSize:N0} байт";
    public string ResizeNotice => Texture.CanResize
        ? $"HD-замена разрешена до {Texture.MaxResizableDimension}×" +
          $"{Texture.MaxResizableDimension}; стороны должны быть степенями двойки."
        : "Для этого формата размер изображения изменять нельзя.";

    public Bitmap? ReplacementPreview
    {
        get => _replacementPreview;
        private set => SetField(ref _replacementPreview, value);
    }

    public string? ReplacementPath
    {
        get => _replacementPath;
        private set => SetField(ref _replacementPath, value);
    }

    public bool HasReplacement => ReplacementPath is not null;
    public bool HasNoReplacement => !HasReplacement;

    public void SetReplacement(string path, Bitmap preview)
    {
        ReplacementPreview?.Dispose();
        ReplacementPreview = preview;
        ReplacementPath = path;
        NotifyState();
    }

    public void ResetReplacement()
    {
        ReplacementPreview?.Dispose();
        ReplacementPreview = null;
        ReplacementPath = null;
        NotifyState();
    }

    public void Dispose()
    {
        OriginalPreview.Dispose();
        ReplacementPreview?.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    private void NotifyState()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasReplacement)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNoReplacement)));
    }
}
