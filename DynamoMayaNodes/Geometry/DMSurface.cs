using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;
using Autodesk.Maya.OpenMaya;
using DynaMaya.Util;

namespace DynaMaya.Geometry
{
    [IsVisibleInDynamoLibrary(false), Serializable]
    public class DMSurface : DMBase
    {
  
        [IsVisibleInDynamoLibrary(false)]
        public DMSurface()
        {
           
        }
        [IsVisibleInDynamoLibrary(false)]
        public DMSurface(MDagPath dagPath, string mspace)
            : base(dagPath, mspace)
        {
            // MayaMesh = new MFnMesh(dagPath);
            DagShape = dagPath;
            AddEvents(dagPath);
            DagNode = new MFnDagNode(dagPath);
        }


        [IsVisibleInDynamoLibrary(false)]
        public DMSurface(MDagPath dagPath, MSpace.Space space)
            : base(dagPath, space)
        {
            DagShape = dagPath;
            AddEvents(dagPath);
            DagNode = new MFnDagNode(dagPath);
        }

        [IsVisibleInDynamoLibrary(false)]
        public static Surface ToDynamoElement(string dagName, string axis)
        {

            return mNurbsSurfaceToDynamoSurfaceFromName(dagName, axis);
            
        }

        [IsVisibleInDynamoLibrary(false)]
        public static void ToMaya(Surface SurfaceToSend, string name, string groupName)
        {
 
            NurbsSurface dynSurf;
            try
            {
                dynSurf = SurfaceToSend.ToNurbsSurface();
            }
            catch (Exception)
            {
                dynSurf = null;
                MGlobal.displayWarning("Surface has no nurbSurface form");
                
            }

            //MFnNurbsSurface updatedSurface;
            MPointArray cvs = new MPointArray();

            Point[][] ctrlPts = dynSurf.ControlPoints();

            for (int i = 0; i < ctrlPts.Length ; i++)
            {
                
                for (int j = 0; j < ctrlPts[i].Length; j++)
                {

                    MPoint p = new MPoint();
                    if (MGlobal.isZAxisUp)
                    {
                        p.x = ctrlPts[i][j].X;
                        p.y = ctrlPts[i][j].Y;
                        p.z = ctrlPts[i][j].Z;
                    }
                    else
                    {
                        p.x = ctrlPts[i][j].X;
                        p.y = ctrlPts[i][j].Z;
                        p.z = -ctrlPts[i][j].Y;
                    }
                   

                    cvs.Add(p);
                }
            }
            MDoubleArray uknot  = new MDoubleArray(dynSurf.UKnots());
            uknot.RemoveAt(0);
            uknot.RemoveAt(uknot.Count - 1);
            MDoubleArray vknot = new MDoubleArray(dynSurf.VKnots());
            vknot.RemoveAt(0);
            vknot.RemoveAt(vknot.Count - 1);

            MFnNurbsSurface.Form formU = MFnNurbsSurface.Form.kInvalid;
            MFnNurbsSurface.Form formV = MFnNurbsSurface.Form.kInvalid;

            if (dynSurf.IsPeriodicInU) formU = MFnNurbsSurface.Form.kPeriodic;
            else if (dynSurf.ClosedInU) formU = MFnNurbsSurface.Form.kClosed;
            else formU = MFnNurbsSurface.Form.kOpen;

            if (dynSurf.IsPeriodicInV) formV = MFnNurbsSurface.Form.kPeriodic;
            else if (dynSurf.ClosedInV) formV = MFnNurbsSurface.Form.kClosed;
            else formV = MFnNurbsSurface.Form.kOpen;

            MDagPath existingDagPath = null;
            bool nodeExists = false;

            //trims
            //toDo: impement trims
            

            Task checkNode = null;

            

            try
            {
                checkNode = Task.Factory.StartNew(() => existingDagPath = DMInterop.getDagNode(name));
                checkNode.Wait(500);
                
                nodeExists = true; 
            }
            catch (Exception)
            {

                nodeExists = false;
            }

            MObject obj;
            if (nodeExists)
            {
                if (checkNode.IsCompleted)
                {
             
                    MFnNurbsSurface existingSurface = new MFnNurbsSurface(existingDagPath);
                    MDGModifier mdgModifier = new MDGModifier();



                    // if (existingSurface.degreeU == dynSurf.DegreeU && existingSurface.degreeV== dynSurf.DegreeV && existingSurface.numCVsInU == ctrlPts.Length && existingSurface.numCVsInV == ctrlPts[0].Length )
                    //{

                    if (existingSurface.degreeU != dynSurf.DegreeU || existingSurface.degreeV != dynSurf.DegreeV || existingSurface.numCVsInU != ctrlPts.Length || existingSurface.numCVsInV != ctrlPts[0].Length)
                    {
                        //this is a hack to rebuild the surface. proper way is to make new surface and assign as input to aold surface
                        MGlobal.executeCommand(string.Format("rebuildSurface -du {0} -dv {1} -su {2} -sv {3} {4}", dynSurf.DegreeU, dynSurf.DegreeV, ctrlPts.Length-3, ctrlPts[0].Length-3, name));
                    }
                      //  updatedSurface = existingSurface;
                    existingSurface.setCVs(cvs);
                    existingSurface.setKnotsInU(uknot,0, (uint)existingSurface.numKnotsInU-1);
                    existingSurface.setKnotsInV(vknot, 0, (uint)existingSurface.numKnotsInV - 1);
                    existingSurface.updateSurface();
                         

                    // }
                     /*
                     else
                     
                     {
                        //get all the existing node types
                        MFnDagNode dagNodeExist = new MFnDagNode(existingDagPath);
                        MObject existSurfObj = existingDagPath.node;
                        MFnDependencyNode depNodeExist = new MFnDependencyNode(existSurfObj);                       


                        updatedSurface = new MFnNurbsSurface();
                        var newSurfObj = dagNodeExist.duplicate();
                        updatedSurface.isIntermediateObject = true;

                        updatedSurface.create(cvs, uknot, vknot, (uint)dynSurf.DegreeU, (uint)dynSurf.DegreeV, formU, formV, dynSurf.IsRational, newSurfObj);
                        MFnDependencyNode depNodeNew = new MFnDependencyNode(newSurfObj);

                        MPlug outSurf = depNodeExist.findPlug("outputSurface");
                        MPlug inSurf = depNodeNew.findPlug("inputSurface");
                       

                        mdgModifier.connect(outSurf, inSurf);
                        
                       
                    }
                    */
                    mdgModifier.doIt();
                    
                    // MFnDependencyNode nodeFn = new MFnDagNode(mSurf);
                    //nodeFn.setName(name);
                    //MGlobal.executeCommand("sets -e -forceElement initialShadingGroup " + nodeFn.name);


                }
            }
            else
            {
                if (checkNode.IsCompleted)
                {
                    MFnNurbsSurface updatedSurface = new MFnNurbsSurface();
                    obj = updatedSurface.create(cvs, uknot, vknot, (uint)dynSurf.DegreeU, (uint)dynSurf.DegreeV, formU, formV, dynSurf.IsRational);
                    MFnDependencyNode nodeFn = new MFnDagNode(obj);
                    
                    nodeFn.setName(name);                
                    MGlobal.executeCommand("sets -e -forceElement initialShadingGroup "+ nodeFn.name);
                    

                }
            }

            if (groupName != "")
            {
                bool groupExists = false;
                try
                {
                    DMInterop.getDagNode(groupName);
                    groupExists = true;
                }
                catch { }

                if(groupExists)
                {
                    MGlobal.executeCommand(string.Format("parent {0} {1}",groupName, name));

                }else
                {
                    MGlobal.executeCommand(string.Format("group -n {0} {1}", groupName, name));
                }
            }
                

        }

        [IsVisibleInDynamoLibrary(false)]
        public static MFnNurbsSurface GetMayaObject(string dagName, string space)
        {
            MDagPath dagPath = DMInterop.getDagNode(dagName);
            MSpace.Space mspace = MSpace.Space.kWorld;
            Enum.TryParse(space, out mspace);

            MFnNurbsSurface mayaObject = new MFnNurbsSurface(dagPath);


            return mayaObject;

        }
        internal static Surface mNurbsSurfaceToDynamoSurfaceFromDag(MDagPath dagPath, MSpace.Space space)
        {
            MFnNurbsSurface surface = new MFnNurbsSurface(dagPath);
            return mNurbsSurfaceToDynamoSurface(surface, space);
        }
            internal static Surface mNurbsSurfaceToDynamoSurface(MFnNurbsSurface surface, MSpace.Space space)
        {

            MPointArray cvs = new MPointArray();
            surface.getCVs(cvs, space);

            MDoubleArray knotU = new MDoubleArray();
            MDoubleArray knotV = new MDoubleArray();
            

            surface.getKnotsInU(knotU);
            surface.getKnotsInV(knotV);
            double Us = knotU[0], Ue = knotU[knotU.Count-1], Vs = knotV[0], Ve = knotV[knotV.Count - 1];
            //surface.getKnotDomain(ref Us, ref Ue, ref Vs, ref Ve);
          
            knotU.insert(Us,0);
            knotU.Add(Ue);
            knotV.insert(Vs, 0);
            knotV.Add(Ve);

            int cvUct = surface.numCVsInU;
            int cvVct = surface.numCVsInV;

            int uDeg = surface.degreeU;
            int vDeg = surface.degreeV;

            Point[][] ctrlPts = new Point[cvUct][];
            double[][] weights = new double[cvUct][];

            for (int i=0; i< cvUct; i++)
            {
                ctrlPts[i] = new Point[cvVct];
                weights[i] = new double[cvVct];

                for (int j = 0; j < cvVct; j++)
                {
                    weights[i][j] = 1;
                    if(MGlobal.isZAxisUp)
                        ctrlPts[i][j] = Point.ByCoordinates(cvs[(i* cvVct) + j].x , cvs[(i * cvVct) + j].y, cvs[(i * cvVct) + j].z);
                    else
                        ctrlPts[i][j] = Point.ByCoordinates(cvs[(i * cvVct) + j].x, -cvs[(i * cvVct) + j].z, cvs[(i * cvVct) + j].y);

                }

            }

            //Surface result = NurbsSurface.ByControlPoints(ctrlPts, uDeg, vDeg);
            Surface result = NurbsSurface.ByControlPointsWeightsKnots(ctrlPts, weights, knotU.ToArray(), knotV.ToArray(), uDeg, vDeg);
            return result;
        }

        internal static Surface mNurbsSurfaceToDynamoSurfaceFromName(string dagName, string space)
        {
            
            MSpace.Space mspace = MSpace.Space.kWorld;
            Enum.TryParse(space, out mspace);
            MDagPath dagPath = DMInterop.getDagNode(dagName);
            return mNurbsSurfaceToDynamoSurfaceFromDag(dagPath, mspace);
          
            
        }

       
    }
}