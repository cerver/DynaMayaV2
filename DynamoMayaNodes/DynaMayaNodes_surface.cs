using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;
using Autodesk.Maya.OpenMaya;
using Dynamo.Graph.Nodes;
using Dynamo.Nodes;


namespace DynaMaya.Nodes.Surfaces
{

    [IsDesignScriptCompatible]
    public static class SurfaceNodes
    {


        public static Point[][] GetSurfaceCVs(MFnNurbsSurface mayaSurface)
        {

            MPointArray cvs = new MPointArray();
            mayaSurface.getCVs(cvs, MSpace.Space.kWorld);

            int cvUct = mayaSurface.numCVsInU;
            int cvVct = mayaSurface.numCVsInV;

            //setup the points
            Point[][] ctrlPts = new Point[cvUct][];
        

            for (int i = 0; i < cvUct; i++)
            {
                ctrlPts[i] = new Point[cvVct];
    

                for (int j = 0; j < cvVct; j++)
                {
 
                    if (MGlobal.isZAxisUp)
                        ctrlPts[i][j] = Point.ByCoordinates(cvs[(i * cvVct) + j].x, cvs[(i * cvVct) + j].y, cvs[(i * cvVct) + j].z);
                    else
                        ctrlPts[i][j] = Point.ByCoordinates(cvs[(i * cvVct) + j].x, -cvs[(i * cvVct) + j].z, cvs[(i * cvVct) + j].y);

                }

            }

            return ctrlPts;
        }
        public static double[] GetKnotsU(MFnNurbsSurface mayaSurface)
        {

            MDoubleArray knotU = new MDoubleArray();
            mayaSurface.getKnotsInU(knotU);
           
            return knotU.ToArray();
        }
        public static double[] GetKnotsV(MFnNurbsSurface mayaSurface)
        {

            MDoubleArray knotV = new MDoubleArray();
            mayaSurface.getKnotsInV(knotV);

            return knotV.ToArray();
        }

    }
}