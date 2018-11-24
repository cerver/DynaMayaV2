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

    [NodeName("Get Selected Mesh")]
    [NodeCategory("DynaMaya.Interop.Select")]
    [NodeDescription("Select Maya Mesh")]
    [OutPortTypes("Mesh","string","MayaMesh")]
    [IsDesignScriptCompatible]
    public class SelectMeshNode : NodeModel
    {
        #region private members
    
        private AssociativeNode _meshLstNode = AstFactory.BuildNullNode();
        private AssociativeNode _SelectedNameLstNode = AstFactory.BuildNullNode();
        private AssociativeNode _mayaMesh = AstFactory.BuildNullNode();
        private MSpace.Space space = MSpace.Space.kWorld;
        private bool firstRun = true;
        private bool hasBeenDeleted = false;
        private bool m_liveUpdate = false;
        private bool isFromUpdate = false;
        private bool isUpdating = false;

        private string m_mSpace = MSpace.Space.kWorld.ToString();
        private Dictionary<string, DMMesh> SelectedItems;
        private List<string> m_SelectedItemNames = new List<string>();
        private string m_SelectedItemNamesString = "";

        private Dictionary<string ,AssociativeNode> dynamoMesh = null;
        private Dictionary<string, AssociativeNode> meshName = null;
        private Dictionary<string, AssociativeNode> mayaMesh = null;

        private Func<string, string, Mesh> dynamoElementFunc = DMMesh.ToDynamoElement;
        private Func<string, string, MFnMesh> MayaElementFunc = DMMesh.GetMayaMesh;


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
                if(m_liveUpdate)
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
                SelectedItems = new Dictionary<string, DMMesh>(splitNames.Length);
              
                foreach (var name in splitNames)
                {
                    try
                    {
                        var itm = new DMMesh(DMInterop.getDagNode(name), space);
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
              
            }catch
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
        public SelectMeshNode()
        {
            OutPorts.Add(new PortModel(PortType.Output, this, new PortData("Mesh", "The Dynamo Mesh (Will only show if the mesh is quad or triangle)")));
            OutPorts.Add(new PortModel(PortType.Output, this, new PortData("Mesh Name", "The name of the object in Maya")));
            OutPorts.Add(new PortModel(PortType.Output, this, new PortData("Maya Mesh", "This is the Maya Mesh typology which gives you access to all of the mesh including NGone mesh data")));

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
        SelectMeshNode(IEnumerable<PortModel> inPorts, IEnumerable<PortModel> outPorts) : base(inPorts, outPorts)
        {
            
        }

        // Restore default button/window text and trigger UI update
        private void Node_PortDisconnected(PortModel obj)
        {
            SelectedItems = new Dictionary<string, DMMesh>() ;

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
                //SelectedItems = new Dictionary<string, DMMesh>();
                _meshLstNode = AstFactory.BuildNullNode();
                _SelectedNameLstNode = AstFactory.BuildNullNode();
                _mayaMesh = AstFactory.BuildNullNode();
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
                        dynamoMesh = new Dictionary<string, AssociativeNode>(SelectedItems.Count);
                        meshName = new Dictionary<string, AssociativeNode>(SelectedItems.Count);
                        mayaMesh = new Dictionary<string, AssociativeNode>(SelectedItems.Count);

                        foreach (var dag in SelectedItems.Values)
                        {
                            buildAstNodes(dag.dagName);

                        }
                    }


                    _meshLstNode = AstFactory.BuildExprList(dynamoMesh.Values.ToList());
                    _SelectedNameLstNode = AstFactory.BuildExprList(meshName.Values.ToList());
                    _mayaMesh = AstFactory.BuildExprList(mayaMesh.Values.ToList());


                }
                else
                {
                    _meshLstNode = AstFactory.BuildNullNode();
                }

            }

            isFromUpdate = false;

            return new[]
            {


                    AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(0), _meshLstNode),
                    AstFactory.BuildAssignment(AstFactory.BuildIdentifier(AstIdentifierBase + "_dummy"),
                        VMDataBridge.DataBridge.GenerateBridgeDataAst(GUID.ToString(), AstFactory.BuildExprList(inputAstNodes))),

                    AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(1), _SelectedNameLstNode),
                    AstFactory.BuildAssignment(AstFactory.BuildIdentifier(AstIdentifierBase + "_dummy"),
                        VMDataBridge.DataBridge.GenerateBridgeDataAst(GUID.ToString(), AstFactory.BuildExprList(inputAstNodes))),

                    AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(2), _mayaMesh),
                    AstFactory.BuildAssignment(AstFactory.BuildIdentifier(AstIdentifierBase + "_dummy"),
                        VMDataBridge.DataBridge.GenerateBridgeDataAst(GUID.ToString(), AstFactory.BuildExprList(inputAstNodes)))


            };
        }

        internal void buildAstNodes(string dagName)
        {
            meshName.Add(dagName, AstFactory.BuildStringNode(dagName));

            mayaMesh.Add(dagName, AstFactory.BuildFunctionCall(
                MayaElementFunc,
                new List<AssociativeNode>
                {
                    AstFactory.BuildStringNode(dagName),
                    AstFactory.BuildStringNode(m_mSpace)
                }));

            dynamoMesh.Add(dagName, AstFactory.BuildFunctionCall(
                dynamoElementFunc,
                new List<AssociativeNode>
                {
                    AstFactory.BuildStringNode(dagName),
                    AstFactory.BuildStringNode(m_mSpace)
                }));
        }

        internal void registerUpdateEvents()
        {
            if(SelectedItems !=null)
            {
                foreach (KeyValuePair<string, DMMesh> itm in SelectedItems)
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
                foreach (KeyValuePair<string, DMMesh> itm in SelectedItems)
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
            MGlobal.getActiveSelectionList(selectionList,true);

            if(selectionList.isEmpty)
            {
                SelectedItems = null;
                OnNodeModified(true);
                return;
            }

            var TransObjectList = selectionList.DagPaths(MFn.Type.kTransform).ToList();
            var DagObjectList = selectionList.DagPaths(MFn.Type.kMesh).ToList();
            SelectedItems = new Dictionary<string, DMMesh>(DagObjectList.Count);
            m_SelectedItemNames = new List<string>(DagObjectList.Count);
         
            foreach (var dag in TransObjectList)
            {
                if (dag.hasFn(MFn.Type.kMesh))
                {
                    var itm = new DMMesh(dag, space);
                    itm.Renamed += Itm_Renamed;
                    itm.Deleted += MObjOnDeleted;
                    SelectedItems.Add(itm.dagName, itm);
                    m_SelectedItemNames.Add(itm.dagName);
                   // ct++;
                }
                else
                {
                    MGlobal.displayWarning($"Selected item is not a kMesh, it is a {dag.apiType}");
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
            string listAsString = "";
            for(int i=0; i<stringList.Count-1; i++)
            {
                listAsString += stringList[i] + ",";
            }
            listAsString += stringList[stringList.Count - 1];
            return listAsString;
        }

        internal void MObjOnDeleted(object sender, MFnDagNode dagNode)
        {

            isFromUpdate = true;
            if (SelectedItems != null && SelectedItems.Count>0)
            {
                var dag = (DMMesh)sender;
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
                    dynamoMesh.Remove(dag.dagName);
                    mayaMesh.Remove(dag.dagName);
                    meshName.Remove(dag.dagName);
                   
                    
                }
                
               
                OnNodeModified(true);
            }
        }

        internal void MObjOnChanged(object sender, MFnDagNode dagNode)
        {
            if (!isUpdating)
            {
                isUpdating = true;

                var dag = new DMMesh(dagNode.dagPath, space);

     
                    isFromUpdate = true;

                    if (SelectedItems.ContainsKey(dag.dagName))
                    {
                        SelectedItems[dag.dagName] = dag;


                        dynamoMesh[dag.dagName] = AstFactory.BuildFunctionCall(
                                    dynamoElementFunc,
                                    new List<AssociativeNode>
                                    {
                                AstFactory.BuildStringNode(dag.dagName),
                                AstFactory.BuildStringNode(m_mSpace)
                                    });

                        mayaMesh[dag.dagName] = AstFactory.BuildFunctionCall(
                            MayaElementFunc,
                            new List<AssociativeNode>
                            {
                                AstFactory.BuildStringNode(dag.dagName),
                                AstFactory.BuildStringNode(m_mSpace)
                            });

                    }

                    OnNodeModified(true);

            }
                

        }

        private void Itm_Renamed(object sender, MFnDagNode dagNode)
        {
            //ToDo: get the rename system working
            var dag = (DMMesh)sender;
            if (SelectedItems.ContainsKey(dag.dagName))
            {
                string newName = dagNode.partialPathName;
                string oldName = dag.dagName;

                isFromUpdate = true;
                SelectedItems.Remove(oldName);
                meshName.Remove(oldName);
                mayaMesh.Remove(oldName);

                var dmmesh = new DMMesh(dagNode.dagPath, m_mSpace);
                SelectedItems.Add(newName, dmmesh);

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
        public  void SelectBtnClicked(object obj)
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
