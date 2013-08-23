using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace X13 {
  public class Topic {
    public static readonly Topic root;
    public const char delmiter='/';
    public const string maskAll="#";
    public const string maskChildren="+";
    private static Func<string, bool, SortedList<string, string>> RequestContext;
    static Topic() {
      root=new Topic(null, "/");
    }

    #region variables
    private SortedList<string, Topic> _children;
    private List<Subscription> _subs;
    private Topic _parent;
    private object _value;
    private string _name;
    private string _path;
    private St _flags;
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
    public Topic this[string path] {
      get {
        return Find(path).FirstOrDefault();
      }
    }
    [Browsable(false)]
    public IEnumerable<Topic> all { get { return new TopicEnumerator(TopicEnumerator.all, 0, this, false); } }
    [Browsable(false)]
    public IEnumerable<Topic> children { get { return new TopicEnumerator(TopicEnumerator.children, 0, this, false); } }
    public IEnumerable<Topic> Find(string mask) {
      if(string.IsNullOrEmpty(mask)) {
        return new Topic[] { this };
      }
      Topic home=this;
      if(mask[0]==delmiter) {
        home=Topic.root;
      }
      return new TopicEnumerator(mask.Split(TopicEnumerator.delmiter, StringSplitOptions.RemoveEmptyEntries), 0, home, true);
    }
    public bool Exist(string path) {
      Topic tmp;
      return Exist(path, out tmp);
    }
    public bool Exist(string mask, out Topic topic) {
      if(string.IsNullOrEmpty(mask)) {
        topic=this;
        return true;
      }
      Topic home=this;
      if(mask[0]==delmiter) {
        home=Topic.root;
      }
      topic=(new TopicEnumerator(mask.Split(TopicEnumerator.delmiter, StringSplitOptions.RemoveEmptyEntries), 0, home, false)).FirstOrDefault();
      return topic!=null;
    }
    public Subscription Subscribe(string mask, Action<Topic, EventArgs> func) {
      Topic home=this;
      Subscription s;
      List<string> maskIt=new List<string>();
      if(string.IsNullOrEmpty(mask)) {
        s=new Subscription(home, new string[0], func);
      } else {
        maskIt.AddRange(mask.Split(TopicEnumerator.delmiter, StringSplitOptions.RemoveEmptyEntries));
        int offs=0;
        Topic tmp;
        if(mask[0]==delmiter) {
          home=Topic.root;
        }
        for(; offs<maskIt.Count; offs++) {
          if(home._children==null || maskIt[offs]==maskAll || maskIt[offs]==maskChildren) {
            break;
          }
          if(home._children.TryGetValue(maskIt[offs], out tmp)) {
            home=tmp;
          } else {
            break;
          }
        }
        maskIt.RemoveRange(0, offs);
        s=new Subscription(home, maskIt.ToArray(), func);
      }
      lock(home) {
        if(home._subs==null) {
          home._subs=new List<Subscription>();
        }
        home._subs.Add(s);
      }
      if(RequestContext!=null) {
        bool found=false;
        Topic tmp=home;
        while(tmp!=null && !found) {
          if(tmp._subs!=null) {
            for(int i=0; i<tmp._subs.Count; i++) {
              if(tmp._subs[i]==s) {
                continue;
              }
              if(tmp._subs[i].Check(maskIt)) {
                found=true;
                break;
              }
            }
          }
          maskIt.Insert(0, tmp.name);
          tmp=tmp.parent;
        }
        if(!found) {
          string req=s.ToString();
          bool setAC=req.EndsWith("/#");
          bool setCAC=req.EndsWith("/+");
          var resp=RequestContext(s.ToString(), func!=null);
          foreach(var kv in resp) {
            if(kv.Key.StartsWith(home.path)) {
              tmp=Get(kv.Key.Substring(home.path.Length), home);
              tmp.SetJson(kv.Value);  //set ActV flag
              if(setAC) {
                tmp.Flags(St.ActCh, true);
              }else if(setCAC && tmp.parent!=null){
                tmp.parent.Flags(St.ActCh, true);
              }
            } else {
              Log.Debug("unrecognized response. mask={0}, resp={1}", s.ToString(), kv.Key);
            }
          }
        }
      }
      s.DoPublish();
      return s;
    }
    private void Flags(St fl, bool st) {
      if(st) {
        _flags|=fl;
      } else {
        _flags&=~fl;
      }
    }
    private bool Flags(St fl) {
      return (_flags & fl)==fl;
    }
    private void SetJson(string json) {

    }
    private Topic Get(string path, Topic home) {
      Topic ret=home, tmp;
      var pt=path.Split(TopicEnumerator.delmiter, StringSplitOptions.RemoveEmptyEntries);
      for(int i=0; i<pt.Length; i++) {
        lock(ret) {
          if(ret._children==null) {
            ret._children=new SortedList<string, Topic>();
          }
          if(!ret._children.TryGetValue(pt[i], out tmp)) {
            tmp=new Topic(ret, pt[i]);
            ret._children.Add(pt[i], tmp);
          }
        }
        ret=tmp;
      }
      return ret;
    }
    public override string ToString() {
      return _path;
    }

    #region operators
    public static implicit operator string(Topic t) {
      if(t._value==null || t._value is string) {
        return t._value as string;
      } else {
        return t._value.ToString();
      }
    }
    public static implicit operator long?(Topic t) {
      if(t._value is long) {
        return (long)t._value;
      } else if(t._value!=null) {
        try {
          return (long)Convert.ChangeType(t._value, typeof(long));
        }
        catch(Exception ex) {
          Log.Warning("{0}.ToLong() - {1}", t._path, ex.Message);
        }
      }
      return null;
    }
    public static implicit operator bool?(Topic t) {
      if(t._value is bool) {
        return (bool)t._value;
      } else if(t._value!=null) {
        try {
          return (bool)Convert.ChangeType(t._value, typeof(bool));
        }
        catch(Exception ex) {
          Log.Warning("{0}.ToBool() - {1}", t._path, ex.Message);
        }
      }
      return null;
    }
    public static implicit operator double?(Topic t) {
      if(t._value is double) {
        return (double)t._value;
      } else if(t._value!=null) {
        try {
          return (double)Convert.ChangeType(t._value, typeof(double));
        }
        catch(Exception ex) {
          Log.Warning("{0}.ToDouble() - {1}", t._path, ex.Message);
        }
      }
      return null;
    }
    #endregion operators

    #region nested types
    [Flags]
    public enum St {
      ActV=1,
      ActCh=2,
    }
    public class Subscription {
      private   Action<Topic, EventArgs> _func;
      private   Topic _owner;
      private   string[] _maskArr;

      public Subscription(Topic home, string[] p, Action<Topic, EventArgs> func) {
        this._owner = home;
        this._maskArr = p;
        this._func = func;
      }

      public bool Check(string path) {
        if(path.StartsWith(_owner.path)) {
          string[] mc=path.Substring(_owner.path.Length).Split(TopicEnumerator.delmiter, StringSplitOptions.RemoveEmptyEntries);
          return Check(mc);
        }
        return false;
      }

      public bool Check(IEnumerable<string> mc) {
        for(int i=0; i<_maskArr.Length && i<=mc.Count(); i++) {
          if(_maskArr[i]==Topic.maskChildren) {
            continue;
          }
          if(_maskArr[i]==Topic.maskAll) {
            return true;
          }
          if(i>=mc.Count() || _maskArr[i]!=mc.ElementAt(i)) {
            return false;
          }
        }
        return (_maskArr.Length==mc.Count());
      }

      public void DoPublish() {
        if(_func!=null) {
          foreach(Topic t in new TopicEnumerator(_maskArr, 0, _owner, false)) {
            try {
              _func(t, null);
            }
            catch(Exception ex) {
              Log.Warning("{0}.{1}({2}) - {3}", _func.Target!=null?_func.Target.GetType().Name:"none", _func.Method.Name, t.path, ex);
            }
          }
        }
      }

      public override string ToString() {
        return _owner.path+"/"+string.Join("/", _maskArr);
      }
    }
    private class TopicEnumerator : IEnumerable<Topic> {
      public static readonly char[] delmiter=new char[] { Topic.delmiter };
      public static readonly string[] all=new string[] { Topic.maskAll };
      public static readonly string[] children=new string[] { Topic.maskChildren };

      private readonly string[] _levels;
      private int _lvl;
      private Topic _cur;
      private bool _createNew;

      public TopicEnumerator(string[] levels, int lvl, Topic cur, bool create) {
        _levels=levels??new string[0];
        _lvl=lvl;
        _cur=cur;
        _createNew=create && _levels.All(z => z!=maskAll && z!=maskChildren);
      }
      public IEnumerator<Topic> GetEnumerator() {
        Topic next;
        Topic[] ch;

        if(_lvl==_levels.Length) {
          yield return _cur;
        }
        for(; _lvl<_levels.Length; _lvl++) {
          if(_cur==null) {
            break;
          }
          if(_levels[_lvl]==maskAll) {
            var hist=new Stack<Topic>();
            hist.Push(_cur);
            next=null;
            do {
              _cur=hist.Peek();
              if((next!=null && next.parent==_cur) || (_cur._children==null || !_cur._children.Any())) {
                next=hist.Pop();
                yield return next;
              } else {
                lock(_cur) {
                  ch=_cur._children.Values.ToArray();
                }
                for(int i=ch.Length-1; i>=0; i--) {
                  hist.Push(ch[i]);
                }
              }
            } while(hist.Any());
            break;
          } else if(_levels[_lvl]==maskChildren) {
            if(_cur._children!=null) {
              lock(_cur) {
                ch=_cur._children.Values.ToArray();
              }
              for(int i=0; i<ch.Length; i++) {
                if(_lvl<_levels.Length-1) {
                  foreach(Topic t2 in new TopicEnumerator(_levels, _lvl+1, ch[i], false)) {
                    yield return t2;
                  }
                } else {
                  yield return ch[i];
                }
              }
            }
            break;
          } else {
            lock(_cur) {
              if(_cur._children==null) {
                if(_createNew) {
                  _cur._children=new SortedList<string, Topic>();
                } else {
                  break;
                }
              }
              if(!_cur._children.TryGetValue(_levels[_lvl], out next)) {
                if(_createNew) {
                  next=new Topic(_cur, _levels[_lvl]);
                  _cur._children.Add(_levels[_lvl], next);
                } else {
                  break;
                }
              }
            }
            _cur=next;
            if(_lvl==_levels.Length-1) {
              yield return _cur;
              break;
            }
          }
        }
      }

      System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
        return GetEnumerator();
      }
    }
    #endregion nested types
  }
}
