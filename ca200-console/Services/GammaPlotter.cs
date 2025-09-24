using ScottPlot;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Ca200SampleConsole.Services
{
    public static class GammaPlotter
    {
        public static void PlotFromCsv(string csvFile, string outputFile)
        {
            // === Read CSV ===
            var lines = File.ReadAllLines(csvFile)
                .Skip(1) // skip the header row
                .Select(line => line.Split(','))
                .ToArray();

            double[] x = lines.Select(l => double.Parse(l[0], CultureInfo.InvariantCulture)).ToArray();
            double[] measured = lines.Select(l => double.Parse(l[1], CultureInfo.InvariantCulture)).ToArray();
            double[] ideal = lines.Select(l => double.Parse(l[2], CultureInfo.InvariantCulture)).ToArray();

            // === Create plot ===
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

            // === set title and axes ===
            plt.Title("Gamma Curve");
            plt.XLabel("Gray Level");
            plt.YLabel("Luminance");
            plt.Legend.IsVisible = true;

            // Set axis limits to 0–255 for both X and Y
            //plt.Axes.SetLimits(0, 255, 0, 255);

            // === Export image ===
            plt.SavePng(outputFile, 800, 600);
            Console.WriteLine($"Gamma curve saved to {outputFile}");

            // === Show the picture ===
            try
            {
                var psi = new ProcessStartInfo(outputFile)
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Can't open the PNG: {ex.Message}");
            }
        }
    }
}
