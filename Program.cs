using CommandLine;
using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.Azure.DigitalTwins.Parser.Models;
using System.Xml.Linq;

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
        private static string inputRoot;
        private static string outputRoot;

        // Data fields
        private static IReadOnlyDictionary<Dtmi, DTEntityInfo> ontology;

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed(o =>
                   {
                       inputRoot = o.InputPath;
                       outputRoot = o.OutputPath;
                   })
                   .WithNotParsed((errs) =>
                   {
                       Environment.Exit(1);
                   });

            if (!(Directory.Exists(inputRoot) && Directory.Exists(outputRoot))) 
            {
                Console.Error.WriteLine("Input and/or output paths do not exist.");
                Environment.Exit(1); 
            }
            LoadInput();

            foreach (DTInterfaceInfo iface in ontology.Values.Where(entity => entity is DTInterfaceInfo))
            {
                List<string> output = new List<string>();
                
                string ifaceName = GetApiName(iface);

                output.Add($"# {ifaceName}");
                output.Add($"**DTMI:** {iface.Id}");

                output.Add("## Display name");
                foreach ((string lang, string dname) in iface.DisplayName)
                {
                    output.Add($"- **{lang}:** {dname}");
                }

                output.Add("## Description");
                foreach ((string lang, string desc) in iface.Description)
                {
                    output.Add($"- **{lang}:** {desc}");
                }

                if (iface.AllRelationships().Any()) {
                    output.Add("## Relationships");
                    if (iface.DirectRelationships().Count() > 0) { 
                        output.Add("|Name|Display name|Description|Multiplicity|Target|Properties|Writable|");
                        output.Add("|-|-|-|-|-|-|-|");
                    }
                    foreach (DTRelationshipInfo relationship in iface.DirectRelationships())
                    {
                        string name = relationship.Name;
                        string dname = string.Join("<br />", relationship.DisplayName.Select(kvp => $"**{kvp.Key}**: {kvp.Value}"));
                        string desc = string.Join("<br />", relationship.Description.Select(kvp => $"**{kvp.Key}**: {kvp.Value}"));
                        string min = relationship.MinMultiplicity.HasValue ? relationship.MinMultiplicity.Value.ToString() : "0";
                        string max = relationship.MaxMultiplicity.HasValue ? relationship.MaxMultiplicity.Value.ToString() : "Infinity";
                        string multiplicity = $"{min}-{max}";
                        string target = relationship.Target == null ? "" : relationship.Target.ToString();
                        string props = string.Join("<br>", relationship.Properties.Select(prop => $"{prop.Name} (schema: TBD)")); // TODO: Property schema translation, implement for property display and borrow
                        bool writable = relationship.Writable;
                        output.Add($"|{name}|{dname}|{desc}|{multiplicity}|{target}|{props}|{writable}|");
                    }
                    if (iface.InheritedRelationships().Any())
                    {
                        output.Add("### Inherited Relationships");
                        foreach (Dtmi parent in iface.InheritedRelationships().Select(ir => ir.DefinedIn).Distinct())
                        {
                            string relationships = string.Join(", ", iface.InheritedRelationships().Where(ir => ir.DefinedIn == parent).Select(ir => ir.Name).OrderBy(irName => irName));
                            output.Add($"* **{parent}:** {relationships}");
                        }
                    }
                }

                if (iface.AllProperties().Any()) {
                    output.Add("## Properties");
                    if (iface.DirectProperties().Count() > 0) { 
                        output.Add("|Name|Display name|Description|Schema|Writable|");
                        output.Add("|-|-|-|-|-|");
                    }
                    foreach (DTPropertyInfo property in iface.DirectProperties()) {
                        string name = property.Name;
                        string dname = string.Join("<br />", property.DisplayName.Select(kvp => $"**{kvp.Key}**: {kvp.Value}"));
                        string desc = string.Join("<br />", property.Description.Select(kvp => $"**{kvp.Key}**: {kvp.Value}"));
                        bool writable = property.Writable;
                        string schema = "TBD"; // TODO: Schema translation.
                        output.Add($"|{name}|{dname}|{desc}|{schema}|{writable}|");
                    }
                    if (iface.InheritedProperties().Any())
                    {
                        output.Add("### Inherited Properties");
                        foreach (Dtmi parent in iface.InheritedProperties().Select(ip => ip.DefinedIn).Distinct())
                        {
                            string properties = string.Join(", ", iface.InheritedProperties().Where(ip => ip.DefinedIn == parent).Select(ip => ip.Name).OrderBy(ipName => ipName));
                            output.Add($"* **{parent}:** {properties}");
                        }
                    }
                }

                if (iface.AllTelemetries().Any()) {
                    output.Add("## Telemetries");
                }

                if (iface.AllCommands().Any()) {
                    output.Add("## Commands");
                }

                if (ontology.RelationshipsTargeting(iface).Any())
                {
                    output.Add("## Target Of");
                    foreach (DTRelationshipInfo relationship in ontology.RelationshipsTargeting(iface))
                    {
                        DTEntityInfo definedIn = ontology[relationship.DefinedIn];
                        output.Add($"* {GetApiName(definedIn)}.{relationship.Name}");
                    }
                }
                
                string outputFilePath = GetPath(iface);
                if (Path.GetDirectoryName(outputFilePath) is string outputDirectoryPath) {
                    Directory.CreateDirectory(outputDirectoryPath);
                }

                File.WriteAllLines(outputFilePath, output);
                Console.WriteLine($"Wrote {outputFilePath}");
            }

        }

        private static string GetPath(DTInterfaceInfo iface)
        {
            // Construct parent directory structure based on longest parent path to root
            string ifaceName = GetApiName(iface);
            List<DTInterfaceInfo> parentDirectories = GetLongestParentPath(iface);
            string outputDirectory = outputRoot + "/" + string.Join("/", parentDirectories.Select(parent => GetApiName(parent)));
            
            // If the interface has children, place it with them
            if (ontology.ChildrenOf(iface).Any()) { outputDirectory += $"/{ifaceName}"; }
            
            string outputFilePath = $"{outputDirectory}/{ifaceName}.md";

            return outputFilePath;
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
            if (File.GetAttributes(inputRoot) == FileAttributes.Directory)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(inputRoot);
                sourceFiles = directoryInfo.EnumerateFiles("*.json", SearchOption.AllDirectories);
            }
            else
            {
                FileInfo singleSourceFile = new FileInfo(inputRoot);
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
                ontology = modelParser.Parse(modelJson);
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