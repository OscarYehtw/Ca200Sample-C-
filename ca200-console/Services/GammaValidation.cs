#define DEBUG_ENABLED

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class GammaValidation
{
    public static void ValidateGamma(string csvFile, string curveFile, double gamma = 2.2, double tolerance = 0.1)
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
        
        if (lumValues.Max() == 0)
            autoLmax = false;

        double fixedLmax = 250.0;   // Custom maximum luminance if needed
        double Lmax = autoLmax ? lumValues.Max() : fixedLmax;

        // === Calculate standard gamma 2.2 grayscale curve ===
        double[] ideal = new double[lumValues.Length];
        for (int i = 0; i < lumValues.Length; i++)
        {
            double norm = grayLevels[i] / 255.0;   // Normalize GrayLevel to 0~1
            //ideal[i] = Math.Pow(norm, gamma) * Lmax;
            double value = Math.Pow(norm, gamma) * Lmax;
            ideal[i] = Math.Truncate(value * 1000) / 1000.0;
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
        using (var writer = new StreamWriter(curveFile))
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

    public static void ValidateGammaRGBW(string csvFile, string curveFile, double gamma = 2.2, double tolerance = 0.3, int minGrayForCheck = 1)
    {
        // === Read CSV ===
        // Assume CSV format: Channel,GrayLevel,Luminance
        // Channel = R/G/B/W
        var lines = File.ReadAllLines(csvFile)
            .Skip(1)
            .Select(l => l.Split(','))
            .Where(parts => parts.Length >= 4 && double.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            .Select(parts => new
            {
                Channel = parts[1].Trim(), // Channel
                GrayLevel = double.Parse(parts[2].Trim(), CultureInfo.InvariantCulture), // Gray
                Luminance = double.Parse(parts[3].Trim(), CultureInfo.InvariantCulture)  // Lv
            })
            .ToList();

        if (lines.Count == 0)
        {
            Console.WriteLine("⚠ No valid data parsed from CSV. Please check file encoding/format.");
            return;
        }

        // Group: each channel separately
        var groups = lines.GroupBy(x => x.Channel);

        bool overallPass = true;
        double toleranceRelative = 0.3;   // Relative error tolerance for high gray levels
        double toleranceAbsolute = 0.3;   // Absolute error tolerance for low gray levels

        using (var writer = new StreamWriter(curveFile))
        {
            writer.WriteLine("Channel,GrayLevel,Measured,Ideal");

            foreach (var group in groups)
            {
                string channel = group.Key;
                var grayLevels = group.Select(g => g.GrayLevel).ToArray();
                var lumValues = group.Select(g => g.Luminance).ToArray();

                double Y_min = lumValues.Min();
                double Y_max = lumValues.Max();
                bool channelPass = true;

                for (int i = 0; i < grayLevels.Length; i++)
                {
                    double gray = grayLevels[i];
                    double Lv = lumValues[i];

                    double gammaCalc = double.NaN;
                    double error = double.NaN;
                    string result = "SKIP";

                    if (gray == 0)
                    {
                        // Skip black level point
                        result = "SKIP";
                    }
                    else if (gray == 255)
                    {
                        // At max gray, only check if equal to Y_max
                        double idealLv = Y_max;
                        error = Math.Abs(Lv - idealLv);
                        result = error <= toleranceAbsolute ? "PASS" : "FAIL";
                    }
                    else if(gray >= minGrayForCheck)
                    {
                        double normalized = (Lv - Y_min) / (Y_max - Y_min);
                        gammaCalc = Math.Log(normalized) / Math.Log(gray / 255.0);
                        error = Math.Abs(gammaCalc - gamma) / gamma;
                        result = error <= toleranceRelative ? "PASS" : "FAIL";
                    }

                    if (result == "FAIL") channelPass = false;

                    writer.WriteLine($"{channel},{gray},{Lv},{Y_max},{(double.IsNaN(gammaCalc) ? "" : gammaCalc.ToString("0.###"))},{error:0.###},{result}");
                    //writer.WriteLine($"{channel},{grayLevels[i]},{lumValues[i]},{ideal[i]}");
#if DEBUG_ENABLED
                    //Console.WriteLine($"CH {channel} - Gray {grayLevels[i],3}: Measured={lumValues[i]:0.00}, Ideal={ideal[i]:0.00}, Error={diff * 100:0.0}%");
#endif
                }

                Console.WriteLine(channelPass
                    ? $"Channel {channel}: PASS"
                    : $"Channel {channel}: FAIL");

                if (!channelPass) overallPass = false;

                //Console.WriteLine(pass
                //    ? $"Channel {channel}: PASS (Gamma {gamma} ± {tolerance})"
                //    : $"Channel {channel}: FAIL (Gamma {gamma} ± {tolerance})");

                //if (!pass) overallPass = false;
            }
        }

        Console.WriteLine(overallPass ? "\n=== FINAL RESULT: PASS ===" : "\n=== FINAL RESULT: FAIL ===");
        Console.WriteLine($"Comparison curves saved to {curveFile}");
    }
}
