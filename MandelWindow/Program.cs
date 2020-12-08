using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;

namespace MandelWindow
{
    class Program
    {
        static WriteableBitmap bitmap;
        static Window windows;
        static Image image;

        [STAThread]
        static void Main(string[] args)
        {
            image = new Image();
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(image, EdgeMode.Aliased);

            windows = new Window();
            windows.Content = image;
            windows.Show();

            bitmap = new WriteableBitmap(
                (int)windows.ActualWidth,
                (int)windows.ActualHeight,
                96,
                96,
                PixelFormats.Bgr32,
                null);

            image.Source = bitmap;

            image.Stretch = Stretch.None;
            image.HorizontalAlignment = HorizontalAlignment.Left;
            image.VerticalAlignment = VerticalAlignment.Top;

            image.MouseLeftButtonDown +=
                new MouseButtonEventHandler(image_MouseLeftButtonDown);
            image.MouseRightButtonDown +=
                new MouseButtonEventHandler(image_MouseRightButtonDown);
            image.MouseMove +=
                new MouseEventHandler(image_MouseMove);

            windows.MouseWheel += new MouseWheelEventHandler(window_MouseWheel);


            UpdateMandel();

            Application app = new Application();
            app.Run();
        }

        static void image_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            int column = (int)e.GetPosition(image).X;
            int row = (int)e.GetPosition(image).Y;

            mandelCenterX = mandelCenterX - mandelWidth + column * ((mandelWidth * 2.0) / bitmap.PixelWidth);
            mandelCenterY = mandelCenterY - mandelHeight + row * ((mandelHeight * 2.0) / bitmap.PixelHeight);
            mandelWidth *= 2.0;
            mandelHeight *= 2.0;

            UpdateMandel();
        }

        static void image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            int column = (int)e.GetPosition(image).X;
            int row = (int)e.GetPosition(image).Y;

            mandelCenterX = mandelCenterX - mandelWidth + column * ((mandelWidth * 2.0) / bitmap.PixelWidth);
            mandelCenterY = mandelCenterY - mandelHeight + row * ((mandelHeight * 2.0) / bitmap.PixelHeight);
            mandelWidth /= 2.0;
            mandelHeight /= 2.0;
           
            UpdateMandel();
        }

        static void window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            int column = (int)e.GetPosition(image).X;
            int row = (int)e.GetPosition(image).Y;

            if (e.Delta > 0)
            {
                mandelCenterX = mandelCenterX - mandelWidth + column * ((mandelWidth * 2.0) / bitmap.PixelWidth);
                mandelCenterY = mandelCenterY - mandelHeight + row * ((mandelHeight * 2.0) / bitmap.PixelHeight);
                mandelWidth /= 2.0;
                mandelHeight /= 2.0;
            }
            else
            {
                mandelCenterX = mandelCenterX - mandelWidth + column * ((mandelWidth * 2.0) / bitmap.PixelWidth);
                mandelCenterY = mandelCenterY - mandelHeight + row * ((mandelHeight * 2.0) / bitmap.PixelHeight);
                mandelWidth *= 2.0;
                mandelHeight *= 2.0;
            }

            UpdateMandel();
        }

        static void image_MouseMove(object sender, MouseEventArgs e)
        {
            int column = (int)e.GetPosition(image).X;
            int row = (int)e.GetPosition(image).Y;

            double mouseCenterX = mandelCenterX - mandelWidth + column * ((mandelWidth * 2.0) / bitmap.PixelWidth);
            double mouseCenterY = mandelCenterY - mandelHeight + row * ((mandelHeight * 2.0) / bitmap.PixelHeight);

            windows.Title = $"Mandelbrot center X:{mouseCenterX} Y:{mouseCenterY}";
        }

        static double mandelCenterX = 0.0;
        static double mandelCenterY = 0.0;
        static double mandelWidth = 2.0;
        static double mandelHeight = 2.0;

        public static int mandelDepth = 360;

        public static void UpdateMandel()
        {
            try
            {
                // Reserve the back buffer for updates.
                bitmap.Lock();

                unsafe
                {
                    for (int row = 0; row < bitmap.PixelHeight; row++)
                    {
                        for (int column = 0; column < bitmap.PixelWidth; column++)
                        {
                            // Get a pointer to the back buffer.
                            IntPtr pBackBuffer = bitmap.BackBuffer;

                            // Find the address of the pixel to draw.
                            pBackBuffer += row * bitmap.BackBufferStride;
                            pBackBuffer += column * 4;

                            int light = IterCount(mandelCenterX - mandelWidth + column * ((mandelWidth * 2.0) / bitmap.PixelWidth), mandelCenterY - mandelHeight + row * ((mandelHeight * 2.0) / bitmap.PixelHeight));

                            int R, G, B;
                            HsvToRgb(light, 1.0, light < mandelDepth ? 1.0 : 0.0, out R, out G, out B);

                            // Compute the pixel's color.
                            int color_data = R << 16; // R
                            color_data |= G << 8;   // G
                            color_data |= B << 0;   // B

                            // Assign the color data to the pixel.
                            *((int*)pBackBuffer) = color_data;
                        }
                    }
                }

                // Specify the area of the bitmap that changed.
                bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            }
            finally
            {
                // Release the back buffer and make it available for display.
                bitmap.Unlock();
            }
        }
        public static int IterCount(double cx, double cy)
        {
            int result = 0;
            double x = 0.0f;
            double y = 0.0f;
            double xx = 0.0f, yy = 0.0;
            while (xx + yy <= 4.0 && result < mandelDepth) // are we out of control disk?
            {
                xx = x * x;
                yy = y * y;
                double xtmp = xx - yy + cx;
                y = 2.0f * x * y + cy; // computes z^2 + c
                x = xtmp;
                result++;
            }
            return result;
        }

        static void HsvToRgb(double h, double S, double V, out int r, out int g, out int b)
        {
            double H = h;
            while (H < 0) { H += 360; };
            while (H >= 360) { H -= 360; };
            double R, G, B;
            if (V <= 0)
            { R = G = B = 0; }
            else if (S <= 0)
            {
                R = G = B = V;
            }
            else
            {
                double hf = H / 60.0;
                int i = (int)Math.Floor(hf);
                double f = hf - i;
                double pv = V * (1 - S);
                double qv = V * (1 - S * f);
                double tv = V * (1 - S * (1 - f));
                switch (i)
                {

                    // Red is the dominant color

                    case 0:
                        R = V;
                        G = tv;
                        B = pv;
                        break;

                    // Green is the dominant color

                    case 1:
                        R = qv;
                        G = V;
                        B = pv;
                        break;
                    case 2:
                        R = pv;
                        G = V;
                        B = tv;
                        break;

                    // Blue is the dominant color

                    case 3:
                        R = pv;
                        G = qv;
                        B = V;
                        break;
                    case 4:
                        R = tv;
                        G = pv;
                        B = V;
                        break;

                    // Red is the dominant color

                    case 5:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // Just in case we overshoot on our math by a little, we put these here. Since its a switch it won't slow us down at all to put these here.

                    case 6:
                        R = V;
                        G = tv;
                        B = pv;
                        break;
                    case -1:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // The color is not defined, we should throw an error.

                    default:
                        //LFATAL("i Value error in Pixel conversion, Value is %d", i);
                        R = G = B = V; // Just pretend its black/white
                        break;
                }
            }
            r = Clamp((int)(R * 255.0));
            g = Clamp((int)(G * 255.0));
            b = Clamp((int)(B * 255.0));
        }

        /// <summary>
        /// Clamp a value to 0-255
        /// </summary>
        static int Clamp(int i)
        {
            if (i < 0) return 0;
            if (i > 255) return 255;
            return i;
        }
    }
}
