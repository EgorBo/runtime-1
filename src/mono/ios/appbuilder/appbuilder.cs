using System;
using System.IO;
using System.Text;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length != 2)
            throw new Exception("usage: appbuilder [folderWithObjFiles] [outputMFile]");

        string inputFolder = args[0];
        string outputFile = args[1];
        string[] objFiles = Directory.GetFiles(inputFolder, "*.dll.o");

        var lsDecl = new StringBuilder();
        lsDecl
            .AppendLine("#include <mono/jit/jit.h>")
            .AppendLine();

        var lsUsage = new StringBuilder();
        lsUsage
            .AppendLine("void mono_ios_register_modules (void)")
            .AppendLine("{");
        foreach (string objFile in objFiles)
        {
            string symbol = "mono_aot_module_" +
                Path.GetFileName(objFile)
                    .Replace(".dll.o", "")
                    .Replace(".", "_")
                    .Replace("-", "_") + "_info";

            lsDecl.Append("extern void *").Append(symbol).Append(';').AppendLine();
            lsUsage.Append("\tmono_aot_register_module (").Append(symbol).Append(");").AppendLine();
        }
        lsDecl.AppendLine().Append(lsUsage).AppendLine("}").AppendLine();
        File.WriteAllText(outputFile, lsDecl.ToString());
    }
}
