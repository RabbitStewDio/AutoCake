using System.Collections.Generic;

public class AnyUnitTestSettings
{
    public AnyUnitTestSettings()
    {
        IncludedTraits = new Dictionary<string, List<string>>();
        ExcludedTraits = new Dictionary<string, List<string>>();
    }

    public bool ForceX86 { get; set; }

    public Dictionary<string, List<string>> IncludedTraits { get; private set; }
    public Dictionary<string, List<string>> ExcludedTraits { get; private set; }

    public List<string> IncludedCategories
    {
        get
        {
            List<string> result;
            if (IncludedTraits.TryGetValue("Category", out result))
                return result;
            result = new List<string>();
            IncludedTraits["Category"] = result;
            return result;
        }
    }

    public List<string> ExcludedCategories
    {
        get
        {
            List<string> result;
            if (ExcludedTraits.TryGetValue("Category", out result))
                return result;
            result = new List<string>();
            ExcludedTraits["Category"] = result;
            return result;
        }
    }

    public bool ShadowCopyAssemblies { get; set; }
    public bool UseSingleThreadedApartment { get; set; }
}