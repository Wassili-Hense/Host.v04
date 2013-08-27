using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace X13 {
  public class Topic {
    public static readonly Topic root;
    public static Func<string, bool, SortedList<string, string>> RequestContext;
    static Topic() {
      root=new Topic(null, "/");
    }

    #region variables
    private SortedList<string, Topic> _children;
    private Topic _parent;
    private object _value;
    private string _name;
    private string _path;
    private MaskType _flags;
    #endregion variables

    private Topic(Topic parent, string name) {
      _name=name;
      _parent=parent;
      if(parent!=null) {
        _path=parent.path+"/"+name;
      } else {
        _path=string.Empty;
      }
    }

    [Browsable(false)]
    public Topic parent {
      get { return _parent; }
    }
    [Category("Content"), DisplayName("Value"), Browsable(true), ReadOnly(false)]
    public object value {
      get { return _value; }
      set { _value=value; }
    }
    [Category("Location"), DisplayName("Name")]
    public string name {
      get { return _name; }
      set { _name=value; }
    }
    [Category("Location"), DisplayName("Path"), ReadOnly(true)]
    public string path {
      get { return _path; }
      set { _path=value; }
    }
    public Topic this[string path] { get { return Get(path, true); } }
    [Browsable(false)]
    public Bill all { get { return new Bill(this, true); } }
    [Browsable(false)]
    public Bill children { get { return new Bill(this, false); } }
    public bool Exist(string path) {
      Topic tmp;
      return Exist(path, out tmp);
    }
    public bool Exist(string path, out Topic topic) {
      topic=Get(path, false);
      return topic!=null;
    }
    public override string ToString() {
      return _path;
    }

    private void SetJson(string json) {
    }
    /// <summary> Get item from tree</summary>
    /// <param name="path">relative or absolute path</param>
    /// <param name="create">true - ReqData & create, false - ReqData, null - create</param>
    /// <returns>item or null</returns>
    private Topic Get(string path, bool? create) {
      if(string.IsNullOrEmpty(path)) {
        return this;
      }
      Topic home=this, next;
      if(path[0]==Bill.delmiter) {
        if(path.StartsWith(this._path)) {
          path=path.Substring(this._path.Length);
        } else {
          home=Topic.root;
        }
      }
      var pt=path.Split(Bill.delmiterArr, StringSplitOptions.RemoveEmptyEntries);
      for(int i=0; i<pt.Length; i++) {
        if(pt[i]==Bill.maskAll || pt[i]==Bill.maskChildren) {
          throw new ArgumentException(string.Format("{0}[{1}] dont allow wildcard", this.path, path));
        }
        next=null;
        if(home._children==null) {
          home._children=new SortedList<string, Topic>();
        }
        if(home._children.TryGetValue(pt[i], out next)) {
          home=next;
        } else if(create==true){
          home.ReqData(pt[i], true);
          home._children.TryGetValue(pt[i], out next);
        }
        if(next==null) {
          if(create!=false) {
            next=new Topic(home, pt[i]);
            home._children.Add(pt[i], next);
          } else {
            return null;
          }
        }
        home=next;
      }
      return home;
    }
    /// <summary>Request data from client</summary>
    /// <param name="name">null - get value, "+" - get children, "#" - get all or name of topic</param>
    /// <param name="sync">wait answer</param>
    private void ReqData(string name, bool sync) {
      if(RequestContext==null) {
        return;
      }
      string mask;
      MaskType mt=MaskType.value;
      bool chFl=false;
      if(name==Bill.maskChildren) {
        if((_flags & MaskType.children)==MaskType.children || ChDeep()) {
          return;
        }
        mask=string.Concat(this.path, Bill.delmiter, Bill.maskChildren);
        chFl=true;
      } else if(name==Bill.maskAll) {
        if(ChDeep()) {
          return;
        }
        mask=string.Concat(this.path, Bill.delmiter, Bill.maskAll);
        mt|=MaskType.children | MaskType.all;
      } else if(string.IsNullOrEmpty(name)) {
        if((_flags & MaskType.value)==MaskType.value) {
          return;
        }
        mask=this.path;
      } else {
        if((_flags & MaskType.children)==MaskType.children || ChDeep()) {
          return;
        }
        mask=string.Concat(this.path, Bill.delmiter, name);
      }
      var data=RequestContext(mask, sync);
      if(!sync || data==null) {
        return;
      }
      Topic cur;
      foreach(var kv in data) {
        cur=this.Get(kv.Key, null);
        cur._flags|=mt;
        if(chFl) {
          cur._parent._flags|=MaskType.children;
          chFl=false;
        }
        cur.SetJson(kv.Value);
      }
    }
    private bool ChDeep() {
      for(Topic cur=this; cur!=null; cur=cur.parent) {
        if((cur._flags & MaskType.all)==MaskType.all) {
          return true;
        }
      }
      return false;
    }

    #region operators
    public static implicit operator string(Topic t) {
      if(t._value==null || t._value is string) {
        return t._value as string;
      } else {
        return t._value.ToString();
      }
    }
    public static implicit operator long(Topic t) {
      return (long)Convert.ChangeType(t._value, typeof(long));
    }
    public static implicit operator bool(Topic t) {
      return (bool)Convert.ChangeType(t._value, typeof(bool));
    }
    public static implicit operator double(Topic t) {
      return (double)Convert.ChangeType(t._value, typeof(double));
    }
    #endregion operators

    #region nested types
    [Flags]
    private enum MaskType {
      value=1,
      children=2,
      all=4
    }

    public class Bill : IEnumerable<Topic> {
      public const char delmiter='/';
      public const string maskAll="#";
      public const string maskChildren="+";
      public static readonly char[] delmiterArr=new char[] { delmiter };
      public static readonly string[] allArr=new string[] { maskAll };
      public static readonly string[] childrenArr=new string[] { maskChildren };

      private Topic _home;
      private MaskType _mask;

      public Bill(Topic home, bool deep) {
        _home=home;
        _mask=deep?MaskType.all:MaskType.children;
      }
      public IEnumerator<Topic> GetEnumerator() {
        if(_mask==MaskType.children) {
          _home.ReqData(maskChildren, true);
          if(_home._children==null) {
            _home._children=new SortedList<string, Topic>();
          }
          foreach(var t in _home._children.Values) {
            yield return t;
          }
        } else if(_mask==MaskType.all) {
          _home.ReqData(maskAll, true);
          var hist=new Stack<Topic>();
          Topic[] ch;
          Topic cur;
          hist.Push(_home);
          do {
            cur=hist.Pop();
            yield return cur;
            if(cur._children==null) {
              cur._children=new SortedList<string, Topic>();
            }
            ch=cur._children.Values.ToArray();
            for(int i=ch.Length-1; i>=0; i--) {
              hist.Push(ch[i]);
            }
          } while(hist.Any());
        } else {
          yield return _home;
        }
      }

      System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
        return GetEnumerator();
      }
    }
    
    #endregion nested types
  }
}
