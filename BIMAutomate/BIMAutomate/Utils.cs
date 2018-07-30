// nadir.arbia@gmail.com
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

        public List<XYZ> lowestPolyg = new List<XYZ>();
        public List<XYZ> highestPolyg = new List<XYZ>();

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

        public void PurgeList(List<Tuple<Tuple<XYZ, XYZ>, Tuple<XYZ, XYZ>, XYZ>> l)
        {
            for (int i = 0; i < l.Count(); i += 1)
            {
                XYZ temp = l.ElementAt(i).Item3;
                for (int j = 0; j< l.Count(); j += 1)
                {
                    if (i != j)
                        if (l.ElementAt(j).Item3.X == l.ElementAt(i).Item3.X &&
                            l.ElementAt(j).Item3.Y == l.ElementAt(i).Item3.Y &&
                            l.ElementAt(j).Item3.Z == l.ElementAt(i).Item3.Z)
                            l.RemoveAt(j);
                }
            }
        }

        public List<Connector> GetClosestConnector(List<Connector> lp, XYZ pos)
        {
            /// projected in 2d
            /// sort  closest distance
            double d1x = lp.ElementAt(0).Origin.X;
            double d1y = lp.ElementAt(0).Origin.Y;

            double d2x = lp.ElementAt(1).Origin.X;
            double d2y = lp.ElementAt(1).Origin.Y;

            double posx = pos.X;
            double posy = pos.Y;

            double d11x = d1x - posx;
            double d11y = d1y - posy;

            double d22x = d2x - posx;
            double d22y = d2y - posy;

            double distance1 = d11x * d11x + d11y * d11y;
            double distance2 = d22x * d22x + d22y * d22y;


            if (distance1 < distance2)
                return lp;
            else
            {
                List<Connector> nlc = new List<Connector>();
                nlc.Add(lp.ElementAt(1));
                nlc.Add(lp.ElementAt(0));
                return nlc;
            }
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
            highestPolyg.Clear();
            lowestPolyg.Clear();

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
            lp.Sort(new XYZComparer());
            lp2.Sort(new XYZComparer());

            highestPolyg = lp;
            lowestPolyg = lp2;
        }
        public static XYZ FindLineIntersection(XYZ start1, XYZ end1, XYZ start2, XYZ end2)
        {

            double denom = ((end1.X - start1.X) * (end2.Y - start2.Y)) - ((end1.Y - start1.Y) * (end2.X - start2.X));

            //  AB & CD are parallel 
            //if (denom == 0)
            //    return PointF.Empty;

            double numer = ((start1.Y - start2.Y) * (end2.X - start2.X)) - ((start1.X - start2.X) * (end2.Y - start2.Y));

            double r = numer / denom;

            double numer2 = ((start1.Y - start2.Y) * (end1.X - start1.X)) - ((start1.X - start2.X) * (end1.Y - start1.Y));

            double s = numer2 / denom;

            if ((r < 0 || r > 1) || (s < 0 || s > 1))
                return null;

            return new XYZ(start1.X + (r * (end1.X - start1.X)), start1.Y + (r * (end1.Y - start1.Y)), 0);
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

        /// <summary>
        /// Return the bottom four XYZ corners of the given 
        /// bounding box in the XY plane at the minimum 
        /// Z elevation in the order lower left, lower 
        /// right, upper right, upper left:
        /// </summary>
        public static XYZ[] GetBottomCorners(
          BoundingBoxXYZ b)
        {
            double z = b.Min.Z;

            return new XYZ[] {
            new XYZ( b.Min.X, b.Min.Y, z ),
            new XYZ( b.Max.X, b.Min.Y, z ),
            new XYZ( b.Max.X, b.Max.Y, z ),
            new XYZ( b.Min.X, b.Max.Y, z )
          };
        }

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

    // X Y Z Comparer / Equality
    public class XYZComparer : IComparer<XYZ>
    {
        public int Compare(XYZ x, XYZ y)
        {
            if (((double)x.X == (double)y.X) && ((double)x.Y == (double)y.Y))
                return 0;
            if (((double)x.X > (double)y.X) || (((double)x.X == (double)y.X) && ((double)x.Y > (double)y.Y)))
                return -1;

            return 1;
        }
    }
    public class XYZEquality : IEqualityComparer<XYZ>
    {
        public bool Equals(XYZ x, XYZ y)
        {
            if (Object.ReferenceEquals(x, y))
                return true;

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            return (x.X == y.X && x.Y == y.Y && x.Z == y.Z);
        }

        public int GetHashCode(XYZ obj)
        {
            return obj.GetHashCode();
        }
    }
    
    public class UnorderedTupleComparer : IEqualityComparer<Tuple<Tuple<XYZ, XYZ>, Tuple<XYZ, XYZ>, XYZ>>
    {
        
        public bool Equals(Tuple<Tuple<XYZ, XYZ>, Tuple<XYZ, XYZ>, XYZ> x, Tuple<Tuple<XYZ, XYZ>, Tuple<XYZ, XYZ>, XYZ> y)
        {
            if (Object.ReferenceEquals(x, y))
                return true;

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            return (x.Item3.X == y.Item3.X && x.Item3.Y == y.Item3.Y && x.Item3.Z == y.Item3.Z);
        }

        public int GetHashCode(Tuple<Tuple<XYZ, XYZ>, Tuple<XYZ, XYZ>, XYZ> obj)
        {
            return obj.GetHashCode();
        }
    }
}


#endregion


