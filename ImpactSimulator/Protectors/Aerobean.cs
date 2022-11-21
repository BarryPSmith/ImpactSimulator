using System;
using System.Collections.Generic;
using System.Text;

namespace ImpactSimulator.Protectors
{
    class Aerobean : IProtector
    {
        private double _maxDeformation;

        public double Force { get; private set; }

        public double Deformation { get; set; }
        public double Speed { get; set; }

        public double ForceConstant { get; set; } = 625E3; // kN / m^2, 16kN @ 16cm
        public double Rebound { get; set; } = 0.03; //m

        public void Reset()
        {
            Force = 0;
            _maxDeformation = 0;
        }

        public void Step(double timeStep)
        {
            Force = GetForce();
            if (Deformation > _maxDeformation)
                _maxDeformation = Deformation;
        }

        private double GetForce()
        {
            if (Deformation >= _maxDeformation)
                return -ForceConstant * Deformation * Deformation;
            if (Deformation <= 0)
                return 0;
            if (Deformation > _maxDeformation - Rebound)
            {
                var forceAtMaxDeformation = -ForceConstant * _maxDeformation * _maxDeformation;
                var portion = 1 - (_maxDeformation - Deformation) / Rebound;
                return forceAtMaxDeformation * portion * portion;
            }
            return 0;
        }
    }
}
