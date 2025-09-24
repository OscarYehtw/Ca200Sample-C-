#define DEBUG_ENABLED

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CA200SRVRLib;

namespace Ca200SampleConsole.Devices
{
    class Ca200Controller
    {
        private Ca200? ca200;
        private Ca? ca;
        private Probe? probe;
        public bool Emulate { get; private set; }

        public Ca200Controller(bool emulate = false)
        {
            Emulate = emulate;
        }

        public void Connect()
        {
            try
            {
                ca200 = new Ca200();
                ca200.AutoConnect();
                ca = ca200.SingleCa;
                probe = ca.SingleProbe;
#if DEBUG_ENABLED
                Console.WriteLine("Connected to CA-200.");
#endif
            }
            catch (Exception ex)
            {
                Console.WriteLine("CA-200 connection failed: " + ex.Message);
#if DEBUG_ENABLED
                Console.WriteLine("Switching to EMULATION MODE.");
#endif
                Emulate = true;
            }
        }

        //public (double Lv, double x, double y, double T, double duv) Measure()
        public (double Lv, double x, double y, double T, double duv) Measure(int gray)
        {
            if (!Emulate && ca != null && probe != null)
            {
                ca.Measure();
                return (probe.Lv, probe.sx, probe.sy, probe.T, probe.duv);
            }
            else
            {
                double Lmax = 250.0;
                double lv = Lmax * Math.Pow(gray / 255.0, 2.4);

                var rand = new Random();
                return (
                    //150 + rand.NextDouble() * 50,
                    lv,
                    0.30 + rand.NextDouble() * 0.02,
                    0.32 + rand.NextDouble() * 0.02,
                    6500 + rand.Next(-200, 200),
                    rand.NextDouble() * 0.01
                );
            }
        }

        public void Release()
        {
            if (!Emulate && ca != null)
                ca.RemoteMode = 0;

            ca200 = null;
            ca = null;
            probe = null;
        }
    }
}
