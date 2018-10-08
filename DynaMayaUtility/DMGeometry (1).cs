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
    public delegate void ChangedEventHandler(object sender, EventArgs e);
    [IsVisibleInDynamoLibrary(false)]
    public class DMCurve 
    {
        private long PrevTime = DateTime.Now.Ticks/10;
        private long CurTime;
        private const long Delta = 300;
        public Curve DynamoCurve;
        public List<Curve> DynamoCurveList;
        public bool isList = false; 
        public MDagPath DagPath;
        public IEnumerable<MDagPath> listAsDags;
        private List<DMCurve> selectedObjects;

        public event ChangedEventHandler Changed;
        protected virtual void OnChanged(EventArgs e)
        {
            if (Changed != null)
                Changed(this, e);
        }

        public DMCurve() : base()
        {
        }

        public DMCurve(MDagPath dagPath, bool addToCurveList=false)
        {
            DagPath = dagPath;
            DagPath.WorldMatrixModified += DagPathOnWorldMatrixModified;
            DagPath.node.NodeDirtyPlug += NodeOnNodeDirtyPlug;
            if(addToCurveList)DynamoCurveList.Add(DMInterop.MTDCurveFromDag(dagPath, 0));  
        }

        //methods
        public  void GetSelectedCurve()
        {

            MSelectionList selectionList = new MSelectionList();
            MGlobal.getActiveSelectionList(selectionList);
           
            listAsDags = selectionList.DagPaths(MFn.Type.kNurbsCurve);

            //List<DMCurve> selectedObjects = new List<DMCurve>((int)selectionList.length);
            var mDagPaths = listAsDags as MDagPath[] ?? listAsDags.ToArray();
            DynamoCurveList = new List<Curve>(mDagPaths.Length);
            selectedObjects = new List<DMCurve>(mDagPaths.Length);
            selectedObjects.AddRange(mDagPaths.Select(dagObj => new DMCurve(dagObj, true)));

        }

        public void UpdateSelectedCurves()
        {
            DynamoCurveList.Clear();
            foreach (var crv in selectedObjects)
            {
                DynamoCurveList.Add(DMInterop.MTDCurveFromDag(crv.DagPath, 0));
            }
        }


        public List<Curve> GetCurves()
        {
            return DynamoCurveList;
        } 

        //events
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