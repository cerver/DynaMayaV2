using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;
using Autodesk.Maya.OpenMaya;
using DynaMaya.Util;
//using DynamoMaya.Contract;

namespace DynaMaya.Geometry
{
    [IsVisibleInDynamoLibrary(false), Serializable]
    public class DMCurve : DMBase
    {
        [IsVisibleInDynamoLibrary(false)]
        public DMCurve()
        {
        }

        [IsVisibleInDynamoLibrary(false)]
        public DMCurve(MDagPath dagPath, MSpace.Space space)
            : base(dagPath, space)
        {
        }

        //methods


        [IsVisibleInDynamoLibrary(false)]
        public static Curve ToDynamoElement(string dagName, string axis)
        {
            return CurveFromMfnNurbsCurveFromName(dagName, axis);
        }

        [IsVisibleInDynamoLibrary(false)]
        public static void ToMaya(Curve CurveToSend, string name)
        {
            NurbsCurve ctsAsNurb = null;
            if (CurveToSend is Rectangle)
            {
                Rectangle rec = (Rectangle) CurveToSend;
                ctsAsNurb = NurbsCurve.ByControlPoints(rec.Points, 1, true);
            }
            else if (CurveToSend is Polygon)
            {
                Polygon rec = (Polygon)CurveToSend;
                ctsAsNurb = NurbsCurve.ByControlPoints(rec.Points, 1, true);
            }
            else
            {
                ctsAsNurb = CurveToSend.ToNurbsCurve();
            }
           
            var ncd = new MFnNurbsCurveData();

            
            MFnNurbsCurveForm mfnform;
            if (ctsAsNurb.IsClosed) mfnform = MFnNurbsCurveForm.kClosed;
            else mfnform = MFnNurbsCurveForm.kOpen;

            var mayaCurve = new MFnNurbsCurve();

            var vtxs = new MPointArray();

            var cvs = ctsAsNurb.ControlPoints();
            var yUp = MGlobal.isYAxisUp;

            if (yUp)
            {
                foreach (var cv in cvs)
                {
                    var pt = new MPoint(cv.X, cv.Z, -cv.Y);
                    vtxs.Add(pt);
                    //pt.Dispose();
                }
            }
            else
            {
                foreach (var cv in cvs)
                {
                    var pt = new MPoint(cv.X, cv.Y, cv.Z);
                    vtxs.Add(pt);
                    //pt.Dispose();
                }
            }

            var knots = ctsAsNurb.Knots();
            var crvKnots = new MDoubleArray(knots);
            crvKnots.RemoveAt(0);
            crvKnots.RemoveAt(crvKnots.Count - 1);

            MDagPath node = null;
            var nodeExists = false;

            Task checkNode = null;
            Task deleteNode = null;

            try
            {
                node = DMInterop.getDagNode(name);
                nodeExists = true;
            }
            catch (Exception)
            {
                nodeExists = false;
            }

            MObject obj;

            if (nodeExists)
            {
                MDagPath nodeShape = node;
                nodeShape.extendToShape();
                var modifyCrv = new MDGModifier();
                mayaCurve = new MFnNurbsCurve(nodeShape);

                try
                {

                    MFnNurbsCurveData dataCreator = new MFnNurbsCurveData();
                    MObject outCurveData = dataCreator.create();
                    var span = (vtxs.Count - ctsAsNurb.Degree);
                    string rblCmd = $"rebuildCurve -rt 0 -s {span} -d {ctsAsNurb.Degree} {name}";

                    if (mayaCurve.numCVs != vtxs.Count || mayaCurve.degree != ctsAsNurb.Degree)
                    {
                        MGlobal.executeCommand(rblCmd);

                    }
                
                        mayaCurve.setCVs(vtxs);
                        mayaCurve.setKnots(crvKnots, 0, crvKnots.length - 1);
                        mayaCurve.updateCurve();
                        modifyCrv.doIt();

                        if (CurveToSend.GetType() == typeof(Circle))
                        {
                            span = 8;
                            rblCmd = $"rebuildCurve -rt 0 -s {span} {name}";
                            MGlobal.executeCommand(rblCmd);
                        }


                }
                catch (Exception e)
                {
                    MGlobal.displayWarning(e.Message);
                        
                }


                
                
            }
   
            else
            {
                obj = mayaCurve.create(vtxs, crvKnots, (uint) ctsAsNurb.Degree, (MFnNurbsCurve.Form) mfnform,
                    false, ctsAsNurb.IsRational);
                MFnDependencyNode nodeFn = new MFnDagNode(obj);
                nodeFn.setName(name);
             
            }
        }

        internal static void decomposeMayaCurve(MDagPath dagnode, MSpace.Space space,
            out Point3DCollection controlVertices, out List<double> weights, out List<double> knots, out int degree,
            out bool closed, out bool rational)
        {
            var nc = new MFnNurbsCurve(dagnode);
            var cvct = nc.numSpans;
            var p_aCVs = new MPointArray();

            degree = nc.degree;
            closed = nc.form == MFnNurbsCurve.Form.kPeriodic ? true : false;
            rational = true;
            nc.getCVs(p_aCVs, space);


            controlVertices = new Point3DCollection();
            weights = new List<double>();
            if (MGlobal.isYAxisUp)
            {
                if (closed)
                {
                    for (var i = 0; i < cvct; i++)
                    {
                        controlVertices.Add(new Point3D(p_aCVs[i].x, p_aCVs[i].y, p_aCVs[i].z));
                        weights.Add(1.0);
                    }
                }
                else
                {
                    foreach (var p in p_aCVs)
                    {
                        controlVertices.Add(new Point3D(p.x, p.y, p.z));
                        weights.Add(1.0);
                    }
                }
            }
            else
            {
                if (closed)
                {
                    for (var i = 0; i < cvct; i++)
                    {
                        controlVertices.Add(new Point3D(p_aCVs[i].x, -p_aCVs[i].z, p_aCVs[i].y));
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
            }

            double min = 0, max = 0;
            nc.getKnotDomain(ref min, ref max);
            var d_aKnots = new MDoubleArray();
            nc.getKnots(d_aKnots);

            knots = new List<double>();
            knots.Add(min);
            knots.AddRange(d_aKnots);
            knots.Add(max);

            nc.Dispose();
            d_aKnots.Dispose();
        }

        internal static Curve CurveFromMfnNurbsCurveFromDag(MDagPath dagPath, MSpace.Space space)
        {
            Point3DCollection controlVertices;
            List<double> weights, knots;
            int degree;
            bool closed, rational;

            decomposeMayaCurve(dagPath, space, out controlVertices, out weights, out knots, out degree, out closed,
                out rational);

            // var controlPoints = new List<Point>(controlVertices.Count);
            var curvePoints = new PointList(controlVertices.Count);

            if (MGlobal.isYAxisUp)
                curvePoints.AddRange(controlVertices.Select(cv => Point.ByCoordinates(cv.X, -cv.Z, cv.Y)));
            else
                curvePoints.AddRange(controlVertices.Select(cv => Point.ByCoordinates(cv.X, cv.Y, cv.Z)));

            Curve theCurve;
            if (closed)
                theCurve = NurbsCurve.ByControlPoints(curvePoints, degree, true);
            else
                theCurve = NurbsCurve.ByControlPointsWeightsKnots(curvePoints, weights.ToArray(), knots.ToArray(),
                    degree);


            curvePoints.Dispose();

            return theCurve;
        }

        internal static Curve CurveFromMfnNurbsCurveFromName(string dagName, string space)
        {
            var mspace = MSpace.Space.kWorld;
            Enum.TryParse(space, out mspace);
            var dagPath = DMInterop.getDagNode(dagName);
            return CurveFromMfnNurbsCurveFromDag(dagPath, mspace);
        }
    }
}