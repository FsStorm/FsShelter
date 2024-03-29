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

  public partial class ListBlobsResult : TBase
  {

    public List<string> Keys { get; set; }

    public string Session { get; set; }

    public ListBlobsResult()
    {
    }

    public ListBlobsResult(List<string> keys, string session) : this()
    {
      this.Keys = keys;
      this.Session = session;
    }

    public ListBlobsResult DeepCopy()
    {
      var tmp487 = new ListBlobsResult();
      if((Keys != null))
      {
        tmp487.Keys = this.Keys.DeepCopy();
      }
      if((Session != null))
      {
        tmp487.Session = this.Session;
      }
      return tmp487;
    }

    public async global::System.Threading.Tasks.Task ReadAsync(TProtocol iprot, CancellationToken cancellationToken)
    {
      iprot.IncrementRecursionDepth();
      try
      {
        bool isset_keys = false;
        bool isset_session = false;
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
                  TList _list488 = await iprot.ReadListBeginAsync(cancellationToken);
                  Keys = new List<string>(_list488.Count);
                  for(int _i489 = 0; _i489 < _list488.Count; ++_i489)
                  {
                    string _elem490;
                    _elem490 = await iprot.ReadStringAsync(cancellationToken);
                    Keys.Add(_elem490);
                  }
                  await iprot.ReadListEndAsync(cancellationToken);
                }
                isset_keys = true;
              }
              else
              {
                await TProtocolUtil.SkipAsync(iprot, field.Type, cancellationToken);
              }
              break;
            case 2:
              if (field.Type == TType.String)
              {
                Session = await iprot.ReadStringAsync(cancellationToken);
                isset_session = true;
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
        if (!isset_keys)
        {
          throw new TProtocolException(TProtocolException.INVALID_DATA);
        }
        if (!isset_session)
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
        var tmp491 = new TStruct("ListBlobsResult");
        await oprot.WriteStructBeginAsync(tmp491, cancellationToken);
        var tmp492 = new TField();
        if((Keys != null))
        {
          tmp492.Name = "keys";
          tmp492.Type = TType.List;
          tmp492.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp492, cancellationToken);
          {
            await oprot.WriteListBeginAsync(new TList(TType.String, Keys.Count), cancellationToken);
            foreach (string _iter493 in Keys)
            {
              await oprot.WriteStringAsync(_iter493, cancellationToken);
            }
            await oprot.WriteListEndAsync(cancellationToken);
          }
          await oprot.WriteFieldEndAsync(cancellationToken);
        }
        if((Session != null))
        {
          tmp492.Name = "session";
          tmp492.Type = TType.String;
          tmp492.ID = 2;
          await oprot.WriteFieldBeginAsync(tmp492, cancellationToken);
          await oprot.WriteStringAsync(Session, cancellationToken);
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
      if (!(that is ListBlobsResult other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return TCollections.Equals(Keys, other.Keys)
        && global::System.Object.Equals(Session, other.Session);
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if((Keys != null))
        {
          hashcode = (hashcode * 397) + TCollections.GetHashCode(Keys);
        }
        if((Session != null))
        {
          hashcode = (hashcode * 397) + Session.GetHashCode();
        }
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp494 = new StringBuilder("ListBlobsResult(");
      if((Keys != null))
      {
        tmp494.Append(", Keys: ");
        Keys.ToString(tmp494);
      }
      if((Session != null))
      {
        tmp494.Append(", Session: ");
        Session.ToString(tmp494);
      }
      tmp494.Append(')');
      return tmp494.ToString();
    }
  }

}
