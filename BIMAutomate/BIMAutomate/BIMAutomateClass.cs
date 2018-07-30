using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Reflection;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Events;
using System.Diagnostics;


namespace BIMAutomate
{
    [TransactionAttribute(TransactionMode.Manual)]
    [RegenerationAttribute(RegenerationOption.Manual)]
    public class BIMAutomateClass : IExternalCommand
    {
        Drawer drawer;
        public BIMAutomateClass()
        {
           
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Debug.WriteLine("+++++++++++++DEBUG STARTING");
               // Get application and document objects
                UIApplication uiApp = commandData.Application;
                drawer = new Drawer(uiApp);
                Document doc = uiApp.ActiveUIDocument.Document;
                Debug.WriteLine("+++++++++++++DEBUG ENDING");

            }
            catch (Exception ex)
                {
                     message = ex.Message;
                     Debug.WriteLine("Execute() failed : " + message);
                     return Result.Failed;
                }
            return Result.Succeeded;
        }
        
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }   
        
        public Result OnStartup(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
