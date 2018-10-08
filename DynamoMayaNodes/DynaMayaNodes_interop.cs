using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Interfaces;
using Autodesk.DesignScript.Runtime;
using Autodesk.Maya.OpenMaya;
using Dynamo.Graph.Nodes;
using Dynamo.Nodes;
using DynaMaya.Geometry;
using DynaMaya.Util;


namespace DynaMaya.Nodes.Interop
{

    [IsDesignScriptCompatible()]
    public class Send: IDisposable
    {
        [IsVisibleInDynamoLibrary(false)]
        Send()
        {
            
        }
        public  static  void SetRobotJointsRotations( double[] rotationVals, string[] jointNames = null)
        {
            string[] jName ;

            if (jointNames != null && jointNames.Length ==6) jName = jointNames;
            else
            {
                jName = new string[6] { "J1", "J2", "J3", "J4", "J5", "J6" };
                
            }

            if (rotationVals.Length != 6) new WarningException("The roation values must contain exactly 6 values, one for each axis");
            /*
            string melCmd =
                string.Format(
                    "setAttr J1.rx  {0}; setAttr J2.rz {1}; setAttr J3.rz {2}; setAttr J4.rx {3}; setAttr J5.rz {4}; setAttr J6.rx {5}; ",
                    rotationVals[0], -rotationVals[1], -rotationVals[2], rotationVals[3], rotationVals[4], rotationVals[5]+90);

            MGlobal.executeCommand(melCmd);
            */
            try
            {
                MPlug j1Plug = DMInterop.getPlug(jName[0], "rx");
                j1Plug.setDouble(rotationVals[0]* 0.0174533);
                MPlug j2Plug = DMInterop.getPlug(jName[1], "rz");
                j2Plug.setDouble(-rotationVals[1] * 0.0174533);
                MPlug j3Plug = DMInterop.getPlug(jName[2], "rz");
                j3Plug.setDouble(-rotationVals[2] * 0.0174533);
                MPlug j4Plug = DMInterop.getPlug(jName[3], "rx");
                j4Plug.setDouble(rotationVals[3] * 0.0174533);
                MPlug j5Plug = DMInterop.getPlug(jName[4], "rz");
                j5Plug.setDouble(-rotationVals[4] * 0.0174533);
                MPlug j6Plug = DMInterop.getPlug(jName[5], "rx");
                j6Plug.setDouble(rotationVals[5] * 0.0174533);
            }
            catch (Exception e)
            {
                MGlobal.displayWarning(e.Message);
            }
            
        }

 
        public static void SendGeometry(List<object> Geometry, List<string> Name)
        {
            int i = 0;
            string paddedName = "";

            foreach (var geom in Geometry)
            {
                if (Geometry.Count > 1 && Geometry.Count == Name.Count) paddedName = Name[i];
                else if (Geometry.Count == 1 && Name.Count == 1) paddedName = Name[0];
                else
                {
                    int pad = Geometry.Count.ToString().Length;
                    paddedName = Name[0] + "_" + i.ToString().PadLeft(pad);

                }

                try
                {
                    DMInterop.processGeometry(geom, paddedName, "");
                }
                catch (Exception e) { MGlobal.displayWarning(e.Message); }

                i++;

            }
        }

        public static Point objectCenter(string ObjName)
        {
            MDoubleArray results = new MDoubleArray();

            Task waitForCompletion = Task.Factory.StartNew(() => MGlobal.executeCommand("objectCenter -gl " + ObjName, results));
            waitForCompletion.Wait(5000);
            Point center;
            if (MGlobal.isZAxisUp)
            {
                center = Point.ByCoordinates(results[0], results[1], results[2]);
            }
            else
            {
                center = Point.ByCoordinates(results[0], results[2], -results[1]);
            }

            return center;
        }

        [CanUpdatePeriodically(true), IsDesignScriptCompatible()]
        public static List<object> MelCommand(string MelCommand )
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
                case MCommandResult.Type.kStringArray :
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

        public static void SetTime(double TimeVal)
        {

            MGlobal.viewFrame(TimeVal);
        }

        public static void RotateNode(List<string> GeomName, List<double> Xval, List<double> Yval, List<double> Zval)
        {
            if (GeomName.Count < 1) return;
            double x, y, z;
            bool xs = false, ys = false, zs = false;
            if (Xval.Count == 1) xs = true;
            if (Yval.Count == 1) ys = true;
            if (Zval.Count == 1) zs = true;


            for (int i = 0; i < GeomName.Count; i++)
            {
                x = xs ? Xval[0] : Xval[i];
                y = ys ? Yval[0] : Yval[i];
                z = zs ? Zval[0] : Zval[i];

                MGlobal.executeCommand(string.Format("rotate {0} {1} {2} {3}", x, y, z, GeomName[i]));
            }
        }

        public static void MoveNode(List<string> GeomName, List<double> Xval, List<double> Yval, List<double> Zval)
        {
            if (GeomName.Count < 1) return;
            double x, y, z;
            bool xs = false, ys = false, zs = false;
            if (Xval.Count == 1) xs = true;
            if (Yval.Count == 1) ys = true;
            if (Zval.Count == 1) zs = true;


            for (int i = 0; i < GeomName.Count; i++)
            {
                x = xs ? Xval[0] : Xval[i];
                y = xs ? Yval[0] : Yval[i];
                z = xs ? Zval[0] : Zval[i];

                MGlobal.executeCommand(string.Format("move {0} {1} {2} {3}", x, y, z, GeomName[i]));
            }
        }

        public static void ScaleNode(List<string> GeomName, List<double> Xval, List<double> Yval, List<double> Zval)
        {
            if (GeomName.Count < 1) return;
            double x, y, z;
            bool xs = false, ys = false, zs = false;
            if (Xval.Count == 1) xs = true;
            if (Yval.Count == 1) ys = true;
            if (Zval.Count == 1) zs = true;


            for (int i = 0; i < GeomName.Count; i++)
            {
                x = xs ? Xval[0] : Xval[i];
                y = xs ? Yval[0] : Yval[i];
                z = xs ? Zval[0] : Zval[i];

                MGlobal.executeCommand(string.Format("scale {0} {1} {2} {3}", x, y, z, GeomName[i]));
            }
        }

        public void Dispose()
        {
            
        }

        public static void SetPlugValue(string objectName, string plugName, object plugValue)
        {
            MPlug plug;
            try
            {
                plug = DMInterop.getPlug(objectName, plugName);
            }
            catch (Exception e)
            {
               MGlobal.displayWarning($"plug not found: {e.Message}");
               return;
            }
            

            if (plugValue is double)
            {
                plug.setDouble(Convert.ToDouble(plugValue));
            }
            else if (plugValue is int)
            {
                plug.setInt(Convert.ToInt32(plugValue));
            }
            else if (plugValue is Vector)
            {
                Vector vec = (Vector) plugValue;
                var mVector = new MVector();
                if (MGlobal.isYAxisUp)
                {
                    mVector.x = vec.X;
                    mVector.y = vec.Z;
                    mVector.z = -vec.Y;
                }
                else
                {
                    mVector.x = vec.X;
                    mVector.y = vec.Y;
                    mVector.z = vec.Z;
                }
               
            }

           
        }
    }



    [IsDesignScriptCompatible]
    public class Receive
    {
        [CanUpdatePeriodically(true)]
        public static double getTime()
        {
            /*
            MDGContext ct = new MDGContext();
            MTime t = new MTime();
            ct.getTime(t);

            return t.value;
            */
            Task tasker;

            double result = 0;
            tasker = Task.Factory.StartNew(() => MGlobal.executeCommand("currentTime -query", out result));
            tasker.Wait();
            return result;
        }

        [CanUpdatePeriodically(true)]
        [MultiReturn(new[] { "Particles", "Velocity" })]
        [IsVisibleInDynamoLibrary(false)]
        public static Dictionary<string, List<object>> getParticles (string name, bool unitizeVelocity = false)
        {

            
            MDoubleArray pPos = new MDoubleArray();
            MGlobal.executeCommand("getParticleAttr -at position -at velocity -array true " + name, pPos);
            List<object> particlePos = new List<object>();
            List<object> particleVec = new List<object>();
            int numPart = 0;


            var posAttr =  DMInterop.getPlug(name, "position");
            if (posAttr.isArray)
            {
                numPart = (int) posAttr.numElements;
                particlePos = new List<object>(numPart);
                particleVec = new List<object>(numPart);

                for (uint i = 0; i < numPart; i += 3)
                {
                   particlePos.Add(Point.ByCoordinates(posAttr[i].asDouble(), posAttr[i+1].asDouble(), posAttr[i+2].asDouble()));  

                }
                 
                
            }

            return new Dictionary<string, List<object>>
            {
                {"Particles", particlePos},
                {"Velocity", particleVec}
            };
        }
    }
}