using System;
using System.IO;
using System.Linq;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.KitchenSink.ParseCs;

namespace ParseCs
{
    public class Program
    {
        static void Main(string[] args)
        {
            var brokenfiles = new string[] { 
                @"C:\c\users\rs\ConwayLifesaver\ConwaySaver.cs",
                @"C:\c\main\common\thirdparty\TaoFramework-2.1.0\source\src\Tao.FFmpeg\AVFormat.cs",
                @"C:\c\main\common\thirdparty\SubSonic-3.0-e0e4ba2\SubSonic.Core\Linq\Translation\SQLite\SQLiteFormatter.cs",
                @"C:\c\main\common\thirdparty\SubSonic-3.0-e0e4ba2\SubSonic.Core\Linq\Translation\MySQL\MySqlFormatter.cs",
                @"C:\c\main\common\thirdparty\SharpZipLib\AssemblyInfo.cs",
            };

            var files = Enumerable.Empty<string>()
                // .Concat(Directory.GetFiles(@"C:\c", "*.cs", SearchOption.AllDirectories))
                // .Concat(Directory.GetFiles(@"C:\c\builds\Release-AnyCPU", "*.cs", SearchOption.AllDirectories))
                // .Concat(brokenfiles)
                .Concat(@"C:\c\main\ExpSok\Translation.cs")
                // .Concat(Directory.GetFiles(@"C:\c\users\timwi\ParseCs", "*.cs", SearchOption.AllDirectories))
                // .Except(brokenfiles)
                ;

            // var x = brokenfiles.Where(b => b.StartsWith(@"C:\c\builds\Debug-AnyCPU\")).ToArray();
            // files = x.SelectMany(b => Directory.GetFiles(@"C:\c", b.Substring(@"C:\c\builds\Debug-AnyCPU\".Length, b.Length - @"C:\c\builds\Debug-AnyCPU\.fmt.cs".Length) + ".cs", SearchOption.AllDirectories)).ToArray();

            TimeSpan totalTaken = TimeSpan.FromMilliseconds(0);

            foreach (var f in files)
            {
                if (f.EndsWith(".fmt.fmt.cs"))
                    continue;

                string source = File.ReadAllText(f);
                try
                {
                    var targetFile = PathUtil.AppPathCombine(Path.GetFileNameWithoutExtension(f) + ".fmt.cs");
                    Console.Write(f);
                    var start = DateTime.Now;
                    var result = Parser.Parse(source);
                    var taken = DateTime.Now - start;
                    File.WriteAllText(targetFile, result.ToString());
                    Console.WriteLine(" parsed successfully. ({1} bytes, {2} sec)".Fmt(f, new FileInfo(f).Length, taken.TotalSeconds));
                    totalTaken += taken;
                }
                catch (ParseException e)
                {
                    Console.WriteLine(": parse error.".Fmt(f));
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
                    int posBef = index == 0 ? -1 : cutSource.LastIndexOf('\n', index - 1);
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

            Console.WriteLine("Finished. Total time spent parsing: {0} sec".Fmt(totalTaken.TotalSeconds));
            Console.ReadLine();
        }
    }
}
