using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Timers;
using System.Windows.Media.Media3D;
using System.Xml;
using Autodesk.DesignScript.Geometry;
using Autodesk.Maya.OpenMaya;
using DynamoMaya.Contract;
using Autodesk.DesignScript.Runtime;
using DynaMaya.Util;


namespace DynaMaya.Geometry
{
    // A delegate type for hooking up change notifications.
 
    [IsVisibleInDynamoLibrary(false)]
    public delegate void ChangedEventHandler(object sender, MDagPath dagPath);

    [IsVisibleInDynamoLibrary(false)]
    public class DMCurve 
    {
        private long _prevTime = DateTime.Now.Ticks/10;
        private long _curTime;
        public long EventTimeInterval = 50;
        public bool IsList = false; 
        public MDagPath DagPath;
        public List<MDagPath> DagObjectList;

        [IsVisibleInDynamoLibrary(false)]
        public event ChangedEventHandler Changed;
       
        protected internal virtual void OnChanged(MDagPath dagPath)
        {
            if (Changed != null)
                Changed(this, dagPath);
        }
        [IsVisibleInDynamoLibrary(false)]
        public DMCurve()
        {
       
        }
        [IsVisibleInDynamoLibrary(false)]
        public DMCurve(MDagPath dagPath)
        {
            IsList = false;
            DagPath = dagPath;
            DagPath.WorldMatrixModified += DagPathOnWorldMatrixModified;
            DagPath.node.NodeDirtyPlug += NodeOnNodeDirtyPlug;  
            
        }

        //methods
        internal void AddEvents(MDagPath dagPath)
        {
            dagPath.WorldMatrixModified += DagPathOnWorldMatrixModified;
            dagPath.node.NodeDirtyPlug += NodeOnNodeDirtyPlug;
            dagPath.node.NodeDestroyed += Node_NodeDestroyed;
            
        }


        [IsVisibleInDynamoLibrary(false)]
        public  void GetSelectedCurve()
        {

            MSelectionList selectionList = new MSelectionList();
            MGlobal.getActiveSelectionList(selectionList);
           
            DagObjectList = selectionList.DagPaths(MFn.Type.kNurbsCurve).ToList();

            foreach (var obj in DagObjectList)
            {
                AddEvents(obj); 
            }
        }


        [IsVisibleInDynamoLibrary(false)]
        public static Curve ToDynamoElement(string dagName, int axis)
        {

            return DMInterop.MTDCurveFromName(dagName, axis);
            
        }

        //events
        private void DagPathOnWorldMatrixModified(object sender, MWorldMatrixModifiedFunctionArgs mWorldMatrixModifiedFunctionArgs)
        {
            _curTime = DateTime.Now.Ticks/10;
            if(_curTime - _prevTime > EventTimeInterval)
                OnChanged(DagPath);
            _prevTime = _curTime;
        }

        private void NodeOnNodeDirtyPlug(object sender, MNodePlugFunctionArgs mNodePlugFunctionArgs)
        {
            _curTime = DateTime.Now.Ticks / 10;
            if (_curTime - _prevTime > EventTimeInterval)
                OnChanged(DagPath);
            _prevTime = _curTime;
        }

        private void Node_NodeDestroyed(object sender, MBasicFunctionArgs e)
        {

            OnChanged(DagPath);

        }
    }
}