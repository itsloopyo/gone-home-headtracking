using System;
using System.IO;

public static class PatcherMain
{
    public static int Main(string[] args)
    {
        // Supported forms:
        //   BootstrapPatcher.exe <in> <out>           -> patch   (back-compat)
        //   BootstrapPatcher.exe patch   <in> <out>   -> patch
        //   BootstrapPatcher.exe unpatch <in> <out>   -> unpatch (reverse)
        string verb;
        string input;
        string output;

        if (args.Length == 2)
        {
            verb = "patch";
            input = args[0];
            output = args[1];
        }
        else if (args.Length == 3)
        {
            verb = args[0].ToLowerInvariant();
            input = args[1];
            output = args[2];
        }
        else
        {
            Console.Error.WriteLine("usage: BootstrapPatcher.exe [patch|unpatch] <input-assembly> <output-assembly>");
            return 2;
        }

        File.Copy(input, output, true);

        switch (verb)
        {
            case "patch":
                return BootstrapPatcher.PatchAssembly(output) ? 0 : 1;
            case "unpatch":
                return BootstrapPatcher.UnpatchAssembly(output) ? 0 : 1;
            default:
                Console.Error.WriteLine("unknown verb '" + verb + "' (expected patch or unpatch)");
                return 2;
        }
    }
}
