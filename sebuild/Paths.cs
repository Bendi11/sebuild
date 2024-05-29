
namespace SeBuild;

/// <summary>
/// File paths for Space Engineers' game binary folder and appdata folder used to add references to scripting
/// DLLs and to export completed scripts to.
public struct Paths {
    /// <summary>
    /// Path to the space engineers scripting library files, usually retrieved from the
    /// SpaceEngineersBinVar environment variable
    /// </summary>
    public string SEBinPath { get; private set; }
    
    /// <summary>
    /// Path to Space Engineer's local AppData folder, where script folders are meant to be placed and where
    /// Digi's auto-reload folder is located if the mod is installed
    /// </summary>
    public string SEAppDataPath { get; private set; }
    
    /// <summary>
    /// Name of the environment variable containing the path to space engineers' library files.
    /// </summary>
    public static readonly string SpaceEngineersBinVar = "MDKGameBinPath";
    public static readonly string SpaceEngineersAppDataVar = "SpaceEngineersAppData";
    
    /// <summary>
    /// Directory where space engineers script directories should be placed to appear ingame
    /// </summary>
    public string SpaceEngineersScriptDir {
        get => Path.Combine(SEAppDataPath, "IngameScripts/local");
    }
    
    /// <summary>
    /// DIrectory where compressed script files should be written in order to appear in Digi's Auto Reload mod
    /// </summary>
    public string DigiAutoReloadScriptPath {
        get => Path.Combine(SEAppDataPath, "2999829512.sbm_PBQuickLoad");
    }

    /// <summary>
    /// Initialize all paths using the provided optional values,
    /// or attempt to fetch them from environment variables.
    /// </summary>
    public Paths(string? seBinPath, string? seAppDataPath = null) {
        SEBinPath = seBinPath is null ?
            Environment.GetEnvironmentVariable(SpaceEngineersBinVar) ??
                throw new Exception($"Failed to locate SE binary folder: no {SpaceEngineersBinVar} environment variable") :
            seBinPath;

        SEAppDataPath = seAppDataPath is null ?
            Environment.GetEnvironmentVariable(SpaceEngineersAppDataVar) ??
                throw new Exception($"Failed to locate SE appdata folder: no {SpaceEngineersAppDataVar} environment variable") :
            seAppDataPath;
    }

}
