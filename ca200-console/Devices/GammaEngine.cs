using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ST7701S_NB_Gamma;

namespace Ca200SampleConsole.Devices
{
    class GammaEngine
    {
        private Class1 engine = new Class1();

        public void LoadVcom(int vcm, int vrh) => engine.B0_VOP_B1_VCOM(vcm, vrh);

        public void LoadGammaParams(int[] gammaValues)
        {
            if (gammaValues.Length < 16) throw new ArgumentException("Gamma values must be 16.");
            engine.Input_Gamma_Parameter(
                gammaValues[0], gammaValues[1], gammaValues[2], gammaValues[3],
                gammaValues[4], gammaValues[5], gammaValues[6], gammaValues[7],
                gammaValues[8], gammaValues[9], gammaValues[10], gammaValues[11],
                gammaValues[12], gammaValues[13], gammaValues[14], gammaValues[15]
            );
        }

        // Must be called AFTER LoadGammaParams(), BEFORE LoadLuminance()
        public void CalGammaVoltage()
        {
            engine.Calculate_Gamma_Voltage_();
        }

        public void LoadLuminance(double[] lumValues)
        {
            if (lumValues.Length < 16) throw new ArgumentException("Must be 16 luminance values.");
            engine.Input_Measure_luminance(
                lumValues[0], lumValues[1], lumValues[2], lumValues[3],
                lumValues[4], lumValues[5], lumValues[6], lumValues[7],
                lumValues[8], lumValues[9], lumValues[10], lumValues[11],
                lumValues[12], lumValues[13], lumValues[14], lumValues[15]
            );
        }

        public int[] Calculate()
        {
            engine.Output_Gamma_Parameter();
            return Class1.Read_Data;
        }
    }
}
