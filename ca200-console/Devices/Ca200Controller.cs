#define DEBUG_ENABLED
#define USING_CASDK2    // Use original CA200 SDK2

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if USING_CASDK2
using CASDK2;
#else
using CA200SRVRLib;
#endif

namespace Ca200SampleConsole.Devices
{
    class Ca200Controller
    {
#if USING_CASDK2
        private CASDK2Ca200 objCa200;
        private CASDK2Cas objCas;
        private CASDK2Ca objCa;
        private CASDK2Probe objProbe;
        private CASDK2Memory objMemory;
        static int err = 0;

        const int MODE_Lvxy = 0;
        const int MODE_Tduv = 1;
        const int MODE_Lvdudv = 5;
        const int MODE_FMA = 6;
        const int MODE_XYZ = 7;
        const int MODE_JEITA = 8;
        const int MODE_LvPeld = 9;
        const int MODE_Waveform = 10;
        const int MODE_FMA2 = 11;
        const int MODE_JEITA2 = 12;
        const int MODE_Waveform2 = 13;

        const int RED = 0;
        const int GREEN = 1;
        const int BLUE = 2;
        const int WHITE = 3;

        public bool Emulate { get; private set; }
        //static bool autoconnectflag = true; // auro or manual

        public Ca200Controller(bool emulate = false)
        {
            objCa200  = new CASDK2Ca200();
            objCas    = new CASDK2Cas();
            objCa     = new CASDK2Ca();
            objProbe  = new CASDK2Probe();
            objMemory = new CASDK2Memory();

            Emulate = emulate;
        }

        public void Connect()
        {
            int lA = 0, lB = 0, lC = 0, lDEFG = 0;
            GetErrorMessage(GlobalFunctions.CASDK2_GetVersion(ref lA, ref lB, ref lC, ref lDEFG));
            Console.WriteLine("SDKVersion:" + lA + "." + lB + lC + "." + lDEFG);

            objCa200 = new CASDK2Ca200();   // Generate application object

            if (objCa200.AutoConnect() == 0)
            {
                GetErrorMessage(objCa200.get_SingleCa(ref objCa));
                GetErrorMessage(objCa.get_Memory(ref objMemory));
                GetErrorMessage(objCa.get_SingleProbe(ref objProbe));
                DefaultSetting();
                //autoconnectflag = true;
            }
            else
            {
                Emulate = true;
            }
        }

        public (double Lv, double x, double y, double T, double duv) Measure(int gray)
        {
            if (!Emulate && objCa != null && objProbe != null)
            {
                int chnum = 1;      //CalibrationCH : 1

                //measurement result
                double Lv = 0.0;
                double sx = 0.0;
                double sy = 0.0;
                double T = 0.0;
                double duv = 0.0;
                //double X = 0.0;
                //double Y = 0.0;
                //double Z = 0.0;

                SetZeroCalEvent();
                GetErrorMessage(objMemory.put_ChannelNO(chnum));

                GetErrorMessage(objCa.put_DisplayMode(MODE_Lvxy));  //Set mode:Color Lvxy
                GetErrorMessage(objCa.Measure());                   //Color measurement

                //Get Color result
                GetErrorMessage(objProbe.get_Lv(ref Lv));
                GetErrorMessage(objProbe.get_sx(ref sx));
                GetErrorMessage(objProbe.get_sy(ref sy));
                GetErrorMessage(objProbe.get_T(ref T));
                GetErrorMessage(objProbe.get_duv(ref duv));
                //GetErrorMessage(objProbe.get_X(ref X));
                //GetErrorMessage(objProbe.get_Y(ref Y));
                //GetErrorMessage(objProbe.get_Z(ref Z));
                return (Lv, sx, sy, T, duv);
            }
            else
            {
                double Lmax = 250.0;
                double lv = Lmax * Math.Pow(gray / 255.0, 2.4);

                var rand = new Random();
                return (
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
        }

        ///<summary>
        ///[Set measurement conditions]
        ///This method set measurement configuration 
        ///</summary>
        private void DefaultSetting()
        {
            int freqmode = 4;   // SyncMode : INT 
            double freq = 60.0; //frequency = 60.0Hz
            int speed = 1;      //Measurement speed : FAST
            int Lvmode = 1;     //Lv : cd/m2

            GetErrorMessage(objCa.CalZero());                       //Zero-Calibration
            GetErrorMessage(objCa.put_DisplayProbe("P1"));          //Set display probe to P1
            GetErrorMessage(objCa.put_SyncMode(freqmode, freq));    //Set sync mode and frequency
            GetErrorMessage(objCa.put_AveragingMode(speed));        //Set measurement speed
            GetErrorMessage(objCa.put_BrightnessUnit(Lvmode));      //SetBrightness unit

            string PID = "";
            string dispprobe = "";
            int syncmode = 0;
            double syncfreq = 0.0;
            int measspeed = 0;

            //Get settings
            GetErrorMessage(objCa.get_PortID(ref PID));                             //Get connection interface
            Console.WriteLine("PortID:" + PID);
            GetErrorMessage(objCa.get_DisplayProbe(ref dispprobe));                 //Get display probe
            Console.WriteLine("DisplayProbe:" + dispprobe);
            GetErrorMessage(objCa.get_SyncMode(ref syncmode, ref syncfreq));        //Get sync mode and frequency
            Console.WriteLine("SyncMode:" + syncmode + ",Syncfreq:" + syncfreq);
            GetErrorMessage(objCa.get_AveragingMode(ref measspeed));                //Get measurement speed
            Console.WriteLine("MeasurementSpeed:" + measspeed);
        }

        ///<summary>
        ///[Set Zero Calibration event]
        ///This method execute zerocalibration when temperature changes significantly
        ///</summary>
        private int ExeCalZero(int dummy)
        {
            Console.WriteLine("Performing Zero Calibration");
            GetErrorMessage(objCa.CalZero());   //Zero calibration
            return err;
        }

        ///<summary>
        ///[Set Zero Calibration event]
        ///This method set zero calibration event
        ///</summary>
        private void SetZeroCalEvent()
        {
            Func<int, int> funczerocal = ExeCalZero;
            GetErrorMessage(objCa.SetExeCalZeroCallback(funczerocal));      //Set function for zero calibration event
        }

        ///<summary>
        ///[Errorhandling]
        ///This method display Error message from Error number
        ///</summary>
        ///<param name = "errornum">Error number from API of SDK</param>
        private static void GetErrorMessage(int errornum)
        {
            string errormessage = "";
            if (errornum != 0)
            {
                //Get Error message from Error number
                err = GlobalFunctions.CASDK2_GetLocalizedErrorMsgFromErrorCode(0, errornum, ref errormessage);
                Console.WriteLine(errormessage);
            }
        }

#else
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
#endif

    }
}
