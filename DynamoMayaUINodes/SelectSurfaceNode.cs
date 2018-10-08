using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Xml;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;
using Autodesk.Maya.OpenMaya;
using Dynamo.Controls;
using Dynamo.Graph;
using Dynamo.Graph.Nodes;
using Dynamo.Models;
using Dynamo.Nodes;
using Dynamo.UI.Commands;
using Dynamo.Utilities;
using Dynamo.Wpf;

using DynaMaya.Geometry;
using DynaMaya.NodeUI;
using ProtoCore.AST.AssociativeAST;
using DynaMaya.Nodes.Properties;
using DynaMaya.Util;
//using Microsoft.Practices.Prism.Commands;


namespace DynaMaya.UINodes
{

    [NodeName("Get Selected Surface")]
    [NodeCategory("DynaMaya.Interop.Select")]

    // The description will display in the tooltip
    // and in the help window for the node.
   // [NodeDescription("CustomNodeModelDescription",typeof(SamplesLibraryUI.Properties.Resources))]

    // Add the IsDesignScriptCompatible attribute to ensure
    // that it gets loaded in Dynamo.
    [IsDesignScriptCompatible]
    public class SelectSurfacehNode : NodeModel
    {
        #region private members
    
        private AssociativeNode _AssocNodeListObject = AstFactory.BuildNullNode();
        private AssociativeNode _AssocNodeListName = AstFactory.BuildNullNode();
        private AssociativeNode _AssocNodeListMObject = AstFactory.BuildNullNode();
        private MSpace.Space space = MSpace.Space.kWorld;
        private bool firstRun = true;
        private bool _hasBeenDeleted = false;
        private bool differUpdate = false;
        private int updateInterval = 300;
        private string m_updateInterval = "50";
        private string m_mSpace = MSpace.Space.kWorld.ToString();
        private Dictionary<string, DMSurface> SelectedItems;
    
            #endregion

        #region properties
        [IsVisibleInDynamoLibrary(false)]
        public string mSpace
        {
            get
            {
                return m_mSpace;
            }
            set
            {
                m_mSpace = value;
                Enum.TryParse(m_mSpace, out space);
                RaisePropertyChanged("NodeMessage");
            }
        }
        /// <summary>
        /// DelegateCommand objects allow you to bind
        /// UI interaction to methods on your data context.
        /// </summary>
        [IsVisibleInDynamoLibrary(false)]
        public DelegateCommand SelectBtnCmd { get; set; }

        [IsVisibleInDynamoLibrary(false)]
        public DelegateCommand ManualUpdateCmd { get; set; }

        #endregion

        #region constructor

        /// <summary>
        /// The constructor for a NodeModel is used to create
        /// the input and output ports and specify the argument
        /// lacing.
        /// </summary>
        [IsVisibleInDynamoLibrary(false)]
        public SelectSurfacehNode()
        {
            // When you create a UI node, you need to do the
            // work of setting up the ports yourself. To do this,
            // you can populate the InPortData and the OutPortData
            // collections with PortData objects describing your ports.
            //InPortData.Add(new PortData("Space", Resources.DMInPortToolTip));

            // Nodes can have an arbitrary number of inputs and outputs.
            // If you want more ports, just create more PortData objects.

            OutPorts.Add(new PortModel(PortType.Output, this, new PortData("Surface", "The Maya Surface as a Dynamo Surface ")));
            OutPorts.Add(new PortModel(PortType.Output, this, new PortData("Surface Name", "The name of the object in Maya")));
            OutPorts.Add(new PortModel(PortType.Output, this, new PortData("Maya Surface", "This is the Maya Surface typology which gives you access to all of the Maya data")));

         

            // This call is required to ensure that your ports are
            // properly created.
            RegisterAllPorts();

            // The arugment lacing is the way in which Dynamo handles
            // inputs of lists. If you don't want your node to
            // support argument lacing, you can set this to LacingStrategy.Disabled.
            ArgumentLacing = LacingStrategy.Shortest;
            SelectBtnCmd = new DelegateCommand(SelectBtnClicked, isOk);
            ManualUpdateCmd = new DelegateCommand(ManualUpdateBtnClicked, isOk);
            this.CanUpdatePeriodically = true;

        }

        #endregion

        #region public methods

        /// <summary>
        /// If this method is not overriden, Dynamo will, by default
        /// pass data through this node. But we wouldn't be here if
        /// we just wanted to pass data through the node, so let's 
        /// try using the data.
        /// </summary>
        /// <param name="inputAstNodes"></param>
        /// <returns></returns>
        [IsVisibleInDynamoLibrary(false)]
        public override IEnumerable<AssociativeNode> BuildOutputAst(List<AssociativeNode> inputAstNodes)
        {

            Func<string, string, Surface> func = DMSurface.ToDynamoElement;
            Func<string, string, MFnNurbsSurface> MayaElementFunc = DMSurface.GetMayaObject;

            List<AssociativeNode> newInputs = null;
            List<AssociativeNode> newNameInputs = null;
            List<AssociativeNode> newMObjectInputs = null;

            if (SelectedItems == null || _hasBeenDeleted)
            {
                SelectedItems = new Dictionary<string, DMSurface>();
                _AssocNodeListObject = AstFactory.BuildNullNode();
                _AssocNodeListName = AstFactory.BuildNullNode();
                _AssocNodeListMObject = AstFactory.BuildNullNode();
                _hasBeenDeleted = false;
            }
            else
            {
                if (SelectedItems.Count > 0)
                {

                    newInputs = new List<AssociativeNode>(SelectedItems.Count);
                    newNameInputs = new List<AssociativeNode>(SelectedItems.Count);
                    newMObjectInputs = new List<AssociativeNode>(SelectedItems.Count);

                    foreach (var dag in SelectedItems.Values)
                    {
                        newInputs.Add(AstFactory.BuildFunctionCall(
                            func,
                            new List<AssociativeNode>
                            {
                                AstFactory.BuildStringNode(dag.DagNode.partialPathName),
                                AstFactory.BuildStringNode(m_mSpace)
                            }));

                        newMObjectInputs.Add(AstFactory.BuildFunctionCall(
                           MayaElementFunc,
                           new List<AssociativeNode>
                           {
                                AstFactory.BuildStringNode(dag.DagNode.partialPathName),
                                AstFactory.BuildStringNode(m_mSpace)
                           }));

                        newNameInputs.Add(AstFactory.BuildStringNode(dag.DagShape.partialPathName));
                    }

                    _AssocNodeListObject = AstFactory.BuildExprList(newInputs);
                    _AssocNodeListName = AstFactory.BuildExprList(newNameInputs);
                    _AssocNodeListMObject = AstFactory.BuildExprList(newMObjectInputs);

                }
                else
                    _AssocNodeListObject = AstFactory.BuildNullNode();


            }

          

            return new[]
            {

                AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(0), _AssocNodeListObject),
                    AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(1), _AssocNodeListName),
                    AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(2), _AssocNodeListMObject)

            };
        }

        protected override void SerializeCore(XmlElement nodeElement, SaveContext context)
        {
            base.SerializeCore(nodeElement, context);
            if (this.SelectedItems != null)
            {
                XmlElement nameElement = nodeElement.OwnerDocument.CreateElement("SurfaceItemNames");

                string nameList = "";
                foreach (var key in SelectedItems.Keys)
                {
                    nameList += key + ",";

                }
                nameElement.SetAttribute("value", nameList);
                nodeElement.AppendChild(nameElement);

                XmlElement spaceElement = nodeElement.OwnerDocument.CreateElement("SurfaceMspace");
                spaceElement.SetAttribute("value", mSpace);
                nodeElement.AppendChild(spaceElement);
            }

        }

        protected override void DeserializeCore(XmlElement nodeElement, SaveContext context)
        {
            base.DeserializeCore(nodeElement, context);

            // int index = -1;
            List<string> names = new List<string>();


            foreach (XmlNode subNode in nodeElement.ChildNodes)
            {
                string nameList = null;

                if (subNode.Name.Equals("SurfaceItemNames"))
                {

                    try
                    {

                        nameList = subNode.Attributes[0].Value;
                    }
                    catch
                    {
                        continue;
                    }

                    if (nameList != null)
                    {
                        names.AddRange(nameList.Split(','));
                        names.RemoveAt(names.Count - 1);
                    }



                }
                else if (subNode.Name.Equals("SurfaceMspace"))
                {
                    try
                    {
                        mSpace = subNode.Attributes[0].Value;

                    }
                    catch
                    {
                        continue;
                    }
                }

            }

            if (names.Count > 0)
            {
                AddItemsFromDeserialize(names);
            }
        }

        internal void AddItemsFromDeserialize(List<string> itms)
        {
            SelectedItems = new Dictionary<string, DMSurface>(itms.Count);

            for (int i = 0; i < itms.Count; i++)
            {
                try
                {
                    var tempCrv = new DMSurface(DMInterop.getDagNode(itms[i]), space);
                    SelectedItems.Add(itms[i], tempCrv);
                    tempCrv.Changed += MObjOnChanged;
                    tempCrv.Deleted += MObjOnDeleted;
                }
                catch (Exception)
                {
                    MGlobal.displayWarning(string.Format("the object {0} was not found and was removed from the selection list", itms[i]));
                }
            }

        }
        internal void GetNewGeom()
        {
            if (firstRun)
                firstRun = false;
            else
            {
                if (SelectedItems != null)
                {
                    foreach (var itm in SelectedItems.Values)
                    {
                        itm.Dispose();
                    }
                }
                    
            }


            MSelectionList selectionList = new MSelectionList();
            MGlobal.getActiveSelectionList(selectionList);

            
            var DagObjectList = selectionList.DagPaths(MFn.Type.kNurbsSurface).ToList();
            SelectedItems = new Dictionary<string, DMSurface>(DagObjectList.Count);

            foreach (var dag in DagObjectList)
            {
                var itm = new DMSurface(dag, space);
                itm.Changed += MObjOnChanged;
                itm.Deleted += MObjOnDeleted;
                SelectedItems.Add(itm.DagShape.partialPathName, itm);

            }

            if(firstRun)firstRun = false;

            OnNodeModified(true);
        }

        internal void MObjOnDeleted(object sender, MFnDagNode dagNode)
        {


            if (SelectedItems != null && SelectedItems.Count > 0)
            {

                if (SelectedItems.Count == 1)
                    _hasBeenDeleted = true;


                var uuidstr = dagNode.uuid().asString();
                if (SelectedItems.ContainsKey(uuidstr))
                {
                    SelectedItems[uuidstr].Dispose();
                    SelectedItems.Remove(uuidstr);
                }


                OnNodeModified(true);
            }
        }

        internal void MObjOnChanged(object sender, MFnDagNode dagNode)
        {
            if (!differUpdate)
                MarkNodeAsModified(true);
            //OnNodeModified(true);

        }

        #endregion

        #region command methods

        internal static bool isOk(object obj)
        {
            return true;
        }

        [IsVisibleInDynamoLibrary(false)]
        internal  void SelectBtnClicked(object obj)
        {
            GetNewGeom();
        
        }

   
        [IsVisibleInDynamoLibrary(false)]
        internal void ManualUpdateBtnClicked(object obj)
        {

            OnNodeModified(true);

        }
        #endregion

        [IsVisibleInDynamoLibrary(false)]
        public override void Dispose()
        {
            base.Dispose();
            
            if (SelectedItems != null)
                foreach (var itm in SelectedItems.Values)
                {
                    itm.Dispose();
                }
               
        }
    }

    /// <summary>
    ///     View customizer for CustomNodeModel Node Model.
    /// </summary>
    [IsVisibleInDynamoLibrary(false)]
    public class SurfaceNodeViewCustomization : INodeViewCustomization<SelectSurfacehNode>
    {
        /// <summary>
        /// At run-time, this method is called during the node 
        /// creation. Here you can create custom UI elements and
        /// add them to the node view, but we recommend designing
        /// your UI declaratively using xaml, and binding it to
        /// properties on this node as the DataContext.
        /// </summary>
        /// <param name="model">The NodeModel representing the node's core logic.</param>
        /// <param name="nodeView">The NodeView representing the node in the graph.</param>
        public void CustomizeView(SelectSurfacehNode model, NodeView nodeView)
        {
            // The view variable is a reference to the node's view.
            // In the middle of the node is a grid called the InputGrid.
            // We reccommend putting your custom UI in this grid, as it has
            // been designed for this purpose.

            // Create an instance of our custom UI class (defined in xaml),
            // and put it into the input grid.
            var SelectNodeControl = new SelectNodeUI();
            nodeView.inputGrid.Children.Add(SelectNodeControl);

            // Set the data context for our control to be this class.
            // Properties in this class which are data bound will raise 
            // property change notifications which will update the UI.
            SelectNodeControl.DataContext = model;
        }

        /// <summary>
        /// Here you can do any cleanup you require if you've assigned callbacks for particular 
        /// UI events on your node.
        /// </summary>
        public void Dispose() { }
    }

}
