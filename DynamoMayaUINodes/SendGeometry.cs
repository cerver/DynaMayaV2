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
using JetBrains.dotMemoryUnit;


namespace DynaMaya.UINodes
{

    [NodeName("Send Geometry")]
    [NodeCategory("DynaMaya.Reference")]

    // The description will display in the tooltip
    // and in the help window for the node.
   // [NodeDescription("CustomNodeModelDescription",typeof(SamplesLibraryUI.Properties.Resources))]

    // Add the IsDesignScriptCompatible attribute to ensure
    // that it gets loaded in Dynamo.
    [IsDesignScriptCompatible]
    public class SendGeometryNode : NodeModel
    {
        #region private members
    
        private AssociativeNode _meshLstNode = AstFactory.BuildNullNode();
        private MSpace.Space space = MSpace.Space.kWorld;
        private string m_mSpace = MSpace.Space.kWorld.ToString();
    
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
        public DelegateCommand BtnCommand { get; set; }

   
        #endregion

        #region constructor

        /// <summary>
        /// The constructor for a NodeModel is used to create
        /// the input and output ports and specify the argument
        /// lacing.
        /// </summary>
        [IsVisibleInDynamoLibrary(false)]
        public SendGeometryNode()
        {
            // When you create a UI node, you need to do the
            // work of setting up the ports yourself. To do this,
            // you can populate the InPortData and the OutPortData
            // collections with PortData objects describing your ports.
            InPortData.Add(new PortData("Geometry", Resources.DMInPortToolTip));

            // Nodes can have an arbitrary number of inputs and outputs.
            // If you want more ports, just create more PortData objects.
           // OutPortData.Add(new PortData("Mesh", Resources.DMOutPortToolTip));

            // This call is required to ensure that your ports are
            // properly created.
            RegisterAllPorts();

            // The arugment lacing is the way in which Dynamo handles
            // inputs of lists. If you don't want your node to
            // support argument lacing, you can set this to LacingStrategy.Disabled.
            ArgumentLacing = LacingStrategy.Shortest;
            BtnCommand = new DelegateCommand(ButtonClicked, CanSendToMaya);

           


        }

        #endregion

        #region public methods

      
        internal void SendToMaya()
        {
            foreach (var dynGeom in InputNodes.Values)
            {
                var type = dynGeom.Item2.GetType().ToString();
                switch ( type )

                {
                    case "Curve":

                        break;

                }
            }

        }


        internal void MObjOnChanged(object sender, MFnDagNode dagNode)
        {
            OnNodeModified(true);

        }

        #endregion

        #region command methods

        internal static bool CanSendToMaya(object obj)
        {
            return true;
        }

        [IsVisibleInDynamoLibrary(false)]
        internal  void ButtonClicked(object obj)
        {
            SendToMaya();
        
        }

        #endregion

        [IsVisibleInDynamoLibrary(false)]
        public override void Dispose()
        {
            base.Dispose();
    
        }
    }

    /// <summary>
    ///     View customizer for CustomNodeModel Node Model.
    /// </summary>
    [IsVisibleInDynamoLibrary(false)]
    public class SendGeomNodeViewCustomization : INodeViewCustomization<SendGeometryNode>
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
        public void CustomizeView(SendGeometryNode model, NodeView nodeView)
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
