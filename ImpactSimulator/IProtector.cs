using System;
using System.Collections.Generic;
using System.Text;

namespace ImpactSimulator
{
    public interface IProtector
    {
        double Force { get; }
        double Deformation { get; set; }
        double Speed { get; set; }

        void Step(double timeStep);
        void Reset();
    }
}
