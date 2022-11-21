using System;
using System.Collections.Generic;
using System.Text;

namespace ImpactSimulator.Protectors
{
    class LiquidProtector : IProtector
    {
        public double ImpactArea { get; set; }
        public double ReleaseArea { get; set; }
        public double BulkModulus { get; set; } = 2.1E9; // Pa, water
        public double InitialVolume { get; set; }
        public double FreeDensity { get; set; } = 1000; // kg/m^3, water
        public double Pressure { get; private set; }
        
        public double Force { get; set; }

        public double Deformation { get; set; }
        public double Speed { get; set; }

        public void Reset()
        {
            Pressure = Force = Deformation = Speed = 0;
        }

        public void Step(double timeStep)
        {
            //StepCompressible(timeStep);
            Force = GetIncompressibleForce();
        }

        double GetIncompressibleForce()
        {
            //Simple approximation for now: Ignore compressibility
            if (Speed <= 0 || Deformation <= 0)
                return 0;
            var releaseSpeed = Speed * ImpactArea / ReleaseArea;
            // Bernoulli: PV = 1/2 (rho * V) * u^2
            // P = 1/2 rho u^2
            var pressure = 0.5 * FreeDensity * releaseSpeed * releaseSpeed;
            return -pressure * ImpactArea;
        }

        double StepCompressible(double timestep)
        {
            if (Deformation <= 0)
            {
                return 0;
            }
            // dV/dt = -Speed * ImpactArea
            // dV/dt = -Volume / BulkModulus * dP/dt - ReleaseSpeed * ReleaseArea
            // P = 1/2 * rho * ReleaseSpeed^2
            // dP/dt = rho * ReleaseSpeed * dRS/dt
            // This can be solved exactly but it uses the lambert W function so bugger that.
            // Let's see if we can solve it iteratively.
            var volume = InitialVolume - ImpactArea * Deformation;
            var dVdt = -Speed * ImpactArea;
            var oldPressure = Pressure;
            var newP = Pressure;
            double tolerance = 10 + Pressure / 1000;
            double deltaP = 0, oldDeltaP, ddeltaP;
            double releaseSpeed = 0, dPdt;
            double smallestddp = double.PositiveInfinity;
            double newPAtSmallestddp = newP;
            int maxSteps = 100;
            bool converged = false;
            bool hasFailed = false;
            for (int i = 0; i < maxSteps; i++)
            {
                if (newP < 0)
                {
                    if (hasFailed)
                        break;
                    hasFailed = true;
                    releaseSpeed = releaseSpeed / 2;
                }
                else
                    releaseSpeed = Math.Sqrt(2 * newP / FreeDensity);
                dPdt = -(dVdt + releaseSpeed * ReleaseArea) / volume * BulkModulus;
                oldDeltaP = deltaP;
                deltaP = dPdt * timestep;
                ddeltaP = Math.Abs(oldDeltaP - deltaP);
                newP = Pressure + deltaP;
                if (ddeltaP < smallestddp)
                {
                    smallestddp = ddeltaP;
                    newPAtSmallestddp = newP;
                }
                if (ddeltaP < tolerance)
                {
                    converged = true;
                    break;
                }
            }
            if (!converged)
            { }

            Pressure = newPAtSmallestddp;
            var f = -Pressure * ImpactArea;
            return Math.Min(f, 0);
        }
    }
}
