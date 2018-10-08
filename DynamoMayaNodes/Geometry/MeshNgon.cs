

using Autodesk.DesignScript.Interfaces;
using Autodesk.DesignScript.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DynaMaya.Geometry
{
  public class nGoneMesh : DesignScriptEntity
  {
    internal IMeshEntity MeshEntity
    {
      get
      {
        return this.HostImpl as IMeshEntity;
      }
    }

    /// <summary>
    /// The vertex indices that make up each face in a counterclockwise fashion
    /// 
    /// </summary>
    /// <search>mesh,meshes
    ///             </search>
    public IndexGroup[] FaceIndices
    {
      get
      {
        return IndexGroup.Wrap(this.MeshEntity.FaceIndices, true);
      }
    }

    /// <summary>
    /// The normal vector at this vertex
    /// 
    /// </summary>
    /// <search>mesh,meshes
    ///             </search>
    public Vector[] VertexNormals
    {
      get
      {
        return Vector.Wrap(this.MeshEntity.VertexNormals, true);
      }
    }

    /// <summary>
    /// The positions of the vertices
    /// 
    /// </summary>
    /// <search>mesh,meshes
    ///             </search>
    public Point[] VertexPositions
    {
      get
      {
        return Point.Wrap(this.MeshEntity.VertexPositions, true);
      }
    }

    internal Mesh(IMeshEntity host, bool persist)
      : base((IDesignScriptEntity) host, persist)
    {
    }

    public override string ToString()
    {
      return "Mesh";
    }

    internal static Mesh Wrap(IMeshEntity host, bool persist = true)
    {
      if (host == null)
        return (Mesh) null;
      return new Mesh(host, persist);
    }

    internal static Mesh[] Wrap(IMeshEntity[] hosts, bool persist = true)
    {
      return Enumerable.ToArray<Mesh>(Enumerable.Select<IMeshEntity, Mesh>((IEnumerable<IMeshEntity>) hosts, (Func<IMeshEntity, Mesh>) (x => Mesh.Wrap(x, persist))));
    }

    internal static Mesh[][] Wrap(IMeshEntity[][] hosts, bool persist = true)
    {
      return Enumerable.ToArray<Mesh[]>(Enumerable.Select<IMeshEntity[], Mesh[]>((IEnumerable<IMeshEntity[]>) hosts, (Func<IMeshEntity[], Mesh[]>) (x => Mesh.Wrap(x, persist))));
    }

    internal static IMeshEntity[][] Unwrap(Mesh[][] o)
    {
      return Enumerable.ToArray<IMeshEntity[]>(Enumerable.Select<Mesh[], IMeshEntity[]>((IEnumerable<Mesh[]>) o, (Func<Mesh[], IMeshEntity[]>) (x => Mesh.Unwrap(x))));
    }

    internal static IMeshEntity[] Unwrap(Mesh[] o)
    {
      return Enumerable.ToArray<IMeshEntity>(Enumerable.Select<Mesh, IMeshEntity>((IEnumerable<Mesh>) o, (Func<Mesh, IMeshEntity>) (x => Mesh.Unwrap(x))));
    }

    internal static IMeshEntity[] Unwrap(IEnumerable<Mesh> o)
    {
      return Enumerable.ToArray<IMeshEntity>(Enumerable.Select<Mesh, IMeshEntity>(o, (Func<Mesh, IMeshEntity>) (x => Mesh.Unwrap(x))));
    }

    internal static IMeshEntity Unwrap(Mesh o)
    {
      return o.MeshEntity;
    }

    /// <summary>
    /// Create a mesh from a collection of Points and a collection of IndexGroups referencing the Point collection
    /// 
    /// </summary>
    /// <search>mesh,meshes
    ///             </search>
    public static Mesh ByPointsFaceIndices(IEnumerable<Point> vertexPositions, IEnumerable<IndexGroup> indices)
    {
      return Mesh.Wrap(HostFactory.Factory.MeshByPointsFaceIndices(Point.Unwrap(vertexPositions), IndexGroup.Unwrap(indices)), true);
    }
  }
}
