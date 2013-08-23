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
      Assert.AreEqual(7, arr.Length);  // ch_a, ch_b/a, ch_b/b, ch_b, ch_c/a, ch_c, child
      Assert.AreEqual(t2, arr[4]);
      Assert.AreEqual(t1, arr[5]);
      Assert.AreEqual(t0, arr[6]);
      arr=t0.Find("#/a").ToArray();
      Assert.AreEqual(7, arr.Length);  // ch_a, ch_b/a, ch_b/b, ch_b, ch_c/a, ch_c, child
      Assert.AreEqual(t2, arr[4]);
      Assert.AreEqual(t1, arr[5]);
      Assert.AreEqual(t0, arr[6]);
      arr=t0.Find("+/a").ToArray();    // ch_b/a, ch_c/a
      Assert.AreEqual(2, arr.Length);
      Assert.AreEqual(t2, arr[1]);
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
    public void Subsribe() {
      m1Topics=new List<Topic>();
      Topic t;
      t=tr["subs/a/a/a"];
      t=tr["subs/a/a/b"];
      t=tr["subs/a/a/c"];
      t=tr["subs/a/b"];
      t=tr["subs/b"];
      t=tr["subs/c/a/a"];
      t=tr["subs/c/b/a"];

      var s=tr.Subscribe("subs/a/+/a", m1);
      Assert.AreEqual("/test/subs/a/+/a", s.ToString());
      Assert.AreEqual(1, m1Topics.Count);
      Assert.IsTrue(s.Check(new string[] { "d", "a" }));
      Assert.IsFalse(s.Check(new string[] { "a", "A" }));
      Assert.IsFalse(s.Check(new string[] { "a", "#" }));
      Assert.IsTrue(s.Check(tr["subs/a/a/a"].path));
      Assert.IsFalse(s.Check(tr["subs/a/a/b"].path));

      m1Topics.Clear();
      s=tr.Subscribe("subs/a/b/a", m1);
      Assert.AreEqual("/test/subs/a/b/a", s.ToString());
      Assert.AreEqual(0, m1Topics.Count);

      m1Topics.Clear();
      s=tr.Subscribe("subs/c/#", m1);
      Assert.AreEqual("/test/subs/c/#", s.ToString());
      Assert.AreEqual(5, m1Topics.Count);
      Assert.IsTrue(s.Check(new string[0]));
      Assert.IsTrue(s.Check(new string[] { "a", "A" }));
      Assert.IsTrue(s.Check(tr["subs/c/a/a"].path));
      Assert.IsFalse(s.Check(tr["subs/b"].path));

      m1Topics.Clear();
    }
    List<Topic> m1Topics;
    private void m1(Topic t, EventArgs a) {
      m1Topics.Add(t);
    }
    [TestMethod]
    public void Value_long() {
      Topic t=Topic.root["/test/long"];
      t.value=1L;
      Assert.AreEqual<long?>(1L, t);
      Assert.AreEqual<bool?>(true, t);
      Assert.AreEqual<double?>(1.0, t);
      Assert.AreEqual<string>(1L.ToString(), t);
      Assert.AreEqual(1L, t.value);
    }
    [TestMethod]
    public void Value_bool() {
      Topic t=Topic.root["/test/bool"];
      t.value=false;
      Assert.AreEqual<long?>(0, t);
      Assert.AreEqual<bool?>(false, t);
      Assert.AreEqual<double?>(0.0, t);
      Assert.AreEqual<string>(false.ToString(), t);
      Assert.AreEqual(false, t.value);
    }
    [TestMethod]
    public void Value_double() {
      Topic t=Topic.root["/test/double"];
      t.value=3.14;
      Assert.AreEqual<long?>(3L, t);
      Assert.AreEqual<bool?>(true, t);
      Assert.AreEqual<double?>(3.14, t);
      Assert.AreEqual<string>(3.14.ToString(), t);
      Assert.AreEqual(3.14, t.value);
    }
    [TestMethod]
    public void Value_strTrue() {
      Topic t=Topic.root["/test/string/true"];
      string val=true.ToString();
      t.value=val;
      Assert.AreEqual<long?>(null, t);
      Assert.AreEqual<bool?>(true, t);
      Assert.AreEqual<double?>(null, t);
      Assert.AreEqual<string>(val, t);
      Assert.AreEqual(val, t.value);
    }
    [TestMethod]
    public void Value_strInt() {
      Topic t=Topic.root["/test/string/int"];
      t.value="42";
      Assert.AreEqual<long?>(42L, t);
      Assert.AreEqual<bool?>(null, t);
      Assert.AreEqual<double?>(42.0, t);
      Assert.AreEqual<string>("42", t);
      Assert.AreEqual("42", t.value);
    }
    [TestMethod]
    public void Value_strFloat() {
      Topic t=Topic.root["/test/string/double"];
      string val=7.91.ToString();
      t.value=val;
      Assert.AreEqual<long?>(null, t);
      Assert.AreEqual<bool?>(null, t);
      Assert.AreEqual<double?>(7.91, t);
      Assert.AreEqual<string>(val, t);
      Assert.AreEqual(val, t.value);
    }
    [TestMethod]
    public void Value_strAlpha() {
      Topic t=Topic.root["/test/string"];
      string val="Hello";
      t.value=val;
      Assert.AreEqual<long?>(null, t);
      Assert.AreEqual<bool?>(null, t);
      Assert.AreEqual<double?>(null, t);
      Assert.AreEqual<string>(val, t);
      Assert.AreEqual(val, t.value);
    }
  }
}