using CommandLine;
using CSharpMinifier;
using System.Text;
namespace SeBuild;

internal class Program {
    static async Task Main(string[] args) {
        await Parser
            .Default
            .ParseArguments<BuildArgs>(args)
            .MapResult(
                async (BuildArgs build) => {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var paths = new Paths(build.SpaceEngineersBinPath, build.SpaceEngineersAppDataPath);
                    var wsBuild = new WorkspaceBuilder(paths);
                    
                    ScriptCommon ctx = await wsBuild.CreateFromMSBuild(build.Project);
                    
                    //Get total characters in project pre-compilation to display size reduction metrics after compilation
                    ulong initialChars = 0;
                    foreach(var doc in ctx.DocumentsIter) {
                        if(doc.FilePath is not null) {
                            try {
                                Console.WriteLine(doc.FilePath);
                                FileInfo fi = new FileInfo(doc.FilePath);
                                initialChars += (ulong)fi.Length;
                            } catch(Exception) {

                            }
                        }
                    }
                    
                    var scriptBuilder = new ScriptBuilder(ctx);
                    var syntax = await scriptBuilder.BuildProject(build);
                    
                    string outputPath;
                    
                    if(build.Output == null) {
                        if(build.AutoReload) {
                            string scriptDir = paths.DigiAutoReloadScriptPath;
                            if(!Path.Exists(scriptDir)) {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"Digi's Auto-Reload mod folder does not exist - creating it");
                                Directory.CreateDirectory(scriptDir);
                            }
                            outputPath = Path.Combine(scriptDir, $"{build.Project}.cs");
                        } else {
                            string scriptDir = paths.SpaceEngineersScriptDir;
                            outputPath = Path.Combine(scriptDir, ctx.Project.Name);
                            Directory.CreateDirectory(outputPath);
                            outputPath = Path.Combine(outputPath, "Script.cs");
                        }
                    } else {
                        outputPath = build.Output;
                    }
                    
                    StringBuilder sb = new StringBuilder();
                    foreach(var decl in syntax) { sb.Append(decl.GetText()); }
                    
                    string output = sb.ToString();
                    using var file = new StreamWriter(File.Create(outputPath), Encoding.UTF8, 65536);
                
                    long len = 0;
                    if(build.Minify) {
                        foreach(var tok in Minifier.Minify(output)) {
                            file.Write(tok);
                            len += tok.Length;
                        }
                    } else {
                        len = output.Length;
                        file.Write(output);
                    }

                    sw.Stop();
 
                    Console.Write($"{outputPath} -");

                    var reduction = ((double)initialChars - (double)len) / (double)initialChars;
                    var color = reduction switch {
                        <= 0.1 => ConsoleColor.DarkGray,
                        <= 0.5 => ConsoleColor.DarkYellow,
                        <= 0.6 => ConsoleColor.Yellow,
                        <= 0.8 => ConsoleColor.DarkGreen,
                        var _ => ConsoleColor.Green,
                    };

                    Console.Write($" ({len:0,0} characters - ");
                    Console.ForegroundColor = color;
                    Console.Write($"{reduction * 100.0:0.00}");
                    Console.ResetColor();
                    Console.WriteLine($"%)({sw.Elapsed.TotalSeconds:0.000} s)");
                },
                async (errs) => await Task.Run(() => {
                    foreach(var err in errs) {
                        if(err is not CommandLine.HelpRequestedError) {
                            Console.WriteLine(err);
                        }
                    }
                    
                    return 1;
                })
            );
    }
}
