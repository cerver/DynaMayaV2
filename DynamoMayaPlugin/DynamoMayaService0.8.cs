using System.Collections.Generic;
using System.ServiceModel;
using System.Windows.Media.Media3D;
using Autodesk.Maya.OpenMaya;
using DynamoMaya.Contract;

namespace DynamoMaya.Service
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public class ServiceImplementation : IService
    {
        /*
      static Action m_update= delegate { };
      public void SubscribeEvent()
      {
          IEvents subscriber = OperationContext.Current.GetCallbackChannel<IEvents>();
          m_update += subscriber.hasUpdated;
      }

      public static void SendUpdateEvent()
      {
          m_update();
      }

      public void UpdateEvent()
      {
          ServiceImplementation.SendUpdateEvent();
      }
      */

        public List<string> getMayaNodesByType(MFnType t)
        {
            var lMayaNodes = new List<string>();
            var itdagn = new MItDag(MItDag.TraversalType.kBreadthFirst, (MFn.Type) t);
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

        // get the DAG node
        public MDagPath getDagNode(string node_name)
        {
            var sl = new MSelectionList();
            sl.add(node_name, true);
            var dp = new MDagPath();
            sl.getDagPath(0, dp);
            return dp;
        }

        // get the plug at the node
        public MPlug getPlug(string node_name, string attribute_name)
        {
            var dn = new MFnDependencyNode(getDependNode(node_name));
            var pl = dn.findPlug(attribute_name);
            return pl;
        }

        public void sendCurveToMaya(string node_name, Point3DCollection controlVertices, List<double> knots, int degree,
            MFnNurbsCurveForm form)
        {
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

            nc.create(p_aControlVertices, d_aKnots, (uint) degree, (MFnNurbsCurve.Form) form, false, true, oOwner);

            plDynamoCreate.setMObject(oOwner);

            MGlobal.executeCommandOnIdle(string.Format("dgdirty {0}.create;", node_name));
        }

        public void receiveCurveFromMayaFromDag(MDagPath dagnode, int space, out Point3DCollection controlVertices,
            out List<double> weights, out List<double> knots, out int degree, out bool closed, out bool rational)
        {
            // var dagnode = getDagNode(node_name);
            //curveDag = dagnode;
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
            if (MGlobal.isZAxisUp)
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

        public void receiveCurveFromMaya(string nodeName, int space, out Point3DCollection controlVertices,
            out List<double> weights, out List<double> knots, out int degree, out bool closed, out bool rational)
        {
            var dagnode = getDagNode(nodeName);
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
            if (MGlobal.isZAxisUp)
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

        public Point3DCollection receiveVertexPositionsFromMaya(string node_name)
        {
            var plLocal = getPlug(node_name, "outMesh");
            var oOutMesh = new MObject();
            plLocal.getValue(oOutMesh);
            var m = new MFnMesh(oOutMesh);
            var p_aVertices = new MPointArray();
            m.getPoints(p_aVertices, MSpace.Space.kWorld);
            var vertices = new Point3DCollection();
            foreach (var p in p_aVertices)
            {
                vertices.Add(new Point3D(p.x, p.y, p.z));
            }
            return vertices;
        }

        // get the dependency node
        private MObject getDependNode(string node_name)
        {
            var sl = new MSelectionList();
            sl.add(node_name, true);
            var o = new MObject();
            sl.getDependNode(0, o);
            return o;
        }
    }
}