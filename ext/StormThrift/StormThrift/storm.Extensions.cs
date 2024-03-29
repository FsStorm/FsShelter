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


#nullable disable                // suppress C# 8.0 nullable contexts (we still support earlier versions)
#pragma warning disable IDE0079  // remove unnecessary pragmas
#pragma warning disable IDE1006  // parts of the code use IDL spelling
#pragma warning disable IDE0083  // pattern matching "that is not SomeType" requires net5.0 but we still support earlier versions

namespace StormThrift
{
  public static class stormExtensions
  {
    public static bool Equals(this Dictionary<List<long>, global::StormThrift.NodeInfo> instance, object that)
    {
      if (!(that is Dictionary<List<long>, global::StormThrift.NodeInfo> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<List<long>, global::StormThrift.NodeInfo> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<List<long>, global::StormThrift.NodeInfo> DeepCopy(this Dictionary<List<long>, global::StormThrift.NodeInfo> source)
    {
      if (source == null)
        return null;

      var tmp1709 = new Dictionary<List<long>, global::StormThrift.NodeInfo>(source.Count);
      foreach (var pair in source)
        tmp1709.Add((pair.Key != null) ? pair.Key.DeepCopy() : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1709;
    }


    public static bool Equals(this Dictionary<List<long>, long> instance, object that)
    {
      if (!(that is Dictionary<List<long>, long> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<List<long>, long> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<List<long>, long> DeepCopy(this Dictionary<List<long>, long> source)
    {
      if (source == null)
        return null;

      var tmp1710 = new Dictionary<List<long>, long>(source.Count);
      foreach (var pair in source)
        tmp1710.Add((pair.Key != null) ? pair.Key.DeepCopy() : null, pair.Value);
      return tmp1710;
    }


    public static bool Equals(this Dictionary<global::StormThrift.ExecutorInfo, global::StormThrift.ExecutorStats> instance, object that)
    {
      if (!(that is Dictionary<global::StormThrift.ExecutorInfo, global::StormThrift.ExecutorStats> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<global::StormThrift.ExecutorInfo, global::StormThrift.ExecutorStats> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<global::StormThrift.ExecutorInfo, global::StormThrift.ExecutorStats> DeepCopy(this Dictionary<global::StormThrift.ExecutorInfo, global::StormThrift.ExecutorStats> source)
    {
      if (source == null)
        return null;

      var tmp1711 = new Dictionary<global::StormThrift.ExecutorInfo, global::StormThrift.ExecutorStats>(source.Count);
      foreach (var pair in source)
        tmp1711.Add((pair.Key != null) ? pair.Key.DeepCopy() : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1711;
    }


    public static bool Equals(this Dictionary<global::StormThrift.GlobalStreamId, double> instance, object that)
    {
      if (!(that is Dictionary<global::StormThrift.GlobalStreamId, double> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<global::StormThrift.GlobalStreamId, double> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<global::StormThrift.GlobalStreamId, double> DeepCopy(this Dictionary<global::StormThrift.GlobalStreamId, double> source)
    {
      if (source == null)
        return null;

      var tmp1712 = new Dictionary<global::StormThrift.GlobalStreamId, double>(source.Count);
      foreach (var pair in source)
        tmp1712.Add((pair.Key != null) ? pair.Key.DeepCopy() : null, pair.Value);
      return tmp1712;
    }


    public static bool Equals(this Dictionary<global::StormThrift.GlobalStreamId, global::StormThrift.ComponentAggregateStats> instance, object that)
    {
      if (!(that is Dictionary<global::StormThrift.GlobalStreamId, global::StormThrift.ComponentAggregateStats> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<global::StormThrift.GlobalStreamId, global::StormThrift.ComponentAggregateStats> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<global::StormThrift.GlobalStreamId, global::StormThrift.ComponentAggregateStats> DeepCopy(this Dictionary<global::StormThrift.GlobalStreamId, global::StormThrift.ComponentAggregateStats> source)
    {
      if (source == null)
        return null;

      var tmp1713 = new Dictionary<global::StormThrift.GlobalStreamId, global::StormThrift.ComponentAggregateStats>(source.Count);
      foreach (var pair in source)
        tmp1713.Add((pair.Key != null) ? pair.Key.DeepCopy() : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1713;
    }


    public static bool Equals(this Dictionary<global::StormThrift.GlobalStreamId, global::StormThrift.Grouping> instance, object that)
    {
      if (!(that is Dictionary<global::StormThrift.GlobalStreamId, global::StormThrift.Grouping> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<global::StormThrift.GlobalStreamId, global::StormThrift.Grouping> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<global::StormThrift.GlobalStreamId, global::StormThrift.Grouping> DeepCopy(this Dictionary<global::StormThrift.GlobalStreamId, global::StormThrift.Grouping> source)
    {
      if (source == null)
        return null;

      var tmp1714 = new Dictionary<global::StormThrift.GlobalStreamId, global::StormThrift.Grouping>(source.Count);
      foreach (var pair in source)
        tmp1714.Add((pair.Key != null) ? pair.Key.DeepCopy() : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1714;
    }


    public static bool Equals(this Dictionary<global::StormThrift.GlobalStreamId, long> instance, object that)
    {
      if (!(that is Dictionary<global::StormThrift.GlobalStreamId, long> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<global::StormThrift.GlobalStreamId, long> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<global::StormThrift.GlobalStreamId, long> DeepCopy(this Dictionary<global::StormThrift.GlobalStreamId, long> source)
    {
      if (source == null)
        return null;

      var tmp1715 = new Dictionary<global::StormThrift.GlobalStreamId, long>(source.Count);
      foreach (var pair in source)
        tmp1715.Add((pair.Key != null) ? pair.Key.DeepCopy() : null, pair.Value);
      return tmp1715;
    }


    public static bool Equals(this Dictionary<global::StormThrift.NodeInfo, global::StormThrift.WorkerResources> instance, object that)
    {
      if (!(that is Dictionary<global::StormThrift.NodeInfo, global::StormThrift.WorkerResources> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<global::StormThrift.NodeInfo, global::StormThrift.WorkerResources> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<global::StormThrift.NodeInfo, global::StormThrift.WorkerResources> DeepCopy(this Dictionary<global::StormThrift.NodeInfo, global::StormThrift.WorkerResources> source)
    {
      if (source == null)
        return null;

      var tmp1716 = new Dictionary<global::StormThrift.NodeInfo, global::StormThrift.WorkerResources>(source.Count);
      foreach (var pair in source)
        tmp1716.Add((pair.Key != null) ? pair.Key.DeepCopy() : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1716;
    }


    public static bool Equals(this Dictionary<int, global::StormThrift.LocalAssignment> instance, object that)
    {
      if (!(that is Dictionary<int, global::StormThrift.LocalAssignment> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<int, global::StormThrift.LocalAssignment> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<int, global::StormThrift.LocalAssignment> DeepCopy(this Dictionary<int, global::StormThrift.LocalAssignment> source)
    {
      if (source == null)
        return null;

      var tmp1717 = new Dictionary<int, global::StormThrift.LocalAssignment>(source.Count);
      foreach (var pair in source)
        tmp1717.Add(pair.Key, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1717;
    }


    public static bool Equals(this Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, double>> instance, object that)
    {
      if (!(that is Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, double>> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, double>> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, double>> DeepCopy(this Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, double>> source)
    {
      if (source == null)
        return null;

      var tmp1718 = new Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, double>>(source.Count);
      foreach (var pair in source)
        tmp1718.Add((pair.Key != null) ? pair.Key : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1718;
    }


    public static bool Equals(this Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>> instance, object that)
    {
      if (!(that is Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>> DeepCopy(this Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>> source)
    {
      if (source == null)
        return null;

      var tmp1719 = new Dictionary<string, Dictionary<global::StormThrift.GlobalStreamId, long>>(source.Count);
      foreach (var pair in source)
        tmp1719.Add((pair.Key != null) ? pair.Key : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1719;
    }


    public static bool Equals(this Dictionary<string, Dictionary<string, double>> instance, object that)
    {
      if (!(that is Dictionary<string, Dictionary<string, double>> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, Dictionary<string, double>> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, Dictionary<string, double>> DeepCopy(this Dictionary<string, Dictionary<string, double>> source)
    {
      if (source == null)
        return null;

      var tmp1720 = new Dictionary<string, Dictionary<string, double>>(source.Count);
      foreach (var pair in source)
        tmp1720.Add((pair.Key != null) ? pair.Key : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1720;
    }


    public static bool Equals(this Dictionary<string, Dictionary<string, long>> instance, object that)
    {
      if (!(that is Dictionary<string, Dictionary<string, long>> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, Dictionary<string, long>> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, Dictionary<string, long>> DeepCopy(this Dictionary<string, Dictionary<string, long>> source)
    {
      if (source == null)
        return null;

      var tmp1721 = new Dictionary<string, Dictionary<string, long>>(source.Count);
      foreach (var pair in source)
        tmp1721.Add((pair.Key != null) ? pair.Key : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1721;
    }


    public static bool Equals(this Dictionary<string, List<global::StormThrift.ErrorInfo>> instance, object that)
    {
      if (!(that is Dictionary<string, List<global::StormThrift.ErrorInfo>> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, List<global::StormThrift.ErrorInfo>> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, List<global::StormThrift.ErrorInfo>> DeepCopy(this Dictionary<string, List<global::StormThrift.ErrorInfo>> source)
    {
      if (source == null)
        return null;

      var tmp1722 = new Dictionary<string, List<global::StormThrift.ErrorInfo>>(source.Count);
      foreach (var pair in source)
        tmp1722.Add((pair.Key != null) ? pair.Key : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1722;
    }


    public static bool Equals(this Dictionary<string, double> instance, object that)
    {
      if (!(that is Dictionary<string, double> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, double> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, double> DeepCopy(this Dictionary<string, double> source)
    {
      if (source == null)
        return null;

      var tmp1723 = new Dictionary<string, double>(source.Count);
      foreach (var pair in source)
        tmp1723.Add((pair.Key != null) ? pair.Key : null, pair.Value);
      return tmp1723;
    }


    public static bool Equals(this Dictionary<string, global::StormThrift.Bolt> instance, object that)
    {
      if (!(that is Dictionary<string, global::StormThrift.Bolt> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, global::StormThrift.Bolt> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, global::StormThrift.Bolt> DeepCopy(this Dictionary<string, global::StormThrift.Bolt> source)
    {
      if (source == null)
        return null;

      var tmp1724 = new Dictionary<string, global::StormThrift.Bolt>(source.Count);
      foreach (var pair in source)
        tmp1724.Add((pair.Key != null) ? pair.Key : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1724;
    }


    public static bool Equals(this Dictionary<string, global::StormThrift.ComponentAggregateStats> instance, object that)
    {
      if (!(that is Dictionary<string, global::StormThrift.ComponentAggregateStats> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, global::StormThrift.ComponentAggregateStats> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, global::StormThrift.ComponentAggregateStats> DeepCopy(this Dictionary<string, global::StormThrift.ComponentAggregateStats> source)
    {
      if (source == null)
        return null;

      var tmp1725 = new Dictionary<string, global::StormThrift.ComponentAggregateStats>(source.Count);
      foreach (var pair in source)
        tmp1725.Add((pair.Key != null) ? pair.Key : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1725;
    }


    public static bool Equals(this Dictionary<string, global::StormThrift.DebugOptions> instance, object that)
    {
      if (!(that is Dictionary<string, global::StormThrift.DebugOptions> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, global::StormThrift.DebugOptions> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, global::StormThrift.DebugOptions> DeepCopy(this Dictionary<string, global::StormThrift.DebugOptions> source)
    {
      if (source == null)
        return null;

      var tmp1726 = new Dictionary<string, global::StormThrift.DebugOptions>(source.Count);
      foreach (var pair in source)
        tmp1726.Add((pair.Key != null) ? pair.Key : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1726;
    }


    public static bool Equals(this Dictionary<string, global::StormThrift.LogLevel> instance, object that)
    {
      if (!(that is Dictionary<string, global::StormThrift.LogLevel> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, global::StormThrift.LogLevel> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, global::StormThrift.LogLevel> DeepCopy(this Dictionary<string, global::StormThrift.LogLevel> source)
    {
      if (source == null)
        return null;

      var tmp1727 = new Dictionary<string, global::StormThrift.LogLevel>(source.Count);
      foreach (var pair in source)
        tmp1727.Add((pair.Key != null) ? pair.Key : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1727;
    }


    public static bool Equals(this Dictionary<string, global::StormThrift.SpoutSpec> instance, object that)
    {
      if (!(that is Dictionary<string, global::StormThrift.SpoutSpec> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, global::StormThrift.SpoutSpec> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, global::StormThrift.SpoutSpec> DeepCopy(this Dictionary<string, global::StormThrift.SpoutSpec> source)
    {
      if (source == null)
        return null;

      var tmp1728 = new Dictionary<string, global::StormThrift.SpoutSpec>(source.Count);
      foreach (var pair in source)
        tmp1728.Add((pair.Key != null) ? pair.Key : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1728;
    }


    public static bool Equals(this Dictionary<string, global::StormThrift.StateSpoutSpec> instance, object that)
    {
      if (!(that is Dictionary<string, global::StormThrift.StateSpoutSpec> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, global::StormThrift.StateSpoutSpec> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, global::StormThrift.StateSpoutSpec> DeepCopy(this Dictionary<string, global::StormThrift.StateSpoutSpec> source)
    {
      if (source == null)
        return null;

      var tmp1729 = new Dictionary<string, global::StormThrift.StateSpoutSpec>(source.Count);
      foreach (var pair in source)
        tmp1729.Add((pair.Key != null) ? pair.Key : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1729;
    }


    public static bool Equals(this Dictionary<string, global::StormThrift.StreamInfo> instance, object that)
    {
      if (!(that is Dictionary<string, global::StormThrift.StreamInfo> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, global::StormThrift.StreamInfo> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, global::StormThrift.StreamInfo> DeepCopy(this Dictionary<string, global::StormThrift.StreamInfo> source)
    {
      if (source == null)
        return null;

      var tmp1730 = new Dictionary<string, global::StormThrift.StreamInfo>(source.Count);
      foreach (var pair in source)
        tmp1730.Add((pair.Key != null) ? pair.Key : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1730;
    }


    public static bool Equals(this Dictionary<string, global::StormThrift.ThriftSerializedObject> instance, object that)
    {
      if (!(that is Dictionary<string, global::StormThrift.ThriftSerializedObject> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, global::StormThrift.ThriftSerializedObject> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, global::StormThrift.ThriftSerializedObject> DeepCopy(this Dictionary<string, global::StormThrift.ThriftSerializedObject> source)
    {
      if (source == null)
        return null;

      var tmp1731 = new Dictionary<string, global::StormThrift.ThriftSerializedObject>(source.Count);
      foreach (var pair in source)
        tmp1731.Add((pair.Key != null) ? pair.Key : null, (pair.Value != null) ? pair.Value.DeepCopy() : null);
      return tmp1731;
    }


    public static bool Equals(this Dictionary<string, int> instance, object that)
    {
      if (!(that is Dictionary<string, int> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, int> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, int> DeepCopy(this Dictionary<string, int> source)
    {
      if (source == null)
        return null;

      var tmp1732 = new Dictionary<string, int>(source.Count);
      foreach (var pair in source)
        tmp1732.Add((pair.Key != null) ? pair.Key : null, pair.Value);
      return tmp1732;
    }


    public static bool Equals(this Dictionary<string, long> instance, object that)
    {
      if (!(that is Dictionary<string, long> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, long> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, long> DeepCopy(this Dictionary<string, long> source)
    {
      if (source == null)
        return null;

      var tmp1733 = new Dictionary<string, long>(source.Count);
      foreach (var pair in source)
        tmp1733.Add((pair.Key != null) ? pair.Key : null, pair.Value);
      return tmp1733;
    }


    public static bool Equals(this Dictionary<string, string> instance, object that)
    {
      if (!(that is Dictionary<string, string> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this Dictionary<string, string> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static Dictionary<string, string> DeepCopy(this Dictionary<string, string> source)
    {
      if (source == null)
        return null;

      var tmp1734 = new Dictionary<string, string>(source.Count);
      foreach (var pair in source)
        tmp1734.Add((pair.Key != null) ? pair.Key : null, (pair.Value != null) ? pair.Value : null);
      return tmp1734;
    }


    public static bool Equals(this List<byte[]> instance, object that)
    {
      if (!(that is List<byte[]> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<byte[]> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<byte[]> DeepCopy(this List<byte[]> source)
    {
      if (source == null)
        return null;

      var tmp1735 = new List<byte[]>(source.Count);
      foreach (var elem in source)
        tmp1735.Add((elem != null) ? elem.ToArray() : null);
      return tmp1735;
    }


    public static bool Equals(this List<global::StormThrift.AccessControl> instance, object that)
    {
      if (!(that is List<global::StormThrift.AccessControl> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<global::StormThrift.AccessControl> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<global::StormThrift.AccessControl> DeepCopy(this List<global::StormThrift.AccessControl> source)
    {
      if (source == null)
        return null;

      var tmp1736 = new List<global::StormThrift.AccessControl>(source.Count);
      foreach (var elem in source)
        tmp1736.Add((elem != null) ? elem.DeepCopy() : null);
      return tmp1736;
    }


    public static bool Equals(this List<global::StormThrift.ErrorInfo> instance, object that)
    {
      if (!(that is List<global::StormThrift.ErrorInfo> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<global::StormThrift.ErrorInfo> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<global::StormThrift.ErrorInfo> DeepCopy(this List<global::StormThrift.ErrorInfo> source)
    {
      if (source == null)
        return null;

      var tmp1737 = new List<global::StormThrift.ErrorInfo>(source.Count);
      foreach (var elem in source)
        tmp1737.Add((elem != null) ? elem.DeepCopy() : null);
      return tmp1737;
    }


    public static bool Equals(this List<global::StormThrift.ExecutorAggregateStats> instance, object that)
    {
      if (!(that is List<global::StormThrift.ExecutorAggregateStats> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<global::StormThrift.ExecutorAggregateStats> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<global::StormThrift.ExecutorAggregateStats> DeepCopy(this List<global::StormThrift.ExecutorAggregateStats> source)
    {
      if (source == null)
        return null;

      var tmp1738 = new List<global::StormThrift.ExecutorAggregateStats>(source.Count);
      foreach (var elem in source)
        tmp1738.Add((elem != null) ? elem.DeepCopy() : null);
      return tmp1738;
    }


    public static bool Equals(this List<global::StormThrift.ExecutorInfo> instance, object that)
    {
      if (!(that is List<global::StormThrift.ExecutorInfo> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<global::StormThrift.ExecutorInfo> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<global::StormThrift.ExecutorInfo> DeepCopy(this List<global::StormThrift.ExecutorInfo> source)
    {
      if (source == null)
        return null;

      var tmp1739 = new List<global::StormThrift.ExecutorInfo>(source.Count);
      foreach (var elem in source)
        tmp1739.Add((elem != null) ? elem.DeepCopy() : null);
      return tmp1739;
    }


    public static bool Equals(this List<global::StormThrift.ExecutorSummary> instance, object that)
    {
      if (!(that is List<global::StormThrift.ExecutorSummary> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<global::StormThrift.ExecutorSummary> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<global::StormThrift.ExecutorSummary> DeepCopy(this List<global::StormThrift.ExecutorSummary> source)
    {
      if (source == null)
        return null;

      var tmp1740 = new List<global::StormThrift.ExecutorSummary>(source.Count);
      foreach (var elem in source)
        tmp1740.Add((elem != null) ? elem.DeepCopy() : null);
      return tmp1740;
    }


    public static bool Equals(this List<global::StormThrift.HBPulse> instance, object that)
    {
      if (!(that is List<global::StormThrift.HBPulse> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<global::StormThrift.HBPulse> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<global::StormThrift.HBPulse> DeepCopy(this List<global::StormThrift.HBPulse> source)
    {
      if (source == null)
        return null;

      var tmp1741 = new List<global::StormThrift.HBPulse>(source.Count);
      foreach (var elem in source)
        tmp1741.Add((elem != null) ? elem.DeepCopy() : null);
      return tmp1741;
    }


    public static bool Equals(this List<global::StormThrift.JavaObjectArg> instance, object that)
    {
      if (!(that is List<global::StormThrift.JavaObjectArg> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<global::StormThrift.JavaObjectArg> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<global::StormThrift.JavaObjectArg> DeepCopy(this List<global::StormThrift.JavaObjectArg> source)
    {
      if (source == null)
        return null;

      var tmp1742 = new List<global::StormThrift.JavaObjectArg>(source.Count);
      foreach (var elem in source)
        tmp1742.Add((elem != null) ? elem.DeepCopy() : null);
      return tmp1742;
    }


    public static bool Equals(this List<global::StormThrift.LSTopoHistory> instance, object that)
    {
      if (!(that is List<global::StormThrift.LSTopoHistory> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<global::StormThrift.LSTopoHistory> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<global::StormThrift.LSTopoHistory> DeepCopy(this List<global::StormThrift.LSTopoHistory> source)
    {
      if (source == null)
        return null;

      var tmp1743 = new List<global::StormThrift.LSTopoHistory>(source.Count);
      foreach (var elem in source)
        tmp1743.Add((elem != null) ? elem.DeepCopy() : null);
      return tmp1743;
    }


    public static bool Equals(this List<global::StormThrift.NimbusSummary> instance, object that)
    {
      if (!(that is List<global::StormThrift.NimbusSummary> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<global::StormThrift.NimbusSummary> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<global::StormThrift.NimbusSummary> DeepCopy(this List<global::StormThrift.NimbusSummary> source)
    {
      if (source == null)
        return null;

      var tmp1744 = new List<global::StormThrift.NimbusSummary>(source.Count);
      foreach (var elem in source)
        tmp1744.Add((elem != null) ? elem.DeepCopy() : null);
      return tmp1744;
    }


    public static bool Equals(this List<global::StormThrift.ProfileRequest> instance, object that)
    {
      if (!(that is List<global::StormThrift.ProfileRequest> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<global::StormThrift.ProfileRequest> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<global::StormThrift.ProfileRequest> DeepCopy(this List<global::StormThrift.ProfileRequest> source)
    {
      if (source == null)
        return null;

      var tmp1745 = new List<global::StormThrift.ProfileRequest>(source.Count);
      foreach (var elem in source)
        tmp1745.Add((elem != null) ? elem.DeepCopy() : null);
      return tmp1745;
    }


    public static bool Equals(this List<global::StormThrift.SupervisorSummary> instance, object that)
    {
      if (!(that is List<global::StormThrift.SupervisorSummary> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<global::StormThrift.SupervisorSummary> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<global::StormThrift.SupervisorSummary> DeepCopy(this List<global::StormThrift.SupervisorSummary> source)
    {
      if (source == null)
        return null;

      var tmp1746 = new List<global::StormThrift.SupervisorSummary>(source.Count);
      foreach (var elem in source)
        tmp1746.Add((elem != null) ? elem.DeepCopy() : null);
      return tmp1746;
    }


    public static bool Equals(this List<global::StormThrift.TopologySummary> instance, object that)
    {
      if (!(that is List<global::StormThrift.TopologySummary> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<global::StormThrift.TopologySummary> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<global::StormThrift.TopologySummary> DeepCopy(this List<global::StormThrift.TopologySummary> source)
    {
      if (source == null)
        return null;

      var tmp1747 = new List<global::StormThrift.TopologySummary>(source.Count);
      foreach (var elem in source)
        tmp1747.Add((elem != null) ? elem.DeepCopy() : null);
      return tmp1747;
    }


    public static bool Equals(this List<long> instance, object that)
    {
      if (!(that is List<long> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<long> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<long> DeepCopy(this List<long> source)
    {
      if (source == null)
        return null;

      var tmp1748 = new List<long>(source.Count);
      foreach (var elem in source)
        tmp1748.Add(elem);
      return tmp1748;
    }


    public static bool Equals(this List<string> instance, object that)
    {
      if (!(that is List<string> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this List<string> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static List<string> DeepCopy(this List<string> source)
    {
      if (source == null)
        return null;

      var tmp1749 = new List<string>(source.Count);
      foreach (var elem in source)
        tmp1749.Add((elem != null) ? elem : null);
      return tmp1749;
    }


    public static bool Equals(this THashSet<long> instance, object that)
    {
      if (!(that is THashSet<long> other)) return false;
      if (ReferenceEquals(instance, other)) return true;

      return TCollections.Equals(instance, other);
    }


    public static int GetHashCode(this THashSet<long> instance)
    {
      return TCollections.GetHashCode(instance);
    }


    public static THashSet<long> DeepCopy(this THashSet<long> source)
    {
      if (source == null)
        return null;

      var tmp1750 = new THashSet<long>(source.Count);
      foreach (var elem in source)
        tmp1750.Add(elem);
      return tmp1750;
    }


  }
}
