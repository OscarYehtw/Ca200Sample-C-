using System;
using System.Globalization;
using System.IO;
using System.Linq;
using ScottPlot;

namespace Ca200SampleConsole.Services
{
    public static class GammaPlotter
    {
        public static void PlotFromCsv(string csvFile, string outputFile)
        {
            // === 1. 讀取 CSV ===
            var lines = File.ReadAllLines(csvFile)
                .Skip(1) // 跳過標題列
                .Select(line => line.Split(','))
                .ToArray();

            double[] x = lines.Select(l => double.Parse(l[0], CultureInfo.InvariantCulture)).ToArray();
            double[] measured = lines.Select(l => double.Parse(l[1], CultureInfo.InvariantCulture)).ToArray();
            double[] ideal = lines.Select(l => double.Parse(l[2], CultureInfo.InvariantCulture)).ToArray();

            // === 2. 建立繪圖 ===
            var plt = new ScottPlot.Plot();

            // Measured scatter
            var measuredScatter = plt.Add.Scatter(x, measured);
            measuredScatter.Color = Colors.Red;
            measuredScatter.MarkerSize = 5;
            measuredScatter.LegendText = "Measured";

            // Ideal scatter
            var idealScatter = plt.Add.Scatter(x, ideal);
            idealScatter.Color = Colors.Blue;
            idealScatter.MarkerSize = 5;
            idealScatter.LegendText = "Ideal Gamma";

            // === 3. 設定標題/軸 ===
            plt.Title("Gamma Curve");
            plt.XLabel("Gray Level");
            plt.YLabel("Luminance");
            plt.Legend.IsVisible = true;

            // === 4. 輸出圖片 ===
            plt.SavePng(outputFile, 800, 600);
            Console.WriteLine($"Gamma curve saved to {outputFile}");
        }
    }
}
