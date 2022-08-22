﻿using CommandLine;
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

                string interfaceMarkdownPath = GetPath(iface);

                File.WriteAllLines(interfaceMarkdownPath, output);
            }

        }

        private static string GetPath(DTInterfaceInfo iface)
        {
            string ifaceName = GetApiName(iface);
            List<DTInterfaceInfo> parentDirectories = GetLongestParentPath(iface);
            string modelPath = string.Join("/", parentDirectories.Select(parent => GetApiName(parent)));
            string modelOutputPath = $"{_outputPath}/{modelPath}/";

            if (DTEntities.ChildrenOf(iface).Any()) { modelOutputPath += $"{ifaceName}/"; }
            Directory.CreateDirectory(modelOutputPath);
            string outputFileName = modelOutputPath + ifaceName + ".md";

            return outputFileName;
        }

        private static List<DTInterfaceInfo> GetLongestParentPath(DTInterfaceInfo iface)
        {
            // If we have no superclass, then we have reached the top level; return
            if (iface.Extends.Count < 1)
            {
                return new List<DTInterfaceInfo>();
            }
            else
            {
                // Assume the first parent has the longest path; if not, it will be replaced in subsequent foreach
                DTInterfaceInfo longestParent = iface.Extends[0];
                List<DTInterfaceInfo> longestParentPath = GetLongestParentPath(longestParent);

                // Iterate through the other parents to see if any is longer
                foreach (DTInterfaceInfo possibleSuperClass in iface.Extends.Skip(1))
                {
                    List<DTInterfaceInfo> possibleSuperClassParents = GetLongestParentPath(possibleSuperClass);
                    if (possibleSuperClassParents.Count > longestParentPath.Count)
                    {
                        longestParent = possibleSuperClass;
                        longestParentPath = possibleSuperClassParents;
                    }
                }

                // At this point longestParentPath + longestParent should together contain the longest path to the root; return them
                longestParentPath.Add(longestParent);
                return longestParentPath;
            }
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