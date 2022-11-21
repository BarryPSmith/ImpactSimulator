using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImpactSimulator
{
    class VanToenEtAlTrunk : IProtectee,
        IReportable
    {
        class RigidMass
        {
            public double Offset { get; set; }
            public double Speed { get; set; }
            public double Mass { get; private set; }
            public string Name { get; private set; }
            public RigidMass(string name, double mass)
            {
                Name = name;
                Mass = mass;
            }
        }

        class DampedSpring
        {
            double _forceAtMaxDeformation;

            public double SpringConstant { get; private set; }
            public double DampingConstant { get; private set; }
            public string Name { get; private set; }
            public double MaxDeformation { get; set; }
            public double SpringConstantBeyondMax { get; set; }
            public DampedSpring(string name, double springConstant, double dampingConstant,
                double maxDeformation = double.PositiveInfinity, double? springConstantBeyondMax = null)
            {
                Name = name;
                SpringConstant = springConstant;
                DampingConstant = dampingConstant;
                MaxDeformation = maxDeformation;
                SpringConstantBeyondMax = springConstantBeyondMax ?? springConstant * 10;
                _forceAtMaxDeformation = - maxDeformation * springConstant;
            }
            public double GetForce(double compression, double speed)
            {
                var dampingForce = -DampingConstant * speed;
                if (compression < MaxDeformation)
                    return -SpringConstant * compression + dampingForce;
                return _forceAtMaxDeformation 
                    - SpringConstantBeyondMax * (compression - MaxDeformation) 
                    + dampingForce;
            }
        }

        RigidMass[] _masses = new RigidMass[]
        {
            // Paper suggested estimate of 10g for skin covering buttocks
            // This seems far too light, 200g seems a more reasonable estimate.
            new RigidMass("Skin", 0.2),
            new RigidMass("Pelvis", 16),
            new RigidMass("Sacrum", 0.7),
            new RigidMass("L5", 1.8),
            new RigidMass("L4", 2.5),
            new RigidMass("Upper Body", 33)
        };

        double[] _netForces = new double[6];

        DampedSpring[] _springs = new DampedSpring[]
        {
            // There should probably be a
            // factor of .65 for buttocks because Van Toen et al
            // used total mody weight instead of torso weight for
            // calculating buttock spring properties.
            // But we're not going to include it because we get too
            // much buttock deformation
            new DampedSpring("Buttocks", 180.5E3, 3.13E3, 0.035, 2E6),
            new DampedSpring("Sacroiliac", 1050E3, 237, 0.005),
            new DampedSpring("L5/S1", 3503E3, 237, 0.010),
            new DampedSpring("L4/L5", 2399E3, 237, 0.010),
            new DampedSpring("L3/L4", 2749E3, 237, 0.010)
        };
        double[] _springForces = new double[5];

        public string[] ReportedParameters =>
            _springs.SelectMany(s =>
                new string[] { 
                    s.Name + " Compression",
                    s.Name + " Force" })
            .Concat(_masses.Select(m => m.Name + " Offset"))
            .Append("Max Spinal Force")
            .ToArray();

        public IEnumerable<double> GetReportValues()
        {
            for (int i = 0; i < _springs.Length; i++)
            {
                yield return _masses[i + 1].Offset - _masses[i].Offset;
                yield return _springForces[i];
            }
            foreach (var m in _masses)
                yield return m.Offset;
            yield return MaxSpinalForce;
        }

        public double MaxSpinalForce => _springForces.Skip(2).Max(f => Math.Abs(f));

        public double Bottom => _masses[0].Offset;

        public double ExternalForce { get; set; }

        public void SetSpeed(double speed)
        {
            foreach (var mass in _masses)
                mass.Speed = speed;
        }

        public void Step(double timeStep)
        {
            var offsets = _masses.Select(m => m.Offset).ToList();
            double bottomForce = ExternalForce;
            for (int i = 0; i < _masses.Length; i++)
            {
                var mass = _masses[i];
                double upperForce = 0;
                if (i < _masses.Length - 1)
                {
                    var upperSpring = _springs[i];
                    var upperMass = _masses[i + 1];
                    var upperCompression = upperMass.Offset - mass.Offset;
                    var speedDiff = upperMass.Speed - mass.Speed;
                    upperForce = upperSpring.GetForce(upperCompression, speedDiff);
                    _springForces[i] = upperForce;
                }
                _netForces[i] = bottomForce - upperForce;
                // Upper force on this mass is bottom force on next mass:
                bottomForce = upperForce;
            }
            var ts2 = timeStep * timeStep;
            for (int i = 0; i < _masses.Length; i++)
            {
                var acceleration = _netForces[i] / _masses[i].Mass + Simulator.Gravity;
                _masses[i].Offset += _masses[i].Speed * timeStep + 0.5 * acceleration * ts2;
                _masses[i].Speed += acceleration * timeStep;
            }
        }
    
        public void Reset()
        {
            for (int i = 0; i < _netForces.Length; i++)
                _netForces[i] = 0;
            for (int i = 0; i < _springForces.Length; i++)
                _springForces[i] = 0;
            for (int i = 0; i < _masses.Length; i++)
            {
                _masses[i].Offset = 0;
                _masses[i].Speed = 0;
            }
            ExternalForce = 0;
        }
    }
}
