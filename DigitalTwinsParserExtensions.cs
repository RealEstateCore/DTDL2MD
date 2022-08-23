using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.Azure.DigitalTwins.Parser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DTDL2MD
{
    public static class DigitalTwinsParserExtensions
    {
        public static IEnumerable<DTInterfaceInfo> ChildrenOf(this IReadOnlyDictionary<Dtmi, DTEntityInfo> ontology, DTInterfaceInfo iface)
        {
            IEnumerable<DTInterfaceInfo> allInterfaces = ontology.Values.Where(entity => entity is DTInterfaceInfo).Select(entity => (DTInterfaceInfo)entity);
            return allInterfaces.Where(childInterface => childInterface.Extends.Contains(iface));
        }

        public static IEnumerable<DTRelationshipInfo> InheritedRelationships(this DTInterfaceInfo iface)
        {
            return iface.Contents.Values
                .Where(content => content is DTRelationshipInfo && content.DefinedIn != iface.Id)
                .Select(content => (DTRelationshipInfo)content);
        }
        public static IEnumerable<DTRelationshipInfo> DirectRelationships(this DTInterfaceInfo iface)
        {
            return iface.Contents.Values
                .Where(content => content is DTRelationshipInfo && content.DefinedIn == iface.Id)
                .Select(content => (DTRelationshipInfo)content);
        }

        public static IEnumerable<DTRelationshipInfo> AllRelationships(this DTInterfaceInfo iface)  
        {
            return iface.Contents.Values
                .Where(content => content is DTRelationshipInfo)
                .Select(content => (DTRelationshipInfo)content);
        }

        public static IEnumerable<DTPropertyInfo> InheritedProperties(this DTInterfaceInfo iface)
        {
            return iface.Contents.Values
                .Where(content => content is DTPropertyInfo && content.DefinedIn != iface.Id)
                .Select(content => (DTPropertyInfo)content);
        }

        public static IEnumerable<DTPropertyInfo> DirectProperties(this DTInterfaceInfo iface)
        {
            return iface.Contents.Values
                .Where(content => content is DTPropertyInfo && content.DefinedIn == iface.Id)
                .Select(content => (DTPropertyInfo)content);
        }

        public static IEnumerable<DTPropertyInfo> AllProperties(this DTInterfaceInfo iface)
        {
            return iface.Contents.Values
                .Where(content => content is DTPropertyInfo)
                .Select(content => (DTPropertyInfo)content);
        }

        public static IEnumerable<DTTelemetryInfo> AllTelemetries(this DTInterfaceInfo iface)
        {
            return iface.Contents.Values
                .Where(content => content is DTTelemetryInfo)
                .Select(content => (DTTelemetryInfo)content);
        }

        public static IEnumerable<DTCommandInfo> AllCommands(this DTInterfaceInfo iface)
        {
            return iface.Contents.Values
                .Where(content => content is DTCommandInfo)
                .Select(content => (DTCommandInfo)content);
        }
    }
}
