using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SMOTextureTool
{
    /// <summary>
    /// Texture storage variant inside SMO.
    /// (Вариант хранения текстуры внутри SMO.)
    /// </summary>
    public enum TextureVariant
    {
        VariantA,
        VariantB
    }

    /// <summary>
    /// Parsed information about one texture block.
    /// (Разобранная информация об одном блоке текстуры.)
    /// </summary>
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

        /// <summary>
        /// Default export file name.
        /// (Имя файла по умолчанию для экспорта.)
        /// </summary>
        public string FileName =>
            $"tex_{Index1Based:D3}_{Width}x{Height}_{Variant}_fmt{FormatCode:X4}.png";
    }

    /// <summary>
    /// Core parser and repacker for SMO texture blocks.
    /// (Основной парсер и репакер блоков текстур SMO.)
    /// </summary>
    public static class SmoParser
    {
        /// <summary>
        /// SBOO block signature.
        /// (Сигнатура блока SBOO.)
        /// </summary>
        private static readonly byte[] Signature =
        {
            0x2B, 0x08, 0xEA, 0x78, 0x53, 0x42, 0x4F, 0x4F
        };

        // File header offsets.
        // (Смещения полей заголовка файла.)
        private const int FileSizeOffset = 0x0C;
        private const int DataStartOffset = 0x14;
        private const int DataSizeOffset = 0x18;

        /// <summary>
        /// Find all supported texture blocks inside an SMO file.
        /// (Найти все поддерживаемые текстурные блоки внутри файла SMO.)
        /// </summary>
        public static List<TextureInfo> FindTextures(byte[] data)
        {
            var result = new List<TextureInfo>();
            int index = 1;

            // Scan the whole file for SBOO signatures.
            // (Сканируем весь файл в поиске сигнатур SBOO.)
            for (int i = 0; i <= data.Length - Signature.Length; i++)
            {
                if (!Match(data, i, Signature))
                    continue;

                // Read the low 16 bits of the format field.
                // (Читаем младшие 16 бит поля формата.)
                ushort formatCode = ReadFormatCode(data, i);
                TextureInfo tex = null;

                // 0x32E3 and 0x43E3 use A-like layout.
                // (0x32E3 и 0x43E3 используют раскладку типа A.)
                if (formatCode == 0x32E3 || formatCode == 0x43E3)
                {
                    tex = TryParseVariantA(data, i, index, formatCode);
                }
                // 0x29E3 uses B-like layout.
                // (0x29E3 использует раскладку типа B.)
                else if (formatCode == 0x29E3)
                {
                    tex = TryParseVariantB(data, i, index, formatCode);
                }
                else
                {
                    // Unknown texture format, skip it for now.
                    // (Неизвестный формат текстуры, пока пропускаем.)
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

        /// <summary>
        /// Decode one texture block into a Bitmap.
        /// (Декодировать один текстурный блок в Bitmap.)
        /// </summary>
        public static Bitmap ExtractTextureBitmap(byte[] data, TextureInfo tex)
        {
            Bitmap bmp = new Bitmap(tex.Width, tex.Height, PixelFormat.Format32bppArgb);

            int p = tex.PixelDataOffset;

            // Read pixels one by one using the variant-specific channel order.
            // (Читаем пиксели по одному, используя порядок каналов для конкретного варианта.)
            for (int y = 0; y < tex.Height; y++)
            {
                for (int x = 0; x < tex.Width; x++)
                {
                    byte r, g, b, a;

                    if (tex.Variant == TextureVariant.VariantA)
                    {
                        // VariantA layout: A B G R
                        // (Раскладка VariantA: A B G R)
                        a = data[p + 0];
                        b = data[p + 1];
                        g = data[p + 2];
                        r = data[p + 3];
                    }
                    else
                    {
                        // VariantB layout: B G R A
                        // (Раскладка VariantB: B G R A)
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

        /// <summary>
        /// Extract all found textures to a folder.
        /// (Извлечь все найденные текстуры в папку.)
        /// </summary>
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

        /// <summary>
        /// Replace one texture block with a new bitmap and rebuild file data.
        /// (Заменить один текстурный блок новым изображением и пересобрать данные файла.)
        /// </summary>
        public static byte[] ReplaceTexture(byte[] data, TextureInfo tex, Bitmap bmp)
        {
            // Repack is currently limited to square textures.
            // (Обратная упаковка сейчас ограничена квадратными текстурами.)
            if (bmp.Width != bmp.Height)
                throw new Exception("Обратная упаковка пока поддерживает только квадратные текстуры.");

            int newWidth = bmp.Width;
            int newHeight = bmp.Height;

            // Convert bitmap into raw bytes for the target variant.
            // (Преобразуем bitmap в сырые байты для нужного варианта.)
            byte[] newPixels = ReadBitmapBytesForVariant(bmp, tex.Variant);
            int newPixelSize = newPixels.Length;

            int oldPixelStart = tex.PixelDataOffset;
            int oldPixelEnd = tex.PixelDataOffset + tex.PixelDataSize;

            // Build a new file buffer with resized texture payload.
            // (Создаём новый буфер файла с учётом изменённого размера текстуры.)
            byte[] newFile = new byte[data.Length - tex.PixelDataSize + newPixelSize];

            // Copy part before pixel data.
            // (Копируем часть до пиксельных данных.)
            Buffer.BlockCopy(data, 0, newFile, 0, oldPixelStart);

            // Insert new pixel data.
            // (Вставляем новые пиксельные данные.)
            Buffer.BlockCopy(newPixels, 0, newFile, oldPixelStart, newPixelSize);

            // Copy tail after old texture payload.
            // (Копируем хвост после старого блока текстуры.)
            int tailSize = data.Length - oldPixelEnd;
            Buffer.BlockCopy(data, oldPixelEnd, newFile, oldPixelStart + newPixelSize, tailSize);

            // Patch texture header depending on the variant.
            // (Исправляем заголовок текстуры в зависимости от варианта.)
            if (tex.Variant == TextureVariant.VariantA)
                PatchTextureHeaderVariantA(newFile, tex.BlockOffset, newWidth, newHeight, tex.FormatCode);
            else
                PatchTextureHeaderVariantB(newFile, tex.BlockOffset, newWidth, newHeight);

            // Patch global file size fields.
            // (Исправляем глобальные поля размера файла.)
            PatchFileHeader(newFile);

            return newFile;
        }

        /// <summary>
        /// Apply replacement textures from a folder by matching file names.
        /// (Применить заменённые текстуры из папки по совпадению имён файлов.)
        /// </summary>
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

                // Re-scan after each replacement because offsets can move.
                // (Пересканируем после каждой замены, потому что смещения могут сдвигаться.)
                textures = FindTextures(currentData);
            }

            return currentData;
        }

        /// <summary>
        /// Read the texture format code from the block header.
        /// (Прочитать код формата текстуры из заголовка блока.)
        /// </summary>
        private static ushort ReadFormatCode(byte[] data, int blockOffset)
        {
            uint value = ReadUInt32LE(data, blockOffset + 0x08);
            return (ushort)(value & 0xFFFF);
        }

        /// <summary>
        /// Try to parse an A-like texture block.
        /// (Попытаться разобрать текстурный блок типа A.)
        /// </summary>
        private static TextureInfo TryParseVariantA(byte[] data, int blockOffset, int index1Based, ushort formatCode)
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

        /// <summary>
        /// Try to parse a B-like texture block.
        /// (Попытаться разобрать текстурный блок типа B.)
        /// </summary>
        private static TextureInfo TryParseVariantB(byte[] data, int blockOffset, int index1Based, ushort formatCode)
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

        /// <summary>
        /// Validate texture dimensions and data range.
        /// (Проверить размеры текстуры и диапазон данных.)
        /// </summary>
        private static bool IsValidTexture(int width, int height, int pixelOffset, int fileLength)
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

        /// <summary>
        /// Convert a bitmap into raw bytes using the target variant layout.
        /// (Преобразовать bitmap в сырые байты согласно раскладке нужного варианта.)
        /// </summary>
        private static byte[] ReadBitmapBytesForVariant(Bitmap bmp, TextureVariant variant)
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
                        // VariantA layout: A B G R
                        // (Раскладка VariantA: A B G R)
                        result[p + 0] = c.A;
                        result[p + 1] = c.B;
                        result[p + 2] = c.G;
                        result[p + 3] = c.R;
                    }
                    else
                    {
                        // VariantB layout: B G R A
                        // (Раскладка VariantB: B G R A)
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

        /// <summary>
        /// Patch an A-like texture header after replacement.
        /// (Исправить заголовок текстуры типа A после замены.)
        /// </summary>
        private static void PatchTextureHeaderVariantA(byte[] data, int blockOffset, int width, int height, ushort formatCode)
        {
            uint areaDiv64 = (uint)((width * height) / 64);
            uint areaDiv16384 = (uint)((width * height) / 16384);

            // Preserve original low 16-bit format code.
            // (Сохраняем исходный код формата в младших 16 битах.)
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

        /// <summary>
        /// Patch a B-like texture header after replacement.
        /// (Исправить заголовок текстуры типа B после замены.)
        /// </summary>
        private static void PatchTextureHeaderVariantB(byte[] data, int blockOffset, int width, int height)
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

        /// <summary>
        /// Patch global file header fields after rebuilding the file.
        /// (Исправить глобальные поля заголовка файла после пересборки.)
        /// </summary>
        private static void PatchFileHeader(byte[] data)
        {
            uint fileSize = (uint)data.Length;
            uint dataStart = ReadUInt32LE(data, DataStartOffset);
            uint dataSize = fileSize - dataStart;

            WriteUInt32LE(data, FileSizeOffset, fileSize);
            WriteUInt32LE(data, DataSizeOffset, dataSize);
        }

        /// <summary>
        /// Compare bytes at a given offset with a signature.
        /// (Сравнить байты по заданному смещению с сигнатурой.)
        /// </summary>
        private static bool Match(byte[] data, int offset, byte[] sig)
        {
            for (int i = 0; i < sig.Length; i++)
            {
                if (data[offset + i] != sig[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Read a little-endian Int32.
        /// (Прочитать Int32 в little-endian.)
        /// </summary>
        private static int ReadInt32LE(byte[] data, int offset) =>
            BitConverter.ToInt32(data, offset);

        /// <summary>
        /// Read a little-endian UInt32.
        /// (Прочитать UInt32 в little-endian.)
        /// </summary>
        private static uint ReadUInt32LE(byte[] data, int offset) =>
            BitConverter.ToUInt32(data, offset);

        /// <summary>
        /// Write a little-endian UInt32.
        /// (Записать UInt32 в little-endian.)
        /// </summary>
        private static void WriteUInt32LE(byte[] data, int offset, uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, data, offset, 4);
        }
    }
}