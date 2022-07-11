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

  public partial class Credentials : TBase
  {

    public Dictionary<string, string> Creds { get; set; }

    public Credentials()
    {
    }

    public Credentials(Dictionary<string, string> creds) : this()
    {
      this.Creds = creds;
    }

    public Credentials DeepCopy()
    {
      var tmp453 = new Credentials();
      if((Creds != null))
      {
        tmp453.Creds = this.Creds.DeepCopy();
      }
      return tmp453;
    }

    public async global::System.Threading.Tasks.Task ReadAsync(TProtocol iprot, CancellationToken cancellationToken)
    {
      iprot.IncrementRecursionDepth();
      try
      {
        bool isset_creds = false;
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
                  TMap _map454 = await iprot.ReadMapBeginAsync(cancellationToken);
                  Creds = new Dictionary<string, string>(_map454.Count);
                  for(int _i455 = 0; _i455 < _map454.Count; ++_i455)
                  {
                    string _key456;
                    string _val457;
                    _key456 = await iprot.ReadStringAsync(cancellationToken);
                    _val457 = await iprot.ReadStringAsync(cancellationToken);
                    Creds[_key456] = _val457;
                  }
                  await iprot.ReadMapEndAsync(cancellationToken);
                }
                isset_creds = true;
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
        if (!isset_creds)
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
        var tmp458 = new TStruct("Credentials");
        await oprot.WriteStructBeginAsync(tmp458, cancellationToken);
        var tmp459 = new TField();
        if((Creds != null))
        {
          tmp459.Name = "creds";
          tmp459.Type = TType.Map;
          tmp459.ID = 1;
          await oprot.WriteFieldBeginAsync(tmp459, cancellationToken);
          {
            await oprot.WriteMapBeginAsync(new TMap(TType.String, TType.String, Creds.Count), cancellationToken);
            foreach (string _iter460 in Creds.Keys)
            {
              await oprot.WriteStringAsync(_iter460, cancellationToken);
              await oprot.WriteStringAsync(Creds[_iter460], cancellationToken);
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
      if (!(that is Credentials other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return TCollections.Equals(Creds, other.Creds);
    }

    public override int GetHashCode() {
      int hashcode = 157;
      unchecked {
        if((Creds != null))
        {
          hashcode = (hashcode * 397) + TCollections.GetHashCode(Creds);
        }
      }
      return hashcode;
    }

    public override string ToString()
    {
      var tmp461 = new StringBuilder("Credentials(");
      if((Creds != null))
      {
        tmp461.Append(", Creds: ");
        Creds.ToString(tmp461);
      }
      tmp461.Append(')');
      return tmp461.ToString();
    }
  }

}
