using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BIMAutomate
{
    public class RevitDataContext
    {
        public static readonly RevitDataContext lazyInstance = new RevitDataContext();
      
        public Dictionary<Space, Tuple<XYZ, List<XYZ>>> SpacesInfo;
        public List<XYZ> Edges = new List<XYZ>();
    }
}