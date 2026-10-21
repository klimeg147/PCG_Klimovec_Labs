using Microsoft.Win32;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace LAB3
{
    public partial class MainWindow : Window
    {
        private Bitmap _originalBitmap;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Изображения (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp";

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    Bitmap loadedBitmap = new Bitmap(openFileDialog.FileName);

                    _originalBitmap = new Bitmap(loadedBitmap.Width, loadedBitmap.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    using (Graphics g = Graphics.FromImage(_originalBitmap))
                    {
                        g.Clear(System.Drawing.Color.White);
                        g.DrawImage(loadedBitmap, 0, 0, loadedBitmap.Width, loadedBitmap.Height);
                    }

                    ImgOriginal.Source = ConvertBitmapToSource(_originalBitmap);
                    ImgProcessed.Source = null;
                    TxtStatus.Text = $"Загружено: {Path.GetFileName(openFileDialog.FileName)} ({_originalBitmap.Width}x{_originalBitmap.Height})";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBitmap == null)
            {
                MessageBox.Show("Сначала загрузите изображение!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int selectedMethod = CmbMethods.SelectedIndex;
                EdgeMode edgeMode = CmbEdge.SelectedIndex == 1 ? EdgeMode.Reflect : EdgeMode.Clamp;

                Bitmap resultBitmap = new Bitmap(_originalBitmap.Width, _originalBitmap.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                using (FastImage sourceFast = new FastImage(_originalBitmap))
                using (FastImage destFast = new FastImage(resultBitmap))
                {
                    switch (selectedMethod)
                    {
                        case 0: // Медианный фильтр
                            int radius = 1;
                            ImageProcessor.ApplyMedianFilter(sourceFast, destFast, radius, edgeMode);
                            break;

                        case 1: // Глобальная пороговая обработка (Оцу)
                            ImageProcessor.ApplyOtsuThreshold(sourceFast, destFast);
                            break;

                        case 2: // Глобальный метод по среднему
                            ImageProcessor.ApplyGlobalMeanThreshold(sourceFast, destFast);
                            break;

                        case 3: // Адаптивная пороговая обработка
                            ImageProcessor.ApplyAdaptiveThreshold(sourceFast, destFast, radius: 10, constantOffset: 10);
                            break;
                    }
                }

                ImgProcessed.Source = ConvertBitmapToSource(resultBitmap);
                TxtStatus.Text = "Обработка завершена успешно!";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обработке: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private BitmapImage ConvertBitmapToSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }
    }
}