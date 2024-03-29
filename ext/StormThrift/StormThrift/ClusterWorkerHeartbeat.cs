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

  public partial class ClusterWorkerHeartbeat : TBase
  {

    public string Storm_id { get; set; }

    public Dictionary<global::StormThrift.ExecutorInfo, global::StormThrift.ExecutorStats> Executor_stats { get; set; }

    public int Time_secs { get; set; }

    public int Uptime_secs { get; set; }

    public ClusterWorkerHeartbeat()
    {
    }

    public ClusterWorkerHeartbeat(string storm_id, Dictionary<global::StormThrift.ExecutorInfo, global::StormThrift.ExecutorStats> executor_stats, int time_secs, int uptime_secs) : this()
    {
      this.Storm_id = storm_id;
      this.Executor_stats = executor_stats;
      this.Time_secs = time_secs;
      this.Uptime_secs = uptime_secs;
    }

    public ClusterWorkerHeartbeat DeepCopy()
    {
      var tmp591 = new ClusterWorkerHeartbeat();
      if((Storm_id != null))
      {
        tmp591.Storm_id = this.Storm_id;
      }
      if((Executor_stats != null))
      {
        tmp591.Executor_stats = this.Executor_stats.DeepCopy();
      }
      tmp591.Time_secs = this.Time_secs;
      tmp591.Uptime_secs = this.Uptime_secs;
      return tmp591;
    }

    public async global::System.Threading.Tasks.Task ReadAsync(TProtocol iprot, CancellationToken cancellationToken)
    {
      iprot.IncrementRecursionDepth();
      try
      {
        bool isset_storm_id = false;
        bool isset_executor_stats = false;
        bool isset_time_secs = false;
        bool isset_uptime_secs = false;
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
                Storm_id = await iprot.ReadStringAsync(cancellationToken);
                isset_storm_id = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 2:
              if (field.Type == TType.Map)
              {
                {
                  TMap _map592 = await iprot.ReadMapBeginAsync(cancellationToken);
                  Executor_stats = new Dictionary<global::StormThrift.ExecutorInfo, global::StormThrift.ExecutorStats>(_map592.Count);
                  for(int _i593 = 0; _i593 < _map592.Count; ++_i593)
                  {
                    global::StormThrift.ExecutorInfo _key594;
                    global::StormThrift.ExecutorStats _val595;
                    _key594 = new global::StormThrift.ExecutorInfo();
                    await _key594.ReadAsync(iprot, cancellationToken);
                    _val595 = new global::StormThrift.ExecutorStats();
                    await _val595.ReadAsync(iprot, cancellationToken);
                    Executor_stats[_key594] = _val595;
                  }
                  await iprot.ReadMapEndAsync(cancellationToken);
                }
                isset_executor_stats = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 3:
              if (field.Type == TType.I32)
              {
                Time_secs = await iprot.ReadI32Async(cancellationToken);
                isset_time_secs = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 4:
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
            default: 
              await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              break;
          }

          await iprot.ReadFieldEndAsync(cancellationToken);
        }

        await iprot.ReadStructEndAsync(cancellationToken);
        if (!isset_storm_id)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_executor_stats)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_time_secs)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_uptime_secs)
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
        var tmp596 = new TStruct("ClusterWorkerHeartbeat");
        await oprot.WriteStructBeginAsync(tmp596, cancellationToken);
        var tmp597 = new TField();
        if((Storm_id != null))
        {
          tmp597.Name = "storm_id";
          tmp597.Type = TType.String;
          tmp597.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp597, cancellationToken);
          await oprot.WriteStringAsync(Storm_id, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Executor_stats != null))
        {
          tmp597.Name = "executor_stats";
          tmp597.Type = TType.Map;
          tmp597.ID = 2;
          await oprot.WriteFieldBeginAsync(tmp597, cancellationToken);
          {
            await oprot.WriteMapBeginAsync(new TMap(TType.Struct, TType.Struct, Executor_stats.Count), cancellationToken);
            foreach (global::StormThrift.ExecutorInfo _iter598 in Executor_stats.Keys)
            {
              await _iter598.WriteAsync(oprot, cancellationToken);
              await Executor_stats[_iter598].WriteAsync(oprot, cancellationToken);
            }
            await oprot.WriteMapEndAsync(cancellationToken);
          }
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        tmp597.Name = "time_secs";
        tmp597.Type = TType.I32;
        tmp597.ID = 3;
        await oprot.WriteFieldBeginAsync(tmp597, cancellationToken);
        await oprot.WriteI32Async(Time_secs, cancellationToken);
        await oprot.WriteFieldEndAsync(cancellationToken);
        tmp597.Name = "uptime_secs";
        tmp597.Type = TType.I32;
        tmp597.ID = 4;
        await oprot.WriteFieldBeginAsync(tmp597, cancellationToken);
        await oprot.WriteI32Async(Uptime_secs, cancellationToken);
        await oprot.WriteFieldEndAsync(cancellationToken);
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
      if (!(that is ClusterWorkerHeartbeat other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return global::System.Object.Equals(Storm_id, other.Storm_id)
        && TCollections.Equals(Executor_stats, other.Executor_stats)
        && global::System.Object.Equals(Time_secs, other.Time_secs)
        && global::System.Object.Equals(Uptime_secs, other.Uptime_secs);
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if((Storm_id != null))
        {
          hashcode = (hashcode * 397) + Storm_id.GetHashCode();
        }
        if((Executor_stats != null))
        {
          hashcode = (hashcode * 397) + TCollections.GetHashCode(Executor_stats);
        }
        hashcode = (hashcode * 397) + Time_secs.GetHashCode();
        hashcode = (hashcode * 397) + Uptime_secs.GetHashCode();
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp599 = new StringBuilder("ClusterWorkerHeartbeat(");
      if((Storm_id != null))
      {
        tmp599.Append(", Storm_id: ");
        Storm_id.ToString(tmp599);
      }
      if((Executor_stats != null))
      {
        tmp599.Append(", Executor_stats: ");
        Executor_stats.ToString(tmp599);
      }
      tmp599.Append(", Time_secs: ");
      Time_secs.ToString(tmp599);
      tmp599.Append(", Uptime_secs: ");
      Uptime_secs.ToString(tmp599);
      tmp599.Append(')');
      return tmp599.ToString();
    }
  }

}
