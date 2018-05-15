using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BIMAutomate
{
    public class Utils
    {
        public static readonly Utils lazyInstance = new Utils();
        private UIApplication uiApplication;
        private UIDocument uidoc;
        private Document doc;
        public ICollection<Element> levels;

        public Dictionary<Level, List<XYZ>> lowestPolyg = new Dictionary<Level, List<XYZ>>();
        public Dictionary<Level, List<XYZ>> highestPolyg = new Dictionary<Level, List<XYZ>>();

        public void SetEnv(UIApplication uiapp)
        {
            uiApplication = uiapp;
            uidoc = uiapp.ActiveUIDocument;
            doc = uidoc.Document;
            GetLevelsOfCurrentView();
        }
        public static bool IsParallel(XYZ p, XYZ q)
        {
            return p.CrossProduct(q).IsZeroLength();
        }

        public static IOrderedEnumerable<Level> FindAndSortLevels(Document doc)
        {
            return new FilteredElementCollector(doc)
                            .WherePasses(new ElementClassFilter(typeof(Level), false))
                            .Cast<Level>()
                            .OrderBy(e => e.Elevation);
        }

        const string _caption = "Information";

        #region Formatting and message handlers
        /// <summary>
        /// MessageBox or Revit TaskDialog 
        /// wrapper for informational message.
        /// </summary>
        public static void InfoMsg(string msg)
        {
            Debug.WriteLine(msg);

            TaskDialog.Show(_caption, msg,
              TaskDialogCommonButtons.Ok);
        }

        /// <summary>   
        /// MessageBox or Revit TaskDialog 
        /// wrapper for error message.
        /// </summary>
        public static void ErrorMsg(string msg)
        {
            Debug.WriteLine(msg);

            TaskDialog d = new TaskDialog(_caption);
            d.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
            d.MainInstruction = msg;
            d.Show();
        }
        #endregion // Message handlers

        public void GetLevelsOfCurrentView()
        {
            Autodesk.Revit.DB.View active = uidoc.ActiveView;

            try
            {
                Parameter level = active.LookupParameter("Niveau de référence");
            }
            catch (NullReferenceException ex)
            {
                ;
            }
            try
            {
                Parameter level = active.LookupParameter("Associated Level");
            }
            catch (NullReferenceException ex)
            {
                Debug.WriteLine("Revit version is neither french neither english.");
            }

            FilteredElementCollector lvlCollector = new FilteredElementCollector(doc);
            ICollection<Element> lvlCollection = lvlCollector.OfClass(typeof(Level)).ToElements();
            levels = lvlCollection;
        }


        public void GetBoundaryFacesOfASpace(Space mySpace)
        {
            SpatialElementGeometryCalculator calculator = new SpatialElementGeometryCalculator(doc);

            // compute the room geometry
            SpatialElementGeometryResults results = calculator.CalculateSpatialElementGeometry(mySpace);

            // get the solid representing the room's geometry
            Solid roomSolid = results.GetGeometry();
            Debug.WriteLine("----- SurfaceArea Total: " + roomSolid.SurfaceArea + "------");



            //foreach (XYZ ii in edge.Tessellate())
            //    double faceArea = face.Area * 0.092903; // convert tofeeet
            // Debug.WriteLine("Surface BfaceArea:" + faceArea);
            // get the sub-faces for the face of the room


            //IList<SpatialElementBoundarySubface> subfaceList = results.GetBoundaryFaceInfo(face);
            //foreach (SpatialElementBoundarySubface subface in subfaceList)
            //{
            //    if (subfaceList.Count > 1) // there are multiple sub-faces that define the face
            //    {
            //        //  Debug.WriteLine(subface.GetSpatialElementFace());
            //        // get the area of each sub-face
            //        double subfaceArea = subface.GetSubface().Area;
            //        Debug.WriteLine("subfaceArea:" + subfaceArea);

            //        // sub-faces exist in situations such as when a room-bounding wall has been
            //        // horizontally split and the faces of each split wall combine to create the 
            //        // entire face of the room
            //    }
            //}
        }

        public XYZ GetCenterOfPolygon(List<XYZ> lxyz)
        {
            double x = 0, y = 0, z = 0;

            foreach (var p in lxyz)
            {
                x += p.X;
                y += p.Y;
                z += p.Z;
            }
            return new XYZ(x / lxyz.Count(), y / lxyz.Count(), z / lxyz.Count());
        }

        /// <summary>
        /// Member of the class are being set here, used as temporary variables
        /// </summary>
        /// <param name="lxyz"></param>
        public void GetLowestAndHighestPolygon(List<XYZ> lxyz, Level lvl)
        {
            XYZ xYZ1 = null;

            foreach (var p2 in lxyz)
            {
                if (xYZ1 == null)
                    xYZ1 = p2;
                if (p2.Z < xYZ1.Z)
                    xYZ1 = p2;
            }

            List<XYZ> lp = new List<XYZ>();
            List<XYZ> lp2 = new List<XYZ>();
            foreach (var p1 in lxyz)
            {
                if (p1.Z.Equals(xYZ1.Z))
                    lp.Add(p1);
                else if (p1.Z > xYZ1.Z)
                    lp2.Add(p1);
            }
            highestPolyg.Add(lvl, lp);
            lowestPolyg.Add(lvl, lp2);
        }

    public bool GetElementLocation(out XYZ p, Element e)
        {
            p = XYZ.Zero;
            bool rc = false;
            Location loc = e.Location;
            if (null != loc)
            {
                LocationPoint lp = loc as LocationPoint;
                if (null != lp)
                {
                    p = lp.Point;
                    rc = true;
                }
                else
                {
                    LocationCurve lc = loc as LocationCurve;

                    Debug.Assert(null != lc,
                      "expected location to be either point or curve");
                    Debug.WriteLine("lp is Null GetElementLocation() expected location to be either point or curve");
                    p = lc.Curve.GetEndPoint(0);
                    rc = true;
                }
            }
            return rc;
        }
    }


    public static class JtBoundingBoxXyzExtensionMethods
    {
        public static BoundingBoxXYZ GetBoundingBox(IList<IList<BoundarySegment>> boundary)
        {
            BoundingBoxXYZ bb = new BoundingBoxXYZ();
            double infinity = double.MaxValue;

            bb.Min = new XYZ(infinity, infinity, infinity);
            bb.Max = -bb.Min;

            foreach (IList<BoundarySegment> loop in boundary)
            {
                foreach (BoundarySegment seg in loop)
                {
                    Curve c = seg.GetCurve();
                    IList<XYZ> pts = c.Tessellate();
                    foreach (XYZ p in pts)
                    {
                        bb.ExpandToContain(p);
                    }
                }
            }
            return bb;
        }

        /// <summary>
        /// Expand the given bounding box to include 
        /// and contain the given point.
        /// </summary>
        public static void ExpandToContain(this BoundingBoxXYZ bb, XYZ p)
        {
            bb.Min = new XYZ(Math.Min(bb.Min.X, p.X),
                             Math.Min(bb.Min.Y, p.Y),
                             Math.Min(bb.Min.Z, p.Z));

            bb.Max = new XYZ(Math.Max(bb.Max.X, p.X),
                             Math.Max(bb.Max.Y, p.Y),
                             Math.Max(bb.Max.Z, p.Z));
        }


        /// <summary>
        /// Expand the given bounding box to include 
        /// and contain the given other one.
        /// </summary>
        public static void ExpandToContain(this BoundingBoxXYZ bb, BoundingBoxXYZ other)
        {
            bb.ExpandToContain(other.Min);
            bb.ExpandToContain(other.Max);
        }
        public static double[,] CalculateMatrixForGlobalToLocalCoordinateSystem(Face face)
        {
            // face.Evaluate uses a rotation matrix and
            // a displacement vector to translate points

            XYZ originDisplacementVectorUV = face.Evaluate(UV.Zero);
            XYZ unitVectorUWithDisplacement = face.Evaluate(UV.BasisU);
            XYZ unitVectorVWithDisplacement = face.Evaluate(UV.BasisV);

            XYZ unitVectorU = unitVectorUWithDisplacement
              - originDisplacementVectorUV;

            XYZ unitVectorV = unitVectorVWithDisplacement
              - originDisplacementVectorUV;

            // The rotation matrix A is composed of
            // unitVectorU and unitVectorV transposed.
            // To get the rotation matrix that translates from 
            // global space to local space, take the inverse of A.

            var a11i = unitVectorU.X;
            var a12i = unitVectorU.Y;
            var a21i = unitVectorV.X;
            var a22i = unitVectorV.Y;

            return new double[2, 2] {
        { a11i, a12i },
        { a21i, a22i }};
        }
        public static ConnectorManager GetConnectorManager(Element e)
        {
            MEPCurve mc = e as MEPCurve;
            FamilyInstance fi = e as FamilyInstance;

            if (null == mc && null == fi)
            {
                throw new ArgumentException(
                  "Element is neither an MEP curve nor a fitting.");
            }

            return null == mc
              ? fi.MEPModel.ConnectorManager
              : mc.ConnectorManager;
        }
        public static List<XYZ> GetPolygon(this EdgeArray ea)
        {
            int n = ea.Size;

            List<XYZ> polygon = new List<XYZ>(n);

            foreach (Edge e in ea)
            {
                IList<XYZ> pts = e.Tessellate();

                n = polygon.Count;

                if (0 < n)
                {
                    /*
                    Debug.Assert(pts[0]
                      .IsAlmostEqualTo(polygon[n - 1]),
                      "expected last edge end point to "
                      + "equal next edge start point");
                      */
                    polygon.RemoveAt(n - 1);
                }
                polygon.AddRange(pts);
            }
            n = polygon.Count;
            /*
            Debug.Assert(polygon[0]
              .IsAlmostEqualTo(polygon[n - 1]),
              "expected first edge start point to "
              + "equal last edge end point");
              */
            polygon.RemoveAt(n - 1);

            return polygon;
        }
        #region MEP utilities


        /// <summary>
        /// Return the element's connector at the given
        /// location, and its other connector as well, 
        /// in case there are exactly two of them.
        /// </summary>
        /// <param name="e">An element, e.g. duct, pipe or family instance</param>
        /// <param name="location">The location of one of its connectors</param>
        /// <param name="otherConnector">The other connector, in case there are just two of them</param>
        /// <returns>The connector at the given location</returns>
        static Connector GetConnectorAt(
          Element e,
          XYZ location,
          out Connector otherConnector)
        {
            otherConnector = null;

            Connector targetConnector = null;

            ConnectorManager cm = GetConnectorManager(e);

            bool hasTwoConnectors = 2 == cm.Connectors.Size;

            foreach (Connector c in cm.Connectors)
            {
                if (c.Origin.IsAlmostEqualTo(location))
                {
                    targetConnector = c;

                    if (!hasTwoConnectors)
                    {
                        break;
                    }
                }
                else if (hasTwoConnectors)
                {
                    otherConnector = c;
                }
            }
            return targetConnector;
        }

        /// <summary>
        /// Return the connector set element
        /// closest to the given point.
        /// </summary>
        static Connector GetConnectorClosestTo(
          ConnectorSet connectors,
          XYZ p)
        {
            Connector targetConnector = null;
            double minDist = double.MaxValue;

            foreach (Connector c in connectors)
            {
                double d = c.Origin.DistanceTo(p);

                if (d < minDist)
                {
                    targetConnector = c;
                    minDist = d;
                }
            }
            return targetConnector;
        }

        /// <summary>
        /// Return the connector on the element 
        /// closest to the given point.
        /// </summary>
        public static Connector GetConnectorClosestTo(
          Element e,
          XYZ p)
        {
            ConnectorManager cm = GetConnectorManager(e);

            return null == cm
              ? null
              : GetConnectorClosestTo(cm.Connectors, p);
        }

        /// <summary>
        /// Connect two MEP elements at a given point p.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if
        /// one of the given elements lacks connectors.
        /// </exception>
        public static void Connect(
          XYZ p,
          Element a,
          Element b)
        {
            ConnectorManager cm = GetConnectorManager(a);

            if (null == cm)
            {
                throw new ArgumentException(
                  "Element a has no connectors.");
            }

            Connector ca = GetConnectorClosestTo(
              cm.Connectors, p);

            cm = GetConnectorManager(b);

            if (null == cm)
            {
                throw new ArgumentException(
                  "Element b has no connectors.");
            }

            Connector cb = GetConnectorClosestTo(
              cm.Connectors, p);

            ca.ConnectTo(cb);
            //cb.ConnectTo( ca );
        }
        /*
        /// <summary>
        /// Compare Connector objects based on their location point.
        /// </summary>
        public class ConnectorXyzComparer : IEqualityComparer<Connector>
        {
            public bool Equals(Connector x, Connector y)
            {
                return null != x
                  && null != y
                  && IsEqual(x.Origin, y.Origin);
            }

            public int GetHashCode(Connector x)
            {
                return HashString(x.Origin).GetHashCode();
            }
        }

        /// <summary>
        /// Get distinct connectors from a set of MEP elements.
        /// </summary>
        public static HashSet<Connector> GetDistinctConnectors(
          List<Connector> cons)
        {
            return cons.Distinct(new ConnectorXyzComparer())
              .ToHashSet();
        }
        #endregion // MEP utilities
       
    }*/

    }

}

#endregion


