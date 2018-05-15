using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using System.Diagnostics;

namespace BIMAutomate
{
    public partial class AutomateForm : System.Windows.Forms.Form
    {
        private Document doc;
        private UIDocument uiDoc;
        private FilteredElementCollector families;
        private FilteredElementCollector familiesSystem;
        private ICollection<ElementId> elements;

        private IOrderedEnumerable<Family> familiesOrder;
        private IOrderedEnumerable<Family> familiesSysOrder;

        private List<string> famName;
        private List<string> famCat;

        private List<string> famNameSys;
        private List<string> famCatSys;


        private bool family;

        private System.Drawing.Point? prevPosition;
        private ToolTip tooltip;

        public AutomateForm(Document _doc, UIDocument _uiDoc)
        {
            InitializeComponent();
            doc = _doc;
            uiDoc = _uiDoc;
            famName = new List<string>();
            famCat = new List<string>();
            famNameSys = new List<string>();
            famCatSys = new List<string>();
            prevPosition = null;
            tooltip = new ToolTip();

            families = new FilteredElementCollector(doc).OfClass(typeof(Family));
            familiesSystem = new FilteredElementCollector(doc).OfClass(typeof(ElementType));
            family = false;
            familiesOrder = from Family fam in families orderby fam.FamilyCategory.Name ascending select fam;
           // familiesSysOrder = from Family fam in familiesSystem orderby fam.FamilyCategory.Name ascending select fam;
            Debug.WriteLine("++AutomateForm constructor passed!");
        }
        void FamilyImportButtonClick(object sender, EventArgs e)
        {
            famName.Clear();
            famCat.Clear();
            famCatSys.Clear();
            famNameSys.Clear();

            family = true;



            foreach (Family fam in familiesOrder)
            {
                famName.Add(fam.Name);
                famCat.Add(fam.FamilyCategory.Name);
            }

            int index = 0;

            treeView1.Nodes.Clear();
            foreach (string cat in famCat)
            {
                try
                {
                    if (treeView1.Nodes[cat].Name == cat)
                    {
                        treeView1.Nodes[cat].Nodes.Add(famName.ElementAt(index), famName.ElementAt(index));
                        treeView1.Nodes[cat].Nodes[famName.ElementAt(index)].Tag = index;
                    }
                }
                catch
                {
                    treeView1.Nodes.Add(cat, cat);
                    treeView1.Nodes[cat].Nodes.Add(famName.ElementAt(index), famName.ElementAt(index));
                    treeView1.Nodes[cat].Nodes[famName.ElementAt(index)].Tag = index;
                }
                index++;
            }
            /*
            foreach (var test in familiesSystem)
            {
                if (test.Name != null)
                {
                    if (test.Category != null)
                    {
                        if (test.Category.Name != null )
                            if (test.Category.Name == "Gaine")
                            {
                                Debug.Write(", Category db: " + test.Category);
                                Debug.Write("test.category.id: " + test.Category.Id);
                                Debug.Write(", Category: " + test.Category.Name);
                                Debug.Write(",test.id: " + test.Id);

                                Debug.WriteLine(", Name of elem:" + test.Name);
                                
                            }
                    }
                }
                */

        
        }


        void ElementSelectionButtonClick(object sender, EventArgs e)
        {
            famName.Clear();
            famCat.Clear();
            treeView1.Nodes.Clear();
            family = false;
            Selection sel = uiDoc.Selection;
            elements = sel.GetElementIds();
            foreach (ElementId elem in elements)
            {
                Element tmp = doc.GetElement(elem);
                if (tmp.Name == "")
                {
                    TaskDialog.Show("Attention", "Vous devez selectionner les éléments sur Revit avant de cliquer sur le button");
                    return;
                }
                famName.Add(tmp.Name);
            }
            int index = 0;
            foreach (string cat in famName)
            {
                try
                {
                    if (treeView1.Nodes[cat].Name == cat)
                    {
                    }
                }
                catch
                {
                    treeView1.Nodes.Add(cat, cat);
                    treeView1.Nodes[cat].Tag = index++;
                }
            }
        }

        private void CheckAllChildNodes(TreeNode treeNode, bool nodeChecked)
        {
            foreach (TreeNode node in treeNode.Nodes)
            {
                node.Checked = nodeChecked;
                if (node.Nodes.Count > 0)
                {
                    CheckAllChildNodes(node, nodeChecked);
                }
            }
        }

        void TreeView1AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Action != TreeViewAction.Unknown)
            {
                if (e.Node.Nodes.Count > 0)
                {
                    CheckAllChildNodes(e.Node, e.Node.Checked);
                }
            }
        }

        private void changeStateNodes(bool check)
        {
            for (int i = 0; i < treeView1.Nodes.Count; i++)
            {
                treeView1.Nodes[i].Checked = check;
                for (int j = 0; j < treeView1.Nodes[i].Nodes.Count; j++)
                {
                    treeView1.Nodes[i].Nodes[j].Checked = check;
                }
            }
        }

        void MainFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }
    }
}
