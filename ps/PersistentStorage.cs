using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace X13 {
  public class PersistentStorage {
    /// <summary>data stored in this record</summary>
    private const uint FL_SAVED_I  =0x01000000;
    /// <summary>data stored as a separate record</summary>
    private const uint FL_SAVED_E  =0x02000000;
    private const uint FL_SAVED_A  =0x07000000;
    private const uint FL_LOCAL    =0x08000000;
    private const uint FL_RECORD   =0x40000000;
    private const uint FL_REMOVED  =0x80000000;
    private const int FL_REC_LEN   =0x00FFFFFF;
    private const int FL_DATA_LEN  =0x3FFFFFFF;

    private Dictionary<Topic, Record> _tr;
    private List<FRec> _free;
    private FileStream _file;
    private List<Record> _refitParent;

    public PersistentStorage() {
      _tr=new Dictionary<Topic, Record>(4096);
      _free=new List<FRec>(1024);
    }
    public void Open() {
      _file=new FileStream("data.dat", FileMode.OpenOrCreate, FileAccess.ReadWrite);
      if(_file.Length<0x40) {
        _file.Write(new byte[0x40], 0, 0x40);
      } else {
        Read();
      }
      Topic.root.all.changed+=TopicChanged;
    }

    private void TopicChanged(Topic t, Topic.TopicArgs p) {
      Save(t);
    }
    private void Save(Topic t) {
      Record rec;
      uint parentPos, oldFl_Size;
      int oldDataSize;
      bool recModified=false, dataModified=false;
      if(t.parent==null) {
        parentPos=0;
      } else if(_tr.TryGetValue(t.parent, out rec)) {
        parentPos=rec.pos;
      } else {
        return;  // parent is unknown
      }
      if(!_tr.TryGetValue(t, out rec)) {
        oldFl_Size=0;
        oldDataSize=0;
        rec=new Record(t, parentPos);
        recModified=true;
        dataModified=true;
        _tr[t]=rec;
      } else {
        oldFl_Size=rec.fl_size;
        oldDataSize=rec.data_size+6;
        if(rec.parent!=parentPos) {
          rec.parent=parentPos;
          recModified=true;
        }
        if(rec.name!=t.name) {
          rec.name=t.name;
          recModified=true;
        }
        rec.fl_size=FL_RECORD | (uint)(14+Encoding.UTF8.GetByteCount(rec.name));
        if(t.saved) {
          if(rec.data!=t.GetJson()) {
            rec.data=t.GetJson();
            dataModified=true;
          }
          rec.data_size=(rec.data==null?0:Encoding.UTF8.GetByteCount(rec.data));
          if(rec.data_size>0 && rec.data_size<16) {
            rec.fl_size=(rec.fl_size+(uint)rec.data_size) | FL_SAVED_I;
          } else {
            rec.fl_size|=FL_SAVED_E;
          }
        } else {
          rec.data=null;
          rec.data_size=0;
          rec.saved=0;
          if(oldDataSize>0) {
            dataModified=true;
          }
        }
      }
      byte[] recBuf=new byte[rec.size];
      if(rec.saved!=0 && rec.data!=null) {
        if(rec.saved==FL_SAVED_I) {
          CopyBytes(rec.data_size, recBuf, 8);
          Encoding.UTF8.GetBytes(rec.data).CopyTo(recBuf, recBuf.Length-rec.data_size-2);
          if(rec.data_pos>0 && oldDataSize>0) {
            AddFree(rec.data_pos, oldDataSize);
            rec.data_pos=0;
          }
          if(dataModified) {
            recModified=true;
          }
        } else {
          if(dataModified) {
            byte[] dataBuf=new byte[6+rec.data_size];
            CopyBytes(dataBuf.Length, dataBuf, 0);
            Encoding.UTF8.GetBytes(rec.data).CopyTo(dataBuf, 4);
            if(Write(ref rec.data_pos, dataBuf, oldDataSize)) {
              recModified=true;
            }
          }
          CopyBytes(rec.data_pos, recBuf, 8);
        }
      } else if(rec.data_pos>0 && oldDataSize>0) {
        AddFree(rec.data_pos, oldDataSize);
        rec.data_pos=0;
        recModified=true;
      }
      if(recModified || rec.fl_size!=oldFl_Size) {
        CopyBytes(rec.fl_size, recBuf, 0);
        CopyBytes(rec.parent, recBuf, 4);
        Encoding.UTF8.GetBytes(rec.name).CopyTo(recBuf, 12);
        if(Write(ref rec.pos, recBuf, (int)oldFl_Size & FL_REC_LEN)) {
          var ch=t.children.ToArray();
          for(int i=ch.Length-1; i>=0; i--) {
            Save(ch[i]);
          }
        }
      }
    }
    private bool Write(ref uint pos, byte[] buf, int oldSize) {
      oldSize=((oldSize+7)&0x7FFFFFF8);
      uint oldPos=pos;
      CopyBytes(Crc16.ComputeChecksum(buf, buf.Length-2), buf, buf.Length-2);
      if(((buf.Length+7)&0x00FFFFF8)!=oldSize) {
        if(pos>0) {
          AddFree(pos, oldSize);
        }
        pos=FindFree(buf.Length);
      }
      _file.Position=(long)pos<<3;
      _file.Write(buf, 0, buf.Length);
      return pos!=oldPos;
    }
    public void Close() {
      Topic.root.all.changed-=TopicChanged;
      _file.Close();
    }
    private void Read() {
      _file.Position=0x40;
      long curPos;
      byte[] lBuf=new byte[4];
      _refitParent=new List<Record>();
      Topic t;

      do {
        curPos=_file.Position;
        _file.Read(lBuf, 0, 4);
        uint fl_size=BitConverter.ToUInt32(lBuf, 0);
        int len=(int)fl_size&(((fl_size&FL_RECORD)!=0)?FL_REC_LEN:FL_DATA_LEN);

        if((fl_size & FL_REMOVED)!=0) {
          AddFree((uint)(curPos>>3), (int)fl_size);
        } else if((fl_size&FL_RECORD)!=0) {
          byte[] buf=new byte[len];
          lBuf.CopyTo(buf, 0);
          _file.Read(buf, 4, (int)len-4);
          ushort crc1=BitConverter.ToUInt16(buf, (int)(len-2));
          ushort crc2=Crc16.ComputeChecksum(buf, (int)(len-2));
          if(crc1!=crc2) {
            throw new ApplicationException("CRC Error Record@0x"+curPos.ToString("X8"));
          }
          var r=new Record((uint)(curPos>>3), buf);

          if(r.parent==0) {
            if(r.name=="/") {
              t=Topic.root;
            } else {
              t=null;
            }
          } else if(r.parent<r.pos) {
            t=_tr.FirstOrDefault(z => z.Value.pos==r.parent).Key;
            if(t!=null) {
              t=t.Get(r.name, null);
            }
          } else {
            t=null;
          }
          if(t!=null) {
            AddTopic(t, r);
          } else {
            int idx=indexPPos(_refitParent, r.parent);
            _refitParent.Insert(idx+1, r);
          }
        }
        _file.Position=curPos+((len+7)&0xFFFFFFF8);
      } while(_file.Position<_file.Length);
      _refitParent=null;
    }
    private void AddTopic(Topic t, Record r) {
      if(r.saved!=0) {
        t.saved=true;
        if(r.saved==FL_SAVED_E) {
          byte[] lBuf=new byte[4];
          _file.Position=(long)r.data_pos<<3;
          _file.Read(lBuf, 0, 4);
          int data_size=BitConverter.ToInt32(lBuf, 0);
          if((data_size & (FL_REMOVED | FL_RECORD))==0) {
            r.data_size=data_size-6;
            byte[] buf=new byte[data_size];
            lBuf.CopyTo(buf, 0);
            _file.Read(buf, 4, buf.Length-4);
            ushort crc1=BitConverter.ToUInt16(buf, buf.Length-2);
            ushort crc2=Crc16.ComputeChecksum(buf, buf.Length-2);
            if(crc1!=crc2) {
              throw new ApplicationException("CRC Error Data@0x"+((long)r.pos<<3).ToString("X8"));
            }
            r.data=Encoding.UTF8.GetString(buf, 4, (int)r.data_size);
          }
        }
        t.SetJson(r.data);
      }
      Console.WriteLine("{0}={1} [0x{2:X4}]", t.path, t.value, r.pos);
      _tr[t]=r;
      int idx=indexPPos(_refitParent, r.pos);
      while(idx>=0 && idx<_refitParent.Count && _refitParent[idx].parent==r.pos) {
        Record nextR=_refitParent[idx];
        _refitParent.RemoveAt(idx);
        AddTopic(t.Get(nextR.name, null), nextR);
        idx=indexPPos(_refitParent, r.pos);
      }
    }
    private int indexPPos(List<Record> np, uint ppos) {
      int min=0, mid=-1, max=np.Count-1;

      while(min<=max) {
        mid = (min + max) / 2;
        if(np[mid].parent < ppos) {
          min = mid + 1;
        } else if(np[mid].parent > ppos) {
          max = mid - 1;
          mid = max;
        } else {
          break;
        }
      }
      if(mid>=0) {
        max=np.Count-1;
        while(mid<max && np[mid+1].parent<=ppos) {
          mid++;
        }
      }
      return mid;
    }
    private void AddFree(uint pos, int size) {
      if(((uint)size & FL_REMOVED)==0) {
        if(((long)pos<<3)+((size+7)&0x7FFFFFF8)>=_file.Length) {
          _file.SetLength((long)pos<<3);
          return;
        }
        byte[] buf=BitConverter.GetBytes((uint)size | FL_REMOVED);
        _file.Position=(long)pos<<3;
        _file.Write(buf, 0, 4);
      }
      FRec fr=new FRec(pos, (size+7)&0x7FFFFFF8);
      int idx=_free.BinarySearch(fr);
      idx=idx<0?~idx:idx+1;
      _free.Insert(idx, fr);
    }
    private uint FindFree(int size) {
      size=(size+7)&0x7FFFFFF8;
      uint rez;
      int min=0, mid=-1, max=_free.Count-1;

      while(min<=max) {
        mid = (min + max) / 2;
        if(_free[mid].size < size) {
          min = mid + 1;
        } else if(_free[mid].size > size) {
          max = mid - 1;
          mid = max;
        } else {
          break;
        }
      }
      if(mid>=0) {
        max=_free.Count-1;
        while(mid<max && _free[mid+1].size <= size) {
          mid++;
        }
      }
      if(mid>=0 && mid<=max && _free[mid].size>=size) {
        var fr=_free[mid];
        _free.RemoveAt(mid);
        if(fr.size==size) {
          rez=fr.pos;
        } else {
          AddFree(fr.pos, fr.size-size);
          rez=(uint)(fr.pos+fr.size-size);
        }
      } else {
        rez=(uint)((_file.Length+7)>>3);
      }
      return rez;
    }
    private static void CopyBytes(int value, byte[] buf, int offset) {
      buf[offset++]=(byte)value;
      buf[offset++]=(byte)(value>>8);
      buf[offset++]=(byte)(value>>16);
      buf[offset++]=(byte)(value>>24);
    }
    private static void CopyBytes(uint value, byte[] buf, int offset) {
      buf[offset++]=(byte)value;
      buf[offset++]=(byte)(value>>8);
      buf[offset++]=(byte)(value>>16);
      buf[offset++]=(byte)(value>>24);
    }
    private static void CopyBytes(ushort value, byte[] buf, int offset) {
      buf[offset++]=(byte)value;
      buf[offset++]=(byte)(value>>8);
    }

    private struct FRec : IComparable<FRec> {
      private long val;

      public FRec(uint pos, int size) {
        val=((long)size<<32) | (~pos);
      }
      public int CompareTo(FRec o) {
        return val.CompareTo(o.val);
      }
      public uint pos { get { return ~(uint)val; } }
      public int size { get { return (int)(val>>32); } }

    }
    private class Record {
      public uint pos;
      public uint fl_size;
      public uint parent;
      public uint data_pos;
      public int data_size;
      public string name;
      public string data;

      public Record(uint pos, byte[] buf) {
        this.pos=pos;
        this.fl_size=BitConverter.ToUInt32(buf, 0);
        uint len=fl_size & FL_REC_LEN;
        parent=BitConverter.ToUInt32(buf, 4);
        uint dataPS=BitConverter.ToUInt32(buf, 8);
        if(dataPS>0) {
          if((fl_size&FL_SAVED_A)==FL_SAVED_I) {
            data_pos=0;
            data_size=(int)dataPS;
            data=Encoding.UTF8.GetString(buf, (int)(buf.Length-data_size-2), (int)(data_size));
          } else {
            data_pos=dataPS;
            data_size=0;
            data=null;
          }
        } else {
          data=null;
          data_pos=0;
          data_size=0;
        }
        name=Encoding.UTF8.GetString(buf, 12, (int)(buf.Length-data_size-14));
      }
      public Record(Topic t, uint parent) {
        pos=0;
        data_pos=0;
        this.parent=parent;
        name=t.name;
        fl_size=FL_RECORD | (uint)(14+Encoding.UTF8.GetByteCount(name));
        if(t.saved) {
          data=t.GetJson();
          data_size=(data==null?0:Encoding.UTF8.GetByteCount(data));
          if(data_size>0 && data_size<16) {
            fl_size=(fl_size+(uint)data_size) | FL_SAVED_I;
          } else {
            fl_size|=FL_SAVED_E;
          }
        } else {
          data=null;
          data_size=0;
        }
      }
      public uint saved { get { return fl_size&FL_SAVED_A; } set { fl_size=(fl_size & ~FL_SAVED_A) | (value & FL_SAVED_A); } }
      public bool local { get { return (fl_size&FL_LOCAL)!=0; } set { fl_size=value?fl_size|FL_LOCAL : fl_size&~FL_LOCAL; } }
      public bool removed { get { return (fl_size&FL_REMOVED)!=0; } set { fl_size=value?fl_size|FL_REMOVED:fl_size&~FL_REMOVED; } }
      public int size { get { return (int)fl_size & FL_REC_LEN; } }
    }
  }
}
