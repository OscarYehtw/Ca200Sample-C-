// See https://aka.ms/new-console-template for more information
//Console.WriteLine("Hello, World!");
using CA200SRVRLib;   // 需先在專案引用 COM: CA200Srvr 1.1 Type Library
using System;
using System.IO;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Ca200SampleConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            string csvFile = "measurements.csv";
            int sampleCount = 20;
            bool emulate = false;

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

            // 建立 CSV 檔，加入標題列
            using (var writer = new StreamWriter(csvFile))
            {
                    writer.WriteLine("Index,Lv,x,y,T,duv");

                    // 連續量測 N 筆
                    for (int i = 1; i <= sampleCount; i++)
                    {
                        double Lv, x, y, T, duv;

                        if (!emulate && objCa != null && objProbe != null)
                        {
                            // 真實量測
                            objCa!.Measure();
                            Lv = objProbe.Lv;
                            x = objProbe.sx;
                            y = objProbe.sy;
                            T = objProbe.T;
                            duv = objProbe.duv;
                        }
                        else
                        {
                            // 模擬數據
                            Lv = 150 + rand.NextDouble() * 50;       // 150 ~ 200 cd/m²
                            x = 0.30 + rand.NextDouble() * 0.02;     // 0.30 ~ 0.32
                            y = 0.32 + rand.NextDouble() * 0.02;     // 0.32 ~ 0.34
                            T = 6500 + rand.Next(-200, 200);         // around 6500K
                            duv = rand.NextDouble() * 0.01;          // 0 ~ 0.01
                        }

                        writer.WriteLine($"{i},{Lv:0.00},{x:0.0000},{y:0.0000},{T:0},{duv:0.0000}");
                        Console.WriteLine($"[{i}] Lv={Lv:0.00}, x={x:0.0000}, y={y:0.0000}, T={T:0}, duv={duv:0.0000}");
                    }
            }

            Console.WriteLine($"Measurement completed. Results saved to {csvFile}");

            // 結束前釋放連線
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
