
using System.Diagnostics;


namespace SeBuild;

/// <summary>
/// Implementation of <c>IProgress</c> intended for use by <c>CompilationPass</c> instances.
/// Writes formatted messages to the console while ticking a console spinner. When disposed, 
/// displays a message including recorded time of the operation
/// </summary>
public class PassProgress: IProgress<int>, IDisposable {
    string _tag;
    Mode _mode;
    
    //The actual message with additional formatting added
    string? _message;
    int _progress;
    Stopwatch _stopWatch;
    byte _ticker;
    
    /// <summary>
    /// Progress display mode to be selected based on what information is available at the time of progress creation:
    /// A number if operations can be counted, or nothing
    /// </summary>
    public enum Mode {
        NoProgress,
        Count,
    }

    /// <summary>
    /// Create a new progress reporter with a <paramref name="name"/> to be displayed alongside a message,
    /// and a <paramref name="mode"/> of displaying progress
    /// </summary>
    /// <param name="mode">Mode to display progress on the console</param>
    /// <param name="name">Title of the ongoing process</param>
    public PassProgress(string name, Mode mode = Mode.Count) {
        _tag = name;
        _stopWatch = new Stopwatch();
        _stopWatch.Start();
        _progress = 0;
        _ticker = 0;
        _message = null;
        _mode = mode;
    }
    
    ///<summary>A string that can be used to clear a line of the console</summary>
    static readonly string CLEAR = new string(' ', Console.WindowWidth - 1);
    
    /// <summary>Spinner characters to tick through
    static readonly char[] SPINNER = { '|', '/', '-', '\\' };
    
    /// <summary>
    /// Report <paramref name="items"/> number of items complete, and display the updated progress information
    /// </summary>
    public void Report(int items) {
        _progress += items;
        _ticker = (_ticker >= 2) ? (byte)0 : (byte)(_ticker + 1);
        Console.CursorVisible = false;
        ClearLine();

        switch(_mode) {
            case Mode.NoProgress:
                Console.Write($"{SPINNER[_ticker]} {_tag}\r");
            break;

            case Mode.Count:
                Console.Write($"{SPINNER[_ticker]} {_tag} [{_progress}]\r");
            break;
        }

        Console.Out.Flush();
    }



    public void Dispose() {
        _stopWatch.Stop();
        var old = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        
        ClearLine();
        /*Console.Write($"✓ {_tag}");
        if(_mode == Mode.Count) {
            Console.Write($" [{_progress}]");
        }

        Console.Write($" - {_stopWatch.Elapsed.TotalSeconds:0.00}s");*/

        Console.CursorVisible = true;
        Console.ForegroundColor = old;
    }

    private void ClearLine() {
        Console.SetCursorPosition(0, Console.GetCursorPosition().Top);
        Console.Write(CLEAR);
        Console.SetCursorPosition(0, Console.GetCursorPosition().Top);
    }
}
