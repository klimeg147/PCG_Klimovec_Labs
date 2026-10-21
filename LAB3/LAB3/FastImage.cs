using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LAB3
{
    public enum EdgeMode
    {
        Clamp,
        Reflect
    }

    public class FastImage : IDisposable
    {
        private Bitmap _bitmap;
        private BitmapData _bitmapData;
        private unsafe byte* _scan0;
        private int _stride;
        private int _bytesPerPixel;

        public int Width { get; }
        public int Height { get; }

        public FastImage(Bitmap bitmap)
        {
            _bitmap = bitmap;
            Width = _bitmap.Width;
            Height = _bitmap.Height;

            Rectangle rect = new Rectangle(0, 0, Width, Height);
            _bitmapData = _bitmap.LockBits(rect, ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            _stride = _bitmapData.Stride;
            unsafe
            {
                _scan0 = (byte*)_bitmapData.Scan0;
            }
            _bytesPerPixel = 3;
        }
        public Bitmap Bitmap => _bitmap;

        public void GetPixelSafe(int x, int y, EdgeMode edgeMode, out byte r, out byte g, out byte b)
        {
            if (x < 0) x = (edgeMode == EdgeMode.Clamp) ? 0 : -x;
            if (x >= Width) x = (edgeMode == EdgeMode.Clamp) ? Width - 1 : 2 * Width - 2 - x;
            if (y < 0) y = (edgeMode == EdgeMode.Clamp) ? 0 : -y;
            if (y >= Height) y = (edgeMode == EdgeMode.Clamp) ? Height - 1 : 2 * Height - 2 - y;

            unsafe
            {
                byte* ptr = _scan0 + y * _stride + x * _bytesPerPixel;
                b = ptr[0]; // Синий
                g = ptr[1]; // Зеленый
                r = ptr[2]; // Красный
            }
        }

        public void SetPixel(int x, int y, byte r, byte g, byte b)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;

            unsafe
            {
                byte* ptr = _scan0 + y * _stride + x * _bytesPerPixel;
                ptr[0] = b; // Синий
                ptr[1] = g; // Зеленый
                ptr[2] = r; // Красный
            }
        }

        public BitmapSource ToBitmapSource()
        {
            var handle = _bitmap.GetHbitmap();
            try
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    handle,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(handle);
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public void Dispose()
        {
            if (_bitmapData != null)
            {
                _bitmap.UnlockBits(_bitmapData);
                _bitmapData = null;
            }
        }
    }
}