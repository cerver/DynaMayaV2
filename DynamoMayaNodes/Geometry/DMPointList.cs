using System;
using System.Collections.Generic;
using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;


namespace DynaMaya.Geometry
{
    // A delegate type for hooking up change notifications.

   
    internal class PointList: List<Point>, IDisposable
    {
        [IsVisibleInDynamoLibrary(false)]
        public PointList()
        {
            
        }
        [IsVisibleInDynamoLibrary(false)]
        public PointList(int count)
        {
            this.Capacity=count;
        }
        [IsVisibleInDynamoLibrary(false)]
        public PointList(List<Point> pointList)
        {
            this.AddRange(pointList);
        }
        [IsVisibleInDynamoLibrary(false)]
        public void Dispose()
        {
            foreach (var itm in this)
            {
                itm.Dispose();
            }
        }
    }

}