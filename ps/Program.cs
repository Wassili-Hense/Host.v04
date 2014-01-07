using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13 {
  class Program {
    static void Main(string[] args) {
      var ps=new PersistentStorage();
      ps.Open();
      Topic.Process();
      Topic root=Topic.root;
      root.saved=true;
      //root.value="LMNOPQRSTUVWXYZ";
      root.value="XYZ";
      Topic.Process();
      Topic l1=root["level1_a"];
      l1.saved=true;
      //l1.value="123456789ABCDEFGIHKLMNOPQRSTUVWXYZ";
      //l1.value="123456789ABCDEFGH";
      l1.value="123456789";
      Topic.Process();
      Console.WriteLine("press Enter");
      Console.ReadLine();
      ps.Close();
    }
  }
}
