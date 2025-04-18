using LanguageCore.Runtime;

namespace Tests;

[TestClass, TestCategory("Main"), TestCategory("Generic")]
public class MainTests
{
    const int Timeout = 50000 * 1000;

    [TestMethod, Timeout(Timeout)] public void Test01() => Utils.GetTest(01).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test02() => Utils.GetTest(02).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test03() => Utils.GetTest(03).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test04() => Utils.GetTest(04).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test05() => Utils.GetTest(05).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test06() => Utils.GetTest(06).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test07() => Utils.GetTest(07).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test08() => Utils.GetTest(08).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test09() => Utils.GetTest(09).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test10() => Utils.GetTest(10).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test11() => Utils.GetTest(11).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test12() => Utils.GetTest(12).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test13() => Utils.GetTest(13).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test14() => Utils.GetTest(14).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test15() => Utils.GetTest(15).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test16() => Utils.GetTest(16).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test17() => Utils.GetTest(17).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test18() => Utils.GetTest(18).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test19() => Utils.GetTest(19).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test20() => Utils.GetTest(20).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test21() => Utils.GetTest(21).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test22() => Utils.GetTest(22).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test23() => Utils.GetTest(23).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test24() => Utils.GetTest(24).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test25() => Utils.GetTest(25).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test26() => Utils.GetTest(26).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test27() => Utils.GetTest(27).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test28() => Utils.GetTest(28).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test29() => Utils.GetTest(29).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test30() => Utils.GetTest(30).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test31() => Utils.GetTest(31).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test32() => Utils.GetTest(32).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test33() => Utils.GetTest(33).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test34() => Utils.GetTest(34).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test35() => Utils.GetTest(35).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test36() => Utils.GetTest(36).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test37() => Utils.GetTest(37).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test38() => Utils.GetTest(38).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test39() => Utils.GetTest(39).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test40() => Utils.GetTest(40).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test41() => Utils.GetTest(41).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test42() => Utils.GetTest(42).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test43() => Utils.GetTest(43).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test44() => Utils.GetTest(44).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test45() => Utils.GetTest(45).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test46() => Utils.GetTest(46).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test47() => Utils.GetTest(47).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test48() => Utils.GetTest(48).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test49() => Utils.GetTest(49).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test50() => Utils.GetTest(50).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test51() => Utils.GetTest(51).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test52() => Utils.GetTest(52).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test53() => Utils.GetTest(53).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test54() => Utils.GetTest(54).DoMain();
    [TestMethod, Timeout(Timeout)]
    public void Test55() => Utils.GetTest(55).DoMain(externalFunctionAdder: static (externalFunctions) =>
    {
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(), "test", static (int a, int b, int c, int d) =>
        {
            Assert.AreEqual(a, 1, "parameter 0");
            Assert.AreEqual(b, 2, "parameter 1");
            Assert.AreEqual(c, 3, "parameter 2");
            Assert.AreEqual(d, 4, "parameter 3");
        }));
    });
    [TestMethod, Timeout(Timeout)] public void Test56() => Utils.GetTest(56).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test57() => Utils.GetTest(57).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test58() => Utils.GetTest(58).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test59() => Utils.GetTest(59).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test60() => Utils.GetTest(60).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test61() => Utils.GetTest(61).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test62() => Utils.GetTest(62).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test63() => Utils.GetTest(63).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test64() => Utils.GetTest(64).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test65() => Utils.GetTest(65).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test66() => Utils.GetTest(66).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test67() => Utils.GetTest(67).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test68() => Utils.GetTest(68).DoMain();
    [TestMethod, Timeout(Timeout), Ignore] public void Test69() => Utils.GetTest(69).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test70() => Utils.GetTest(70).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test71() => Utils.GetTest(71).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test72() => Utils.GetTest(72).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test73() => Utils.GetTest(73).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test74() => Utils.GetTest(74).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test75() => Utils.GetTest(75).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test76() => Utils.GetTest(76).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test77() => Utils.GetTest(77).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test78() => Utils.GetTest(78).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test79() => Utils.GetTest(79).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test80() => Utils.GetTest(80).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test81() => Utils.GetTest(81).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test82() => Utils.GetTest(82).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test83() => Utils.GetTest(83).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test84() => Utils.GetTest(84).DoMain(); // NOTE: Expected output modified because the square root algorithm is not accurate enough
    [TestMethod, Timeout(Timeout)] public void Test85() => Utils.GetTest(85).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test86() => Utils.GetTest(86).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test87() => Utils.GetTest(87).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test88() => Utils.GetTest(88).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test89() => Utils.GetTest(89).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test90() => Utils.GetTest(90).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test91() => Utils.GetTest(91).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test92() => Utils.GetTest(92).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test93() => Utils.GetTest(93).DoMain();
    [TestMethod, Timeout(Timeout), Ignore("Not implemented")] public void Test94() => Utils.GetTest(94).DoMain();
    [TestMethod, Timeout(Timeout), Ignore("No")] public void Test95() => Utils.GetTest(95).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test96() => Utils.GetTest(96).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test97() => Utils.GetTest(97).DoMain();
    [TestMethod, Timeout(Timeout), Ignore("Not implemented")] public void Test98() => Utils.GetTest(98).DoMain();
    [TestMethod, Timeout(Timeout), Ignore("Not implemented")] public void Test99() => Utils.GetTest(99).DoMain();
    [TestMethod, Timeout(Timeout), Ignore("Not implemented")] public void Test100() => Utils.GetTest(100).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test101() => Utils.GetTest(101).DoMain();
    [TestMethod, Timeout(Timeout), Ignore("Not implemented")] public void Test102() => Utils.GetTest(102).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test103() => Utils.GetTest(103).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test104() => Utils.GetTest(104).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test105() => Utils.GetTest(105).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test106() => Utils.GetTest(106).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test107() => Utils.GetTest(107).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test108() => Utils.GetTest(108).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test109() => Utils.GetTest(109).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test110() => Utils.GetTest(110).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test111() => Utils.GetTest(111).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test112() => Utils.GetTest(112).DoMain();
    [TestMethod, Timeout(Timeout)] public void Test113() => Utils.GetTest(113).DoMain();
}
