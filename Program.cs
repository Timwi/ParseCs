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
            string path = @"..\..\main\common\thirdparty";
            // string path = @"..\..\users\timwi\ParseCs";
            foreach (var f in Directory.GetFiles(PathUtil.AppPathCombine(path), "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(f);
                try
                {
                    var targetFile = PathUtil.AppPathCombine(Path.GetFileNameWithoutExtension(f) + ".fmt.cs");
                    var start = DateTime.Now;
                    var result = Parser.Parse(source);
                    var taken = DateTime.Now - start;
                    File.WriteAllText(targetFile, result.ToString());
                    Console.WriteLine("{0} parsed successfully. ({1} bytes, {2} sec)".Fmt(f, new FileInfo(f).Length, taken.TotalSeconds));
                }
                catch (ParseException e)
                {
                    Console.WriteLine("{0}: parse error.".Fmt(f));
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(new string('-', Console.WindowWidth - 1));

                    string beforeIndex = source.Substring(0, e.Index);
                    int newLinesBefore = beforeIndex.Count(ch => ch == '\n');
                    string cutSource = source;
                    int index = e.Index;
                    if (newLinesBefore > 5)
                    {
                        int cutOff = e.Index;
                        for (int i = 0; i < 5; i++)
                            cutOff = beforeIndex.LastIndexOf('\n', cutOff - 1);
                        cutSource = source.Substring(cutOff);
                        index -= cutOff;
                    }

                    int pos = cutSource.IndexOf('\n', index);
                    int posBef = cutSource.LastIndexOf('\n', index - 1);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine(pos == -1 ? cutSource : cutSource.Substring(0, pos));
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    var indentation = new string(' ', index - posBef - 1);
                    Console.WriteLine(indentation + "^^^");
                    Console.WriteLine(indentation + e.Message);
                    Console.WriteLine(indentation + "(line " + (source.Substring(0, e.Index).Count(ch => ch == '\n') + 1) + " col " + (index - posBef - 1) + " ind " + e.Index + ")");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(new string('-', Console.WindowWidth - 1));
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
            }
            Console.WriteLine("Finished.");
            Console.ReadLine();
        }
    }
}
