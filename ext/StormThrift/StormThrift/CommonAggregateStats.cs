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

  public partial class CommonAggregateStats : TBase
  {
    private int _num_executors;
    private int _num_tasks;
    private long _emitted;
    private long _transferred;
    private long _acked;
    private long _failed;

    public int Num_executors
    {
      get
      {
        return _num_executors;
      }
      set
      {
        __isset.num_executors = true;
        this._num_executors = value;
      }
    }

    public int Num_tasks
    {
      get
      {
        return _num_tasks;
      }
      set
      {
        __isset.num_tasks = true;
        this._num_tasks = value;
      }
    }

    public long Emitted
    {
      get
      {
        return _emitted;
      }
      set
      {
        __isset.emitted = true;
        this._emitted = value;
      }
    }

    public long Transferred
    {
      get
      {
        return _transferred;
      }
      set
      {
        __isset.transferred = true;
        this._transferred = value;
      }
    }

    public long Acked
    {
      get
      {
        return _acked;
      }
      set
      {
        __isset.acked = true;
        this._acked = value;
      }
    }

    public long Failed
    {
      get
      {
        return _failed;
      }
      set
      {
        __isset.failed = true;
        this._failed = value;
      }
    }


    public Isset __isset;
    public struct Isset
    {
      public bool num_executors;
      public bool num_tasks;
      public bool emitted;
      public bool transferred;
      public bool acked;
      public bool failed;
    }

    public CommonAggregateStats()
    {
    }

    public CommonAggregateStats DeepCopy()
    {
      var tmp335 = new CommonAggregateStats();
      if(__isset.num_executors)
      {
        tmp335.Num_executors = this.Num_executors;
      }
      tmp335.__isset.num_executors = this.__isset.num_executors;
      if(__isset.num_tasks)
      {
        tmp335.Num_tasks = this.Num_tasks;
      }
      tmp335.__isset.num_tasks = this.__isset.num_tasks;
      if(__isset.emitted)
      {
        tmp335.Emitted = this.Emitted;
      }
      tmp335.__isset.emitted = this.__isset.emitted;
      if(__isset.transferred)
      {
        tmp335.Transferred = this.Transferred;
      }
      tmp335.__isset.transferred = this.__isset.transferred;
      if(__isset.acked)
      {
        tmp335.Acked = this.Acked;
      }
      tmp335.__isset.acked = this.__isset.acked;
      if(__isset.failed)
      {
        tmp335.Failed = this.Failed;
      }
      tmp335.__isset.failed = this.__isset.failed;
      return tmp335;
    }

    public async global::System.Threading.Tasks.Task ReadAsync(TProtocol iprot, CancellationToken cancellationToken)
    {
      iprot.IncrementRecursionDepth();
      try
      {
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
              if (field.Type == TType.I32)
              {
                Num_executors = await iprot.ReadI32Async(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 2:
              if (field.Type == TType.I32)
              {
                Num_tasks = await iprot.ReadI32Async(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 3:
              if (field.Type == TType.I64)
              {
                Emitted = await iprot.ReadI64Async(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 4:
              if (field.Type == TType.I64)
              {
                Transferred = await iprot.ReadI64Async(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 5:
              if (field.Type == TType.I64)
              {
                Acked = await iprot.ReadI64Async(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 6:
              if (field.Type == TType.I64)
              {
                Failed = await iprot.ReadI64Async(cancellationToken);
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
        var tmp336 = new TStruct("CommonAggregateStats");
        await oprot.WriteStructBeginAsync(tmp336, cancellationToken);
        var tmp337 = new TField();
        if(__isset.num_executors)
        {
          tmp337.Name = "num_executors";
          tmp337.Type = TType.I32;
          tmp337.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp337, cancellationToken);
          await oprot.WriteI32Async(Num_executors, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if(__isset.num_tasks)
        {
          tmp337.Name = "num_tasks";
          tmp337.Type = TType.I32;
          tmp337.ID = 2;
          await oprot.WriteFieldBeginAsync(tmp337, cancellationToken);
          await oprot.WriteI32Async(Num_tasks, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if(__isset.emitted)
        {
          tmp337.Name = "emitted";
          tmp337.Type = TType.I64;
          tmp337.ID = 3;
          await oprot.WriteFieldBeginAsync(tmp337, cancellationToken);
          await oprot.WriteI64Async(Emitted, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if(__isset.transferred)
        {
          tmp337.Name = "transferred";
          tmp337.Type = TType.I64;
          tmp337.ID = 4;
          await oprot.WriteFieldBeginAsync(tmp337, cancellationToken);
          await oprot.WriteI64Async(Transferred, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if(__isset.acked)
        {
          tmp337.Name = "acked";
          tmp337.Type = TType.I64;
          tmp337.ID = 5;
          await oprot.WriteFieldBeginAsync(tmp337, cancellationToken);
          await oprot.WriteI64Async(Acked, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if(__isset.failed)
        {
          tmp337.Name = "failed";
          tmp337.Type = TType.I64;
          tmp337.ID = 6;
          await oprot.WriteFieldBeginAsync(tmp337, cancellationToken);
          await oprot.WriteI64Async(Failed, cancellationToken);
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
      if (!(that is CommonAggregateStats other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return ((__isset.num_executors == other.__isset.num_executors) && ((!__isset.num_executors) || (global::System.Object.Equals(Num_executors, other.Num_executors))))
        && ((__isset.num_tasks == other.__isset.num_tasks) && ((!__isset.num_tasks) || (global::System.Object.Equals(Num_tasks, other.Num_tasks))))
        && ((__isset.emitted == other.__isset.emitted) && ((!__isset.emitted) || (global::System.Object.Equals(Emitted, other.Emitted))))
        && ((__isset.transferred == other.__isset.transferred) && ((!__isset.transferred) || (global::System.Object.Equals(Transferred, other.Transferred))))
        && ((__isset.acked == other.__isset.acked) && ((!__isset.acked) || (global::System.Object.Equals(Acked, other.Acked))))
        && ((__isset.failed == other.__isset.failed) && ((!__isset.failed) || (global::System.Object.Equals(Failed, other.Failed))));
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if(__isset.num_executors)
        {
          hashcode = (hashcode * 397) + Num_executors.GetHashCode();
        }
        if(__isset.num_tasks)
        {
          hashcode = (hashcode * 397) + Num_tasks.GetHashCode();
        }
        if(__isset.emitted)
        {
          hashcode = (hashcode * 397) + Emitted.GetHashCode();
        }
        if(__isset.transferred)
        {
          hashcode = (hashcode * 397) + Transferred.GetHashCode();
        }
        if(__isset.acked)
        {
          hashcode = (hashcode * 397) + Acked.GetHashCode();
        }
        if(__isset.failed)
        {
          hashcode = (hashcode * 397) + Failed.GetHashCode();
        }
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp338 = new StringBuilder("CommonAggregateStats(");
      int tmp339 = 0;
      if(__isset.num_executors)
      {
        if(0 < tmp339++) { tmp338.Append(", "); }
        tmp338.Append("Num_executors: ");
        Num_executors.ToString(tmp338);
      }
      if(__isset.num_tasks)
      {
        if(0 < tmp339++) { tmp338.Append(", "); }
        tmp338.Append("Num_tasks: ");
        Num_tasks.ToString(tmp338);
      }
      if(__isset.emitted)
      {
        if(0 < tmp339++) { tmp338.Append(", "); }
        tmp338.Append("Emitted: ");
        Emitted.ToString(tmp338);
      }
      if(__isset.transferred)
      {
        if(0 < tmp339++) { tmp338.Append(", "); }
        tmp338.Append("Transferred: ");
        Transferred.ToString(tmp338);
      }
      if(__isset.acked)
      {
        if(0 < tmp339++) { tmp338.Append(", "); }
        tmp338.Append("Acked: ");
        Acked.ToString(tmp338);
      }
      if(__isset.failed)
      {
        if(0 < tmp339++) { tmp338.Append(", "); }
        tmp338.Append("Failed: ");
        Failed.ToString(tmp338);
      }
      tmp338.Append(')');
      return tmp338.ToString();
    }
  }

}
