using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Windows.Media.Media3D;
using System.Xml;
using Autodesk.DesignScript.Geometry;
using Autodesk.Maya.OpenMaya;
using DynamoMaya.Contract;
using Autodesk.DesignScript.Runtime;
using ProtoCore.AST;
using ProtoCore.SyntaxAnalysis;
using ProtoCore.Utils;
using ProtoCore.AST.ImperativeAST;
using  ProtoCore.AST.AssociativeAST;
using ProtoCore.DesignScriptParser;
using ArrayNameNode = ProtoCore.AST.AssociativeAST.ArrayNameNode;



namespace DynaMaya.Util
{
    [IsVisibleInDynamoLibrary(false)]
    public class MayaCommunication
    {
        [IsVisibleInDynamoLibrary(false)]
        public static IService OpenChannelToMaya()
        {
            ChannelFactory<IService> cf;
            var binding = new NetTcpBinding();
            binding.MaxBufferSize = int.MaxValue;
            binding.MaxReceivedMessageSize = int.MaxValue;
            binding.ReaderQuotas = XmlDictionaryReaderQuotas.Max;
            cf = new ChannelFactory<IService>(binding, "net.tcp://localhost:8000");
            return cf.CreateChannel();
        }

        [IsVisibleInDynamoLibrary(false)]
        public static void CloseChannelToMaya(IService s)
        {
            (s as ICommunicationObject).Close();
        }
    }

    [IsVisibleInDynamoLibrary(false)]
    public class DMInterop
    {

        #region Remote Helpers
        [IsVisibleInDynamoLibrary(false)]
        public static Curve MTDCurveFromNameRemote(string nodeName, int space)
        {
            Point3DCollection controlVertices;
            List<double> weights, knots;
            int degree;
            bool closed, rational;

            var s = MayaCommunication.OpenChannelToMaya();
            s.receiveCurveFromMaya(nodeName, space, out controlVertices, out weights, out knots, out degree, out closed, out rational);
            MayaCommunication.CloseChannelToMaya(s);

            var controlPoints = new List<Point>(controlVertices.Count);
            controlPoints.AddRange(controlVertices.Select(cv => Point.ByCoordinates(cv.X, cv.Z, cv.Y)));

            Curve theCurve = NurbsCurve.ByControlPointsWeightsKnots(controlPoints, weights.ToArray(), knots.ToArray(),
                degree);

            return theCurve;
        }

       
        [IsVisibleInDynamoLibrary(false)]
        public static List<string> getMayaNodesByType(MFnType t)
        {
            var lMayaNodes = new List<string>();
            var itdagn = new MItDag(MItDag.TraversalType.kBreadthFirst, (MFn.Type)t);
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

        #endregion
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
        internal static void sendCurveToMaya(string node_name, Point3DCollection controlVertices, List<double> knots, int degree, MFnNurbsCurveForm form)
        {
            /*
            var dn = new MFnDagNode(getDagNode(node_name));
            
            var plCreate = dn.findPlug("create");
            var plDynamoCreate = new MPlug();

            try
            {
                plDynamoCreate = dn.findPlug("dynamoCreate");
            }
            catch
            {
                var tAttr = new MFnTypedAttribute();
                var ldaDynamoCreate = tAttr.create("dynamoCreate", "dc", MFnData.Type.kNurbsCurve, MObject.kNullObj);
                try
                {
                    dn.addAttribute(ldaDynamoCreate, MFnDependencyNode.MAttrClass.kLocalDynamicAttr);
                    plDynamoCreate = dn.findPlug(ldaDynamoCreate);
                    var dagm = new MDagModifier();
                    dagm.connect(plDynamoCreate, plCreate);
                    dagm.doIt();
                }
                catch
                {
                    return;
                }
            }

            var ncd = new MFnNurbsCurveData();
            var oOwner = ncd.create();
            var nc = new MFnNurbsCurve();

            var p_aControlVertices = new MPointArray();
            foreach (var p in controlVertices)
            {
                p_aControlVertices.Add(new MPoint(p.X, p.Y, p.Z));
            }

            var d_aKnots = new MDoubleArray();
            for (var i = 1; i < knots.Count - 1; ++i)
            {
                d_aKnots.Add(knots[i]);
            }

            nc.create(p_aControlVertices, d_aKnots, (uint)degree, (MFnNurbsCurve.Form)form, false, true, oOwner);

            plDynamoCreate.setMObject(oOwner);

            MGlobal.executeCommandOnIdle(string.Format("dgdirty {0}.create;", node_name));
            */
        }

        [IsVisibleInDynamoLibrary(false)]
        public static DSegment3d[] GetMeshEdges(Mesh m, out int[] startVtx, out int[] endVtx)
        {
            Dictionary<string, DSegment3d> edgeDic = new Dictionary<string, DSegment3d>(m.Vertices.Length);

            string key, keyr;

            DPoint3d p0, p1;

            List<int> svtx = new List<int>(m.Vertices.Length * 2);
            List<int> evtx = new List<int>(m.Vertices.Length * 2);
            string hash1, hash2;

            foreach (var f in m.Indices)
            {
                for (int i = 0; i < f.Length; i++)
                {

                    //hash2 = m.Vertices[f[i] - 1].GetHashCode().ToString();
                    hash2 = (f[i] - 1).ToString();

                    if (i < f.Length - 1)
                    {
                        //hash1 = m.Vertices[f[i + 1] - 1].GetHashCode().ToString();
                        hash1 = (f[i + 1] - 1).ToString();

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
                        if (i < f.Length - 1)
                        {
                            p0 = m.Vertices[f[i] - 1].DPoint3d;
                            p1 = m.Vertices[f[i + 1] - 1].DPoint3d;

                            svtx.Add(f[i] - 1);
                            evtx.Add(f[i + 1] - 1);
                        }
                        else
                        {
                            p0 = m.Vertices[f[i] - 1].DPoint3d;
                            p1 = m.Vertices[f[0] - 1].DPoint3d;
                            svtx.Add(f[i] - 1);
                            evtx.Add(f[0] - 1);
                        }
                        try
                        {
                            edgeDic.Add(key, new DSegment3d(ref p0, ref p1));
                        }
                        catch (ArgumentException)
                        {
                            Feature.Print("key exits");
                        }

                    }

                }

            }
            startVtx = svtx.ToArray();
            endVtx = evtx.ToArray();

            return edgeDic.Values.ToArray();


        }

        [IsVisibleInDynamoLibrary(false)]
        public static int[] GetConnectedPointID(Mesh m, DPoint3d centerPoint)
        {

            int ptid = -1;
            for (int i = 0; i < m.Vertices.Length; i++)
            {
                if (m.Vertices[i].X == centerPoint.X && m.Vertices[i].Y == centerPoint.Y && m.Vertices[i].Z == centerPoint.Z)
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
            DSegment3d[] edges = GetMeshEdges(m, out svtx, out evtx);
            Dictionary<int, int> connectedID = new Dictionary<int, int>(6);

            for (int i = 0; i < svtx.Length; i++)
            {
                if (svtx[i] == ptid) connectedID.Add(evtx[i], evtx[i]);
                if (evtx[i] == ptid) connectedID.Add(svtx[i], svtx[i]);
            }

            return connectedID.Values.ToArray();

        }


    }


 
   
}
 