using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;
using Autodesk.Maya.OpenMaya;
using Dynamo.Controls;
using Dynamo.Graph;
using Dynamo.Models;
using Dynamo.Nodes;
using Dynamo.UI.Commands;
using Dynamo.Wpf;

using DynaMaya.NodeUI;
using DynaMaya.Nodes.Properties;
using DynaMaya.Util;
using Dynamo.Graph.Nodes;
using ProtoCore.AST.AssociativeAST;

namespace DynaMaya.UINodes
{
 
    [NodeName("Send MEL Command")]
    [NodeCategory("DynaMaya.Interop.Send")]

    [IsDesignScriptCompatible]
    public class MelCommandNode : NodeModel
    {
        #region private members
    
        private AssociativeNode _melCmdLstNode = AstFactory.BuildNullNode();
        private AssociativeNode _melCmdLstNameNode = AstFactory.BuildNullNode();

        private bool firstRun = true;
        private bool _hasBeenDeleted = false;
        private int updateInterval = 300;
        private string m_updateInterval = "50";
        private Dictionary<string, string> MelCmdItms;
   

        #endregion

        #region properties
     
        /// <summary>
        /// DelegateCommand objects allow you to bind
        /// UI interaction to methods on your data context.
        /// </summary>

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
        public MelCommandNode()
        {
            // When you create a UI node, you need to do the
            // work of setting up the ports yourself. To do this,
            // you can populate the InPortData and the OutPortData
            // collections with PortData objects describing your ports.
            InPorts.Add(new PortModel(PortType.Input, this, new PortData("MEL", "Add the MEL code here as a string")));
            // InPortData.Add(new PortData("MEL", Resources.DMInPortToolTip));

            // Nodes can have an arbitrary number of inputs and outputs.
            // If you want more ports, just create more PortData objects.
            // OutPortData.Add(new PortData("Result", Resources.DMOutPortToolTip));
            OutPorts.Add(new PortModel(PortType.Output, this, new PortData("Result", "This is what is returned by the MEL command if anything")));

            // This call is required to ensure that your ports are
            // properly created.
            RegisterAllPorts();

            

            // The arugment lacing is the way in which Dynamo handles
            // inputs of lists. If you don't want your node to
            // support argument lacing, you can set this to LacingStrategy.Disabled.
            ArgumentLacing = LacingStrategy.Shortest;
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

            Func<string, List<object>> func = DMInterop.SendMelCommand;
            _melCmdLstNode = AstFactory.BuildFunctionCall(func, inputAstNodes);
 
            return new[]
            {
                AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(0), _melCmdLstNode)
            };
        }


        #endregion

        #region command methods

        internal static bool isOk(object obj)
        {
            return true;
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
        }
    }

    /// <summary>
    ///     View customizer for CustomNodeModel Node Model.
    /// </summary>
    [IsVisibleInDynamoLibrary(false)]
    public class MelCmdViewCustomization : INodeViewCustomization<MelCommandNode>
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
        public void CustomizeView(MelCommandNode model, NodeView nodeView)
        {
            // The view variable is a reference to the node's view.
            // In the middle of the node is a grid called the InputGrid.
            // We reccommend putting your custom UI in this grid, as it has
            // been designed for this purpose.

            // Create an instance of our custom UI class (defined in xaml),
            // and put it into the input grid.
            var MelNodeControl = new CommandNodeUI();
            nodeView.inputGrid.Children.Add(MelNodeControl);

            // Set the data context for our control to be this class.
            // Properties in this class which are data bound will raise 
            // property change notifications which will update the UI.
            MelNodeControl.DataContext = model;
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
