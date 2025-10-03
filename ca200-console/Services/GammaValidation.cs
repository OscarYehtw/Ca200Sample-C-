#define DEBUG_ENABLED

using MathNet.Numerics.Optimization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static SkiaSharp.HarfBuzz.SKShaper;
using MathLA = MathNet.Numerics.LinearAlgebra;

class GammaValidation
{
    // Simplified struct used to store measurement data
    public struct Measurement
    {
        public string Channel;
        public double GrayLevel;
        public double Luminance;
    }

    // Struct used to store calculated results for each channel
    public struct GammaResult
    {
        public string Channel;
        public double ActualGamma;
        public double RmsError;
        public string Result;
        public double Y_black;
        public double Y_white;
    }

    /* ************************************************************
     * MathNet Numerics fitting implementation
     * ************************************************************ */

    /// <summary>
    /// Weight calculation function: reduces the influence of very low gray levels on overall fitting.
    /// The goal is to give medium-to-high gray levels (close to the Gamma 2.2 region) greater influence.
    /// </summary>
    private static double GetWeight(double grayLevel)
    {
        if (grayLevel <= 15)
        {
            return 0.001; // Extremely dark region (Gray 15 and below) barely participates in fitting
        }
        else if (grayLevel <= 47)
        {
            return 0.1;  // Low gray levels (Gray 47 and below) have very low weight
        }
        else if (grayLevel <= 127)
        {
            return 0.5; // Medium gray levels
        }
        else
        {
            return 1.0;  // Medium-to-high gray levels (Gray 127 and above) have the highest weight
        }
    }

    /// <summary>
    /// Defines the cost function: calculates the weighted sum of squared residuals (WSSR) for a given Gamma value.
    /// </summary>
    /// <param name="gamma">Gamma value to evaluate</param>
    /// <param name="fitData">Data points containing (GrayLevel, NormalizedLuminance)</param>
    /// <returns>Weighted sum of squared residuals</returns>
    private static double CostFunction(double gamma, List<(double Gray, double NormL)> fitData)
    {
        double weightedSumSquaredResiduals = 0;

        foreach (var p in fitData)
        {
            double gray = p.Gray;
            double L_actual = p.NormL;
            double V = gray / 255.0;

            // 1. Fit luminance L_fitted = V^gamma
            double L_fitted = Math.Pow(V, gamma);

            // 2. Calculate residual
            double residual = L_fitted - L_actual;

            // 3. Apply weight: weighted sum of squared residuals
            double weight = GetWeight(gray);
            weightedSumSquaredResiduals += Math.Pow(residual * weight, 2);
        }

        return weightedSumSquaredResiduals;
    }

    /// <summary>
    /// [Alternative] Perform high-precision grid search to calculate the best Gamma value.
    /// This method does not rely on the MathNet.Numerics.Optimization package.
    /// </summary>
    private static double FitWeightedGamma(List<double> grayLevels, List<double> normalizedLuminances)
    {
        // Filter out Gray=0 points because V=0 cannot participate in the power-law model
        var fitData = grayLevels.Zip(normalizedLuminances, (g, l) => (Gray: g, NormL: l))
                                .Where(p => p.Gray > 0)
                                .ToList();

        if (fitData.Count < 2) return double.NaN;

        // --- Grid search parameters ---
        const double GammaMin = 1.0;     // Lower bound of search range
        const double GammaMax = 3.5;     // Upper bound of search range
        const int Steps = 2500;          // Number of search steps (2500 steps provide 0.001 precision)
        double stepSize = (GammaMax - GammaMin) / Steps;

        double bestGamma = double.NaN;
        double minCost = double.MaxValue;

        for (int i = 0; i <= Steps; i++)
        {
            double currentGamma = GammaMin + i * stepSize;

            // Calculate weighted cost (WSSR) for the current Gamma
            double currentCost = CostFunction(currentGamma, fitData);

            if (currentCost < minCost)
            {
                minCost = currentCost;
                bestGamma = currentGamma;
            }
        }

        // Return the Gamma corresponding to the minimum cost found
        return bestGamma;
    }

    /// <summary>
    /// Linear regression in log-log space: Log10(L') = Gamma * Log10(V) + C.
    /// The slope is the Actual Gamma.
    /// </summary>
    /// <param name="X">Log10(V)</param>
    /// <param name="Y">Log10(L')</param>
    /// <returns>Calculated Actual Gamma value</returns>
    private static double PerformLinearRegression(List<double> X, List<double> Y)
    {
        int N = X.Count;
        double sumX = X.Sum();
        double sumY = Y.Sum();
        double sumXX = X.Select(x => x * x).Sum();
        double sumXY = X.Select((x, i) => x * Y[i]).Sum();

        // Gamma = Slope (m) = (N * Sum(XY) - Sum(X) * Sum(Y)) / (N * Sum(X^2) - Sum(X)^2)
        double numerator = (N * sumXY) - (sumX * sumY);
        double denominator = (N * sumXX) - (sumX * sumX);

        return denominator == 0 ? double.NaN : numerator / denominator;
    }

    /// <summary>
    /// Calculate RMS error between the actual normalized luminance L' and fitted luminance V^Gamma.
    /// </summary>
    /// <param name="normalizedV">Normalized gray level V (0~1)</param>
    /// <param name="normalizedLvActual">Normalized actual luminance L'</param>
    /// <param name="actualGamma">Calculated Actual Gamma</param>
    /// <returns>RMS Error</returns>
    private static double CalculateRmsError(List<double> normalizedV, List<double> normalizedLvActual, double actualGamma)
    {
        double sumSquaredError = 0;
        int N = 0;

        for (int i = 0; i < normalizedV.Count; i++)
        {
            // Exclude Gray=0 points
            if (normalizedV[i] == 0) continue;

            // L'_fitted = V^Gamma (using Actual Gamma)
            double normLvFitted = Math.Pow(normalizedV[i], actualGamma);

            // Sum of squared errors
            sumSquaredError += Math.Pow(normalizedLvActual[i] - normLvFitted, 2);
            N++;
        }

        // Root Mean Square Error
        return N > 0 ? Math.Sqrt(sumSquaredError / N) : double.NaN;
    }

    // === New: Read targetxy.csv and check whether the white point is within SPEC range ===
    public static bool ValidateWhitePoint(string sku, string measuredFile, string targetFile, string resultFile)
    {
#if DEBUG_ENABLED
        Console.WriteLine("\n=== Checking White Point Spec ===");
#endif
        // 1. Read targetxy.csv
        var lines = File.ReadAllLines(targetFile);
        var header = lines[0].Split(',');

        // Find column indexes
        int skuIdx = Array.IndexOf(header, "SKU");
        int xMinIdx = Array.IndexOf(header, "x_min");
        int xMaxIdx = Array.IndexOf(header, "x_max");
        int yMinIdx = Array.IndexOf(header, "y_min");
        int yMaxIdx = Array.IndexOf(header, "y_max");

        if (skuIdx < 0 || xMinIdx < 0 || xMaxIdx < 0 || yMinIdx < 0 || yMaxIdx < 0)
        {
#if DEBUG_ENABLED
            Console.WriteLine($"Invalid header format in {targetFile}.");
#endif
            return false;
        }

        // 2. Find the spec for the corresponding SKU
        var specLine = lines.Skip(1)
                            .Select(l => l.Split(','))
                            .FirstOrDefault(p => p[skuIdx].Trim().Equals(sku, StringComparison.OrdinalIgnoreCase));

        if (specLine == null)
        {
#if DEBUG_ENABLED
            Console.WriteLine($"SKU {sku} not found in {targetFile}.");
 #endif
            return false;
        }

        double xMin = double.Parse(specLine[xMinIdx], CultureInfo.InvariantCulture);
        double xMax = double.Parse(specLine[xMaxIdx], CultureInfo.InvariantCulture);
        double yMin = double.Parse(specLine[yMinIdx], CultureInfo.InvariantCulture);
        double yMax = double.Parse(specLine[yMaxIdx], CultureInfo.InvariantCulture);

        // 3. Read the White channel from measured_rgbw.csv
        var whitePoints = File.ReadAllLines(measuredFile)
            .Skip(1)
            .Select(l => l.Split(','))
            .Where(p => p.Length >= 6 && p[1].Trim().Equals("W", StringComparison.OrdinalIgnoreCase))
            .Select(p => new
            {
                Gray = int.Parse(p[2].Trim(), CultureInfo.InvariantCulture),
                Lv = double.Parse(p[3].Trim(), CultureInfo.InvariantCulture),
                x = double.Parse(p[4].Trim(), CultureInfo.InvariantCulture),
                y = double.Parse(p[5].Trim(), CultureInfo.InvariantCulture)
            })
            .ToList();

        if (whitePoints.Count == 0)
        {
#if DEBUG_ENABLED
            Console.WriteLine("No White channel data found in measured file.");
#endif
            return false;
        }

        // Find the white point with maximum luminance
        var maxWhite = whitePoints.OrderByDescending(w => w.Lv).First();

#if DEBUG_ENABLED
        Console.WriteLine($"Max White (Gray={maxWhite.Gray}, Lv={maxWhite.Lv:F2}): x={maxWhite.x:F4}, y={maxWhite.y:F4}");
        Console.WriteLine($"Spec Range ({sku}): x=[{xMin:F4}, {xMax:F4}], y=[{yMin:F4}, {yMax:F4}]");
#endif
        bool pass = (maxWhite.x >= xMin && maxWhite.x <= xMax &&
                     maxWhite.y >= yMin && maxWhite.y <= yMax);

#if DEBUG_ENABLED
        Console.WriteLine(pass ? "=== WHITE POINT RESULT: PASS ===" : "=== WHITE POINT RESULT: FAIL ===");
#endif

        // 4. Write result into targetxy_result.csv
        using (var writer = new StreamWriter(resultFile, false)) // overwrite
        {
            writer.WriteLine("SKU,Gray,Lv,x,y,x_min,x_max,y_min,y_max,Result");
            writer.WriteLine($"{sku},{maxWhite.Gray},{maxWhite.Lv:F4},{maxWhite.x:F4},{maxWhite.y:F4}," +
                             $"{xMin:F4},{xMax:F4},{yMin:F4},{yMax:F4},{(pass ? "PASS" : "FAIL")}");
        }

        return pass;
    }

    /// <summary>
    /// Validate and calculate the Actual Gamma and RMS error for each channel.
    /// </summary>
    /// <param name="measuredFile">Input file: measured_rgbw.csv</param>
    /// <param name="resultFile">Output file: gamma_curve.csv</param>
    /// <param name="targetGamma">Target Gamma (default 2.2)</param>
    /// <param name="tolerance">Absolute Gamma tolerance (e.g., 0.3)</param>
    /// <param name="minGrayForCheck">Minimum gray level (not used, but kept)</param>
    public static void ValidateGammaRGBW(string measuredFile, string resultFile, double targetGamma = 2.2, double tolerance = 0.3)
    {
        // === Read CSV ===
        // Assumed CSV format: Index,Channel,Gray,Lv,...
        List<Measurement> lines;
        try
        {
            // Note: File.ReadAllLines and StreamWriter require System.IO
            lines = File.ReadAllLines(measuredFile)
                .Skip(1) // Skip header line
                .Select(l => l.Split(','))
                .Where(parts => parts.Length >= 4 && double.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                .Select(parts => new Measurement
                {
                    Channel = parts[1].Trim(),       // Channel (parts[1])
                    GrayLevel = double.Parse(parts[2].Trim(), CultureInfo.InvariantCulture), // Gray (parts[2])
                    Luminance = double.Parse(parts[3].Trim(), CultureInfo.InvariantCulture)  // Lv (parts[3])
                })
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading or parsing CSV file: {ex.Message}");
            return;
        }

        if (lines.Count == 0)
        {
#if DEBUG_ENABLED
            Console.WriteLine("No valid data parsed from CSV. Please check file encoding/format.");
#endif
            return;
        }

        // Group: each channel separately
        var groups = lines.GroupBy(x => x.Channel);
        var results = new List<GammaResult>();
#if DEBUG_ENABLED
        bool overallPass = true;
#endif
        // Write detailed curve fitting data
        using (var writer = new StreamWriter(resultFile))
        {
            // Output CSV header: Channel,GrayLevel,Measured,Fitted
            //writer.WriteLine("Channel,GrayLevel,Measured,Fitted");

#if DEBUG_ENABLED
            Console.WriteLine("\n------------------------------------------------------------");
            Console.WriteLine($"| Target Gamma: {targetGamma:F2} (Tolerance: ±{tolerance:F2})                    |");
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("| Channel | Actual Gamma | RMS Error (Normalized) | Result |");
            Console.WriteLine("------------------------------------------------------------");
#endif
            foreach (var group in groups)
            {
                string channel = group.Key;
                var channelData = group.OrderBy(g => g.GrayLevel).ToList();

                // 1. Get black and white luminance (for normalization)
                double Y_black = channelData.FirstOrDefault(m => m.GrayLevel == 0).Luminance;
                double Y_white = channelData.FirstOrDefault(m => m.GrayLevel == 255).Luminance;
                double deltaY = Y_white - Y_black;

                if (deltaY <= 0)
                {
#if DEBUG_ENABLED
                    Console.WriteLine($"| {channel,-7} | N/A          | N/A                    | FAIL   |");
                    overallPass = false;
#endif
                    continue;
                }

                // 2. Prepare all data points for weighted fitting and RMS calculation
                List<double> allGrayLevels = channelData.Select(m => m.GrayLevel).ToList();
                List<double> allNormV = channelData.Select(m => m.GrayLevel / 255.0).ToList();
                List<double> allNormL = channelData.Select(m => (m.Luminance - Y_black) / deltaY).ToList();

                // 3. [Core change] Use weighted nonlinear fitting to calculate Actual Gamma
                double actualGamma = FitWeightedGamma(allGrayLevels, allNormL);

                if (double.IsNaN(actualGamma))
                {
#if DEBUG_ENABLED
                    Console.WriteLine($"| {channel,-7} | N/A          | N/A                    | FAIL   |");
                    overallPass = false;
#endif
                    continue;
                }

                // 4. Calculate RMS error (using all points, ignoring weight)
                double rmsError = CalculateRmsError(allNormV, allNormL, actualGamma);

                // 5. Check result
                double gammaDeviation = Math.Abs(actualGamma - targetGamma);
                bool channelPass = gammaDeviation <= tolerance;
#if DEBUG_ENABLED
                if (!channelPass) overallPass = false;
#endif

#if false
                // 6. Write curve data (Measured and Fitted)
                foreach (var m in channelData)
                {
                    double normV = m.GrayLevel / 255.0;

                    // L_fitted = Y_black + (Y_white - Y_black) * V^gamma
                    double Lv_fitted = Y_black + deltaY * Math.Pow(normV, actualGamma);

                    writer.WriteLine($"{channel},{m.GrayLevel},{m.Luminance:F4},{Lv_fitted:F4}");
                }
#endif
                // 7. Output console results
                string resultStr = channelPass ? "PASS" : "FAIL";
                results.Add(new GammaResult
                {
                    Channel = channel,
                    ActualGamma = actualGamma,
                    RmsError = rmsError,
                    Result = resultStr,
                    Y_black = Y_black,
                    Y_white = Y_white
                });
#if DEBUG_ENABLED
                Console.WriteLine($"| {channel,-7} | {actualGamma:F3}        | {rmsError:F6}               | {resultStr}   |");
#endif
            }

            // === Write Summary results (as per your requirement 1) ===
            writer.WriteLine("--- Summary ---");
            writer.WriteLine($"TargetGamma,{targetGamma}");
            writer.WriteLine($"Tolerance,{tolerance}");
            writer.WriteLine("Channel,ActualGamma,RmsError,Result,Y_black,Y_white");
            foreach (var res in results)
            {
                writer.WriteLine($"{res.Channel},{res.ActualGamma:F3},{res.RmsError:F6},{res.Result},{res.Y_black:F4},{res.Y_white:F4}");
            }
            writer.WriteLine("--- End Summary ---");

#if DEBUG_ENABLED
            Console.WriteLine("------------------------------------------------------------");
#endif
        }

#if DEBUG_ENABLED
        Console.WriteLine(overallPass ? "\n=== FINAL RESULT: PASS ===" : "\n=== FINAL RESULT: FAIL ===");
        Console.WriteLine($"Comparison curves (Measured vs Fitted) saved to {resultFile} (FileTag: gamma_curve.csv)");
#endif
    }

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
#if DEBUG_ENABLED
        bool pass = true;
        for (int i = 0; i < lumValues.Length; i++)
        {
            double diff = Math.Abs(lumValues[i] - ideal[i]) / (ideal[i] == 0 ? 1 : ideal[i]);
            Console.WriteLine($"Gray {grayLevels[i],3}: Measured={lumValues[i]:0.00}, Ideal={ideal[i]:0.00}, Error={diff * 100:0.0}%");
            if (diff > tolerance) pass = false;
        }

        Console.WriteLine(pass ? "\n=== RESULT: PASS ===" : "\n=== RESULT: FAIL ===");
#endif
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

}
