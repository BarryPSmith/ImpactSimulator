using ImpactSimulator.Protectors;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImpactSimulator
{
    public enum ProtectorType 
    { 
        SimpleSpring, 
        ConstantForce,
        CompositeParallel, 
        AdiabaticPiston, 
        BaffledAirProtector,
        Aerobean,
        Liquid,
    }
    public enum ProtecteeType { Rigid, VanToenTrunk }

    public enum OutputGroupingType { Protector, Protectee, DropHeight }
    
    public enum SummaryType { EN1651, HumanImpact }

    public class ModelConfig
    {
        public string Name { get; set; }
        public ProtectorType ModelType { get; set; }
        public JObject Config { get; set; }
        public bool? Include { get; set; }
        public int ModelSet { get; set; } = 0;

        static Dictionary<ProtectorType, Type> _protectorTypeMap = new Dictionary<ProtectorType, Type>
        {
            { ProtectorType.AdiabaticPiston, typeof(AdiabaticPiston) },
            { ProtectorType.BaffledAirProtector, typeof(BaffledAirProtector) },
            { ProtectorType.Aerobean, typeof(Aerobean) },
            { ProtectorType.SimpleSpring, typeof(SimpleSpring) },
            { ProtectorType.ConstantForce, typeof(ConstantForce) },
            { ProtectorType.Liquid, typeof(LiquidProtector) },
        };
        public IProtector GetModel()
            => GetModel(ModelType, Config);

        public static IProtector GetModel(ProtectorType protectorType, JObject Config)
        {
            if (_protectorTypeMap.TryGetValue(protectorType, out var type))
                return (IProtector)Config.ToObject(type);
            if (protectorType != ProtectorType.CompositeParallel)
                throw new Exception("Unrecognised protector type.");
            return new CompositeParallel()
            {
                Protectors = Config[nameof(CompositeParallel.Protectors)]
                    .Select(jo => jo.ToObject<ModelConfig>().GetModel())
                    .ToList()
            };
        }
    }

    class Config
    {
        public List<int> SetsToModel { get; set; } = new List<int> { 0 };
        public double StepSize { get; set; } = 1E-6;
        public int Steps { get; set; } = 200000;
        public bool WriteTraces { get; set; } = false;
        public List<double> DropHeights { get; set; }
        public List<ModelConfig> ProtectorModels { get; set; }
        public List<ProtecteeType> ProtecteeModels { get; set; }
        public List<double> RigidMasses { get; set; } = new List<double>() { 50 };
        public List<OutputGroupingType> OutputGrouping { get; set; }
        public List<string> ColumnFilters { get; set; }
        public bool DefaultInclude { get; set; } = false;
        public SummaryType SummaryType { get; set; } = SummaryType.EN1651;
    }
}
