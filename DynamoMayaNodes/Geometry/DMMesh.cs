using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Geometry.TSpline;
using Autodesk.DesignScript.Interfaces;
using Autodesk.DesignScript.Runtime;
using Autodesk.Maya.OpenMaya;
using DynaMaya.Util;



namespace DynaMaya.Geometry
{
    [IsVisibleInDynamoLibrary(false)]
    public class DMMesh : DMBase
    {
        //[IsVisibleInDynamoLibrary(false)]
        //public Mesh DynMesh;

        [IsVisibleInDynamoLibrary(false)]
        public MDagModifier dagMod;
        [IsVisibleInDynamoLibrary(false)]
        public MFnMesh meshFn;

        [IsVisibleInDynamoLibrary(false)]
        public DMMesh()
        {

        }

        [IsVisibleInDynamoLibrary(false)]
        public DMMesh(MDagPath dagPath, MSpace.Space mspace)
            : base(dagPath, mspace)
        {
            //MayaMesh = new MFnMesh(dagPath);
            

        }
        [IsVisibleInDynamoLibrary(false)]
        public DMMesh(MDagPath dagPath, string mspace)
            : base(dagPath, mspace)
        {
           // MayaMesh = new MFnMesh(dagPath);

        }
        [IsVisibleInDynamoLibrary(false)]
        public DMMesh(MDagPath dagShape, MDagPath dagTransform, MSpace.Space mspace)
            : base(dagShape, dagTransform, mspace)
        {
           // MayaMesh = new MFnMesh(dagShape);

        }


        //methods


        [IsVisibleInDynamoLibrary(false)]
        public static Mesh ToDynamoElement(string dagName, string space)
        {


            return MTDMeshFromName(dagName, space);
        }

        public static Mesh ToDynamoElement(MFnMesh mMesh, string space )
        {
            MSpace.Space mspace = MSpace.Space.kWorld;
            Enum.TryParse(space, out mspace);

            return MTDMeshFromMayaMesh(mMesh, mspace);
        }

        [IsVisibleInDynamoLibrary(false)]
        public static bool ToMayaOLD(Mesh MeshToSend, string name)
        {
            bool nodeExists = false;
            MDagPath node = null;
            Task checkNode = null;
            Task mobjMesh = null;

            try
            {
                checkNode = Task.Factory.StartNew(() => node = DMInterop.getDagNode(name));
                checkNode.Wait(500);

                nodeExists = true;


            }
            catch (Exception)
            {

                nodeExists = false;
            }
            MFnMesh mfnMesh;

            if (nodeExists)
            {
                mfnMesh = new MFnMesh(node);
            }
            else
            {
                mfnMesh = new MFnMesh();
            }
           
            //unpack geom
            int numVert = MeshToSend.VertexPositions.Length;
            int numPoly = MeshToSend.FaceIndices.Length;

            MIntArray faceCnx = new MIntArray();
            MFloatPointArray verticies = new MFloatPointArray();
            MIntArray faceVtxCt = new MIntArray();

            MFloatPoint vtxToAdd = new MFloatPoint();
            foreach (var vtx in MeshToSend.VertexPositions)
            {
                if (MGlobal.isZAxisUp)
                {
                    vtxToAdd.x = (float) vtx.X;
                    vtxToAdd.y = (float) vtx.Y;
                    vtxToAdd.z = (float) vtx.Z;
                }
                else
                {
                    vtxToAdd.x = (float)vtx.X;
                    vtxToAdd.y = (float)vtx.Z;
                    vtxToAdd.z = -(float)vtx.Y;
                }

                verticies.Add(vtxToAdd);
            }

            foreach (var fidx in MeshToSend.FaceIndices)
            {

                int vtxCt = (int) fidx.Count;
                faceVtxCt.Add(vtxCt);
                if (vtxCt == 3)
                {
                    faceCnx.Add((int)fidx.A);
                    faceCnx.Add((int)fidx.B);
                    faceCnx.Add((int)fidx.C);

                }
                else
                {
                    faceCnx.Add((int)fidx.A);
                    faceCnx.Add((int)fidx.B);
                    faceCnx.Add((int)fidx.C);
                    faceCnx.Add((int)fidx.D);
                }
                
            }

            //create maya mesh
            if (nodeExists)
            {

                mfnMesh.createInPlace(numVert, numPoly, verticies, faceVtxCt, faceCnx);
                mfnMesh.Dispose();

            }
            else
            {
                using (var obj = mfnMesh.create(numVert, numPoly, verticies, faceVtxCt, faceCnx))
                {
                    MFnDependencyNode nodeFn = new MFnDagNode(obj);
                    nodeFn.setName(name);
                    MGlobal.executeCommand("sets -e -forceElement initialShadingGroup " + nodeFn.name);
                    mfnMesh.Dispose();
                }
                
            }


            return true;
        }

        [IsVisibleInDynamoLibrary(false)]
        public bool ToMaya(Mesh MeshToSend, string name)
        {
            try
            {
                dynMeshToMayaMesh(name, MeshToSend);
            }
            catch (Exception e)
            {
                return false;
            }


            return true;
        }

        [IsVisibleInDynamoLibrary(false)]
        public bool ToMayaFromTSplinesSurf(TSplineSurface TsMeshToSend, string name)
        {
            try
            {
                dynMeshToMayaMesh(name, null, TsMeshToSend);
            }
            catch (Exception e)
            {
                return false;
            }


            return true;
        }
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute()]
        private void dynMeshToMayaMesh( string name, Mesh dynMesh=null, TSplineSurface tsMesh=null)
        {
            bool nodeExists = false;
            MDagPath node = null;
            Task unpackTask = null;
            Task mobjTask = null;

            try
            {
                node = DMInterop.getDagNode(name);
                nodeExists = true;

            }
            catch (Exception)
            {

                nodeExists = false;
            }

            MIntArray faceCnx = new MIntArray();
            MFloatPointArray verticies = new MFloatPointArray();
            MIntArray faceVtxCt = new MIntArray();
            int numVert = 0;
            int numPoly = 0;

            if (dynMesh != null)
            {
                numVert = dynMesh.VertexPositions.Length;
                numPoly = dynMesh.FaceIndices.Length;
                unpackTask = Task.Factory.StartNew(() => unpackDynMesh(dynMesh, out faceCnx, out verticies, out faceVtxCt));
                unpackTask.Wait(4000);
            }

            if (tsMesh != null)
            {
                numVert = tsMesh.VerticesCount;
                numPoly = tsMesh.FacesCount;
                unpackTask = Task.Factory.StartNew(() => unpackTsMesh(tsMesh, out faceCnx, out verticies, out faceVtxCt));
                unpackTask.Wait(4000);
            }



            if (nodeExists)
            {
                try
                {
                    meshFn = new MFnMesh(node);
                    meshFn.createInPlace(numVert, numPoly, verticies, faceVtxCt, faceCnx);
                   
                }
                catch (Exception e)
                {
                    MGlobal.displayWarning(e.Message);
                }
              

            }
            else
            {
       
                try
                {
                    dagMod = new MDagModifier();
                    // Create a mesh data wrapper to hold the new geometry.
                    MFnMeshData dataFn = new MFnMeshData();
                    MObject dataWrapper = dataFn.create();

                    // Create the mesh geometry and put it into the wrapper.
                    meshFn = new MFnMesh();
                    MObject dataObj = meshFn.create(numVert, numPoly, verticies, faceVtxCt, faceCnx, dataWrapper);
                    MObject transform = dagMod.createNode("mesh", MObject.kNullObj);

                    dagMod.doIt();

                    renameNodes(transform, name);
                    dagMod.doIt();
                    assignShadingGroup(transform, "initialShadingGroup");
                    dagMod.doIt();
                    setMeshData(transform, dataWrapper);
                }
                catch (Exception e)
                {
                    MGlobal.displayWarning(e.Message);
                }
                

            }

            //GC.Collect();
            //GC.WaitForPendingFinalizers();
        }
 
        private  void assignShadingGroup(MObject transform, string groupName)
        {
            // Get the name of the mesh node.
            //
            // We need to use an MFnDagNode rather than an MFnMesh because the mesh
            // is not fully realized at this point and would be rejected by MFnMesh.
            MFnDagNode dagFn = new MFnDagNode(transform);
            dagFn.setObject(dagFn.child(0));

            string meshName = dagFn.name;

            // Use the DAG modifier to put the mesh into a shading group
            string cmd = "sets -e -fe ";
            cmd += groupName + " " + meshName;
            dagMod.commandToExecute(cmd);

            // Use the DAG modifier to select the new mesh.
            cmd = "select " + meshName;
            dagMod.commandToExecute(cmd);
        }
        private  void renameNodes(MObject transform, string baseName)
        {
            //  Rename the transform to something we know no node will be using.
            dagMod.renameNode(transform, "polyPrimitiveCmdTemp");

            //  Rename the mesh to the same thing but with 'Shape' on the end.
            MFnDagNode dagFn = new MFnDagNode(transform);

            dagMod.renameNode(dagFn.child(0), "polyPrimitiveCmdTempShape");

            //  Now that they are in the 'something/somethingShape' format, any
            //  changes we make to the name of the transform will automatically be
            //  propagated to the shape as well.
            //
            //  Maya will replace the '#' in the string below with a number which
            //  ensures uniqueness.
            string transformName = baseName+"Shape";
            dagMod.renameNode(transform, baseName);
            dagMod.renameNode(dagFn.child(0), transformName);
        }

        private  void setMeshData(MObject transform, MObject dataWrapper)
        {
            // Get the mesh node.
            MFnDagNode dagFn = new MFnDagNode(transform);
            MObject mesh = dagFn.child(0);

            // The mesh node has two geometry inputs: 'inMesh' and 'cachedInMesh'.
            // 'inMesh' is only used when it has an incoming connection, otherwise
            // 'cachedInMesh' is used. Unfortunately, the docs say that 'cachedInMesh'
            // is for internal use only and that changing it may render Maya
            // unstable.
            //
            // To get around that, we do the little dance below...

            // Use a temporary MDagModifier to create a temporary mesh attribute on
            // the node.
            MFnTypedAttribute tAttr = new MFnTypedAttribute();
            MObject tempAttr = tAttr.create("tempMesh", "tmpm", MFnData.Type.kMesh);
            MDagModifier tempMod = new MDagModifier();

            tempMod.addAttribute(mesh, tempAttr);

            tempMod.doIt();

            // Set the geometry data onto the temp attribute.
            dagFn.setObject(mesh);

            MPlug tempPlug = dagFn.findPlug(tempAttr);

            tempPlug.setValue(dataWrapper);

            // Use the temporary MDagModifier to connect the temp attribute to the
            // node's 'inMesh'.
            MPlug inMeshPlug = dagFn.findPlug("inMesh");

            tempMod.connect(tempPlug, inMeshPlug);

            tempMod.doIt();

            // Force the mesh to update by grabbing its output geometry.
            dagFn.findPlug("outMesh").asMObject();

            // Undo the temporary modifier.
            tempMod.undoIt();
        }

        private static void unpackDynMesh(Mesh dynMesh, out MIntArray faceCnx, out MFloatPointArray verticies, out MIntArray faceVtxCt)
        {

            MIntArray m_faceCnx = new MIntArray();
            MFloatPointArray m_verticies = new MFloatPointArray();
            MIntArray m_faceVtxCt = new MIntArray();

            MFloatPoint vtxToAdd = new MFloatPoint();


            foreach (var vtx in dynMesh.VertexPositions)
            {
                if (MGlobal.isZAxisUp)
                {
                    vtxToAdd.x = (float)vtx.X;
                    vtxToAdd.y = (float)vtx.Y;
                    vtxToAdd.z = (float)vtx.Z;
                }
                else
                {
                    vtxToAdd.x = (float)vtx.X;
                    vtxToAdd.y = (float)vtx.Z;
                    vtxToAdd.z = -(float)vtx.Y;
                }

                m_verticies.Add(vtxToAdd);
            }

            foreach (var fidx in dynMesh.FaceIndices)
            {

                int vtxCt = (int)fidx.Count;
                m_faceVtxCt.Add(vtxCt);
                if (vtxCt == 3)
                {
                    m_faceCnx.Add((int)fidx.A);
                    m_faceCnx.Add((int)fidx.B);
                    m_faceCnx.Add((int)fidx.C);

                }
                else
                {
                    m_faceCnx.Add((int)fidx.A);
                    m_faceCnx.Add((int)fidx.B);
                    m_faceCnx.Add((int)fidx.C);
                    m_faceCnx.Add((int)fidx.D);
                }

            }

            verticies = m_verticies;
            faceCnx = m_faceCnx;
            faceVtxCt = m_faceVtxCt;
        }

        private static void unpackTsMesh(TSplineSurface tsMesh, out MIntArray faceCnx, out MFloatPointArray verticies, out MIntArray faceVtxCt)
        {

            MIntArray m_faceCnx = new MIntArray();
            MFloatPointArray m_verticies = new MFloatPointArray();
            MIntArray m_faceVtxCt = new MIntArray();

            MFloatPoint vtxToAdd = new MFloatPoint();

            var tsMeshCompress = tsMesh.CompressIndexes();

            foreach (var vtx in tsMeshCompress.Vertices)
            {
                if (MGlobal.isZAxisUp)
                {
                    vtxToAdd.x = (float)vtx.PointGeometry.X;
                    vtxToAdd.y = (float)vtx.PointGeometry.Y;
                    vtxToAdd.z = (float)vtx.PointGeometry.Z;
                }
                else
                {
                    vtxToAdd.x = (float)vtx.PointGeometry.X;
                    vtxToAdd.y = (float)vtx.PointGeometry.Z;
                    vtxToAdd.z = -(float)vtx.PointGeometry.Y;
                }

                m_verticies.Add(vtxToAdd);
            }

            foreach (var fidx in tsMeshCompress.Faces)
            {
                
                int vtxCt = fidx.Vertices.Length;
                m_faceVtxCt.Add(vtxCt);

                foreach (var fVert in fidx.Vertices)
                {
                    m_faceCnx.Add(fVert.Index);
                }


            }
            verticies = m_verticies;
            faceCnx = m_faceCnx;
            faceVtxCt = m_faceVtxCt;
        }

        internal static Mesh MTDMeshFromName(string dagName, string space )
        {
            MDagPath dagPath = DMInterop.getDagNode(dagName);
            MSpace.Space mspace = MSpace.Space.kWorld;
            Enum.TryParse(space, out mspace);
           

            return MTDMeshFromDag(dagPath, mspace);

        }

        internal static Mesh MTDMeshFromDag(MDagPath dagPath, MSpace.Space space)
        {
            
            
            return MTDMeshFromMayaMesh(new MFnMesh( dagPath), space);

        }

        internal static Mesh MTDMeshFromMayaMesh(MFnMesh mayaMesh, MSpace.Space space)
        {

            PointList vertices = new PointList(mayaMesh.numVertices);
            ;
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

                    WarningException wa = new WarningException("The DynMesh will not show in Dynamo if it has any faces with 4 verts or more. The DynMesh can be represented as closed curves .");
                    return null;
                }
                if (faceIndex.length == 3)
                    faceIndexList.Add(IndexGroup.ByIndices((uint) faceIndex[0], (uint) faceIndex[1], (uint) faceIndex[2]));
                else
                    faceIndexList.Add(IndexGroup.ByIndices((uint) faceIndex[0], (uint) faceIndex[1], (uint) faceIndex[2],
                        (uint) faceIndex[3]));

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
                mayaMesh.getPolygonVertices(i, ids);

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



    }
}