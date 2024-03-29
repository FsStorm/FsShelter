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

  public partial class HBMessageData : TBase
  {
    private string _path;
    private global::StormThrift.HBPulse _pulse;
    private bool _boolval;
    private global::StormThrift.HBRecords _records;
    private global::StormThrift.HBNodes _nodes;
    private byte[] _message_blob;

    public string Path
    {
      get
      {
        return _path;
      }
      set
      {
        __isset.path = true;
        this._path = value;
      }
    }

    public global::StormThrift.HBPulse Pulse
    {
      get
      {
        return _pulse;
      }
      set
      {
        __isset.pulse = true;
        this._pulse = value;
      }
    }

    public bool Boolval
    {
      get
      {
        return _boolval;
      }
      set
      {
        __isset.boolval = true;
        this._boolval = value;
      }
    }

    public global::StormThrift.HBRecords Records
    {
      get
      {
        return _records;
      }
      set
      {
        __isset.records = true;
        this._records = value;
      }
    }

    public global::StormThrift.HBNodes Nodes
    {
      get
      {
        return _nodes;
      }
      set
      {
        __isset.nodes = true;
        this._nodes = value;
      }
    }

    public byte[] Message_blob
    {
      get
      {
        return _message_blob;
      }
      set
      {
        __isset.message_blob = true;
        this._message_blob = value;
      }
    }


    public Isset __isset;
    public struct Isset
    {
      public bool path;
      public bool pulse;
      public bool boolval;
      public bool records;
      public bool nodes;
      public bool message_blob;
    }

    public HBMessageData()
    {
    }

    public HBMessageData DeepCopy()
    {
      var tmp748 = new HBMessageData();
      if((Path != null) && __isset.path)
      {
        tmp748.Path = this.Path;
      }
      tmp748.__isset.path = this.__isset.path;
      if((Pulse != null) && __isset.pulse)
      {
        tmp748.Pulse = (global::StormThrift.HBPulse)this.Pulse.DeepCopy();
      }
      tmp748.__isset.pulse = this.__isset.pulse;
      if(__isset.boolval)
      {
        tmp748.Boolval = this.Boolval;
      }
      tmp748.__isset.boolval = this.__isset.boolval;
      if((Records != null) && __isset.records)
      {
        tmp748.Records = (global::StormThrift.HBRecords)this.Records.DeepCopy();
      }
      tmp748.__isset.records = this.__isset.records;
      if((Nodes != null) && __isset.nodes)
      {
        tmp748.Nodes = (global::StormThrift.HBNodes)this.Nodes.DeepCopy();
      }
      tmp748.__isset.nodes = this.__isset.nodes;
      if((Message_blob != null) && __isset.message_blob)
      {
        tmp748.Message_blob = this.Message_blob.ToArray();
      }
      tmp748.__isset.message_blob = this.__isset.message_blob;
      return tmp748;
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
              if (field.Type == TType.String)
              {
                Path = await iprot.ReadStringAsync(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 2:
              if (field.Type == TType.Struct)
              {
                Pulse = new global::StormThrift.HBPulse();
                await Pulse.ReadAsync(iprot, cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 3:
              if (field.Type == TType.Bool)
              {
                Boolval = await iprot.ReadBoolAsync(cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 4:
              if (field.Type == TType.Struct)
              {
                Records = new global::StormThrift.HBRecords();
                await Records.ReadAsync(iprot, cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 5:
              if (field.Type == TType.Struct)
              {
                Nodes = new global::StormThrift.HBNodes();
                await Nodes.ReadAsync(iprot, cancellationToken);
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 7:
              if (field.Type == TType.String)
              {
                Message_blob = await iprot.ReadBinaryAsync(cancellationToken);
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
        var tmp749 = new TStruct("HBMessageData");
        await oprot.WriteStructBeginAsync(tmp749, cancellationToken);
        var tmp750 = new TField();
        if((Path != null) && __isset.path)
        {
          tmp750.Name = "path";
          tmp750.Type = TType.String;
          tmp750.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp750, cancellationToken);
          await oprot.WriteStringAsync(Path, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Pulse != null) && __isset.pulse)
        {
          tmp750.Name = "pulse";
          tmp750.Type = TType.Struct;
          tmp750.ID = 2;
          await oprot.WriteFieldBeginAsync(tmp750, cancellationToken);
          await Pulse.WriteAsync(oprot, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if(__isset.boolval)
        {
          tmp750.Name = "boolval";
          tmp750.Type = TType.Bool;
          tmp750.ID = 3;
          await oprot.WriteFieldBeginAsync(tmp750, cancellationToken);
          await oprot.WriteBoolAsync(Boolval, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Records != null) && __isset.records)
        {
          tmp750.Name = "records";
          tmp750.Type = TType.Struct;
          tmp750.ID = 4;
          await oprot.WriteFieldBeginAsync(tmp750, cancellationToken);
          await Records.WriteAsync(oprot, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Nodes != null) && __isset.nodes)
        {
          tmp750.Name = "nodes";
          tmp750.Type = TType.Struct;
          tmp750.ID = 5;
          await oprot.WriteFieldBeginAsync(tmp750, cancellationToken);
          await Nodes.WriteAsync(oprot, cancellationToken);
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Message_blob != null) && __isset.message_blob)
        {
          tmp750.Name = "message_blob";
          tmp750.Type = TType.String;
          tmp750.ID = 7;
          await oprot.WriteFieldBeginAsync(tmp750, cancellationToken);
          await oprot.WriteBinaryAsync(Message_blob, cancellationToken);
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
      if (!(that is HBMessageData other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return ((__isset.path == other.__isset.path) && ((!__isset.path) || (global::System.Object.Equals(Path, other.Path))))
        && ((__isset.pulse == other.__isset.pulse) && ((!__isset.pulse) || (global::System.Object.Equals(Pulse, other.Pulse))))
        && ((__isset.boolval == other.__isset.boolval) && ((!__isset.boolval) || (global::System.Object.Equals(Boolval, other.Boolval))))
        && ((__isset.records == other.__isset.records) && ((!__isset.records) || (global::System.Object.Equals(Records, other.Records))))
        && ((__isset.nodes == other.__isset.nodes) && ((!__isset.nodes) || (global::System.Object.Equals(Nodes, other.Nodes))))
        && ((__isset.message_blob == other.__isset.message_blob) && ((!__isset.message_blob) || (TCollections.Equals(Message_blob, other.Message_blob))));
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if((Path != null) && __isset.path)
        {
          hashcode = (hashcode * 397) + Path.GetHashCode();
        }
        if((Pulse != null) && __isset.pulse)
        {
          hashcode = (hashcode * 397) + Pulse.GetHashCode();
        }
        if(__isset.boolval)
        {
          hashcode = (hashcode * 397) + Boolval.GetHashCode();
        }
        if((Records != null) && __isset.records)
        {
          hashcode = (hashcode * 397) + Records.GetHashCode();
        }
        if((Nodes != null) && __isset.nodes)
        {
          hashcode = (hashcode * 397) + Nodes.GetHashCode();
        }
        if((Message_blob != null) && __isset.message_blob)
        {
          hashcode = (hashcode * 397) + Message_blob.GetHashCode();
        }
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp751 = new StringBuilder("HBMessageData(");
      int tmp752 = 0;
      if((Path != null) && __isset.path)
      {
        if(0 < tmp752++) { tmp751.Append(", "); }
        tmp751.Append("Path: ");
        Path.ToString(tmp751);
      }
      if((Pulse != null) && __isset.pulse)
      {
        if(0 < tmp752++) { tmp751.Append(", "); }
        tmp751.Append("Pulse: ");
        Pulse.ToString(tmp751);
      }
      if(__isset.boolval)
      {
        if(0 < tmp752++) { tmp751.Append(", "); }
        tmp751.Append("Boolval: ");
        Boolval.ToString(tmp751);
      }
      if((Records != null) && __isset.records)
      {
        if(0 < tmp752++) { tmp751.Append(", "); }
        tmp751.Append("Records: ");
        Records.ToString(tmp751);
      }
      if((Nodes != null) && __isset.nodes)
      {
        if(0 < tmp752++) { tmp751.Append(", "); }
        tmp751.Append("Nodes: ");
        Nodes.ToString(tmp751);
      }
      if((Message_blob != null) && __isset.message_blob)
      {
        if(0 < tmp752++) { tmp751.Append(", "); }
        tmp751.Append("Message_blob: ");
        Message_blob.ToString(tmp751);
      }
      tmp751.Append(')');
      return tmp751.ToString();
    }
  }

}
