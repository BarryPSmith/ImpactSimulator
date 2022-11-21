using System;
using System.Collections.Generic;
using System.Text;

namespace ImpactSimulator
{
    interface IProtectee
    {
        double Bottom { get; }
        double ExternalForce { get; set; }

        void SetSpeed(double speed);
        void Step(double timeStep);
        void Reset();
    }
}
