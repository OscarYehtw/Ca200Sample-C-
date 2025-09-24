using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ca200SampleConsole.Models;
using Ca200SampleConsole.Devices;

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
    }
}
