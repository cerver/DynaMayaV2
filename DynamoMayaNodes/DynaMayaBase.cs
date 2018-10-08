using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Timers;
using System.Windows.Forms.VisualStyles;
using System.Windows.Media.Media3D;
using System.Xml;
using Autodesk.DesignScript.Geometry;
using Autodesk.Maya.OpenMaya;
using DynamoMaya.Contract;
using Autodesk.DesignScript.Runtime;


namespace DynaMaya
{
    [SupressImportIntoVM]
    // A delegate type for hooking up change notifications.
    public delegate void ChangedEventHandler(object sender, EventArgs e);
    [SupressImportIntoVM]
    public class MayaObject
    {
        private long PrevTime = DateTime.Now.Ticks/10;
        private long CurTime;
        private const long Delta = 300;

        public event ChangedEventHandler Changed;
        protected virtual void OnChanged(EventArgs e)
        {
            if (Changed != null)
                Changed(this, e);
        }

        public MDagPath DagPath;

        public MayaObject()
        {
        }

        public MayaObject(MDagPath dagPath)
        {
            DagPath = dagPath;
            DagPath.WorldMatrixModified += DagPathOnWorldMatrixModified;
            DagPath.node.NodeDirtyPlug += NodeOnNodeDirtyPlug;
        }

       
        public static List<MayaObject> GetSelectedObjects(MFn.Type filter = MFn.Type.kInvalid)
        {
            
            MSelectionList selectionList = new MSelectionList();
            MGlobal.getActiveSelectionList(selectionList);
            IEnumerable<MDagPath> listAsDags;

            if (filter == MFn.Type.kInvalid)
                listAsDags = selectionList.DagPaths();
            else
                listAsDags = selectionList.DagPaths(filter);

            List<MayaObject> selectedObjects = new List<MayaObject>((int)selectionList.length);
            var mDagPaths = listAsDags as MDagPath[] ?? listAsDags.ToArray();

            selectedObjects.AddRange(mDagPaths.Select(dagObj => new MayaObject(dagObj)));

            return selectedObjects;
        }

        private void DagPathOnWorldMatrixModified(object sender, MWorldMatrixModifiedFunctionArgs mWorldMatrixModifiedFunctionArgs)
        {
            CurTime = DateTime.Now.Ticks/10;
            if(CurTime - PrevTime > Delta)
                OnChanged(EventArgs.Empty);
            PrevTime = CurTime;
        }

        private void NodeOnNodeDirtyPlug(object sender, MNodePlugFunctionArgs mNodePlugFunctionArgs)
        {
            CurTime = DateTime.Now.Ticks / 10;
            if (CurTime - PrevTime > Delta)
                OnChanged(EventArgs.Empty);
            PrevTime = CurTime;
        }


    }
}