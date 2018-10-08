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


    [IsDesignScriptCompatible]
    public class DMNodeModel : NodeModel
    {
        #region private members
      
        internal int updateInterval = 50;
        internal string m_updateInterval = "50";
        internal bool _hasBeenDeleted = false;
        internal bool firstRun = true;
        internal bool differUpdate = false;

       
 
            #endregion

        #region properties
        
        

        /// <summary>
        /// DelegateCommand objects allow you to bind
        /// UI interaction to methods on your data context.
        /// </summary>
        [IsVisibleInDynamoLibrary(false)]
        internal DelegateCommand BtnCommand { get; set; }
        internal DelegateCommand DifferCmd { get; set; }
        internal DelegateCommand ManualUpdateCmd { get; set; }


        #endregion



        #region command methods

        internal static bool isOk(object obj)
        {
            return true;
        }


        [IsVisibleInDynamoLibrary(false)]
        internal void DifferClicked(object obj)
        {
            if (differUpdate) differUpdate = false;
            else differUpdate = true;

            if (!differUpdate)
                OnNodeModified(true);

        }

        [IsVisibleInDynamoLibrary(false)]
        internal void ManualUpdate(object obj)
        {
     
                OnNodeModified(true);

        }

        #endregion

      
    }


}
