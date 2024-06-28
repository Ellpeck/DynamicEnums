# DynamicEnums
Enum-like single-instance values with additional capabilities, including dynamic addition of new arbitrary values and flags

A dynamic enum uses `BigInteger` as its underlying type, allowing for an arbitrary number of enum values to be created, even when a `Flags`-like structure is used that would only allow for up to 64 values in a regular enum. All boolean operations including `And<T>(T, T)`, `Or<T>(T, T)`, `Xor<T>(T, T)` and `Neg<T>(T)` are supported and can be implemented in derived classes using operator overloads.

## Setting it up
To create a custom dynamic enum, simply create a class that extends `DynamicEnum`. New values can then be added using `Add<T>(string, BigInteger)`, `AddValue<T>(string)` or `AddFlag<T>(string)`. In this example, they are added as static values in the class itself, but they can be added from anywhere.

```cs
public class MyEnum : DynamicEnum {
 
    // adding specifically defined values
    public static readonly MyEnum ValueOne = DynamicEnum.Add<MyEnum>("ValueOne", 1);
    public static readonly MyEnum ValueTwo = DynamicEnum.Add<MyEnum>("ValueTwo", 2);
    public static readonly MyEnum ValueThree = DynamicEnum.Add<MyEnum>("ValueThree", 3);
    
    // adding flags, which automatically uses the next available power of two as its value
    public static readonly MyEnum FlagOne = DynamicEnum.AddFlag<MyEnum>("FlagOne");
    public static readonly MyEnum FlagTwo = DynamicEnum.AddFlag<MyEnum>("FlagTwo");
    public static readonly MyEnum FlagThree = DynamicEnum.AddFlag<MyEnum>("FlagThree");
 
    // you can optionally create operator overloads for easier operations
    public static implicit operator BigInteger(MyEnum value) => DynamicEnum.GetValue(value);
    public static implicit operator MyEnum(BigInteger value) => DynamicEnum.GetEnumValue<MyEnum>(value);
    public static MyEnum operator |(MyEnum left, MyEnum right) => DynamicEnum.Or(left, right);
    public static MyEnum operator &(MyEnum left, MyEnum right) => DynamicEnum.And(left, right);
    public static MyEnum operator ^(MyEnum left, MyEnum right) => DynamicEnum.Xor(left, right);
    public static MyEnum operator ~(MyEnum value) => DynamicEnum.Neg(value);
}
```

## Using it
Dynamic enums work very similarly to regular enums in how you use them, and each operation us optimized through an internal cache. Here are some examples of interactions with the dynamic enum we created above.

```cs
// getting the underlying value
BigInteger val1 = DynamicEnum.GetValue(MyEnum.FlagTwo); // using GetValue
BigInteger val2 = (BigInteger) MyEnum.FlagTwo; // using our operator overloads

// creating a combined flag
MyEnum allFlags1 = DynamicEnum.Or(MyEnum.FlagOne, DynamicEnum.Or(MyEnum.FlagTwo, MyEnum.FlagThree)); // using Or
MyEnum allFlags2 = MyEnum.FlagOne | MyEnum.FlagTwo | MyEnum.FlagThree; // using our operator overloads
MyEnum mixedFlags = DynamicEnum.Or(MyEnum.FlagOne, DynamicEnum.GetEnumValue<MyEnum>(17)); // using non-defined values in our combined flags

// querying flag information
bool hasAny = allFlags1.HasAnyFlags(MyEnum.FlagOne | MyEnum.ValueOne); // true
bool hasAll = allFlags1.HasAllFlags(MyEnum.FlagOne | MyEnum.ValueOne); // false

// displaying a dynamic enum value or flag
Console.WriteLine(MyEnum.FlagOne); // "FlagOne"
Console.WriteLine(allFlags1); // "FlagOne | FlagTwo | FlagThree"

// parsing a dynamic enum value
MyEnum parsed1 = DynamicEnum.Parse<MyEnum>("FlagOne");
MyEnum parsed2 = DynamicEnum.Parse<MyEnum>("FlagOne | FlagThree");
```

You can also check out [the tests](https://github.com/Ellpeck/DynamicEnums/tree/main/Tests) for some more complex examples.
