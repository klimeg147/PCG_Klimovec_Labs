using System;
using System.IO;
using System.Text;

namespace LAB2
{
    public static class ImageParser
    {
        public static ImageInfo Parse(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (fs.Length < 4)
                        return CreateCorrupted(fileName, filePath, "Файл слишком мал");

                    //первые 4 байта для определения формата
                    byte[] sig = new byte[4];
                    fs.Read(sig, 0, 4);
                    fs.Position = 0;

                    if (sig[0] == 0x89 && sig[1] == 0x50 && sig[2] == 0x4E && sig[3] == 0x47)
                        return ParsePng(fs, fileName, filePath);

                    if (sig[0] == 0x42 && sig[1] == 0x4D)
                        return ParseBmp(fs, fileName, filePath);

                    if (sig[0] == 0xFF && sig[1] == 0xD8)
                        return ParseJpeg(fs, fileName, filePath);

                    if (sig[0] == 0x47 && sig[1] == 0x49 && sig[2] == 0x46)
                        return ParseGif(fs, fileName, filePath);

                    if ((sig[0] == 0x49 && sig[1] == 0x49 && sig[2] == 0x2A) ||
                        (sig[0] == 0x4D && sig[1] == 0x4D && sig[2] == 0x2A))
                        return ParseTiff(fs, fileName, filePath);

                    if (sig[0] == 0x0A)
                        return ParsePcx(fs, fileName, filePath);

                    string ext = Path.GetExtension(filePath).ToLower();
                    if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".bmp" || ext == ".tif" || ext == ".tiff" || ext == ".pcx")
                    {
                        return CreateCorrupted(fileName, filePath, "Подменено расширение");
                    }

                    return null;
                }
            }
            catch
            {
                return CreateCorrupted(fileName, filePath, "Ошибка доступа к файлу");
            }
        }

        private static ImageInfo ParsePng(FileStream fs, string name, string path)
        {
            using (var r = new BinaryReader(fs, Encoding.UTF8, true))
            {
                byte[] chunkType = r.ReadBytes(4);
                string typeStr = Encoding.ASCII.GetString(chunkType);

                if (typeStr != "IHDR")
                    return CreateCorrupted(name, path, "Ошибка: нет чанка IHDR");

                int w = ReadInt32BE(r);
                int h = ReadInt32BE(r);
                byte depth = r.ReadByte();
                byte color = r.ReadByte();
                r.ReadByte(); // Сжатие (всегда 0)
                byte filter = r.ReadByte();

                int bpp = color switch
                {
                    2 => depth * 3, // RGB
                    4 => depth * 2, // Grayscale + Alpha
                    6 => depth * 4, // RGBA
                    _ => depth
                };

                string dpiStr = "96 x 96 DPI (По умолчанию)";
                fs.Position = 33;

                while (fs.Position < fs.Length - 12)
                {
                    int chunkLength = ReadInt32BE(r);
                    byte[] nameBytes = r.ReadBytes(4);
                    string nameStr = Encoding.ASCII.GetString(nameBytes);

                    if (nameStr == "pHYs")
                    {
                        int ppuX = ReadInt32BE(r); // Пикселей на метр по горизонтали
                        int ppuY = ReadInt32BE(r); // Пикселей на метр по вертикали
                        byte unit = r.ReadByte();  // Единица измерения (1 = метр)

                        if (unit == 1 && ppuX > 0)
                        {
                            // Перевод из пикселей на метр в дюймы (1 дюйм = 0.0254 метра)
                            int dpiX = (int)Math.Round(ppuX * 0.0254);
                            int dpiY = (int)Math.Round(ppuY * 0.0254);
                            dpiStr = $"{dpiX} x {dpiY} DPI";
                        }
                        break;
                    }
                    else if (nameStr == "IEND")
                    {
                        break;
                    }
                    else
                    {
                        fs.Position += chunkLength + 4;
                    }
                }

                bool hasIend = false;
                if (fs.Length > 12)
                {
                    fs.Position = fs.Length - 12;
                    byte[] endBytes = r.ReadBytes(12);
                    string endChunk = Encoding.ASCII.GetString(endBytes, 4, 4);
                    if (endChunk == "IEND") hasIend = true;
                }

                return new ImageInfo
                {
                    FileName = name,
                    FilePath = path,
                    Dimensions = $"{w} x {h}",
                    Resolution = dpiStr,
                    ColorDepth = $"{bpp} bpp",
                    Compression = hasIend ? $"DEFLATE (Фильтр {filter})" : "Файл поврежден (Нет IEND)"
                };
            }
        }


        private static ImageInfo ParseBmp(FileStream fs, string name, string path)
        {
            using (var r = new BinaryReader(fs, Encoding.UTF8, true))
            {
                fs.Position = 2;
                uint size = r.ReadUInt32();
                if (fs.Length < size)
                    return CreateCorrupted(name, path, "Размер файла меньше заявленного");

                fs.Position = 18; //BITMAPINFOHEADER
                int w = r.ReadInt32();
                int h = Math.Abs(r.ReadInt32());
                r.ReadUInt16(); // Planes
                ushort bitCount = r.ReadUInt16();
                uint comp = r.ReadUInt32();
                r.ReadUInt32(); // SizeImage
                int xPels = r.ReadInt32();
                int yPels = r.ReadInt32();

                string dpiStr;
                if (xPels > 0 && yPels > 0)
                {
                    int dpiX = (int)Math.Round(xPels * 0.0254);
                    int dpiY = (int)Math.Round(yPels * 0.0254);
                    dpiStr = $"{dpiX} x {dpiY} DPI";
                }
                else
                {
                    dpiStr = "96 x 96 DPI (По умолчанию)";
                }

                string cStr = comp switch
                {
                    0 => "BI_RGB (Без сжатия)",
                    1 => "BI_RLE8",
                    2 => "BI_RLE4",
                    3 => "BI_BITFIELDS",
                    _ => $"Код: {comp}"
                };

                return new ImageInfo
                {
                    FileName = name,
                    FilePath = path,
                    Dimensions = $"{w} x {h} px",
                    Resolution = dpiStr,
                    ColorDepth = $"{bitCount} bpp",
                    Compression = cStr
                };
            }
        }

        private static ImageInfo ParsePcx(FileStream fs, string name, string path)
        {
            using (var r = new BinaryReader(fs, Encoding.UTF8, true))
            {
                byte manuf = r.ReadByte();
                _ = r.ReadByte();
                byte enc = r.ReadByte();
                byte bpp = r.ReadByte();
                ushort xMin = r.ReadUInt16(); ushort yMin = r.ReadUInt16();
                ushort xMax = r.ReadUInt16(); ushort yMax = r.ReadUInt16();
                ushort hdpi = r.ReadUInt16(); ushort vdpi = r.ReadUInt16();
                fs.Position = 65;
                byte planes = r.ReadByte();

                if (manuf != 10) return CreateCorrupted(name, path, "Невалидный PCX");
                return new ImageInfo
                {
                    FileName = name,
                    FilePath = path,
                    Dimensions = $"{(xMax - xMin + 1)} x {(yMax - yMin + 1)}",
                    Resolution = $"{hdpi} x {vdpi}",
                    ColorDepth = $"{(bpp * planes)} bpp",
                    Compression = enc == 1 ? "PCX RLE" : "Без сжатия"
                };
            }
        }
        private static ImageInfo ParseJpeg(FileStream fs, string name, string path)
        {
            using (var r = new BinaryReader(fs, Encoding.UTF8, true))
            {
                bool hasEoi = false;
                if (fs.Length > 2)
                {
                    fs.Position = fs.Length - 2;
                    if (r.ReadByte() == 0xFF && r.ReadByte() == 0xD9) hasEoi = true;
                }

                fs.Position = 2;
                int w = 0, h = 0, bpp = 24;
                string dpiStr = "72 x 72 (По умолчанию)";

                while (fs.Position < fs.Length)
                {
                    if (r.ReadByte() != 0xFF) return CreateCorrupted(name, path, "Ошибка структуры JPEG");
                    byte marker = r.ReadByte();
                    if (marker == 0xD9 || marker == 0xDA) break;

                    if (marker == 0x00 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) continue;

                    ushort len = (ushort)((r.ReadByte() << 8) | r.ReadByte());
                    long next = fs.Position + len - 2;

                    if (marker == 0xE0 && len >= 14)
                    {
                        long savedPos = fs.Position;
                        byte[] jfifId = r.ReadBytes(5); // Ожидаю "JFIF\0"
                        if (jfifId[0] == 0x4A && jfifId[1] == 0x46 && jfifId[2] == 0x49 && jfifId[3] == 0x53) // Сверка "JFIF" побайтово
                        {
                            fs.Position += 2;
                            byte unit = r.ReadByte(); // 1 = DPI, 2 = точек на см
                            ushort xDensity = (ushort)((r.ReadByte() << 8) | r.ReadByte());
                            ushort yDensity = (ushort)((r.ReadByte() << 8) | r.ReadByte());

                            if (unit == 1 && xDensity > 0)
                            {
                                dpiStr = $"{xDensity} x {yDensity}";
                            }
                            else if (unit == 2 && xDensity > 0)
                            {
                                int dpiX = (int)Math.Round(xDensity * 2.54);
                                int dpiY = (int)Math.Round(yDensity * 2.54);
                                dpiStr = $"{dpiX} x {dpiY}";
                            }
                        }
                        fs.Position = savedPos;
                    }

                    if ((marker == 0xC0 || marker == 0xC2) && len >= 9)
                    {
                        byte prec = r.ReadByte();
                        h = (r.ReadByte() << 8) | r.ReadByte();
                        w = (r.ReadByte() << 8) | r.ReadByte();
                        bpp = prec * r.ReadByte();
                    }
                    fs.Position = next;
                    if (w > 0 && h > 0 && hasEoi) break;
                }

                return new ImageInfo
                {
                    FileName = name,
                    FilePath = path,
                    Dimensions = $"{w} x {h}",
                    Resolution = dpiStr,
                    ColorDepth = $"{bpp} bpp",
                    Compression = hasEoi ? "JPEG (Сжатие DCT)" : "Битый файл (Нет EOI)"
                };
            }
        }

        private static ImageInfo ParseGif(FileStream fs, string name, string path)
        {
            using (var r = new BinaryReader(fs, Encoding.UTF8, true))
            {
                fs.Position = 6;
                ushort w = r.ReadUInt16();
                ushort h = r.ReadUInt16();
                byte fields = r.ReadByte();
                int colors = 1 << ((fields & 0x07) + 1);

                fs.Position = fs.Length - 1;
                bool valid = r.ReadByte() == 0x3B;

                return new ImageInfo
                {
                    FileName = name,
                    FilePath = path,
                    Dimensions = $"{w} x {h}",
                    Resolution = "96 x 96",
                    ColorDepth = $"{((fields & 0x07) + 1)} bpp",
                    Compression = valid ? $"LZW (Палитра: {colors})" : "Битый GIF"
                };
            }
        }

        private static ImageInfo ParseTiff(FileStream fs, string name, string path)
        {
            using (var r = new BinaryReader(fs, Encoding.UTF8, true))
            {
                byte[] endianBytes = r.ReadBytes(2);
                bool isLittleEndian = (endianBytes[0] == 0x49 && endianBytes[1] == 0x49); // "II" или "MM"

                ushort magic = r.ReadUInt16();
                if (!isLittleEndian) magic = (ushort)((magic << 8) | (magic >> 8));
                if (magic != 42) return CreateCorrupted(name, path, "Невалидный файл TIFF");

                uint ifdOffset = r.ReadUInt32();
                if (!isLittleEndian)
                    ifdOffset = ((ifdOffset & 0x000000FF) << 24) | ((ifdOffset & 0x0000FF00) << 8) |
                                 ((ifdOffset & 0x00FF0000) >> 8) | ((ifdOffset & 0xFF000000) >> 24);

                fs.Position = ifdOffset;

                ushort numEntries = r.ReadUInt16();
                if (!isLittleEndian) numEntries = (ushort)((numEntries << 8) | (numEntries >> 8));

                int w = 0, h = 0, compCode = 1, unit = 2; // unit = 2 по дефолту дюймы
                uint xResOffset = 0, yResOffset = 0;

                for (int i = 0; i < numEntries; i++)
                {
                    ushort tag = r.ReadUInt16();
                    ushort type = r.ReadUInt16();
                    uint count = r.ReadUInt32();
                    uint valueOffset = r.ReadUInt32();

                    if (!isLittleEndian)
                    {
                        tag = (ushort)((tag << 8) | (tag >> 8));
                        valueOffset = ((valueOffset & 0x000000FF) << 24) | ((valueOffset & 0x0000FF00) << 8) |
                                      ((valueOffset & 0x00FF0000) >> 8) | ((valueOffset & 0xFF000000) >> 24);
                    }

                    if (tag == 256) w = (int)valueOffset; // Ширина
                    if (tag == 257) h = (int)valueOffset; // Высота
                    if (tag == 259) compCode = (int)(valueOffset & 0xFFFF); // Сжатие
                    if (tag == 296) unit = (int)(valueOffset & 0xFFFF);     // Единицы (2 - дюйм, 3 - см)
                    if (tag == 282) xResOffset = valueOffset; // Адрес где лежит дробь X-разрешения
                    if (tag == 283) yResOffset = valueOffset; // Адрес где лежит дробь Y-разрешения
                }

                int dpiX = 300, dpiY = 300; // Дефолтные значения на случай, если тегов нет
                string statusDpi = " (По умолчанию)";

                if (xResOffset > 0 && yResOffset > 0)
                {
                    statusDpi = "";
                    // XResolution (дробь: 4 байта числитель + 4 байта знаменатель)
                    fs.Position = xResOffset;
                    uint numX = r.ReadUInt32(); uint denX = r.ReadUInt32();

                    // YResolution
                    fs.Position = yResOffset;
                    uint numY = r.ReadUInt32(); uint denY = r.ReadUInt32();

                    if (!isLittleEndian)
                    {
                        numX = ((numX & 0x000000FF) << 24) | ((numX & 0x0000FF00) << 8) | ((numX & 0x00FF0000) >> 8) | ((numX & 0xFF000000) >> 24);
                        denX = ((denX & 0x000000FF) << 24) | ((denX & 0x0000FF00) << 8) | ((denX & 0x00FF0000) >> 8) | ((denX & 0xFF000000) >> 24);
                        numY = ((numY & 0x000000FF) << 24) | ((numY & 0x0000FF00) << 8) | ((numY & 0x00FF0000) >> 8) | ((numY & 0xFF000000) >> 24);
                        denY = ((denY & 0x000000FF) << 24) | ((denY & 0x0000FF00) << 8) | ((denY & 0x00FF0000) >> 8) | ((denY & 0xFF000000) >> 24);
                    }

                    if (denX > 0 && denY > 0)
                    {
                        dpiX = (int)(numX / denX);
                        dpiY = (int)(numY / denY);

                        // Если указано в сантиметрах (unit == 3), переводим в дюймы (DPI = точек/см * 2.54)
                        if (unit == 3)
                        {
                            dpiX = (int)Math.Round(dpiX * 2.54);
                            dpiY = (int)Math.Round(dpiY * 2.54);
                        }
                    }
                }

                string compStr = compCode switch
                {
                    1 => "Без сжатия (Raw)",
                    5 => "LZW",
                    32773 => "PackBits",
                    _ => $"Код: {compCode}"
                };

                return new ImageInfo
                {
                    FileName = name,
                    FilePath = path,
                    Dimensions = $"{w} x {h} px",
                    Resolution = $"{dpiX} x {dpiY} DPI{statusDpi}",
                    ColorDepth = "24 bit",
                    Compression = $"TIFF ({compStr})"
                };
            }
        }

        private static ImageInfo CreateCorrupted(string name, string path, string msg)
        {
            return new ImageInfo { FileName = name, FilePath = path, Dimensions = "Ошибка", Resolution = "Ошибка", ColorDepth = "Ошибка", Compression = msg };
        }

        private static int ReadInt32BE(BinaryReader r)
        {
            byte[] b = r.ReadBytes(4);
            if (b.Length < 4) return 0;
            return ((int)b[0] << 24) | ((int)b[1] << 16) | ((int)b[2] << 8) | b[3];
        }
    }
}
