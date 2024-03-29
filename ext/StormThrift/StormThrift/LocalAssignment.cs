/**
 * <auto-generated>
 * Autogenerated by Thrift Compiler (0.16.0)
 * DO NOT EDIT UNLESS YOU ARE SURE THAT YOU KNOW WHAT YOU ARE DOING
 * </auto-generated>
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Thrift;
using Thrift.Collections;

using Thrift.Protocol;
using Thrift.Protocol.Entities;
using Thrift.Protocol.Utilities;
using Thrift.Transport;
using Thrift.Transport.Client;
using Thrift.Transport.Server;
using Thrift.Processor;


#nullable disable                // suppress C# 8.0 nullable contexts (we still support earlier versions)
#pragma warning disable IDE0079  // remove unnecessary pragmas
#pragma warning disable IDE1006  // parts of the code use IDL spelling
#pragma warning disable IDE0083  // pattern matching "that is not SomeType" requires net5.0 but we still support earlier versions

namespace StormThrift
{

  public partial class LocalAssignment : TBase
  {
    private global::StormThrift.WorkerResources _resources;

    public string Topology_id { get; set; }

    public List<global::StormThrift.ExecutorInfo> Executors { get; set; }

    public global::StormThrift.WorkerResources Resources
    {
      get
      {
        return _resources;
      }
      set
      {
        __isset.resources = true;
        this._resources = value;
      }
    }


    public Isset __isset;
    public struct Isset
    {
      public bool resources;
    }

    public LocalAssignment()
    {
    }

    public LocalAssignment(string topology_id, List<global::StormThrift.ExecutorInfo> executors) : this()
    {
      this.Topology_id = topology_id;
      this.Executors = executors;
    }

    public LocalAssignment DeepCopy()
    {
      var tmp616 = new LocalAssignment();
      if((Topology_id != null))
      {
        tmp616.Topology_id = this.Topology_id;
      }
      if((Executors != null))
      {
        tmp616.Executors = this.Executors.DeepCopy();
      }
      if((Resources != null) && __isset.resources)
      {
        tmp616.Resources = (global::StormThrift.WorkerResources)this.Resources.DeepCopy();
      }
      tmp616.__isset.resources = this.__isset.resources;
      return tmp616;
    }

    public async global::System.Threading.Tasks.Task ReadAsync(TProtocol iprot, CancellationToken cancellationToken)
    {
      iprot.IncrementRecursionDepth();
      try
      {
        bool isset_topology_id = false;
        bool isset_executors = false;
        TField field;
        await iprot.ReadStructBeginAsync(cancellationToken);
        while (true)
        {
          field = await iprot.ReadFieldBeginAsync(cancellationToken);
          if (field.Type == TType.Stop)
          {
            break;
          }

          switch (field.ID)
          {
            case 1:
              if (field.Type == TType.String)
              {
                Topology_id = await iprot.ReadStringAsync(cancellationToken);
                isset_topology_id = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 2:
              if (field.Type == TType.List)
              {
                {
                  TList _list617 = await iprot.ReadListBeginAsync(cancellationToken);
                  Executors = new List<global::StormThrift.ExecutorInfo>(_list617.Count);
                  for(int _i618 = 0; _i618 < _list617.Count; ++_i618)
                  {
                    global::StormThrift.ExecutorInfo _elem619;
                    _elem619 = new global::StormThrift.ExecutorInfo();
                    await _elem619.ReadAsync(iprot, cancellationToken);
                    Executors.Add(_elem619);
                  }
                  await iprot.ReadListEndAsync(cancellationToken);
                }
                isset_executors = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 3:
              if (field.Type == TType.Struct)
              {
                Resources = new global::StormThrift.WorkerResources();
                await Resources.ReadAsync(iprot, cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            default: 
              await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              break;
          }

          await iprot.ReadFieldEndAsync(cancellationToken);
        }

        await iprot.ReadStructEndAsync(cancellationToken);
        if (!isset_topology_id)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_executors)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
      }
      finally
      {
        iprot.DecrementRecursionDepth();
      }
    }

    public async global::System.Threading.Tasks.Task WriteAsync(TProtocol oprot, CancellationToken cancellationToken)
    {
      oprot.IncrementRecursionDepth();
      try
      {
        var tmp620 = new TStruct("LocalAssignment");
        await oprot.WriteStructBeginAsync(tmp620, cancellationToken);
        var tmp621 = new TField();
        if((Topology_id != null))
        {
          tmp621.Name = "topology_id";
          tmp621.Type = TType.String;
          tmp621.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp621, cancellationToken);
          await oprot.WriteStringAsync(Topology_id, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Executors != null))
        {
          tmp621.Name = "executors";
          tmp621.Type = TType.List;
          tmp621.ID = 2;
          await oprot.WriteFieldBeginAsync(tmp621, cancellationToken);
          {
            await oprot.WriteListBeginAsync(new TList(TType.Struct, Executors.Count), cancellationToken);
            foreach (global::StormThrift.ExecutorInfo _iter622 in Executors)
            {
              await _iter622.WriteAsync(oprot, cancellationToken);
            }
            await oprot.WriteListEndAsync(cancellationToken);
          }
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Resources != null) && __isset.resources)
        {
          tmp621.Name = "resources";
          tmp621.Type = TType.Struct;
          tmp621.ID = 3;
          await oprot.WriteFieldBeginAsync(tmp621, cancellationToken);
          await Resources.WriteAsync(oprot, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        await oprot.WriteFieldStopAsync(cancellationToken);
        await oprot.WriteStructEndAsync(cancellationToken);
      }
      finally
      {
        oprot.DecrementRecursionDepth();
      }
    }

    public override bool Equals(object that)
    {
      if (!(that is LocalAssignment other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return global::System.Object.Equals(Topology_id, other.Topology_id)
        && TCollections.Equals(Executors, other.Executors)
        && ((__isset.resources == other.__isset.resources) && ((!__isset.resources) || (global::System.Object.Equals(Resources, other.Resources))));
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if((Topology_id != null))
        {
          hashcode = (hashcode * 397) + Topology_id.GetHashCode();
        }
        if((Executors != null))
        {
          hashcode = (hashcode * 397) + TCollections.GetHashCode(Executors);
        }
        if((Resources != null) && __isset.resources)
        {
          hashcode = (hashcode * 397) + Resources.GetHashCode();
        }
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp623 = new StringBuilder("LocalAssignment(");
      if((Topology_id != null))
      {
        tmp623.Append(", Topology_id: ");
        Topology_id.ToString(tmp623);
      }
      if((Executors != null))
      {
        tmp623.Append(", Executors: ");
        Executors.ToString(tmp623);
      }
      if((Resources != null) && __isset.resources)
      {
        tmp623.Append(", Resources: ");
        Resources.ToString(tmp623);
      }
      tmp623.Append(')');
      return tmp623.ToString();
    }
  }

}
