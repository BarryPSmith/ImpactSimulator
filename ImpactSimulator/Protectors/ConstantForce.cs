using System;
using System.Collections.Generic;
using System.Text;

namespace ImpactSimulator.Protectors
{
    class ConstantForce : IProtector
    {
        double _crushLevel;
        public double Cushion { get; set; } = 1E-3;
        public double SpringConstant => ForceConstant / Cushion;
        //double Cushion => ForceConstant / SpringConstant;
        public double Dampening { get; set; } = 0;
        public double ForceConstant { get; set; }
        public double Force
        {
            get
            {
                if (Deformation <= 0 || (Deformation < _crushLevel))
                    return 0;
                var dampeningForce = Dampening * Speed;
                var standardForce = ForceConstant + dampeningForce;
                if (Deformation < _crushLevel + Cushion)
                {
                    var frac = (Deformation - _crushLevel) / Cushion;
                    return -standardForce * frac;
                }
                return -standardForce;
            }
        }

        double _deformation;
        public double Deformation 
        {
            get => _deformation;
            set
            {
                _deformation = value;
                if (value - Cushion > _crushLevel)
                    _crushLevel = value - Cushion;
            }
        }
        public double Speed { get; set; }

        public void Reset()
        {
            _crushLevel = Deformation = Speed = 0;
        }

        public void Step(double timeStep)
        {
            // Do nothing
        }
    }
}
