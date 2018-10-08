using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;
using Autodesk.Maya.OpenMaya;
using Dynamo.Controls;
using Dynamo.Graph;
using Dynamo.Graph.Nodes;
using Dynamo.UI.Commands;
using Dynamo.Wpf;
using DynaMaya.Geometry;
using DynaMaya.NodeUI;
using DynaMaya.Util;
using ProtoCore.AST.AssociativeAST;
using DynaMaya.Nodes.Properties;
using Dynamo.Events;


namespace DynaMaya.UINodes
{
    [NodeName("Get Selected Curves")]
    [NodeCategory("DynaMaya.Interop.Select")]
    [IsDesignScriptCompatible]
    public class SelectCurveNode : NodeModel
    {
        #region private members
    
        private AssociativeNode _curveLstNode = AstFactory.BuildNullNode();
        private AssociativeNode _SelectedNameLstNode = AstFactory.BuildNullNode();
        private MSpace.Space _space = MSpace.Space.kWorld;
        private bool firstRun = true;
        private bool _hasBeenDeleted = false;

        private int updateInterval = 300;
        private string m_updateInterval = "50";
        private string m_mSpace = MSpace.Space.kWorld.ToString();
        private Dictionary<string, DMCurve> SelectedItems;

        private string currentFile = "";
        private bool differUpdate = false;

        private bool exacutionComplete = false;

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
                Enum.TryParse(m_mSpace, out _space);
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
        public SelectCurveNode()
        {

            OutPorts.Add(new PortModel(PortType.Output, this, new PortData("Curve", "The Maya Curve as a Dynamo Curve ")));
            OutPorts.Add(new PortModel(PortType.Output, this, new PortData("Curve Name", "The name of the object in Maya")));
           

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

            Dynamo.Events.ExecutionEvents.GraphPostExecution += ExecutionEvents_GraphPostExecution;
            ExecutionEvents.GraphPreExecution += ExecutionEvents_GraphPreExecution;

        }

        private void ExecutionEvents_GraphPreExecution(Dynamo.Session.IExecutionSession session)
        {
            exacutionComplete = false;
        }

        private void ExecutionEvents_GraphPostExecution(Dynamo.Session.IExecutionSession session)
        {
            //MessageBox.Show("has fully exacuted");
            exacutionComplete = true;


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
           
            Func<string, string, Curve> func = DMCurve.ToDynamoElement;
            List<AssociativeNode> newInputs = null;
            List<AssociativeNode> newNameInputs = null;

           

            if (SelectedItems == null || _hasBeenDeleted)
            {
                SelectedItems = new Dictionary<string, DMCurve>();
                _curveLstNode = AstFactory.BuildNullNode();
                _SelectedNameLstNode = AstFactory.BuildNullNode();
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

                    _curveLstNode = AstFactory.BuildExprList(newInputs);
                    _SelectedNameLstNode = AstFactory.BuildExprList(newNameInputs);

                }
                else
                    _curveLstNode = AstFactory.BuildNullNode();


            }



            return new[]
            {

                AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(0), _curveLstNode),
                    AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(1), _SelectedNameLstNode)

            };
        }

        protected override void SerializeCore(XmlElement nodeElement, SaveContext context)
        {
            base.SerializeCore(nodeElement, context);
            if (this.SelectedItems != null)
            {
                XmlElement nameElement = nodeElement.OwnerDocument.CreateElement("CurveItemNames");
 
                string nameList = "";
                foreach (var key in SelectedItems.Keys)
                {
                    nameList += key + ",";
                  
                }
                nameElement.SetAttribute("value", nameList);
                nodeElement.AppendChild(nameElement);

                XmlElement spaceElement = nodeElement.OwnerDocument.CreateElement("CurveMspace");
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

                if (subNode.Name.Equals("CurveItemNames"))
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
                        names.RemoveAt(names.Count-1);
                    }

 

                }
                else if (subNode.Name.Equals("CurveMspace"))
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

            if (names.Count>0)
            {
                AddItemsFromDeserialize(names);
            }
        }

        internal void AddItemsFromDeserialize(List<string> itms )
        {
            SelectedItems = new Dictionary<string, DMCurve>(itms.Count);

            for (int i=0; i<itms.Count; i++)
            {
                try
                {
                    var tempCrv = new DMCurve(DMInterop.getDagNode(itms[i]), _space);
                    SelectedItems.Add(itms[i], tempCrv);
                    tempCrv.Changed += MObjOnChanged;
                    tempCrv.Deleted += MObjOnDeleted;

                }
                catch (Exception)
                {
                    MGlobal.displayWarning(string.Format("the object {0} was not found and was removed from the selection list", itms[i]));
                }
            }
            GetNewGeom();

        }

        internal void GetNewGeom(bool isFromDeserial = false)
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


            var DagObjectList = selectionList.DagPaths(MFn.Type.kCurve).ToList();
            SelectedItems = new Dictionary<string, DMCurve>(DagObjectList.Count);

            foreach (var dag in DagObjectList)
            {
                var itm = new DMCurve(dag, _space);
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


               // var uuidstr = dagNode.uuid().asString();
                if (SelectedItems.ContainsKey(dagNode.partialPathName))
                {
                    SelectedItems[dagNode.partialPathName].Dispose();
                    SelectedItems.Remove(dagNode.partialPathName);
                }


                OnNodeModified(true);
            }
        }

        internal void MObjOnChanged(object sender, MFnDagNode dagNode)
        {
           // if (!differUpdate)
               // MarkNodeAsModified(true);
            if(exacutionComplete) MarkNodeAsModified(true);


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
    public class CurveNodeViewCustomization : INodeViewCustomization<SelectCurveNode>
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
        public void CustomizeView(SelectCurveNode model, NodeView nodeView)
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
