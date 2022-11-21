using System;
using System.Collections.Generic;
using System.Text;

namespace ImpactSimulator.Protectors
{
    class BaffledAirProtector : IProtector, IReportable
    {
        double _totalMassTransferred;


        public double Area { get; set; }
        public double InitialHeight { get; set; }
        public double InitialPressure { get; set; } = 101325;
        public double OutsidePressure { get; set; } = 101325;
        public double InitialTemperature { get; set; } = 300;
        public double BaffledVolume { get; set; }
        public double MolecularWeight { get; set; } = 0.029; // kg / mol
        public double HoleSize { get; set; }
        public double Gamma { get; set; } = 1.4;
        public double Cv { get; set; } = 717; // kJ / kg K
        public double PistonThermalConductivity { get; set; } = 0;

        public double FlowSpeed { get; private set; }

        private double _force;
        public double Force
        {
            get => _force;
            set
            {
                _force = value;
                if (value > 0)
                { }
            }
        }

        public double Deformation { get; set; }
        public double Speed { get; set; }

        public void Reset()
        {
            Deformation = Speed = 0;
            ImpactContainer = new GasContainer(InitialPressure, InitialHeight * Area, InitialTemperature, Gamma,
                Cv, MolecularWeight);
            BaffleContainer = new GasContainer(InitialPressure, BaffledVolume, InitialTemperature, Gamma,
                Cv, MolecularWeight);
            _totalMassTransferred = 0;
            Force = 0;
        }

        public void Step(double timeStep)
        {
            //CalculateForceLeakless();
            Compress(timeStep);
            TransferGas(timeStep);
            LeakHeat(timeStep);
            CalculateForce();
        }

        public class GasContainer
        {
            public GasContainer(double pressure, double volume, double temperature, double gamma,
                double cv, double molecularWeight)
            {
                Pressure = pressure;
                Volume = volume;
                Temperature = temperature;
                Gamma = gamma;
                Cv = cv;
                MolecularWeight = molecularWeight;
            }
            public double Pressure { get; set; }
            public double Volume { get; set; }
            public double Temperature { get; set; }
            public double Gamma { get; set; }
            public double Cv { get; set; }
            public double MolecularWeight { get; set; }

            public void Expand(double volumeChange)
            {
                Pressure *= Math.Pow(1 + volumeChange / Volume, -Gamma);
                Temperature *= Math.Pow(1 + volumeChange / Volume, 1 - Gamma);
            }

            public void AddHeat(double heatAmount)
            {
                var mass = GetDensity() * Volume;
                var deltaT = heatAmount / (mass * Cv);
                var newTemp = Temperature + deltaT;
                Pressure *= newTemp / Temperature;
                Temperature = newTemp;
            }


            //PV = nRT; n = PV / RT
            //m = nM; m = MPV / RT; rho = MP / RT
            public double GetDensity()
                => MolecularWeight * Pressure / (R * Temperature);
        }

        public GasContainer ImpactContainer { get; private set; }
        public GasContainer BaffleContainer { get; private set; }

        public string[] ReportedParameters => new string[] { "P_Impact", "T_Impact", "P_Baffle", "T_Baffle", "Flow Speed" };

        const double R = 8.315;
        private void TransferGas(double stepSize)
        {
            // Simple calculation of leak rate: Conservation of energy
            // Let's assume that the expansion of the gas as it leaks is not converted to KE.
            // So for a volume dV, we have dE = deltaP dV
            // dm = rho * dV
            // KE = 1/2 dm * u^2
            // PV = nRT
            // m = nM, n = m / M
            // PV = m / M RT
            // rho = m / V = PM / RT
            // KE = 1/2 * rho * dV * u^2
            //    = deltaP dV
            // 1/2 * rho * u^2 = deltaP
            // 1/2 * PM / RT * u^2 = deltaP
            // u = sqrt(2 * deltaP * RT / PM)
            // Now, switch what dv, dm, etc means as we flow out the hole:
            // dV = u A dt
            if (BaffleContainer.Pressure == ImpactContainer.Pressure)
                return;
            GasContainer sourceContainer;
            GasContainer destinationContainer;
            int flowDirection;
            if (BaffleContainer.Pressure > ImpactContainer.Pressure)
            {
                sourceContainer = BaffleContainer;
                destinationContainer = ImpactContainer;
                flowDirection = 1;
            }
            else
            {
                sourceContainer = ImpactContainer;
                destinationContainer = BaffleContainer;
                flowDirection = -1;
            }
            double deltaP = sourceContainer.Pressure - destinationContainer.Pressure;
            
            double gasSpeed = Math.Sqrt(2 * deltaP * R * sourceContainer.Temperature / (sourceContainer.Pressure * MolecularWeight));
            FlowSpeed = gasSpeed * flowDirection;
            double volumeFromSource = gasSpeed * HoleSize * stepSize;
            var srcDensity = sourceContainer.GetDensity();
            var massTransferred = volumeFromSource * srcDensity;
            _totalMassTransferred += massTransferred;

            // Source container expands adiabatically into the volume that has been lost:
            sourceContainer.Expand(volumeFromSource);

            // Ok, now we've moved a chunk of mass into the destination container, but it's still compressed.
            // Let it expand until density matches:
            var finalVolumeInDestination = massTransferred / destinationContainer.GetDensity();
            // Expanding will change its temperature.
            if (finalVolumeInDestination > 0)
            {
                var parcelTInDestination = sourceContainer.Temperature * Math.Pow(volumeFromSource / finalVolumeInDestination, Gamma - 1);
                destinationContainer.Expand(-finalVolumeInDestination);

                // Current temperature in destination container has only been changed by work done from volume of gas entering.
                // But the new gas may be at a different temperature, and also has kinetic energy.
                // Assume kinetic energy is rapidly dissipated to heat.
                var kineticEnergy = 0.5 * massTransferred * gasSpeed * gasSpeed;
                var excessEnthalpyInGas = Cv * (parcelTInDestination - destinationContainer.Temperature) * massTransferred;

                destinationContainer.AddHeat(kineticEnergy + excessEnthalpyInGas);
            }
        }

        private void LeakHeat(double stepSize)
        {
            var heatLeaked = (ImpactContainer.Temperature - InitialTemperature) * PistonThermalConductivity * stepSize;
            ImpactContainer.AddHeat(-heatLeaked);

        }

        private void Compress(double stepSize)
        {
            double volumeChange = Speed * stepSize * Area;
            ImpactContainer.Expand(-volumeChange);
            ImpactContainer.Volume = Area * (InitialHeight - Deformation);
        }

        private void CalculateForce()
        {
            if (Deformation < 0)
            {
                Force = 0;
                return;
            }
            var deltaP = ImpactContainer.Pressure - OutsidePressure;
            if (deltaP < 0)
                Force = 0;
            else
                Force = -deltaP * Area;
        }

        public IEnumerable<double> GetReportValues()
        {
            yield return ImpactContainer.Pressure;
            yield return ImpactContainer.Temperature;
            yield return BaffleContainer.Pressure;
            yield return BaffleContainer.Temperature;
            yield return FlowSpeed;
        }
    }
}
