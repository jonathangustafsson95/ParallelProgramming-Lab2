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
using System.Diagnostics;
using Amplifier.OpenCL;
using Amplifier;
using System.IO;
using CsvHelper;
using System.Globalization;
using System.Runtime;

namespace MandelWindow
{
    class Program
    {
        static WriteableBitmap bitmap;
        static dynamic exec;
        static Window windows;
        static Image image;
        static bool parallel = true;

        [STAThread]
        static void Main(string[] args)
        {


            image = new Image();
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(image, EdgeMode.Aliased);

            windows = new Window();
            windows.Content = image;

            windows.KeyDown += new KeyEventHandler(windows_KeyDown);

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

        static void windows_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.P:
                    parallel = !parallel;
                    Console.WriteLine($"Parallel variable = {parallel.ToString()}!");
                    break;
                case Key.R:
                    Console.WriteLine("Running experiments, this may take a while.");
                    RunExperiments();
                    break;

                default:
                    Console.WriteLine($"Key '{e.Key}' has no command!");
                    break;
            }

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
               
        public static TimeSpan UpdateMandel()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            int[] values = new int[bitmap.PixelHeight * bitmap.PixelWidth];
            if (parallel)
            {
                OpenCLCompiler compiler = new OpenCLCompiler();
                compiler.UseDevice(0);
                compiler.CompileKernel(typeof(Kernel));
                exec = compiler.GetExec();

                //exec.ParallelIter(mandelCenterX, mandelCenterY, mandelWidth, mandelHeight, bitmap.PixelWidth, bitmap.PixelHeight, mandelDepth, values);
                compiler.Execute("ParallelIter", mandelCenterX, mandelCenterY, mandelWidth, mandelHeight, bitmap.PixelWidth, bitmap.PixelHeight, mandelDepth, values);
                compiler.Dispose();
            }
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

                            int light;
                            if (parallel)
                            {
                                light = values[row + bitmap.PixelHeight * column];
                            }
                            else
                                light = IterCount(mandelCenterX - mandelWidth + column * ((mandelWidth * 2.0) / bitmap.PixelWidth), mandelCenterY - mandelHeight + row * ((mandelHeight * 2.0) / bitmap.PixelHeight));

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
                    Array.Clear(values, 0, values.Length);
                }

                // Specify the area of the bitmap that changed.
                bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            }
            finally
            {
                // Release the back buffer and make it available for display.
                bitmap.Unlock();
            }
            stopWatch.Stop();
            return stopWatch.Elapsed;
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

        public static void HsvToRgb(double h, double S, double V, out int r, out int g, out int b)
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

        public static void RunExperiments()
        {
            List<ExperimentResult> results = new List<ExperimentResult>();

            // Iterate through max depth
            int defaultDepth = mandelDepth;
            int step = 100;
            for (int i = 100; i <= 2100; i+=step)
            {
                mandelDepth = i;
                parallel = false;
                Console.WriteLine($"Executing Depth Test, parallel = False, mandelDepth = {i}");
                results.Add(new ExperimentResult("Depth Test", parallel ? "Parallel" : "Sequential", i, mandelWidth, bitmap.PixelWidth, UpdateMandel().TotalSeconds));

                parallel = true;
                Console.WriteLine($"Executing Depth Test, parallel = True mandelDepth = {i}");
                results.Add(new ExperimentResult("Depth Test", parallel ? "Parallel" : "Sequential", i, mandelWidth, bitmap.PixelWidth, UpdateMandel().TotalSeconds));
            }
            //Restore mandelDepth
            mandelDepth = defaultDepth;

            // iterate through mandelbrot height/width
            double defaultWidthHeight = mandelWidth;
            for (double i = 2; i >= 0.00000191; i /= 2)
            {
                mandelWidth = i;
                mandelHeight = i;
                parallel = false;
                Console.WriteLine($"Executing Dimensions Test, parallel = False, mandelDepth = {i}");
                results.Add(new ExperimentResult("Mandel Dimensions", parallel ? "Parallel" : "Sequential", mandelDepth, i, bitmap.PixelWidth, UpdateMandel().TotalSeconds));

                parallel = true;
                Console.WriteLine($"Executing Dimensions Test, parallel = True, mandelDepth = {i}");
                results.Add(new ExperimentResult("Mandel Dimensions", parallel ? "Parallel" : "Sequential", mandelDepth, i, bitmap.PixelWidth, UpdateMandel().TotalSeconds));
            }
            //restore dimensions
            mandelWidth = defaultWidthHeight;
            mandelHeight = defaultWidthHeight;

            //iterate through amount of pixels

            WriteableBitmap defaultBitmap = bitmap;
            mandelCenterX = -1.25441900889079;
            mandelCenterY = -0.381432545744887;
            for (int i = 360; i <= 1920; i += 80)
            {
                bitmap = new WriteableBitmap(
                    i,
                    i * 9 / 16,
                    96,
                    96,
                    PixelFormats.Bgr32,
                    null);
                parallel = false;
                Console.WriteLine($"Executing Pixel Test, parallel = True, Pixels = {i}x{(i * 9/16)}");
                results.Add(new ExperimentResult("Pixels", parallel ? "Parallel" : "Sequential", mandelDepth, mandelWidth, i, UpdateMandel().TotalSeconds));

                parallel = true;
                Console.WriteLine($"Executing Pixel Test, parallel = True, Pixels = {i}x{(i * 9 / 16)}");
                results.Add(new ExperimentResult("Pixels", parallel ? "Parallel" : "Sequential", mandelDepth, mandelWidth, i, UpdateMandel().TotalSeconds));
            }
            bitmap = defaultBitmap;

            // Write Results to CSV
            Console.WriteLine("Done!");
            WriteToCsv(results);
        }

        public static void WriteToCsv(List<ExperimentResult> results)
        {
            using (var writer = new StreamWriter("data.csv"))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(results);
            }
        }
    }

    class Kernel : OpenCLFunctions
    {
        [OpenCLKernel]
        void ParallelIter(double mandelCenterX, double mandelCenterY, double mandelWidth, double mandelHeight, int PixelWidth, int PixelHeight, double mandelDepth, [Global] int[] values)
        {
            int id = get_global_id(0);
            int column = id / PixelHeight;
            int row = id % PixelHeight;
            double cx = mandelCenterX - mandelWidth + column * ((mandelWidth * 2.0) / PixelWidth);
            double cy = mandelCenterY - mandelHeight + row * ((mandelHeight * 2.0) / PixelHeight);

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


            values[id] = result;
        }
    }
    
    public class ExperimentResult
    {
        public string Test { get; set; }
        public string Type { get; set; }
        public int MaxDepth { get; set; }
        public double Dimensions { get; set; }
        public int Pixels { get; set; }
        public double Time { get; set; }

        public ExperimentResult(string test, string type, int maxDepth, double dimension, int pixels, double time)
        {
            Test = test;
            Type = type;
            MaxDepth = maxDepth;
            Dimensions = dimension;
            Pixels = pixels;
            Time = time;
        }
    }
}

