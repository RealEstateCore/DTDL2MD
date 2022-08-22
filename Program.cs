using CommandLine;
using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.Azure.DigitalTwins.Parser.Models;

namespace DTDL2MD
{
    internal class Program
    {
        public class Options
        {
            [Option('i', "inputPath", Required = true, HelpText = "The path to the ontology root directory or file to translate.")]
            public string InputPath { get; set; } = "";
            [Option('o', "outputPath", Required = true, HelpText = "The path at which to put the generated OAS file.")]
            public string OutputPath { get; set; } = "";
        }

        // Configuration fields
        private static string _inputPath;
        private static string _outputPath;

        // Data fields
        private static IReadOnlyDictionary<Dtmi, DTEntityInfo> DTEntities;

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed(o =>
                   {
                       _inputPath = o.InputPath;
                       _outputPath = o.OutputPath;
                   })
                   .WithNotParsed((errs) =>
                   {
                       Environment.Exit(1);
                   });

            LoadInput();

            foreach (DTInterfaceInfo iface in DTEntities.Values.Where(entity => entity is DTInterfaceInfo))
            {
                List<string> output = new List<string>();
                
                string ifaceName = GetApiName(iface);

                output.Add($"# {ifaceName}");
                output.Add($"**DTMI:** {iface.Id}");
                output.Add("## Description");
                foreach ((string lang, string desc) in iface.Description)
                {
                    output.Add($"- {lang}");
                    output.Add($"    - {desc}");
                }
                output.Add("## Telemetries");
                output.Add("## Commands");
                output.Add("## Properties");
                output.Add("## Relationships");

                string path = _outputPath + GetPath(iface);

                File.WriteAllLines(path, output);
            }

        }

        private static string GetPath(DTInterfaceInfo iface)
        {
            // TODO Implement me
            string ifaceName = GetApiName(iface);
            return "\\" + ifaceName + ".md";
        }

        private static string GetApiName(DTEntityInfo entityInfo)
        {
            if (entityInfo is DTNamedEntityInfo)
                return ((DTNamedEntityInfo)entityInfo).Name;

            string versionLessDtmi = entityInfo.Id.Versionless;
            string entityNamespace = versionLessDtmi.Substring(0, versionLessDtmi.LastIndexOf(':'));
            string entityId = versionLessDtmi.Substring(versionLessDtmi.LastIndexOf(':') + 1);

            return versionLessDtmi.Split(':').Last();
        }

        // Load a file or a directory of files from disk
        private static void LoadInput()
        {
            // Get selected file or, if directory selected, all JSON files in selected dir
            IEnumerable<FileInfo> sourceFiles;
            if (File.GetAttributes(_inputPath) == FileAttributes.Directory)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(_inputPath);
                sourceFiles = directoryInfo.EnumerateFiles("*.json", SearchOption.AllDirectories);
            }
            else
            {
                FileInfo singleSourceFile = new FileInfo(_inputPath);
                sourceFiles = new[] { singleSourceFile };
            }


            List<string> modelJson = new List<string>();
            foreach (FileInfo file in sourceFiles)
            {
                using StreamReader modelReader = new StreamReader(file.FullName);
                modelJson.Add(modelReader.ReadToEnd());
            }
            ModelParser modelParser = new ModelParser(0);

            try
            {
                DTEntities = modelParser.Parse(modelJson);
            }
            catch (ParsingException parserEx)
            {
                Console.Error.WriteLine(parserEx.Message);
                Console.Error.WriteLine(string.Join("\n\n", parserEx.Errors.Select(error => error.Message)));
                Environment.Exit(1);
            }
        }
    }
}