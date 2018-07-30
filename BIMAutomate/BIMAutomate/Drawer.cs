//nadir.arbia@gmail.com
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using System.Reflection;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Analysis;
using System.Text.RegularExpressions;

namespace BIMAutomate
{
    public class Drawer
    {
        private Application app;
        private UIApplication uiApplication;
        private UIDocument uidoc;
        private Document doc;
        private Element currentElement;
        private List<Duct> Ducts = new List<Duct>();
        private Dictionary<string, Connector> currentConnectors = new Dictionary<string, Connector>();
        private Connector currentConn = null;
        private int nbOfLvls = 0;
        private List<double> elevations = new List<double>();
        private Dictionary<Space, List<Tuple<Space, List<Tuple<Tuple<XYZ, XYZ>, Tuple<XYZ, XYZ>, XYZ>>>>> CorrespIntersec
            = new Dictionary<Space, List<Tuple<Space, List<Tuple<Tuple<XYZ, XYZ>, Tuple<XYZ, XYZ>, XYZ>>>>>();

        private XYZ currentConnOrigin = null, test11 = null;
        private List<int> tiltingSpaces = new List<int>();
        private Dictionary<int, int> correspSmallestSpaces = new Dictionary<int, int>();
        private List<SpatialElement> localsTech2 = new List<SpatialElement>();
        private List<SpatialElement> localsTech = new List<SpatialElement>();
        private List<List<SpatialElement>> sortedlocalsTech = new List<List<SpatialElement>>();
        private List<SpatialElement> stored = new List<SpatialElement>();

        public double diameter = 170 / 305.8;

        // was meant to be used to draw extraction mouth on Bath/Cooking/Wc rooms
        private List<Room> neihbourRoom = new List<Room>();

        public Drawer(UIApplication uiapp)
        {
            uiApplication = uiapp;
            app = uiapp.Application;
            uidoc = uiapp.ActiveUIDocument;
            doc = uidoc.Document;
            Utils.lazyInstance.SetEnv(uiapp);
            localsTech = GetElements<SpatialElement>(BuiltInCategory.OST_MEPSpaces).ToList();
         
            GetNumberOf_Level();
            if (SortLocalsTech() == -1)
                TaskDialog.Show("Erreur", "La codification des gaines techniques n'a pas été effectué.");
            Draw();
        }
        public List<SpatialElement> getVeriticality(SpatialElement el)
        {
            List<SpatialElement> spatialElements = new List<SpatialElement>();
            int nbPerVerticality = 0;
            int caughttemp1 = 0;
            int caughttemp2 = Convert.ToInt16(el.Name[2].ToString()
             + el.Name[3].ToString());
            foreach (var item in localsTech)
            {
                if (item.Name.ToUpper().Contains("GT"))
                {
                    try
                    {
                        caughttemp1 = Convert.ToInt16(item.Name[2].ToString() + item.Name[3].ToString());
                        if (caughttemp1 == caughttemp2)
                        {
                            nbPerVerticality += 1;
                            spatialElements.Add(item);
                        }
                    }
                    catch
                    {
                        TaskDialog.Show("Erreur de codification.", "Codification invalide de la gaine: " + item.Name);
                        System.Environment.Exit(-1);
                    }
                }
                spatialElements.Sort(delegate (SpatialElement c1, SpatialElement c2)
                {
                    return c1.Level.Elevation.CompareTo(c2.Level.Elevation);
                });
            }
            return spatialElements;
        }

        private int SortLocalsTech()
        {
            if (localsTech.Count() == 0)
                return -1;

            try
            {
                bool neg = false;
                int i = 0;
                string nb = "1";
                string nb2 = "1";
                foreach (var item in localsTech)
                    if (localsTech.ElementAt(i).Name.ToUpper().Contains("GT"))
                    {
                        nb = localsTech.ElementAt(i).Name[6].ToString() + localsTech.ElementAt(i).Name[7].ToString();
                        nb2 = localsTech.ElementAt(i).Name[6].ToString() + localsTech.ElementAt(i).Name[7].ToString();
                        break;
                    }


                List<int> nbs = new List<int>();

                for (int index = 0; index < localsTech.Count(); index += 1)
                {
                    int k = index;
                    if (localsTech.ElementAt(index).Name.ToUpper().Contains("GT"))
                        nb = localsTech.ElementAt(index).Name[2].ToString() + localsTech.ElementAt(index).Name[3].ToString();

                    if (localsTech.ElementAt(index).Name.ToUpper().Contains("GT") && !nbs.Contains(Convert.ToInt32(nb)))
                    {
                        var sorted = getVeriticality(localsTech.ElementAt(index));
                        if (sorted.Count() == 0)
                            return -1;
                        sortedlocalsTech.Add(sorted);
                        nbs.Add(Convert.ToInt32(nb));
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Erreur de nommage.", "Pour la bonne reconnaissance du programme, il est nécessaire de nommer vos gaines tel que: \"GT[n]\", \"n\" étant le numéro de gaine de la verticalité.(Entier relatif) Example: GT1, gt1, gt01, GT01_N-1...");
            }
            return 0;
        }

        public void Draw()
        {
            sortedlocalsTech = sortedlocalsTech.Distinct(new CusComparer()).ToList();
            try
            {
                for (int i = 0; i < sortedlocalsTech.Count(); i += 1)
                {
                    foreach (var item in sortedlocalsTech.ElementAt(i))
                    {
                        var myspace = doc.GetElement(item.Id) as Space;
                        SpatialElementGeometryCalculator calculator = new SpatialElementGeometryCalculator(doc);
                        SpatialElementGeometryResults results = calculator.CalculateSpatialElementGeometry(myspace); // compute the room geometry 
                        Solid roomSolid = results.GetGeometry();
                        results.GetGeometry();

                        XYZ sp = null, ep = null;

                        ElementId levelId = ElementId.InvalidElementId;
                        Document document = uidoc.Document;
                        Utils.lazyInstance.GetElementLocation(out sp, myspace);
                        BoundingBoxXYZ bbxyz = myspace.get_BoundingBox(null);   

                        bbxyz.Enabled = true;
                        List<XYZ> edges = new List<XYZ>();
                        edges.Add(new XYZ(Math.Round(bbxyz.get_Bounds(0).X, 2), Math.Round(bbxyz.get_Bounds(0).Y, 2), Math.Round(bbxyz.get_Bounds(1).Z, 2)));
                        edges.Add(new XYZ(Math.Round(bbxyz.get_Bounds(1).X, 2), Math.Round(bbxyz.get_Bounds(0).Y, 2), Math.Round(bbxyz.get_Bounds(1).Z, 2)));
                        edges.Add(new XYZ(Math.Round(bbxyz.get_Bounds(0).X, 2), Math.Round(bbxyz.get_Bounds(0).Y, 2), Math.Round(bbxyz.get_Bounds(0).Z, 2)));
                        edges.Add(new XYZ(Math.Round(bbxyz.get_Bounds(1).X, 2), Math.Round(bbxyz.get_Bounds(0).Y, 2), Math.Round(bbxyz.get_Bounds(0).Z, 2)));
                        edges.Add(new XYZ(Math.Round(bbxyz.get_Bounds(0).X, 2), Math.Round(bbxyz.get_Bounds(1).Y, 2), Math.Round(bbxyz.get_Bounds(1).Z, 2)));
                        edges.Add(new XYZ(Math.Round(bbxyz.get_Bounds(1).X, 2), Math.Round(bbxyz.get_Bounds(1).Y, 2), Math.Round(bbxyz.get_Bounds(1).Z, 2)));
                        edges.Add(new XYZ(Math.Round(bbxyz.get_Bounds(0).X, 2), Math.Round(bbxyz.get_Bounds(1).Y, 2), Math.Round(bbxyz.get_Bounds(0).Z, 2)));
                        edges.Add(new XYZ(Math.Round(bbxyz.get_Bounds(1).X, 2), Math.Round(bbxyz.get_Bounds(1).Y, 2), Math.Round(bbxyz.get_Bounds(0).Z, 2)));

                        Utils.lazyInstance.GetLowestAndHighestPolygon(edges, myspace.Level);
                        RevitDataContext.lazyInstance.SpacesInfo.Add(myspace, new Tuple<XYZ,
                                                                                        List<XYZ>,
                                                                                        List<XYZ>,
                                                                                        List<XYZ>>(Utils.lazyInstance.GetCenterOfPolygon(edges),
                                                                                                    edges, new List<XYZ>(Utils.lazyInstance.lowestPolyg), new List<XYZ>(Utils.lazyInstance.highestPolyg)));
                        ep = sp;

                    }
                    Debug.WriteLine("count(): " + RevitDataContext.lazyInstance.SpacesInfo.Count());
                    CreateDucts(i);
                    correspSmallestSpaces.Clear();
                    tiltingSpaces.Clear();
                    currentConnectors.Clear();
                    CorrespIntersec.Clear();
                    RevitDataContext.lazyInstance.SpacesInfo.Clear();
                }
            }
            catch
            {
                TaskDialog.Show("Erreur", "Erreur: Vérifier bien que toutes les gaines ont été correctement assigné. Supprimez tous les espaces non assignés et non clos.");
            }
        }



        // Get the path  best one
        private Tuple<XYZ, XYZ> GetBestPath()
        {
            XYZ bStartingP = null, bEndingP = null;

            //left in the plan
            XYZ S1 = new XYZ(); XYZ S2 = new XYZ(); XYZ S3 = new XYZ(); XYZ S4 = new XYZ();
            // right in the plan
            XYZ S5 = new XYZ(); XYZ S6 = new XYZ(); XYZ S7 = new XYZ(); XYZ S8 = new XYZ();


            ////// Lookup, it is the 2nd space  we are checking in
            //left in the plan
            XYZ S1Lookup = new XYZ(); XYZ S2Lookup = new XYZ(); XYZ S3Lookup = new XYZ(); XYZ S4Lookup = new XYZ();
            // right in the plan
            XYZ S5Lookup = new XYZ(); XYZ S6Lookup = new XYZ(); XYZ S7Lookup = new XYZ(); XYZ S8Lookup = new XYZ();
            int i = 0;
            foreach (var spaceInfoItem in RevitDataContext.lazyInstance.SpacesInfo)
            {
                // Polygon Right in the plan
                BoundingBoxXYZ bbxyz = spaceInfoItem.Key.get_BoundingBox(null);
                bbxyz.Enabled = true;

                S1 = new XYZ(Math.Round(bbxyz.get_Bounds(0).X, 2), Math.Round(bbxyz.get_Bounds(0).Y, 2), Math.Round(bbxyz.get_Bounds(1).Z, 2));
                S2 = new XYZ(Math.Round(bbxyz.get_Bounds(1).X, 2), Math.Round(bbxyz.get_Bounds(0).Y, 2), Math.Round(bbxyz.get_Bounds(1).Z, 2));
                S3 = new XYZ(Math.Round(bbxyz.get_Bounds(0).X, 2), Math.Round(bbxyz.get_Bounds(0).Y, 2), Math.Round(bbxyz.get_Bounds(0).Z, 2));
                S4 = new XYZ(Math.Round(bbxyz.get_Bounds(1).X, 2), Math.Round(bbxyz.get_Bounds(0).Y, 2), Math.Round(bbxyz.get_Bounds(0).Z, 2));
                S5 = new XYZ(Math.Round(bbxyz.get_Bounds(0).X, 2), Math.Round(bbxyz.get_Bounds(1).Y, 2), Math.Round(bbxyz.get_Bounds(1).Z, 2));
                S6 = new XYZ(Math.Round(bbxyz.get_Bounds(1).X, 2), Math.Round(bbxyz.get_Bounds(1).Y, 2), Math.Round(bbxyz.get_Bounds(1).Z, 2));
                S7 = new XYZ(Math.Round(bbxyz.get_Bounds(0).X, 2), Math.Round(bbxyz.get_Bounds(1).Y, 2), Math.Round(bbxyz.get_Bounds(0).Z, 2));
                S8 = new XYZ(Math.Round(bbxyz.get_Bounds(1).X, 2), Math.Round(bbxyz.get_Bounds(1).Y, 2), Math.Round(bbxyz.get_Bounds(0).Z, 2));

                List<Tuple<Space, List<Tuple<Tuple<XYZ, XYZ>, Tuple<XYZ, XYZ>, XYZ>>>> Gintersections
                         = new List<Tuple<Space, List<Tuple<Tuple<XYZ, XYZ>, Tuple<XYZ, XYZ>, XYZ>>>>();
                ////// Lookup, it is the 2nd space  we are checking in
                for (int j = 0; j < RevitDataContext.lazyInstance.SpacesInfo.Count(); j += 1)
                {
                    if (j != i)
                    {
                        /// param are projected face start & ending point, lookingup start & ending point and intersection respectively 
                        List<Tuple<Tuple<XYZ, XYZ>, Tuple<XYZ, XYZ>, XYZ>> intersections = new List<Tuple<Tuple<XYZ, XYZ>, Tuple<XYZ, XYZ>, XYZ>>();

                        BoundingBoxXYZ bbxyzLookup = RevitDataContext.lazyInstance.SpacesInfo.ElementAt(j).Key.get_BoundingBox(null);
                        bbxyzLookup.Enabled = true;

                        // Polygon Left in the plan
                        S1Lookup = new XYZ(Math.Round(bbxyzLookup.get_Bounds(0).X, 2), Math.Round(bbxyzLookup.get_Bounds(0).Y, 2), Math.Round(bbxyzLookup.get_Bounds(1).Z, 2));
                        S2Lookup = new XYZ(Math.Round(bbxyzLookup.get_Bounds(1).X, 2), Math.Round(bbxyzLookup.get_Bounds(0).Y, 2), Math.Round(bbxyzLookup.get_Bounds(1).Z, 2));
                        S3Lookup = new XYZ(Math.Round(bbxyzLookup.get_Bounds(0).X, 2), Math.Round(bbxyzLookup.get_Bounds(0).Y, 2), Math.Round(bbxyzLookup.get_Bounds(0).Z, 2));
                        S4Lookup = new XYZ(Math.Round(bbxyzLookup.get_Bounds(1).X, 2), Math.Round(bbxyzLookup.get_Bounds(0).Y, 2), Math.Round(bbxyzLookup.get_Bounds(0).Z, 2));
                        // Polygon Right in the plan
                        S5Lookup = new XYZ(Math.Round(bbxyzLookup.get_Bounds(0).X, 2), Math.Round(bbxyzLookup.get_Bounds(1).Y, 2), Math.Round(bbxyzLookup.get_Bounds(1).Z, 2));
                        S6Lookup = new XYZ(Math.Round(bbxyzLookup.get_Bounds(1).X, 2), Math.Round(bbxyzLookup.get_Bounds(1).Y, 2), Math.Round(bbxyzLookup.get_Bounds(1).Z, 2));
                        S7Lookup = new XYZ(Math.Round(bbxyzLookup.get_Bounds(0).X, 2), Math.Round(bbxyzLookup.get_Bounds(1).Y, 2), Math.Round(bbxyzLookup.get_Bounds(0).Z, 2));
                        S8Lookup = new XYZ(Math.Round(bbxyzLookup.get_Bounds(1).X, 2), Math.Round(bbxyzLookup.get_Bounds(1).Y, 2), Math.Round(bbxyzLookup.get_Bounds(0).Z, 2));

                        // Polygon projected from highest  of first polygon  to lowest lookup's polygon
                        XYZ S1Pp = new XYZ(Math.Round(S2.X, 2), Math.Round(S2.Y, 2), Math.Round(S4Lookup.Z, 2));
                        XYZ S2Pp = new XYZ(Math.Round(S6.X, 2), Math.Round(S6.Y, 2), Math.Round(S8Lookup.Z, 2));
                        XYZ S3Pp = new XYZ(Math.Round(S1.X, 2), Math.Round(S1.Y, 2), Math.Round(S3Lookup.Z, 2));
                        XYZ S4Pp = new XYZ(Math.Round(S5.X, 2), Math.Round(S5.Y, 2), Math.Round(S7Lookup.Z, 2));

                        // because for some reasons Line object has no properties startingpoint and ending point         
                        List<Tuple<Line, XYZ, XYZ>> projectedFace = new List<Tuple<Line, XYZ, XYZ>>();
                        projectedFace.Add(new Tuple<Line, XYZ, XYZ>(Line.CreateBound(S8Lookup, S7Lookup), S8Lookup, S7Lookup));
                        projectedFace.Add(new Tuple<Line, XYZ, XYZ>(Line.CreateBound(S4Lookup, S3Lookup), S4Lookup, S3Lookup));
                        projectedFace.Add(new Tuple<Line, XYZ, XYZ>(Line.CreateBound(S4Lookup, S8Lookup), S4Lookup, S8Lookup));
                        projectedFace.Add(new Tuple<Line, XYZ, XYZ>(Line.CreateBound(S3Lookup, S7Lookup), S3Lookup, S7Lookup));

                        List<Tuple<Line, XYZ, XYZ>> lookingUpFace = new List<Tuple<Line, XYZ, XYZ>>();
                        lookingUpFace.Add(new Tuple<Line, XYZ, XYZ>(Line.CreateBound(S2Pp, S4Pp), S2Pp, S4Pp));
                        lookingUpFace.Add(new Tuple<Line, XYZ, XYZ>(Line.CreateBound(S1Pp, S3Pp), S1Pp, S3Pp));
                        lookingUpFace.Add(new Tuple<Line, XYZ, XYZ>(Line.CreateBound(S1Pp, S2Pp), S1Pp, S2Pp));
                        lookingUpFace.Add(new Tuple<Line, XYZ, XYZ>(Line.CreateBound(S3Pp, S4Pp), S3Pp, S4Pp));

                        foreach (Tuple<Line, XYZ, XYZ> lp in projectedFace)
                        {
                            foreach (Tuple<Line, XYZ, XYZ> llu in lookingUpFace)
                            {
                                IntersectionResultArray intersectionR = new IntersectionResultArray();
                                SetComparisonResult res = SetComparisonResult.Disjoint;
                                res = lp.Item1.Intersect(llu.Item1, out intersectionR);
                                if (SetComparisonResult.Disjoint != res)
                                {
                                    if (intersectionR != null)
                                    {
                                        if (!intersectionR.IsEmpty)
                                        {
                                            intersections.Add(new Tuple<Tuple<XYZ, XYZ>, Tuple<XYZ, XYZ>, XYZ>(new Tuple<XYZ, XYZ>(new XYZ(Math.Round(lp.Item2.X, 2), Math.Round(lp.Item2.Y, 2), Math.Round(lp.Item2.Z, 2)),
                                                                                                                                   new XYZ(Math.Round(lp.Item3.X, 2), Math.Round(lp.Item3.Y, 2), Math.Round(lp.Item3.Z, 2))),
                                                                                                                                   new Tuple<XYZ, XYZ>(new XYZ(Math.Round(llu.Item2.X, 2), Math.Round(llu.Item2.Y, 2), Math.Round(llu.Item2.Z, 2)),
                                                                                                                                   new XYZ(Math.Round(llu.Item3.X, 2), Math.Round(llu.Item3.Y, 2), Math.Round(llu.Item3.Z, 2))),
                                                                                                                                   new XYZ(Math.Round(intersectionR.get_Item(0).XYZPoint.X, 2), Math.Round(intersectionR.get_Item(0).XYZPoint.Y, 2), Math.Round(intersectionR.get_Item(0).XYZPoint.Z, 2))));
                                        }
                                    }
                                }
                            }
                        }
                        Utils.lazyInstance.PurgeList(intersections);
                        Gintersections.Add(new Tuple<Space, List<Tuple<Tuple<XYZ, XYZ>, Tuple<XYZ, XYZ>, XYZ>>>(RevitDataContext.lazyInstance.SpacesInfo.ElementAt(j).Key, intersections));
                    }
                }
                var Ginters = Gintersections.Distinct().ToList();
                CorrespIntersec.Add(spaceInfoItem.Key, Ginters);
                i += 1;
            }
            return new Tuple<XYZ, XYZ>(bStartingP, bEndingP);
        }

        private void GetTiltingSpace()
        {
            for (int i = 0; i < CorrespIntersec.Count(); i += 1)
            {
                int tiltingIndex = 0;
                int j = 0;
                var coord1 = Math.Abs(RevitDataContext.lazyInstance.SpacesInfo.ElementAt(i).Value.Item3.ElementAt(0).X);
                var coord4 = Math.Abs(RevitDataContext.lazyInstance.SpacesInfo.ElementAt(i).Value.Item3.ElementAt(0).X);
                foreach (var xyz in RevitDataContext.lazyInstance.SpacesInfo.ElementAt(i).Value.Item3)
                {
                    if (coord1 < Math.Abs(xyz.X))
                        coord1 = Math.Abs(xyz.X);
                    if (coord4 > Math.Abs(xyz.X))
                        coord4 = Math.Abs(xyz.X);
                }
                foreach (var item in CorrespIntersec.ElementAt(i).Value)
                {
                    var coord2 = Math.Abs(RevitDataContext.lazyInstance.SpacesInfo.ElementAt(j).Value.Item3.ElementAt(0).X);
                    var coord3 = Math.Abs(RevitDataContext.lazyInstance.SpacesInfo.ElementAt(j).Value.Item3.ElementAt(0).X);


                    foreach (var xyz1 in RevitDataContext.lazyInstance.SpacesInfo.ElementAt(j).Value.Item3)
                    {
                        if (coord2 < Math.Abs(xyz1.X))
                            coord2 = Math.Abs(xyz1.X);
                        if (coord3 > Math.Abs(xyz1.X))
                            coord3 = Math.Abs(xyz1.X);
                    }
                    if (item.Item2.Count() == 0 && tiltingIndex >= i)
                    {
                        tiltingSpaces.Add(tiltingIndex);
                        break;
                    }
                    if (tiltingIndex >= i
                        && coord1 - coord3 <= 0
                        && coord1 - coord2 < 0 && coord4 - coord3 < 0)
                    {
                        tiltingSpaces.Add(j);
                        break;
                    }
                    if (tiltingIndex >= i
                        && coord4 - coord2 <= 0
                        && coord1 - coord2 < 0 && coord4 - coord3 < 0)
                    {
                        tiltingSpaces.Add(j - 1);
                        break;
                    }
                    tiltingIndex += 1;
                    j += 1;
                }
                i = tiltingIndex;
            }
        }

        private void GetBestPosition()
        {
            List<int> sp = new List<int>();
            int stop = 0;
            tiltingSpaces.Add(RevitDataContext.lazyInstance.SpacesInfo.Count() - 1);
            if (tiltingSpaces.Count() == 1)
            {
                for (int i = 0; i < RevitDataContext.lazyInstance.SpacesInfo.Count(); i += 1)
                {
                    correspSmallestSpaces.Add(i, RevitDataContext.lazyInstance.SpacesInfo.Count() - 1);
                }
                return;
            }
            stop = tiltingSpaces.ElementAt(0);
            int range = 0;
            for (int i = 0; i < RevitDataContext.lazyInstance.SpacesInfo.Count();)
            {
                int index = 0;
                if (i > tiltingSpaces.ElementAt(range))
                {
                    index = range;
                    range += 1;
                }
                while (index < tiltingSpaces.ElementAt(range))
                {
                    var volume = RevitDataContext.lazyInstance.SpacesInfo.ElementAt(index).Key.Volume;
                    if (RevitDataContext.lazyInstance.SpacesInfo.ElementAt(index).Key.Volume <= volume)
                        volume = RevitDataContext.lazyInstance.SpacesInfo.ElementAt(index).Key.Volume;
                    index += 1;
                }
                if (index > i)
                {
                    while (i < index)
                    {
                        correspSmallestSpaces.Add(i, index);
                        i += 1;
                    }
                }
                else
                {
                    correspSmallestSpaces.Add(i, index);
                    i += 1;
                }
            }
        }

        public IEnumerable<T> GetElements<T>(BuiltInCategory builtInCategory) where T : Element
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            // Seems you must be a subclass of element to use the OfClass method
            if (typeof(T) != typeof(Element))
                collector.OfClass(typeof(T));
            collector.OfCategory(builtInCategory);
            return collector.Cast<T>();
        }
        Room GetRoomNeighbourAt(BoundarySegment bs, Room r)
        {
            Document doc = r.Document;

            Element e = doc.GetElement(bs.ElementId);
            Wall w =  e as Wall;

            double wallThickness = w.Width;

            double wallLength = (w.Location as
              LocationCurve).Curve.Length;

            Transform derivatives = bs.GetCurve()
              .ComputeDerivatives(0.5, true);

            XYZ midPoint = derivatives.Origin;

            Debug.Assert(
              midPoint.IsAlmostEqualTo(
                bs.GetCurve().Evaluate(0.5, true)),
              "expected same result from Evaluate and derivatives");

            XYZ tangent = derivatives.BasisX.Normalize();

            XYZ normal = new XYZ(tangent.Y, tangent.X * (-1), tangent.Z);

            XYZ p = midPoint + wallThickness * normal;

            Room otherRoom = doc.GetRoomAtPoint(p);

            if (null != otherRoom)
            {
                if (otherRoom.Id == r.Id)
                {
                    normal = new XYZ(tangent.Y * (-1),
                      tangent.X, tangent.Z);

                    p = midPoint + wallThickness * normal;

                    otherRoom = doc.GetRoomAtPoint(p);

                    Debug.Assert(null == otherRoom
                        || otherRoom.Id != r.Id,
                      "expected different room on other side");
                }
            }
            return otherRoom;
        }

        public List<String> GetRoomNeihbour(Room myroom)
        {
            List<string> rnbh = new List<string>();

            SpatialElementBoundaryOptions opt
            = new SpatialElementBoundaryOptions();

            IList<IList<BoundarySegment>> loops;

            Room neighbour;
            loops = myroom.GetBoundarySegments(opt);
            foreach (IList<BoundarySegment> loop in loops)
            {
                foreach (BoundarySegment seg in loop)
                {
                    neighbour = GetRoomNeighbourAt(seg, myroom);
                    Debug.WriteLine("neighbour Name: " + neighbour.Name);
                }
            }
            return rnbh;
        }

        private void GetNumberOf_Level()
        {
            StringBuilder levelInformation = new StringBuilder();
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ICollection<Element> collection = collector.OfClass(typeof(Level)).ToElements();
            foreach (Element e in collection)
            {
                Level level = e as Level;
                if (null != level)
                {
                    nbOfLvls += 1;
                    elevations.Add(level.Elevation);
                }
            }
        }
        private void CreateDucts(int jndex)
        { 
            if (sortedlocalsTech.ElementAt(jndex).ElementAt(0).Name.Contains("GT24"))
                Debug.WriteLine("TEST GAINE 24");
            Debug.WriteLine(jndex);
            ElementId levelId = ElementId.InvalidElementId;
            tiltingSpaces.Clear();
            GetBestPath();
            GetTiltingSpace();
            GetBestPosition();
            currentConn = null;

            for (int i = 0; i < sortedlocalsTech.ElementAt(jndex).Count();)
            {
                if (sortedlocalsTech.ElementAt(jndex).Count() == 1)
                {
                    Debug.WriteLine("i: " + i);
                    break;  
                }                

                Element e = doc.GetElement(sortedlocalsTech.ElementAt(jndex).ElementAt(i).Id);
                var space = doc.GetElement(sortedlocalsTech.ElementAt(jndex).ElementAt(i).Id) as Space;

                try
                {
                    if (i >= RevitDataContext.lazyInstance.SpacesInfo.Count())
                        break;
                    SpatialElementGeometryCalculator calculator = new SpatialElementGeometryCalculator(doc);
                    SpatialElementGeometryResults results = calculator.CalculateSpatialElementGeometry(space); // compute the room geometry 
                    Solid roomSolid = results.GetGeometry();
                    List<Face> lf = new List<Face>();
                    List<XYZ> Edges = new List<XYZ>();
                    List<XYZ> temp = new List<XYZ>();
                    foreach (Face face in roomSolid.Faces)
                    {
                        foreach (EdgeArray item in face.EdgeLoops)
                        {
                            List<XYZ> lc = JtBoundingBoxXyzExtensionMethods.GetPolygon(item);
                            foreach (var subitem in lc)
                                temp.Add(subitem);
                        }
                    }
                    XYZ tempXYZ = null;
                    #region delete identical points, Distinct() linq not that smart
                    foreach (var item in temp)
                    {
                        if (tempXYZ == null)
                        {
                            tempXYZ = item;
                            Edges.Add(item);
                        }
                        else
                        {
                            bool isPresent = false;
                            foreach (var item2 in Edges)
                            {
                                if (item.X == item2.X
                                    && item.Y == item2.Y
                                    && item.Z == item2.Z)
                                    isPresent = true;
                            }
                            if (isPresent == false)
                                Edges.Add(item);
                        }
                    }
                    #endregion

                    Level currentLevel = null;
                    //Get the duct Type
                    FilteredElementCollector collector1 = new FilteredElementCollector(doc);
                    collector1.OfClass(typeof(DuctType));
                    DuctType ductType = null;
                    foreach (Element elem in collector1)
                    {
                        /** 
                            * Raccord avec té et coude droit
                            Raccord par té avec coude segmenté
                            Raccord par piquage et coude segmenté
                            Raccord par té et coude droit
                            Raccord par piquage et coude droit
                            Raccord avec té et coude segmenté
                            Raccord par piquage et coude à rayon
                            Raccord par piquage et coude segmenté
                            Raccord par piquage et coude droit
                            Raccord par piquage et coude lisse
                            Raccord avec té et coude lisse
                            Synoptique de désenfumage
                            Raccord par piquage et coude droit chanfreiné
                            Raccord avec té et coude à rayon
                        **/
                        if (elem.Name == "Raccord avec té et coude lisse"
                            || elem.Name == "Connection with tee and smooth elbow") // gerer english
                            ductType = elem as DuctType;
                    }
                    FilteredElementCollector collector2 = new FilteredElementCollector(doc);
                    collector2.OfClass(typeof(MechanicalSystemType)).OfCategory(BuiltInCategory.OST_DuctSystem);
                    MechanicalSystemType mechType = null;
                    foreach (Element e1 in collector2)
                    {
                        /**
                            Désenfumage air neuf
                            Conduit de Fumée
                            Soufflage
                            Reprise
                            Extraction
                            VMC
                            Desenfumage Extraction
                            Air Neuf
                            Rejet
                            Desenfumage Air Neuf
                            Soufflage Recylage
                            Reprise Recyclage
                            Soufflage VC
                            Soufflage CTA
                            ..
                            **/
                        if (e1.Name == "VMC" || e1.Name == "CMV")  // gerer english
                            mechType = e1 as MechanicalSystemType;
                    }

                    /**
                        *  Get next space 
                        **/


                    bool GetMe = false;
                    int at = 0;
                    foreach (var key in RevitDataContext.lazyInstance.SpacesInfo.Keys)
                    {
                        if (key.Number == space.Number)
                            GetMe = true;
                        if (GetMe == true)
                        {
                            if (at != 0)
                                at -= 1;
                            break;
                        }
                        at += 1;
                    }

                    currentLevel = e.Document.GetElement(e.LevelId) as Level;
                    XYZ startingPoint = null;
                    XYZ endingPoint = null;

                    /**
                        *  Get the starting point shifted depending on next space location
                        * */

                    Utils.lazyInstance.GetLowestAndHighestPolygon(RevitDataContext.lazyInstance.SpacesInfo.ElementAt(i).Value.Item2, space.Level);
                    List<XYZ> test1 = new List<XYZ>();
                    List<XYZ> test2 = new List<XYZ>();
                    test1 = Utils.lazyInstance.lowestPolyg;
                    test2 = Utils.lazyInstance.highestPolyg;
                    if (test1.Count() != 0 && test2.Count() != 0)
                    {
                        startingPoint = Utils.lazyInstance.GetCenterOfPolygon(test1);
                        endingPoint = Utils.lazyInstance.GetCenterOfPolygon(test2);
                    }
         
                    Utils.lazyInstance.GetLowestAndHighestPolygon(RevitDataContext.lazyInstance.SpacesInfo.ElementAt(correspSmallestSpaces[i]).Value.Item2,
                                                                    RevitDataContext.lazyInstance.SpacesInfo.ElementAt(correspSmallestSpaces[i]).Key.Level);
                    List<XYZ> test5 = new List<XYZ>();
                    List<XYZ> test6 = new List<XYZ>();
                    test5 = Utils.lazyInstance.lowestPolyg;
                    test6 = Utils.lazyInstance.highestPolyg;
                    XYZ SendingPoint = Utils.lazyInstance.GetCenterOfPolygon(test6);
                    startingPoint = Utils.lazyInstance.GetCenterOfPolygon(test5);
                    Duct duct = null;
                    Connector ductStart = null;
                    Connector ductEnd = null;
                    Utils.lazyInstance.GetLowestAndHighestPolygon(RevitDataContext.lazyInstance.SpacesInfo.ElementAt(i).Value.Item2,
                                                                    RevitDataContext.lazyInstance.SpacesInfo.ElementAt(i).Key.Level);
                    List<XYZ> test3 = new List<XYZ>();
                    List<XYZ> test4 = new List<XYZ>();
                    test3 = Utils.lazyInstance.lowestPolyg;
                    test4 = Utils.lazyInstance.highestPolyg;
                    endingPoint = new XYZ(SendingPoint.X, SendingPoint.Y, Utils.lazyInstance.GetCenterOfPolygon(test4).Z);

                    if (tiltingSpaces.Contains(correspSmallestSpaces[i]) && i != RevitDataContext.lazyInstance.SpacesInfo.Count() - 1)
                    {
                        XYZ nSt = new XYZ(startingPoint.X - 0.1, startingPoint.Y, startingPoint.Z - 0.8);
                        startingPoint = nSt;
                    }
                    Debug.WriteLine("i: " + i);
                    if (!tiltingSpaces.Contains(i) || i == 0 || i == tiltingSpaces.LastOrDefault())
                    {
                        if (currentConn != null)
                        {
                            XYZ nStart1 = currentConnOrigin;
                            endingPoint = nStart1;
                        }
                        using (Transaction tr = new Transaction(doc))
                        {
                            tr.Start("Create New Duct");
                            {
                                if (currentConn == null)
                                    duct = Duct.Create(doc, mechType.Id, ductType.Id, RevitDataContext.lazyInstance.SpacesInfo.ElementAt(i).Key.LevelId, startingPoint, endingPoint);
                                else
                                {
                                    duct = Duct.Create(doc, ductType.Id, RevitDataContext.lazyInstance.SpacesInfo.ElementAt(i).Key.LevelId, currentConn, startingPoint);
                                    Debug.WriteLine("currentConn.Origin: " + currentConn.Origin);
                                    Debug.WriteLine("startingPoint Origin: " + startingPoint);
                                }
                              

                                Parameter parameter = duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                                parameter.Set(diameter);
                                List<Connector> lC = new List<Connector>();
                                foreach (Connector conn in duct.ConnectorManager.Connectors)
                                {
                                    if (conn.ConnectorType == ConnectorType.End)
                                        lC.Add(conn);
                                }

                                if (lC.ElementAt(0).Origin.Z > lC.ElementAt(1).Origin.Z)
                                {
                                    ductStart = lC.ElementAt(1);
                                    ductEnd = lC.ElementAt(0);
                                }
                                else
                                {
                                    ductStart = lC.ElementAt(0);
                                    ductEnd = lC.ElementAt(1);
                                }
                                if (currentConn == null)
                                {
                                    currentConnectors.Add("start" + i, ductStart);
                                    currentConnectors.Add("end" + i, ductEnd);
                                }
                                else
                                {
                                    currentConnectors.Add("start" + i, currentConn);
                                    currentConnectors.Add("end" + i, ductEnd);
                                    doc.Create.NewElbowFitting(ductStart, currentConn);
                                }
                                Debug.WriteLine("Passed, ductStart.Origin : " + ductStart.Origin);
                                tr.Commit();
                                currentConn = ductEnd;
                            }
                        }
                    }

                    // is getting tilted
                    if (tiltingSpaces.Contains(correspSmallestSpaces[i]) 
                        && i != RevitDataContext.lazyInstance.SpacesInfo.Count() - 1 
                        && correspSmallestSpaces[i] != RevitDataContext.lazyInstance.SpacesInfo.Count() - 1)
                    {
                        
                        Utils.lazyInstance.GetLowestAndHighestPolygon(RevitDataContext.lazyInstance.SpacesInfo.ElementAt(correspSmallestSpaces[i + 1]).Value.Item2,
                                                                        RevitDataContext.lazyInstance.SpacesInfo.ElementAt(correspSmallestSpaces[i + 1]).Key.Level);
                        List<XYZ> test7 = new List<XYZ>();
                        test7 = Utils.lazyInstance.lowestPolyg;
                        var tempZ = startingPoint.Z;
                        endingPoint = new XYZ(Utils.lazyInstance.GetCenterOfPolygon(test7).X, Utils.lazyInstance.GetCenterOfPolygon(test7).Y, tempZ);

                        if (currentConn != null)
                        {
                            Utils.lazyInstance.GetLowestAndHighestPolygon(RevitDataContext.lazyInstance.SpacesInfo.ElementAt(correspSmallestSpaces[correspSmallestSpaces[i] + 1]).Value.Item2,
                                                                        RevitDataContext.lazyInstance.SpacesInfo.ElementAt(correspSmallestSpaces[i + 1]).Key.Level);
                            List<XYZ> test77 = new List<XYZ>();
                            test77 = Utils.lazyInstance.lowestPolyg;
                            var tempZZ = startingPoint.Z;
                            endingPoint = new XYZ(Utils.lazyInstance.GetCenterOfPolygon(test77).X, Utils.lazyInstance.GetCenterOfPolygon(test77).Y, tempZ);
                        }

                        using (Transaction tr2 = new Transaction(doc))
                        {
                            tr2.Start("Create New Tilting Duct");
                            {
                                duct = Duct.Create(doc, mechType.Id, ductType.Id, RevitDataContext.lazyInstance.SpacesInfo.ElementAt(i).Key.LevelId, startingPoint, endingPoint);
                                Debug.WriteLine("startingPoint Origin: " + startingPoint);
                                Debug.WriteLine("endingPoint.Origin: " + endingPoint);

                                Parameter parameter = duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                                parameter.Set(diameter);
                                List<Connector> lC = new List<Connector>();
                                foreach (Connector conn in duct.ConnectorManager.Connectors)
                                {
                                    if (conn.ConnectorType == ConnectorType.End)
                                        lC.Add(conn);
                                }
                                int tempi = i + 1;
                                if (i != 0)
                                    tempi = correspSmallestSpaces[correspSmallestSpaces[i] + 1];
                                // getting the right start and end duct
                                Utils.lazyInstance.GetLowestAndHighestPolygon(RevitDataContext.lazyInstance.SpacesInfo.ElementAt(tempi).Value.Item2,
                                                                                RevitDataContext.lazyInstance.SpacesInfo.ElementAt(tempi).Key.Level);

                                List<XYZ> test8 = new List<XYZ>();
                                test8 = Utils.lazyInstance.lowestPolyg;
                                lC = Utils.lazyInstance.GetClosestConnector(lC, Utils.lazyInstance.GetCenterOfPolygon(test8));
                                ductStart = lC.ElementAt(0);
                                ductEnd = lC.ElementAt(1);

                                currentConnOrigin = ductStart.Origin;
                                test11 = ductEnd.Origin;
                                Debug.WriteLine("Start of tilting is :" + ductStart.Origin);
                                Debug.WriteLine("end of tilting is :" + ductEnd.Origin);
                                currentConn = ductStart;
                                doc.Create.NewElbowFitting(ductEnd, currentConnectors.ElementAt(currentConnectors.Count() - 1).Value);
                                currentConnectors.Add("startTilted" + i, ductStart);
                                currentConnectors.Add("endTilted" + i, ductEnd);
                            }
                            tr2.Commit();
                        }
                    }
                    i = correspSmallestSpaces[i] + 1;
                    currentElement = e;
                }
                catch (Exception ex)
                {
                    break;
                }   
            }
        }
    }
}

// custom equality to compare list of SpatialElement
public class CusComparer : IEqualityComparer<List<SpatialElement>>
{
    public bool Equals(List<SpatialElement> x, List<SpatialElement> y)
    {
        bool eq = true;
        for (int i = 0; i < x.Count() && i < y.Count(); i += 1)
        {
            if (x.ElementAt(i).Number != y.ElementAt(i).Number)
                eq = false;
        }
        return eq;
    }

    public int GetHashCode(List<SpatialElement> obj)
    {
         int hashCode = 0;

        for (var index = 0; index < obj.Count; index++)
        {
            hashCode ^= new {Index = index, Item = obj[index]}.GetHashCode();
        }
        return hashCode;
    }
}

