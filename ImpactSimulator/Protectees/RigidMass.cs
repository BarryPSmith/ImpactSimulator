using System;
using System.Collections.Generic;
using System.Text;

namespace ImpactSimulator
{
    class RigidMass : IProtectee
    {
        double _speed;

        public double Mass { get; set; }

        public double Bottom { get; set; }

        public double ExternalForce { get; set; }

        public void SetSpeed(double speed)
        {
            _speed = speed;
        }

        public void Step(double timeStep)
        {
            var acceleration = ExternalForce / Mass + Simulator.Gravity;
            Bottom += _speed * timeStep + 0.5 * acceleration * timeStep * timeStep;
            _speed += acceleration * timeStep;
        }

        public void Reset()
        {
            Bottom = ExternalForce = _speed = 0;
        }
    }
}
