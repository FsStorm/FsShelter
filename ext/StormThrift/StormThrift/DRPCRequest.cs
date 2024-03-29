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

  public partial class DRPCRequest : TBase
  {

    public string Func_args { get; set; }

    public string Request_id { get; set; }

    public DRPCRequest()
    {
    }

    public DRPCRequest(string func_args, string request_id) : this()
    {
      this.Func_args = func_args;
      this.Request_id = request_id;
    }

    public DRPCRequest DeepCopy()
    {
      var tmp715 = new DRPCRequest();
      if((Func_args != null))
      {
        tmp715.Func_args = this.Func_args;
      }
      if((Request_id != null))
      {
        tmp715.Request_id = this.Request_id;
      }
      return tmp715;
    }

    public async global::System.Threading.Tasks.Task ReadAsync(TProtocol iprot, CancellationToken cancellationToken)
    {
      iprot.IncrementRecursionDepth();
      try
      {
        bool isset_func_args = false;
        bool isset_request_id = false;
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
                Func_args = await iprot.ReadStringAsync(cancellationToken);
                isset_func_args = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 2:
              if (field.Type == TType.String)
              {
                Request_id = await iprot.ReadStringAsync(cancellationToken);
                isset_request_id = true;
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
        if (!isset_func_args)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_request_id)
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
        var tmp716 = new TStruct("DRPCRequest");
        await oprot.WriteStructBeginAsync(tmp716, cancellationToken);
        var tmp717 = new TField();
        if((Func_args != null))
        {
          tmp717.Name = "func_args";
          tmp717.Type = TType.String;
          tmp717.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp717, cancellationToken);
          await oprot.WriteStringAsync(Func_args, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Request_id != null))
        {
          tmp717.Name = "request_id";
          tmp717.Type = TType.String;
          tmp717.ID = 2;
          await oprot.WriteFieldBeginAsync(tmp717, cancellationToken);
          await oprot.WriteStringAsync(Request_id, cancellationToken);
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
      if (!(that is DRPCRequest other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return global::System.Object.Equals(Func_args, other.Func_args)
        && global::System.Object.Equals(Request_id, other.Request_id);
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if((Func_args != null))
        {
          hashcode = (hashcode * 397) + Func_args.GetHashCode();
        }
        if((Request_id != null))
        {
          hashcode = (hashcode * 397) + Request_id.GetHashCode();
        }
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp718 = new StringBuilder("DRPCRequest(");
      if((Func_args != null))
      {
        tmp718.Append(", Func_args: ");
        Func_args.ToString(tmp718);
      }
      if((Request_id != null))
      {
        tmp718.Append(", Request_id: ");
        Request_id.ToString(tmp718);
      }
      tmp718.Append(')');
      return tmp718.ToString();
    }
  }

}
