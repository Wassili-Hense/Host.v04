using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13 {
  public class Topic {
    static Topic() {
      root=new Topic(null, "ROOT");
    }
    public static Topic root;
    public static event Action<Topic> chg;

    public Topic parent;
    public string name;
    public string value;
    public bool saved { get; set; }
    public List<Topic> children;
    private Topic(Topic parent, string name) {
      this.name=name;
      this.parent=parent;
      this.children=new List<Topic>();
    }
    public Topic Get(string name) {
      Topic ret=null;
      ret=children.FirstOrDefault(z => z.name==name);
      if(ret==null) {
        ret=new Topic(this, name);
        children.Add(ret);
        if(chg!=null) {
          chg(ret);
        }
      }
      return ret;
    }
    public string path {
      get {
        if(parent==null) {
          return "/";
        } else if(parent==root) {
          return "/"+name;
        } else {
          return parent.path+"/"+name;
        }
      }
    }
  }
}
