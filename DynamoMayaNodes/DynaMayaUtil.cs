using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Geometry.TSpline;
using Autodesk.Maya.OpenMaya;
using Autodesk.DesignScript.Runtime;
using DynaMaya.Util;
using DynaMaya.Geometry;

namespace DynaMaya.Util
{
   

    [IsVisibleInDynamoLibrary(false)]
    public class DMInterop
    {
 
        
        
        [IsVisibleInDynamoLibrary(false)]
        public static List<string> getMayaNodesByType(MFn.Type t)
        {
            var lMayaNodes = new List<string>();
            var itdagn = new MItDag(MItDag.TraversalType.kBreadthFirst, t);
            MFnDagNode dagn;

            while (!itdagn.isDone)
            {
                dagn = new MFnDagNode(itdagn.item());
                if (!dagn.isIntermediateObject)
                    lMayaNodes.Add(dagn.partialPathName);
                itdagn.next();
            }

            return lMayaNodes;
        }

        [IsVisibleInDynamoLibrary(false)]
        public static MDagPath getDagNode(string node_name)
        {
            var sl = new MSelectionList();

            sl.add(node_name, true);
            var dp = new MDagPath();
            sl.getDagPath(0, dp);
            return dp;
        }

        [IsVisibleInDynamoLibrary(false)]
        public static MObject getDependNode(string node_name)
        {
            var sl = new MSelectionList();
            sl.add(node_name, true);
            var o = new MObject();
            sl.getDependNode(0, o);
            return o;
        }

        [IsVisibleInDynamoLibrary(false)]
        public static Line[] GetMeshEdges(Mesh m, out int[] startVtx, out int[] endVtx)
        {
            Dictionary<string, Line> edgeDic = new Dictionary<string, Line>(m.VertexPositions.Length);

            string key, keyr;

            Point p0, p1;

            List<int> svtx = new List<int>(m.VertexPositions.Length * 2);
            List<int> evtx = new List<int>(m.VertexPositions.Length * 2);
            string hash1, hash2;

            foreach (var fi in m.FaceIndices)
            {
                int[] f;
                if (fi.Count == 3) f = new[] { (int)fi.A, (int)fi.B, (int)fi.C};
                else f = new[] { (int)fi.A, (int)fi.B, (int)fi.C , (int)fi.D};

                for (int i = 0; i < f.Length-1; i++)
                {
                   
                    hash2 = (f[i] ).ToString();

                    if (i < f.Length-1)
                    {  
                        hash1 = (f[i + 1]).ToString();

                        key = hash2 + "|" + hash1;
                        keyr = hash1 + "|" + hash2;
                    }
                    else
                    {
                        //hash1 = m.Vertices[f[0] - 1].GetHashCode().ToString();
                        hash1 = (f[0] - 1).ToString();

                        key = hash2 + "|" + hash1;
                        keyr = hash1 + "|" + hash2;
                    }

                    if (!edgeDic.ContainsKey(key) && !edgeDic.ContainsKey(keyr))
                    {
                        if (i < f.Length-1)
                        {
                            p0 = m.VertexPositions[f[i]];
                            p1 = m.VertexPositions[f[i + 1]];

                            svtx.Add(f[i]);
                            evtx.Add(f[i + 1]);
                        }
                        else
                        {
                            p0 = m.VertexPositions[f[i]];
                            p1 = m.VertexPositions[f[0]];
                            svtx.Add(f[i]);
                            evtx.Add(f[0]);
                        }
                        try
                        {
                            edgeDic.Add(key, Line.ByStartPointEndPoint(p0,p1));
                        }
                        catch (ArgumentException)
                        {
                            //catch the error
                        }

                    }

                }

            }
            startVtx = svtx.ToArray();
            endVtx = evtx.ToArray();

            return edgeDic.Values.ToArray();


        }

        [IsVisibleInDynamoLibrary(false)]
        public static int[] GetConnectedPointID(Mesh m, Point centerPoint)
        {

            int ptid = -1;
            for (int i = 0; i < m.VertexPositions.Length-1; i++)
            {
                double TOLERANCE = 0.01;
                if (Math.Abs(m.VertexPositions[i].X - centerPoint.X) < TOLERANCE && Math.Abs(m.VertexPositions[i].Y - centerPoint.Y) < TOLERANCE && Math.Abs(m.VertexPositions[i].Z - centerPoint.Z) < TOLERANCE)
                {
                    ptid = i;
                    break;
                }

            }

            return GetConnectedPointID(m, ptid);

        }

        [IsVisibleInDynamoLibrary(false)]
        public static int[] GetConnectedPointID(Mesh m, int vtxId)
        {

            int ptid = vtxId;

            int[] svtx, evtx;
            Line[] edges = GetMeshEdges(m, out svtx, out evtx);
            Dictionary<int, int> connectedID = new Dictionary<int, int>(6);

            for (int i = 0; i < svtx.Length; i++)
            {
                if (svtx[i] == ptid) connectedID.Add(evtx[i], evtx[i]);
                if (evtx[i] == ptid) connectedID.Add(svtx[i], svtx[i]);
            }

            return connectedID.Values.ToArray();

        }

        // get the plug at the node
        [IsVisibleInDynamoLibrary(false)]
        public static MPlug getPlug(string node_name, string attribute_name)
        {
            var dn = new MFnDependencyNode(getDependNode(node_name));
            var pl = dn.findPlug(attribute_name);
            return pl;
        }

        [IsVisibleInDynamoLibrary(false)]
        public static bool processGeometry(object geom,  string name, string groupName)
        {
            bool sucess = true;

            if (geom is Curve)
                DMCurve.ToMaya((Curve)geom, name);
            else if(geom is Rectangle)
                DMCurve.ToMaya((Rectangle)geom, name);
            else if (geom is Surface)
                DMSurface.ToMaya((Surface)geom, name, groupName);
            else if(geom is CoordinateSystem)
                DMLocator.ToMaya((CoordinateSystem)geom, name);
            else if(geom is Mesh)
                DMMesh.ToMaya((Mesh)geom, name);
            else if(geom is TSplineSurface)
                DMMesh.ToMayaFromTSplinesSurf((TSplineSurface)geom, name);
            else if(geom is CoordinateSystem)
                DMLocator.ToMaya((CoordinateSystem)geom, name);
            else if(geom is Point)
                DMLocator.ToMaya((Point)geom, name);
            else
            {
                sucess = false;
            }
            return sucess;
        }

        [IsVisibleInDynamoLibrary(false)]
        public static List<object> SendMelCommand(string MelCommand)
        {
            MStringArray stringResults = new MStringArray();
            MIntArray intResults = new MIntArray();
            MDoubleArray doubleResults = new MDoubleArray();
            MVectorArray vectorResults = new MVectorArray();
            List<object> results = new List<object>();
            MCommandResult mcr = new MCommandResult();


            MDagPath dag = new MDagPath();
            try
            {
                MGlobal.executeCommand(MelCommand, mcr);
                //   MGlobal.executeCommand(MelCommand, stringResults);

            }
            catch (MemberAccessException e)
            {
                MGlobal.displayWarning(e.Message);
            }

            switch (mcr.resultType)
            {
                case MCommandResult.Type.kStringArray:
                    mcr.getResult(stringResults);
                    results.AddRange(stringResults);
                    break;
                case MCommandResult.Type.kIntArray:
                    mcr.getResult(intResults);
                    results.AddRange(intResults.Cast<object>());
                    break;
                case MCommandResult.Type.kDoubleArray:
                    mcr.getResult(doubleResults);
                    results.AddRange(doubleResults.Cast<object>());
                    break;
                case MCommandResult.Type.kVectorArray:
                    mcr.getResult(vectorResults);
                    results.AddRange(vectorResults.Cast<object>());
                    break;
                default:
                    mcr.getResult(stringResults);
                    results.AddRange(stringResults);
                    break;
            }
            mcr.Dispose();
            return results;
        }

    }



}


namespace DynaMaya.ExtensionMethods
{
    [IsVisibleInDynamoLibrary(false)]
    internal static class MeshExtensions
    {
        [IsVisibleInDynamoLibrary(false)]
        public static Line[] Edges(this Mesh m)
        {
            int[] svtx, evtx;

            return DMInterop.GetMeshEdges(m, out svtx, out evtx);

        }
        [IsVisibleInDynamoLibrary(false)]
        public static int[] ConnectedVtxID(this Mesh m, Point searchPoint)
        {
            return DMInterop.GetConnectedPointID(m, searchPoint);
        }
        [IsVisibleInDynamoLibrary(false)]
        public static int[] ConnectedVtxID(this Mesh m, int vtxIndex)
        {
            return DMInterop.GetConnectedPointID(m, vtxIndex);
        }
        [IsVisibleInDynamoLibrary(false)]
        public static Point[] ConnectedVtx(this Mesh m, Point searchPoint)
        {

            int[] conectedVtxID = DMInterop.GetConnectedPointID(m, searchPoint);

            List<Point> connectedVtx = new List<Point>(conectedVtxID.Length);

            foreach (var id in conectedVtxID)
            {

                connectedVtx.Add(m.VertexPositions[id]);
            }

            return connectedVtx.ToArray();

        }
        [IsVisibleInDynamoLibrary(false)]
        public static Point[] ConnectedVtx(this Mesh m, Point searchPoint, out int[] conectedVtxIDs)
        {

            int[] conectedVtxID = DMInterop.GetConnectedPointID(m, searchPoint);
            conectedVtxIDs = conectedVtxID;

            List<Point> connectedVtx = new List<Point>(conectedVtxID.Length);

            foreach (var id in conectedVtxID)
            {

                connectedVtx.Add(m.VertexPositions[id]);
            }

            return connectedVtx.ToArray();

        }


    }


}