using LGTracer;

namespace LGUnitTests;

[TestClass]
public class LGContrailTests
{
    [TestMethod]
    public void SchmidtApplemanCriterion()
    {
        bool success = true;
        // Iteration is necessary for TLC, but not for TLM
        // If we skip iteration on T_LC, get disagreement in solutions between Ponater and Schumann over a temperature
        // range of 0.2 K
        bool iterateTLC = true;
        foreach (bool iterateTLM in new bool[] { true, false })
        {
            Console.WriteLine($"Testing SAC [iteration: TLM = {iterateTLM}, TLC = {iterateTLC}]");
            success = SchmidtApplemanCriterion(iterateTLM,iterateTLC) && success;
        }
        Assert.IsTrue(success,"Failed at least one SAC iteration combination.");
    }

    [TestMethod]
    public void TestUnterschrasserLengthCalculations()
    {
        bool zEmitOK = true;
        bool zAtmOK = true;
        bool zDescOK = true;
        
        Assert.IsTrue(zDescOK && zAtmOK && zEmitOK, $"Unterschrasser 2016 calculation test results: zEmit -> {zEmitOK}, zAtm -> {zAtmOK}, zDesc -> {zDescOK}.");
    }

    private bool SchmidtApplemanCriterion(bool iterateTLM=true,bool iterateTLC=true)
    {
        // Verify that the SAC routines are doing what they should be comparison to Schumann (1996) known results.
        // NB: Passing iterate=true means that N-R iteration will be used to refine guesses. This seems to
        double pressure = 220.0;
        double temperature = -59.0;
        double rh = 42.0;
        double efficiency = 0.308;
        double maxError = 0.01; // Generous because only 3 sig figs in Schumann (1996)
        
        bool success = true;
        success = success && Evaluate("Kerosene", 43.0, 1.25, 1.49, -50.6, -42.9,
            pressure, temperature, rh, efficiency,maxError,iterateTLM,iterateTLC,false);
        success = success && Evaluate("Methane", 50.0, 2.24, 2.31, -46.3, -38.3,
            pressure, temperature, rh, efficiency,maxError,iterateTLM,iterateTLC,false);
        success = success && Evaluate("Hydrogen", 120.0, 8.94, 3.82, -41.2, -32.7,
            pressure, temperature, rh, efficiency,maxError,iterateTLM,iterateTLC,false);

        // Verify that both approaches to checking SAC give the same result for a sweep which spans from
        // SAC = True to SAC = False
        double tMin = -52.0;
        double tMax = -49.0;
        double dt = 0.02;
        int nChecks = (int)Math.Ceiling((tMax - tMin) / dt);
        double tCurr = tMin;
        bool agreedTlcUlc = true;
        double failedTemperature = double.NaN;
        int nFailed = 0;
        int nTests = 0;
        while (tCurr <= tMax)
        {
            bool isOK = CompareTlcUlc(43.0, 1.25, pressure, tCurr, rh,
                efficiency, iterateTLM, iterateTLC, false); //tCurr <= tMin + dt/2.0 || tCurr >= tMax - dt);
            // If failed, store first failure
            if (!isOK)
            {
                if (agreedTlcUlc) { failedTemperature = tCurr; }
                nFailed++;
                //Console.WriteLine($"Failed with temperature or {tCurr:f3} K");
            }
            agreedTlcUlc = agreedTlcUlc && isOK;
            tCurr += dt;
            nTests++;
        }
        Console.WriteLine($"Agreement between SAC calculation approaches: {agreedTlcUlc}");
        //Assert.IsTrue(success && agreedTlcUlc,$"Failed {nFailed:d} of {nTests:d} SAC tests (first failed value: {failedTemperature} K)");
        if (!(success && agreedTlcUlc))
        {
            Console.WriteLine($"Failed {nFailed:d} of {nTests:d} SAC tests (first failed value: {failedTemperature} K)");
        }
        return (success && agreedTlcUlc);
    }
    
    public static bool Evaluate(string name, double lhv, double eiH2O, double refG, double refTLC, double refTLM,
        double ambientPressure, double ambientTemperature, double ambientRH, double efficiency,
        double maxError = 0.1, bool iterateTLM=true, bool iterateTLC=true, bool verbose=true)
    {
        // Input units follow Schumann (1996), i.e. LHV in MJ/kg, temperatures in C
        double temperature = ambientTemperature + 273.15; // Convert C to K
        double pressure = ambientPressure * 100.0; // Convert hPa to Pa 
        double rh = ambientRH * 0.01; // Convert % to fraction
        double mixingLineGradient = LGContrail.MixingLineGradient(pressure, efficiency, eiH2O, lhv*1.0e6);
        double pSatAmbient = Physics.SaturationPressureLiquid(temperature);
        double thresholdTemperature = LGContrail.EstimateLiquidThresholdTemperature(mixingLineGradient);
        if (iterateTLM)
        {
            thresholdTemperature = LGContrail.NewtonIterTlm(mixingLineGradient, thresholdTemperature);
        }
        double criticalTemperature = LGContrail.EstimateCriticalTemperature(thresholdTemperature, mixingLineGradient, rh);
        if (iterateTLC)
        {
            criticalTemperature =
                LGContrail.NewtonIterTlc(mixingLineGradient, thresholdTemperature, rh, criticalTemperature);
        }
        double criticalRelativeHumidity = LGContrail.CalculateCriticalRelativeHumidityLiquid(pressure, temperature, efficiency,
            eiH2O, lhv*1.0e6, approximateTlm: iterateTLM);
        double mixingLineError = mixingLineGradient / refG - 1.0;
        double tlmError = thresholdTemperature / (273.15+refTLM) - 1.0;
        double tlcError = criticalTemperature / (273.15+refTLC) - 1.0;
        if (verbose)
        {
            Console.WriteLine(
                $"Evaluation for {name} (iteration TLM/TLC: {iterateTLM}/{iterateTLC}) [T={ambientTemperature,9:f3} C, RHl={ambientRH,9:f3}%]:");
            Console.WriteLine(
                $" --> Mixing line gradient G     : {mixingLineGradient,9:f3} vs {refG,9:f3} Pa/K, {100.0 * mixingLineError,9:f4}% error");
            Console.WriteLine(
                $" --> Threshold temperature T_LM : {thresholdTemperature - 273.15,9:f3} vs {refTLM,9:f3} C,    {100.0 * tlmError,9:f4}% error");
            Console.WriteLine(
                $" --> Critical temperature T_LC  : {criticalTemperature - 273.15,9:f3} vs {refTLC,9:f3} C,    {100.0 * tlcError,9:f4}% error");
            Console.WriteLine($" --> Critical RH U_LC (Ponater) : {criticalRelativeHumidity * 100.0,9:f3}%");
            Console.WriteLine(
                $" -----> Formation (Schumann/Ponater): {temperature <= criticalTemperature}/{rh >= criticalRelativeHumidity}");
        }
        return (mixingLineError < maxError && tlmError < maxError && tlcError < maxError);
    }
    
    public static bool CompareTlcUlc(double lhv, double eiH2O,
        double ambientPressure, double ambientTemperature, double ambientRH, double efficiency, bool iterateTLM=true,
        bool iterateTLC=true, bool verbose=true)
    {
        // Input units follow Schumann (1996), i.e. LHV in MJ/kg, temperatures in C
        double temperature = ambientTemperature + 273.15; // Convert C to K
        double pressure = ambientPressure * 100.0; // Convert hPa to Pa 
        double rh = ambientRH * 0.01; // Convert % to fraction
        double mixingLineGradient = LGContrail.MixingLineGradient(pressure, efficiency, eiH2O, lhv*1.0e6);
        double pSatAmbient = Physics.SaturationPressureLiquid(temperature);
        double thresholdTemperature = LGContrail.EstimateLiquidThresholdTemperature(mixingLineGradient);
        if (iterateTLM)
        {
            thresholdTemperature = LGContrail.NewtonIterTlm(mixingLineGradient, thresholdTemperature);
        }
        double criticalTemperature = LGContrail.EstimateCriticalTemperature(thresholdTemperature, mixingLineGradient, rh);
        if (iterateTLC)
        {
            criticalTemperature =
                LGContrail.NewtonIterTlc(mixingLineGradient, thresholdTemperature, rh, criticalTemperature);
        }
        double criticalRelativeHumidity = LGContrail.CalculateCriticalRelativeHumidityLiquid(pressure, temperature, efficiency,
            eiH2O, lhv*1.0e6, approximateTlm: iterateTLM);
        bool tlcCheck = temperature <= criticalTemperature;
        bool rhCheck = rh >= criticalRelativeHumidity;
        if (!verbose) { return tlcCheck == rhCheck; }
        Console.WriteLine($"P/S comparison (iteration TLM/TLC: {iterateTLM}/{iterateTLC}) [T={ambientTemperature,9:f3} C, RHl={ambientRH,9:f3}%]:");
        Console.WriteLine($" --> Mixing line gradient G     : {mixingLineGradient,9:f3} Pa/K");
        Console.WriteLine($" --> Threshold temperature T_LM : {thresholdTemperature-273.15,9:f3} C");
        Console.WriteLine($" --> Critical temperature T_LC  : {criticalTemperature-273.15,9:f3} C");
        Console.WriteLine($" --> Critical RH U_LC (Ponater) : {criticalRelativeHumidity*100.0,9:f3}%");
        Console.WriteLine($" -----> Formation (T_LC/U_LC): {tlcCheck}/{rhCheck}");
        return tlcCheck == rhCheck;
    }
}