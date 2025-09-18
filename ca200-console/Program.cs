// See https://aka.ms/new-console-template for more information
//#define DEBUG_ENABLED

using CA200SRVRLib;   // Must first add reference to COM: CA200Srvr 1.1 Type Library
using ST7701S_NB_Gamma;  // You must first add ST7701S_NB_Gamma.dll to References
using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Ca200SampleConsole
{
    class GrayLevel
    {
        public int Gray { get; set; }
        public string Brightness { get; set; } = "";
    }

    class Program
    {
        static void Main(string[] args)
        {
            string csvFile = "measurements.csv";
            string grayFile = "graylevels.csv";
            string vcomFile = "vcom.csv";
            string gammaFile = "gamma.csv";
            bool emulate = false;

            // Default COM1, can be overridden by parameter: .\ca200-console.exe -p COM3
            string comPort = "COM1";
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-p")
                {
                    comPort = args[i + 1];
                }
            }

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
                // connect to CA-200
                objCa200 = new Ca200();
                objCa200.AutoConnect();
                objCa = objCa200.SingleCa;
                objProbe = objCa.SingleProbe;
                #if DEBUG_ENABLED
                Console.WriteLine("Connected to CA-200. Start measuring...");
                #endif
            }
            catch (Exception ex)
            {
                //#if DEBUG_ENABLED
                Console.WriteLine("CA-200 connection failed: " + ex.Message);
                Console.WriteLine("Switching to EMULATION MODE.");
                //#endif
                emulate = true;
            }

            using (SerialPort sp = new SerialPort(comPort, 115200, Parity.None, 8, StopBits.One))

            // Create CSV file and write header
            using (var writer = new StreamWriter(csvFile))
            {
                // Open UART
                sp.Open();

                // Backlight ON
                string cmd = $"fct-bl start\r\n";
                sp.Write(cmd);

                #if DEBUG_ENABLED
                Console.WriteLine($"Send: {cmd.Trim()}");
                #endif

                // Backlight set brightness
                cmd = $"fct-bl set-brightness 255\r\n";
                sp.Write(cmd);

                #if DEBUG_ENABLED
                Console.WriteLine($"Send: {cmd.Trim()}");
                #endif

                writer.WriteLine("Index,Lv,x,y,T,duv");

                // Measure N data points continuously
                for (int i = 0; i < grayData.Count; i++)
                    {
                        int gray = grayData[i].Gray;
                        double Lv, x, y, T, duv;

                        // === Prepare RGB value (Gray = R=G=B) ===
                        int rgb = (gray << 16) | (gray << 8) | gray;  // RGB888

                        #if DEBUG_ENABLED
                        Console.WriteLine($"Gray[{gray}]");
                        #endif
                    
                        // fct-lcd fill <rgb888> - Fill screen with rgb color, Ex: fct-lcd fill 0xFFFFFF (white), ACK: lcd
                        cmd = $"fct-lcd fill 0x{rgb:X6}\r\n";
                        
                        // === Send UART command ===
                        sp.Write(cmd);
                        
                        #if DEBUG_ENABLED
                        Console.WriteLine($"[{i}] Send: {cmd.Trim()}");
                        #endif

                        // === Delay 100ms to wait for LCD update ===
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
                        
                        #if DEBUG_ENABLED
                        Console.WriteLine($"[{i}] Lv={Lv:0.00}, x={x:0.0000}, y={y:0.0000}, T={T:0}, duv={duv:0.0000}");
                        #endif

                        // Update Brightness column in graylevels.csv
                        grayData[i].Brightness = Lv.ToString("0.00") + "f";
                }

                // Backlight OFF
                cmd = $"fct-bl stop\r\n";
                sp.Write(cmd);
                #if DEBUG_ENABLED
                Console.WriteLine($"Send: {cmd.Trim()}");
                #endif
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

            #if DEBUG_ENABLED
            Console.WriteLine($"Measurement completed. Results saved to {csvFile} and updated {grayFile}");
            #endif

            ST7701S_NB_Gamma.Class1 F = new Class1();

            #if DEBUG_ENABLED
            Console.WriteLine("Gamma calculation started...");
            #endif

            // === Read VCOM parameters ===
            string[] vcomLines = File.ReadAllLines(vcomFile);
            var vcomValues = vcomLines.Skip(1).First().Split(',');
            int vcm = Convert.ToInt32(vcomValues[0], 16);
            int vrh = Convert.ToInt32(vcomValues[1], 16);

            F.B0_VOP_B1_VCOM(vcm, vrh);
            #if DEBUG_ENABLED
            Console.WriteLine($"VCOM parameters loaded: VCM={vcm}, VRH={vrh}");
            #endif

            // === 2. Read gamma.csv ===
            string[] gammaLines = File.ReadAllLines(gammaFile);
            var gammaValues = gammaLines
                .Skip(1)
                .Select(line => line.Split(',')[1]) // Take the 2nd column (Value)
                .Select(hex => Convert.ToInt32(hex, 16)) // Convert HEX to int
                .ToArray();

            if (gammaValues.Length < 16)
                throw new Exception("gamma.csv must contain at least 16 integer parameters");

            F.Input_Gamma_Parameter(
                gammaValues[0], gammaValues[1], gammaValues[2], gammaValues[3],
                gammaValues[4], gammaValues[5], gammaValues[6], gammaValues[7],
                gammaValues[8], gammaValues[9], gammaValues[10], gammaValues[11],
                gammaValues[12], gammaValues[13], gammaValues[14], gammaValues[15]
            );

            #if DEBUG_ENABLED
            Console.WriteLine("Gamma parameters loaded.");
            #endif

            F.Calculate_Gamma_Voltage_();

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

            #if DEBUG_ENABLED
            Console.WriteLine("Luminance values read from measurements.csv:");
            for (int i = 0; i < lumValues.Length; i++)
            {
                Console.WriteLine($"Index {i}: {lumValues[i]:0.00}");
            }
            #endif

            F.Input_Measure_luminance(
                lumValues[0], lumValues[1], lumValues[2], lumValues[3],
                lumValues[4], lumValues[5], lumValues[6], lumValues[7],
                lumValues[8], lumValues[9], lumValues[10], lumValues[11],
                lumValues[12], lumValues[13], lumValues[14], lumValues[15]
            );

            #if DEBUG_ENABLED
            Console.WriteLine("Luminance measurements loaded.");
            #endif

            F.Output_Gamma_Parameter();

            // === Call ST7701S_NB_Gamma for calculation ===
            int[] temp = ST7701S_NB_Gamma.Class1.Read_Data;   // Read gamma results from DLL
            #if DEBUG_ENABLED
            Console.WriteLine("Gamma calculation finished.");
            #endif

            // === Output gamma_out.csv ===
            using (var writer = new StreamWriter("gamma_out.csv"))
            {
                writer.WriteLine("Index,Value");
                for (int i = 0; i < temp.Length; i++)
                {
                    writer.WriteLine($"{i},{temp[i]:X}");
                }
            }

            #if DEBUG_ENABLED
            Console.WriteLine("Results saved to gamma_cal.csv");
            #endif

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
