using LAB2;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

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
            TxtProgressStatus.Text = "Сканирование папки...";

            List<string> files = await Task.Run(() => Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly).ToList());

            if (files.Count == 0)
            {
                TxtProgressStatus.Text = "Готово";
                BtnSelectFolder.IsEnabled = true;
                return;
            }

            LoadingProgressBar.Maximum = files.Count;
            LoadingProgressBar.Value = 0;

            var results = new ConcurrentBag<ImageInfo>();
            int processedCount = 0;

            var progress = new Progress<int>(value => {
                LoadingProgressBar.Value = value;
                TxtProgressStatus.Text = $"Обработано: {value} из {files.Count}";
            });

            await Task.Run(() =>
            {
                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file =>
                {
                    ImageInfo info = ImageParser.Parse(file);

                    if (info != null)
                        results.Add(info);

                    int current = System.Threading.Interlocked.Increment(ref processedCount);
                    if (current % 20 == 0 || current == files.Count)
                        ((IProgress<int>)progress).Report(current);

                });
            });


            GridImageResults.ItemsSource = results.ToList();
            TxtTotalFiles.Text = $"Всего файлов обработано: {results.Count}";
            BtnSelectFolder.IsEnabled = true;
            TxtProgressStatus.Text = "Готово!";
        }
    }
}
