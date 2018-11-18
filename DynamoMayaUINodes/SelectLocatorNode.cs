using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;
using Autodesk.Maya.OpenMaya;
using Dynamo.Controls;
using Dynamo.Graph;

using Dynamo.UI.Commands;
using Dynamo.Wpf;

using DynaMaya.Geometry;
using DynaMaya.NodeUI;
using ProtoCore.AST.AssociativeAST;
using DynaMaya.Nodes.Properties;
using DynaMaya.Util;
using Dynamo.Graph.Nodes;
using Newtonsoft.Json;

namespace DynaMaya.UINodes
{
 
    [NodeName("Get Selected Locator")]
    [NodeCategory("DynaMaya.Interop.Select")]

    [IsDesignScriptCompatible]
    public class SelectLocatorNode : NodeModel
    {
        #region private members
    
        private AssociativeNode _locatorLstNode = AstFactory.BuildNullNode();
        private AssociativeNode _locatorLstNameNode = AstFactory.BuildNullNode();
        private AssociativeNode _locatorLstRotationNode = AstFactory.BuildNullNode();
        private MSpace.Space _space = MSpace.Space.kWorld;
        private bool firstRun = true;
        private bool _hasBeenDeleted = false;
        private bool m_liveUpdate = false;
        private bool isFromUpdate = false;
        private bool isUpdating = false;
        private int updateInterval = 300;
        private string m_updateInterval = "50";
        private string m_mSpace = MSpace.Space.kWorld.ToString();
        private Dictionary<string, DMLocator> SelectedItems;
        private bool differUpdate = false;

        #endregion

        #region properties
        [IsVisibleInDynamoLibrary(false)]
        [JsonProperty(PropertyName = "mSpace")]
        public string mSpace
        {
            get
            {
                return m_mSpace;
            }
            set
            {
                m_mSpace = value;
                Enum.TryParse(m_mSpace, out _space);
                RaisePropertyChanged("NodeMessage");
            }
        }

        [IsVisibleInDynamoLibrary(false)]
        [JsonProperty(PropertyName = "liveUpdate")]
        public bool liveUpdate
        {
            get
            {
                return m_liveUpdate;
            }
            set
            {
                m_liveUpdate = value;
                if (m_liveUpdate)
                {
                    registerUpdateEvents();
                }
                else
                {
                    unRegisterUpdateEvents();
                }
                RaisePropertyChanged("liveUpdate");
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
        // Use the VMDataBridge to safely retrieve our input values
        #region databridge callback
        /// <summary>
        /// Register the data bridge callback.
        /// </summary>
        protected override void OnBuilt()
        {
            base.OnBuilt();
            VMDataBridge.DataBridge.Instance.RegisterCallback(GUID.ToString(), DataBridgeCallback);
            isUpdating = false;
        }



        /// <summary>
        /// Callback method for DataBridge mechanism.
        /// This callback only gets called when 
        ///     - The AST is executed
        ///     - After the BuildOutputAST function is executed 
        ///     - The AST is fully built
        /// </summary>
        /// <param name="data">The data passed through the data bridge.</param>
        private void DataBridgeCallback(object data)
        {
            try
            {
                var dataObj = data;
                //JsonConvert.SerializeObject(SelectedItems);

            }
            catch
            {
                Warning("DataBridge callback failed");
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
        public SelectLocatorNode()
        {
            // When you create a UI node, you need to do the
            // work of setting up the ports yourself. To do this,
            // you can populate the InPortData and the OutPortData
            // collections with PortData objects describing your ports.
            //InPortData.Add(new PortData("Space", Resources.DMInPortToolTip));

            // Nodes can have an arbitrary number of inputs and outputs.
            // If you want more ports, just create more PortData objects.
            OutPorts.Add(new PortModel(PortType.Output, this, new PortData("CoordinateSystem", "The locator as a Coordinate System ")));
            OutPorts.Add(new PortModel(PortType.Output, this, new PortData("Locator Name", "The name of the object in Maya")));
            
           

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
            
            Func<string, string, CoordinateSystem> func = DMLocator.ToDynamoElement;
            List<AssociativeNode> newInputs = null;
            List<AssociativeNode> newNameInputs = null;
            List<AssociativeNode> NewRotationInputs = null;

            if (SelectedItems == null || _hasBeenDeleted)
            {
                SelectedItems = new Dictionary<string, DMLocator>();
                _locatorLstNode = AstFactory.BuildNullNode();
                _locatorLstNameNode = AstFactory.BuildNullNode();
                _locatorLstRotationNode = AstFactory.BuildNullNode();

                _hasBeenDeleted = false;
            }
            else
            {
                if (SelectedItems.Count > 0)
                {
                    newInputs = new List<AssociativeNode>(SelectedItems.Count);
                    newNameInputs = new List<AssociativeNode>(SelectedItems.Count);
                    foreach (var dag in SelectedItems.Values)
                    {
                        newInputs.Add(AstFactory.BuildFunctionCall(
                            func,
                            new List<AssociativeNode>
                            {
                                AstFactory.BuildStringNode(dag.DagNode.partialPathName),
                                AstFactory.BuildStringNode(m_mSpace)
                            }));

                        newNameInputs.Add(AstFactory.BuildStringNode(dag.DagShape.partialPathName));
                    
                    }

                    _locatorLstNode = AstFactory.BuildExprList(newInputs);
                    _locatorLstNameNode = AstFactory.BuildExprList(newNameInputs);

                }
                else
                    _locatorLstNode = AstFactory.BuildNullNode();


            }



            return new[]
            {

                AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(0), _locatorLstNode),
                AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(1), _locatorLstNameNode)

            };
        }

        internal void registerUpdateEvents()
        {
            if (SelectedItems != null)
            {
                foreach (KeyValuePair<string, DMLocator> itm in SelectedItems)
                {
                    itm.Value.AddEvents(itm.Value.DagShape);
                    itm.Value.Changed += MObjOnChanged;
                }
            }
        }

        internal void unRegisterUpdateEvents()
        {
            if (SelectedItems != null)
            {
                foreach (KeyValuePair<string, DMLocator> itm in SelectedItems)
                {
                    itm.Value.Changed -= MObjOnChanged;
                    itm.Value.RemoveEvents(itm.Value.DagShape);
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


            var DagObjectList = selectionList.DagPaths(MFn.Type.kLocator).ToList();
            SelectedItems = new Dictionary<string, DMLocator>(DagObjectList.Count);

            foreach (var dag in DagObjectList)
            {
                var itm = new DMLocator(dag, _space);
                itm.Changed += MObjOnChanged;
                itm.Deleted += MObjOnDeleted;
                SelectedItems.Add(itm.DagShape.partialPathName, itm);

            }



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
    public class PointNodeViewCustomization : INodeViewCustomization<SelectLocatorNode>
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
        public void CustomizeView(SelectLocatorNode model, NodeView nodeView)
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
        public void Dispose()
        {
           
        }
    }

}
