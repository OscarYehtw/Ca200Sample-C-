// See https://aka.ms/new-console-template for more information
//#define DEBUG_ENABLED

using Ca200SampleConsole.Models;
using Ca200SampleConsole.Devices;
using Ca200SampleConsole.Services;
using CA200SRVRLib;   // Must first add reference to COM: CA200Srvr 1.1 Type Library
using ST7701S_NB_Gamma;  // You must first add ST7701S_NB_Gamma.dll to References
using System;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using ScottPlot;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Ca200SampleConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            string ca310File = "ca-310.csv";
            string grayFile = "graylevels.csv";
            string csvFile = "measurements.csv";
            string vcomFile = "vcom.csv";
            string gammaFile = "gamma.csv";
            string gammaoutFile = "gamma_out.csv";
            string curvecsvFile = "gamma_curve.csv";
            string curvepngFile = "gamma_curve.png";
            bool emulate = false;

            // Default COM1, can be overridden by parameter: .\ca200-console.exe -p COM3
            string comPort = args.Contains("-p") ? args[Array.IndexOf(args, "-p") + 1] : "COM1";

            // === Read graylevels.csv (Gray, Brightness) ===
            var grayLines = File.ReadAllLines(grayFile);
            var header = grayLines[0].Trim();
            var grayData = grayLines.Skip(1)
                        .Select(line => line.Split(','))
                        .Select(parts => new GrayLevel
                        {
                            Gray = int.Parse(parts[0]),
                            Brightness = parts.Length > 1 ? parts[1] : ""
                        })
                        .ToList();

            var ca200 = new Ca200Controller(emulate);
            ca200.Connect();

            var backlight = new BacklightController(comPort);
            var service = new MeasurementService(ca200, backlight);

            service.RunMeasurements(grayData, csvFile);

            // Write back graylevels.csv (keep header)
            using (var writer = new StreamWriter(grayFile))
            {
                writer.WriteLine(header);
                foreach (var g in grayData)
                {
                    writer.WriteLine($"{g.Gray},{g.Brightness}");
                }
            }

#if DEBUG_ENABLED
            Console.WriteLine($"Measurement completed. Results saved to {csvFile} and updated {grayFile}");
#endif

#if DEBUG_ENABLED
            Console.WriteLine("Gamma calculation started...");
#endif
            var engine = new GammaEngine();

            // === Read VCOM parameters ===
            string[] vcomLines = File.ReadAllLines(vcomFile);
            var vcomValues = vcomLines.Skip(1).First().Split(',');
            int vcm = Convert.ToInt32(vcomValues[0], 16);
            int vrh = Convert.ToInt32(vcomValues[1], 16);
            engine.LoadVcom(vcm, vrh);

#if DEBUG_ENABLED
            Console.WriteLine($"VCOM parameters loaded: VCM={vcm}, VRH={vrh}");
#endif
            // === Read gamma.csv ===
            string[] gammaLines = File.ReadAllLines(gammaFile);
            var gammaValues = gammaLines
                .Skip(1)
                .Select(line => line.Split(',')[1]) // Take the 2nd column (Value)
                .Select(hex => Convert.ToInt32(hex, 16)) // Convert HEX to int
                .ToArray();

            if (gammaValues.Length < 16)
                throw new Exception("gamma.csv must contain at least 16 integer parameters");
            
            engine.LoadGammaParams(gammaValues);

#if DEBUG_ENABLED
            Console.WriteLine("Gamma parameters loaded.");
#endif
            engine.CalGammaVoltage();

            // === Read measurements.csv ===
            string[] luminanceLines = File.ReadAllLines(csvFile);
            var lumValues = luminanceLines
                .Skip(1)
                .Select(line => line.Split(',')[1])   // Take Lux values
                .Select(val => val.Replace("f", ""))  // Remove f unit
                .Select(double.Parse)
                .ToArray();

            if (lumValues.Length < 16)
                throw new Exception("measurements.csv must contain at least 16 luminance values");

            engine.LoadLuminance(lumValues);

#if DEBUG_ENABLED
            Console.WriteLine("Luminance values read from measurements.csv:");
            for (int i = 0; i < lumValues.Length; i++)
            {
                Console.WriteLine($"Index {i}: {lumValues[i]:0.00}");
            }

            Console.WriteLine("Luminance measurements loaded.");
#endif
            // === Run Gamma calculation ===
            int[] results = engine.Calculate();

#if DEBUG_ENABLED
            Console.WriteLine("Gamma calculation finished.");
#endif
            // === Output gamma_out.csv ===
            using (var writer = new StreamWriter(gammaoutFile))
            {
                writer.WriteLine("Index,Value");
                for (int i = 0; i < results.Length; i++)
                {
                    writer.WriteLine($"{i},{results[i]:X}");
                }
            }

#if DEBUG_ENABLED
            Console.WriteLine("Results saved to gamma_cal.csv");
#endif

            GammaValidation.ValidateGamma(ca310File, gamma: 2.2, tolerance: 0.1);
            //GammaValidation.ValidateGamma(grayFile, gamma: 2.2, tolerance: 0.1);

            GammaPlotter.PlotFromCsv(curvecsvFile, curvepngFile);

            ca200.Release();
        }
    }
}
