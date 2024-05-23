
namespace SeBuild;

/// <summary>
/// File paths for Space Engineers' game binary folder and appdata folder used to add references to scripting
/// DLLs and to export completed scripts to.
public struct Paths {
    public string SEBinPath { get; private set; }
    public string SEAppDataPath { get; private set; }

    public static readonly string SpaceEngineersBinVar = "SpaceEngineersBin";
    public static readonly string SpaceEngineersAppDataVar = "SpaceEngineersAppData";

    public string SpaceEngineersScriptDir {
        get => Path.Combine(SEAppDataPath, "IngameScripts/local");
    }

    public string DigiAutoReloadScriptPath {
        get => Path.Combine(SEAppDataPath, "2999829512.sbm_PBQuickLoad");
    }


    public Paths(string? seBinPath, string? seAppDataPath = null) {
        SEBinPath = seBinPath is null ?
            Environment.GetEnvironmentVariable(SpaceEngineersBinVar) ??
                throw new Exception($"Failed to locate SE binary folder: no {SpaceEngineersBinVar} environment variable") :
            seBinPath;

        seAppDataPath = seAppDataPath is null ?
            Environment.GetEnvironmentVariable(SpaceEngineersAppDataVar) ??
                throw new Exception($"Failed to locate SE appdata folder: no {SpaceEngineersAppDataVar} environment variable") :
            seAppDataPath;
    }

}
