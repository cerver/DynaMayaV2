using System.Collections.Generic;
using System.ServiceModel;
using System.Windows.Media.Media3D;
using Autodesk.Maya.OpenMaya;

namespace DynamoMaya.Contract
{
    public enum MFnType
    {
        kNurbsCurve = MFn.Type.kNurbsCurve,
        kMesh = MFn.Type.kMesh,
        kParticle = MFn.Type.kParticle
    }

    public enum MFnNurbsCurveForm
    {
        kClosed = MFnNurbsCurve.Form.kClosed,
        kOpen = MFnNurbsCurve.Form.kOpen
    }


    [ServiceContract]
    public interface IService
    {
        [OperationContract]
        List<string> getMayaNodesByType(MFnType t);

        [OperationContract]
        MPlug getPlug(string node_name, string attribute_name);

        [OperationContract]
        MDagPath getDagNode(string node_name);

        [OperationContract]
        void sendCurveToMaya(string node_name, Point3DCollection controlVertices, List<double> knots, int degree,
            MFnNurbsCurveForm form);

        [OperationContract]
        void receiveCurveFromMayaFromDag(MDagPath node, int space, out Point3DCollection controlVertices,
            out List<double> weights, out List<double> knots, out int degree, out bool closed, out bool rational);

        [OperationContract]
        void receiveCurveFromMaya(string nodeName, int space, out Point3DCollection controlVertices,
            out List<double> weights, out List<double> knots, out int degree, out bool closed, out bool rational);

        [OperationContract]
        Point3DCollection receiveVertexPositionsFromMaya(string node_name);
    }
}