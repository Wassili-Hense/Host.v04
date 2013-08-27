using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using X13;
using System.Collections.Generic;

namespace engine.UT {
  [TestClass]
  public class TopicTst {
    private Topic tr;
    [TestInitialize]
    public void Initialize() {
      tr=Topic.root["/test"];
    }

    [TestMethod]
    public void CreateTopic() {
      Assert.IsNull(Topic.root.parent);
      Assert.IsNotNull(tr);
      Assert.AreEqual<Topic>(Topic.root, tr.parent);
      Assert.AreEqual("test", tr.name);
      Assert.AreEqual("/test", tr.path);
    }
    [TestMethod]
    public void GetChild() {
      Topic t1=Topic.root["/test/child"];
      Topic t2=tr["child"];
      Assert.AreEqual<Topic>(t1, t2);
      Topic t3=tr["/test/child"];
      Assert.AreEqual<Topic>(t1, t3);
    }
    [TestMethod]
    public void GetChildren() {
      Topic t0=tr["child"];
      var arr=t0.children.ToArray();
      Assert.AreEqual(0, arr.Length);
      var t1=t0["ch_a"];
      arr=t0.children.ToArray();
      Assert.AreEqual(1, arr.Length);
      Assert.AreEqual(t1, arr[0]);
      t1=t0["ch_b"];
      var t2=t1["a"];
      t2=t1["b"];
      t1=t0["ch_c"];
      t2=t1["a"];
      arr=t0.children.ToArray();
      Assert.AreEqual(3, arr.Length);
      arr=t0.all.ToArray();
      Assert.AreEqual(7, arr.Length);  // child, ch_a, ch_b, ch_b/a, ch_b/b, ch_c, ch_c/a
      Assert.AreEqual(t2, arr[6]);
      Assert.AreEqual(t1, arr[5]);
      Assert.AreEqual(t0, arr[0]);
    }
    [TestMethod]
    public void Exist() {
      bool rez=Topic.root.Exist("/test/child/d");
      Assert.AreEqual(false, rez);
      Topic t1=tr["child/a"];
      Topic t2;
      rez=Topic.root.Exist("/test/child/a", out t2);
      Assert.AreEqual(true, rez);
      Assert.AreEqual(t1, t2);
    }
    [TestMethod]
    public void ReqData() {
      Topic.RequestContext=ReqCtx;
      var t1=Topic.root["test/req"];
      var t1_ch=t1.children.ToArray();
      Assert.AreEqual(t1_ch.Length, 2);
      Assert.AreEqual(t1_ch[0].path, "/test/req/a");
      Assert.AreEqual(t1_ch[1].parent, t1);
      Assert.AreEqual(t1_ch[1].name, "b");
      t1_ch=t1.all.ToArray();
      Assert.AreEqual(t1_ch.Length, 4);
      Assert.AreEqual(t1_ch[0].path, "/test/req");
      Assert.AreEqual(t1_ch[1].parent, t1);
      Assert.AreEqual(t1_ch[2].name, "c");
      t1_ch=t1.all.ToArray();
      Topic.RequestContext=null;
    }
    SortedList<string, string> ReqCtx(string mask, bool sync) {
      Log.Debug("ReqCtx({0}, {1})", mask, sync);
      SortedList<string,string> ret=new SortedList<string,string>();
      switch(mask) {
      case "/test/req":
        ret["/test/req"]="\"Hello World!!!\"";
        break;
      case "/test/req/+":
        ret["/test/req/a"]="1";
        ret["/test/req/b"]="true";
        break;
      case "/test/req/#":
        ret["/test/req"]="\"Hello World!!!\"";
        ret["/test/req/a"]="1";
        ret["/test/req/a/c"]="3.1415";
        ret["/test/req/b"]="true";
        break;
      }
      return ret;
    }
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void TopicGetWithWildcard() {
      var t=Topic.root["/test/#"];
    }
    [TestMethod]
    public void Value_long() {
      Topic t=Topic.root["/test/long"];
      t.value=1L;
      Assert.AreEqual<long>(1L, t);
      Assert.AreEqual<bool>(true, t);
      Assert.AreEqual<double>(1.0, t);
      Assert.AreEqual<string>(1L.ToString(), t);
      Assert.AreEqual(1L, t.value);
    }
    [TestMethod]
    public void Value_bool() {
      Topic t=Topic.root["/test/bool"];
      t.value=false;
      Assert.AreEqual<long>(0, t);
      Assert.AreEqual<bool>(false, t);
      Assert.AreEqual<double>(0.0, t);
      Assert.AreEqual<string>(false.ToString(), t);
      Assert.AreEqual(false, t.value);
    }
    [TestMethod]
    public void Value_double() {
      Topic t=Topic.root["/test/double"];
      t.value=3.14;
      Assert.AreEqual<long>(3L, t);
      Assert.AreEqual<bool>(true, t);
      Assert.AreEqual<double>(3.14, t);
      Assert.AreEqual<string>(3.14.ToString(), t);
      Assert.AreEqual(3.14, t.value);
    }
    [TestMethod]
    public void Value_strTrue() {
      Topic t=Topic.root["/test/string/true"];
      string val=true.ToString();
      t.value=val;
      try {
        long t_l=t;
        Assert.Fail("strTrue -> long");
      }
      catch(Exception ex) {
        Assert.IsTrue(ex is Exception);
      }
      Assert.AreEqual<bool>(true, t);
      try {
        double t_l=t;
        Assert.Fail("strTrue -> double");
      }
      catch(Exception ex) {
        Assert.IsTrue(ex is Exception);
      }
      Assert.AreEqual<string>(val, t);
      Assert.AreEqual(val, t.value);
    }
    [TestMethod]
    public void Value_strInt() {
      Topic t=Topic.root["/test/string/int"];
      t.value="42";
      Assert.AreEqual<long>(42L, t);
      try {
        bool t_l=t;
        Assert.Fail("strInt -> bool");
      }
      catch(Exception ex) {
        Assert.IsTrue(ex is Exception);
      }
      Assert.AreEqual<double>(42.0, t);
      Assert.AreEqual<string>("42", t);
      Assert.AreEqual("42", t.value);
    }
    [TestMethod]
    public void Value_strFloat() {
      Topic t=Topic.root["/test/string/double"];
      string val=7.91.ToString();
      t.value=val;
      try {
        long t_l=t;
        Assert.Fail("strFloat -> long");
      }
      catch(Exception ex) {
        Assert.IsTrue(ex is Exception);
      }
      try {
        bool t_l=t;
        Assert.Fail("strFloat -> bool");
      }
      catch(Exception ex) {
        Assert.IsTrue(ex is Exception);
      }
      Assert.AreEqual<double>(7.91, t);
      Assert.AreEqual<string>(val, t);
      Assert.AreEqual(val, t.value);
    }
    [TestMethod]
    public void Value_strAlpha() {
      Topic t=Topic.root["/test/string"];
      string val="Hello";
      t.value=val;
      try {
        long t_l=t;
        Assert.Fail("strAlpha -> long");
      }
      catch(Exception ex) {
        Assert.IsTrue(ex is Exception);
      }
      try {
        bool t_l=t;
        Assert.Fail("strAlpha -> bool");
      }
      catch(Exception ex) {
        Assert.IsTrue(ex is Exception);
      }
      try {
        double t_l=t;
        Assert.Fail("strAlpha -> double");
      }
      catch(Exception ex) {
        Assert.IsTrue(ex is Exception);
      }
      Assert.AreEqual<string>(val, t);
      Assert.AreEqual(val, t.value);
    }
  }
}