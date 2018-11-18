using System;
using System.Threading.Tasks;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;
using Autodesk.Maya.OpenMaya;
using DynaMaya.Util;

namespace DynaMaya.Geometry
{
    [IsVisibleInDynamoLibrary(false)]
    public class DMLocator : DMBase
    {


        [IsVisibleInDynamoLibrary(false)]
        public DMLocator()
        {
           
        }

        [IsVisibleInDynamoLibrary(false)]
        public DMLocator(MDagPath dagPath, string mspace)
            : base(dagPath, mspace)
        {
            // MayaMesh = new MFnMesh(dagPath);
            DagShape = dagPath;
            AddEvents(dagPath);
            DagNode = new MFnDagNode(dagPath);
        }


        [IsVisibleInDynamoLibrary(false)]
        public DMLocator(MDagPath dagPath, MSpace.Space space) 
            :base (dagPath, space)
        {
            DagShape = dagPath;
            AddEvents(dagPath);
            DagNode = new MFnDagNode(dagPath);
        }

        [IsVisibleInDynamoLibrary(false)]
        public static void ToMaya(CoordinateSystem csToSend, string name)
        {
            double x = 0, y = 0, z = 0, rx = 0, ry = 0, rz=0;
            Vector vec = Vector.ByCoordinates(0,0,0);
            

            if (MGlobal.isYAxisUp)
            {
                x = csToSend.Origin.X;
                y = csToSend.Origin.Z;
                z = -csToSend.Origin.Y;
                Vector vecX = Vector.ByCoordinates(csToSend.XAxis.X, csToSend.XAxis.Z, -csToSend.XAxis.Y);
                Vector vecY = Vector.ByCoordinates(csToSend.YAxis.X, csToSend.YAxis.Z, -csToSend.YAxis.Y);
                Vector vecZ = Vector.ByCoordinates(csToSend.ZAxis.X, csToSend.ZAxis.Z, -csToSend.ZAxis.Y);
                rx = vecX.AngleWithVector(Vector.XAxis());
                ry = vecX.AngleWithVector(Vector.ZAxis());
                rz = vecX.AngleWithVector(Vector.YAxis().Reverse());
            }
            else
            {
                x = csToSend.Origin.X;
                y = csToSend.Origin.Y;
                z = csToSend.Origin.Z;
                Vector vecX = Vector.ByCoordinates(csToSend.XAxis.X, csToSend.XAxis.Y, csToSend.XAxis.Z);
                Vector vecY = Vector.ByCoordinates(csToSend.YAxis.X, csToSend.YAxis.Y, csToSend.YAxis.Z);
                Vector vecZ = Vector.ByCoordinates(csToSend.ZAxis.X, csToSend.ZAxis.Y, csToSend.ZAxis.Z);
                rx = vecX.AngleWithVector(Vector.XAxis());
                ry = vecX.AngleWithVector(Vector.YAxis());
                rz = vecX.AngleWithVector(Vector.ZAxis());
            }

            MDagPath node = null;
            bool nodeExists = false;

            Task checkNode = null;
            Task makeChangeTask = null;

            try
            {
                checkNode = Task.Factory.StartNew(() => node = DMInterop.getDagNode(name));
                checkNode.Wait(5000);

                nodeExists = true;
            }
            catch (Exception)
            {

                nodeExists = false;
            }

            if (nodeExists)
            {
                if (checkNode.IsCompleted)
                {
                    makeChangeTask = Task.Factory.StartNew(() => changeLocator(x,y,z,rx,ry,rz,name));
                    makeChangeTask.Wait(5000);

                }
            }
            else
            {
                if (checkNode.IsCompleted)
                {
                    makeChangeTask = Task.Factory.StartNew(() => createLocator(x, y, z, rx, ry, rz, name));
                    makeChangeTask.Wait(5000);
                }
            }





        }

        [IsVisibleInDynamoLibrary(false)]
        public static void ToMaya(Point ptToSend, string name)
        {
            
            CoordinateSystem cs = CoordinateSystem.ByOrigin(ptToSend);
            ToMaya(cs,name);


        }


        [IsVisibleInDynamoLibrary(false)]
        public static CoordinateSystem ToDynamoElement(string dagName, string space)
        {
            return mLocatorFromName(dagName, space);          
        }


        internal static CoordinateSystem mLocatorFromName(string dagName, string space)
        {
            MDagPath dagPath = DMInterop.getDagNode(dagName);
            MSpace.Space mspace = MSpace.Space.kWorld;
            Enum.TryParse(space, out mspace);
            //  MObject obj = DMInterop.getDependNode(dagName);


            MMatrix worldPos = dagPath.inclusiveMatrix;
            double x = worldPos[3, 0];
            double y = worldPos[3, 1];
            double z = worldPos[3, 2];
            
            MEulerRotation rot = new MEulerRotation();
            
            double xr = worldPos[3, 0];
            double yr = worldPos[3, 1];
            double zr= worldPos[3, 2];

            //MFnTransform loc = new MFnTransform(dagShape.node);
            //var vec = loc.transformation.getTranslation(mspace);
            //return Point.ByCoordinates(vec.x, vec.y, vec.z); ;
            CoordinateSystem cs;
            if (MGlobal.isZAxisUp)
            {
                cs = CoordinateSystem.ByOrigin(x, y, z);
                cs.Rotate(cs.Origin, Vector.XAxis(), x);
                cs.Rotate(cs.Origin, Vector.YAxis(), y);
                cs.Rotate(cs.Origin, Vector.ZAxis(), z);
                return cs;
            }
            else
            {
                cs = CoordinateSystem.ByOrigin(x, -z, y);
                cs.Rotate(cs.Origin, Vector.XAxis(), x);
                cs.Rotate(cs.Origin, Vector.YAxis(), y);
                cs.Rotate(cs.Origin, Vector.ZAxis(), z);
                return cs;
            }


        }

        internal static bool changeLocator(double x, double y, double z, double rx, double ry, double rz, string name)
        {
            MStringArray moveResult = new MStringArray();
            MStringArray rotateResult = new MStringArray();

            MGlobal.executeCommand(string.Format("move {0} {1} {2} {3}", x, y, z, name), moveResult);
            MGlobal.executeCommand(string.Format("rotate {0} {1} {2} {3}", rx, ry, rz, name), rotateResult);

            return true;
        }

        internal static bool createLocator(double x, double y, double z, double rx, double ry, double rz, string name)
        {
            MStringArray moveResult = new MStringArray();
            MStringArray rotateResult = new MStringArray();

            MGlobal.executeCommand(string.Format("spaceLocator -a -p {0} {1} {2} -n {3}", x, y, z, name),moveResult);
            MGlobal.executeCommand(string.Format("rotate {0} {1} {2} {3}", rx, ry, rz, name),rotateResult);

            return true;
        }


    }
}