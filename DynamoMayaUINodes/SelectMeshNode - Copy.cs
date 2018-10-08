using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;
using Autodesk.Maya.OpenMaya;
using Dynamo.Controls;
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
using JetBrains.dotMemoryUnit;



namespace DynaMaya.UINodes
{

    [NodeName("Get Selected Mesh")]
    [NodeCategory("DynaMaya.Reference")]

    // The description will display in the tooltip
    // and in the help window for the node.
   // [NodeDescription("CustomNodeModelDescription",typeof(SamplesLibraryUI.Properties.Resources))]

    // Add the IsDesignScriptCompatible attribute to ensure
    // that it gets loaded in Dynamo.
    [IsDesignScriptCompatible]
    public class SelectMeshNode : DMNodeModel
    {
        #region private members
    
        private AssociativeNode _meshLstNode = AstFactory.BuildNullNode();
        private AssociativeNode _SelectedNameLstNode = AstFactory.BuildNullNode();
        private AssociativeNode _mayaMesh = AstFactory.BuildNullNode();
        private Dictionary<string, DMMesh> SelectedItems;
        internal string m_mSpace = MSpace.Space.kWorld.ToString();
        internal MSpace.Space space = MSpace.Space.kWorld;
        #endregion

        #region properties
        [IsVisibleInDynamoLibrary(false)]
        internal string mSpace
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


        [IsVisibleInDynamoLibrary(false)]
        internal string UpdateInterval
        {
            get
            {
                return m_updateInterval;
            }
            set
            {
                m_updateInterval = value;
                if (int.TryParse(m_updateInterval, out updateInterval))
                {
                    foreach (var itm in SelectedItems.Values)
                    {
                        itm.EventTimeInterval = updateInterval;
                    }
                    
                }
                RaisePropertyChanged("NodeMessage");
            }
        }
        #endregion

        #region constructor

        /// <summary>
        /// The constructor for a NodeModel is used to create
        /// the input and output ports and specify the argument
        /// lacing.
        /// </summary>
        [IsVisibleInDynamoLibrary(false)]
        public SelectMeshNode()
        {
            // When you create a UI node, you need to do the
            // work of setting up the ports yourself. To do this,
            // you can populate the InPortData and the OutPortData
            // collections with PortData objects describing your ports.
            //InPortData.Add(new PortData("Space", Resources.DMInPortToolTip));

            // Nodes can have an arbitrary number of inputs and outputs.
            // If you want more ports, just create more PortData objects.

            OutPortData.Add(new PortData("Mesh", Resources.DMOutPortToolTip));
            OutPortData.Add(new PortData("Mesh Name", Resources.DMOutPortToolTip));
            OutPortData.Add(new PortData("Maya Mesh", Resources.DMOutPortToolTip));

            // This call is required to ensure that your ports are
            // properly created.
            RegisterAllPorts();

            // The arugment lacing is the way in which Dynamo handles
            // inputs of lists. If you don't want your node to
            // support argument lacing, you can set this to LacingStrategy.Disabled.
            ArgumentLacing = LacingStrategy.Shortest;
            BtnCommand = new DelegateCommand(ButtonClicked, isOk);
            DifferCmd = new DelegateCommand(DifferClicked, isOk);
            ManualUpdateCmd = new DelegateCommand(ManualUpdate, isOk);


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

            Func<string, string, Mesh> dynamoElementFunc = DMMesh.ToDynamoElement;
            Func<string, string, MFnMesh> MayaElementFunc = DMMesh.GetMayaMesh;

            List<AssociativeNode> newInputs = null;
            List<AssociativeNode> newNameInputs = null;
            List<AssociativeNode> mayaMesh = null;

            
            if (SelectedItems == null || _hasBeenDeleted)
            {
                SelectedItems = new Dictionary<string, DMMesh>();
                _meshLstNode = AstFactory.BuildNullNode();
                _SelectedNameLstNode = AstFactory.BuildNullNode();
                _mayaMesh = AstFactory.BuildNullNode();
                _hasBeenDeleted = false;
            }
            else
            {
                if (SelectedItems.Count > 0)
                {

                   newInputs = new List<AssociativeNode>(SelectedItems.Count);
                   newNameInputs = new List<AssociativeNode>(SelectedItems.Count);
                   mayaMesh = new List<AssociativeNode>(SelectedItems.Count);

                    foreach (var dag in SelectedItems.Values)
                    {
                        newInputs.Add(AstFactory.BuildFunctionCall(
                            dynamoElementFunc,
                            new List<AssociativeNode>
                            {
                                AstFactory.BuildStringNode(dag.DagNode.partialPathName),
                                AstFactory.BuildStringNode(m_mSpace)
                            }));

                        mayaMesh.Add(AstFactory.BuildFunctionCall(
                            MayaElementFunc,
                            new List<AssociativeNode>
                            {
                                AstFactory.BuildStringNode(dag.DagNode.partialPathName),
                                AstFactory.BuildStringNode(m_mSpace)
                            }));

                        newNameInputs.Add(AstFactory.BuildStringNode(dag.DagPath.partialPathName));
                     

                    }

                    _meshLstNode = AstFactory.BuildExprList(newInputs);
                    _SelectedNameLstNode = AstFactory.BuildExprList(newNameInputs);
                    _mayaMesh = AstFactory.BuildExprList(mayaMesh);


                }
                else
                    _meshLstNode = AstFactory.BuildNullNode();


            }

          

            return new[]
            {

                AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(0), _meshLstNode),
                    AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(1), _SelectedNameLstNode),
                    AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(2), _mayaMesh)


            };
        }
             
        internal void GetNewGeom()
        {
            if (base.firstRun)
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

            
            var DagObjectList = selectionList.DagPaths(MFn.Type.kMesh).ToList();
            SelectedItems = new Dictionary<string, DMMesh>(DagObjectList.Count);

            foreach (var dag in DagObjectList)
            {
                var itm = new DMMesh(dag, space);
                itm.Changed += MObjOnChanged;
                itm.Deleted += MObjOnDeleted;
                SelectedItems.Add(itm.DagNode.uuid().asString(), itm);
  
            }

            if(firstRun)firstRun = false;

            OnNodeModified(true);
        }

        internal void MObjOnDeleted(object sender, MFnDagNode dagNode)
        {


            if (SelectedItems != null && SelectedItems.Count>0)
            {
             
                if (SelectedItems.Count == 1)
                    base._hasBeenDeleted = true;
             

                var uuidstr = dagNode.uuid().asString();
                if (SelectedItems.ContainsKey(uuidstr))
                {
                    SelectedItems[uuidstr].Dispose();
                    SelectedItems.Remove(uuidstr); 
                }
                
               
                OnNodeModified(true);
            }
        }

        [IsVisibleInDynamoLibrary(false)]
        public void MObjOnChanged(object sender, MFnDagNode dagNode)
        {
            if (!differUpdate)
                OnNodeModified(true);

        }

        #endregion

        #region command methods

        internal static bool isOk(object obj)
        {
            return true;
        }

        [IsVisibleInDynamoLibrary(false)]
        internal  void ButtonClicked(object obj)
        {
            GetNewGeom();
        
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
    public class MeshNodeViewCustomization : INodeViewCustomization<SelectMeshNode>
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
        public void CustomizeView(SelectMeshNode model, NodeView nodeView)
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
