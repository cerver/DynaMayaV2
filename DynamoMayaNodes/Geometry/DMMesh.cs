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
   
        public Mesh DynMesh;
        internal MFnMesh MayaMesh;

        [IsVisibleInDynamoLibrary(false)]
        public DMMesh()
        {

        }

        [IsVisibleInDynamoLibrary(false)]
        public DMMesh(MDagPath dagPath, MSpace.Space mspace)
            : base(dagPath, mspace)
        {
            MayaMesh = new MFnMesh(dagPath);
            

        }
        [IsVisibleInDynamoLibrary(false)]
        public DMMesh(MDagPath dagPath, string mspace)
            : base(dagPath, mspace)
        {
            MayaMesh = new MFnMesh(dagPath);

        }
        [IsVisibleInDynamoLibrary(false)]
        public DMMesh(MDagPath dagShape, MDagPath dagTransform, MSpace.Space mspace)
            : base(dagShape, dagTransform, mspace)
        {
            MayaMesh = new MFnMesh(dagShape);

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
        public static bool ToMaya(Mesh MeshToSend, string name)
        {
            bool nodeExists = false;
            MDagPath node = null;
            Task checkNode = null;

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
            MFnMesh mayaMesh;

            if (nodeExists)
            {
                mayaMesh = new MFnMesh(node);
            }
            else
            {
                mayaMesh = new MFnMesh();
            }
           

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


            if (nodeExists)
            {
               
                mayaMesh.createInPlace(numVert, numPoly, verticies, faceVtxCt, faceCnx);


            }
            else
            {
                var obj = mayaMesh.create(numVert, numPoly, verticies, faceVtxCt, faceCnx);
                MFnDependencyNode nodeFn = new MFnDagNode(obj);
                nodeFn.setName(name);
                MGlobal.executeCommand("sets -e -forceElement initialShadingGroup " + nodeFn.name);
            }


            return true;
        }

        [IsVisibleInDynamoLibrary(false)]
        public static bool ToMayaFromTSplinesSurf(TSplineSurface TsMeshToSend, string name)
        {
            bool nodeExists = false;
            MDagPath node = null;
            Task checkNode = null;

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
            MFnMesh mayaMesh;

            if (nodeExists)
            {
                
                mayaMesh = new MFnMesh(node);
            }
            else
            {
                mayaMesh = new MFnMesh();
            }

            

            int numVert = TsMeshToSend.VerticesCount;
            int numPoly = TsMeshToSend.FacesCount;

            MIntArray faceCnx = new MIntArray();
            MFloatPointArray verticies = new MFloatPointArray();
            MIntArray faceVtxCt = new MIntArray();

            MFloatPoint vtxToAdd = new MFloatPoint();

            Parallel.Invoke(() =>
            {
                foreach (var vtx in TsMeshToSend.Vertices)
                {
                    if (MGlobal.isZAxisUp)
                    {

                        vtxToAdd.x = (float) vtx.PointGeometry.X;
                        vtxToAdd.y = (float) vtx.PointGeometry.Y;
                        vtxToAdd.z = (float) vtx.PointGeometry.Z;
                    }
                    else
                    {
                        vtxToAdd.x = (float) vtx.PointGeometry.X;
                        vtxToAdd.y = (float) vtx.PointGeometry.Z;
                        vtxToAdd.z = -(float) vtx.PointGeometry.Y;
                    }

                    verticies.Add(vtxToAdd);
                }
            },
            () =>
            {
                foreach (var fidx in TsMeshToSend.Faces)
                {

                    int vtxCt = fidx.Vertices.Length;
                    faceVtxCt.Add(vtxCt);

                    foreach (var fVert in fidx.Vertices)
                    {
                        faceCnx.Add(fVert.Index);
                    }


                }
            });

            if (nodeExists)
            {
 
                mayaMesh.createInPlace(numVert, numPoly, verticies, faceVtxCt, faceCnx);
                mayaMesh.updateSurface();
                
            }
            else
            {
                var obj = mayaMesh.create(numVert, numPoly, verticies, faceVtxCt, faceCnx);
                Thread.Sleep(1500);
                MFnDependencyNode nodeFn = new MFnDagNode(obj);
                nodeFn.setName(name);
                MGlobal.executeCommand("sets -e -forceElement initialShadingGroup " + nodeFn.name);

            }


            return true;
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