using Ca200SampleConsole.Devices;
using Ca200SampleConsole.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;
//using System.Reflection;
//using static System.Net.Mime.MediaTypeNames;

namespace Ca200SampleConsole.Services
{
    class MeasurementService
    {
        private Ca200Controller ca200;
        private BacklightController backlight;

        public MeasurementService(Ca200Controller ca200, BacklightController backlight)
        {
            this.ca200 = ca200;
            this.backlight = backlight;
        }

        public void RunMeasurements(List<GrayLevel> grayData, string outputCsv)
        {
            using var writer = new StreamWriter(outputCsv);
            writer.WriteLine("Index,Lv,x,y,T,duv");

            backlight.Open();
            backlight.Start();
            backlight.SetBrightness(255);

            for (int i = 0; i < grayData.Count; i++)
            {
                int gray = grayData[i].Gray;
                int rgb = (gray << 16) | (gray << 8) | gray;
                backlight.FillColor(rgb);

                Thread.Sleep(100);

                //var (Lv, x, y, T, duv) = ca200.Measure();
                var (Lv, x, y, T, duv) = ca200.Measure(gray);
                writer.WriteLine($"{i},{Lv:0.00}f,{x:0.0000},{y:0.0000},{T:0},{duv:0.0000}");
                grayData[i].Brightness = Lv.ToString("0.00") + "f";
            }

            backlight.Stop();
            backlight.Close();
        }

        public void RunMeasureRGBW(List<GrayLevel> grayData, string outputCsv)
        {
            using var writer = new StreamWriter(outputCsv);
            writer.WriteLine("Index,Channel,Gray,Lv,x,y,T,duv");

            // Create ColorWindow
            var colorWindow = new Form
            {
                Text = "Color Window",
                Width = 480,
                Height = 480,
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                StartPosition = FormStartPosition.CenterScreen
            };

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.Black
            };
            colorWindow.Controls.Add(panel);

            var thread = new Thread(() => Application.Run(colorWindow));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            Thread.Sleep(500); // Ensure the window is initialized

            string[] channels = { "R", "G", "B", "W" };
            int index = 0;

            foreach (var ch in channels)
            {
                for (int i = 0; i < grayData.Count; i++)
                {
                    int gray = grayData[i].Gray;
                    System.Drawing.Color color;

                    switch (ch)
                    {
                        case "R":
                            color = System.Drawing.Color.FromArgb(gray, 0, 0);
                            break;
                        case "G":
                            color = System.Drawing.Color.FromArgb(0, gray, 0);
                            break;
                        case "B":
                            color = System.Drawing.Color.FromArgb(0, 0, gray);
                            break;
                        case "W":
                            color = System.Drawing.Color.FromArgb(gray, gray, gray);
                            break;
                        default:
                            color = System.Drawing.Color.Black;
                            break;
                    }

                    // Update Panel color (cross-thread access)
                    panel.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                    {
                        panel.BackColor = color;
                    }));

                    Thread.Sleep(50); // Allow time for display to update

                    var (Lv, x, y, T, duv) = ca200.Measure(gray);

                    writer.WriteLine($"{index},{ch},{gray},{Lv:0.00},{x:0.0000},{y:0.0000},{T:0},{duv:0.0000}");
                    index++;
                }
            }

            // === Close ColorWindow ===
            if (colorWindow != null && !colorWindow.IsDisposed)
            {
                colorWindow.Invoke((MethodInvoker)(() =>
                {
                    colorWindow.Close();
                }));
            }

            // Wait for UI thread to finish
            thread.Join();
        }
    }
}
