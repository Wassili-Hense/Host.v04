using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;

namespace X13 {
  public sealed class Topic : IComparable<Topic> {
    public static readonly Topic root;
    public static Func<string, bool, SortedList<string, string>> RequestContext;
    private static SortedList<Topic, object> _prIp;
    private static SortedList<Topic, object> _prOp;
    private static JsonConverter[] _jcs;
    private static int _busyFlag;

    static Topic() {
      _prIp=new SortedList<Topic, object>();
      _prOp=new SortedList<Topic, object>();
      root=new Topic(null, "/");
      _jcs=new JsonConverter[] { new Newtonsoft.Json.Converters.JavaScriptDateTimeConverter() };
      _busyFlag=1;
    }
    public static void Process() {
      if(Interlocked.CompareExchange(ref _busyFlag, 2, 1)!=1) {
        return;
      }
      _prOp=Interlocked.Exchange(ref _prIp, _prOp);
      foreach(var tv in _prOp) {
        tv.Key.SetValue((tv.Key._flags & MaskType.remove)==MaskType.remove?null:tv.Value);
      }
      if(RequestContext!=null) {
        var data=RequestContext(string.Empty, false);
        foreach(var kv in data) {
          Topic t=Topic.root.Get(kv.Key, null);
          t.SetJson(kv.Value);
          _prOp[t]=null;
        }
      }
      foreach(var tv in _prOp) {
        tv.Key.Publish(null);
        if((tv.Key._flags & MaskType.remove)==MaskType.remove) {
          ITenant v;
          if((v=tv.Key._value as ITenant)!=null) {
            v.owner=null;
          }
          if(tv.Key.parent!=null) {
            tv.Key.parent._children.Remove(tv.Key.name);
          }
        }
      }
      _prOp.Clear();
      _busyFlag=1;
    }

    #region variables
    private SortedList<string, Topic> _children;
    private List<Tuple<MaskType, Action<Topic, TopicArgs>>> _subs;
    private Topic _parent;
    private object _value;
    private string _name;
    private string _path;
    private MaskType _flags;
    private string _json;
    #endregion variables

    private Topic(Topic parent, string name) {
      _name=name;
      _parent=parent;
      if(parent==null) {
        _path="/";
      } else if(parent==root) {
        _path="/"+name;
      }else{
        _path=parent.path+"/"+name;
      }
      _subs=new List<Tuple<MaskType, Action<Topic, TopicArgs>>>();
      _flags=MaskType.saved;
    }

    public event Action<Topic, TopicArgs> changed {
      add {
        Subscribe(MaskType.value, value);
        Publish(value);
      }
      remove {
        Unsubscribe(MaskType.value, value);
      }
    }

    [Browsable(false)]
    public Topic parent {
      get { return _parent; }
    }
    [Category("Content"), DisplayName("Value"), Browsable(true), ReadOnly(false)]
    public object value {
      get { return _value; }
      set { _prIp[this]=value; }
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
    [Browsable(false)]
    public Bill all { get { return new Bill(this, true); } }
    [Browsable(false)]
    public Bill children { get { return new Bill(this, false); } }
    public bool this[MaskType t] { get { return (_flags & t)==t; } internal set { _flags=value?_flags|t:_flags&~t; } }

    public bool Exist(string path) {
      Topic tmp;
      return Exist(path, out tmp);
    }
    public bool Exist(string path, out Topic topic) {
      topic=Get(path, false);
      return topic!=null;
    }
    public void Remove() {
      foreach(var t in this.all) {
        t._flags|=MaskType.remove;
        _prIp[t]=null;
      }
    }
    public void Move(Topic nParent, string nName) {
      throw new NotImplementedException();
    }
    public override string ToString() {
      return _path;
    }
    public int CompareTo(Topic other) {
      if(other==null) {
        return 1;
      }
      return string.Compare(this._path, other._path);
    }

    /// <summary> Get item from tree</summary>
    /// <param name="path">relative or absolute path</param>
    /// <param name="create">true - ReqData & create, false - ReqData, null - create</param>
    /// <returns>item or null</returns>
    public Topic Get(string path, bool? create=true) {
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
        } else if(create==true) {
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
    /// <param name="name">null - get current, name of topic, "+" - get children, "#" - get all</param>
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
    private void Subscribe(MaskType maskType, Action<Topic, TopicArgs> func) {
      if(_subs.All(z => z.Item1!=maskType && z.Item2!=func)) {
        _subs.Add(new Tuple<MaskType, Action<Topic, TopicArgs>>(maskType, func));
      }
    }
    private void Unsubscribe(MaskType maskType, Action<Topic, TopicArgs> func) {
      _subs.RemoveAll(z => z.Item1!=maskType && z.Item2!=func);
    }
    private void Publish(Action<Topic, TopicArgs> func) {
      if((_flags & MaskType.changed)!=MaskType.changed) {
        return;
      }
      _flags&=~MaskType.changed;
      ITenant tt;
      if((tt=_value as ITenant)!=null) {
        tt.owner=this;
      }
      if(func!=null) {
        try {
          func(this, null);
        }
        catch(Exception ex) {
          Log.Warning("{0}.{1}({2}, ) - {3}", func.Method.DeclaringType.Name, func.Method.Name, this.path, ex.ToString());
        }
      } else {
        for(Topic cur=this; cur!=null; cur=cur._parent) {
          for(int i=0; i<cur._subs.Count; i++) {
            func=cur._subs[i].Item2;
            if(func!=null 
              && (((cur._subs[i].Item1 & MaskType.all)==MaskType.all)
                || (cur==this && (cur._subs[i].Item1 & MaskType.value)==MaskType.value)
                || (cur==this.parent && (cur._subs[i].Item1 & MaskType.children)==MaskType.children))) {
              try {
                func(this, null);
              }
              catch(Exception ex) {
                Log.Warning("{0}.{1}({2}, ) - {3}", func.Method.DeclaringType.Name, func.Method.Name, this.path, ex.ToString());
              }
            }
          }
        }

      }
    }
    internal void SetJson(string json) {
      bool ch=false;
      object tmp=null;
      if(string.IsNullOrWhiteSpace(json)) {
        tmp=null; // TODO: Remove
      } else if(json[0]=='{') {
        JObject o=JObject.Parse(json);
        JToken jDesc;
        if(o.TryGetValue("+", out jDesc)) {
          string type=jDesc.ToObject<string>();
          if(type=="Topic") {
            JToken jt1;
            if(o.TryGetValue("p", out jt1)) {
              string t1=jt1.Value<string>();
              if(t1.StartsWith("../")) {
                Topic mop=this;
                while(t1.StartsWith("../")) {
                  t1=t1.Substring(3);
                  mop=mop.parent;
                }
                t1=mop.path+"/"+t1;
              }
              tmp=this.Get(t1, null);
            } else {
              Type tt=Type.GetType(type);
              if(tt==null) {
                tmp=o;
              } else if(tt.IsEnum) {
                JToken v;
                if(o.TryGetValue("v", out v)) {
                  tmp=o.ToObject(tt);
                }
              } else {
                o.Remove("+");
                if(o.Count>0) {
                  if(_value==null || _value.GetType()!=tt) {
                    tmp=o.ToObject(tt);
                  } else {
                    tmp=_value;
                    JsonConvert.PopulateObject(o.ToString(), tmp);
                    ch=true;
                  }
                }
              }
            }
          }
        }
      }else if(json.StartsWith("new Date(")) {
        tmp=JsonConvert.DeserializeObject<DateTime>(json, _jcs).ToLocalTime();
      }else{
        tmp=JsonConvert.DeserializeObject(json);
      }
      if(!ch && !object.Equals(tmp, _value)) {
        ITenant tt;
        if((tt=_value as ITenant)!=null) {
          tt.owner=null;
        }
        ch=true;
      }
      if(ch) {
        _value=tmp;
        _flags|=MaskType.changed;
      }
    }
    internal string GetJson() {
      if(_json==null) {
        lock(this) {
          if(_json==null) {
            try {
              Type valueType=_value==null?null:_value.GetType();
              if(valueType==null) {
                _json="null";
              } else if(Type.GetTypeCode(valueType)!=TypeCode.Object) {
                if(valueType.IsEnum) {
                  _json=(new JObject(
                    new JProperty("v", JsonConvert.SerializeObject(_value)),
                    new JProperty("+", valueType.FullName))).ToString();
                } else if(valueType==typeof(string) && string.IsNullOrEmpty((string)_value)) {
                  _json="\"\"";
                } else {
                  _json=JsonConvert.SerializeObject(_value, _jcs);
                }
              } else if(valueType==typeof(Topic)) {
                Topic link=this._value as Topic;
                if(link==null) {
                  _json=(new JObject(new JProperty("+", "Topic"))).ToString();
                } else {
                  string sPath=link.path;
                  Stack<Topic> mPath=new Stack<Topic>();
                  Topic cur=this;
                  do {
                    mPath.Push(cur);
                  } while((cur=cur.parent)!=root);
                  Stack<Topic> lPath=new Stack<Topic>();
                  cur=link;
                  do {
                    lPath.Push(cur);
                  } while((cur=cur.parent)!=root);
                  while(mPath.Peek()==lPath.Peek()) {
                    mPath.Pop();
                    lPath.Pop();
                  }
                  if(mPath.Count<3) {
                    StringBuilder sb=new StringBuilder();
                    for(int i=mPath.Count-1; i>=0; i--) {
                      sb.Append("../");
                    }
                    while(lPath.Count>0) {
                      if(lPath.Count>1) {
                        sb.AppendFormat("{0}/", lPath.Pop().name);
                      } else {
                        sb.AppendFormat(lPath.Pop().name);
                      }
                    }
                    sPath=sb.ToString();
                  }
                  _json=(new JObject(
                    new JProperty("p", sPath),
                    new JProperty("+", "Topic"))).ToString();
                }
              } else {
                JObject o;
                if(valueType==typeof(JObject)) {
                  o=_value as JObject;
                } else {
                  o=JObject.FromObject(_value);
                  o["+"]=valueType.FullName;
                }
                _json=o.ToString();
              }

            }
            catch(Exception ex) {
              Log.Error("{0}.ToJson() val={1}, err={2}", this.path, _value, ex.Message);
            }
          }
        }
      }
      return _json;
    }
    private void SetValue(object v) {
      if(!object.Equals(v, _value)) {
        ITenant tt;
        if((tt=_value as ITenant)!=null) {
          tt.owner=null;
        }
        _value=v;
        _json=null;
        _flags|=MaskType.changed;
      }
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
    public enum MaskType {
      None=0,
      value=1,
      children=2,
      all=4,
      changed=8,
      remove=16,
      saved=32,
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
      public event Action<Topic, TopicArgs> changed {
        add {
          _home.Subscribe(_mask, value);
          foreach(var t in this) {
            t.Publish(value);
          }
        }
        remove {
          _home.Unsubscribe(_mask, value);
        }
      }
      System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
        return GetEnumerator();
      }
    }

    public class TopicArgs : EventArgs {
    }
    #endregion nested types
  }

  public interface ITenant {
    Topic owner { get; set; }
  }
}
