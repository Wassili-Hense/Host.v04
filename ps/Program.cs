using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13 {
  class Program {
    static void Main(string[] args) {
      var ps=new PersistentStorage();
      ps.Open();
      //Topic.Process();
      Topic root=Topic.root;
      //root.value="LMNOPQRSTUVWXYZ";
      root.value="XYZ";
      Topic l1=root.Get("level1_a");
      //l1.value="123456789ABCDEFGIHKLMNOPQRSTUVWXYZ";
      //l1.value="123456789ABCDEFGH";
      l1.value="123456789AB";
      Topic l2=l1.Get("level2_a");
      l2.value=true;
      Topic.Process();
      Console.WriteLine("press Enter");
      Console.ReadLine();
      l2.Remove();
      Topic.Process();
      Console.WriteLine("press Enter");
      Console.ReadLine();
      ps.Close();
    }
  }
}
