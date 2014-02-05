using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13 {
  public class Engine {
    static void Main(string[] args) {
      X13.PLC.PLC plc=new PLC.PLC();
      plc.Test();
    }
  }
}
