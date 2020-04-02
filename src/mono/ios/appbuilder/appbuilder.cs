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

        //  Generate 'modules.m' in order to register all managed libraries
        //
        //
        // extern void *mono_aot_module_Lib1_info;
        // extern void *mono_aot_module_Lib2_info;
        // ...
        //
        // void mono_ios_register_modules (void)
        // {
        //     mono_aot_register_module (mono_aot_module_Lib1_info);
        //     mono_aot_register_module (mono_aot_module_Lib2_info);
        //     ...
        // }

        var lsDecl = new StringBuilder();
        lsDecl
            .AppendLine("#include <mono/jit/jit.h>")
            .AppendLine()
            .AppendLine("#ifdef DEVICE")
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
        lsDecl
            .AppendLine()
            .Append(lsUsage)
            .AppendLine("}")
            .AppendLine()
            .AppendLine("#endif")
            .AppendLine();

        File.WriteAllText(outputFile, lsDecl.ToString());
    }
}
