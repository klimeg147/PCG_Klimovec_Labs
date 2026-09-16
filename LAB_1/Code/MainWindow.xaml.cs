using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PCG_Lab1_var10
{
    public partial class MainWindow : Window
    {
        private bool _isUpdating = false;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += (s, e) => UpdateAllFromRgb(128, 128, 128);
        }

        #region Математика перевода цветов

        private static (double C, double M, double Y, double K) RgbToCmyk(double r, double g, double b)
        {
            double rf = r / 255.0; double gf = g / 255.0; double bf = b / 255.0;
            double k = 1.0 - Math.Max(rf, Math.Max(gf, bf));
            if (k == 1.0) return (0, 0, 0, 100);
            return ((1.0 - rf - k) / (1.0 - k) * 100, (1.0 - gf - k) / (1.0 - k) * 100, (1.0 - bf - k) / (1.0 - k) * 100, k * 100);
        }

        private static (double R, double G, double B) CmykToRgb(double c, double m, double y, double k)
        {
            return (255.0 * (1.0 - c / 100.0) * (1.0 - k / 100.0), 255.0 * (1.0 - m / 100.0) * (1.0 - k / 100.0), 255.0 * (1.0 - y / 100.0) * (1.0 - k / 100.0));
        }

        private static (double L, double A, double B) RgbToLab(double r, double g, double b)
        {
            double rL = r / 255.0; double gL = g / 255.0; double bL = b / 255.0;
            rL = rL > 0.04045 ? Math.Pow((rL + 0.055) / 1.055, 2.4) : rL / 12.92;
            gL = gL > 0.04045 ? Math.Pow((gL + 0.055) / 1.055, 2.4) : gL / 12.92;
            bL = bL > 0.04045 ? Math.Pow((bL + 0.055) / 1.055, 2.4) : bL / 12.92;

            double x = (rL * 0.4124 + gL * 0.3576 + bL * 0.1805) * 100;
            double y = (rL * 0.2126 + gL * 0.7152 + bL * 0.0722) * 100;
            double z = (rL * 0.0193 + gL * 0.1192 + bL * 0.9505) * 100;

            double fx = x / 95.047; double fy = y / 100.00; double fz = z / 108.883;
            fx = fx > 0.008856 ? Math.Pow(fx, 1.0 / 3.0) : (7.787 * fx) + (16.0 / 116.0);
            fy = fy > 0.008856 ? Math.Pow(fy, 1.0 / 3.0) : (7.787 * fy) + (16.0 / 116.0);
            fz = fz > 0.008856 ? Math.Pow(fz, 1.0 / 3.0) : (7.787 * fz) + (16.0 / 116.0);

            return ((116.0 * fy) - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz));
        }

        private static (double R, double G, double B, bool outOfGamut) LabToRgb(double L, double a, double b)
        {
            double fy = (L + 16.0) / 116.0;
            double fx = a / 500.0 + fy;
            double fz = fy - b / 200.0;

            double xr = Math.Pow(fx, 3) > 0.008856 ? Math.Pow(fx, 3) : (fx - 16.0 / 116.0) / 7.787;
            double yr = Math.Pow(fy, 3) > 0.008856 ? Math.Pow(fy, 3) : (fy - 16.0 / 116.0) / 7.787;
            double zr = Math.Pow(fz, 3) > 0.008856 ? Math.Pow(fz, 3) : (fz - 16.0 / 116.0) / 7.787;

            double x = xr * 95.047 / 100.0; double y = yr * 100.00 / 100.0; double z = zr * 108.883 / 100.0;

            double rL = x * 3.2406 + y * -1.5372 + z * -0.4986;
            double gL = x * -0.9689 + y * 1.8758 + z * 0.0415;
            double bL = x * 0.0557 + y * -0.2040 + z * 1.0570;

            rL = rL > 0.0031308 ? 1.055 * Math.Pow(rL, 1.0 / 2.4) - 0.055 : 12.92 * rL;
            gL = gL > 0.0031308 ? 1.055 * Math.Pow(gL, 1.0 / 2.4) - 0.055 : 12.92 * gL;
            bL = bL > 0.0031308 ? 1.055 * Math.Pow(bL, 1.0 / 2.4) - 0.055 : 12.92 * bL;

            bool gamutViolation = (rL < 0 || rL > 1 || gL < 0 || gL > 1 || bL < 0 || bL > 1);
            return (Math.Clamp(rL * 255, 0, 255), Math.Clamp(gL * 255, 0, 255), Math.Clamp(bL * 255, 0, 255), gamutViolation);
        }

        #endregion
        #region Синхронизация контролов (Слушатели событий)

        private void UpdateAllFromRgb(double r, double g, double b, bool gamutWarning = false)
        {
            if (slR == null || slG == null || slB_Rgb == null ||
                slC == null || slM == null || slY == null || slK == null ||
                slL == null || slA == null || slB == null) return;

            _isUpdating = true;

            ColorPreview.Background = new SolidColorBrush(Color.FromRgb((byte)r, (byte)g, (byte)b));
            WarningPanel.Visibility = gamutWarning ? Visibility.Visible : Visibility.Collapsed;

            slR.Value = r; slG.Value = g; slB_Rgb.Value = b;
            var cmyk = RgbToCmyk(r, g, b);
            slC.Value = cmyk.C; slM.Value = cmyk.M; slY.Value = cmyk.Y; slK.Value = cmyk.K;
            var lab = RgbToLab(r, g, b);
            slL.Value = lab.L; slA.Value = lab.A; slB.Value = lab.B;

            txtR.Text = Math.Round(r).ToString(); txtG.Text = Math.Round(g).ToString(); txtB_Rgb.Text = Math.Round(b).ToString();
            txtC.Text = Math.Round(cmyk.C).ToString(); txtM.Text = Math.Round(cmyk.M).ToString(); txtY.Text = Math.Round(cmyk.Y).ToString(); txtK.Text = Math.Round(cmyk.K).ToString();
            txtL.Text = Math.Round(lab.L).ToString(); txtA.Text = Math.Round(lab.A).ToString(); txtB.Text = Math.Round(lab.B).ToString();

            _isUpdating = false;

            RenderDynamicPalettes();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;
            if (slR == null || slG == null || slB_Rgb == null ||
                slC == null || slM == null || slY == null || slK == null ||
                slL == null || slA == null || slB == null) return;

            if (sender == slR || sender == slG || sender == slB_Rgb)
                UpdateAllFromRgb(slR.Value, slG.Value, slB_Rgb.Value);
            else if (sender == slC || sender == slM || sender == slY || sender == slK)
            {
                var rgb = CmykToRgb(slC.Value, slM.Value, slY.Value, slK.Value);
                UpdateAllFromRgb(rgb.R, rgb.G, rgb.B);
            }
            else if (sender == slL || sender == slA || sender == slB)
            {
                var res = LabToRgb(slL.Value, slA.Value, slB.Value);
                UpdateAllFromRgb(res.R, res.G, res.B, res.outOfGamut);
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (txtR == null || txtG == null || txtB_Rgb == null ||
                txtC == null || txtM == null || txtY == null || txtK == null ||
                txtL == null || txtA == null || txtB == null) return;

            if (sender is TextBox tb && double.TryParse(tb.Text, out double _))
            {
                if ((tb == txtR || tb == txtG || tb == txtB_Rgb) && double.TryParse(txtR.Text, out double r) && double.TryParse(txtG.Text, out double g) && double.TryParse(txtB_Rgb.Text, out double b))
                    UpdateAllFromRgb(r, g, b);
                else if ((tb == txtC || tb == txtM || tb == txtY || tb == txtK) && double.TryParse(txtC.Text, out double c) && double.TryParse(txtM.Text, out double m) && double.TryParse(txtY.Text, out double y) && double.TryParse(txtK.Text, out double k))
                {
                    var rgb = CmykToRgb(c, m, y, k);
                    UpdateAllFromRgb(rgb.R, rgb.G, rgb.B);
                }
                else if ((tb == txtL || tb == txtA || tb == txtB) && double.TryParse(txtL.Text, out double l) && double.TryParse(txtA.Text, out double a) && double.TryParse(txtB.Text, out double bLab))
                {
                    var res = LabToRgb(l, a, bLab);
                    UpdateAllFromRgb(res.R, res.G, res.B, res.outOfGamut);
                }
            }
        }

        #endregion

        #region Отрисовка динамических палитр

        private void RenderDynamicPalettes()
        {
            if (RgbPalette == null || CmykPalette == null || LabPalette == null) return;

            WriteableBitmap rgbBmp = new WriteableBitmap(200, 200, 96, 96, PixelFormats.Bgr32, null);
            int[] rgbPixels = new int[200 * 200];
            byte currentB = (byte)slB_Rgb.Value;
            for (int y = 0; y < 200; y++)
            {
                for (int x = 0; x < 200; x++)
                {
                    byte r = (byte)(x / 199.0 * 255);
                    byte g = (byte)((199 - y) / 199.0 * 255);
                    rgbPixels[y * 200 + x] = (r << 16) | (g << 8) | currentB;
                }
            }
            rgbBmp.WritePixels(new Int32Rect(0, 0, 200, 200), rgbPixels, 200 * 4, 0);
            RgbPalette.Source = rgbBmp;

            WriteableBitmap cmykBmp = new WriteableBitmap(200, 200, 96, 96, PixelFormats.Bgr32, null);
            int[] cmykPixels = new int[200 * 200];
            double fixedY = slY.Value;
            double fixedK = slK.Value;
            for (int y = 0; y < 200; y++)
            {
                for (int x = 0; x < 200; x++)
                {
                    double c = (x / 199.0) * 100;
                    double m = ((199 - y) / 199.0) * 100;
                    var (r, g, b) = CmykToRgb(c, m, fixedY, fixedK);
                    cmykPixels[y * 200 + x] = ((int)Math.Clamp(r, 0, 255) << 16) | ((int)Math.Clamp(g, 0, 255) << 8) | (int)Math.Clamp(b, 0, 255);
                }
            }
            cmykBmp.WritePixels(new Int32Rect(0, 0, 200, 200), cmykPixels, 200 * 4, 0);
            CmykPalette.Source = cmykBmp;

            WriteableBitmap labBmp = new WriteableBitmap(200, 200, 96, 96, PixelFormats.Bgr32, null);
            int[] labPixels = new int[200 * 200];
            double fixedL = slL.Value;
            for (int y = 0; y < 200; y++)
            {
                for (int x = 0; x < 200; x++)
                {
                    double a = -128 + (x / 199.0) * 255;
                    double b = 127 - (y / 199.0) * 255;
                    var res = LabToRgb(fixedL, a, b);
                    labPixels[y * 200 + x] = ((int)res.R << 16) | ((int)res.G << 8) | (int)res.B;
                }
            }
            labBmp.WritePixels(new Int32Rect(0, 0, 200, 200), labPixels, 200 * 4, 0);
            LabPalette.Source = labBmp;
        }

        private void Pad_MouseDown(object sender, MouseButtonEventArgs e) => HandlePadClick(sender, e);
        private void Pad_MouseMove(object sender, MouseEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) HandlePadClick(sender, e); }

        private void HandlePadClick(object sender, MouseEventArgs e)
        {
            if (sender is Image img)
            {
                Point p = e.GetPosition(img);
                double pctX = Math.Clamp(p.X / img.ActualWidth, 0, 1);
                double pctY = Math.Clamp(p.Y / img.ActualHeight, 0, 1);

                if (img == RgbPalette)
                    UpdateAllFromRgb(pctX * 255, (1 - pctY) * 255, slB_Rgb.Value);
                else if (img == CmykPalette)
                {
                    var rgb = CmykToRgb(pctX * 100, (1 - pctY) * 100, slY.Value, slK.Value);
                    UpdateAllFromRgb(rgb.R, rgb.G, rgb.B);
                }
                else if (img == LabPalette)
                {
                    double a = -128 + pctX * 255;
                    double b = 127 - pctY * 255;
                    var res = LabToRgb(slL.Value, a, b);
                    UpdateAllFromRgb(res.R, res.G, res.B, res.outOfGamut);
                }
            }
        }

        #endregion
    }
}
