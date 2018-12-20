using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Autodesk.DesignScript.Geometry;
using Autodesk.Maya.OpenMaya;
using Dynamo.Controls;
using Dynamo.Graph;
using Dynamo.Graph.Nodes;
using Dynamo.UI.Commands;
using Dynamo.Wpf;
using DynaMaya.Geometry;
using DynaMaya.NodeUI;
using Autodesk.DesignScript.Runtime;
using ProtoCore.AST.AssociativeAST;
using DynaMaya.Util;
using Newtonsoft.Json;
using System.Collections;
using System.Runtime.CompilerServices;

namespace DynaMaya.UINodes
{

    [NodeName("Get Selected Locator")]
    [NodeCategory("DynaMaya.Interop.Select")]
    [NodeDescription("Select Maya Locator (as a point or CS)")]
    [IsDesignScriptCompatible]
    public class SelectLocatorNode : NodeModel
    {
        #region private members

        private AssociativeNode _locatorLstNode = AstFactory.BuildNullNode();
        private AssociativeNode _SelectedNameLstNode = AstFactory.BuildNullNode();
        private AssociativeNode _mayaLocator = AstFactory.BuildNullNode();
        private MSpace.Space space = MSpace.Space.kWorld;
        private bool firstRun = true;
        private bool hasBeenDeleted = false;
        private bool m_liveUpdate = false;
        private bool isFromUpdate = false;
        private bool isUpdating = false;

        private string m_mSpace = MSpace.Space.kWorld.ToString();
        private Dictionary<string, DMLocator> SelectedItems;
        private List<string> m_SelectedItemNames = new List<string>();
        private string m_SelectedItemNamesString = "";

        private Dictionary<string, AssociativeNode> dynamoObject = null;
        private Dictionary<string, AssociativeNode> locatorName = null;
        private Dictionary<string, AssociativeNode> mayaLocator = null;

        private Func<string, string, CoordinateSystem> dynamoElementFunc = DMLocator.ToDynamoElement;
       


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
                Enum.TryParse(m_mSpace, out space);
                RaisePropertyChanged("mSpace");
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
        [IsVisibleInDynamoLibrary(false)]
        [JsonProperty(PropertyName = "SelectedItemNamesString")]
        public string SelectedItemNamesString
        {
            get
            {
                return m_SelectedItemNamesString;
            }
            set
            {
                m_SelectedItemNamesString = value;
                DeserializeNameList(m_SelectedItemNamesString);

                RaisePropertyChanged("SelectedItemNamesString");
                OnNodeModified();
            }
        }

        private void DeserializeNameList(string nameListString)
        {
            var splitNames = nameListString.Split(',');
            if (splitNames.Length > 0)
            {
                SelectedItems = new Dictionary<string, DMLocator>(splitNames.Length);

                foreach (var name in splitNames)
                {
                    try
                    {
                        var itm = new DMLocator(DMInterop.getDagNode(name), space);
                        itm.Deleted += MObjOnDeleted;
                        itm.Changed += MObjOnChanged;
                        SelectedItems.Add(itm.dagName, itm);
                    }
                    catch
                    {
                        Warning($"Object {name} does not exist or is not valid");
                    }



                }
            }

        }

        /// <summary>
        /// DelegateCommand objects allow you to bind
        /// UI interaction to methods on your data context.
        /// </summary>
        [IsVisibleInDynamoLibrary(false), JsonIgnore]
        public DelegateCommand SelectBtnCmd { get; set; }

        [IsVisibleInDynamoLibrary(false), JsonIgnore]
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
        public SelectLocatorNode()
        {
            OutPorts.Add(new PortModel(PortType.Output, this, new PortData("CoordinateSystem", "The Dynamo Object")));
            OutPorts.Add(new PortModel(PortType.Output, this, new PortData("Locator Name", "The name of the object in Maya")));
            //OutPorts.Add(new PortModel(PortType.Output, this, new PortData("Maya Mesh", "This is the Maya Locator typology which gives you access to all of the maya properties")));

            RegisterAllPorts();
            ArgumentLacing = LacingStrategy.Shortest;
            CanUpdatePeriodically = true;
            PortDisconnected += Node_PortDisconnected;
            SelectBtnCmd = new DelegateCommand(SelectBtnClicked, isOk);
            ManualUpdateCmd = new DelegateCommand(ManualUpdateBtnClicked, isOk);

        }

        // Starting with Dynamo v2.0 you must add Json constructors for all nodeModel
        // dervived nodes to support the move from an Xml to Json file format.  Failing to
        // do so will result in incorrect ports being generated upon serialization/deserialization.
        // This constructor is called when opening a Json graph.
        [JsonConstructor]
        SelectLocatorNode(IEnumerable<PortModel> inPorts, IEnumerable<PortModel> outPorts) : base(inPorts, outPorts)
        {

        }

        // Restore default button/window text and trigger UI update
        private void Node_PortDisconnected(PortModel obj)
        {
            SelectedItems = new Dictionary<string, DMLocator>();

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
            this.ClearErrorsAndWarnings();


            if (SelectedItems == null || hasBeenDeleted)
            {
                //SelectedItems = new Dictionary<string, DMLocator>();
                _locatorLstNode = AstFactory.BuildNullNode();
                _SelectedNameLstNode = AstFactory.BuildNullNode();
               // _mayaLocator = AstFactory.BuildNullNode();
                hasBeenDeleted = false;
                //return Enumerable.Empty<AssociativeNode>();
            }
            else
            {
                if (SelectedItems.Count > 0)
                {
                    //only rebuild the entire list of geom if needed. otherwise this has been created and is built and updated as needed on only the geometry that has changed
                    if (!isFromUpdate)
                    {
                        dynamoObject = new Dictionary<string, AssociativeNode>(SelectedItems.Count);
                        locatorName = new Dictionary<string, AssociativeNode>(SelectedItems.Count);
                        //mayaLocator = new Dictionary<string, AssociativeNode>(SelectedItems.Count);

                        foreach (var dag in SelectedItems.Values)
                        {
                            buildAstNodes(dag.dagName);

                        }
                    }


                    _locatorLstNode = AstFactory.BuildExprList(dynamoObject.Values.ToList());
                    _SelectedNameLstNode = AstFactory.BuildExprList(locatorName.Values.ToList());
                    //_mayaLocator = AstFactory.BuildExprList(mayaLocator.Values.ToList());


                }
                else
                {
                    _locatorLstNode = AstFactory.BuildNullNode();
                }

            }

            isFromUpdate = false;

            return new[]
            {


                    AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(0), _locatorLstNode),
                    AstFactory.BuildAssignment(AstFactory.BuildIdentifier(AstIdentifierBase + "_dummy"),
                        VMDataBridge.DataBridge.GenerateBridgeDataAst(GUID.ToString(), AstFactory.BuildExprList(inputAstNodes))),

                    AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(1), _SelectedNameLstNode),
                    AstFactory.BuildAssignment(AstFactory.BuildIdentifier(AstIdentifierBase + "_dummy"),
                        VMDataBridge.DataBridge.GenerateBridgeDataAst(GUID.ToString(), AstFactory.BuildExprList(inputAstNodes))),

            };
        }

        internal void buildAstNodes(string dagName)
        {
            locatorName.Add(dagName, AstFactory.BuildStringNode(dagName));
            /*
            mayaLocator.Add(dagName, AstFactory.BuildFunctionCall(
                MayaElementFunc,
                new List<AssociativeNode>
                {
                    AstFactory.BuildStringNode(dagName),
                    AstFactory.BuildStringNode(m_mSpace)
                }));
                */
            dynamoObject.Add(dagName, AstFactory.BuildFunctionCall(
                dynamoElementFunc,
                new List<AssociativeNode>
                {
                    AstFactory.BuildStringNode(dagName),
                    AstFactory.BuildStringNode(m_mSpace)
                }));
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
            // VMDataBridge.DataBridge.Instance.UnregisterCallback(GUID.ToString());
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
            MGlobal.getActiveSelectionList(selectionList, true);

            if (selectionList.isEmpty)
            {
                SelectedItems = null;
                OnNodeModified(true);
                return;
            }

            var TransObjectList = selectionList.DagPaths(MFn.Type.kTransform).ToList();
            var DagObjectList = selectionList.DagPaths(MFn.Type.kLocator).ToList();
            SelectedItems = new Dictionary<string, DMLocator>(DagObjectList.Count);
            m_SelectedItemNames = new List<string>(DagObjectList.Count);

            foreach (var dag in TransObjectList)
            {
                if (dag.hasFn(MFn.Type.kLocator))
                {
                    var itm = new DMLocator(dag, space);
                    itm.Renamed += Itm_Renamed;
                    itm.Deleted += MObjOnDeleted;
                    SelectedItems.Add(itm.dagName, itm);
                    m_SelectedItemNames.Add(itm.dagName);
                    // ct++;
                }
            }
            
            m_SelectedItemNamesString = ConvertStringListToString(m_SelectedItemNames);

            OnNodeModified(true);
        }

        internal void rebuildItemNameList(bool updateNameListString = true)
        {
            m_SelectedItemNames = new List<string>(SelectedItems.Count);
            foreach (var itm in SelectedItems)
            {
                m_SelectedItemNames.Add(itm.Value.dagName);
            }

            if (updateNameListString)
            {
                ConvertStringListToString(m_SelectedItemNames);
            }
        }

        internal static string ConvertStringListToString(List<string> stringList)
        {
            if (stringList.Count > 0)
            {

            string listAsString = "";
            for (int i = 0; i < stringList.Count - 1; i++)
            {
                listAsString += stringList[i] + ",";
            }
            listAsString += stringList[stringList.Count - 1];
            return listAsString;

            }
            MGlobal.displayWarning("selected items are invalid");
            return "";
        }

        internal void MObjOnDeleted(object sender, MFnDagNode dagNode)
        {

            isFromUpdate = true;
            if (SelectedItems != null && SelectedItems.Count > 0)
            {
                var dag = (DMLocator)sender;
                if (SelectedItems.Count == 1)
                {
                    hasBeenDeleted = true;
                    SelectedItems = null;
                    OnNodeModified(true);
                    return;
                }


                if (SelectedItems.ContainsKey(dag.dagName))
                {
                    SelectedItems.Remove(dag.dagName);
                    dynamoObject.Remove(dag.dagName);
                    mayaLocator.Remove(dag.dagName);
                    locatorName.Remove(dag.dagName);


                }


                OnNodeModified(true);
            }
        }

        internal void MObjOnChanged(object sender, MFnDagNode dagNode)
        {
            if (!isUpdating)
            {
                isUpdating = true;

                var dag = new DMLocator(dagNode.dagPath, space);

                //  if (liveUpdate)
                //  {
                isFromUpdate = true;

                if (SelectedItems.ContainsKey(dag.dagName))
                {
                    SelectedItems[dag.dagName] = dag;


                    dynamoObject[dag.dagName] = AstFactory.BuildFunctionCall(
                                dynamoElementFunc,
                                new List<AssociativeNode>
                                {
                                AstFactory.BuildStringNode(dag.dagName),
                                AstFactory.BuildStringNode(m_mSpace)
                                });
                    /*
                    mayaLocator[dag.dagName] = AstFactory.BuildFunctionCall(
                        MayaElementFunc,
                        new List<AssociativeNode>
                        {
                                AstFactory.BuildStringNode(dag.dagName),
                                AstFactory.BuildStringNode(m_mSpace)
                        });
                    */
                }

                OnNodeModified(true);
            
            }


        }

        private void Itm_Renamed(object sender, MFnDagNode dagNode)
        {
            //ToDo: get the rename system working
            var dag = (DMLocator)sender;
            if (SelectedItems.ContainsKey(dag.dagName))
            {
                string newName = dagNode.partialPathName;
                string oldName = dag.dagName;

                isFromUpdate = true;
                SelectedItems.Remove(oldName);
                locatorName.Remove(oldName);
                mayaLocator.Remove(oldName);

                var DMLocator = new DMLocator(dagNode.dagPath, m_mSpace);
                SelectedItems.Add(newName, DMLocator);

                buildAstNodes(newName);

                rebuildItemNameList();

                OnNodeModified(true);

            }
            dag.Dispose();
        }

        #endregion

        #region command methods

        internal static bool isOk(object obj)
        {
            return true;
        }

        [IsVisibleInDynamoLibrary(false)]
        public void SelectBtnClicked(object obj)
        {
            GetNewGeom();
        }

        [IsVisibleInDynamoLibrary(false)]
        public void ManualUpdateBtnClicked(object obj)
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
    public class LocatorNodeViewCustomization : INodeViewCustomization<SelectLocatorNode>
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
        public void Dispose() { }
    }

}
