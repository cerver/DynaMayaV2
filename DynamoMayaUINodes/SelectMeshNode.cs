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

namespace DynaMaya.UINodes
{

    [NodeName("Get Selected Mesh")]
    [NodeCategory("DynaMaya.Interop.Select")]

    [NodeDescription("Select Maya Mesh")]

    [OutPortTypes("Mesh","string","MayaMesh")]
   // [OutPortTypes("string")]
    // Add the IsDesignScriptCompatible attribute to ensure
    // that it gets loaded in Dynamo.
    [IsDesignScriptCompatible]
    public class SelectMeshNode : NodeModel
    {
        #region private members
    
        private AssociativeNode _meshLstNode = AstFactory.BuildNullNode();
        private AssociativeNode _SelectedNameLstNode = AstFactory.BuildNullNode();
        private AssociativeNode _mayaMesh = AstFactory.BuildNullNode();
        private MSpace.Space space = MSpace.Space.kWorld;
        private bool firstRun = true;
        private bool _hasBeenDeleted = false;
        private bool m_liveUpdate = false;

        private int updateInterval = 300;
        private string m_updateInterval = "50";
        private string m_mSpace = MSpace.Space.kWorld.ToString();
        private Dictionary<string, DMMesh> SelectedItems;
    
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

        [IsVisibleInDynamoLibrary(false)]
        public bool liveUpdate
        {
            get
            {
                return m_liveUpdate;
            }
            set
            {
                m_liveUpdate = value;
                RaisePropertyChanged("NodeMessage");
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
            ArrayList inputs = data as ArrayList;
            string inputText = "";
            foreach (var input in inputs)
            {
                inputText += input.ToString() + " ";
            }
            
           // WindowText = ("Data bridge callback of node " + GUID.ToString().Substring(0, 5) + ": " + inputText);
           // ButtonText = inputText;
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
            SelectBtnCmd = new DelegateCommand(SelectBtnClicked, isOk);
            ManualUpdateCmd = new DelegateCommand(ManualUpdateBtnClicked, isOk);
            this.CanUpdatePeriodically = true;

        }

        // Starting with Dynamo v2.0 you must add Json constructors for all nodeModel
        // dervived nodes to support the move from an Xml to Json file format.  Failing to
        // do so will result in incorrect ports being generated upon serialization/deserialization.
        // This constructor is called when opening a Json graph.
        [JsonConstructor]
        SelectMeshNode(IEnumerable<PortModel> inPorts, IEnumerable<PortModel> outPorts) : base(inPorts, outPorts)
        {
            this.PortDisconnected += Node_PortDisconnected;
            SelectBtnCmd = new DelegateCommand(SelectBtnClicked, isOk);
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
                                AstFactory.BuildStringNode(dag.dagName),
                                AstFactory.BuildStringNode(m_mSpace)
                            }));

                        mayaMesh.Add(AstFactory.BuildFunctionCall(
                            MayaElementFunc,
                            new List<AssociativeNode>
                            {
                                AstFactory.BuildStringNode(dag.dagName),
                                AstFactory.BuildStringNode(m_mSpace)
                            }));

                        newNameInputs.Add(AstFactory.BuildStringNode(dag.dagName));
                     

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

               /* AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(0), _meshLstNode),
                    AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(1), _SelectedNameLstNode),
                    AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(2), _mayaMesh),*/

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

    
        internal void GetNewGeom()
        {
            VMDataBridge.DataBridge.Instance.UnregisterCallback(GUID.ToString());
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

            var TransObjectList = selectionList.DagPaths(MFn.Type.kTransform).ToList();
            var DagObjectList = selectionList.DagPaths(MFn.Type.kMesh).ToList();
            SelectedItems = new Dictionary<string, DMMesh>(DagObjectList.Count);

         
            foreach (var dag in TransObjectList)
            {
                if (dag.hasFn(MFn.Type.kMesh))
                {
                    var itm = new DMMesh(dag, space);
                    //itm.Changed += MObjOnChanged;
                    //itm.Deleted += MObjOnDeleted;
                    SelectedItems.Add(itm.dagName, itm);
                   // ct++;
                }
            }



            OnNodeModified(true);
        }

        internal void MObjOnDeleted(object sender, MFnDagNode dagNode)
        {


            if (SelectedItems != null && SelectedItems.Count>0)
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
            if (liveUpdate)
            {
                OnNodeModified(true);
            }   
            else
            {
               MarkNodeAsModified(true);
            }
                

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
