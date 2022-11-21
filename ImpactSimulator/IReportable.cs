using System;
using System.Collections.Generic;
using System.Text;

namespace ImpactSimulator
{
    interface IReportable
    {
        string[] ReportedParameters { get; }
        IEnumerable<double> GetReportValues();
    }
}
