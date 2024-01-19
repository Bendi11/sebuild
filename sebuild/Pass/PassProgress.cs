
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
    int _progress, _total;
    Stopwatch _stopWatch;
    CancellationTokenSource? _tick = null;
    
    /// <summary>
    /// Progress display mode to be selected based on what information is available at the time of progress creation:
    /// A progress bar if total number of items is known, a number if operations can be counted, or nothing
    /// </summary>
    public enum Mode {
        NoProgress,
        Count,
        Bar,
    }

    /// <summary>
    /// The formatted message that will is currently displayed on the console
    /// </summary>
    public string? Message {
        get => _message;
        set => _message = value ?? $" - {value}";
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
        _total = 1;
        _message = null;
    }
    
    /// <summary>
    /// Add a progress bar displaying the current number of items out of <paramref name="totalItems"/>
    /// </summary>
    /// <param name="totalItems">Must be greater than 0</param>
    /// <exception cref="ArgumentException">When <paramref name="totalItems"/> is less than or equal to 0</exception>
    public void AddBar(int totalItems) {
        if(totalItems <= 0) {
            throw new ArgumentException($"Total items must be greater than 0");
        }

        _total = totalItems;
        _mode = Mode.Bar;
    }
    
    ///<summary>A string that can be used to clear a line of the console</summary>
    static readonly string CLEAR = new string(' ', Console.WindowWidth - 1);
    
    /// <summary>
    /// Report <paramref name="items"/> number of items complete, and display the updated progress information
    /// </summary>
    public void Report(int items) {
        _progress += items;
        Console.CursorVisible = false;
        ClearLine();


        switch(_mode) {
            case Mode.NoProgress:
                Console.Write($"⟳ {_tag} {Message}\r");
            break;

            case Mode.Count:
                Console.Write($"⟳ {_tag} [{_progress}] {Message}\r");
            break;

            case Mode.Bar:
                Console.Write($"⟳ {_tag} {Message}\r");
                Console.Write('\n');
                Console.Write($"PROGRESS");
            break;
        }
    }



    public void Dispose() {
        _stopWatch.Stop();
        if(_tick is not null) {
            _tick.Cancel(); 
        }
        var old = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;

        ClearLine();
        var total = _useNumbers ? $"- {_total}" : "";
        Console.WriteLine($"✓ {_tag} {total} ({_stopWatch.Elapsed.TotalSeconds:0.000})");

        Console.CursorVisible = true;
        Console.ForegroundColor = old;
    }

    private void ClearLine() {
        Console.SetCursorPosition(0, Console.GetCursorPosition().Top);
        Console.Write(CLEAR);
        Console.SetCursorPosition(0, Console.GetCursorPosition().Top);
    }
}
