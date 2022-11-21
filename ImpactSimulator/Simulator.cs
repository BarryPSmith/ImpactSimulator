using System;
using System.Collections.Generic;
using System.Text;

namespace ImpactSimulator
{
    class Simulator
    {
        public double Time { get; private set; }

        public const double Gravity = 9.8; // m/s^2
        public double StepSize { get; set; }
        public IProtector Protector { get; set; }
        public IProtectee Protectee { get; set; }


        public void SetDrop(double dropHeight)
        {
            SetImpact(Math.Sqrt(2 * Gravity * dropHeight));
        }

        public void SetImpact(double speed)
        {
            Protectee.SetSpeed(speed);
        }

        public void Step()
        {
            SimulateStep();
        }

        void SimulateStep()
        {
            double force = Protector.Force;
            Protectee.ExternalForce = force;
            double oldBottom = Protectee.Bottom;
            
            Protectee.Step(StepSize);
            double newBottom = Protectee.Bottom;
            var velocity = (newBottom - oldBottom) / StepSize;
            Protector.Deformation = newBottom;
            Protector.Speed = velocity;

            if (Time >= 0.02)
            { }
            Protector.Step(StepSize);
            Time += StepSize;
        }

        internal void Reset()
        {
            Time = 0;
            Protectee.Reset();
            Protector.Reset();
        }
    }
}
