using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.DesignScript.Runtime;
using Autodesk.Maya.OpenMaya;


namespace DynaMaya.Geometry
{
    [ SupressImportIntoVM]
    public delegate void DMEventHandler(object sender, MFnDagNode dagNode);

    [SupressImportIntoVM]
    public enum MFnType
    {
        kNurbsCurve = MFn.Type.kNurbsCurve,
        kMesh = MFn.Type.kMesh,
        kParticle = MFn.Type.kParticle
    }
    [SupressImportIntoVM]
    public enum MFnNurbsCurveForm
    {
        kClosed = MFnNurbsCurve.Form.kClosed,
        kOpen = MFnNurbsCurve.Form.kOpen
    }

    [IsVisibleInDynamoLibrary(false)]
    public class DMBase
    {
        private TimeSpan _prevTime = TimeSpan.FromTicks(DateTime.Now.Ticks);
        private TimeSpan _curTime;
        public long EventTimeInterval = 50;

        public MDagPath DagShape;
        public MFnDagNode DagNode;
        public string dagName;
        public string space;

        [IsVisibleInDynamoLibrary(false)]
        public event DMEventHandler Changed;

        [IsVisibleInDynamoLibrary(false)]
        public event DMEventHandler Deleted;

        [IsVisibleInDynamoLibrary(false)]
        protected internal virtual void OnChanged(MFnDagNode dagNode)
        {
            Changed?.Invoke(this, dagNode);
        }

        [IsVisibleInDynamoLibrary(false)]
        protected internal virtual void OnDeleted(MFnDagNode dagNode)
        {
            Deleted?.Invoke(this, dagNode);
        }

        [IsVisibleInDynamoLibrary(false)]
        public DMBase()
        {

        }

        [IsVisibleInDynamoLibrary(false)]
        public DMBase(MDagPath dagShape, MSpace.Space mspace)
        {
            DagShape = dagShape;
            AddEvents(dagShape);
            DagNode = new MFnDagNode(dagShape);
            dagName =  DagShape.partialPathName;
            space = mspace.ToString();
        }

        [IsVisibleInDynamoLibrary(false)]
        public DMBase(MDagPath dagShape, MDagPath dagTransform, MSpace.Space mspace)
        {
            DagShape = dagShape;
            AddEvents(dagShape);
            DagNode = new MFnDagNode(dagShape);
            dagName = dagTransform.partialPathName;
            space = mspace.ToString();
        }

        //methods

        internal void AddEvents(MDagPath dagPath)
        {
            dagPath.WorldMatrixModified += DagPathOnWorldMatrixModified;
            dagPath.node.NodeDirtyPlug += NodeOnNodeDirtyPlug;
            dagPath.node.NodeAboutToDelete += NodeOnNodeAboutToDelete;
        }

        internal void RemoveEvents(MDagPath dagPath)
        {
            dagPath.WorldMatrixModified -= DagPathOnWorldMatrixModified;
            dagPath.node.NodeDirtyPlug -= NodeOnNodeDirtyPlug;
            dagPath.node.NodeAboutToDelete -= NodeOnNodeAboutToDelete;
        }

        //events
        internal void DagPathOnWorldMatrixModified(object sender, MWorldMatrixModifiedFunctionArgs mWorldMatrixModifiedFunctionArgs)
        {

                OnChanged(DagNode);
    
        }

        internal void NodeOnNodeDirtyPlug(object sender, MNodePlugFunctionArgs mNodePlugFunctionArgs)
        {
   
                OnChanged(DagNode);
       
        }
        internal void NodeOnNodeAboutToDelete(object sender, MNodeModifierFunctionArgs mNodeModifierFunctionArgs)
        {
            OnDeleted(DagNode);

        }

        [IsVisibleInDynamoLibrary(false)]
        public void Dispose()
        {
            RemoveEvents(DagShape);
        }
        
    }

    
}