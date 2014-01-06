using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13 {
  class Program {
    static void Main(string[] args) {
      var ps=new PersistentStorage();
      ps.Open();
      Topic root=Topic.root;
      root.saved=true;
      //root.value="LMNOPQRSTUVWXYZ";
      root.value="XYZ";
      ps.Save(root);
      Topic l1=root.Get("level1_a");
      l1.saved=true;
      l1.value="123456789ABCDEFGH";
      //l1.value="123456789";
      //ps.Save(l1);
      Console.WriteLine("press Enter");
      Console.ReadLine();
      ps.Close();
    }
  }
}
