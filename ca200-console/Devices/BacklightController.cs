#define DEBUG_ENABLED
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ca200SampleConsole.Devices
{
    class BacklightController : IDisposable
    {
        private SerialPort sp;

        public BacklightController(string portName)
        {
            sp = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One);
        }

        public void Open() => sp.Open();

        public void Start() => Send("fct-bl start");

        public void SetBrightness(int value) => Send($"fct-bl set-brightness {value}");

        public void FillColor(int rgb888) => Send($"fct-lcd fill 0x{rgb888:X6}");

        public void Stop() => Send("fct-bl stop");

        // Send gamma table (16 bytes) to fct-lcd in one command
        public void SetGamma(int[] gammaValues)
        {
            if (gammaValues == null || gammaValues.Length != 16)
                throw new ArgumentException("Gamma array must contain exactly 16 values.");

            //string joined = string.Join(" ", gammaValues);
            // Format each value as hex with 0x prefix
            string joined = string.Join(" ", gammaValues.Select(v => $"0x{v:X2}"));
            string cmd = $"fct-lcd gamma {joined}";
            Send(cmd);
            #if DEBUG_ENABLED
            //Console.WriteLine("Gamma table sent successfully (single command).");
            #endif
        }

        private void Send(string cmd)
        {
            string fullCmd = $"{cmd}\r\n";
            sp.Write(fullCmd);
            #if DEBUG_ENABLED
            Console.WriteLine($"Send: {cmd}");
            #endif
        }
        public void Close()
        {
            if (sp != null && sp.IsOpen)
                sp.Close();
        }

        public void Dispose()
        {
            Close();
            sp?.Dispose();
        }
    }
}
