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

  public partial class LSSupervisorId : TBase
  {

    public string Supervisor_id { get; set; }

    public LSSupervisorId()
    {
    }

    public LSSupervisorId(string supervisor_id) : this()
    {
      this.Supervisor_id = supervisor_id;
    }

    public LSSupervisorId DeepCopy()
    {
      var tmp625 = new LSSupervisorId();
      if((Supervisor_id != null))
      {
        tmp625.Supervisor_id = this.Supervisor_id;
      }
      return tmp625;
    }

    public async global::System.Threading.Tasks.Task ReadAsync(TProtocol iprot, CancellationToken cancellationToken)
    {
      iprot.IncrementRecursionDepth();
      try
      {
        bool isset_supervisor_id = false;
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
                Supervisor_id = await iprot.ReadStringAsync(cancellationToken);
                isset_supervisor_id = true;
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
        if (!isset_supervisor_id)
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
        var tmp626 = new TStruct("LSSupervisorId");
        await oprot.WriteStructBeginAsync(tmp626, cancellationToken);
        var tmp627 = new TField();
        if((Supervisor_id != null))
        {
          tmp627.Name = "supervisor_id";
          tmp627.Type = TType.String;
          tmp627.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp627, cancellationToken);
          await oprot.WriteStringAsync(Supervisor_id, cancellationToken);
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
      if (!(that is LSSupervisorId other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return global::System.Object.Equals(Supervisor_id, other.Supervisor_id);
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if((Supervisor_id != null))
        {
          hashcode = (hashcode * 397) + Supervisor_id.GetHashCode();
        }
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp628 = new StringBuilder("LSSupervisorId(");
      if((Supervisor_id != null))
      {
        tmp628.Append(", Supervisor_id: ");
        Supervisor_id.ToString(tmp628);
      }
      tmp628.Append(')');
      return tmp628.ToString();
    }
  }

}
