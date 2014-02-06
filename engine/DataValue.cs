using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace X13 {
  //public struct DataValue {
  //  public static implicit operator long(DataValue dv) {
  //    switch((DataValueType)dv._type) {
  //    case DataValueType.Null:
  //      return 0L;
  //    case DataValueType.Bool:
  //    case DataValueType.Integer:
  //    case DataValueType.DateTime:
  //      return dv._ld.l;
  //    case DataValueType.Float:
  //      return (long)dv._ld.d;
  //    default:
  //      return (long)Convert.ToInt64(dv._o);
  //    }
  //  }
  //  public static implicit operator DataValue(long v) {
  //    var ret=new DataValue();
  //    ret._type=(int)DataValueType.Bool;
  //    ret._ld.l=v;
  //    return ret;
  //  }
  //  public static implicit operator DataValue(bool b) {
  //    var ret=new DataValue();
  //    ret._type=(int)DataValueType.Bool;
  //    ret._ld.l=b?1:0;
  //    return ret;
  //  }
  //  private PriDT _ld;
  //  private int _type;
  //  private object _o;
  //  private string _json;
  //  public enum DataValueType : int {
  //    Undefined = 0,
  //    Null = 1,
  //    Integer = 2,
  //    DateTime = 3,
  //    Bool = 4,
  //    String = 5,
  //    Float = 6,
  //    Object = 7,
  //    Array = 8,
  //    Record = 9,
  //    Binary = 10
  //  }
  //  [StructLayout(LayoutKind.Explicit)]
  //  private struct PriDT {
  //    [FieldOffset(0)]
  //    public Int64 l;
  //    [FieldOffset(0)]
  //    public double d;
  //  }
  //}
}
