// nadir.arbia@gmail.com
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BIMAutomate
{
    using SpaceInformations = Dictionary<Space, Tuple<XYZ, List<XYZ>, List<XYZ>, List<XYZ>>>;

    public class RevitDataContext
    {
        public static readonly RevitDataContext lazyInstance = new RevitDataContext();

        public SpaceInformations SpacesInfo = new SpaceInformations();
        public List<XYZ> Edges = new List<XYZ>();
}
}