using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Windows.Media.Media3D;
using System.Xml;
using Autodesk.DesignScript.Geometry;
using Autodesk.Maya.OpenMaya;
using DynamoMaya.Contract;
using Autodesk.DesignScript.Runtime;
using ProtoCore.AST.AssociativeAST;
using ProtoCore.SyntaxAnalysis;


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
        public static Curve MTDCurveFromName(string nodeName, int space)
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
        public  static void SendCurveToMaya(Curve CurveToSend)
        {
            var ctsAsNurb = CurveToSend.ToNurbsCurve();

            var vtxs = new Point3DCollection();
            foreach (var vtx in ctsAsNurb.ControlPoints())
            {
                vtxs.Add(new Point3D(vtx.X, vtx.Y, vtx.Z));
            }

            MFnNurbsCurveForm mfnform;
            if (ctsAsNurb.IsClosed) mfnform = MFnNurbsCurveForm.kClosed;
            else mfnform = MFnNurbsCurveForm.kOpen;

            var s = MayaCommunication.OpenChannelToMaya();
            s.sendCurveToMaya("DynamayaCurve", vtxs, ctsAsNurb.Knots().ToList(), ctsAsNurb.Degree, mfnform);
            MayaCommunication.CloseChannelToMaya(s);
        }

        #endregion
        #region Local Helpers
        [IsVisibleInDynamoLibrary(false)]
        public static Curve MTDCurveFromDag(MDagPath nodedag, int space)
        {
            Point3DCollection controlVertices;
            List<double> weights, knots;
            int degree;
            bool closed, rational;

            CurveFromMayaFromDag(nodedag, space, out controlVertices, out weights, out knots, out degree, out closed,
                out rational);

            var controlPoints = new List<Point>(controlVertices.Count);

            controlPoints.AddRange(controlVertices.Select(cv => Point.ByCoordinates(cv.X, cv.Y, cv.Z)));

            Curve theCurve = NurbsCurve.ByControlPointsWeightsKnots(controlPoints, weights.ToArray(), knots.ToArray(),
                degree);

            return theCurve;
        }
        [IsVisibleInDynamoLibrary(false)]
        public static void CurveFromMayaFromDag(MDagPath dagnode, int space, out Point3DCollection controlVertices,
            out List<double> weights, out List<double> knots, out int degree, out bool closed, out bool rational)
        {
            var nc = new MFnNurbsCurve(dagnode);

            var p_aCVs = new MPointArray();
            switch (space)
            {
                case 0: //object
                    nc.getCVs(p_aCVs, MSpace.Space.kObject);
                    break;
                case 1: //world
                    nc.getCVs(p_aCVs, MSpace.Space.kWorld);
                    break;
                default:
                    nc.getCVs(p_aCVs, MSpace.Space.kWorld);
                    break;
            }


            controlVertices = new Point3DCollection();
            weights = new List<double>();
            if (MGlobal.isYAxisUp)
            {
                foreach (var p in p_aCVs)
                {
                    controlVertices.Add(new Point3D(p.x, p.y, p.z));
                    weights.Add(1.0);
                }
            }
            else
            {
                foreach (var p in p_aCVs)
                {
                    controlVertices.Add(new Point3D(p.x, -p.z, p.y));
                    weights.Add(1.0);
                }
            }

            double min = 0, max = 0;
            nc.getKnotDomain(ref min, ref max);
            var d_aKnots = new MDoubleArray();
            nc.getKnots(d_aKnots);

            knots = new List<double>();
            knots.Add(min);
            foreach (var d in d_aKnots)
            {
                knots.Add(d);
            }
            knots.Add(max);

            degree = nc.degree;
            closed = nc.form == MFnNurbsCurve.Form.kClosed ? true : false;
            rational = true;
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
        [IsVisibleInDynamoLibrary(false)]
        private static MObject getDependNode(string node_name)
        {
            var sl = new MSelectionList();
            sl.add(node_name, true);
            var o = new MObject();
            sl.getDependNode(0, o);
            return o;
        }

        #endregion
    }

  

    [IsVisibleInDynamoLibrary(false)]
    public static class UINodeUtility
    {
        [IsVisibleInDynamoLibrary(false)]
        public static List<Curve> GetMayaCurve()
        {
            var selectionList = MGlobal.activeSelectionList;
            List<Curve> outCurves = new List<Curve>((int)selectionList.length);
            outCurves.AddRange(from itm in selectionList.DagPaths() where itm.apiType == MFn.Type.kCurve select DMInterop.MTDCurveFromDag(itm, 0));

            return outCurves;
        }


    }


}
 