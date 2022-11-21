using System;
using System.Collections.Generic;
using System.Text;

namespace ImpactSimulator.Protectors
{
    class AdiabaticPiston : IProtector
    {
        double _pvToGamma;
        double _initialVolume;
        double InitialVolume
        {
            get => _initialVolume;
            set
            {
                _initialVolume = value;
                _pvToGamma = InitialPressure * Math.Pow(_initialVolume, Gamma);
            }
        }
        double _area;
        public double Area
        {
            get => _area;
            set => _area = value;
        }
        double _initialHeight;
        public double InitialHeight
        {
            get => _initialHeight;
            set => _initialHeight = value;
        }
        private double _initialPressure = 101325;
        public double InitialPressure
        {
            get => _initialPressure;
            set
            {
                _initialPressure = value;
                _pvToGamma = InitialPressure * Math.Pow(InitialVolume, Gamma);
            }
        }
        public double InitialTemperature { get; set; } = 300;

        private double _gamma = 1.4;
        public double Gamma
        {
            get => _gamma;
            set
            {
                _gamma = value;
                _pvToGamma = InitialPressure * Math.Pow(InitialVolume, Gamma);
            }
        }

        private double _force;
        public double Force
        {
            get => _force;
            set
            {
                _force = value;
            }
        }

        public double Deformation { get; set; }
        public double Speed { get; set; }
        public double Pressure { get; private set; }

        public void Reset()
        {
            Deformation = Speed = 0;
            Force = 0;
            CalculateSideLength();
            Pressure = InitialPressure;
            MaximumStretch = 0;
            InitialVolume = Volume(0, InitialPressure, out _);
        }

        public void Step(double timeStep)
        {
            Force = CalculateForceLeakless(out var stretchVolume);
            if (stretchVolume > MaximumStretch)
                MaximumStretch = stretchVolume;
        }

        public double Volume(double deformation, double pressure,
            out double stretchVolume)
        {
            stretchVolume = GetStretchedVolume(pressure);
            return Area * (_initialHeight - deformation)
                + SideBulgeVolume(deformation)
                + stretchVolume;
        }

        public double FabricStretchiness { get; set; } // In Pa-1
        public double FabricMemory { get; set; }
        public double MaximumStretch { get; private set; }

        public double GetStretchedVolume(double pressure)
        {
            var nonMemorisedVolume = FabricStretchiness * (pressure - InitialPressure) * InitialVolume;
            if (nonMemorisedVolume < MaximumStretch)
            {
                var memorisedVolume = nonMemorisedVolume * (1 + FabricMemory);
                if (memorisedVolume > MaximumStretch)
                    memorisedVolume = MaximumStretch;
                return memorisedVolume;
            }
            else
                return nonMemorisedVolume;
        }

        public double BulgingLength { get; set; }

        void CalculateSideLength()
        {
            if (BulgingLength == 0)
            {
                SideLength = InitialHeight;
                return;
            }
            double lastTest = InitialHeight;
            double lastVol = InitialHeight * Area;
            double thisTest = InitialHeight * 2;
            double tolerance = lastVol * 1E-6;
            double test = 1E-4;
            while (true)
            {
                double diff;
                if (thisTest <= InitialHeight)
                    diff = 1;
                else
                {
                    SideLength = thisTest;
                    var vol0 = Volume(0, InitialPressure, out _);
                    var vol1 = Volume(test, InitialPressure, out _);
                    diff = vol1 - vol0;
                    if (diff < 0 && -diff < tolerance)
                        return;
                }
                double nextTest;
                if (Math.Sign(diff) != Math.Sign(thisTest - lastTest))
                    nextTest = (thisTest + lastTest) / 2;
                else
                    nextTest = thisTest + (thisTest - lastTest);
                lastTest = thisTest;
                thisTest = nextTest;
            }
        }

        double _sideLength;
        double SideLength
        {
            get => _sideLength;
            set
            {
                if (_sideLength == value)
                    return;
                _hs = null;
                _sideLength = value;
            }
        }

        const int hCount = 400;
        double _rMin, _rStep;
        double[] _hs;
        void PopulateHs()
        {
            if (Math.Abs(SideLength - 0.16648437500000002) < 0.00000001)
            { }
            if (_hs != null)
                return;
            _hs = new double[hCount];
            _rMin = SideLength / (2 * Math.PI);
            var rMax = _rMin * 101;
            for (int j = 0; j < 2; j++)
            {
                _rStep = (rMax - _rMin) / (hCount - 1);
                for (int i = 0; i < hCount; i++)
                {
                    var r = _rMin + _rStep * i;
                    var height = 2 * r * Math.Sin(Math.PI - SideLength / (2 * r));
                    if (j == 0 && height > InitialHeight)
                    {
                        rMax = r;
                        break;
                    }
                    _hs[i] = height;
                }
            }
        }

        double SideBulgeVolume(double Deformation)
        { 
            if (BulgingLength == 0)
                return 0;
            /*if (Deformation <= 0)
                return 0;*/
            if (Deformation >= InitialHeight)
                return 0;
            //Simple assumption: Side bulge will be two triangles.
            var height = InitialHeight - Deformation;
            var bulge = Math.Sqrt(InitialHeight * InitialHeight - height * height) / 2;
            var area = 0.5 * height * bulge;
            //return area * BulgingLength;
            //Or maybe the arc of a circle
            // Arc length = r * theta
            // theta = 2(pi - arcsin(height / 2r))
            // Arc length = 2(pi*r - r*arcsin(height / 2r))
            // height = 2r * sin(pi - arc length / 2r)
            // Solve for r, or use a lookup
            PopulateHs();
            var idx = Array.BinarySearch(_hs, height);
            double r;
            if (idx >= 0)
                r = _rMin + _rStep * idx;
            else
            {
                var nextIdx = ~idx;
                if (nextIdx >= hCount)
                    return 0;
                var prevIdx = nextIdx - 1;
                var grad = _hs[nextIdx] - _hs[prevIdx];
                var diff = height - _hs[prevIdx];
                var frac = diff / grad;
                r = _rMin + _rStep * (prevIdx + frac);
            }
            var theta = SideLength / r;

            //var theta = 2 * (Math.PI - Math.Asin(height / (2 * r)));
            if (theta > Math.PI)
                area = r * r * theta / 2 + r * Math.Cos(theta / 2) * height / 2;
            else
                area = r * r * theta / 2 - r * Math.Cos(theta / 2) * height / 2;

            if (Deformation > 0.09)
            { }
            if (Deformation > 0.091)
            { }

            return area * BulgingLength;
        }
        
        private double CalculateForceLeakless(out double stretchVolume)
        {
            if (Deformation < 0)
            {
                stretchVolume = 0;
                return 0;
            }
            if (Deformation > 0.1)
            { }
            var volume = Volume(Deformation, Pressure, out _);
            var newPressure = _pvToGamma / Math.Pow(volume, Gamma);
            var stretchVolumeTmp = 0.0;
            for (int i = 0; i < 5; i++)
            {
                volume = Volume(Deformation, newPressure, out stretchVolumeTmp);
                var thisPressure = _pvToGamma / Math.Pow(volume, Gamma);

                if (volume < 0 || thisPressure < InitialPressure || thisPressure > 1E6)
                { }

                newPressure = (thisPressure + newPressure) * 0.5;
            }
            Pressure = newPressure;
            stretchVolume = stretchVolumeTmp;
            if (Pressure < InitialPressure)
                return 0;
            return -(Pressure - InitialPressure) * Area;
        }
    }
}
