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

namespace BIMAutomate
{
    public class Drawer
    {
        private Application app;
        private UIApplication uiApplication;
        private UIDocument uidoc;
        private Document doc;
        private Element currentElement;
        private List<XYZ> currentEdges;
        private List<Duct> Ducts = new List<Duct>();
        private Dictionary<string, Connector> currentConnectors = new Dictionary<string, Connector>();
        private Dictionary<string, XYZ> EdgesOfDuct = new Dictionary<string, XYZ>();
        private Connector currentConn = null;
        public double diameter = 170 /305.8;

        public Drawer(UIApplication uiapp)
        {
            uiApplication = uiapp;
            app = uiapp.Application;
            uidoc = uiapp.ActiveUIDocument;
            doc = uidoc.Document;
            Utils.lazyInstance.SetEnv(uiapp);
            Debug.WriteLine("++Drawer constructor passed!");
        }

        #region TEST type of view
        // test
        public void GetViewType(Autodesk.Revit.DB.View view)
        {
            // Get the view type of the given view, and format the prompt string
            String prompt = "The view is ";

            switch (view.ViewType)
            {
                case ViewType.AreaPlan:
                    prompt += "an area view.";
                    break;
                case ViewType.CeilingPlan:
                    prompt += "a reflected ceiling plan view.";
                    break;
                case ViewType.ColumnSchedule:
                    prompt += "a column schedule view.";
                    break;
                case ViewType.CostReport:
                    prompt += "a cost report view.";
                    break;
                case ViewType.Detail:
                    prompt += "a detail view.";
                    break;
                case ViewType.DraftingView:
                    prompt += "a drafting view.";
                    break;
                case ViewType.DrawingSheet:
                    prompt += "a drawing sheet view.";
                    break;
                case ViewType.Elevation:
                    prompt += "an elevation view.";
                    break;
                case ViewType.EngineeringPlan:
                    prompt += "an engineering view.";
                    break;
                case ViewType.FloorPlan:
                    prompt += "afloor plan view.";
                    break;
                case ViewType.Internal:
                    prompt += "Revit's internal view.";
                    break;
                case ViewType.Legend:
                    prompt += "a legend view.";
                    break;
                case ViewType.LoadsReport:
                    prompt += "a loads report view.";
                    break;
                case ViewType.PanelSchedule:
                    prompt += "a panel schedule view.";
                    break;
                case ViewType.PresureLossReport:
                    prompt += "a pressure loss report view.";
                    break;
                case ViewType.Rendering:
                    prompt += "a rendering view.";
                    break;
                case ViewType.Report:
                    prompt += "a report view.";
                    break;
                case ViewType.Schedule:
                    prompt += "a schedule view.";
                    break;
                case ViewType.Section:
                    prompt += "a cross section view.";
                    break;
                case ViewType.ThreeD:
                    prompt += "a 3-D view.";
                    break;
                case ViewType.Undefined:
                    prompt += "an undefined/unspecified view.";
                    break;
                case ViewType.Walkthrough:
                    prompt += "a walkthrough view.";
                    break;
                default:
                    break;
            }

            // Give the user some information
            Autodesk.Revit.UI.TaskDialog.Show("Revit", prompt);
        }
        #endregion


        public void Draw()
        {
            #region winform running..
            /*
            try
            {
                System.Windows.Forms.Application.EnableVisualStyles();
        
            }
            catch {}
            try
            {
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            }
            catch {}
            try
            {
                System.Windows.Forms.Application.Run(new AutomateForm(doc, uidoc));
            }
            catch {}
            */
            #endregion


            FilteredElementCollector spaceColl = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_MEPSpaces);
            //  LabelUtils.GetLabelFor(BuiltInParameter.FAMILY_ELEM_SUBCATEGORY);
            XYZ sp = null, ep = null;

            ElementId levelId = ElementId.InvalidElementId;
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            Document document = uidoc.Document;
            int i = 0;
            RevitDataContext.lazyInstance.SpacesInfo = new Dictionary<Space, Tuple<XYZ, List<XYZ>>>();
            foreach (ElementId id in selectedIds)
            {
                Element e = document.GetElement(id);
                //Debug.WriteLine(e.Name);
                if (e is Space)
                {
                    Space space = e as Space;
                    if (space.Name.Contains("GAINE"))
                    {
                        Utils.lazyInstance.GetElementLocation(out sp, space);
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
                        RevitDataContext.lazyInstance.SpacesInfo.Add(space, new Tuple<XYZ, List<XYZ>>(Utils.lazyInstance.GetCenterOfPolygon(Edges), Edges));
                    }
                }
            }
            ep = sp;
            CreateDucts();
        }

        private void CreateDucts()
        {
            bool nextIsTilting = false;
            ElementId levelId = ElementId.InvalidElementId;
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            Document document = uidoc.Document;
            int i = 0;

            foreach (ElementId id in selectedIds)
            {
                Element e = document.GetElement(id);
                if (e is Space)
                {
                    Space space = e as Space;
                    if (space.Name.Contains("GAINE"))
                    {
                        SpatialElementGeometryCalculator calculator = new SpatialElementGeometryCalculator(document);
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

                        try
                        {
                            Level currentLevel = null;
                            FilteredElementCollector collector = new FilteredElementCollector(document);
                            //Get the duct Type
                            FilteredElementCollector collector1 = new FilteredElementCollector(document);
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
                            */
                                if (elem.Name == "Raccord avec té et coude lisse")
                                    ductType = elem as DuctType;
                            }
                            FilteredElementCollector collector2 = new FilteredElementCollector(document);
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
                                    Debug.WriteLine(e1.Name);
                                    ..
                                    **/
                                if (e1.Name == "Extraction")
                                    mechType = e1 as MechanicalSystemType;
                            }
                            if (currentLevel != null)
                            {
                                if (currentLevel.Name.Equals(Utils.lazyInstance.levels.LastOrDefault().Name))
                                    i = 0;
                            }

                            /**
                             *  Get next space 
                             **/
                            bool GetMe = false;
                            Space nextSpace = null;
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

                            if (GetMe == true)
                            {
                                var kvp1 = RevitDataContext.lazyInstance.SpacesInfo.ElementAt(at);
                                Level level = e.Document.GetElement(kvp1.Key.LevelId) as Level;
                                Utils.lazyInstance.GetLowestAndHighestPolygon(kvp1.Value.Item2, level);

                                List<XYZ> sortedLowestPoly = Utils.lazyInstance.lowestPolyg.ElementAt(0).Value
                                                             .OrderBy(x => x.X).ThenBy(x => x.Y).ToList();
                                Utils.lazyInstance.lowestPolyg.ElementAt(0).Value.Sort();
                                foreach (var item in Utils.lazyInstance.lowestPolyg.ElementAt(0).Value)
                                    Debug.WriteLine(item.X + ", " + item.Y + ", " + item.Z);

                                var kvp2 = RevitDataContext.lazyInstance.SpacesInfo.ElementAt(at + 1);
                                Level nextlevel = e.Document.GetElement(kvp2.Key.LevelId) as Level;
                                Utils.lazyInstance.GetLowestAndHighestPolygon(kvp2.Value.Item2, nextlevel);

                                XYZ Nsp1 = kvp2.Value.Item2.ElementAt(0), Nsp2 = kvp2.Value.Item2.ElementAt(0),
                                    Nsp3 = kvp2.Value.Item2.ElementAt(0), Nsp4 = kvp2.Value.Item2.ElementAt(0),
                                    Nsp5 = kvp2.Value.Item2.ElementAt(0), Nsp6 = kvp2.Value.Item2.ElementAt(0),
                                    Nsp7 = kvp2.Value.Item2.ElementAt(0), Nsp8 = kvp2.Value.Item2.ElementAt(0);

                                foreach (var summit in Utils.lazyInstance.lowestPolyg.ElementAt(0).Value)
                                {

                                }
                                foreach (var summit in Utils.lazyInstance.highestPolyg.Values)
                                {

                                }


                            }
                            currentLevel = e.Document.GetElement(e.LevelId) as Level;
                            XYZ startingPoint;
                            XYZ endingPoint;

                            /**
                             *  Get the starting point shifted depending on next space location
                             * */
                             
                            //startingPoint = new XYZ(startingPoint.X + 1 , )

                            Utils.lazyInstance.GetLowestAndHighestPolygon(Edges, currentLevel);
                            startingPoint = Utils.lazyInstance.GetCenterOfPolygon(Utils.lazyInstance.lowestPolyg[currentLevel]);
                            //startingPoint = new XYZ(Utils.lazyInstance.GetCenterOfPolygon(Utils.lazyInstance.lowestPolyg[currentLevel]).X, Utils.lazyInstance.GetCenterOfPolygon(Utils.lazyInstance.lowestPolyg[currentLevel]).Y, Utils.lazyInstance.GetCenterOfPolygon(Utils.lazyInstance.highestPolyg[currentLevel]).Z);
                            endingPoint = Utils.lazyInstance.GetCenterOfPolygon(Utils.lazyInstance.highestPolyg[currentLevel]);
                            Duct duct;
                            Connector ductStart;
                            Connector ductEnd;
                            using (Transaction tr = new Transaction(document))
                            {
                                tr.Start("Create New Duct");
                                {
                                    duct = Duct.Create(doc, mechType.Id, ductType.Id, currentLevel.Id, startingPoint, endingPoint);
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
                                    currentConnectors.Add("start" + i, ductStart);
                                    currentConnectors.Add("end" + i, ductEnd);
                                    i += 1;
                                }
                                tr.Commit();
                            }

                            if (i != 1)
                            {
                                Duct ductConnection;
                                List<Connector> lC2 = new List<Connector>();
                                var list = currentConnectors.Keys.ToList();
                                list.Sort();
                                using (Transaction tr2 = new Transaction(document))
                                {
                                    tr2.Start("Create Connection Duct");
                                    {
                                        ductConnection = Duct.Create(doc, ductType.Id, currentLevel.Id, currentConnectors[list.ElementAt(0)], currentConnectors[list.ElementAt(3)]);

                                        foreach (Connector conn in ductConnection.ConnectorManager.Connectors)
                                        {
                                            if (conn.ConnectorType == ConnectorType.End)
                                                lC2.Add(conn);
                                        }
                                        int index = i - 2;
                                        currentConnectors.Remove("start" + index);
                                        currentConnectors.Remove("end" + index);
                                    }
                                    tr2.Commit();
                                }

                                Connector beforeCurrentDuctStart;
                                Connector beforeCurrentDuctEnd;
                                Connector intermediateDuctStart;
                                Connector intermediateDuctEnd;

                                using (Transaction tr3 = new Transaction(document))
                                {
                                    tr3.Start("Create Elbow Fitting Duct");
                                    {

                                        List<Connector> lC3 = new List<Connector>();
                                        foreach (Connector conn in Ducts.Last().ConnectorManager.Connectors)
                                        {
                                            if (conn.ConnectorType == ConnectorType.End)
                                                lC3.Add(conn);
                                        }

                                        if (lC3.ElementAt(0).Origin.Z > lC3.ElementAt(1).Origin.Z)
                                        {
                                            beforeCurrentDuctStart = lC3.ElementAt(1);
                                            beforeCurrentDuctEnd = lC3.ElementAt(0);
                                        }
                                        else
                                        {
                                            beforeCurrentDuctStart = lC3.ElementAt(0);
                                            beforeCurrentDuctEnd = lC3.ElementAt(1);
                                        }

                                        List<Connector> lC4 = new List<Connector>();
                                        foreach (Connector conn in ductConnection.ConnectorManager.Connectors)
                                        {
                                            if (conn.ConnectorType == ConnectorType.End)
                                                lC4.Add(conn);
                                        }

                                        if (lC4.ElementAt(0).Origin.Z > lC4.ElementAt(1).Origin.Z)
                                        {
                                            intermediateDuctStart = lC4.ElementAt(1);
                                            intermediateDuctEnd = lC4.ElementAt(0);
                                        }
                                        else
                                        {
                                            intermediateDuctStart = lC4.ElementAt(0);
                                            intermediateDuctEnd = lC4.ElementAt(1);
                                        }

                                        try
                                        {
                                            doc.Create.NewElbowFitting(beforeCurrentDuctEnd, intermediateDuctStart);
                                            Debug.WriteLine("Elbow fitting created : intermediateDuct  / beforeCurrentDuct");
                                        }
                                        catch (Exception ex)
                                        {
                                       //     Debug.WriteLine("Exception: " + ex.ToString());
                                            Debug.WriteLine("Connection FAILED :  intermediateDuct  / beforeCurrentDuct");
                                        }

                                        try
                                        {
                                            doc.Create.NewElbowFitting(intermediateDuctEnd, ductStart);
                                            Debug.WriteLine("Elbow fitting created : intermediateDuct  / newDuct");
                                        }
                                        catch (Exception ex)
                                        {
                                          //  Debug.WriteLine("Exception: " + ex.ToString());
                                            Debug.WriteLine("Connection FAILED :  intermediateDuct  / newDuct");
                                        }
                                    }
                                    tr3.Commit();
                                }
                                
                                using (Transaction tr4 = new Transaction(document))
                                {
                                    tr4.Start("Create  Connection Between Ducts");
                                    {
                                        try
                                        {
                                            //beforeCurrentDuctStart.ConnectTo(intermediateDuctEnd);
                                            Debug.WriteLine("Connection PASSED : intermediateDuct / before currentDuct ");
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine("Exception: " + ex.ToString());
                                            Debug.WriteLine("Connection FAILED : intermediateDuct / before currentDuct ");
                                        }

                                        try
                                        {
                                          //  intermediateDuctEnd.ConnectTo(ductStart);
                                            Debug.WriteLine("Connection PASSED: intermediateDuct / currentDuct ");
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine("Exception: " + ex.ToString());
                                            Debug.WriteLine("Connection FAILED : intermediateDuct / currentDuct ");
                                        }
                                        try
                                        {
                                          //  intermediateDuctEnd.ConnectTo(ductStart);
                                            Debug.WriteLine("Connection PASSED: intermediateDuct / currentDuct ");
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine("Exception: " + ex.ToString());
                                            Debug.WriteLine("Connection FAILED : intermediateDuct / currentDuct ");
                                        }
                                    }
                                    tr4.Commit();
                                }
                            }
                            Ducts.Add(duct);
                            currentEdges = Edges;
                            currentElement = e;
                            Utils.lazyInstance.lowestPolyg.Clear();
                            Utils.lazyInstance.highestPolyg.Clear();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Exception: " + ex.ToString());
                            Debug.WriteLine("Process canceled before error ");
                            break;
                        }
                    }
                }
            }
        }
    }
}