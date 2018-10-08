using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Interfaces;
using Autodesk.DesignScript.Runtime;
using Autodesk.Maya.OpenMaya;
using DynaMaya.Util;
using ProtoCore.AST.AssociativeAST;
using ProtoCore.DesignScriptParser;
using ProtoScript.Messages;

namespace DynaMaya.Geometry
{
    [IsVisibleInDynamoLibrary(false)]
    public class DMMesh
    {
        private TimeSpan _prevTime = TimeSpan.FromTicks(DateTime.Now.Ticks);
        private TimeSpan _curTime;
        public long EventTimeInterval = 50;

        public MDagPath DagPath;
        public MFnDagNode DagNode;
        public Mesh mesh;
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
        public DMMesh()
            :base()
        {

        }
        [IsVisibleInDynamoLibrary(false)]
        public DMMesh(MDagPath dagPath, MSpace.Space mspace = MSpace.Space.kWorld)
        {
            DagPath = dagPath;
            AddEvents(dagPath);
            DagNode = new MFnDagNode(dagPath);
            dagName = DagPath.partialPathName;
            space = mspace.ToString();

        }

        //methods


        [IsVisibleInDynamoLibrary(false)]
        public static Mesh ToDynamoElement(string dagName, string axis)
        {
            return MTDMeshFromName(dagName, axis);
        }

        [IsVisibleInDynamoLibrary(false)]
        public static bool ToMaya(string dagName, string axis)
        {
            return true;
        }

    

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

        internal static Mesh MTDMeshFromName(string dagName, string space)
        {
            MDagPath dagPath = DMInterop.getDagNode(dagName);
            MSpace.Space mspace = MSpace.Space.kWorld;
            Enum.TryParse(space, out mspace);


            return MTDMeshFromDag(dagPath, mspace);

        }
       
        internal static Mesh MTDMeshFromDag(MDagPath dagPath, MSpace.Space space)
        {
            MFnMesh mayaMesh = new MFnMesh(dagPath);

            PointList vertices = new PointList(mayaMesh.numVertices); ;
            List<IndexGroup> faceIndexList = new List<IndexGroup>(mayaMesh.numPolygons);

            MPointArray mayaVerts = new MPointArray();
            mayaMesh.getPoints(mayaVerts, space);
            if (MGlobal.isYAxisUp)
                vertices.AddRange(mayaVerts.Select(v => Point.ByCoordinates(v.x, -v.z, v.y)));
            else
                vertices.AddRange(mayaVerts.Select(v => Point.ByCoordinates(v.x, v.y, v.z)));

            MIntArray faceIndex = new MIntArray();
            for (int i = 0; i < mayaMesh.numPolygons; i++)
            {
                mayaMesh.getPolygonVertices(i, faceIndex);
                if (faceIndex.length > 4)
                {
                    Warning wa = new Warning();
                    wa.Message = "The mesh will not show in Dynamo if it has any faces with 4 verts or more. The mesh can be represented as closed curves and covnered to surfaces.";
                    return null;
                }
                if (faceIndex.length == 3)
                    faceIndexList.Add(IndexGroup.ByIndices((uint)faceIndex[0], (uint)faceIndex[1], (uint)faceIndex[2]));
                else
                    faceIndexList.Add(IndexGroup.ByIndices((uint)faceIndex[0], (uint)faceIndex[1], (uint)faceIndex[2], (uint)faceIndex[3]));

            }
            mayaMesh.Dispose();
            mayaVerts.Dispose();
            faceIndex.Dispose();
            

            using (vertices)
            {
                return Mesh.ByPointsFaceIndices(vertices, faceIndexList);
            }
            
        }

        [IsVisibleInDynamoLibrary(false)]
        public static List<int[]> GetFaceVertexIdx(string dagName, string space)
        {
            MDagPath dagPath = DMInterop.getDagNode(dagName);
            MSpace.Space mspace = MSpace.Space.kWorld;
            Enum.TryParse(space, out mspace);

            MFnMesh mayaMesh = new MFnMesh(dagPath);

            List<int[]> vtxIds = new List<int[]>(mayaMesh.numPolygons);
            MIntArray ids = new MIntArray();

            for (int i = 0; i < mayaMesh.numPolygons; i++)
            {
                mayaMesh.getPolygonVertices(i,ids);

                vtxIds.Add(ids.ToArray());
            }
            return vtxIds;

        }

        [IsVisibleInDynamoLibrary(false)]
        public static MFnMesh GetMayaMesh(string dagName, string space)
        {
            MDagPath dagPath = DMInterop.getDagNode(dagName);
            MSpace.Space mspace = MSpace.Space.kWorld;
            Enum.TryParse(space, out mspace);

            MFnMesh mayaMesh = new MFnMesh(dagPath);

        
            return mayaMesh;

        }

        //events
        internal void DagPathOnWorldMatrixModified(object sender, MWorldMatrixModifiedFunctionArgs mWorldMatrixModifiedFunctionArgs)
        {
            _curTime = TimeSpan.FromTicks(DateTime.Now.Ticks);

            if (_curTime.Subtract(_prevTime).TotalMilliseconds > EventTimeInterval)
                OnChanged(DagNode);
            _prevTime = _curTime;
        }

        internal void NodeOnNodeDirtyPlug(object sender, MNodePlugFunctionArgs mNodePlugFunctionArgs)
        {
            _curTime = TimeSpan.FromTicks(DateTime.Now.Ticks);

            if (_curTime.Subtract(_prevTime).TotalMilliseconds > EventTimeInterval)
                OnChanged(DagNode);
            _prevTime = _curTime;
        }
        internal void NodeOnNodeAboutToDelete(object sender, MNodeModifierFunctionArgs mNodeModifierFunctionArgs)
        {
            OnDeleted(DagNode);

        }

        [IsVisibleInDynamoLibrary(false)]
        public void Dispose()
        {
            RemoveEvents(DagPath);
            //DagPath.Dispose();
            //DagNode.Dispose();
         
        }

        public void SetOwner(object owner)
        {
            throw new NotImplementedException();
        }
    }

    
}