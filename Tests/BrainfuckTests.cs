﻿namespace Tests;

[TestClass, TestCategory("Brainfuck"), TestCategory("FileTest")]
public class BrainfuckFileTests
{
    const int Timeout = 10 * 1000;
    const int LongTimeout = 30 * 1000;

    [TestMethod, Timeout(Timeout)] public void Test01() => Utils.GetTest(01).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test02() => Utils.GetTest(02).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test03() => Utils.GetTest(03).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test04() => Utils.GetTest(04).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test05() => Utils.GetTest(05).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test06() => Utils.GetTest(06).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test07() => Utils.GetTest(07).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test08() => Utils.GetTest(08).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test09() => Utils.GetTest(09).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test10() => Utils.GetTest(10).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test11() => Utils.GetTest(11).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test12() => Utils.GetTest(12).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test13() => Utils.GetTest(13).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test14() => Utils.GetTest(14).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test15() => Utils.GetTest(15).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test16() => Utils.GetTest(16).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test17() => Utils.GetTest(17).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test18() => Utils.GetTest(18).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test19() => Utils.GetTest(19).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test20() => Utils.GetTest(20).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test21() => Utils.GetTest(21).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test22() => Utils.GetTest(22).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test23() => Utils.GetTest(23).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test24() => Utils.GetTest(24).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test25() => Utils.GetTest(25).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test26() => Utils.GetTest(26).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test27() => Utils.GetTest(27).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test28() => Utils.GetTest(28).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test29() => Utils.GetTest(29).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test30() => Utils.GetTest(30).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test31() => Utils.GetTest(31).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test32() => Utils.GetTest(32).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test33() => Utils.GetTest(33).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test34() => Utils.GetTest(34).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test35() => Utils.GetTest(35).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test36() => Utils.GetTest(36).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test37() => Utils.GetTest(37).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test38() => Utils.GetTest(38).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test39() => Utils.GetTest(39).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test40() => Utils.GetTest(40).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test41() => Utils.GetTest(41).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test42() => Utils.GetTest(42).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("Function pointers are not supported")] public void Test43() => Utils.GetTest(43).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test44() => Utils.GetTest(44).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test45() => Utils.GetTest(45).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore(":(")] public void Test46() => Utils.GetTest(46).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test47() => Utils.GetTest(47).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test48() => Utils.GetTest(48).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("Floats not supported")] public void Test49() => Utils.GetTest(49).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("Floats not supported")] public void Test50() => Utils.GetTest(50).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test51() => Utils.GetTest(51).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test52() => Utils.GetTest(52).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test53() => Utils.GetTest(53).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test54() => Utils.GetTest(54).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("External functions not supported")] public void Test55() => Utils.GetTest(55).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test56() => Utils.GetTest(56).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test57() => Utils.GetTest(57).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test58() => Utils.GetTest(58).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("Arrays with big element size not implemented")] public void Test59() => Utils.GetTest(59).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test60() => Utils.GetTest(60).DoBrainfuck();
    [TestMethod, Timeout(LongTimeout)] public void Test61() => Utils.GetTest(61).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test62() => Utils.GetTest(62).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test63() => Utils.GetTest(63).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test64() => Utils.GetTest(64).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test65() => Utils.GetTest(65).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test66() => Utils.GetTest(66).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test67() => Utils.GetTest(67).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test68() => Utils.GetTest(68).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test69() => Utils.GetTest(69).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test70() => Utils.GetTest(70).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test71() => Utils.GetTest(71).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test72() => Utils.GetTest(72).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("i8 not supported")] public void Test73() => Utils.GetTest(73).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("u16 not supported")] public void Test74() => Utils.GetTest(74).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("i16 not supported")] public void Test75() => Utils.GetTest(75).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("u32 not supported")] public void Test76() => Utils.GetTest(76).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("i32 not supported")] public void Test77() => Utils.GetTest(77).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test78() => Utils.GetTest(78).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("i8 not supported")] public void Test79() => Utils.GetTest(79).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("u16 not supported")] public void Test80() => Utils.GetTest(80).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("i16 not supported")] public void Test81() => Utils.GetTest(81).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("u32 not supported")] public void Test82() => Utils.GetTest(82).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("i32 not supported")] public void Test83() => Utils.GetTest(83).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("Floats not supported")] public void Test84() => Utils.GetTest(84).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test85() => Utils.GetTest(85).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("Floats not supported")] public void Test86() => Utils.GetTest(86).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test87() => Utils.GetTest(87).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("Stack pointers not supported")] public void Test88() => Utils.GetTest(88).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test89() => Utils.GetTest(89).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test90() => Utils.GetTest(90).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test91() => Utils.GetTest(91).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test92() => Utils.GetTest(92).DoBrainfuck();
    [TestMethod, Timeout(Timeout)] public void Test93() => Utils.GetTest(93).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("Not implemented")] public void Test94() => Utils.GetTest(94).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("Not implemented")] public void Test95() => Utils.GetTest(95).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("Not implemented")] public void Test96() => Utils.GetTest(96).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("Not implemented")] public void Test97() => Utils.GetTest(97).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("Not implemented")] public void Test98() => Utils.GetTest(98).DoBrainfuck();
    [TestMethod, Timeout(Timeout), Ignore("Not implemented")] public void Test99() => Utils.GetTest(99).DoBrainfuck();
}
