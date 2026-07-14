var mode = Environment.GetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_MODE") ?? "normal";
var commandLog = Environment.GetEnvironmentVariable("BLUNDERFORGE_FAKE_STOCKFISH_COMMAND_LOG");

if (mode.Equals("exit", StringComparison.OrdinalIgnoreCase))
{
    return 42;
}

while (await Console.In.ReadLineAsync() is { } line)
{
    if (!string.IsNullOrWhiteSpace(commandLog))
    {
        await File.AppendAllTextAsync(commandLog, line + Environment.NewLine);
    }
    if (line == "uci")
    {
        Console.WriteLine("id name Fakefish 1");
        Console.WriteLine("uciok");
    }
    else if (line == "isready")
    {
        Console.WriteLine("readyok");
    }
    else if (line.StartsWith("go ", StringComparison.Ordinal))
    {
        if (mode.Equals("protocol", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("bestmove invalid");
        }
        else
        {
            Console.WriteLine("info depth 1 multipv 1 score cp 42 pv e2e4 e7e5");
            Console.WriteLine("bestmove e2e4");
        }
    }
    else if (line == "quit")
    {
        return 0;
    }
}

return 0;
