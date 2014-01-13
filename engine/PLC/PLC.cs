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
      engine.SetGlobalValue("console", new FirebugConsole(engine));
      engine.SetGlobalValue("root", new JuTopic(engine, Topic.root));
      engine.SetGlobalValue("Decl", new DeclConstructor(engine));
      engine.Execute(@"function CreateBlock(init, path){ return new init(path); }");
      string add=@"(function(){
        var add=new Decl('Add');
        add.Init=function(path){
        }
        add.Calc=function(sender){
          var r=this.A+this.B;
          var arr=[this.C];
          var i=arr.Length-1;
          for(; i>=0; i--){
            if(arr[i]!=null){
              r+=arr[i];
            }
          }
          this.Q=r;
        }
        add.Register('A', Decl.ip, {'pos':1});
        add.Register('B', Decl.ip);
        add.Register('C', Decl.ip | Decl.optional);
        add.Register('Q', Decl.op, {'pos':1});
      })();";

      engine.Execute(add);
      var test=Topic.root.Get("test");
      DeclInstance.funcs[0].Init(test);
      Topic.Process();
      test.Get("A").value=1;
      test.Get("B").value=Math.PI;
      Topic.Process();

      //engine.Execute("var testAdd=new PLC.Add('/test');");
      //DeclInstance.funcs[0].f.Call(null);

      //engine.ExecuteFile("add.js");
      //engine.Execute("root.value=1;");
      //engine.Execute("var topics=[]; topics['/l1_1']=root.Get('l1_1');");
      //engine.Execute("topics['/l1_1/l1_2']=topics['/l1_1'].Get('l2_1');");
      //engine.Execute("root.Callback(function(){ console.log('run');});");
      //var topics=engine.GetGlobalValue("topics");
      //string json=JSONObject.Stringify(engine, topics);
      //JSONObject.Parse
      //JSONObject.Stringify
    }
  }
  public class JuTopic : ObjectInstance {
    private Topic _ref;
    public JuTopic(ScriptEngine engine, Topic r)
      : base(engine) {
      PopulateFunctions();
      _ref=r;
    }
    [JSProperty(Name = "name")]
    public string Name {
      get {
        return _ref.name;
      }
    }
    [JSProperty(Name = "value")]
    public object value {
      get {
        return _ref.value;
      }
      set {
        _ref.value=value;
      }
    }
    [JSFunction(Name = "Get")]
    public JuTopic Get(string name) {
      return new JuTopic(base.Engine, _ref.Get(name, true));
    }
    [JSFunction]
    public void Callback(ObjectInstance f) {
      var func=(f as FunctionInstance);
      if(func!=null) {
        func.Call(null);
      }
    }
  }

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
    public void Init(Topic owner) {
      if(InitFunc==null){
        return;
      }
      var inst=InitFunc.Engine.CallGlobalFunction<ObjectInstance>("CreateBlock", this.InitFunc, owner.path);
      if(inst==null) {
        return;
      }
      owner.children.changed+=children_changed;
      owner.Get("_f").value=inst;
      //.local=true;
      Topic tPin;
      for(int i=_pins.Count-1; i>=0; i--) {
        if((_pins[i].flags & Flags.optional)!=Flags.optional) {
          tPin=owner.Get(_pins[i].name);
        } else if(!owner.Exist(_pins[i].name, out tPin)) {
          continue;
        }
        inst.SetPropertyValue(_pins[i].name, tPin.value, true);
      }
    }

    private void children_changed(Topic t, Topic.TopicArgs p) {
      Topic tObj;
      Topic owner;
      ObjectInstance inst;
      if(t.parent!=null && t.parent.Exist("_f", out tObj) && (inst=tObj.value as ObjectInstance)!=null) {
        owner=t.parent;
      } else if(t.Exist("_f", out tObj) && (inst=tObj.value as ObjectInstance)!=null) {
        owner=t;
      } else {
        return;
      }
      if(t!=owner) {
        int idx=_pins.FindIndex(z => z.name==t.name);
        if(idx<0) {
          return;
        }
        if((_pins[idx].flags & Flags.input)==Flags.input) {
          inst.SetPropertyValue(_pins[idx].name, t.value, true);
        }
        if(CalcFunc!=null) {
          CalcFunc.Call(inst, t.name);
          Topic tPin;
          for(int i=_pins.Count-1; i>=0; i--) {
            if((_pins[i].flags & Flags.output)==Flags.output) {
              tPin=owner.Get(_pins[i].name);
              object op=inst.GetPropertyValue(_pins[i].name);
              tPin.value=op;
              Log.Debug(tPin.path+"="+(op==null?"null":op.ToString()));
            }
          }
        }
      }
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
}
