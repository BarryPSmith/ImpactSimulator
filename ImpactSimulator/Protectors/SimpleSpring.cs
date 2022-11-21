using System;
using System.Collections.Generic;
using System.Text;

namespace ImpactSimulator
{
    class SimpleSpring : IProtector
    {
        public double SpringConstant { get; set; }
        public double Force
        {
            get
            {
                if (Deformation <= 0)
                    return 0;
                var springForce = -SpringConstant * Deformation;
                var dampeningForce = -Dampening * Speed;
                var ret = springForce + dampeningForce;
                if (Dampening > 0)
                { }
                if (ret > 0)
                    return 0;
                return ret;
            }
        }
        public double Dampening { get; set; }

        public double Deformation { get; set; }
        public double Speed { get; set; }

        public void Reset()
        {
            Deformation = Speed = 0;
        }

        void IProtector.Step(double timeStep)
        {
            // Do nothing
        }
    }
}
