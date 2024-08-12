using System;
using System.Linq;
using System.Numerics;
using DynamicEnums;
using NUnit.Framework;

namespace Tests;

public class EnumTests {

    [Test]
    public void TestRegularEnums() {
        Assert.AreEqual(
            new[] {TestEnum.Zero, TestEnum.One, TestEnum.Two, TestEnum.Eight, TestEnum.Sixteen, TestEnum.EightSixteen, TestEnum.ThirtyTwo, TestEnum.OneTwentyEight, TestEnum.OneTwentyEightTwoOne},
            EnumHelper.GetValues<TestEnum>());
        Assert.AreEqual(
            new[] {TestEnum.Zero, TestEnum.One, TestEnum.Two, TestEnum.Eight, TestEnum.Sixteen, TestEnum.ThirtyTwo, TestEnum.OneTwentyEight},
            EnumHelper.GetUniqueValues<TestEnum>());

        Assert.AreEqual(
            new[] {TestEnum.One, TestEnum.Two, TestEnum.Eight, TestEnum.Sixteen, TestEnum.EightSixteen},
            EnumHelper.GetFlags(TestEnum.One | TestEnum.Sixteen | TestEnum.Eight | TestEnum.Two, false));
        Assert.AreEqual(
            new[] {TestEnum.Zero, TestEnum.One, TestEnum.Two, TestEnum.Eight, TestEnum.Sixteen, TestEnum.EightSixteen},
            EnumHelper.GetFlags(TestEnum.One | TestEnum.Sixteen | TestEnum.Eight | TestEnum.Two));

        Assert.AreEqual(
            new[] {TestEnum.One, TestEnum.Two, TestEnum.Eight, TestEnum.Sixteen},
            EnumHelper.GetUniqueFlags(TestEnum.One | TestEnum.Sixteen | TestEnum.Eight | TestEnum.Two));

        Assert.AreEqual(TestEnum.One.HasAnyFlags(TestEnum.Two | TestEnum.One), true);
        Assert.AreEqual(TestEnum.One.HasAnyFlags(TestEnum.One), true);
        Assert.AreEqual(TestEnum.One.HasAnyFlags(TestEnum.Two), false);
        Assert.AreEqual(TestEnum.One.HasAnyFlags((TestEnum) 0), false);

        Assert.AreEqual(TestEnum.One.HasAllFlags(TestEnum.Two | TestEnum.One), TestEnum.One.HasFlag(TestEnum.Two | TestEnum.One));
        Assert.AreEqual(TestEnum.One.HasAllFlags(TestEnum.One), TestEnum.One.HasFlag(TestEnum.One));
        Assert.AreEqual(TestEnum.One.HasAllFlags((TestEnum) 0), TestEnum.One.HasFlag((TestEnum) 0));
    }

    [Test]
    public void TestDynamicEnums() {
        var combined = DynamicEnum.Add<TestDynamicEnum>("Combined", (1 << 7) | (1 << 13));
        var flags = new TestDynamicEnum[100];
        for (var i = 0; i < flags.Length; i++)
            flags[i] = DynamicEnum.AddFlag<TestDynamicEnum>("Flag" + i);
        var zero = DynamicEnum.Add<TestDynamicEnum>("Zero", 0);

        DynamicEnum.Add<TestEnumWithConstructor>("Test", 10);
        Assert.AreEqual(DynamicEnum.GetEnumValue<TestEnumWithConstructor>(10).ToString(), "TestModified");

        Assert.AreEqual(
            flags.Append(zero).Prepend(combined),
            DynamicEnum.GetValues<TestDynamicEnum>());
        Assert.AreEqual(
            flags.Prepend(zero),
            DynamicEnum.GetUniqueValues<TestDynamicEnum>());

        Assert.AreEqual(DynamicEnum.GetValue(flags[7]), BigInteger.One << 7);
        Assert.AreEqual(DynamicEnum.GetEnumValue<TestDynamicEnum>(BigInteger.One << 75), flags[75]);

        Assert.AreEqual(DynamicEnum.GetValue(DynamicEnum.Or(flags[2], flags[17])), BigInteger.One << 2 | BigInteger.One << 17);
        Assert.AreEqual(DynamicEnum.GetValue(DynamicEnum.And(flags[2], flags[3])), BigInteger.Zero);
        Assert.AreEqual(DynamicEnum.And(DynamicEnum.Or(flags[24], flags[52]), DynamicEnum.Or(flags[52], flags[75])), flags[52]);
        Assert.AreEqual(DynamicEnum.Xor(DynamicEnum.Or(flags[85], flags[73]), flags[73]), flags[85]);
        Assert.AreEqual(DynamicEnum.Xor(DynamicEnum.Or(flags[85], DynamicEnum.Or(flags[73], flags[12])), flags[73]), DynamicEnum.Or(flags[85], flags[12]));
        Assert.AreEqual(DynamicEnum.GetValue(DynamicEnum.Neg(flags[74])), ~(BigInteger.One << 74));

        Assert.AreEqual(DynamicEnum.Or(flags[24], flags[52]).HasAllFlags(flags[24]), true);
        Assert.AreEqual(DynamicEnum.Or(flags[24], flags[52]).HasAnyFlags(flags[24]), true);
        Assert.AreEqual(DynamicEnum.Or(flags[24], flags[52]).HasAllFlags(DynamicEnum.Or(flags[24], flags[26])), false);
        Assert.AreEqual(DynamicEnum.Or(flags[24], flags[52]).HasAnyFlags(DynamicEnum.Or(flags[24], flags[26])), true);
        Assert.AreEqual(flags[24].HasAllFlags(DynamicEnum.GetEnumValue<TestDynamicEnum>(0)), true);
        Assert.AreEqual(flags[24].HasAnyFlags(DynamicEnum.GetEnumValue<TestDynamicEnum>(0)), false);

        Assert.AreEqual(DynamicEnum.Parse<TestDynamicEnum>("Flag24"), flags[24]);
        Assert.AreEqual(DynamicEnum.Parse<TestDynamicEnum>("Flag24, Flag43"), DynamicEnum.Or(flags[24], flags[43]));

        Assert.AreEqual(flags[24].ToString(), "Flag24");
        Assert.AreEqual(zero.ToString(), "Zero");
        Assert.AreEqual(DynamicEnum.GetEnumValue<TestDynamicEnum>(0).ToString(), "Zero");
        Assert.AreEqual(DynamicEnum.Or(flags[24], flags[43]).ToString(), "Flag24, Flag43");
        Assert.AreEqual(DynamicEnum.Or(flags[24], DynamicEnum.GetEnumValue<TestDynamicEnum>(new BigInteger(1) << 99)).ToString(), "Flag24, Flag99");
        Assert.AreEqual(DynamicEnum.Or(flags[24], DynamicEnum.GetEnumValue<TestDynamicEnum>(new BigInteger(1) << 101)).ToString(), (DynamicEnum.GetValue(flags[24]) | new BigInteger(1) << 101).ToString());

        Assert.True(DynamicEnum.IsDefined(flags[27]));
        Assert.True(DynamicEnum.IsDefined(combined));
        Assert.False(DynamicEnum.IsDefined(DynamicEnum.Or(flags[17], flags[49])));
        Assert.False(DynamicEnum.IsDefined(DynamicEnum.Or(combined, flags[49])));

        Assert.AreEqual(
            new[] {combined, flags[0], flags[7], flags[13]},
            DynamicEnum.GetFlags(DynamicEnum.Or(DynamicEnum.Or(flags[0], flags[13]), flags[7]), false));
        Assert.AreEqual(
            new[] {combined, flags[0], flags[7], flags[13], zero},
            DynamicEnum.GetFlags(DynamicEnum.Or(DynamicEnum.Or(flags[0], flags[13]), flags[7])));

        Assert.AreEqual(
            new[] {flags[0], flags[7], flags[13]},
            DynamicEnum.GetUniqueFlags(DynamicEnum.Or(DynamicEnum.Or(flags[0], flags[13]), flags[7])));
    }

    [Flags]
    private enum TestEnum {

        Zero = 0,
        One = 1,
        Two = 2,
        EightSixteen = TestEnum.Eight | TestEnum.Sixteen,
        Eight = 8,
        OneTwentyEightTwoOne = TestEnum.OneTwentyEight | TestEnum.Two | TestEnum.One,
        ThirtyTwo = 32,
        Sixteen = 16,
        OneTwentyEight = 128,

    }

    private class TestDynamicEnum : DynamicEnum;

    private class TestEnumWithConstructor : DynamicEnum {

        public TestEnumWithConstructor(string name, BigInteger value, bool defined) : base($"{name}Modified", value, defined) {}

    }

}
