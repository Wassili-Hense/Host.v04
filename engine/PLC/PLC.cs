using Jurassic;
using Jurassic.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.PLC {
  public class PLC {
    public void Test() {
      var engine = new ScriptEngine();

      //var obj=JSONObject.Parse(engine, "{ \"type\" : \"Statement\", \"declarer\" : \"Add\" }");
      //engine.SetGlobalValue("console", new FirebugConsole(engine));
      //engine.SetGlobalValue("root", new JuTopic(engine, Topic.root));
      //var topics=engine.GetGlobalValue("topics");
      //JSONObject.Parse
      //JSONObject.Stringify

      engine.SetGlobalValue("Decl", new DeclConstructor(engine));
      string add=@"(function(){
        var add=new Decl('Add');
        add.Init=function(path){
        }
        add.Calc=function(sender){
          var r=this.A+this.B;
          if(this.C!=null){
            r+=this.C;
          }
          this.Q=r;
        }
        add.Register('A', Decl.ip, {'pos':1});
        add.Register('B', Decl.ip);
        add.Register('C', Decl.ip | Decl.optional);
        add.Register('Q', Decl.op, {'pos':1});
        return add;
      })();";

      engine.Execute(add);
      var test=Topic.root.Get("test");
      test.value=DeclInstance.funcs[0].Init();
      Topic.Process();
      test.Get("A").value=1;
      test.Get("B").value=3;
      Topic.Process();
      test.Get("C").value=9;
      Topic.Process();
      string json=JSONObject.Stringify(engine, test.value);
      Log.Debug("{0}", json);

      object o=JSONObject.Parse(engine, json);
      ObjectInstance jo=o as ObjectInstance;
      var test2=Topic.root.Get("test2");
      if(jo!=null) {
        string jo_decl;
        if((jo.GetPropertyValue("class") as string)=="function" && (jo_decl=jo.GetPropertyValue("declarer") as string)!=null) {
          var decl=DeclInstance.funcs.FirstOrDefault(z => z._name==jo_decl);
          if(decl!=null) {
            test2.value=decl.Init();
          }
        }
      }
      Topic.Process();
      test2.Get("A").value=1;
      test2.Get("B").value=3;
      Topic.Process();
      test2.Get("C").value=9;
      Topic.Process();
      json=JSONObject.Stringify(engine, test2.value);
      Log.Debug("{0}", json);

    }
  }
  //public class JuTopic : ObjectInstance {
  //  private Topic _ref;
  //  public JuTopic(ScriptEngine engine, Topic r)
  //    : base(engine) {
  //    PopulateFunctions();
  //    _ref=r;
  //  }
  //  [JSProperty(Name = "name")]
  //  public string Name {
  //    get {
  //      return _ref.name;
  //    }
  //  }
  //  [JSProperty(Name = "value")]
  //  public object value {
  //    get {
  //      return _ref.value;
  //    }
  //    set {
  //      _ref.value=value;
  //    }
  //  }
  //  [JSFunction(Name = "Get")]
  //  public JuTopic Get(string name) {
  //    return new JuTopic(base.Engine, _ref.Get(name, true));
  //  }
  //}

  public class DeclConstructor : ClrFunction {
    public DeclConstructor(ScriptEngine engine)
      : base(engine.Function.InstancePrototype, "Func", new DeclInstance(engine.Object.InstancePrototype)) {
      this.DefineProperty("ip", new PropertyDescriptor(1, PropertyAttributes.Sealed), true);
      this.DefineProperty("op", new PropertyDescriptor(2, PropertyAttributes.Sealed), true);
      this.DefineProperty("optional", new PropertyDescriptor(4, PropertyAttributes.Sealed), true);
    }

    [JSConstructorFunction]
    public DeclInstance Construct(string name) {
      return new DeclInstance(this.InstancePrototype, name);
    }
  }

  public class DeclInstance : ObjectInstance {
    public static List<DeclInstance> funcs=new List<DeclInstance>();
    public string _name;
    public List<PinDecl> _pins;

    public DeclInstance(ObjectInstance prototype)
      : base(prototype) {
      this.PopulateFunctions();
    }

    public DeclInstance(ObjectInstance prototype, string name)
      : base(prototype) {
      _name=name;
      _pins=new List<PinDecl>();
      funcs.Add(this);
    }

    [JSFunction(Name = "Register")]
    public void Register(string variable, int flags) {
      Register(variable, flags, null);
    }
    [JSFunction(Name = "Register")]
    public void Register(string variable, int flags, ObjectInstance p) {
      var pin =new PinDecl();
      pin.name=variable;
      pin.flags=(Flags)flags;
      if(p!=null) {
        foreach(var pi in p.Properties) {
          switch(pi.Name) {
          case "pos":
            pin.pos=(int)pi.Value;
            break;
          }
        }
      }
      _pins.Add(pin);
    }
    [JSProperty(Name = "Init")]
    public FunctionInstance InitFunc { get; set; }
    [JSProperty(Name = "Calc")]
    public FunctionInstance CalcFunc { get; set; }
    [JSProperty(Name = "Deinit")]
    public FunctionInstance DeinitFunc { get; set; }
    public FuncInst Init() {
      return new FuncInst(Engine, this);
    }
    [Flags]
    public enum Flags {
      input=1,
      output=2,
      optional=4,
    }
    public struct PinDecl {
      public string name;
      public Flags flags;
      public int pos;
    }
  }

  public class FuncInst : ObjectInstance, ITenant {
    private Topic _owner;
    private DeclInstance _decl;

    public FuncInst(ScriptEngine engine, DeclInstance decl)
      : base(engine) {
      _decl=decl;
      base.DefineProperty("class", new PropertyDescriptor("function", PropertyAttributes.Enumerable), true);
      this.PopulateFunctions();
    }
    [JSProperty(Name = "declarer")]
    public string declarer { get { return _decl._name; } }
    public Topic owner { get { return _owner; } set { SetOwner(value); } }
    private void SetOwner(Topic owner) {
      if(_owner!=owner) {
        if(_owner!=null) {

        }
        _owner=owner;
        if(_owner!=null) {
          for(int i=_decl._pins.Count-1; i>=0; i--) {
            base.DefineProperty(_decl._pins[i].name, new PropertyDescriptor(
              new TopicGetter(Engine, this, _decl._pins[i]),
              new TopicSetter(Engine, this, _decl._pins[i]),
              PropertyAttributes.IsAccessorProperty | PropertyAttributes.NonEnumerable | PropertyAttributes.Writable), true);
          }
          if(_decl.InitFunc!=null) {
            _decl.InitFunc.Call(this, owner.path);
          }
          _owner.children.changed+=children_changed;
        }
      }
    }
    private void children_changed(Topic t, Topic.TopicArgs a) {
      if(_decl.CalcFunc!=null) {
        _decl.CalcFunc.Call(this, t.name);
      }
    }
    private class TopicSetter : FunctionInstance {
      private   FuncInst _owner;
      private   DeclInstance.PinDecl _decl;
      private   Topic _ref;

      public TopicSetter(ScriptEngine engine, FuncInst funcInst, DeclInstance.PinDecl pinDecl)
        : base(engine) {
        _owner = funcInst;
        _decl = pinDecl;

        if((_decl.flags & DeclInstance.Flags.optional)!=DeclInstance.Flags.optional) {
          _ref=_owner._owner.Get(_decl.name);
        } else {
          _owner._owner.Exist(_decl.name, out _ref);       
        }

      }
      public override object CallLateBound(object thisObject, params object[] argumentValues) {
        if(argumentValues.Length==1) {
          if(_ref==null) {
            _ref=_owner._owner.Get(_decl.name);
          }
          _ref.value=argumentValues[0];
          Log.Debug("{0}={1}", _ref.path, argumentValues[0]);
        }
        return null;
      }
    }
    private class TopicGetter : FunctionInstance {
      private   FuncInst _owner;
      private   DeclInstance.PinDecl _decl;
      private   Topic _ref;

      public TopicGetter(ScriptEngine engine, FuncInst funcInst, DeclInstance.PinDecl pinDecl)
        : base(engine) {
        _owner = funcInst;
        _decl = pinDecl;

        if((_decl.flags & DeclInstance.Flags.optional)!=DeclInstance.Flags.optional) {
          _ref=_owner._owner.Get(_decl.name);
        }
      }
      public override object CallLateBound(object thisObject, params object[] argumentValues) {
        if(_ref==null && !_owner._owner.Exist(_decl.name, out _ref)) {
          Log.Debug("get {0}/{1} - undefined", _owner._owner.path, _decl.name);
          return Undefined.Value;
        }
        Log.Debug("{0}={1}", _ref.path, _ref.value);
        return _ref.value;
      }
    }
  }
}
