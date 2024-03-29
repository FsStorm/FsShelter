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

  public partial class LSTopoHistoryList : TBase
  {

    public List<global::StormThrift.LSTopoHistory> Topo_history { get; set; }

    public LSTopoHistoryList()
    {
    }

    public LSTopoHistoryList(List<global::StormThrift.LSTopoHistory> topo_history) : this()
    {
      this.Topo_history = topo_history;
    }

    public LSTopoHistoryList DeepCopy()
    {
      var tmp672 = new LSTopoHistoryList();
      if((Topo_history != null))
      {
        tmp672.Topo_history = this.Topo_history.DeepCopy();
      }
      return tmp672;
    }

    public async global::System.Threading.Tasks.Task ReadAsync(TProtocol iprot, CancellationToken cancellationToken)
    {
      iprot.IncrementRecursionDepth();
      try
      {
        bool isset_topo_history = false;
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
              if (field.Type == TType.List)
              {
                {
                  TList _list673 = await iprot.ReadListBeginAsync(cancellationToken);
                  Topo_history = new List<global::StormThrift.LSTopoHistory>(_list673.Count);
                  for(int _i674 = 0; _i674 < _list673.Count; ++_i674)
                  {
                    global::StormThrift.LSTopoHistory _elem675;
                    _elem675 = new global::StormThrift.LSTopoHistory();
                    await _elem675.ReadAsync(iprot, cancellationToken);
                    Topo_history.Add(_elem675);
                  }
                  await iprot.ReadListEndAsync(cancellationToken);
                }
                isset_topo_history = true;
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
        if (!isset_topo_history)
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
        var tmp676 = new TStruct("LSTopoHistoryList");
        await oprot.WriteStructBeginAsync(tmp676, cancellationToken);
        var tmp677 = new TField();
        if((Topo_history != null))
        {
          tmp677.Name = "topo_history";
          tmp677.Type = TType.List;
          tmp677.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp677, cancellationToken);
          {
            await oprot.WriteListBeginAsync(new TList(TType.Struct, Topo_history.Count), cancellationToken);
            foreach (global::StormThrift.LSTopoHistory _iter678 in Topo_history)
            {
              await _iter678.WriteAsync(oprot, cancellationToken);
            }
            await oprot.WriteListEndAsync(cancellationToken);
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
      if (!(that is LSTopoHistoryList other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return TCollections.Equals(Topo_history, other.Topo_history);
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if((Topo_history != null))
        {
          hashcode = (hashcode * 397) + TCollections.GetHashCode(Topo_history);
        }
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp679 = new StringBuilder("LSTopoHistoryList(");
      if((Topo_history != null))
      {
        tmp679.Append(", Topo_history: ");
        Topo_history.ToString(tmp679);
      }
      tmp679.Append(')');
      return tmp679.ToString();
    }
  }

}
