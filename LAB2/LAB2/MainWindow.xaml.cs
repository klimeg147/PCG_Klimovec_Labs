using LAB2;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace LAB2
{
    public partial class MainWindow : System.Windows.Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;
                    TxtFolderPath.Text = selectedPath;

                    await ProcessFolderAsync(selectedPath);
                }
            }
        }

        private async Task ProcessFolderAsync(string folderPath)
        {
            BtnSelectFolder.IsEnabled = false;
            GridImageResults.ItemsSource = null;
            TxtProgressStatus.Text = "Поиск файлов...";

            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".pcx" };

            List<string> files = await Task.Run(() =>
                Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                         .Where(f => extensions.Contains(Path.GetExtension(f)))
                         .ToList());

            if (files.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "В выбранной папке не найдены поддерживаемые изображения.",
                    "Информация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                BtnSelectFolder.IsEnabled = true;
                TxtProgressStatus.Text = "Готово";
                return;
            }

            LoadingProgressBar.Maximum = files.Count;
            LoadingProgressBar.Value = 0;

            var results = new ConcurrentBag<ImageInfo>();
            int processedCount = 0;

            var progress = new Progress<int>(value =>
            {
                LoadingProgressBar.Value = value;
                TxtProgressStatus.Text = $"Обработано: {value} из {files.Count}";
            });

            IProgress<int> progressReporter = progress;

            await Task.Run(() =>
            {
                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file =>
                {
                    ImageInfo? info = AnalyzeFile(file);
                    if (info != null)
                    {
                        results.Add(info);
                    }

                    int current = System.Threading.Interlocked.Increment(ref processedCount);
                    if (current % 50 == 0 || current == files.Count)
                    {
                        progressReporter.Report(current);
                    }
                });
            });

            GridImageResults.ItemsSource = results.ToList();
            TxtTotalFiles.Text = $"Всего файлов обработано: {results.Count}";
            BtnSelectFolder.IsEnabled = true;
            TxtProgressStatus.Text = "Готово!";
        }

        private ImageInfo? AnalyzeFile(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();

            if (ext == ".pcx")
            {
                return AnalyzePcx(filePath);
            }

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    BitmapDecoder decoder = BitmapDecoder.Create(
                        stream,
                        BitmapCreateOptions.DelayCreation,
                        BitmapCacheOption.None);

                    if (decoder.Frames.Count > 0)
                    {
                        BitmapFrame frame = decoder.Frames[0];

                        return new ImageInfo
                        {
                            FileName = Path.GetFileName(filePath),
                            FilePath = filePath,
                            Dimensions = $"{frame.PixelWidth} x {frame.PixelHeight}",
                            Resolution = $"{Math.Round(frame.DpiX)} x {Math.Round(frame.DpiY)}",
                            ColorDepth = $"{frame.Format.BitsPerPixel} bpp",
                            Compression = GetCompressionInfo(decoder, ext)
                        };
                    }
                }
            }
            catch
            {
                return new ImageInfo
                {
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    Dimensions = "Ошибка",
                    Resolution = "Ошибка",
                    ColorDepth = "Ошибка",
                    Compression = "Файл поврежден/Не поддерживается"
                };
            }
            return null;
        }

        private string GetCompressionInfo(BitmapDecoder decoder, string ext)
        {
            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    return "JPEG (Сжатие с потерями)";
                case ".png":
                    return "DEFLATE (Без потерь)";
                case ".gif":
                    return "LZW (Без потерь)";
                case ".bmp":
                    return "Обычно без сжатия (BI_RGB)";
                case ".tif":
                case ".tiff":
                    return "Зависит от тега (LZW/PackBits/ZIP)";
                default:
                    return "Нет данных";
            }
        }

        private ImageInfo AnalyzePcx(string filePath)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fs))
                {
                    if (fs.Length < 128) throw new Exception();

                    byte manufacturer = reader.ReadByte();
                    byte version = reader.ReadByte();
                    byte encoding = reader.ReadByte();
                    byte bitsPerPixel = reader.ReadByte();

                    ushort xMin = reader.ReadUInt16();
                    ushort yMin = reader.ReadUInt16();
                    ushort xMax = reader.ReadUInt16();
                    ushort yMax = reader.ReadUInt16();

                    int width = xMax - xMin + 1;
                    int height = yMax - yMin + 1;

                    ushort hdpi = reader.ReadUInt16();
                    ushort vdpi = reader.ReadUInt16();

                    fs.Seek(48, SeekOrigin.Current);

                    byte reserved = reader.ReadByte();
                    byte colorPlanes = reader.ReadByte();

                    int totalBpp = bitsPerPixel * colorPlanes;
                    string compressionType = (encoding == 1) ? "PCX RLE" : "Без сжатия";

                    if (manufacturer != 10) throw new Exception();

                    return new ImageInfo
                    {
                        FileName = Path.GetFileName(filePath),
                        FilePath = filePath,
                        Dimensions = $"{width} x {height}",
                        Resolution = $"{hdpi} x {vdpi}",
                        ColorDepth = $"{totalBpp} bpp",
                        Compression = compressionType
                    };
                }
            }
            catch
            {
                return new ImageInfo
                {
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    Dimensions = "Ошибка",
                    Resolution = "Ошибка",
                    ColorDepth = "Ошибка",
                    Compression = "Файл поврежден/Не PCX"
                };
            }
        }
    }
}
