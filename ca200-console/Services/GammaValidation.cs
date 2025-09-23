#define DEBUG_ENABLED

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class GammaValidation
{
    public static void ValidateGamma(string csvFile, double gamma = 2.2, double tolerance = 0.1)
    {
        // === Read GrayLevel and measured luminance ===
        string[] lines = File.ReadAllLines(csvFile);

        var grayLevels = lines
            .Skip(1)
            .Select(line => int.Parse(line.Split(',')[0], CultureInfo.InvariantCulture))
            .ToArray();

        var lumValues = lines
            .Skip(1)
            .Select(line => double.Parse(line.Split(',')[1].Replace("f", ""), CultureInfo.InvariantCulture))
            .ToArray();

        if (lumValues.Length < 16)
            throw new Exception("CSV must contain at least 16 luminance values.");

        // Mode setting: true = align automatically to Lmax, false = use fixed maximum luminance
        bool autoLmax = true;
        double fixedLmax = 200.0;   // Custom maximum luminance if needed
        double Lmax = autoLmax ? lumValues.Max() : fixedLmax;

        // === Calculate standard gamma 2.2 grayscale curve ===
        double[] ideal = new double[lumValues.Length];
        for (int i = 0; i < lumValues.Length; i++)
        {
            double norm = grayLevels[i] / 255.0;   // Normalize GrayLevel to 0~1
            ideal[i] = Math.Pow(norm, gamma) * Lmax;
        }

        // === Compare to see if it is within the error tolerance ===
        bool pass = true;
        for (int i = 0; i < lumValues.Length; i++)
        {
            double diff = Math.Abs(lumValues[i] - ideal[i]) / (ideal[i] == 0 ? 1 : ideal[i]);
#if DEBUG_ENABLED
            Console.WriteLine($"Gray {grayLevels[i],3}: Measured={lumValues[i]:0.00}, Ideal={ideal[i]:0.00}, Error={diff * 100:0.0}%");
#endif
            if (diff > tolerance) pass = false;
        }

        Console.WriteLine(pass ? "\n=== RESULT: PASS ===" : "\n=== RESULT: FAIL ===");

        // === Output to CSV (for plotting in Excel) ===
        using (var writer = new StreamWriter("gamma_curve.csv"))
        {
            writer.WriteLine($"GrayLevel,Measured,IdealGamma{gamma}");
            for (int i = 0; i < lumValues.Length; i++)
            {
                writer.WriteLine($"{grayLevels[i]},{lumValues[i]},{ideal[i]}");
            }
        }

#if DEBUG_ENABLED
        Console.WriteLine("Comparison curve saved to gamma_compare.csv (you can plot it with Excel).");
#endif
    }
}
