using MidiSynth;
using SharpHdl.Emit;

string path = args.Length > 0 ? args[0] : Path.Combine("build", $"{nameof(TangPrimer25KTop)}.v");

TangPrimer25KTop top = new();
top.Describe();
string verilog = VerilogEmitter.Emitter(top, nameof(TangPrimer25KTop));

string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
if (directory is not null)
{
	Directory.CreateDirectory(directory);
}
File.WriteAllText(path, verilog);
Console.WriteLine(path);
