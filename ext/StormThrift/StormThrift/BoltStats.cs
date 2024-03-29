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

  public partial class BoltStats : TBase
  {

    public Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>> Acked { get; set; }

    public Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>> Failed { get; set; }

    public Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, double>> Process_ms_avg { get; set; }

    public Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>> Executed { get; set; }

    public Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, double>> Execute_ms_avg { get; set; }

    public BoltStats()
    {
    }

    public BoltStats(Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>> acked, Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>> failed, Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, double>> process_ms_avg, Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>> executed, Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, double>> execute_ms_avg) : this()
    {
      this.Acked = acked;
      this.Failed = failed;
      this.Process_ms_avg = process_ms_avg;
      this.Executed = executed;
      this.Execute_ms_avg = execute_ms_avg;
    }

    public BoltStats DeepCopy()
    {
      var tmp177 = new BoltStats();
      if((Acked != null))
      {
        tmp177.Acked = this.Acked.DeepCopy();
      }
      if((Failed != null))
      {
        tmp177.Failed = this.Failed.DeepCopy();
      }
      if((Process_ms_avg != null))
      {
        tmp177.Process_ms_avg = this.Process_ms_avg.DeepCopy();
      }
      if((Executed != null))
      {
        tmp177.Executed = this.Executed.DeepCopy();
      }
      if((Execute_ms_avg != null))
      {
        tmp177.Execute_ms_avg = this.Execute_ms_avg.DeepCopy();
      }
      return tmp177;
    }

    public async global::System.Threading.Tasks.Task ReadAsync(TProtocol iprot, CancellationToken cancellationToken)
    {
      iprot.IncrementRecursionDepth();
      try
      {
        bool isset_acked = false;
        bool isset_failed = false;
        bool isset_process_ms_avg = false;
        bool isset_executed = false;
        bool isset_execute_ms_avg = false;
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
              if (field.Type == TType.Map)
              {
                {
                  TMap _map178 = await iprot.ReadMapBeginAsync(cancellationToken);
                  Acked = new Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>>(_map178.Count);
                  for(int _i179 = 0; _i179 < _map178.Count; ++_i179)
                  {
                    string _key180;
                    Dictionary<global::StormThrift.GlobalStreamId, long> _val181;
                    _key180 = await iprot.ReadStringAsync(cancellationToken);
                    {
                      TMap _map182 = await iprot.ReadMapBeginAsync(cancellationToken);
                      _val181 = new Dictionary<global::StormThrift.GlobalStreamId, long>(_map182.Count);
                      for(int _i183 = 0; _i183 < _map182.Count; ++_i183)
                      {
                        global::StormThrift.GlobalStreamId _key184;
                        long _val185;
                        _key184 = new global::StormThrift.GlobalStreamId();
                        await _key184.ReadAsync(iprot, cancellationToken);
                        _val185 = await iprot.ReadI64Async(cancellationToken);
                        _val181[_key184] = _val185;
                      }
                      await iprot.ReadMapEndAsync(cancellationToken);
                    }
                    Acked[_key180] = _val181;
                  }
                  await iprot.ReadMapEndAsync(cancellationToken);
                }
                isset_acked = true;
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
                  TMap _map186 = await iprot.ReadMapBeginAsync(cancellationToken);
                  Failed = new Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>>(_map186.Count);
                  for(int _i187 = 0; _i187 < _map186.Count; ++_i187)
                  {
                    string _key188;
                    Dictionary<global::StormThrift.GlobalStreamId, long> _val189;
                    _key188 = await iprot.ReadStringAsync(cancellationToken);
                    {
                      TMap _map190 = await iprot.ReadMapBeginAsync(cancellationToken);
                      _val189 = new Dictionary<global::StormThrift.GlobalStreamId, long>(_map190.Count);
                      for(int _i191 = 0; _i191 < _map190.Count; ++_i191)
                      {
                        global::StormThrift.GlobalStreamId _key192;
                        long _val193;
                        _key192 = new global::StormThrift.GlobalStreamId();
                        await _key192.ReadAsync(iprot, cancellationToken);
                        _val193 = await iprot.ReadI64Async(cancellationToken);
                        _val189[_key192] = _val193;
                      }
                      await iprot.ReadMapEndAsync(cancellationToken);
                    }
                    Failed[_key188] = _val189;
                  }
                  await iprot.ReadMapEndAsync(cancellationToken);
                }
                isset_failed = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 3:
              if (field.Type == TType.Map)
              {
                {
                  TMap _map194 = await iprot.ReadMapBeginAsync(cancellationToken);
                  Process_ms_avg = new Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, double>>(_map194.Count);
                  for(int _i195 = 0; _i195 < _map194.Count; ++_i195)
                  {
                    string _key196;
                    Dictionary<global::StormThrift.GlobalStreamId, double> _val197;
                    _key196 = await iprot.ReadStringAsync(cancellationToken);
                    {
                      TMap _map198 = await iprot.ReadMapBeginAsync(cancellationToken);
                      _val197 = new Dictionary<global::StormThrift.GlobalStreamId, double>(_map198.Count);
                      for(int _i199 = 0; _i199 < _map198.Count; ++_i199)
                      {
                        global::StormThrift.GlobalStreamId _key200;
                        double _val201;
                        _key200 = new global::StormThrift.GlobalStreamId();
                        await _key200.ReadAsync(iprot, cancellationToken);
                        _val201 = await iprot.ReadDoubleAsync(cancellationToken);
                        _val197[_key200] = _val201;
                      }
                      await iprot.ReadMapEndAsync(cancellationToken);
                    }
                    Process_ms_avg[_key196] = _val197;
                  }
                  await iprot.ReadMapEndAsync(cancellationToken);
                }
                isset_process_ms_avg = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 4:
              if (field.Type == TType.Map)
              {
                {
                  TMap _map202 = await iprot.ReadMapBeginAsync(cancellationToken);
                  Executed = new Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>>(_map202.Count);
                  for(int _i203 = 0; _i203 < _map202.Count; ++_i203)
                  {
                    string _key204;
                    Dictionary<global::StormThrift.GlobalStreamId, long> _val205;
                    _key204 = await iprot.ReadStringAsync(cancellationToken);
                    {
                      TMap _map206 = await iprot.ReadMapBeginAsync(cancellationToken);
                      _val205 = new Dictionary<global::StormThrift.GlobalStreamId, long>(_map206.Count);
                      for(int _i207 = 0; _i207 < _map206.Count; ++_i207)
                      {
                        global::StormThrift.GlobalStreamId _key208;
                        long _val209;
                        _key208 = new global::StormThrift.GlobalStreamId();
                        await _key208.ReadAsync(iprot, cancellationToken);
                        _val209 = await iprot.ReadI64Async(cancellationToken);
                        _val205[_key208] = _val209;
                      }
                      await iprot.ReadMapEndAsync(cancellationToken);
                    }
                    Executed[_key204] = _val205;
                  }
                  await iprot.ReadMapEndAsync(cancellationToken);
                }
                isset_executed = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 5:
              if (field.Type == TType.Map)
              {
                {
                  TMap _map210 = await iprot.ReadMapBeginAsync(cancellationToken);
                  Execute_ms_avg = new Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, double>>(_map210.Count);
                  for(int _i211 = 0; _i211 < _map210.Count; ++_i211)
                  {
                    string _key212;
                    Dictionary<global::StormThrift.GlobalStreamId, double> _val213;
                    _key212 = await iprot.ReadStringAsync(cancellationToken);
                    {
                      TMap _map214 = await iprot.ReadMapBeginAsync(cancellationToken);
                      _val213 = new Dictionary<global::StormThrift.GlobalStreamId, double>(_map214.Count);
                      for(int _i215 = 0; _i215 < _map214.Count; ++_i215)
                      {
                        global::StormThrift.GlobalStreamId _key216;
                        double _val217;
                        _key216 = new global::StormThrift.GlobalStreamId();
                        await _key216.ReadAsync(iprot, cancellationToken);
                        _val217 = await iprot.ReadDoubleAsync(cancellationToken);
                        _val213[_key216] = _val217;
                      }
                      await iprot.ReadMapEndAsync(cancellationToken);
                    }
                    Execute_ms_avg[_key212] = _val213;
                  }
                  await iprot.ReadMapEndAsync(cancellationToken);
                }
                isset_execute_ms_avg = true;
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
        if (!isset_acked)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_failed)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_process_ms_avg)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_executed)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_execute_ms_avg)
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
        var tmp218 = new TStruct("BoltStats");
        await oprot.WriteStructBeginAsync(tmp218, cancellationToken);
        var tmp219 = new TField();
        if((Acked != null))
        {
          tmp219.Name = "acked";
          tmp219.Type = TType.Map;
          tmp219.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp219, cancellationToken);
          {
            await oprot.WriteMapBeginAsync(new TMap(TType.String, TType.Map, Acked.Count), cancellationToken);
            foreach (string _iter220 in Acked.Keys)
            {
              await oprot.WriteStringAsync(_iter220, cancellationToken);
              {
                await oprot.WriteMapBeginAsync(new TMap(TType.Struct, TType.I64, Acked[_iter220].Count), cancellationToken);
                foreach (global::StormThrift.GlobalStreamId _iter221 in Acked[_iter220].Keys)
                {
                  await _iter221.WriteAsync(oprot, cancellationToken);
                  await oprot.WriteI64Async(Acked[_iter220][_iter221], cancellationToken);
                }
                await oprot.WriteMapEndAsync(cancellationToken);
              }
            }
            await oprot.WriteMapEndAsync(cancellationToken);
          }
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Failed != null))
        {
          tmp219.Name = "failed";
          tmp219.Type = TType.Map;
          tmp219.ID = 2;
          await oprot.WriteFieldBeginAsync(tmp219, cancellationToken);
          {
            await oprot.WriteMapBeginAsync(new TMap(TType.String, TType.Map, Failed.Count), cancellationToken);
            foreach (string _iter222 in Failed.Keys)
            {
              await oprot.WriteStringAsync(_iter222, cancellationToken);
              {
                await oprot.WriteMapBeginAsync(new TMap(TType.Struct, TType.I64, Failed[_iter222].Count), cancellationToken);
                foreach (global::StormThrift.GlobalStreamId _iter223 in Failed[_iter222].Keys)
                {
                  await _iter223.WriteAsync(oprot, cancellationToken);
                  await oprot.WriteI64Async(Failed[_iter222][_iter223], cancellationToken);
                }
                await oprot.WriteMapEndAsync(cancellationToken);
              }
            }
            await oprot.WriteMapEndAsync(cancellationToken);
          }
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Process_ms_avg != null))
        {
          tmp219.Name = "process_ms_avg";
          tmp219.Type = TType.Map;
          tmp219.ID = 3;
          await oprot.WriteFieldBeginAsync(tmp219, cancellationToken);
          {
            await oprot.WriteMapBeginAsync(new TMap(TType.String, TType.Map, Process_ms_avg.Count), cancellationToken);
            foreach (string _iter224 in Process_ms_avg.Keys)
            {
              await oprot.WriteStringAsync(_iter224, cancellationToken);
              {
                await oprot.WriteMapBeginAsync(new TMap(TType.Struct, TType.Double, Process_ms_avg[_iter224].Count), cancellationToken);
                foreach (global::StormThrift.GlobalStreamId _iter225 in Process_ms_avg[_iter224].Keys)
                {
                  await _iter225.WriteAsync(oprot, cancellationToken);
                  await oprot.WriteDoubleAsync(Process_ms_avg[_iter224][_iter225], cancellationToken);
                }
                await oprot.WriteMapEndAsync(cancellationToken);
              }
            }
            await oprot.WriteMapEndAsync(cancellationToken);
          }
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Executed != null))
        {
          tmp219.Name = "executed";
          tmp219.Type = TType.Map;
          tmp219.ID = 4;
          await oprot.WriteFieldBeginAsync(tmp219, cancellationToken);
          {
            await oprot.WriteMapBeginAsync(new TMap(TType.String, TType.Map, Executed.Count), cancellationToken);
            foreach (string _iter226 in Executed.Keys)
            {
              await oprot.WriteStringAsync(_iter226, cancellationToken);
              {
                await oprot.WriteMapBeginAsync(new TMap(TType.Struct, TType.I64, Executed[_iter226].Count), cancellationToken);
                foreach (global::StormThrift.GlobalStreamId _iter227 in Executed[_iter226].Keys)
                {
                  await _iter227.WriteAsync(oprot, cancellationToken);
                  await oprot.WriteI64Async(Executed[_iter226][_iter227], cancellationToken);
                }
                await oprot.WriteMapEndAsync(cancellationToken);
              }
            }
            await oprot.WriteMapEndAsync(cancellationToken);
          }
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Execute_ms_avg != null))
        {
          tmp219.Name = "execute_ms_avg";
          tmp219.Type = TType.Map;
          tmp219.ID = 5;
          await oprot.WriteFieldBeginAsync(tmp219, cancellationToken);
          {
            await oprot.WriteMapBeginAsync(new TMap(TType.String, TType.Map, Execute_ms_avg.Count), cancellationToken);
            foreach (string _iter228 in Execute_ms_avg.Keys)
            {
              await oprot.WriteStringAsync(_iter228, cancellationToken);
              {
                await oprot.WriteMapBeginAsync(new TMap(TType.Struct, TType.Double, Execute_ms_avg[_iter228].Count), cancellationToken);
                foreach (global::StormThrift.GlobalStreamId _iter229 in Execute_ms_avg[_iter228].Keys)
                {
                  await _iter229.WriteAsync(oprot, cancellationToken);
                  await oprot.WriteDoubleAsync(Execute_ms_avg[_iter228][_iter229], cancellationToken);
                }
                await oprot.WriteMapEndAsync(cancellationToken);
              }
            }
            await oprot.WriteMapEndAsync(cancellationToken);
          }
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
      if (!(that is BoltStats other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return TCollections.Equals(Acked, other.Acked)
        && TCollections.Equals(Failed, other.Failed)
        && TCollections.Equals(Process_ms_avg, other.Process_ms_avg)
        && TCollections.Equals(Executed, other.Executed)
        && TCollections.Equals(Execute_ms_avg, other.Execute_ms_avg);
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if((Acked != null))
        {
          hashcode = (hashcode * 397) + TCollections.GetHashCode(Acked);
        }
        if((Failed != null))
        {
          hashcode = (hashcode * 397) + TCollections.GetHashCode(Failed);
        }
        if((Process_ms_avg != null))
        {
          hashcode = (hashcode * 397) + TCollections.GetHashCode(Process_ms_avg);
        }
        if((Executed != null))
        {
          hashcode = (hashcode * 397) + TCollections.GetHashCode(Executed);
        }
        if((Execute_ms_avg != null))
        {
          hashcode = (hashcode * 397) + TCollections.GetHashCode(Execute_ms_avg);
        }
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp230 = new StringBuilder("BoltStats(");
      if((Acked != null))
      {
        tmp230.Append(", Acked: ");
        Acked.ToString(tmp230);
      }
      if((Failed != null))
      {
        tmp230.Append(", Failed: ");
        Failed.ToString(tmp230);
      }
      if((Process_ms_avg != null))
      {
        tmp230.Append(", Process_ms_avg: ");
        Process_ms_avg.ToString(tmp230);
      }
      if((Executed != null))
      {
        tmp230.Append(", Executed: ");
        Executed.ToString(tmp230);
      }
      if((Execute_ms_avg != null))
      {
        tmp230.Append(", Execute_ms_avg: ");
        Execute_ms_avg.ToString(tmp230);
      }
      tmp230.Append(')');
      return tmp230.ToString();
    }
  }

}
