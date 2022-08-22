﻿using Microsoft.Azure.DigitalTwins.Parser;
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
    }
}