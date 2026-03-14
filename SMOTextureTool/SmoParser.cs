using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SMOTextureTool
{
    public enum TextureVariant
    {
        VariantA,
        VariantB
    }

    public class TextureInfo
    {
        public int Index1Based { get; set; }
        public int BlockOffset { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int PixelDataOffset { get; set; }
        public int PixelDataSize { get; set; }
        public TextureVariant Variant { get; set; }
        public ushort FormatCode { get; set; }

        public string FileName =>
            $"tex_{Index1Based:D3}_{Width}x{Height}_{Variant}_fmt{FormatCode:X4}.png";
    }

    public static class SmoParser
    {
        static readonly byte[] Signature = { 0x2B, 0x08, 0xEA, 0x78, 0x53, 0x42, 0x4F, 0x4F };

        const int FileSizeOffset = 0x0C;
        const int DataStartOffset = 0x14;
        const int DataSizeOffset = 0x18;

        public static List<TextureInfo> FindTextures(byte[] data)
        {
            var result = new List<TextureInfo>();
            int index = 1;

            for (int i = 0; i <= data.Length - Signature.Length; i++)
            {
                if (!Match(data, i, Signature))
                    continue;

                ushort formatCode = ReadFormatCode(data, i);
                TextureInfo tex = null;

                // 0x43E3 тоже читается как A-подобный блок
                if (formatCode == 0x32E3 || formatCode == 0x43E3)
                {
                    tex = TryParseVariantA(data, i, index, formatCode);
                }
                else if (formatCode == 0x29E3)
                {
                    tex = TryParseVariantB(data, i, index, formatCode);
                }
                else
                {
                    continue;
                }

                if (tex != null)
                {
                    result.Add(tex);
                    index++;
                }
            }

            return result;
        }

        public static Bitmap ExtractTextureBitmap(byte[] data, TextureInfo tex)
        {
            Bitmap bmp = new Bitmap(tex.Width, tex.Height, PixelFormat.Format32bppArgb);

            int p = tex.PixelDataOffset;

            for (int y = 0; y < tex.Height; y++)
            {
                for (int x = 0; x < tex.Width; x++)
                {
                    byte r, g, b, a;

                    if (tex.Variant == TextureVariant.VariantA)
                    {
                        // VariantA = A B G R
                        a = data[p + 0];
                        b = data[p + 1];
                        g = data[p + 2];
                        r = data[p + 3];
                    }
                    else
                    {
                        // VariantB = B G R A
                        b = data[p + 0];
                        g = data[p + 1];
                        r = data[p + 2];
                        a = data[p + 3];
                    }

                    p += 4;
                    bmp.SetPixel(x, y, Color.FromArgb(a, r, g, b));
                }
            }

            return bmp;
        }

        public static void ExtractAllTextures(string smoFile, string outputFolder)
        {
            byte[] data = File.ReadAllBytes(smoFile);
            var textures = FindTextures(data);

            Directory.CreateDirectory(outputFolder);

            foreach (var tex in textures)
            {
                using Bitmap bmp = ExtractTextureBitmap(data, tex);
                string outFile = Path.Combine(outputFolder, tex.FileName);
                bmp.Save(outFile, ImageFormat.Png);
            }
        }

        public static byte[] ReplaceTexture(byte[] data, TextureInfo tex, Bitmap bmp)
        {
            // Пока оставляем только квадратные для обратной упаковки,
            // потому что заголовок прямоугольных ещё не проверен.
            if (bmp.Width != bmp.Height)
                throw new Exception("Обратная упаковка пока поддерживает только квадратные текстуры.");

            int newWidth = bmp.Width;
            int newHeight = bmp.Height;

            byte[] newPixels = ReadBitmapBytesForVariant(bmp, tex.Variant);
            int newPixelSize = newPixels.Length;

            int oldPixelStart = tex.PixelDataOffset;
            int oldPixelEnd = tex.PixelDataOffset + tex.PixelDataSize;

            byte[] newFile = new byte[data.Length - tex.PixelDataSize + newPixelSize];

            Buffer.BlockCopy(data, 0, newFile, 0, oldPixelStart);
            Buffer.BlockCopy(newPixels, 0, newFile, oldPixelStart, newPixelSize);

            int tailSize = data.Length - oldPixelEnd;
            Buffer.BlockCopy(data, oldPixelEnd, newFile, oldPixelStart + newPixelSize, tailSize);

            if (tex.Variant == TextureVariant.VariantA)
                PatchTextureHeaderVariantA(newFile, tex.BlockOffset, newWidth, newHeight, tex.FormatCode);
            else
                PatchTextureHeaderVariantB(newFile, tex.BlockOffset, newWidth, newHeight);

            PatchFileHeader(newFile);

            return newFile;
        }

        public static byte[] ApplyFolderReplacements(byte[] originalData, string folder)
        {
            byte[] currentData = originalData;
            var textures = FindTextures(currentData);

            for (int i = 0; i < textures.Count; i++)
            {
                var tex = textures[i];
                string path = Path.Combine(folder, tex.FileName);

                if (!File.Exists(path))
                    continue;

                using Bitmap bmp = new Bitmap(path);
                currentData = ReplaceTexture(currentData, tex, bmp);
                textures = FindTextures(currentData);
            }

            return currentData;
        }

        static ushort ReadFormatCode(byte[] data, int blockOffset)
        {
            uint value = ReadUInt32LE(data, blockOffset + 0x08);
            return (ushort)(value & 0xFFFF);
        }

        static TextureInfo TryParseVariantA(byte[] data, int blockOffset, int index1Based, ushort formatCode)
        {
            int width = ReadInt32LE(data, blockOffset + 0x24);
            int height = ReadInt32LE(data, blockOffset + 0x28);
            int pixelOffset = blockOffset + 0x3C;

            if (!IsValidTexture(width, height, pixelOffset, data.Length))
                return null;

            int pixelSize = checked(width * height * 4);

            return new TextureInfo
            {
                Index1Based = index1Based,
                BlockOffset = blockOffset,
                Width = width,
                Height = height,
                PixelDataOffset = pixelOffset,
                PixelDataSize = pixelSize,
                Variant = TextureVariant.VariantA,
                FormatCode = formatCode
            };
        }

        static TextureInfo TryParseVariantB(byte[] data, int blockOffset, int index1Based, ushort formatCode)
        {
            int width = ReadInt32LE(data, blockOffset + 0x28);
            int height = ReadInt32LE(data, blockOffset + 0x30);
            int pixelOffset = blockOffset + 0x34;

            if (!IsValidTexture(width, height, pixelOffset, data.Length))
                return null;

            int pixelSize = checked(width * height * 4);

            return new TextureInfo
            {
                Index1Based = index1Based,
                BlockOffset = blockOffset,
                Width = width,
                Height = height,
                PixelDataOffset = pixelOffset,
                PixelDataSize = pixelSize,
                Variant = TextureVariant.VariantB,
                FormatCode = formatCode
            };
        }

        static bool IsValidTexture(int width, int height, int pixelOffset, int fileLength)
        {
            if (width <= 0 || height <= 0)
                return false;

            if (width > 4096 || height > 4096)
                return false;

            long pixelSize = (long)width * height * 4;
            if (pixelOffset < 0 || pixelOffset + pixelSize > fileLength)
                return false;

            return true;
        }

        static byte[] ReadBitmapBytesForVariant(Bitmap bmp, TextureVariant variant)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            byte[] result = new byte[checked(width * height * 4)];

            int p = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color c = bmp.GetPixel(x, y);

                    if (variant == TextureVariant.VariantA)
                    {
                        // VariantA = A B G R
                        result[p + 0] = c.A;
                        result[p + 1] = c.B;
                        result[p + 2] = c.G;
                        result[p + 3] = c.R;
                    }
                    else
                    {
                        // VariantB = B G R A
                        result[p + 0] = c.B;
                        result[p + 1] = c.G;
                        result[p + 2] = c.R;
                        result[p + 3] = c.A;
                    }

                    p += 4;
                }
            }

            return result;
        }

        static void PatchTextureHeaderVariantA(byte[] data, int blockOffset, int width, int height, ushort formatCode)
        {
            uint areaDiv64 = (uint)((width * height) / 64);
            uint areaDiv16384 = (uint)((width * height) / 16384);

            // Сохраняем исходный код формата: 0x32E3 или 0x43E3
            WriteUInt32LE(data, blockOffset + 0x08, formatCode | (areaDiv64 << 16));
            WriteUInt32LE(data, blockOffset + 0x20, 0x01000000u | areaDiv64);
            WriteUInt32LE(data, blockOffset + 0x24, (uint)width);
            WriteUInt32LE(data, blockOffset + 0x28, (uint)height);
            WriteUInt32LE(data, blockOffset + 0x2C, 0u);
            WriteUInt32LE(data, blockOffset + 0x30, (uint)((width << 8) | 0x0001));
            WriteUInt32LE(data, blockOffset + 0x34, (uint)(width << 10));
            WriteUInt32LE(data, blockOffset + 0x38, (uint)(width << 8));
            WriteUInt32LE(data, blockOffset + 0x1C, 0x1AE00000u | areaDiv16384);
        }

        static void PatchTextureHeaderVariantB(byte[] data, int blockOffset, int width, int height)
        {
            uint areaDiv64 = (uint)((width * height) / 64);
            uint areaDiv16384 = (uint)((width * height) / 16384);

            WriteUInt32LE(data, blockOffset + 0x08, 0x000029E3u | (areaDiv64 << 16));
            WriteUInt32LE(data, blockOffset + 0x10, 0x000020E1u | (areaDiv64 << 16));
            WriteUInt32LE(data, blockOffset + 0x14, 0x001AE000u | (areaDiv16384 << 24));
            WriteUInt32LE(data, blockOffset + 0x18, 0x00010000u | areaDiv16384);
            WriteUInt32LE(data, blockOffset + 0x1C, 0x00000001u);
            WriteUInt32LE(data, blockOffset + 0x20, 0x00000001u);
            WriteUInt32LE(data, blockOffset + 0x24, 0x01000000u);
            WriteUInt32LE(data, blockOffset + 0x28, (uint)width);
            WriteUInt32LE(data, blockOffset + 0x2C, (uint)(width << 2));
            WriteUInt32LE(data, blockOffset + 0x30, (uint)height);
        }

        static void PatchFileHeader(byte[] data)
        {
            uint fileSize = (uint)data.Length;
            uint dataStart = ReadUInt32LE(data, DataStartOffset);
            uint dataSize = fileSize - dataStart;

            WriteUInt32LE(data, FileSizeOffset, fileSize);
            WriteUInt32LE(data, DataSizeOffset, dataSize);
        }

        static bool Match(byte[] data, int offset, byte[] sig)
        {
            for (int i = 0; i < sig.Length; i++)
            {
                if (data[offset + i] != sig[i])
                    return false;
            }

            return true;
        }

        static int ReadInt32LE(byte[] data, int offset) => BitConverter.ToInt32(data, offset);
        static uint ReadUInt32LE(byte[] data, int offset) => BitConverter.ToUInt32(data, offset);

        static void WriteUInt32LE(byte[] data, int offset, uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, data, offset, 4);
        }
    }
}
