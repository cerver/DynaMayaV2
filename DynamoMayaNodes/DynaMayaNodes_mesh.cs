using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.DesignScript.Geometry;
using Autodesk.Maya.OpenMaya;
using Dynamo.Graph.Nodes;
using DynaMaya.Geometry;
using DynaMaya.ExtensionMethods;
using System.Threading.Tasks;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;
using DSIronPython;
using DynaMaya.Util;

namespace DynaMaya.Nodes.Meshes
{

    [IsDesignScriptCompatible]
    public static class MeshNodes
    {
        public static List<Surface> meshToNurbs(MFnMesh mayaMesh)
        {
            MFnSubd subDmesh = new MFnSubd();
            subDmesh.isIntermediateObject = true;
            MPointArray mayaVerts = new MPointArray();
            
            mayaMesh.getPoints(mayaVerts, MSpace.Space.kWorld);

            MIntArray polyVertct = new MIntArray();


            MIntArray ids = new MIntArray();
            MIntArray idList = new MIntArray();

            for (int i = 0; i < mayaMesh.numPolygons; i++)
            {
                mayaMesh.getPolygonVertices(i, ids);
                foreach (var id in ids) idList.Add(id);
                polyVertct.Add(ids.Count);

            }
            subDmesh.createBaseMesh(false, mayaMesh.numVertices, mayaMesh.numPolygons, mayaVerts, polyVertct, idList);

            try
            {
                MUintArray creaseEdgId = new MUintArray();
                MDoubleArray creaseEdgeVal = new MDoubleArray();
                mayaMesh.getCreaseEdges(creaseEdgId, creaseEdgeVal);

                foreach (var edgId in creaseEdgId)
                {
                    subDmesh.edgeSetCrease(edgId, true);
                }

            }
            catch{}

            try
            {
                MUintArray creaseVertId = new MUintArray();
                MDoubleArray creaseVertVal = new MDoubleArray();
                mayaMesh.getCreaseVertices(creaseVertId, creaseVertVal);

                foreach (var vertId in creaseVertId)
                {
                    subDmesh.vertexSetCrease(vertId, true);
                }
            }
            catch { }
         

            subDmesh.updateAllEditsAndCreases();

            MObjectArray nurbsSurfs = new MObjectArray();
            subDmesh.convertToNurbs(nurbsSurfs);

            List<MFnNurbsSurface> mfnSurfaceList = new List<MFnNurbsSurface>(nurbsSurfs.Count);
            foreach (var surf in nurbsSurfs) mfnSurfaceList.Add(new MFnNurbsSurface(surf));

            List<Surface> dynSurfaceList = new List<Surface>(mfnSurfaceList.Count);

            foreach (var mfnNS in mfnSurfaceList) dynSurfaceList.Add(DMSurface.mNurbsSurfaceToDynamoSurface(mfnNS, MSpace.Space.kObject));

            MGlobal.deleteNode(subDmesh.model);
            return dynSurfaceList;
        }

        [MultiReturn(new[] { "mesh", "mayaMesh" })]
        public static Dictionary<string, object> getSmoothMesh(MFnMesh mayaMesh)
        {
            
            MObject tempMesh = new MObject();
            MFnMeshData meshData = new MFnMeshData() ;
            MObject dataObject;
            MObject smoothedObj = new MObject();

            dataObject = meshData.create();
            smoothedObj = mayaMesh.generateSmoothMesh(dataObject);
            MFnMesh meshFn = new MFnMesh(smoothedObj);
            
          
           // var smoothMeshObj = mayaMesh.generateSmoothMesh();

            
           // MFnDependencyNode mfnDn = new MFnDependencyNode(smoothedObj);
           // var meshDag = DMInterop.getDagNode(mfnDn.name);
            Mesh dynamoMesh = DMMesh.MTDMeshFromMayaMesh(meshFn, MSpace.Space.kObject);

            //MGlobal.displayInfo(smoothedObj.apiTypeStr);

            //MGlobal.deleteNode(smoothedObj);
 

            return new Dictionary<string, object>
            {
                {"mesh", dynamoMesh},
                {"mayaMesh", meshFn}
             };

        }

        public static Point MeshCP(Mesh m, Point p)
        {
            double cdist = m.VertexPositions[0].DistanceTo(p);
            double dist = cdist;
            Point cp = m.VertexPositions[0];

            foreach (Point mp in m.VertexPositions)
            {
                dist = mp.DistanceTo(p);
                if (dist < cdist)
                {
                    cp = mp;
                    cdist = dist;
                }

            }

            return cp;

        }

        public static Line[] MeshEdges(Mesh m)
        {
            return m.Edges();

        }

        public static List<Line> MayaMeshEdges(MFnMesh mayaMesh)
        {
            List<Line> edges = new List<Line>(mayaMesh.numEdges);

            int[] edgId = new int[2];
            var verts = getMeshVerticies(mayaMesh);


            for (int i = 0; i < mayaMesh.numEdges; i++)
            {
                mayaMesh.getEdgeVertices(i, edgId);
                edges.Add(Line.ByStartPointEndPoint(verts[edgId[0]], verts[edgId[1]]));
            }

            return edges;
        }

        public static Point[] MeshConnectedVtx(Mesh m, Point vertex)
        {
            return m.ConnectedVtx(vertex);
        }

        public static int[][] getFaceVertId(MFnMesh mayaMesh)
        {

            List<int[]> vtxIds = new List<int[]>(mayaMesh.numPolygons);
            MIntArray ids = new MIntArray();

            for (int i = 0; i < mayaMesh.numPolygons; i++)
            {
                mayaMesh.getPolygonVertices(i, ids);

                vtxIds.Add(ids.ToArray());
            }
            return vtxIds.ToArray();
        }

        public static List<Point> getMeshVerticies(MFnMesh mayaMesh)
        {
            PointList verts = new PointList(mayaMesh.numVertices);
            MPointArray mayaVerts = new MPointArray();
            mayaMesh.getPoints(mayaVerts, MSpace.Space.kWorld);
           
            foreach (var v in mayaVerts)
            {
                if (MGlobal.isZAxisUp)
                    verts.Add(Point.ByCoordinates(v.x,v.y,v.z));
            else
                {
                    verts.Add(Point.ByCoordinates(v.x, -v.z, v.y));
                }
            }

            return verts;
        }

        public static List<List<Point>> getFaceVerticies(MFnMesh mayaMesh)
        {
            List<List<Point>> faces = new List<List<Point>>(mayaMesh.numVertices);
            PointList verts;
            MPointArray mayaVerts = new MPointArray();
            MIntArray ids = new MIntArray();
            mayaMesh.getPoints(mayaVerts, MSpace.Space.kWorld);


            for (int i = 0; i < mayaMesh.numPolygons; i++)
            {
                mayaMesh.getPolygonVertices(i, ids);
                verts = new PointList(ids.Count);

                foreach (var vtxid in ids)
                {
                    if (MGlobal.isZAxisUp)
                    {
                        verts.Add(Point.ByCoordinates(mayaVerts[vtxid].x, mayaVerts[vtxid].y, mayaVerts[vtxid].z));
                    }
                    else
                    {
                        verts.Add(Point.ByCoordinates(mayaVerts[vtxid].x, -mayaVerts[vtxid].z, mayaVerts[vtxid].y));
                    }
                }
                faces.Add(verts);
                

            }

            return faces;
        }

        public static List<Vector> getFaceNormals(MFnMesh mayaMesh)
        {
            List<Vector> normals = new List<Vector>(mayaMesh.numPolygons);

            MVector norm = new MVector();
            

            for (int i = 0; i < mayaMesh.numPolygons; i++)
                {
                    mayaMesh.getPolygonNormal(i, norm, MSpace.Space.kWorld);

                if (MGlobal.isZAxisUp)
                    {
                        normals.Add(Vector.ByCoordinates(norm.x, norm.y, norm.z));
                    }
                    else
                    {
                        normals.Add(Vector.ByCoordinates(norm.x, -norm.z, norm.y));
                    }
                }



            return normals;
        }

        public static List<Vector> getVertexNormals(MFnMesh mayaMesh, bool angleWeighted)
        {
            List<Vector> vecNormals = new List<Vector>(mayaMesh.numVertices);
            MFloatVectorArray norm = new MFloatVectorArray();
            mayaMesh.getVertexNormals(angleWeighted, norm, MSpace.Space.kWorld);

            if (MGlobal.isZAxisUp)
            {
                vecNormals.AddRange(norm.Select(n => Vector.ByCoordinates(n.x, n.y, n.z)));
            }
            else
            {
                vecNormals.AddRange(norm.Select(n => Vector.ByCoordinates(n.x, -n.z, n.y)));
               
            }
            return vecNormals;
        }

        public static List<Curve> getSmothMeshEdges(MFnMesh mayaMesh,  bool createInMaya = false)
        {

            //MCommandResult result = new MCommandResult();
            int ne = mayaMesh.numEdges;
            MFnTransform group = new MFnTransform();
            List<Curve> curveObjects = new List<Curve>(ne);
            MStringArray resultStr = new MStringArray();

            var fullName = mayaMesh.fullPathName.Split('|');
            string transformName = fullName[fullName.Length - 2];
           

            if (createInMaya)
            { 
                for (int i = 0; i < ne; i++)
                {
                    using (MCommandResult result = new MCommandResult())
                    {
                        MGlobal.executeCommand(
                            $"polyToCurve -name {transformName}Curves -form 2 -degree 3 -conformToSmoothMeshPreview 1 {transformName}.e[{i}]",
                            result);
                        result.getResult(resultStr);
                        curveObjects.Add(
                            DMCurve.CurveFromMfnNurbsCurveFromName(resultStr[0],
                                MSpace.Space.kPostTransform.ToString()));
                    }
                }
            }
            else
            {

                //Parallel.For(0, ne, i => {
                for(int i=0; i<ne; i++)
                {
                    using (MCommandResult result = new MCommandResult())
                    {
                        MGlobal.executeCommand(
                            $"polyToCurve -name deleteMe11232204332AA -form 2 -degree 3 -conformToSmoothMeshPreview 1 {transformName}.e[{i}]",
                            result);
                        result.getResult(resultStr);
                        curveObjects.Add(
                            DMCurve.CurveFromMfnNurbsCurveFromName(resultStr[0],
                                MSpace.Space.kPostTransform.ToString()));
                        try
                        {
                            MGlobal.deleteNode(DMInterop.getDependNode(resultStr[0]));
                        }
                        catch
                        {
                            MGlobal.displayWarning("getSmothMeshEdges: unable to delete temp object");
                        }
                    }
                }

                // });

               
            }


        return curveObjects;
        }


        public static List<List<Curve>> getSmothMeshEdgesPerFace(MFnMesh mayaMesh, bool createInMaya = false)
        {

            MCommandResult ptcResult = new MCommandResult();
            MCommandResult teResult = new MCommandResult();

            int numPoly = mayaMesh.numPolygons;

            List<List<Curve>> curveObjects = new  List<List<Curve>>(numPoly);
            MStringArray ptcResultStr = new MStringArray();
            MStringArray teResultStr = new MStringArray();
            MStringArray teResultStrFlat = new MStringArray();

            List<Curve> tempCurveArray = null;

            if (createInMaya)
            {
                
            }
            else
            {
                for (int i = 0; i < numPoly; i++)
                {
                    MGlobal.executeCommand($"polyListComponentConversion -te {mayaMesh.name}.f[{i}]", teResult);
                    teResult.getResult(teResultStr);
                    MGlobal.clearSelectionList();

                    foreach (var ters in teResultStr)
                    {
                        MGlobal.selectByName(ters, MGlobal.ListAdjustment.kAddToList);
                    }
                    MGlobal.executeCommand($"ls -sl -fl", teResult);
                    teResult.getResult(teResultStrFlat);

                    tempCurveArray = new List<Curve>((int) teResultStrFlat.length);

                    foreach (var e in teResultStrFlat)
                    {
                        MGlobal.executeCommand($"polyToCurve -name deleteMe11232204332AA -form 2 -degree 3 -conformToSmoothMeshPreview 1 {e}", ptcResult);
                        ptcResult.getResult(ptcResultStr);
                        tempCurveArray.Add(DMCurve.CurveFromMfnNurbsCurveFromName(ptcResultStr[0], MSpace.Space.kPostTransform.ToString()));
                        try
                        {
                            MGlobal.deleteNode(DMInterop.getDependNode(ptcResultStr[0]));
                        }
                        catch
                        {
                            MGlobal.displayWarning("getSmothMeshEdges: unable to delete temp object");
                        }
                    }

                    curveObjects.Add(tempCurveArray);

                }
                
            }


            return curveObjects;
        }
    }
}