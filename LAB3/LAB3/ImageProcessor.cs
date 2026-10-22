using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace LAB3
{
    public static class ImageProcessor
    {
        public static void ApplyMedianFilter(FastImage source, FastImage destination, int radius, EdgeMode edgeMode)
        {
            int width = source.Width;
            int height = source.Height;
            int windowSize = (2 * radius + 1) * (2 * radius + 1);

            System.Threading.Tasks.Parallel.For(0, height, y =>
            {
                int[] rArray = new int[windowSize];
                int[] gArray = new int[windowSize];
                int[] bArray = new int[windowSize];

                for (int x = 0; x < width; x++)
                {
                    int index = 0;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;

                            source.GetPixelSafe(nx, ny, edgeMode, out byte r, out byte g, out byte b);
                            rArray[index] = r;
                            gArray[index] = g;
                            bArray[index] = b;
                            index++;
                        }
                    }

                    Array.Sort(rArray, 0, windowSize);
                    Array.Sort(gArray, 0, windowSize);
                    Array.Sort(bArray, 0, windowSize);

                    int medianIndex = windowSize / 2;
                    
                    destination.SetPixel(x, y, (byte)rArray[medianIndex], (byte)gArray[medianIndex], (byte)bArray[medianIndex]);
                }
            });
        }

        private static void SortTriplets(int[] r, int[] g, int[] b, int length)
        {
            for (int i = 0; i < length - 1; i++)
            {
                for (int j = 0; j < length - i - 1; j++)
                {
                    int bright1 = r[j] + g[j] + b[j];
                    int bright2 = r[j + 1] + g[j + 1] + b[j + 1];

                    if (bright1 > bright2)
                    {
                        int tr = r[j]; r[j] = r[j + 1]; r[j + 1] = tr;
                        int tg = g[j]; g[j] = g[j + 1]; g[j + 1] = tg;
                        int tb = b[j]; b[j] = b[j + 1]; b[j + 1] = tb;
                    }
                }
            }
        }

        public static void ApplyOtsuThreshold(FastImage source, FastImage destination)
        {
            int width = source.Width;
            int height = source.Height;
            int totalPixels = width * height;
            int[] hist = new int[256];

            // Гистограмма яркости
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    source.GetPixelSafe(x, y, EdgeMode.Clamp, out byte r, out byte g, out byte b);
                    int gray = (r + g + b) / 3;
                    hist[gray]++;
                }
            }

            //Оптимальный порог методом Оцу
            double sum = 0;
            for (int i = 0; i < 256; i++) sum += i * hist[i];

            double sumB = 0;
            int wB = 0;
            int wF = 0;
            double maxVariance = 0;
            int threshold = 0;

            for (int t = 0; t < 256; t++)
            {
                wB += hist[t];
                if (wB == 0) continue;

                wF = totalPixels - wB;
                if (wF == 0) break;

                sumB += (double)(t * hist[t]);

                double mB = sumB / wB;
                double mF = (sum - sumB) / wF;

                double variance = (double)wB * wF * (mB - mF) * (mB - mF);

                if (variance > maxVariance)
                {
                    maxVariance = variance;
                    threshold = t;
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    source.GetPixelSafe(x, y, EdgeMode.Clamp, out byte r, out byte g, out byte b);
                    int gray = (r + g + b) / 3;
                    byte val = gray >= threshold ? (byte)255 : (byte)0;
                    destination.SetPixel(x, y, val, val, val);
                }
            }
        }

        public static void ApplyGlobalMeanThreshold(FastImage source, FastImage destination)
        {
            int width = source.Width;
            int height = source.Height;
            long sum = 0;
            int totalPixels = width * height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    source.GetPixelSafe(x, y, EdgeMode.Clamp, out byte r, out byte g, out byte b);
                    sum += (r + g + b) / 3;
                }
            }
            byte threshold = (byte)(sum / totalPixels);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    source.GetPixelSafe(x, y, EdgeMode.Clamp, out byte r, out byte g, out byte b);
                    int gray = (r + g + b) / 3;
                    byte val = gray >= threshold ? (byte)255 : (byte)0;
                    destination.SetPixel(x, y, val, val, val);
                }
            }
        }

        public static void ApplyAdaptiveThreshold(FastImage source, FastImage destination, int radius, int constantOffset)
        {
            int width = source.Width;
            int height = source.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    long localSum = 0;
                    int count = 0;

                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                source.GetPixelSafe(nx, ny, EdgeMode.Clamp, out byte r, out byte g, out byte b);
                                localSum += (r + g + b) / 3;
                                count++;
                            }
                        }
                    }

                    double localMean = (double)localSum / count;
                    double threshold = localMean - constantOffset;

                    source.GetPixelSafe(x, y, EdgeMode.Clamp, out byte cr, out byte cg, out byte cb);
                    int currentGray = (cr + cg + cb) / 3;

                    byte val = currentGray >= threshold ? (byte)255 : (byte)0;
                    destination.SetPixel(x, y, val, val, val);
                }
            }
        }
    }
}