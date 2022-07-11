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

  public partial class ExecutorStats : TBase
  {

    public Dictionary<string, Dictionary<string, long>> Emitted { get; set; }

    public Dictionary<string, Dictionary<string, long>> Transferred { get; set; }

    public global::StormThrift.ExecutorSpecificStats Specific { get; set; }

    public double Rate { get; set; }

    public ExecutorStats()
    {
    }

    public ExecutorStats(Dictionary<string, Dictionary<string, long>> emitted, Dictionary<string, Dictionary<string, long>> transferred, global::StormThrift.ExecutorSpecificStats specific, double rate) : this()
    {
      this.Emitted = emitted;
      this.Transferred = transferred;
      this.Specific = specific;
      this.Rate = rate;
    }

    public ExecutorStats DeepCopy()
    {
      var tmp272 = new ExecutorStats();
      if((Emitted != null))
      {
        tmp272.Emitted = this.Emitted.DeepCopy();
      }
      if((Transferred != null))
      {
        tmp272.Transferred = this.Transferred.DeepCopy();
      }
      if((Specific != null))
      {
        tmp272.Specific = (global::StormThrift.ExecutorSpecificStats)this.Specific.DeepCopy();
      }
      tmp272.Rate = this.Rate;
      return tmp272;
    }

    public async global::System.Threading.Tasks.Task ReadAsync(TProtocol iprot, CancellationToken cancellationToken)
    {
      iprot.IncrementRecursionDepth();
      try
      {
        bool isset_emitted = false;
        bool isset_transferred = false;
        bool isset_specific = false;
        bool isset_rate = false;
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
                  TMap _map273 = await iprot.ReadMapBeginAsync(cancellationToken);
                  Emitted = new Dictionary<string, Dictionary<string, long>>(_map273.Count);
                  for(int _i274 = 0; _i274 < _map273.Count; ++_i274)
                  {
                    string _key275;
                    Dictionary<string, long> _val276;
                    _key275 = await iprot.ReadStringAsync(cancellationToken);
                    {
                      TMap _map277 = await iprot.ReadMapBeginAsync(cancellationToken);
                      _val276 = new Dictionary<string, long>(_map277.Count);
                      for(int _i278 = 0; _i278 < _map277.Count; ++_i278)
                      {
                        string _key279;
                        long _val280;
                        _key279 = await iprot.ReadStringAsync(cancellationToken);
                        _val280 = await iprot.ReadI64Async(cancellationToken);
                        _val276[_key279] = _val280;
                      }
                      await iprot.ReadMapEndAsync(cancellationToken);
                    }
                    Emitted[_key275] = _val276;
                  }
                  await iprot.ReadMapEndAsync(cancellationToken);
                }
                isset_emitted = true;
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
                  TMap _map281 = await iprot.ReadMapBeginAsync(cancellationToken);
                  Transferred = new Dictionary<string, Dictionary<string, long>>(_map281.Count);
                  for(int _i282 = 0; _i282 < _map281.Count; ++_i282)
                  {
                    string _key283;
                    Dictionary<string, long> _val284;
                    _key283 = await iprot.ReadStringAsync(cancellationToken);
                    {
                      TMap _map285 = await iprot.ReadMapBeginAsync(cancellationToken);
                      _val284 = new Dictionary<string, long>(_map285.Count);
                      for(int _i286 = 0; _i286 < _map285.Count; ++_i286)
                      {
                        string _key287;
                        long _val288;
                        _key287 = await iprot.ReadStringAsync(cancellationToken);
                        _val288 = await iprot.ReadI64Async(cancellationToken);
                        _val284[_key287] = _val288;
                      }
                      await iprot.ReadMapEndAsync(cancellationToken);
                    }
                    Transferred[_key283] = _val284;
                  }
                  await iprot.ReadMapEndAsync(cancellationToken);
                }
                isset_transferred = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 3:
              if (field.Type == TType.Struct)
              {
                Specific = new global::StormThrift.ExecutorSpecificStats();
                await Specific.ReadAsync(iprot, cancellationToken);
                isset_specific = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 4:
              if (field.Type == TType.Double)
              {
                Rate = await iprot.ReadDoubleAsync(cancellationToken);
                isset_rate = true;
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
        if (!isset_emitted)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_transferred)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_specific)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_rate)
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
        var tmp289 = new TStruct("ExecutorStats");
        await oprot.WriteStructBeginAsync(tmp289, cancellationToken);
        var tmp290 = new TField();
        if((Emitted != null))
        {
          tmp290.Name = "emitted";
          tmp290.Type = TType.Map;
          tmp290.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp290, cancellationToken);
          {
            await oprot.WriteMapBeginAsync(new TMap(TType.String, TType.Map, Emitted.Count), cancellationToken);
            foreach (string _iter291 in Emitted.Keys)
            {
              await oprot.WriteStringAsync(_iter291, cancellationToken);
              {
                await oprot.WriteMapBeginAsync(new TMap(TType.String, TType.I64, Emitted[_iter291].Count), cancellationToken);
                foreach (string _iter292 in Emitted[_iter291].Keys)
                {
                  await oprot.WriteStringAsync(_iter292, cancellationToken);
                  await oprot.WriteI64Async(Emitted[_iter291][_iter292], cancellationToken);
                }
                await oprot.WriteMapEndAsync(cancellationToken);
              }
            }
            await oprot.WriteMapEndAsync(cancellationToken);
          }
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Transferred != null))
        {
          tmp290.Name = "transferred";
          tmp290.Type = TType.Map;
          tmp290.ID = 2;
          await oprot.WriteFieldBeginAsync(tmp290, cancellationToken);
          {
            await oprot.WriteMapBeginAsync(new TMap(TType.String, TType.Map, Transferred.Count), cancellationToken);
            foreach (string _iter293 in Transferred.Keys)
            {
              await oprot.WriteStringAsync(_iter293, cancellationToken);
              {
                await oprot.WriteMapBeginAsync(new TMap(TType.String, TType.I64, Transferred[_iter293].Count), cancellationToken);
                foreach (string _iter294 in Transferred[_iter293].Keys)
                {
                  await oprot.WriteStringAsync(_iter294, cancellationToken);
                  await oprot.WriteI64Async(Transferred[_iter293][_iter294], cancellationToken);
                }
                await oprot.WriteMapEndAsync(cancellationToken);
              }
            }
            await oprot.WriteMapEndAsync(cancellationToken);
          }
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Specific != null))
        {
          tmp290.Name = "specific";
          tmp290.Type = TType.Struct;
          tmp290.ID = 3;
          await oprot.WriteFieldBeginAsync(tmp290, cancellationToken);
          await Specific.WriteAsync(oprot, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        tmp290.Name = "rate";
        tmp290.Type = TType.Double;
        tmp290.ID = 4;
        await oprot.WriteFieldBeginAsync(tmp290, cancellationToken);
        await oprot.WriteDoubleAsync(Rate, cancellationToken);
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
      if (!(that is ExecutorStats other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return TCollections.Equals(Emitted, other.Emitted)
        && TCollections.Equals(Transferred, other.Transferred)
        && global::System.Object.Equals(Specific, other.Specific)
        && global::System.Object.Equals(Rate, other.Rate);
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if((Emitted != null))
        {
          hashcode = (hashcode * 397) + TCollections.GetHashCode(Emitted);
        }
        if((Transferred != null))
        {
          hashcode = (hashcode * 397) + TCollections.GetHashCode(Transferred);
        }
        if((Specific != null))
        {
          hashcode = (hashcode * 397) + Specific.GetHashCode();
        }
        hashcode = (hashcode * 397) + Rate.GetHashCode();
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp295 = new StringBuilder("ExecutorStats(");
      if((Emitted != null))
      {
        tmp295.Append(", Emitted: ");
        Emitted.ToString(tmp295);
      }
      if((Transferred != null))
      {
        tmp295.Append(", Transferred: ");
        Transferred.ToString(tmp295);
      }
      if((Specific != null))
      {
        tmp295.Append(", Specific: ");
        Specific.ToString(tmp295);
      }
      tmp295.Append(", Rate: ");
      Rate.ToString(tmp295);
      tmp295.Append(')');
      return tmp295.ToString();
    }
  }

}
