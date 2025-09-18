// See https://aka.ms/new-console-template for more information
using CA200SRVRLib;   // Must first add reference to COM: CA200Srvr 1.1 Type Library
using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Ca200SampleConsole
{
    class GrayLevel
    {
        public int Gray { get; set; }
        public string Brightness { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Default COM1, can be overridden by parameter: .\ca200-console.exe -p COM3
            string comPort = "COM1";
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-p")
                {
                    comPort = args[i + 1];
                }
            }

            string csvFile = "measurements.csv";
            string grayFile = "graylevels.csv";
            bool emulate = false;

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

            Ca200? objCa200 = null;
            Ca? objCa = null;
            Probe? objProbe = null;
            Random rand = new Random();

            try
            {
                // 連線 CA-200
                objCa200 = new Ca200();
                objCa200.AutoConnect();
                objCa = objCa200.SingleCa;
                objProbe = objCa.SingleProbe;

                Console.WriteLine("Connected to CA-200. Start measuring...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("CA-200 connection failed: " + ex.Message);
                Console.WriteLine("Switching to EMULATION MODE.");
                emulate = true;
            }

            using (SerialPort sp = new SerialPort(comPort, 115200, Parity.None, 8, StopBits.One))

            // Create CSV file and write header
            using (var writer = new StreamWriter(csvFile))
            {
                // Open UART
                sp.Open();

                writer.WriteLine("Index,Lv,x,y,T,duv");

                // Measure N data points continuously
                for (int i = 0; i < grayData.Count; i++)
                    {
                        int gray = grayData[i].Gray;
                        double Lv, x, y, T, duv;

                        // === 1. Prepare RGB value (Gray = R=G=B) ===
                        int rgb = (gray << 16) | (gray << 8) | gray;  // RGB888
    
                        Console.WriteLine($"Gray[{gray}]");

                        string cmd = $"./b1_fct -p {comPort} shell lcd fill 0x{rgb:X6}\r\n";
                        
                        // === 2. Send UART command ===
                        sp.Write(cmd);
                        Console.WriteLine($"[{i}] Send: {cmd.Trim()}");

                        // === 3. Delay 100ms to wait for LCD update ===
                        Thread.Sleep(100);

                        if (!emulate && objCa != null && objProbe != null)
                        {
                            // Real measurement
                            objCa!.Measure();
                            Lv = objProbe.Lv;
                            x = objProbe.sx;
                            y = objProbe.sy;
                            T = objProbe.T;
                            duv = objProbe.duv;
                        }
                        else
                        {
                            // Simulated data
                            Lv = 150 + rand.NextDouble() * 50;       // 150 ~ 200 cd/m²
                            x = 0.30 + rand.NextDouble() * 0.02;     // 0.30 ~ 0.32
                            y = 0.32 + rand.NextDouble() * 0.02;     // 0.32 ~ 0.34
                            T = 6500 + rand.Next(-200, 200);         // around 6500K
                            duv = rand.NextDouble() * 0.01;          // 0 ~ 0.01
                        }

                        writer.WriteLine($"{i},{Lv:0.00}f,{x:0.0000},{y:0.0000},{T:0},{duv:0.0000}");
                        Console.WriteLine($"[{i}] Lv={Lv:0.00}, x={x:0.0000}, y={y:0.0000}, T={T:0}, duv={duv:0.0000}");

                        // Update Brightness column in graylevels.csv
                        grayData[i].Brightness = Lv.ToString("0.00") + "f";
                }
            }

            // Write back graylevels.csv (keep header)
            using (var writer = new StreamWriter(grayFile))
            {
                writer.WriteLine(header);
                foreach (var g in grayData)
                {
                    writer.WriteLine($"{g.Gray},{g.Brightness}");
                }
            }

            Console.WriteLine($"Measurement completed. Results saved to {csvFile} and updated {grayFile}");

            // Release connection before exit
            if (!emulate && objCa != null)
            {
                objCa.RemoteMode = 0;
            }
            objCa200 = null;
            objCa = null;
            objProbe = null;
        }
    }
}
