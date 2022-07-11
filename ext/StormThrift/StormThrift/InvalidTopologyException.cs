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

  public partial class InvalidTopologyException : TException, TBase
  {

    public string Msg { get; set; }

    public InvalidTopologyException()
    {
    }

    public InvalidTopologyException(string msg) : this()
    {
      this.Msg = msg;
    }

    public InvalidTopologyException DeepCopy()
    {
      var tmp120 = new InvalidTopologyException();
      if((Msg != null))
      {
        tmp120.Msg = this.Msg;
      }
      return tmp120;
    }

    public async global::System.Threading.Tasks.Task ReadAsync(TProtocol iprot, CancellationToken cancellationToken)
    {
      iprot.IncrementRecursionDepth();
      try
      {
        bool isset_msg = false;
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
                Msg = await iprot.ReadStringAsync(cancellationToken);
                isset_msg = true;
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
        if (!isset_msg)
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
        var tmp121 = new TStruct("InvalidTopologyException");
        await oprot.WriteStructBeginAsync(tmp121, cancellationToken);
        var tmp122 = new TField();
        if((Msg != null))
        {
          tmp122.Name = "msg";
          tmp122.Type = TType.String;
          tmp122.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp122, cancellationToken);
          await oprot.WriteStringAsync(Msg, cancellationToken);
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
      if (!(that is InvalidTopologyException other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return global::System.Object.Equals(Msg, other.Msg);
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if((Msg != null))
        {
          hashcode = (hashcode * 397) + Msg.GetHashCode();
        }
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp123 = new StringBuilder("InvalidTopologyException(");
      if((Msg != null))
      {
        tmp123.Append(", Msg: ");
        Msg.ToString(tmp123);
      }
      tmp123.Append(')');
      return tmp123.ToString();
    }
  }

}
