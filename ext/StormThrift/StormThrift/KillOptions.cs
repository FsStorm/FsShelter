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

  public partial class KillOptions : TBase
  {
    private int _wait_secs;

    public int Wait_secs
    {
      get
      {
        return _wait_secs;
      }
      set
      {
        __isset.wait_secs = true;
        this._wait_secs = value;
      }
    }


    public Isset __isset;
    public struct Isset
    {
      public bool wait_secs;
    }

    public KillOptions()
    {
    }

    public KillOptions DeepCopy()
    {
      var tmp438 = new KillOptions();
      if(__isset.wait_secs)
      {
        tmp438.Wait_secs = this.Wait_secs;
      }
      tmp438.__isset.wait_secs = this.__isset.wait_secs;
      return tmp438;
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
                Wait_secs = await iprot.ReadI32Async(cancellationToken);
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
        var tmp439 = new TStruct("KillOptions");
        await oprot.WriteStructBeginAsync(tmp439, cancellationToken);
        var tmp440 = new TField();
        if(__isset.wait_secs)
        {
          tmp440.Name = "wait_secs";
          tmp440.Type = TType.I32;
          tmp440.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp440, cancellationToken);
          await oprot.WriteI32Async(Wait_secs, cancellationToken);
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
      if (!(that is KillOptions other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return ((__isset.wait_secs == other.__isset.wait_secs) && ((!__isset.wait_secs) || (global::System.Object.Equals(Wait_secs, other.Wait_secs))));
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if(__isset.wait_secs)
        {
          hashcode = (hashcode * 397) + Wait_secs.GetHashCode();
        }
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp441 = new StringBuilder("KillOptions(");
      int tmp442 = 0;
      if(__isset.wait_secs)
      {
        if(0 < tmp442++) { tmp441.Append(", "); }
        tmp441.Append("Wait_secs: ");
        Wait_secs.ToString(tmp441);
      }
      tmp441.Append(')');
      return tmp441.ToString();
    }
  }

}
