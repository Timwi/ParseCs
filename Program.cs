using System;
using System.Linq;
using System.IO;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace ParseCs
{
    public class Program
    {
        static void Main(string[] args)
        {
            foreach (var f in Directory.GetFiles(PathUtil.AppPathCombine(@"..\..\users\timwi\ParseCs"), "*.cs", SearchOption.AllDirectories))
            {
                CsDocument result;
                string source = File.ReadAllText(f);
                try
                {
                    var targetFile = f + ".fmt";
                    result = Parser.Parse(source);
                    File.WriteAllText(targetFile, result.ToString());
                    Console.WriteLine("{0} parsed successfully.".Fmt(f));
                }
                catch (ParseException e)
                {
                    if (e.IncompleteResult is CsDocument)
                    {
                        Console.ForegroundColor = ConsoleColor.Gray;
                        result = (CsDocument) e.IncompleteResult;
                        Console.WriteLine(result.ToString());
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine(new string('-', Console.WindowWidth - 1));

                        string beforeIndex = source.Substring(0, e.Index);
                        int newLinesBefore = beforeIndex.Count(ch => ch == '\n');
                        string cutSource = source;
                        int index = e.Index;
                        if (newLinesBefore > 3)
                        {
                            int cutOff = e.Index;
                            for (int i = 0; i < 3; i++)
                                cutOff = beforeIndex.LastIndexOf('\n', cutOff - 1);
                            cutSource = source.Substring(cutOff);
                            index -= cutOff;
                        }

                        int pos = cutSource.IndexOf('\n', index);
                        int posBef = cutSource.LastIndexOf('\n', index);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine(pos == -1 ? cutSource : cutSource.Substring(0, pos));
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        var indentation = new string(' ', index - posBef - 1);
                        Console.WriteLine(indentation + "^^^");
                        Console.WriteLine(indentation + e.Message);
                        Console.WriteLine(indentation + "(line " + (source.Substring(0, e.Index).Count(ch => ch == '\n') + 1) + " col " + (index - posBef - 1) + " ind " + e.Index + ")");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine(new string('-', Console.WindowWidth - 1));
                        Console.ReadLine();
                    }
                }
            }
        }
    }
}
