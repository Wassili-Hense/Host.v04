using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace X13 {
  public class PersistentStorage {
    private const uint FL_SAVED    =0x01000000;
    private const uint FL_LOCAL    =0x02000000;
    private const uint FL_EMBEDDED =0x04000000;
    private const uint FL_RECORD   =0x40000000;
    private const uint FL_REMOVED  =0x80000000;
    private const uint FL_REC_LEN  =0x00FFFFFF;
    private const uint FL_DATA_LEN =0x3FFFFFFF;

    private Dictionary<Topic, Record> _tr;
    private List<FRec> _free;
    private FileStream _file;

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
    }
    public void Save(Topic t) {
      Record rec;
      uint parentPos;
      if(t.parent==null) {
        parentPos=0;
      } else if(_tr.TryGetValue(t.parent, out rec)) {
        parentPos=rec.pos;
      } else {
        return;  // parent is unknown
      }

      if(!_tr.TryGetValue(t, out rec)) {
        rec=new Record(t, parentPos);
        _tr[t]=rec;
      } else {
        //rec.Update(t, parentPos);
      }
      rec.Write(_file);
    }
    public void Close() {
      _file.Close();
    }
    private void Read() {
      _file.Position=0x40;
      long curPos;
      byte[] lBuf=new byte[4];
      List<Record> np=new List<Record>();
      Topic t;

      do {
        curPos=_file.Position;
        _file.Read(lBuf, 0, 4);
        uint fl_size=BitConverter.ToUInt32(lBuf, 0);
        uint len=fl_size&(((fl_size&FL_RECORD)!=0)?FL_REC_LEN:FL_DATA_LEN);

        if((fl_size & FL_REMOVED)!=0) {
          AddFree(new FRec((uint)(curPos>>3), (len+7)&0xFFFFFFF8));
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
            t=Topic.root;
          } else if(r.parent<r.pos) {
            t=_tr.FirstOrDefault(z => z.Value.pos==r.parent).Key;
            if(t!=null) {
              t=t.Get(r.name);
            }
          } else {
            t=null;
          }
          if(t!=null) {
            if(r.saved) {
              t.saved=true;
              if(!r.embedded) {
                _file.Position=(long)r.data_pos<<3;
                _file.Read(lBuf, 0, 4);
                uint data_size=BitConverter.ToUInt32(lBuf, 0);
                if((data_size & (FL_REMOVED | FL_RECORD))==0) {
                  r.data_size=data_size-6;
                  buf=new byte[data_size];
                  lBuf.CopyTo(buf, 0);
                  _file.Read(buf, 4, buf.Length-4);
                  crc1=BitConverter.ToUInt16(buf, buf.Length-2);
                  crc2=Crc16.ComputeChecksum(buf, buf.Length-2);
                  if(crc1!=crc2) {
                    throw new ApplicationException("CRC Error Data@0x"+curPos.ToString("X8"));
                  }
                  r.data=Encoding.UTF8.GetString(buf, 4, (int)r.data_size);
                }
              }
              t.value=r.data;
            }
            _tr[t]=r;
            int idx=indexPPos(np, r.pos);
            if(idx>=0) {
              throw new NotImplementedException("NP!!!!");
            }
          } else {
            int idx=indexPPos(np, r.parent);
            np.Insert(idx, r);
          }
        }
        _file.Position=curPos+((len+7)&0xFFFFFFF8);
      } while(_file.Position<_file.Length);
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
    private void AddFree(Record r) {
      FRec fr=new FRec(r.pos, ((uint)r.size+7)&0xFFFFFFF8);
      AddFree(fr);
    }
    private void AddFree(FRec fr) {
      int idx=_free.BinarySearch(fr);
      _free.Insert(idx, fr);
    }

    private struct FRec {
      public FRec(uint pos, uint size) {
        this.pos=pos;
        this.size=size;
      }
      public readonly uint pos;
      public readonly uint size;
    }
    private class Record {
      public uint pos;
      public uint fl_size;
      public uint parent;
      public uint data_pos;
      public uint data_size;
      public string name;
      public string data;

      public Record(uint pos, byte[] buf) {
        this.pos=pos;
        this.fl_size=BitConverter.ToUInt32(buf, 0);
        uint len=fl_size & FL_REC_LEN;
        parent=BitConverter.ToUInt32(buf, 4);
        uint dataPS=BitConverter.ToUInt32(buf, 8);
        if(dataPS>0) {
          if((fl_size&FL_EMBEDDED)!=0) {
            data_pos=0;
            data_size=dataPS;
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
          data=t.value; // t.ToJson()
          data_size=(data==null?0:(uint)(Encoding.UTF8.GetByteCount(data)));
          if(data_size>0 && data_size<16) {
            fl_size=(fl_size+data_size) | FL_SAVED | FL_EMBEDDED;
          } else {
            fl_size|=FL_SAVED;
          }
        } else {
          data=null;
          data_size=0;
        }
      }
      public void Write(FileStream file) {
        pos=(uint)((file.Length+7)>>3);  //!!!!!!!!!!!!!!!!!!!!!!!!!!!

        byte[] recBuf=new byte[size];
        CopyBytes(fl_size, recBuf, 0);
        CopyBytes(parent, recBuf, 4);
        Encoding.UTF8.GetBytes(name).CopyTo(recBuf, 12);
        if(saved && data!=null) {
          if(embedded) {
            CopyBytes(data_size, recBuf, 8);
            Encoding.UTF8.GetBytes(data).CopyTo(recBuf, recBuf.Length-data_size-2);
          } else {
            data_pos=pos+(((uint)size+7)>>3); //!!!!!!!!!!!!!!!!!!!!

            CopyBytes(data_pos, recBuf, 8);
            byte[] dataBuf=new byte[6+data_size];
            CopyBytes(data_size+6, dataBuf, 0);
            Encoding.UTF8.GetBytes(data).CopyTo(dataBuf, 4);
            CopyBytes(Crc16.ComputeChecksum(dataBuf, dataBuf.Length-2), dataBuf, dataBuf.Length-2);
            file.Position=data_pos<<3;
            file.Write(dataBuf, 0, dataBuf.Length);
          }
        }
        CopyBytes(Crc16.ComputeChecksum(recBuf, recBuf.Length-2), recBuf, recBuf.Length-2);
        file.Position=pos<<3;
        file.Write(recBuf, 0, recBuf.Length);
      }
      public bool saved { get { return (fl_size&FL_SAVED)!=0; } set { fl_size=value?fl_size|FL_SAVED:fl_size&~FL_SAVED; } }
      public bool local { get { return (fl_size&FL_LOCAL)!=0; } set { fl_size=value?fl_size|FL_LOCAL : fl_size&~FL_LOCAL; } }
      public bool embedded { get { return (fl_size&FL_EMBEDDED)!=0; } set { fl_size=value?fl_size|FL_EMBEDDED:fl_size&~FL_EMBEDDED; } }
      public bool removed { get { return (fl_size&FL_REMOVED)!=0; } set { fl_size=value?fl_size|FL_REMOVED:fl_size&~FL_REMOVED; } }
      public int size { get { return (int)(fl_size & FL_REC_LEN); } }

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
    }
  }
}
