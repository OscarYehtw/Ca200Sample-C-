#define DEBUG_ENABLED
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ca200SampleConsole.Devices
{
    class BacklightController
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

        private void Send(string cmd)
        {
            string fullCmd = $"{cmd}\r\n";
            sp.Write(fullCmd);
            #if DEBUG_ENABLED
            Console.WriteLine($"Send: {cmd}");
            #endif
        }
    }
}
