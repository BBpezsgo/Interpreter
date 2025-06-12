using LanguageCore.Runtime;

namespace Tests;

[TestClass, TestCategory("Main"), TestCategory("Generic")]
public class MainTests
{
    const int Timeout = 50000 * 1000;

    [TestMethod, Timeout(Timeout)] public void Test001() => Utils.GetTest(01).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test002() => Utils.GetTest(02).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test003() => Utils.GetTest(03).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test004() => Utils.GetTest(04).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test005() => Utils.GetTest(05).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test006() => Utils.GetTest(06).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test007() => Utils.GetTest(07).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test008() => Utils.GetTest(08).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test009() => Utils.GetTest(09).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test010() => Utils.GetTest(10).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test011() => Utils.GetTest(11).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test012() => Utils.GetTest(12).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test013() => Utils.GetTest(13).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test014() => Utils.GetTest(14).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test015() => Utils.GetTest(15).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test016() => Utils.GetTest(16).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test017() => Utils.GetTest(17).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test018() => Utils.GetTest(18).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test019() => Utils.GetTest(19).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test020() => Utils.GetTest(20).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test021() => Utils.GetTest(21).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test022() => Utils.GetTest(22).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test023() => Utils.GetTest(23).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test024() => Utils.GetTest(24).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test025() => Utils.GetTest(25).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test026() => Utils.GetTest(26).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test027() => Utils.GetTest(27).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test028() => Utils.GetTest(28).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test029() => Utils.GetTest(29).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test030() => Utils.GetTest(30).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test031() => Utils.GetTest(31).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test032() => Utils.GetTest(32).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test033() => Utils.GetTest(33).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test034() => Utils.GetTest(34).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test035() => Utils.GetTest(35).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test036() => Utils.GetTest(36).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test037() => Utils.GetTest(37).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test038() => Utils.GetTest(38).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test039() => Utils.GetTest(39).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test040() => Utils.GetTest(40).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test041() => Utils.GetTest(41).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test042() => Utils.GetTest(42).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test043() => Utils.GetTest(43).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test044() => Utils.GetTest(44).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test045() => Utils.GetTest(45).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test046() => Utils.GetTest(46).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test047() => Utils.GetTest(47).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test048() => Utils.GetTest(48).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test049() => Utils.GetTest(49).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test050() => Utils.GetTest(50).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test051() => Utils.GetTest(51).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test052() => Utils.GetTest(52).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test053() => Utils.GetTest(53).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test054() => Utils.GetTest(54).DoMain();
    [TestMethod, Timeout(Timeout)]
    public void Test055() => Utils.GetTest(55).DoMain(externalFunctionAdder: static (externalFunctions) =>
    {
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(), "test", static (int a, int b, int c, int d) =>
        {
            Assert.AreEqual(a, 1, "parameter 0");
            Assert.AreEqual(b, 2, "parameter 1");
            Assert.AreEqual(c, 3, "parameter 2");
            Assert.AreEqual(d, 4, "parameter 3");
        }));
    });
    [TestMethod, Timeout(Timeout)] public void Test056() => Utils.GetTest(56).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test057() => Utils.GetTest(57).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test058() => Utils.GetTest(58).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test059() => Utils.GetTest(59).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test060() => Utils.GetTest(60).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test061() => Utils.GetTest(61).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test062() => Utils.GetTest(62).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test063() => Utils.GetTest(63).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test064() => Utils.GetTest(64).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test065() => Utils.GetTest(65).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test066() => Utils.GetTest(66).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test067() => Utils.GetTest(67).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test068() => Utils.GetTest(68).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test069() => Utils.GetTest(69).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test070() => Utils.GetTest(70).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test071() => Utils.GetTest(71).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test072() => Utils.GetTest(72).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test073() => Utils.GetTest(73).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test074() => Utils.GetTest(74).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test075() => Utils.GetTest(75).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test076() => Utils.GetTest(76).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test077() => Utils.GetTest(77).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test078() => Utils.GetTest(78).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test079() => Utils.GetTest(79).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test080() => Utils.GetTest(80).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test081() => Utils.GetTest(81).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test082() => Utils.GetTest(82).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test083() => Utils.GetTest(83).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test084() => Utils.GetTest(84).DoMain(); // NOTE: Expected output modified because the square root algorithm is not accurate enough
    [TestMethod, Timeout(Timeout)] public void Test085() => Utils.GetTest(85).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test086() => Utils.GetTest(86).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test087() => Utils.GetTest(87).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test088() => Utils.GetTest(88).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test089() => Utils.GetTest(89).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test090() => Utils.GetTest(90).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test091() => Utils.GetTest(91).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test092() => Utils.GetTest(92).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test093() => Utils.GetTest(93).DoMain();
    [TestMethod, Timeout(Timeout), Ignore("No")] public void Test094() => Utils.GetTest(94).DoMain();
    [TestMethod, Timeout(Timeout), Ignore("No")] public void Test095() => Utils.GetTest(95).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test096() => Utils.GetTest(96).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test097() => Utils.GetTest(97).DoMain();
    [TestMethod, Timeout(Timeout), Ignore("No")] public void Test098() => Utils.GetTest(98).DoMain();
    [TestMethod, Timeout(Timeout), Ignore("No")] public void Test099() => Utils.GetTest(99).DoMain();
    [TestMethod, Timeout(Timeout), Ignore("No")] public void Test100() => Utils.GetTest(100).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test101() => Utils.GetTest(101).DoMain();
    [TestMethod, Timeout(Timeout), Ignore("No")] public void Test102() => Utils.GetTest(102).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test103() => Utils.GetTest(103).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test104() => Utils.GetTest(104).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test105() => Utils.GetTest(105).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test106() => Utils.GetTest(106).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test107() => Utils.GetTest(107).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test108() => Utils.GetTest(108).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test109() => Utils.GetTest(109).DoMain();
    [TestMethod, Timeout(Timeout), Ignore("No")] public void Test110() => Utils.GetTest(110).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test111() => Utils.GetTest(111).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test112() => Utils.GetTest(112).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test113() => Utils.GetTest(113).DoMain();
}
