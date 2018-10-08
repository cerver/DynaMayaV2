using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;
using DynamoMaya.Contract;
using Dynamo.Models;
using Dynamo.Nodes;
using DynaMaya.Geometry;
using DynaMaya.Util;


namespace DynaMaya.Nodes
{

    [IsVisibleInDynamoLibrary(false), IsDesignScriptCompatible]
    public class DynaMayaRemoteNodes
    {

        
        /// <summary>
        ///Get Curve via remote connection to maya
        /// </summary>
        /// <param name="curveName"></param>
        public static Curve GetCurveByNameRemote(string curveName)
        {
            var s = MayaCommunication.OpenChannelToMaya();
            var dagpath = s.getDagNode(curveName);
            MayaCommunication.CloseChannelToMaya(s);

            return DMCurve.MTDCurveFromDag(dagpath, 0);
        }
   
        public static Curve[] ReceiveAllCurvesRemote(string CS)
        {
            List<Curve> MayaCurves;
            var lMayaNurbsCurves = new List<string>();
 
            try
            {
                var s = MayaCommunication.OpenChannelToMaya();
                lMayaNurbsCurves = s.getMayaNodesByType(MFnType.kNurbsCurve);
                MayaCommunication.CloseChannelToMaya(s);
            }
            finally
            {
                MayaCurves = new List<Curve>(lMayaNurbsCurves.Count);
                foreach (var c in lMayaNurbsCurves)
                {
                    MayaCurves.Add(DMCurve.MTDCurveFromName(c, CS));
                }
            }


            return MayaCurves.ToArray();
        }
        
     

        [IsVisibleInDynamoLibrary(true)]
        public static void SendCurves( List<Curve> Crv, string CurveName = "DynamoCurve")
        {
            if (Crv.Count > 0)
            {
                for (int i = 0; i < Crv.Count; i++)
                {
                    DMCurve.DynamoCurveToMaya(Crv[i], CurveName);
                    
                }
            }


        }

    }
}