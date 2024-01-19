
namespace SeBuild;

public abstract class CompilationPass {
    public ScriptCommon Common;
    public PassProgress? Progress = null;

    protected void Tick() {
        if(Progress is not null) {
            Progress.Report(1);
        }
    }

    protected void Msg(string message) {
        if(Progress is not null) {
            Progress.Message = message;
        }
    }

    public CompilationPass(ScriptCommon ctx, PassProgress? progress) {
        Common = ctx;
        Progress = progress;
    }
    
    /// Execute the pass on the loaded documents, potentially replacing `Solution` with a new solution
    public abstract Task Execute();
}
