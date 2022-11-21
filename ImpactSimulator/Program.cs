using ImpactSimulator.Protectors;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ImpactSimulator
{
    class Program
    {
        struct StepInfo
        {
            public double Time;
            public double Deformation;
            public double Force;
            public double[] ReportedValues;
            public override string ToString()
            {
                var ret = $"{Deformation},{Force}";
                if (ReportedValues != null)
                    ret += $",{string.Join(',', ReportedValues)}";
                return ret;
            }
        }

        struct TestIdentifier
        {
            public int DropHeightCm;
            public string ProtectorName;
            public string ProtecteeName;
            public string[] ReportedParameters;
        }
        static void Main(string[] args)
        {
            string configFn = args.FirstOrDefault() ?? "Config.json";
            var config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configFn));
            if (config.ProtecteeModels.Count == 1 && config.ProtecteeModels[0] == ProtecteeType.VanToenTrunk)
                config.SummaryType = SummaryType.HumanImpact;
            bool writeFile = config.WriteTraces;
            bool recordProtector = false;
            bool recordProtectee = true;

            Dictionary<string, IProtector> protectors =
                config.ProtectorModels
                .Where(pm => (pm.Include ?? config.DefaultInclude) && config.SetsToModel.Contains(pm.ModelSet))
                .ToDictionary(
                    pm => pm.Name,
                    pm => pm.GetModel());

            Dictionary<string, IProtectee> protectees =
                config.ProtecteeModels.SelectMany(pm => pm == ProtecteeType.Rigid ?
                    config.RigidMasses.Select(m => new { Name = $"{m}kg Rigid", Model = (IProtectee)new RigidMass { Mass = m } })
                    :
                    new[] { new { Name = "Human Model", Model = (IProtectee)new VanToenEtAlTrunk() } }
                    )
                .ToDictionary(a => a.Name, a => a.Model);

            var dropHeights = config.DropHeights;

            var simulator = new Simulator()
            {
                StepSize = config.StepSize
            };

            int steps = config.Steps;

            Dictionary<TestIdentifier, StepInfo[]> tests = new Dictionary<TestIdentifier, StepInfo[]>();

            foreach (var height in dropHeights)
            {
                
                foreach (var protector in protectors)
                {
                    simulator.Protector = protector.Value;
                    foreach (var protectee in protectees)
                    {
                        var testId = new TestIdentifier
                        {
                            DropHeightCm = (int)(height * 100),
                            ProtecteeName = protectee.Key,
                            ProtectorName = protector.Key,
                        };
                        var protectorReportable = protector.Value as IReportable;
                        var protecteeReportable = protectee.Value as IReportable;
                        string[] reportedParameters = null;
                        List<bool> shouldReport = null;
                        if (protectorReportable != null)
                        {
                            if (recordProtector)
                                reportedParameters = protectorReportable.ReportedParameters;
                            else
                                protectorReportable = null;
                        }
                        if (protecteeReportable != null)
                        {
                            if (recordProtectee)
                                reportedParameters = (testId.ReportedParameters ?? new string[0])
                                    .Concat(protecteeReportable.ReportedParameters)
                                    .ToArray();
                            else
                                protecteeReportable = null;
                        }
                        if (reportedParameters != null)
                        {
                            if (config.ColumnFilters != null)
                            {
                                var regexes = config.ColumnFilters.Select(s => new Regex(s));
                                shouldReport = reportedParameters
                                    .Select(n => regexes.Any(r => r.IsMatch(n)))
                                    .ToList();
                                reportedParameters = reportedParameters.Where((n, i) =>
                                    shouldReport[i]).ToArray();
                            }
                            else
                                shouldReport = reportedParameters.Select(r => true).ToList();
                            testId.ReportedParameters = reportedParameters;
                        }

                        simulator.Protectee = protectee.Value;
                        simulator.Reset();
                        simulator.SetDrop(height);

                        StepInfo[] stepInfos = new StepInfo[steps];
                        for (int step = 0; step < steps; step++)
                        {
                            stepInfos[step].Time = simulator.Time;
                            stepInfos[step].Deformation = simulator.Protector.Deformation;
                            stepInfos[step].Force = -simulator.Protector.Force;
                            if (protectorReportable != null || protecteeReportable != null)
                            {
                                stepInfos[step].ReportedValues =
                                    (protectorReportable?.GetReportValues() ?? Enumerable.Empty<double>())
                                    .Concat(
                                        protecteeReportable?.GetReportValues() ?? Enumerable.Empty<double>())
                                    .Where((v, i) => shouldReport[i])
                                    .ToArray();
                            }
                            simulator.Step();
                        }
                        tests[testId] = stepInfos;

                        Console.WriteLine($"Completed {height} / {protector.Key} / {protectee.Key}");
                    }
                }
            }
            Console.WriteLine(GenerateSummary(tests, config.SummaryType, OutputGroupingType.DropHeight,
                config.StepSize));
            if (writeFile)
                WriteFiles(config, tests);
        }

        private static void WriteFiles(Config config, Dictionary<TestIdentifier, StepInfo[]> tests)
        {
            int fileIndex = 0;
            while (tests.Keys.Any(ti => File.Exists($"Test {GetGroupName(config.OutputGrouping, ti)} ({fileIndex}).csv")))
                fileIndex++;

            foreach (var protectorGroup in tests.GroupBy(kvp => GetGroupName(config.OutputGrouping, kvp.Key)))
            {
                var fn = $"Test {protectorGroup.Key} ({fileIndex}).csv";

                var header = string.Join(',',
                    protectorGroup.Select(n =>
                    {
                        var prefix = GetHeaderPrefix(config.OutputGrouping, n.Key);
                        var headerGroup = $"{prefix} Deformation,{prefix} Force";
                        if (n.Key.ReportedParameters != null)
                            headerGroup += "," + string.Join(',', n.Key.ReportedParameters.Select(rp => $"{prefix} {rp}"));
                        return headerGroup;
                    }));
                header = "Time," + header;

                var bodies = Enumerable.Range(0, config.Steps)
                    .Select(i =>
                        protectorGroup.FirstOrDefault().Value[i].Time
                        + ","
                        + string.Join(',', protectorGroup
                            .Select(v => v.Value[i].ToString()))
                        )
                    .ToList();


                File.WriteAllLines(fn,
                    new[] { header }
                    .Concat(bodies));
            }
        }

        private static string GenerateSummary(Dictionary<TestIdentifier, StepInfo[]> tests,
            SummaryType summaryType, OutputGroupingType outputGrouping,
            double stepSize)
        {
            var grouped = tests.GroupBy(t => $"Drop Height: {t.Key.DropHeightCm}cm");
            if (outputGrouping == OutputGroupingType.Protector)
                grouped = tests.GroupBy(t => $"Protector: {t.Key.ProtectorName}");
            StringBuilder ret = new StringBuilder();

            const int firstColWidth = 15;
            const int colWidth = 12;

            foreach (var grp in grouped)
            {
                ret.AppendLine();
                ret.AppendLine(grp.Key);
                string[] colNames;
                if (summaryType == SummaryType.EN1651)
                    colNames = new[] { "", "Protectee", "Max Force", "Deformation",
                        "T > 19kN",
                        "T > 10kN",
                    };
                else
                    colNames = new[] { "", "Protectee", "Max Force", "Deformation",
                        "F_Spine (max)",
                    //"Max Buttock Compression"
                    };
                if (outputGrouping == OutputGroupingType.Protector)
                    colNames[0] = "Drop Height";
                else
                    colNames[0] = "Protector";
                ret.Append($"{colNames[0],firstColWidth}|");
                ret.AppendLine(string.Join('|', colNames.Skip(1).Select(s => $"{s,colWidth}")));
                ret.AppendLine(new string('-', firstColWidth) + "|" +
                    string.Join('|', Enumerable.Repeat(new string('-', colWidth), colNames.Length - 1)));

                foreach (var kvp in grp)
                {
                    var stepInfos = kvp.Value;
                    var testId = kvp.Key;
                    var maxForce = stepInfos
                        .Skip(500)
                        .Max(si => si.Force);
                    var maxDeformation = stepInfos.Max(si => si.Deformation);
                    var timeAbove19kN = stepInfos.Count(si => si.Force > 19E3) * stepSize;
                    var timeAbove10kN = stepInfos.Count(si => si.Force > 10E3) * stepSize;
                    int spinalForceIdx = -1;
                    if (testId.ReportedParameters != null)
                        spinalForceIdx = Array.IndexOf(testId.ReportedParameters, "Max Spinal Force");
                    double? maxSpinalForce = null;
                    if (spinalForceIdx >= 0)
                        maxSpinalForce = stepInfos.Max(si => si.ReportedValues[spinalForceIdx]);
                    var gs = maxForce / 500;
                    var maxForceStr = $"{maxForce / 1E3:F1}/{gs:F1}";

                    var firstCol = outputGrouping == OutputGroupingType.Protector ? testId.DropHeightCm.ToString() : testId.ProtectorName;

                    ret.Append(
                        $"{firstCol,firstColWidth}|" +
                        $"{testId.ProtecteeName,colWidth}|" +
                        $"{maxForce / 1E3,colWidth:F1}|" +
                        $"{maxDeformation * 100,colWidth:F1}|");
                    if (summaryType == SummaryType.EN1651)
                        ret.Append(
                            $"{timeAbove19kN * 1000,colWidth:F1}|" +
                            $"{timeAbove10kN * 1000,colWidth:F1}|");
                    else
                        ret.Append(
                        $"{maxSpinalForce / 1E3,colWidth:F1}|");
                    ret.AppendLine();
                }
            }
            return ret.ToString();
        }

        private static string GetHeaderPrefix(List<OutputGroupingType> outputGrouping, TestIdentifier key)
        {
            if (outputGrouping == null)
                return string.Empty;
            var nonGrouped = Enum.GetValues(typeof(OutputGroupingType))
                .Cast<OutputGroupingType>()
                .Except(outputGrouping)
                .ToList();
            return GetGroupName(nonGrouped, key);

        }

        private static string GetGroupName(List<OutputGroupingType> outputGrouping, TestIdentifier key)
        {
            if (outputGrouping == null)
                return $"{key.ProtectorName} + {key.ProtecteeName} + {key.DropHeightCm}";
            return string.Join(' ', outputGrouping.Select(og =>
            {
                switch (og)
                {
                    case OutputGroupingType.DropHeight:
                        return key.DropHeightCm.ToString() + "cm";
                    case OutputGroupingType.Protector:
                        return key.ProtectorName;
                    case OutputGroupingType.Protectee:
                        return key.ProtecteeName;
                    default:
                        return og.ToString();
                }
            }));
        }

    }
}
