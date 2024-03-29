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

  public partial class GlobalStreamId : TBase
  {

    public string ComponentId { get; set; }

    public string StreamId { get; set; }

    public GlobalStreamId()
    {
    }

    public GlobalStreamId(string componentId, string streamId) : this()
    {
      this.ComponentId = componentId;
      this.StreamId = streamId;
    }

    public GlobalStreamId DeepCopy()
    {
      var tmp18 = new GlobalStreamId();
      if((ComponentId != null))
      {
        tmp18.ComponentId = this.ComponentId;
      }
      if((StreamId != null))
      {
        tmp18.StreamId = this.StreamId;
      }
      return tmp18;
    }

    public async global::System.Threading.Tasks.Task ReadAsync(TProtocol iprot, CancellationToken cancellationToken)
    {
      iprot.IncrementRecursionDepth();
      try
      {
        bool isset_componentId = false;
        bool isset_streamId = false;
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
                ComponentId = await iprot.ReadStringAsync(cancellationToken);
                isset_componentId = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 2:
              if (field.Type == TType.String)
              {
                StreamId = await iprot.ReadStringAsync(cancellationToken);
                isset_streamId = true;
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
        if (!isset_componentId)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_streamId)
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
        var tmp19 = new TStruct("GlobalStreamId");
        await oprot.WriteStructBeginAsync(tmp19, cancellationToken);
        var tmp20 = new TField();
        if((ComponentId != null))
        {
          tmp20.Name = "componentId";
          tmp20.Type = TType.String;
          tmp20.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp20, cancellationToken);
          await oprot.WriteStringAsync(ComponentId, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((StreamId != null))
        {
          tmp20.Name = "streamId";
          tmp20.Type = TType.String;
          tmp20.ID = 2;
          await oprot.WriteFieldBeginAsync(tmp20, cancellationToken);
          await oprot.WriteStringAsync(StreamId, cancellationToken);
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
      if (!(that is GlobalStreamId other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return global::System.Object.Equals(ComponentId, other.ComponentId)
        && global::System.Object.Equals(StreamId, other.StreamId);
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if((ComponentId != null))
        {
          hashcode = (hashcode * 397) + ComponentId.GetHashCode();
        }
        if((StreamId != null))
        {
          hashcode = (hashcode * 397) + StreamId.GetHashCode();
        }
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp21 = new StringBuilder("GlobalStreamId(");
      if((ComponentId != null))
      {
        tmp21.Append(", ComponentId: ");
        ComponentId.ToString(tmp21);
      }
      if((StreamId != null))
      {
        tmp21.Append(", StreamId: ");
        StreamId.ToString(tmp21);
      }
      tmp21.Append(')');
      return tmp21.ToString();
    }
  }

}
