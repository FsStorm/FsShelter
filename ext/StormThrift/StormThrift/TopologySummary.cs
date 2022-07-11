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

  public partial class TopologySummary : TBase
  {
    private string _sched_status;
    private string _owner;
    private int _replication_count;
    private double _requested_memonheap;
    private double _requested_memoffheap;
    private double _requested_cpu;
    private double _assigned_memonheap;
    private double _assigned_memoffheap;
    private double _assigned_cpu;

    public string Id { get; set; }

    public string Name { get; set; }

    public int Num_tasks { get; set; }

    public int Num_executors { get; set; }

    public int Num_workers { get; set; }

    public int Uptime_secs { get; set; }

    public string Status { get; set; }

    public string Sched_status
    {
      get
      {
        return _sched_status;
      }
      set
      {
        __isset.sched_status = true;
        this._sched_status = value;
      }
    }

    public string Owner
    {
      get
      {
        return _owner;
      }
      set
      {
        __isset.owner = true;
        this._owner = value;
      }
    }

    public int Replication_count
    {
      get
      {
        return _replication_count;
      }
      set
      {
        __isset.replication_count = true;
        this._replication_count = value;
      }
    }

    public double Requested_memonheap
    {
      get
      {
        return _requested_memonheap;
      }
      set
      {
        __isset.requested_memonheap = true;
        this._requested_memonheap = value;
      }
    }

    public double Requested_memoffheap
    {
      get
      {
        return _requested_memoffheap;
      }
      set
      {
        __isset.requested_memoffheap = true;
        this._requested_memoffheap = value;
      }
    }

    public double Requested_cpu
    {
      get
      {
        return _requested_cpu;
      }
      set
      {
        __isset.requested_cpu = true;
        this._requested_cpu = value;
      }
    }

    public double Assigned_memonheap
    {
      get
      {
        return _assigned_memonheap;
      }
      set
      {
        __isset.assigned_memonheap = true;
        this._assigned_memonheap = value;
      }
    }

    public double Assigned_memoffheap
    {
      get
      {
        return _assigned_memoffheap;
      }
      set
      {
        __isset.assigned_memoffheap = true;
        this._assigned_memoffheap = value;
      }
    }

    public double Assigned_cpu
    {
      get
      {
        return _assigned_cpu;
      }
      set
      {
        __isset.assigned_cpu = true;
        this._assigned_cpu = value;
      }
    }


    public Isset __isset;
    public struct Isset
    {
      public bool sched_status;
      public bool owner;
      public bool replication_count;
      public bool requested_memonheap;
      public bool requested_memoffheap;
      public bool requested_cpu;
      public bool assigned_memonheap;
      public bool assigned_memoffheap;
      public bool assigned_cpu;
    }

    public TopologySummary()
    {
    }

    public TopologySummary(string id, string name, int num_tasks, int num_executors, int num_workers, int uptime_secs, string status) : this()
    {
      this.Id = id;
      this.Name = name;
      this.Num_tasks = num_tasks;
      this.Num_executors = num_executors;
      this.Num_workers = num_workers;
      this.Uptime_secs = uptime_secs;
      this.Status = status;
    }

    public TopologySummary DeepCopy()
    {
      var tmp135 = new TopologySummary();
      if((Id != null))
      {
        tmp135.Id = this.Id;
      }
      if((Name != null))
      {
        tmp135.Name = this.Name;
      }
      tmp135.Num_tasks = this.Num_tasks;
      tmp135.Num_executors = this.Num_executors;
      tmp135.Num_workers = this.Num_workers;
      tmp135.Uptime_secs = this.Uptime_secs;
      if((Status != null))
      {
        tmp135.Status = this.Status;
      }
      if((Sched_status != null) && __isset.sched_status)
      {
        tmp135.Sched_status = this.Sched_status;
      }
      tmp135.__isset.sched_status = this.__isset.sched_status;
      if((Owner != null) && __isset.owner)
      {
        tmp135.Owner = this.Owner;
      }
      tmp135.__isset.owner = this.__isset.owner;
      if(__isset.replication_count)
      {
        tmp135.Replication_count = this.Replication_count;
      }
      tmp135.__isset.replication_count = this.__isset.replication_count;
      if(__isset.requested_memonheap)
      {
        tmp135.Requested_memonheap = this.Requested_memonheap;
      }
      tmp135.__isset.requested_memonheap = this.__isset.requested_memonheap;
      if(__isset.requested_memoffheap)
      {
        tmp135.Requested_memoffheap = this.Requested_memoffheap;
      }
      tmp135.__isset.requested_memoffheap = this.__isset.requested_memoffheap;
      if(__isset.requested_cpu)
      {
        tmp135.Requested_cpu = this.Requested_cpu;
      }
      tmp135.__isset.requested_cpu = this.__isset.requested_cpu;
      if(__isset.assigned_memonheap)
      {
        tmp135.Assigned_memonheap = this.Assigned_memonheap;
      }
      tmp135.__isset.assigned_memonheap = this.__isset.assigned_memonheap;
      if(__isset.assigned_memoffheap)
      {
        tmp135.Assigned_memoffheap = this.Assigned_memoffheap;
      }
      tmp135.__isset.assigned_memoffheap = this.__isset.assigned_memoffheap;
      if(__isset.assigned_cpu)
      {
        tmp135.Assigned_cpu = this.Assigned_cpu;
      }
      tmp135.__isset.assigned_cpu = this.__isset.assigned_cpu;
      return tmp135;
    }

    public async global::System.Threading.Tasks.Task ReadAsync(TProtocol iprot, CancellationToken cancellationToken)
    {
      iprot.IncrementRecursionDepth();
      try
      {
        bool isset_id = false;
        bool isset_name = false;
        bool isset_num_tasks = false;
        bool isset_num_executors = false;
        bool isset_num_workers = false;
        bool isset_uptime_secs = false;
        bool isset_status = false;
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
                Id = await iprot.ReadStringAsync(cancellationToken);
                isset_id = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 2:
              if (field.Type == TType.String)
              {
                Name = await iprot.ReadStringAsync(cancellationToken);
                isset_name = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 3:
              if (field.Type == TType.I32)
              {
                Num_tasks = await iprot.ReadI32Async(cancellationToken);
                isset_num_tasks = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 4:
              if (field.Type == TType.I32)
              {
                Num_executors = await iprot.ReadI32Async(cancellationToken);
                isset_num_executors = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 5:
              if (field.Type == TType.I32)
              {
                Num_workers = await iprot.ReadI32Async(cancellationToken);
                isset_num_workers = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 6:
              if (field.Type == TType.I32)
              {
                Uptime_secs = await iprot.ReadI32Async(cancellationToken);
                isset_uptime_secs = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 7:
              if (field.Type == TType.String)
              {
                Status = await iprot.ReadStringAsync(cancellationToken);
                isset_status = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 513:
              if (field.Type == TType.String)
              {
                Sched_status = await iprot.ReadStringAsync(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 514:
              if (field.Type == TType.String)
              {
                Owner = await iprot.ReadStringAsync(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 515:
              if (field.Type == TType.I32)
              {
                Replication_count = await iprot.ReadI32Async(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 521:
              if (field.Type == TType.Double)
              {
                Requested_memonheap = await iprot.ReadDoubleAsync(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 522:
              if (field.Type == TType.Double)
              {
                Requested_memoffheap = await iprot.ReadDoubleAsync(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 523:
              if (field.Type == TType.Double)
              {
                Requested_cpu = await iprot.ReadDoubleAsync(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 524:
              if (field.Type == TType.Double)
              {
                Assigned_memonheap = await iprot.ReadDoubleAsync(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 525:
              if (field.Type == TType.Double)
              {
                Assigned_memoffheap = await iprot.ReadDoubleAsync(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 526:
              if (field.Type == TType.Double)
              {
                Assigned_cpu = await iprot.ReadDoubleAsync(cancellationToken);
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
        if (!isset_id)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_name)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_num_tasks)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_num_executors)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_num_workers)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_uptime_secs)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_status)
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
        var tmp136 = new TStruct("TopologySummary");
        await oprot.WriteStructBeginAsync(tmp136, cancellationToken);
        var tmp137 = new TField();
        if((Id != null))
        {
          tmp137.Name = "id";
          tmp137.Type = TType.String;
          tmp137.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
          await oprot.WriteStringAsync(Id, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Name != null))
        {
          tmp137.Name = "name";
          tmp137.Type = TType.String;
          tmp137.ID = 2;
          await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
          await oprot.WriteStringAsync(Name, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        tmp137.Name = "num_tasks";
        tmp137.Type = TType.I32;
        tmp137.ID = 3;
        await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
        await oprot.WriteI32Async(Num_tasks, cancellationToken);
        await oprot.WriteFieldEndAsync(cancellationToken);
        tmp137.Name = "num_executors";
        tmp137.Type = TType.I32;
        tmp137.ID = 4;
        await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
        await oprot.WriteI32Async(Num_executors, cancellationToken);
        await oprot.WriteFieldEndAsync(cancellationToken);
        tmp137.Name = "num_workers";
        tmp137.Type = TType.I32;
        tmp137.ID = 5;
        await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
        await oprot.WriteI32Async(Num_workers, cancellationToken);
        await oprot.WriteFieldEndAsync(cancellationToken);
        tmp137.Name = "uptime_secs";
        tmp137.Type = TType.I32;
        tmp137.ID = 6;
        await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
        await oprot.WriteI32Async(Uptime_secs, cancellationToken);
        await oprot.WriteFieldEndAsync(cancellationToken);
        if((Status != null))
        {
          tmp137.Name = "status";
          tmp137.Type = TType.String;
          tmp137.ID = 7;
          await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
          await oprot.WriteStringAsync(Status, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Sched_status != null) && __isset.sched_status)
        {
          tmp137.Name = "sched_status";
          tmp137.Type = TType.String;
          tmp137.ID = 513;
          await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
          await oprot.WriteStringAsync(Sched_status, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Owner != null) && __isset.owner)
        {
          tmp137.Name = "owner";
          tmp137.Type = TType.String;
          tmp137.ID = 514;
          await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
          await oprot.WriteStringAsync(Owner, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if(__isset.replication_count)
        {
          tmp137.Name = "replication_count";
          tmp137.Type = TType.I32;
          tmp137.ID = 515;
          await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
          await oprot.WriteI32Async(Replication_count, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if(__isset.requested_memonheap)
        {
          tmp137.Name = "requested_memonheap";
          tmp137.Type = TType.Double;
          tmp137.ID = 521;
          await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
          await oprot.WriteDoubleAsync(Requested_memonheap, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if(__isset.requested_memoffheap)
        {
          tmp137.Name = "requested_memoffheap";
          tmp137.Type = TType.Double;
          tmp137.ID = 522;
          await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
          await oprot.WriteDoubleAsync(Requested_memoffheap, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if(__isset.requested_cpu)
        {
          tmp137.Name = "requested_cpu";
          tmp137.Type = TType.Double;
          tmp137.ID = 523;
          await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
          await oprot.WriteDoubleAsync(Requested_cpu, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if(__isset.assigned_memonheap)
        {
          tmp137.Name = "assigned_memonheap";
          tmp137.Type = TType.Double;
          tmp137.ID = 524;
          await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
          await oprot.WriteDoubleAsync(Assigned_memonheap, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if(__isset.assigned_memoffheap)
        {
          tmp137.Name = "assigned_memoffheap";
          tmp137.Type = TType.Double;
          tmp137.ID = 525;
          await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
          await oprot.WriteDoubleAsync(Assigned_memoffheap, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if(__isset.assigned_cpu)
        {
          tmp137.Name = "assigned_cpu";
          tmp137.Type = TType.Double;
          tmp137.ID = 526;
          await oprot.WriteFieldBeginAsync(tmp137, cancellationToken);
          await oprot.WriteDoubleAsync(Assigned_cpu, cancellationToken);
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
      if (!(that is TopologySummary other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return global::System.Object.Equals(Id, other.Id)
        && global::System.Object.Equals(Name, other.Name)
        && global::System.Object.Equals(Num_tasks, other.Num_tasks)
        && global::System.Object.Equals(Num_executors, other.Num_executors)
        && global::System.Object.Equals(Num_workers, other.Num_workers)
        && global::System.Object.Equals(Uptime_secs, other.Uptime_secs)
        && global::System.Object.Equals(Status, other.Status)
        && ((__isset.sched_status == other.__isset.sched_status) && ((!__isset.sched_status) || (global::System.Object.Equals(Sched_status, other.Sched_status))))
        && ((__isset.owner == other.__isset.owner) && ((!__isset.owner) || (global::System.Object.Equals(Owner, other.Owner))))
        && ((__isset.replication_count == other.__isset.replication_count) && ((!__isset.replication_count) || (global::System.Object.Equals(Replication_count, other.Replication_count))))
        && ((__isset.requested_memonheap == other.__isset.requested_memonheap) && ((!__isset.requested_memonheap) || (global::System.Object.Equals(Requested_memonheap, other.Requested_memonheap))))
        && ((__isset.requested_memoffheap == other.__isset.requested_memoffheap) && ((!__isset.requested_memoffheap) || (global::System.Object.Equals(Requested_memoffheap, other.Requested_memoffheap))))
        && ((__isset.requested_cpu == other.__isset.requested_cpu) && ((!__isset.requested_cpu) || (global::System.Object.Equals(Requested_cpu, other.Requested_cpu))))
        && ((__isset.assigned_memonheap == other.__isset.assigned_memonheap) && ((!__isset.assigned_memonheap) || (global::System.Object.Equals(Assigned_memonheap, other.Assigned_memonheap))))
        && ((__isset.assigned_memoffheap == other.__isset.assigned_memoffheap) && ((!__isset.assigned_memoffheap) || (global::System.Object.Equals(Assigned_memoffheap, other.Assigned_memoffheap))))
        && ((__isset.assigned_cpu == other.__isset.assigned_cpu) && ((!__isset.assigned_cpu) || (global::System.Object.Equals(Assigned_cpu, other.Assigned_cpu))));
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if((Id != null))
        {
          hashcode = (hashcode * 397) + Id.GetHashCode();
        }
        if((Name != null))
        {
          hashcode = (hashcode * 397) + Name.GetHashCode();
        }
        hashcode = (hashcode * 397) + Num_tasks.GetHashCode();
        hashcode = (hashcode * 397) + Num_executors.GetHashCode();
        hashcode = (hashcode * 397) + Num_workers.GetHashCode();
        hashcode = (hashcode * 397) + Uptime_secs.GetHashCode();
        if((Status != null))
        {
          hashcode = (hashcode * 397) + Status.GetHashCode();
        }
        if((Sched_status != null) && __isset.sched_status)
        {
          hashcode = (hashcode * 397) + Sched_status.GetHashCode();
        }
        if((Owner != null) && __isset.owner)
        {
          hashcode = (hashcode * 397) + Owner.GetHashCode();
        }
        if(__isset.replication_count)
        {
          hashcode = (hashcode * 397) + Replication_count.GetHashCode();
        }
        if(__isset.requested_memonheap)
        {
          hashcode = (hashcode * 397) + Requested_memonheap.GetHashCode();
        }
        if(__isset.requested_memoffheap)
        {
          hashcode = (hashcode * 397) + Requested_memoffheap.GetHashCode();
        }
        if(__isset.requested_cpu)
        {
          hashcode = (hashcode * 397) + Requested_cpu.GetHashCode();
        }
        if(__isset.assigned_memonheap)
        {
          hashcode = (hashcode * 397) + Assigned_memonheap.GetHashCode();
        }
        if(__isset.assigned_memoffheap)
        {
          hashcode = (hashcode * 397) + Assigned_memoffheap.GetHashCode();
        }
        if(__isset.assigned_cpu)
        {
          hashcode = (hashcode * 397) + Assigned_cpu.GetHashCode();
        }
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp138 = new StringBuilder("TopologySummary(");
      if((Id != null))
      {
        tmp138.Append(", Id: ");
        Id.ToString(tmp138);
      }
      if((Name != null))
      {
        tmp138.Append(", Name: ");
        Name.ToString(tmp138);
      }
      tmp138.Append(", Num_tasks: ");
      Num_tasks.ToString(tmp138);
      tmp138.Append(", Num_executors: ");
      Num_executors.ToString(tmp138);
      tmp138.Append(", Num_workers: ");
      Num_workers.ToString(tmp138);
      tmp138.Append(", Uptime_secs: ");
      Uptime_secs.ToString(tmp138);
      if((Status != null))
      {
        tmp138.Append(", Status: ");
        Status.ToString(tmp138);
      }
      if((Sched_status != null) && __isset.sched_status)
      {
        tmp138.Append(", Sched_status: ");
        Sched_status.ToString(tmp138);
      }
      if((Owner != null) && __isset.owner)
      {
        tmp138.Append(", Owner: ");
        Owner.ToString(tmp138);
      }
      if(__isset.replication_count)
      {
        tmp138.Append(", Replication_count: ");
        Replication_count.ToString(tmp138);
      }
      if(__isset.requested_memonheap)
      {
        tmp138.Append(", Requested_memonheap: ");
        Requested_memonheap.ToString(tmp138);
      }
      if(__isset.requested_memoffheap)
      {
        tmp138.Append(", Requested_memoffheap: ");
        Requested_memoffheap.ToString(tmp138);
      }
      if(__isset.requested_cpu)
      {
        tmp138.Append(", Requested_cpu: ");
        Requested_cpu.ToString(tmp138);
      }
      if(__isset.assigned_memonheap)
      {
        tmp138.Append(", Assigned_memonheap: ");
        Assigned_memonheap.ToString(tmp138);
      }
      if(__isset.assigned_memoffheap)
      {
        tmp138.Append(", Assigned_memoffheap: ");
        Assigned_memoffheap.ToString(tmp138);
      }
      if(__isset.assigned_cpu)
      {
        tmp138.Append(", Assigned_cpu: ");
        Assigned_cpu.ToString(tmp138);
      }
      tmp138.Append(')');
      return tmp138.ToString();
    }
  }

}
