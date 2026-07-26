# SMO Texture Tool 2.0

**Texture extractor and repacker for Winx Club PC `.smo` models**<br>
**Инструмент для извлечения и замены текстур в `.smo`-моделях Winx Club для ПК**

---

## 🌐 Language / Язык

- [English](#english)
- [Русский](#русский)

---

# English

## 📌 Description

**SMO Texture Tool 2.0** is a desktop utility for editing embedded textures in
model files from the PC version of **Winx Club**.

It can:

- find and preview every supported texture in an SMO file;
- export one texture or the complete set as PNG;
- replace textures individually or from a folder;
- use larger HD replacements and recalculate all affected 32-bit block sizes;
- preserve ABGR and BGRA channel layouts used by the game;
- inspect RGB and Alpha channels separately;
- preview grayscale textures with the vertex colors of their actual model;
- rebuild and validate the resulting SMO before saving it.

Version 2.0 has been rewritten with **Avalonia UI** and no longer depends on the
Visual Studio WinForms designer.

## 📥 Download

Download the latest build from:

https://github.com/AnDi-SD/SMOTextureTool/releases

The Windows x64 self-contained build does not require a separate .NET
installation.

## 🚀 How to use

### 1. Open an SMO

Click **Open SMO** and select a model file. The original file is only read and
is never overwritten automatically.

### 2. Inspect and export textures

Use **Save PNG** for one texture or **Extract all** for the complete set.

The preview selector can show:

- RGBA on a checkerboard;
- raw RGBA;
- RGB without transparency;
- the Alpha channel;
- **Model coloring**, which multiplies grayscale texture data by the diffuse
  vertex colors of its linked mesh.

### 3. Edit textures

Edit the exported PNG files in an image editor. Keep their filenames if you
want to load a complete replacement folder.

Grayscale model-colored textures should normally remain grayscale. Their pink,
yellow, or other final colors are stored in the model vertices and applied by
the game with `D3DTOP_MODULATE`.

### 4. Choose replacements

Use **Choose replacement** for one texture or **Replacement folder** for a
complete set.

Replacement images may be PNG, BMP, or JPEG. Both dimensions must be powers of
two.

### 5. Save a new SMO

Click **Save new SMO**. The utility updates nested texture blocks and the main
file header, then parses the generated file again before writing it.

## 🖼 HD textures

- Safe mode allows textures up to `4096×4096`.
- Experimental mode allows up to `16384×16384`.
- `1024×1024` and `2048×2048` replacements have been tested in the original
  game.
- Texture data is stored uncompressed at four bytes per pixel, so very large
  replacements substantially increase both file size and video-memory usage.

## ⚠️ Important notes

- Always save to a new SMO and keep the original file.
- The colored UV preview is diagnostic. Overlapping UV surfaces can use the same
  texture pixels with different vertex colors, so a single flat preview cannot
  reproduce every 3D surface simultaneously.
- Model coloring changes preview only. Export and repacking always use the raw
  texture, preventing colors from being applied twice.
- Only the texture layouts documented in
  [`docs/SMO_FORMAT.md`](docs/SMO_FORMAT.md) are modified.

## 🛠 Development

Requirements:

- .NET 8 SDK or newer;
- Windows, Linux, or macOS for development.

```powershell
dotnet restore SMOTextureTool.slnx
dotnet build SMOTextureTool.slnx
dotnet run --project SMOTextureTool/SMOTextureTool.csproj
dotnet run --project SMOTextureTool.FormatTests/SMOTextureTool.FormatTests.csproj
```

Windows x64 release build:

```powershell
dotnet publish SMOTextureTool/SMOTextureTool.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None -p:DebugSymbols=false
```

Project structure:

- `SMOTextureTool` — Avalonia desktop interface;
- `SMOTextureTool.Core` — SMO parsing, texture decoding, repacking, material
  graph, and vertex-color preview;
- `SMOTextureTool.FormatTests` — regression tests using local sample models;
- `docs/SMO_FORMAT.md` — reverse-engineered SMO format notes.

## 📜 License

Copyright © 2026 AnDi-SD.

Original project: https://github.com/AnDi-SD/SMOTextureTool

Licensed under the GNU General Public License v3.0 or later. You may use,
redistribute, and modify the program under its terms. When distributing copies
or modified versions, preserve the copyright, license, and origin notices.
See `COPYRIGHT.txt` and `LICENSE.txt`.

---

# Русский

## 📌 Описание

**SMO Texture Tool 2.0** — настольная программа для редактирования встроенных
текстур в файлах моделей ПК-версии **Winx Club**.

Возможности:

- поиск и просмотр всех поддерживаемых текстур внутри SMO;
- сохранение отдельной текстуры или всего набора в PNG;
- замена по одной текстуре или целой папкой;
- установка HD-текстур с пересчётом всех вложенных 32-битных размеров;
- сохранение используемых игрой раскладок каналов ABGR и BGRA;
- отдельный просмотр RGB и Alpha;
- просмотр серых текстур с цветами вершин их настоящей модели;
- повторная проверка собранного SMO перед сохранением.

Версия 2.0 полностью переписана на **Avalonia UI** и больше не зависит от
WinForms Designer в Visual Studio.

## 📥 Скачать

Последняя версия:

https://github.com/AnDi-SD/SMOTextureTool/releases

Самодостаточная сборка Windows x64 не требует отдельной установки .NET.

## 🚀 Использование

### 1. Открыть SMO

Нажмите **Открыть SMO** и выберите файл модели. Исходный файл только читается и
никогда не перезаписывается автоматически.

### 2. Посмотреть и извлечь текстуры

Кнопка **Сохранить PNG** сохраняет одну текстуру, **Извлечь все** — полный
набор.

Переключатель предпросмотра поддерживает:

- RGBA на шахматном фоне;
- исходный RGBA;
- RGB без прозрачности;
- Alpha-канал;
- **Окраску модели** — умножение серой основы на diffuse-цвета вершин
  действительно связанного меша.

### 3. Отредактировать изображения

Измените PNG в любом графическом редакторе. Не переименовывайте файлы, если
планируете загружать сразу всю папку замен.

Окрашиваемые серые текстуры обычно следует оставлять серыми. Розовый, жёлтый и
другие итоговые цвета находятся в вершинах модели и накладываются игрой через
`D3DTOP_MODULATE`.

### 4. Выбрать замены

Используйте **Выбрать замену** для одной текстуры или **Папка замен** для полного
набора.

Принимаются PNG, BMP и JPEG. Обе стороны изображения должны быть степенями
двойки.

### 5. Сохранить новый SMO

Нажмите **Сохранить новый SMO**. Программа обновит вложенные текстурные блоки и
главный заголовок файла, после чего повторно проверит полученный SMO.

## 🖼 HD-текстуры

- Безопасный режим разрешает размеры до `4096×4096`.
- Экспериментальный режим — до `16384×16384`.
- Замены `1024×1024` и `2048×2048` проверены в оригинальной игре.
- Текстуры хранятся без сжатия по четыре байта на пиксель, поэтому очень большие
  изображения заметно увеличивают файл и расход видеопамяти.

## ⚠️ Важно

- Всегда сохраняйте результат в новый SMO и оставляйте оригинал.
- Цветная UV-развёртка является диагностическим предпросмотром. Пересекающиеся
  поверхности могут использовать одни пиксели с разными цветами вершин, поэтому
  одна плоская картинка не способна одновременно показать все стороны модели.
- Окрашивание влияет только на просмотр. Экспорт и упаковка используют исходную
  текстуру, поэтому цвет не накладывается дважды.
- Программа изменяет только раскладки, описанные в
  [`docs/SMO_FORMAT.md`](docs/SMO_FORMAT.md).

## 🛠 Разработка

Требуется .NET 8 SDK или новее.

```powershell
dotnet restore SMOTextureTool.slnx
dotnet build SMOTextureTool.slnx
dotnet run --project SMOTextureTool/SMOTextureTool.csproj
dotnet run --project SMOTextureTool.FormatTests/SMOTextureTool.FormatTests.csproj
```

Структура:

- `SMOTextureTool` — интерфейс Avalonia;
- `SMOTextureTool.Core` — разбор SMO, декодирование, пересборка, граф материалов
  и предпросмотр vertex color;
- `SMOTextureTool.FormatTests` — регрессионные тесты на локальных образцах;
- `docs/SMO_FORMAT.md` — результаты исследования формата.

## 📜 Лицензия

Copyright © 2026 AnDi-SD.

Оригинальный проект: https://github.com/AnDi-SD/SMOTextureTool

Проект распространяется по лицензии GNU General Public License v3.0 или более
поздней версии. Программу можно использовать, распространять и изменять на
условиях GPL. При распространении копий и изменённых версий необходимо
сохранять сведения об авторе, лицензии и происхождении проекта. Полные условия
находятся в `COPYRIGHT.txt` и `LICENSE.txt`.
