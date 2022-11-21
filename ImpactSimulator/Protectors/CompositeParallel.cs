using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImpactSimulator.Protectors
{
    class CompositeParallel : IProtector
    {
        public List<IProtector> Protectors { get; set; }
        public double Force => Protectors.Sum(p => p.Force);

        private double _deformation;
        public double Deformation
        {
            get => _deformation;
            set
            {
                _deformation = value;
                foreach (var p in Protectors)
                    p.Deformation = _deformation;
            }
        }
        private double _speed;
        public double Speed
        {
            get => _speed;
            set
            {
                _speed = value;
                foreach (var p in Protectors)
                    p.Speed = value;
            }
        }

        public void Step(double timeStep)
        {
            foreach (var p in Protectors)
                p.Step(timeStep);
        }

        public void Reset()
        {
            _speed = _deformation = 0;
            foreach (var p in Protectors)
                p.Reset();
        }
    }
}
