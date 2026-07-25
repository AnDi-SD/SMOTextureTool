namespace SMOTextureTool.Core;

public enum TextureLayout
{
    Abgr,
    Bgra
}

public sealed record TextureInfo(
    int Index,
    int BlockOffset,
    int PixelDataOffset,
    int Width,
    int Height,
    ushort FormatCode,
    TextureLayout Layout)
{
    public int PixelDataSize => checked(Width * Height * 4);
    public bool CanResize => Layout == TextureLayout.Abgr;
    public int MaxResizableDimension => CanResize ? 1024 : Math.Max(Width, Height);

    public string FileName =>
        $"tex_{Index:D3}_{Width}x{Height}_fmt{FormatCode:X4}.png";
}
