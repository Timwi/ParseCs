using System;
using System.IO;
using System.Linq;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace ParseCs
{
    public class Program
    {
        static void Main(string[] args)
        {
            var brokenfiles = new string[] { @"C:\c\users\timwi\Nest\NestServer.cs", @"C:\c\users\rs\i4c\Compressor.cs", @"C:\c\users\rs\i4c\IntField.cs", @"C:\c\users\rs\HobbyPort\Oscilloscope.cs", @"C:\c\users\rs\Grevolution\Samples\Sample1\WorldPyramid.cs", @"C:\c\users\rs\ConwayLifesaver\ConwayField.cs", @"C:\c\users\rs\ConwayLifesaver\ConwaySaver.cs", @"C:\c\users\rs\AstroEmperor\Settings.cs", @"C:\c\users\rs\AssemblyEdit\MainForm.cs", @"C:\c\main\Wheels\WheelsCli\WheelClient.cs", @"C:\c\main\ProcessTraffic\MainForm.cs", @"C:\c\main\GammaTool\MainForm.cs", @"C:\c\main\FingerGym\Layouts\physical.UK-Desktop-Standard.cs", @"C:\c\main\common\thirdparty\TaoFramework-2.1.0\source\src\Tao.Platform.Windows\WglDelegates.cs", @"C:\c\main\common\thirdparty\TaoFramework-2.1.0\source\src\Tao.OpenGl\GLDelegates.cs", @"C:\c\main\common\thirdparty\TaoFramework-2.1.0\source\src\Tao.FFmpeg\AVFormat.cs", @"C:\c\main\common\thirdparty\TaoFramework-2.1.0\source\examples\TaoMediaPlayer\MainForm.cs", @"C:\c\main\common\thirdparty\TaoFramework-2.1.0\source\examples\Redbook\Font.cs", @"C:\c\main\common\thirdparty\SubSonic-3.0-e0e4ba2\SubSonic.Core\Schema\DatabaseColumn.cs", @"C:\c\main\common\thirdparty\SubSonic-3.0-e0e4ba2\SubSonic.Core\Schema\StoredProcedure.cs", @"C:\c\main\common\thirdparty\SubSonic-3.0-e0e4ba2\SubSonic.Core\Query\Aggregate.cs", @"C:\c\main\common\thirdparty\SubSonic-3.0-e0e4ba2\SubSonic.Core\Linq\Translation\QueryBinder.cs", @"C:\c\main\common\thirdparty\SubSonic-3.0-e0e4ba2\SubSonic.Core\Linq\Translation\SQLite\SQLiteFormatter.cs", @"C:\c\main\common\thirdparty\SubSonic-3.0-e0e4ba2\SubSonic.Core\Linq\Translation\MySQL\MySqlFormatter.cs", @"C:\c\main\common\thirdparty\SubSonic-3.0-e0e4ba2\SubSonic.Core\Linq\Structure\ExecutionBuilder.cs", @"C:\c\main\common\thirdparty\SubSonic-3.0-e0e4ba2\SubSonic.Core\Linq\Structure\TSqlFormatter.cs", @"C:\c\main\common\thirdparty\SubSonic-3.0-e0e4ba2\SubSonic.Core\Extensions\Strings.cs", @"C:\c\main\common\thirdparty\SubSonic-3.0-e0e4ba2\SubSonic.Core\DataProviders\DbDataProvider.cs", @"C:\c\main\common\thirdparty\SharpZipLib\AssemblyInfo.cs", @"C:\c\main\common\thirdparty\SharpZipLib\Zip\ZipFile.cs", @"C:\c\main\common\thirdparty\SharpZipLib\Zip\ZipOutputStream.cs", @"C:\c\main\common\thirdparty\SharpZipLib\Zip\Compression\Inflater.cs", @"C:\c\main\common\thirdparty\SharpZipLib\Zip\Compression\InflaterDynHeader.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\Mono.Xml\MiniParser.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\CodeGen\templates\BaseMetadataVisitor.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\CodeGen\templates\Code.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\CodeGen\templates\CodedIndex.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\CodeGen\templates\IMetadataVisitor.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\CodeGen\templates\IndexedCollection.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\CodeGen\templates\MetadataRowReader.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\CodeGen\templates\MetadataRowWriter.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\CodeGen\templates\MetadataTableReader.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\CodeGen\templates\MetadataTableWriter.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\CodeGen\templates\NamedCollection.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\CodeGen\templates\OpCodes.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\CodeGen\templates\Table.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\CodeGen\templates\Tests.cs", @"C:\c\main\common\thirdparty\Mono.Cecil\CodeGen\templates\Utilities.cs", @"C:\c\main\common\thirdparty\DynamicQuery\Dynamic.cs", @"C:\c\main\common\RummageLib\RummageAssembly.cs" };

            var files = Enumerable.Empty<string>()
                // .Concat(Directory.GetFiles(@"C:\c", "*.cs", SearchOption.AllDirectories))
                // .Concat(Directory.GetFiles(PathUtil.AppPathCombine(@"..\..\users\timwi\ParseCs"), "*.cs", SearchOption.AllDirectories))
                // .Concat(Directory.GetFiles(PathUtil.AppPathCombine(@"..\..\main\common\Util"), "*.cs", SearchOption.AllDirectories))
                // .Concat(@"C:\c\builds\Debug-AnyCPU\AudioSource.fmt.cs")
                .Concat(brokenfiles)
                ;

            // var x = brokenfiles.Where(b => b.StartsWith(@"C:\c\builds\Debug-AnyCPU\")).ToArray();
            // files = x.SelectMany(b => Directory.GetFiles(@"C:\c", b.Substring(@"C:\c\builds\Debug-AnyCPU\".Length, b.Length - @"C:\c\builds\Debug-AnyCPU\.fmt.cs".Length) + ".cs", SearchOption.AllDirectories)).ToArray();

            foreach (var f in files)
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
            Console.WriteLine("Finished.");
            Console.ReadLine();
        }
    }
}
